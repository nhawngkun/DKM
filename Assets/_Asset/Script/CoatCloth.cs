using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Áo choàng Dante bằng Unity Cloth. Đặt CÙNG GameObject với Animator.
/// Bake: tìm các SkinnedMeshRenderer có đỉnh skin theo xương áo (CoatCloth_xx, CoatCoverCloth_xx, WaistCloth),
/// thêm Cloth và tính maxDistance mỗi đỉnh = tổng (trọng số xương áo × độ sâu xương trong chuỗi) -> gần ngực ghim chặt,
/// gấu áo bay nhiều nhất; đỉnh không dính xương áo bị ghim theo skin. Thêm collider capsule trên chân để áo không xuyên.
/// Đổi thông số rồi chuột phải component -> "Bake Coat Cloth". Cần mesh bật Read/Write.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CoatCloth : MonoBehaviour
{
    [Header("Xương áo")]
    public string[] BonePrefixes = { "CoatCloth_", "CoatCoverCloth_", "WaistCloth" };
    [Tooltip("Xương chứa chuỗi này coi như cố định (cổ áo)")]
    public string[] ExcludeContains = { "Collar" };

    [Header("Độ lắc")]
    public float MaxDistance = 0.4f;
    [Tooltip("Theo độ sâu xương trong chuỗi (0 = gốc ở ngực, 1 = đuôi ở gấu áo)")]
    public AnimationCurve DepthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.02f), new Keyframe(1f, 1f));
    [Tooltip("Bỏ qua đỉnh có maxDistance nhỏ hơn mức này (để ghim hẳn)")]
    public float MinDistance = 0.005f;

    [Header("Collider chân")]
    public float HipRadius = 0.08f;
    public float ThighRadius = 0.08f;
    public float ShinRadius = 0.065f;

    [Header("Cloth")]
    public float StretchingStiffness = 0.95f;
    public float BendingStiffness = 0.3f;
    public float Damping = 0.2f;
    [Tooltip("Ảnh hưởng vận tốc nhân vật (dash 25m/s -> để thấp cho áo khỏi bay quá)")]
    public float WorldVelocityScale = 0.4f;
    public float WorldAccelerationScale = 0.8f;
    public float Friction = 0.3f;
    public float CollisionMassScale = 0.1f;

    private Cloth[] _cloths;

    private void OnEnable()
    {
        _cloths = GetComponentsInChildren<Cloth>(true);
        foreach (var c in _cloths) c.ClearTransformMotion();
    }

