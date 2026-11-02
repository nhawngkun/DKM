using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physic áo choàng / tóc Dante trên các chuỗi xương có sẵn trong model DMC5 (CoatCloth_xx, CoatCoverCloth_xx,
/// WaistCloth, HairCloth...). Anim humanoid không điều khiển các xương này -> tự mô phỏng lò xo (Verlet):
/// quán tính khi chạy/dash, trọng lực, kéo về pose gốc, va chạm capsule chân để vạt áo không xuyên đùi.
/// Đặt CÙNG GameObject với Animator. Chạy sau Animator + PlayerIK + nội suy KCC (LateUpdate order 200).
/// Hitstop (Time.timeScale = 0) thì áo đứng yên theo.
/// </summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Animator))]
public class CoatPhysics : MonoBehaviour
{
    [System.Serializable]
    public class ChainGroup
    {
        [Tooltip("Xương gốc có tên chứa chuỗi này và kết thúc bằng _start (VD: CoatCloth_ -> CoatCloth_00_start..07_start)")]
        public string NameContains = "CoatCloth_";
        public bool Enabled = true;
        [Tooltip("Kéo về pose gốc theo độ sâu chuỗi (0 = gốc, 1 = đuôi)")]
        public AnimationCurve Stiffness = AnimationCurve.Linear(0f, 0.25f, 1f, 0.04f);
        [Range(0f, 1f)] public float Elasticity = 0.08f;   // Giữ hình dạng so với xương cha
        [Range(0f, 1f)] public float Damping = 0.12f;
        [Range(0f, 1f)] public float Inertia = 0.35f;      // 1 = áo đi theo người hoàn toàn, 0 = bị bỏ lại phía sau
        public Vector3 Gravity = new Vector3(0f, -4f, 0f);
        public float Radius = 0.03f;
        public bool Collide = true;
    }

    public List<ChainGroup> Groups = new List<ChainGroup>
    {
        new ChainGroup { NameContains = "CoatCloth_" },
        new ChainGroup { NameContains = "CoatCoverCloth_", Stiffness = AnimationCurve.Linear(0f, 0.4f, 1f, 0.2f) },
        new ChainGroup { NameContains = "WaistCloth", Stiffness = AnimationCurve.Linear(0f, 0.4f, 1f, 0.2f) },
        new ChainGroup { NameContains = "HairCloth", Stiffness = AnimationCurve.Linear(0f, 0.6f, 1f, 0.3f), Gravity = new Vector3(0f, -1f, 0f), Inertia = 0.6f, Radius = 0.01f, Collide = false },
    };

    [Header("Va chạm (capsule theo xương humanoid)")]
    public float HipRadius = 0.08f;
    public float ThighRadius = 0.08f;
    public float ShinRadius = 0.07f;

    [Header("Mô phỏng")]
    public float UpdateRate = 60f;
    public int MaxSubSteps = 3;
    [Tooltip("Nhân vật dịch chuyển xa hơn mức này trong 1 frame (teleport) -> reset áo về pose gốc")]
    public float TeleportDistance = 3f;

    public bool DrawGizmos = true;

    private class Particle
    {
        public Transform Transform;
        public int Parent;
        public int Child = -1;        // Xương con duy nhất dùng để xoay xương này (-1 = không xoay)
        public ChainGroup Group;
        public float Depth;           // 0..1 dọc theo chuỗi
        public Vector3 InitLocalPos;
        public Quaternion InitLocalRot;
        public Vector3 AnimPos;       // Vị trí theo pose gốc frame này
        public Vector3 Pos, PrevPos;
        public float Length;
    }

    private struct Capsule
    {
        public Transform A, B;
        public float Radius;
    }

    private readonly List<Particle> _particles = new List<Particle>();
    private readonly List<Capsule> _capsules = new List<Capsule>();
    private Vector3 _lastRootPos;
    private float _accumulator;

    private void Awake()
    {
        var anim = GetComponent<Animator>();
        BuildChains();
        if (anim.isHuman) BuildCapsules(anim);
    }

    private void OnEnable() => ResetParticles();

    #region Build
    private void BuildChains()
    {
        _particles.Clear();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.EndsWith("_start")) continue;
            foreach (var g in Groups)
            {
                if (!g.Enabled || !t.name.Contains(g.NameContains)) continue;
                int maxDepth = MaxDepth(t, 0);
                AddParticle(t, -1, g, 0, Mathf.Max(1, maxDepth));
                break;
            }
        }
    }

    private static int MaxDepth(Transform t, int d)
    {
        int max = d;
        foreach (Transform c in t) max = Mathf.Max(max, MaxDepth(c, d + 1));
        return max;
    }

    private void AddParticle(Transform t, int parent, ChainGroup g, int depth, int maxDepth)
    {
        var p = new Particle
        {
            Transform = t, Parent = parent, Group = g, Depth = (float)depth / maxDepth,
            InitLocalPos = t.localPosition, InitLocalRot = t.localRotation,
        };
        int index = _particles.Count;
        _particles.Add(p);
        if (parent >= 0)
        {
            var pp = _particles[parent];
            p.Length = Vector3.Distance(pp.Transform.position, t.position);
            // Xương có nhiều con (VD CoatCloth_00_00 -> chuỗi áo + cổ áo): xoay theo con đầu tiên
            if (pp.Child < 0 && p.Length > 0.001f) pp.Child = index;
        }
        foreach (Transform c in t) AddParticle(c, index, g, depth + 1, maxDepth);
    }

    private void BuildCapsules(Animator anim)
    {
        _capsules.Clear();
        void Add(HumanBodyBones a, HumanBodyBones b, float r)
        {
            var ta = anim.GetBoneTransform(a);
            var tb = anim.GetBoneTransform(b);
            if (ta != null && tb != null) _capsules.Add(new Capsule { A = ta, B = tb, Radius = r });
        }
        Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HipRadius);
        Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, ThighRadius);
        Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, ThighRadius);
        Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, ShinRadius);
        Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, ShinRadius);
    }
    #endregion

    #region Simulate
    // Trả xương về pose gốc trước Animator, tránh cộng dồn xoay của frame trước
    private void Update()
    {
        foreach (var p in _particles)
        {
            p.Transform.localPosition = p.InitLocalPos;
            p.Transform.localRotation = p.InitLocalRot;
        }
    }

    private void LateUpdate()
    {
        if (_particles.Count == 0) return;

        foreach (var p in _particles) p.AnimPos = p.Transform.position;

        Vector3 rootMove = transform.position - _lastRootPos;
        _lastRootPos = transform.position;
        if (rootMove.sqrMagnitude > TeleportDistance * TeleportDistance)
        {
            ResetParticles();
            return;
        }

        float step = 1f / UpdateRate;
        _accumulator = Mathf.Min(_accumulator + Time.deltaTime, step * MaxSubSteps);
        int steps = Mathf.FloorToInt(_accumulator / step);
        _accumulator -= steps * step;

        Vector3 movePerStep = steps > 0 ? rootMove / steps : Vector3.zero;
        for (int s = 0; s < steps; s++) Step(step, movePerStep);

        if (steps == 0)
        {
            // Không đủ thời gian cho 1 bước (hitstop / FPS cao): vẫn kéo theo người để áo không bị bỏ lại
            foreach (var p in _particles)
                if (p.Parent >= 0) { p.Pos += rootMove; p.PrevPos += rootMove; }
        }

        Apply();
    }

    private void Step(float dt, Vector3 rootMove)
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            if (p.Parent < 0)
            {
                p.PrevPos = p.Pos;
                p.Pos = p.AnimPos;
                continue;
            }

            var g = p.Group;
            var pp = _particles[p.Parent];

            Vector3 velocity = (p.Pos - p.PrevPos) * (1f - g.Damping);
            p.PrevPos = p.Pos + rootMove * g.Inertia;
            p.Pos += velocity + rootMove * g.Inertia + g.Gravity * (dt * dt);

            // Kéo về pose gốc + giữ hình dạng so với xương cha
            p.Pos += (p.AnimPos - p.Pos) * g.Stiffness.Evaluate(p.Depth);
            Vector3 restPos = pp.Pos + (p.AnimPos - pp.AnimPos);
            p.Pos += (restPos - p.Pos) * g.Elasticity;

            if (g.Collide)
                foreach (var c in _capsules) PushOutCapsule(ref p.Pos, c.A.position, c.B.position, c.Radius + g.Radius);

            // Giữ chiều dài xương
            Vector3 dir = p.Pos - pp.Pos;
            float len = dir.magnitude;
            p.Pos = len > 1e-5f ? pp.Pos + dir * (p.Length / len) : pp.Pos + (p.AnimPos - pp.AnimPos);
        }
    }

    private void Apply()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            if (p.Parent >= 0) p.Transform.position = p.Pos;
            if (p.Child < 0) continue;

            // Hướng xương con theo rotation hiện tại (đã gồm phần xoay của xương cha vừa áp)
            var c = _particles[p.Child];
            Vector3 animDir = p.Transform.TransformVector(c.Transform.localPosition);
            Vector3 simDir = c.Pos - p.Pos;
            p.Transform.rotation = Quaternion.FromToRotation(animDir, simDir) * p.Transform.rotation;
        }
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

    public void ResetParticles()
    {
        foreach (var p in _particles)
        {
            p.Transform.localPosition = p.InitLocalPos;
            p.Transform.localRotation = p.InitLocalRot;
            p.Pos = p.PrevPos = p.Transform.position;
        }
        _lastRootPos = transform.position;
        _accumulator = 0f;
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (!DrawGizmos || !Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        foreach (var c in _capsules)
        {
            Gizmos.DrawWireSphere(c.A.position, c.Radius);
            Gizmos.DrawWireSphere(c.B.position, c.Radius);
            Gizmos.DrawLine(c.A.position, c.B.position);
        }
        Gizmos.color = Color.cyan;
        foreach (var p in _particles)
            if (p.Parent >= 0) Gizmos.DrawLine(_particles[p.Parent].Pos, p.Pos);
    }
}
