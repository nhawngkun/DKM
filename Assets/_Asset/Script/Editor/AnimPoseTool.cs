#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool Editor chỉnh anim humanoid theo "muscle" (không cần Blender):
/// copy clip, đổi giá trị trung bình / biên độ của từng cơ, sửa hướng hông, đo góc và chụp ảnh so sánh.
/// Dùng qua script (MCP) hoặc gọi từ code Editor khác.
/// </summary>
public static class AnimPoseTool
{
    /// <summary>
    /// Copy clip nguồn sang clip mới để luôn chỉnh từ bản gốc. Nếu clip đích đã có thì ghi đè NỘI DUNG
    /// (giữ nguyên GUID) - xoá rồi tạo lại sẽ đổi GUID và làm Animator mất tham chiếu tới clip.
    /// </summary>
    public static AnimationClip Clone(string srcPath, string dstPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(srcPath);
        var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);
        if (dst == null)
        {
            AssetDatabase.CopyAsset(srcPath, dstPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);
        }
        string name = dst.name;
        EditorUtility.CopySerialized(src, dst);
        dst.name = name;
        EditorUtility.SetDirty(dst);
        return dst;
    }

    /// <summary>
    /// Chỉnh 1 cơ: giá trị mới = (mean mới) + (giá trị gốc - mean gốc) * amp.
    /// mean = NaN -> giữ mean gốc + offset. Trả về mean gốc (để log).
    /// </summary>
    public static float EditMuscle(AnimationClip clip, string muscle, float mean = float.NaN, float amp = 1f, float offset = 0f)
    {
        var bind = AnimationUtility.GetCurveBindings(clip).FirstOrDefault(b => b.propertyName == muscle);
        if (bind.propertyName == null) return float.NaN;
        var curve = AnimationUtility.GetEditorCurve(clip, bind);
        var keys = curve.keys;
        float origMean = MeanOf(curve, clip.length);
        float newMean = float.IsNaN(mean) ? origMean + offset : mean;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value = Mathf.Clamp(newMean + (keys[i].value - origMean) * amp, -1.2f, 1.2f);
            keys[i].inTangent *= amp;
            keys[i].outTangent *= amp;
        }
        AnimationUtility.SetEditorCurve(clip, bind, new AnimationCurve(keys));
        return origMean;
    }

    /// <summary>Đặt hằng số cho 1 cơ (không dao động).</summary>
    public static void SetConstant(AnimationClip clip, string muscle, float value)
    {
        EditMuscle(clip, muscle, value, 0f);
    }

    public static float GetMean(AnimationClip clip, string muscle)
    {
        var bind = AnimationUtility.GetCurveBindings(clip).FirstOrDefault(b => b.propertyName == muscle);
        if (bind.propertyName == null) return float.NaN;
        return MeanOf(AnimationUtility.GetEditorCurve(clip, bind), clip.length);
    }

    private static float MeanOf(AnimationCurve c, float length)
    {
        if (c.keys.Length == 0) return 0f;
        float s = 0f; const int n = 32;
        for (int i = 0; i < n; i++) s += c.Evaluate(length * i / n);
        return s / n;
    }

    /// <summary>Xoay hướng hông (RootQ) để thân quay thẳng về phía trước (bỏ góc lệch yaw trung bình).</summary>
    public static float FixHipYaw(AnimationClip clip)
    {
        var binds = AnimationUtility.GetCurveBindings(clip);
        var bx = binds.First(b => b.propertyName == "RootQ.x"); var by = binds.First(b => b.propertyName == "RootQ.y");
        var bz = binds.First(b => b.propertyName == "RootQ.z"); var bw = binds.First(b => b.propertyName == "RootQ.w");
        var cx = AnimationUtility.GetEditorCurve(clip, bx); var cy = AnimationUtility.GetEditorCurve(clip, by);
        var cz = AnimationUtility.GetEditorCurve(clip, bz); var cw = AnimationUtility.GetEditorCurve(clip, bw);
        var times = cx.keys.Select(k => k.time).ToArray();
        float yaw = 0f;
        foreach (var t in times) { var f = new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t)) * Vector3.forward; yaw += Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }
        yaw /= Mathf.Max(1, times.Length);
        var fix = Quaternion.Euler(0f, -yaw, 0f);
        var kx = new Keyframe[times.Length]; var ky = new Keyframe[times.Length]; var kz = new Keyframe[times.Length]; var kw = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            float t = times[i];
            var q = fix * new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));
            kx[i] = new Keyframe(t, q.x); ky[i] = new Keyframe(t, q.y); kz[i] = new Keyframe(t, q.z); kw[i] = new Keyframe(t, q.w);
        }
        AnimationUtility.SetEditorCurve(clip, bx, Smooth(kx)); AnimationUtility.SetEditorCurve(clip, by, Smooth(ky));
        AnimationUtility.SetEditorCurve(clip, bz, Smooth(kz)); AnimationUtility.SetEditorCurve(clip, bw, Smooth(kw));
        return yaw;
    }

    /// <summary>Ngả hông (RootQ) quanh trục ngang của nhân vật: + = ngả ra sau (giảm đổ người tới trước).</summary>
    public static void AdjustHipPitch(AnimationClip clip, float degreesBack)
    {
        var binds = AnimationUtility.GetCurveBindings(clip);
        var bx = binds.First(b => b.propertyName == "RootQ.x"); var by = binds.First(b => b.propertyName == "RootQ.y");
        var bz = binds.First(b => b.propertyName == "RootQ.z"); var bw = binds.First(b => b.propertyName == "RootQ.w");
        var cx = AnimationUtility.GetEditorCurve(clip, bx); var cy = AnimationUtility.GetEditorCurve(clip, by);
        var cz = AnimationUtility.GetEditorCurve(clip, bz); var cw = AnimationUtility.GetEditorCurve(clip, bw);
        var times = cx.keys.Select(k => k.time).Union(cy.keys.Select(k => k.time)).Union(cw.keys.Select(k => k.time)).OrderBy(t => t).ToArray();
        var fix = Quaternion.AngleAxis(-degreesBack, Vector3.right);
        var kx = new Keyframe[times.Length]; var ky = new Keyframe[times.Length]; var kz = new Keyframe[times.Length]; var kw = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            float t = times[i];
            var q = fix * new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));
            kx[i] = new Keyframe(t, q.x); ky[i] = new Keyframe(t, q.y); kz[i] = new Keyframe(t, q.z); kw[i] = new Keyframe(t, q.w);
        }
        AnimationUtility.SetEditorCurve(clip, bx, Smooth(kx)); AnimationUtility.SetEditorCurve(clip, by, Smooth(ky));
        AnimationUtility.SetEditorCurve(clip, bz, Smooth(kz)); AnimationUtility.SetEditorCurve(clip, bw, Smooth(kw));
    }

    /// <summary>Sample clip nhưng giữ nguyên gốc model (bỏ root motion) để đo/chụp đúng tư thế.</summary>
    private static void SampleInPlace(GameObject model, AnimationClip clip, float time)
    {
        Vector3 pos = model.transform.position; Quaternion rot = model.transform.rotation;
        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(model, clip, time); AnimationMode.EndSampling();
        model.transform.SetPositionAndRotation(pos, rot);
    }

    private static AnimationCurve Smooth(Keyframe[] k)
    {
        var c = new AnimationCurve(k);
        for (int i = 0; i < k.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return c;
    }

    /// <summary>
    /// Tính lại đường IK goal của 2 bàn chân (LeftFootT/RightFootT) từ đúng pose các cơ của clip trên rig này.
    /// Bắt buộc sau khi sửa cơ chân: Root Y "Based Upon Feet" và GetIKPosition đều đọc các đường goal,
    /// nếu để goal cũ thì chân bị kéo về pose cũ / nhân vật lún hoặc lơ lửng.
    /// Hướng bàn chân (FootQ) và goal tay lấy từ clip nguồn (rotation goal khó tính lại, ít thay đổi).
    /// </summary>
    public static string RebuildFootGoals(GameObject model, AnimationClip clip, AnimationClip source)
    {
        var anim = model.GetComponent<Animator>();
        var handler = new HumanPoseHandler(anim.avatar, model.transform);
        var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot); var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        float hs = anim.humanScale;

        // Đường goal (vị trí / hướng) lấy từ nguồn, sẽ ghi đè phần vị trí chân
        foreach (var b in AnimationUtility.GetCurveBindings(source))
        {
            string p = b.propertyName;
            bool goal = (p.StartsWith("LeftFoot") || p.StartsWith("RightFoot") || p.StartsWith("LeftHand") || p.StartsWith("RightHand")) && (p.Contains("T.") || p.Contains("Q."));
            if (goal) AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(source, b));
        }

        int n = Mathf.Max(2, Mathf.CeilToInt(clip.length * 30f) + 1);
        var curves = new Dictionary<string, AnimationCurve>();
        foreach (var s in new[] { "LeftFootT.x", "LeftFootT.y", "LeftFootT.z", "RightFootT.x", "RightFootT.y", "RightFootT.z" }) curves[s] = new AnimationCurve();
        // Sample không có root motion / không lệ thuộc Root Y: dùng vị trí cổ chân so với body của chính pose đó
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        var tmp = Object.Instantiate(clip);
        var ts = AnimationUtility.GetAnimationClipSettings(tmp); ts.keepOriginalPositionY = true; ts.heightFromFeet = false; AnimationUtility.SetAnimationClipSettings(tmp, ts);
        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i < n; i++)
            {
                float t = clip.length * i / (n - 1);
                SampleInPlace(model, tmp, Mathf.Min(t, clip.length - 1e-4f));
                var hp = new HumanPose(); handler.GetHumanPose(ref hp);
                var inv = Quaternion.Inverse(hp.bodyRotation);
                // Goal chân của Unity nằm ở ĐẾ GIÀY (cổ chân hạ xuống feetBottomHeight), không phải ở cổ chân
                Vector3 lSole = model.transform.InverseTransformPoint(lf.position) - Vector3.up * anim.leftFeetBottomHeight;
                Vector3 rSole = model.transform.InverseTransformPoint(rf.position) - Vector3.up * anim.rightFeetBottomHeight;
                Vector3 l = inv * (lSole / hs - hp.bodyPosition);
                Vector3 r = inv * (rSole / hs - hp.bodyPosition);
                curves["LeftFootT.x"].AddKey(t, l.x); curves["LeftFootT.y"].AddKey(t, l.y); curves["LeftFootT.z"].AddKey(t, l.z);
                curves["RightFootT.x"].AddKey(t, r.x); curves["RightFootT.y"].AddKey(t, r.y); curves["RightFootT.z"].AddKey(t, r.z);
            }
        }
        finally { AnimationMode.StopAnimationMode(); Object.DestroyImmediate(tmp); }
        foreach (var kv in curves)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), kv.Value);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return $"{clip.name}: goal chân {n} khung";
    }

    public static void Save(AnimationClip clip)
    {
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Đo pose (sample nhiều thời điểm): độ đổ người (hông->ngực, + = tới trước), góc mặt (+ = nhìn xuống),
    /// khoảng cách 2 bàn chân, độ cao cổ chân thấp/cao nhất, độ cao tay so với hông.
    /// </summary>
    public static string Measure(GameObject model, AnimationClip clip, int samples = 12)
    {
        var anim = model.GetComponent<Animator>();
        var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        var chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
        var head = anim.GetBoneTransform(HumanBodyBones.Head);
        var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot); var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand); var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
        // trục mặt lấy theo pose gốc trong scene (trước khi sample anim), mặt nhìn thẳng về phía trước
        Vector3 faceLocal = head.InverseTransformDirection(model.transform.forward).normalized;
        float lean = 0, face = 0, feet = 0, footMin = 9, footMax = -9, handMin = 9, handMax = -9;
        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i < samples; i++)
            {
                SampleInPlace(model, clip, clip.length * i / samples);
                Vector3 right = model.transform.right; Vector3 up = Vector3.up;
                Vector3 spine = chest.position - hips.position;
                lean += Vector3.SignedAngle(up, Vector3.ProjectOnPlane(spine, right), right);
                Vector3 f = head.TransformDirection(faceLocal);
                face += Vector3.SignedAngle(Vector3.ProjectOnPlane(f, up), f, right);
                feet += Vector3.Distance(lf.position, rf.position);
                float y0 = model.transform.position.y;
                footMin = Mathf.Min(footMin, lf.position.y - y0, rf.position.y - y0);
                footMax = Mathf.Max(footMax, lf.position.y - y0, rf.position.y - y0);
                handMin = Mathf.Min(handMin, lh.position.y - hips.position.y, rh.position.y - hips.position.y);
                handMax = Mathf.Max(handMax, lh.position.y - hips.position.y, rh.position.y - hips.position.y);
            }
        }
        finally { AnimationMode.StopAnimationMode(); }
        return $"{clip.name}: lean={lean / samples:F1}° face={face / samples:F1}° feetDist={feet / samples:F2}m foot[{footMin:F2},{footMax:F2}] handVsHip[{handMin:F2},{handMax:F2}]";
    }

    /// <summary>Chụp dải ảnh nhiều khung (cols ảnh) của clip, camera xoay camYaw độ quanh nhân vật (0 = chính diện, 90 = ngang).</summary>
    public static string RenderStrip(GameObject model, AnimationClip clip, string path, int frames = 6, float camYaw = 90f, float startNorm = 0f, float endNorm = 1f)
    {
        const int W = 320, H = 480;
        var strip = new Texture2D(W * frames, H, TextureFormat.RGB24, false);
        var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i < frames; i++)
            {
                float tn = frames == 1 ? startNorm : Mathf.Lerp(startNorm, endNorm, (float)i / (frames - 1));
                SampleInPlace(model, clip, clip.length * Mathf.Min(tn, 0.999f));
                var baked = new List<GameObject>();
                foreach (var s in smrs)
                {
                    if (!s.enabled || !s.gameObject.activeInHierarchy) continue;
                    var m = new Mesh(); s.BakeMesh(m, true);
                    var g = new GameObject("__bake") { hideFlags = HideFlags.HideAndDontSave };
                    g.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                    g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials;
                    baked.Add(g); s.enabled = false;
                }
                var camGo = new GameObject("__PoseCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 32f; cam.nearClipPlane = 0.05f; cam.aspect = (float)W / H;
                // Bám theo hông (anim nhảy nâng cả người lên cao so với gốc model)
                Transform t = model.transform;
                var hipsBone = model.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                Vector3 center = new Vector3(t.position.x, hipsBone.position.y, t.position.z);
                camGo.transform.position = center + Quaternion.AngleAxis(camYaw, Vector3.up) * t.forward * 4.4f;
                camGo.transform.LookAt(center);
                var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; strip.ReadPixels(new Rect(0, 0, W, H), W * i, 0); RenderTexture.active = null;
                cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
                foreach (var g in baked) { Object.DestroyImmediate(g.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(g); }
                foreach (var s in smrs) s.enabled = true;
            }
        }
        finally
        {
            foreach (var s in smrs) s.enabled = true;
            AnimationMode.StopAnimationMode();
        }
        strip.Apply();
        File.WriteAllBytes(path, strip.EncodeToPNG());
        Object.DestroyImmediate(strip);
        return path;
    }

    /// <summary>Liệt kê giá trị trung bình của các cơ khớp regex (để xem nhanh 1 clip).</summary>
    public static string DumpMeans(AnimationClip clip, string regex)
    {
        var re = new System.Text.RegularExpressions.Regex(regex);
        var sb = new StringBuilder();
        foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => re.IsMatch(b.propertyName)))
        {
            var c = AnimationUtility.GetEditorCurve(clip, b);
            float min = c.keys.Min(k => k.value), max = c.keys.Max(k => k.value);
            sb.Append($"{b.propertyName}={MeanOf(c, clip.length):F2}[{min:F2},{max:F2}]; ");
        }
        return sb.ToString();
    }
}
#endif