#if UNITY_EDITOR
    [ContextMenu("Bake Coat Cloth")]
    public void Bake()
    {
        var anim = GetComponent<Animator>();
        if (!anim.isHuman)
        {
            Debug.LogError("[CoatCloth] Cần Animator humanoid", this);
            return;
        }

        var capsules = BuildCapsules(anim);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) continue;
            if (!mesh.isReadable)
            {
                Debug.LogError($"[CoatCloth] Mesh {mesh.name} chưa bật Read/Write", smr);
                continue;
            }
            BakeRenderer(smr, capsules);
        }
    }

    private void BakeRenderer(SkinnedMeshRenderer smr, CapsuleCollider[] capsules)
    {
        // Độ lắc của từng xương (0 = không phải xương áo)
        var bones = smr.bones;
        var boneFactor = new float[bones.Length];
        bool hasCoatBone = false;
        for (int i = 0; i < bones.Length; i++)
        {
            boneFactor[i] = BoneFactor(bones[i]);
            hasCoatBone |= boneFactor[i] > 0f;
        }

        var mesh = smr.sharedMesh;
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;
        var welded = new Dictionary<Vector3, int>();
        var maxDist = new List<float>();
        int freeCount = 0;
        for (int v = 0; v < vertices.Length && hasCoatBone; v++)
        {
            var w = weights[v];
            float d = MaxDistance * (w.weight0 * boneFactor[w.boneIndex0] + w.weight1 * boneFactor[w.boneIndex1]
                                   + w.weight2 * boneFactor[w.boneIndex2] + w.weight3 * boneFactor[w.boneIndex3]);
            if (d < MinDistance) d = 0f;

            // Cloth gộp các đỉnh trùng vị trí theo thứ tự xuất hiện đầu tiên
            if (welded.TryGetValue(vertices[v], out int idx)) maxDist[idx] = Mathf.Max(maxDist[idx], d);
            else
            {
                welded.Add(vertices[v], maxDist.Count);
                maxDist.Add(d);
            }
        }
        foreach (var d in maxDist) if (d > 0f) freeCount++;

        var cloth = smr.GetComponent<Cloth>();
        if (freeCount == 0)
        {
            if (cloth != null) UnityEditor.Undo.DestroyObjectImmediate(cloth);
            return;
        }

        if (cloth == null) cloth = UnityEditor.Undo.AddComponent<Cloth>(smr.gameObject);
        else UnityEditor.Undo.RecordObject(cloth, "Bake Coat Cloth");

        var coefficients = cloth.coefficients;
        if (coefficients.Length != maxDist.Count)
        {
            Debug.LogError($"[CoatCloth] {smr.name}: số đỉnh Cloth ({coefficients.Length}) khác số đỉnh đã gộp ({maxDist.Count})", smr);
            return;
        }
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i].maxDistance = maxDist[i];
            coefficients[i].collisionSphereDistance = 0f;
        }
        cloth.coefficients = coefficients;

        cloth.stretchingStiffness = StretchingStiffness;
        cloth.bendingStiffness = BendingStiffness;
        cloth.damping = Damping;
        cloth.worldVelocityScale = WorldVelocityScale;
        cloth.worldAccelerationScale = WorldAccelerationScale;
        cloth.friction = Friction;
        cloth.collisionMassScale = CollisionMassScale;
        cloth.useGravity = true;
        cloth.capsuleColliders = capsules;

        UnityEditor.EditorUtility.SetDirty(cloth);
        Debug.Log($"[CoatCloth] {smr.name}: {freeCount}/{coefficients.Length} đỉnh được lắc", smr);
    }

    // Độ sâu xương trong chuỗi áo -> DepthCurve. Gốc chuỗi là xương cha gần nhất không phải xương áo
    private float BoneFactor(Transform bone)
    {
        if (bone == null || !IsCoatBone(bone.name)) return 0f;
        foreach (var ex in ExcludeContains) if (bone.name.Contains(ex)) return 0f;

        int depth = 0;
        Transform root = bone;
        while (root.parent != null && IsCoatBone(root.parent.name))
        {
            root = root.parent;
            depth++;
        }
        int length = Mathf.Max(1, MaxDepth(root));
        return Mathf.Max(0f, DepthCurve.Evaluate(Mathf.Clamp01((float)depth / length)));
    }

    private bool IsCoatBone(string boneName)
    {
        foreach (var p in BonePrefixes) if (boneName.StartsWith(p)) return true;
        return false;
    }

    private int MaxDepth(Transform t)
    {
        int max = 0;
        foreach (Transform c in t)
            if (IsCoatBone(c.name)) max = Mathf.Max(max, MaxDepth(c) + 1);
        return max;
    }

    private CapsuleCollider[] BuildCapsules(Animator anim)
    {
        Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
        Transform lUp = B(HumanBodyBones.LeftUpperLeg), lLow = B(HumanBodyBones.LeftLowerLeg), lFoot = B(HumanBodyBones.LeftFoot);
        Transform rUp = B(HumanBodyBones.RightUpperLeg), rLow = B(HumanBodyBones.RightLowerLeg), rFoot = B(HumanBodyBones.RightFoot);
        return new[]
        {
            BuildCapsule(B(HumanBodyBones.Hips), lUp.position, rUp.position, HipRadius),
            BuildCapsule(lUp, lUp.position, lLow.position, ThighRadius),
            BuildCapsule(rUp, rUp.position, rLow.position, ThighRadius),
            BuildCapsule(lLow, lLow.position, lFoot.position, ShinRadius),
            BuildCapsule(rLow, rLow.position, rFoot.position, ShinRadius),
        };
    }

    // Collider trigger trên layer Ignore Raycast: chỉ để Cloth va chạm (KCC, camera đều bỏ qua trigger)
    private static CapsuleCollider BuildCapsule(Transform bone, Vector3 a, Vector3 b, float radius)
    {
        const string name = "CoatCollider";
        Transform t = bone.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(t.gameObject, "Bake Coat Cloth");
            t.SetParent(bone, false);
        }
        else UnityEditor.Undo.RecordObject(t, "Bake Coat Cloth");

        t.gameObject.layer = 2;
        t.position = (a + b) * 0.5f;
        t.rotation = Quaternion.FromToRotation(Vector3.up, b - a);

        var col = t.GetComponent<CapsuleCollider>();
        if (col == null) col = UnityEditor.Undo.AddComponent<CapsuleCollider>(t.gameObject);
        else UnityEditor.Undo.RecordObject(col, "Bake Coat Cloth");
        float scale = Mathf.Max(t.lossyScale.x, 0.0001f);
        col.isTrigger = true;
        col.direction = 1;
        col.center = Vector3.zero;
        col.radius = radius / scale;
        col.height = (Vector3.Distance(a, b) + radius * 2f) / scale;
        return col;
    }
#endif
}
