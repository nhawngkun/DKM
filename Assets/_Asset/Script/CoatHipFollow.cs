using UnityEngine;

/// <summary>
/// Xương áo Dante đều gắn vào Chest nên khi hông xoay / nghiêng so với ngực (thế thủ, chạy, đá chân),
/// đoạn áo ngang eo vẫn theo ngực -> hông và đùi lún xuyên qua thân áo.
/// Component này xoay đoạn xương phía trên để khớp áo vùng eo đi theo xương Hips (theo trọng số),
/// chạy trong LateUpdate trước khi MagicaCloth (AfterLateUpdate) đọc pose -> đuôi áo bay quanh vị trí đã sửa.
/// Đặt CÙNG GameObject với Animator.
/// </summary>
[DefaultExecutionOrder(150)]
[RequireComponent(typeof(Animator))]
public class CoatHipFollow : MonoBehaviour
{
    [System.Serializable]
    public class Link
    {
        [Tooltip("Khớp áo cần kéo theo hông (xoay xương cha của nó)")]
        public string Bone;
        [Range(0f, 1f)] public float HipWeight = 0.8f;
    }

    // Xếp từ trên xuống: khớp eo kéo nhẹ, khớp ngang hông (gốc MagicaCloth) kéo mạnh.
    // Chỉ vạt trước (00, 07) và vạt hông (01, 06) - chỗ đùi đẩy vào. Vạt sau (02-05) giữ theo ngực,
    // kéo theo hông thì lúc cúi người chạy lưng áo lún vào eo.
    // Vạt trước gốc Magica đặt cao hơn (_04) để khớp _05 ngang đầu đùi còn được đùi đẩy ra
    public Link[] Links =
    {
        new Link { Bone = "CoatCloth_00_03", HipWeight = 0.15f }, new Link { Bone = "CoatCloth_00_04" },
        new Link { Bone = "CoatCloth_01_01", HipWeight = 0.35f }, new Link { Bone = "CoatCloth_01_02" },
        new Link { Bone = "CoatCloth_06_01", HipWeight = 0.35f }, new Link { Bone = "CoatCloth_06_02" },
        new Link { Bone = "CoatCloth_07_03", HipWeight = 0.15f }, new Link { Bone = "CoatCloth_07_04" },
    };

    private struct Bound
    {
        public Transform Target, Parent;
        public Quaternion ParentInitLocalRot;
        public Vector3 HipLocalPos;   // Vị trí khớp trong không gian Hips ở pose gốc
        public float Weight;
    }

    private Bound[] _bound;
    private Transform _hips;

    // Pose lúc khởi tạo (trước khi Animator chạy) là pose gốc của áo so với hông
    private void Awake()
    {
        _hips = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        var all = GetComponentsInChildren<Transform>(true);
        var list = new System.Collections.Generic.List<Bound>();
        foreach (var link in Links)
        {
            Transform t = System.Array.Find(all, x => x.name == link.Bone);
            if (t == null || t.parent == null || _hips == null)
            {
                Debug.LogWarning($"[CoatHipFollow] Không tìm thấy xương {link.Bone}", this);
                continue;
            }
            list.Add(new Bound
            {
                Target = t, Parent = t.parent, ParentInitLocalRot = t.parent.localRotation,
                HipLocalPos = _hips.InverseTransformPoint(t.position), Weight = link.HipWeight,
            });
        }
        _bound = list.ToArray();
    }

    // Animator không điều khiển xương áo -> trả về pose gốc mỗi frame, tránh cộng dồn xoay
    private void Update()
    {
        foreach (var b in _bound) b.Parent.localRotation = b.ParentInitLocalRot;
    }

    private void LateUpdate()
    {
        foreach (var b in _bound)
        {
            Vector3 goal = Vector3.Lerp(b.Target.position, _hips.TransformPoint(b.HipLocalPos), b.Weight);
            Vector3 from = b.Target.position - b.Parent.position;
            Vector3 to = goal - b.Parent.position;
            if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f)
                b.Parent.rotation = Quaternion.FromToRotation(from, to) * b.Parent.rotation;
        }
    }
}
