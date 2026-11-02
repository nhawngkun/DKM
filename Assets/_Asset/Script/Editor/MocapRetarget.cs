#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chuyển dữ liệu khớp 3D (MediaPipe Pose, 33 điểm / khung, đã đổi sang toạ độ Unity) thành AnimationClip humanoid.
/// Mỗi khung: đưa rig về pose gốc (T-pose của avatar), xoay từng xương theo hướng xương thật
/// (giữ mặt phẳng gập của gối / khuỷu tay để không bị vặn khớp), rồi đọc HumanPose (muscle) và ghi curve.
/// CSV: mỗi dòng = thời gian, rồi 33 điểm x,y,z (gốc ở giữa hông, mặt nhân vật hướng +Z).
/// </summary>
public static class MocapRetarget
{
    // Chỉ số điểm MediaPipe
    const int Nose = 0, LEar = 7, REar = 8, LSh = 11, RSh = 12, LEl = 13, REl = 14, LWr = 15, RWr = 16, LIdx = 19, RIdx = 20,
              LHip = 23, RHip = 24, LKnee = 25, RKnee = 26, LAnk = 27, RAnk = 28, LHeel = 29, RHeel = 30, LToe = 31, RToe = 32;

    public enum GroundMode { PinFeet, Air }

    public class Options
    {
        public float StartTime = 0f;
        public float EndTime = float.MaxValue;
        public bool Loop;
        public int LoopBlendFrames = 4;
        /// <summary>Khoảng thời gian (trong CSV) nhân vật ở trên không: không ghim chân xuống đất.</summary>
        public List<Vector2> AirRanges = new List<Vector2>();
        public float HeadWeight = 0.7f;
        public float HandWeight = 0.6f;
    }

