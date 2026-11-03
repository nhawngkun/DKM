using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

/// <summary>
/// Chốt chặn cuối cho đuôi áo Dante: sau khi MagicaCloth ghi xương xong (MagicaManager.OnPostSimulation),
/// đẩy từng khớp và điểm giữa từng đoạn xương áo ra khỏi capsule chân / hông bằng cách xoay xương cha.
/// Magica tự trả xương về pose gốc đầu frame sau nên không cộng dồn. Chỉ sửa phần hiển thị,
/// không ảnh hưởng mô phỏng của Magica -> đảm bảo áo không bao giờ lún vào chân dù Magica va chạm thiếu.
/// Đặt CÙNG GameObject với Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CoatBodyCollision : MonoBehaviour
{
    [Tooltip("Xương gốc của MagicaCloth (cố định); các xương con của chúng được đẩy ra")]
    public string[] RootBones =
    {
        "CoatCloth_00_04", "CoatCloth_01_02", "CoatCloth_02_02", "CoatCloth_03_02",
        "CoatCloth_04_02", "CoatCloth_05_02", "CoatCloth_06_02", "CoatCloth_07_04",
    };

    [Header("Bán kính thân (m)")]
    public float HipRadius = 0.11f;
    public float ThighRadius = 0.1f;
    public float ShinRadius = 0.075f;
    [Tooltip("Độ dày áo cộng thêm vào bán kính")]
    public float Thickness = 0.02f;

    private struct Capsule
    {
        public Transform A, B;
        public float Radius;
    }

    private readonly List<Transform> _bones = new List<Transform>();   // Thứ tự cha trước con
    private readonly List<Capsule> _capsules = new List<Capsule>();

    private void Awake()
    {
        var anim = GetComponent<Animator>();
        Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
        void Add(HumanBodyBones a, HumanBodyBones b, float r)
        {
            if (B(a) != null && B(b) != null) _capsules.Add(new Capsule { A = B(a), B = B(b), Radius = r });
        }
        Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HipRadius);
        Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, ThighRadius);
        Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, ThighRadius);
        Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, ShinRadius);
        Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, ShinRadius);

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var name in RootBones)
        {
            Transform root = System.Array.Find(all, t => t.name == name);
            if (root == null) Debug.LogWarning($"[CoatBodyCollision] Không tìm thấy xương {name}", this);
            else foreach (Transform c in root) AddRecursive(c);
        }
    }

    private void AddRecursive(Transform t)
    {
        _bones.Add(t);
        foreach (Transform c in t) AddRecursive(c);
    }

    private void OnEnable() => MagicaManager.OnPostSimulation += PushOut;
    private void OnDisable() => MagicaManager.OnPostSimulation -= PushOut;

    private void PushOut()
    {
        foreach (var bone in _bones)
        {
            Transform parent = bone.parent;
            // Điểm giữa đoạn trước, rồi tới khớp: xoay xương cha sao cho điểm đó ra khỏi thân
            PushSample(parent, Vector3.Lerp(parent.position, bone.position, 0.5f));
            PushSample(parent, bone.position);
        }
    }

    private void PushSample(Transform parent, Vector3 sample)
    {
        Vector3 pushed = sample;
        foreach (var c in _capsules) PushOutCapsule(ref pushed, c.A.position, c.B.position, c.Radius + Thickness);
        if ((pushed - sample).sqrMagnitude < 1e-8f) return;

        Vector3 from = sample - parent.position;
        Vector3 to = pushed - parent.position;
        if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f)
            parent.rotation = Quaternion.FromToRotation(from, to) * parent.rotation;
    }

    private static void PushOutCapsule(ref Vector3 pos, Vector3 a, Vector3 b, float radius)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        Vector3 closest = a + ab * t;
        Vector3 d = pos - closest;
        float dist = d.magnitude;
        if (dist < radius && dist > 1e-5f) pos = closest + d * (radius / dist);
    }
}