    public static List<(float t, Vector3[] p)> LoadCsv(string path)
    {
        var frames = new List<(float, Vector3[])>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var v = line.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var pts = new Vector3[33];
            for (int j = 0; j < 33; j++) pts[j] = new Vector3(v[1 + j * 3], v[2 + j * 3], v[3 + j * 3]);
            frames.Add((v[0], pts));
        }
        return frames;
    }

    public static string Build(GameObject model, string csvPath, string clipPath, Options o)
    {
        var anim = model.GetComponent<Animator>();
        var all = LoadCsv(csvPath).Where(f => f.t >= o.StartTime - 1e-4f && f.t <= o.EndTime + 1e-4f).ToList();
        if (all.Count < 2) return "không đủ khung";

        // Lưu pose hiện tại của toàn bộ xương để trả lại sau
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var saved = transforms.Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
        var rest = anim.avatar.humanDescription.skeleton.ToDictionary(s => s.name, s => s);
        var handler = new HumanPoseHandler(anim.avatar, model.transform);
        var poses = new List<HumanPose>();
        float t0 = all[0].t;
        var bone = new Dictionary<HumanBodyBones, Transform>();
        foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            if (b != HumanBodyBones.LastBone) { var tr = anim.GetBoneTransform(b); if (tr) bone[b] = tr; }

        try
        {
            foreach (var (t, p) in all)
            {
                // 1) Pose gốc của avatar (T-pose)
                foreach (var tr in transforms)
                    if (tr != model.transform && rest.TryGetValue(tr.name, out var sb)) { tr.localPosition = sb.position; tr.localRotation = sb.rotation; }

                PoseFrame(model.transform, bone, p, o);

                var hp = new HumanPose();
                handler.GetHumanPose(ref hp);

                // 2) Ghim chân xuống đất (trừ lúc ở trên không)
                bool air = o.AirRanges.Any(r => t >= r.x && t <= r.y);
                float soleL = bone[HumanBodyBones.LeftFoot].position.y - anim.leftFeetBottomHeight;
                float soleR = bone[HumanBodyBones.RightFoot].position.y - anim.rightFeetBottomHeight;
                float toeL = bone.ContainsKey(HumanBodyBones.LeftToes) ? bone[HumanBodyBones.LeftToes].position.y - 0.02f : soleL;
                float toeR = bone.ContainsKey(HumanBodyBones.RightToes) ? bone[HumanBodyBones.RightToes].position.y - 0.02f : soleR;
                float lowest = Mathf.Min(soleL, soleR, toeL, toeR) - model.transform.position.y;
                if (!air)
                {
                    hp.bodyPosition.y -= lowest / anim.humanScale;
                }
                hp.bodyPosition.x = 0f; hp.bodyPosition.z = 0f;
                poses.Add(hp);
            }
        }
        finally
        {
            foreach (var (t, lp, lr, ls) in saved) { t.localPosition = lp; t.localRotation = lr; t.localScale = ls; }
        }

        // Trên không: giữ hông cao ngang lúc đứng (KCC tự lo phần bay lên)
        if (o.AirRanges.Count > 0)
        {
            var ground = poses.Where((_, i) => !o.AirRanges.Any(r => all[i].t >= r.x && all[i].t <= r.y)).ToList();
            float standY = ground.Count > 0 ? ground.Max(h => h.bodyPosition.y) : poses[0].bodyPosition.y;
            for (int i = 0; i < poses.Count; i++)
                if (o.AirRanges.Any(r => all[i].t >= r.x && all[i].t <= r.y)) { var h = poses[i]; h.bodyPosition.y = standY; poses[i] = h; }
        }

        if (o.Loop) BlendLoop(poses, o.LoopBlendFrames);

        var clip = WriteClip(poses, all.Select(f => f.t - t0).ToArray(), clipPath, o.Loop);
        return $"{clip.name}: {poses.Count} khung, {clip.length:F2}s";
    }

    /// <summary>Xoay rig theo các điểm của 1 khung.</summary>
    static void PoseFrame(Transform root, Dictionary<HumanBodyBones, Transform> b, Vector3[] p, Options o)
    {
        // Đổi điểm sang toạ độ world của model (gốc dữ liệu = giữa hông = vị trí xương hông)
        Vector3 hipW = b[HumanBodyBones.Hips].position;
        Vector3 W(int i) => hipW + root.rotation * p[i];
        Vector3 Mid(int a, int c) => (W(a) + W(c)) * 0.5f;

        Vector3 hipMid = Mid(LHip, RHip), shMid = Mid(LSh, RSh);
        Vector3 bodyRight = (W(RHip) - W(LHip)).normalized;
        Vector3 spineUp = (shMid - hipMid).normalized;

        // --- Hông: khớp hướng phải (đường nối 2 hông) + hướng lên (hông -> vai)
        var hips = b[HumanBodyBones.Hips];
        Vector3 restRight = (b[HumanBodyBones.RightUpperLeg].position - b[HumanBodyBones.LeftUpperLeg].position).normalized;
        Vector3 restUp = (MidT(b[HumanBodyBones.LeftUpperArm], b[HumanBodyBones.RightUpperArm]) - hips.position).normalized;
        hips.rotation = FrameDelta(restRight, restUp, bodyRight, spineUp) * hips.rotation;

        // --- Cột sống: chia đều độ cong cho Spine / Chest / UpperChest để khớp đường vai + hướng ngực
        Vector3 shRight = (W(RSh) - W(LSh)).normalized;
        var spineBones = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }.Where(b.ContainsKey).Select(x => b[x]).ToArray();
        for (int i = 0; i < spineBones.Length; i++)
        {
            Vector3 curRight = (b[HumanBodyBones.RightUpperArm].position - b[HumanBodyBones.LeftUpperArm].position).normalized;
            Vector3 curUp = (MidT(b[HumanBodyBones.LeftUpperArm], b[HumanBodyBones.RightUpperArm]) - hips.position).normalized;
            var delta = FrameDelta(curRight, curUp, shRight, spineUp);
            float w = 1f / (spineBones.Length - i);
            spineBones[i].rotation = Quaternion.Slerp(Quaternion.identity, delta, w) * spineBones[i].rotation;
        }

        // --- Đầu: hướng lên = vai -> giữa tai, hướng nhìn = giữa tai -> mũi
        if (b.ContainsKey(HumanBodyBones.Head) && o.HeadWeight > 0f)
        {
            var head = b[HumanBodyBones.Head];
            Vector3 earMid = Mid(LEar, REar);
            Vector3 headUp = (earMid - shMid).normalized;
            Vector3 headFwd = Vector3.ProjectOnPlane(W(Nose) - earMid, headUp).normalized;
            // trục hiện tại của đầu: lên = hướng cổ, trước = hướng mặt (lấy theo nhân vật ở pose hiện tại)
            Vector3 curUp = (head.position - MidT(b[HumanBodyBones.LeftUpperArm], b[HumanBodyBones.RightUpperArm])).normalized;
            Vector3 curFace = Vector3.ProjectOnPlane(hips.forward, curUp).normalized;
            var delta = Quaternion.LookRotation(headFwd, headUp) * Quaternion.Inverse(Quaternion.LookRotation(curFace, curUp));
            head.rotation = Quaternion.Slerp(Quaternion.identity, delta, o.HeadWeight) * head.rotation;
        }

        // --- Tay: (cánh tay, cẳng tay) giữ mặt phẳng gập khuỷu; bàn tay hướng theo ngón trỏ
        Limb(b[HumanBodyBones.LeftUpperArm], b[HumanBodyBones.LeftLowerArm], b[HumanBodyBones.LeftHand], W(LSh), W(LEl), W(LWr), hips.up);
        Limb(b[HumanBodyBones.RightUpperArm], b[HumanBodyBones.RightLowerArm], b[HumanBodyBones.RightHand], W(RSh), W(REl), W(RWr), -hips.up);
        Aim(b[HumanBodyBones.LeftHand], ChildPos(b, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal), W(LIdx) - W(LWr), o.HandWeight);
        Aim(b[HumanBodyBones.RightHand], ChildPos(b, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal), W(RIdx) - W(RWr), o.HandWeight);

        // --- Chân: (đùi, cẳng chân) giữ mặt phẳng gập gối; bàn chân hướng theo mũi chân
        Limb(b[HumanBodyBones.LeftUpperLeg], b[HumanBodyBones.LeftLowerLeg], b[HumanBodyBones.LeftFoot], W(LHip), W(LKnee), W(LAnk), hips.right);
        Limb(b[HumanBodyBones.RightUpperLeg], b[HumanBodyBones.RightLowerLeg], b[HumanBodyBones.RightFoot], W(RHip), W(RKnee), W(RAnk), hips.right);
        if (b.ContainsKey(HumanBodyBones.LeftToes)) Aim(b[HumanBodyBones.LeftFoot], b[HumanBodyBones.LeftToes].position, W(LToe) - W(LAnk), 1f);
        if (b.ContainsKey(HumanBodyBones.RightToes)) Aim(b[HumanBodyBones.RightFoot], b[HumanBodyBones.RightToes].position, W(RToe) - W(RAnk), 1f);
    }

    /// <summary>
    /// Xoay xương trên + dưới của 1 chi theo 3 điểm (gốc, khớp giữa, đầu chi).
    /// restBendAxis: trục gập tự nhiên của khớp giữa ở pose gốc (gối: sang phải, khuỷu: theo chiều trên/dưới).
    /// </summary>
    static void Limb(Transform upper, Transform lower, Transform end, Vector3 a, Vector3 m, Vector3 c, Vector3 restBendAxis)
    {
        Vector3 dUp = (m - a).normalized, dLow = (c - m).normalized;
        Vector3 bend = Vector3.Cross(dUp, dLow);
        // Chi gần thẳng: dùng trục gập gốc (chiếu lên mặt phẳng vuông góc hướng xương)
        Vector3 targetAxis = bend.magnitude > 0.08f ? bend.normalized : Vector3.ProjectOnPlane(restBendAxis, dUp).normalized;

        Vector3 curUp = (lower.position - upper.position).normalized;
        Vector3 curLow = (end.position - lower.position).normalized;
        Vector3 curBend = Vector3.Cross(curUp, curLow);
        Vector3 curAxis = curBend.magnitude > 0.08f ? curBend.normalized : Vector3.ProjectOnPlane(restBendAxis, curUp).normalized;

        upper.rotation = FrameDeltaDirAxis(curUp, curAxis, dUp, targetAxis) * upper.rotation;
        // Xương dưới: chỉ cần khớp hướng (đã nằm đúng mặt phẳng gập nhờ xương trên)
        Vector3 nowLow = (end.position - lower.position).normalized;
        lower.rotation = Quaternion.FromToRotation(nowLow, dLow) * lower.rotation;
    }

    static void Aim(Transform t, Vector3 childPos, Vector3 targetDir, float weight)
    {
        if (targetDir.sqrMagnitude < 1e-6f) return;
        Vector3 cur = (childPos - t.position).normalized;
        var q = Quaternion.FromToRotation(cur, targetDir.normalized);
        t.rotation = Quaternion.Slerp(Quaternion.identity, q, weight) * t.rotation;
    }

    static Vector3 ChildPos(Dictionary<HumanBodyBones, Transform> b, HumanBodyBones self, HumanBodyBones child)
        => b.ContainsKey(child) ? b[child].position : b[self].position + b[self].forward * 0.08f;

    static Vector3 MidT(Transform a, Transform c) => (a.position + c.position) * 0.5f;

    /// <summary>Phép xoay đưa khung (right, up) hiện tại về khung (right, up) mục tiêu.</summary>
    static Quaternion FrameDelta(Vector3 curRight, Vector3 curUp, Vector3 tgtRight, Vector3 tgtUp)
    {
        Vector3 curFwd = Vector3.Cross(curRight, curUp), tgtFwd = Vector3.Cross(tgtRight, tgtUp);
        return Quaternion.LookRotation(tgtFwd, tgtUp) * Quaternion.Inverse(Quaternion.LookRotation(curFwd, curUp));
    }

    /// <summary>Phép xoay đưa (hướng xương, trục gập) hiện tại về mục tiêu.</summary>
    static Quaternion FrameDeltaDirAxis(Vector3 curDir, Vector3 curAxis, Vector3 tgtDir, Vector3 tgtAxis)
    {
        return Quaternion.LookRotation(tgtDir, tgtAxis) * Quaternion.Inverse(Quaternion.LookRotation(curDir, curAxis));
    }

    /// <summary>Hoà cuối clip vào đầu clip để lặp không giật.</summary>
    static void BlendLoop(List<HumanPose> poses, int n)
    {
        n = Mathf.Min(n, poses.Count / 3);
        int last = poses.Count - 1;
        for (int k = 0; k < n; k++)
        {
            float w = (k + 1f) / (n + 1f); // càng về cuối càng giống khung đầu
            int i = last - n + 1 + k;
            var a = poses[i]; var b = poses[0];
            for (int m = 0; m < a.muscles.Length; m++) a.muscles[m] = Mathf.Lerp(a.muscles[m], b.muscles[m], w);
            a.bodyPosition = Vector3.Lerp(a.bodyPosition, b.bodyPosition, w);
            a.bodyRotation = Quaternion.Slerp(a.bodyRotation, b.bodyRotation, w);
            poses[i] = a;
        }
        // khung cuối = khung đầu
        var e = poses[last]; var s = poses[0];
        e.muscles = (float[])s.muscles.Clone(); e.bodyPosition = s.bodyPosition; e.bodyRotation = s.bodyRotation; poses[last] = e;
    }

    static AnimationClip WriteClip(List<HumanPose> poses, float[] times, string path, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.frameRate = 30f;

        void Set(string prop, System.Func<HumanPose, float> f)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < poses.Count; i++) c.AddKey(new Keyframe(times[i], f(poses[i])));
            for (int i = 0; i < c.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), c);
        }

        // Giữ quaternion liên tục (tránh lật dấu giữa các khung)
        for (int i = 1; i < poses.Count; i++)
        {
            var h = poses[i];
            if (Quaternion.Dot(h.bodyRotation, poses[i - 1].bodyRotation) < 0f)
                h.bodyRotation = new Quaternion(-h.bodyRotation.x, -h.bodyRotation.y, -h.bodyRotation.z, -h.bodyRotation.w);
            poses[i] = h;
        }
        Set("RootT.x", h => h.bodyPosition.x); Set("RootT.y", h => h.bodyPosition.y); Set("RootT.z", h => h.bodyPosition.z);
        Set("RootQ.x", h => h.bodyRotation.x); Set("RootQ.y", h => h.bodyRotation.y); Set("RootQ.z", h => h.bodyRotation.z); Set("RootQ.w", h => h.bodyRotation.w);
        for (int m = 0; m < HumanTrait.MuscleCount; m++)
        {
            int mm = m;
            Set(HumanTrait.MuscleName[m], h => h.muscles[mm]);
        }

        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        s.loopBlendOrientation = true; s.keepOriginalOrientation = true;
        s.loopBlendPositionY = true; s.keepOriginalPositionY = true;
        s.loopBlendPositionXZ = true; s.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return clip;
    }
}
#endif
