using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quét lưỡi kiếm thật trong cửa sổ quanh mỗi mốc HitTimes của đòn, nên chỉ khi kiếm chạm mục tiêu
/// (hoặc đập xuống đất) mới có hitstop, rung, tia lửa. Đặt cùng GameObject với CharacterCombo.
/// Chạy sau WeaponHolder để đọc đúng vị trí kiếm của frame này.
/// </summary>
[DefaultExecutionOrder(110)]
public class MeleeHitDetector : MonoBehaviour
{
    public CharacterCombo characterCombo;
    public WeaponHolder weaponHolder;
    public ComboCameraFeedback cameraFeedback;
    public CombatEffects effects;
    [Tooltip("Vệt chém, script đặt chúng ở mũi kiếm và chỉ bật trong cửa sổ chém")]
    public TrailRenderer[] BladeTrails;

    [Header("Hiệu ứng vung")]
    [Tooltip("Tâm cung chém nằm trên đường thẳng đứng qua nhân vật, ở độ cao này (m)")]
    public float SwingPivotHeight = 1.2f;
    [Tooltip("Bán kính ngoài của cung trong prefab slash khi scale = 1 (m)")]
    public float SlashMeshRadius = 2.5f;
    public float MinSlashScale = 0.5f;
    public float MaxSlashScale = 1.1f;
    public float ThrustScale = 0.8f;
    [Range(0f, 1f), Tooltip("Mũi kiếm đi dọc theo lưỡi tới mức này thì tính là đâm, không phải chém")]
    public float ThrustAlignment = 0.75f;

    [Header("Quét lưỡi kiếm")]
    public float BladeRadius = 0.12f;
    [Tooltip("Mở cửa sổ chém trước mốc HitTimes bấy nhiêu giây clip")]
    public float WindowBefore = 0.12f;
    [Tooltip("Đóng cửa sổ chém sau mốc HitTimes bấy nhiêu giây clip")]
    public float WindowAfter = 0.08f;
    [Tooltip("Kiếm đi xa mỗi frame khi vung nhanh: nội suy bấy nhiêu bước để không xuyên qua mục tiêu")]
    public int SweepSteps = 4;
    public LayerMask HitLayers = ~0;

    [Header("Quét chân khi đá")]
    public float KickRadius = 0.2f;
    [Tooltip("Mở cửa sổ đá trước mốc KickTimes bấy nhiêu giây clip")]
    public float KickWindowBefore = 0.1f;
    [Tooltip("Đóng cửa sổ đá sau mốc KickTimes bấy nhiêu giây clip")]
    public float KickWindowAfter = 0.1f;
    [Tooltip("Đá đẩy mục tiêu lùi xa gấp bấy nhiêu lần chém")]
    public float KickKnockbackScale = 1.5f;

    [Header("Quét nắm tay (vũ khí Fist)")]
    public float FistRadius = 0.15f;
    [Tooltip("Nắm tay nhô ra trước cổ tay bấy nhiêu m")]
    public float FistLength = 0.1f;

    [Header("Tầm lưỡi kiếm (để CharacterCombat chọn khoảng dừng)")]
    [Tooltip("Lưỡi kiếm lệch sang ngang quá bấy nhiêu m so với hướng nhìn thì không tính vào tầm")]
    public float ReachHalfWidth = 0.5f;
    [Tooltip("Lưỡi kiếm cao hơn chân bấy nhiêu m thì không tính vào tầm (quá đầu mục tiêu)")]
    public float ReachMaxHeight = 2.2f;

    public bool LogHits;

    private readonly Dictionary<AttackStep, float> _reach = new Dictionary<AttackStep, float>();

    private readonly Collider[] _overlaps = new Collider[16];
    private readonly HashSet<HitReaction> _hitThisWindow = new HashSet<HitReaction>();
    private readonly HashSet<HitReaction> _hitThisKick = new HashSet<HitReaction>();
    private Transform[] _kneeBones;
    private Transform[] _footBones;
    private readonly Vector3[] _previousKnees = new Vector3[2];
    private readonly Vector3[] _previousFeet = new Vector3[2];
    private Transform[] _elbowBones;
    private Transform[] _handBones;
    private readonly Vector3[] _previousElbows = new Vector3[2];
    private readonly Vector3[] _previousFists = new Vector3[2];
    private readonly Vector3[] _elbows = new Vector3[2];
    private readonly Vector3[] _fists = new Vector3[2];
    private bool _hasPreviousFists;
    private int _kickIndex = -1;
    // mốc (đấm hoặc đá) đã nhận đòn chốt trong bước này: mốc chạm sau không thành đòn chốt lần 2
    private int _finalSource = -1;
    private bool _kickFeedbackPlayed;
    private CharController _character;
    private AttackStep _step;
    private int _windowIndex = -1;
    private bool _feedbackPlayed;
    private bool _slamPlayed;
    private int _swingEffectIndex = -1;

    private GameObject _bladeOwner;
    private Vector3 _localBase;
    private Vector3 _localTip;
    private bool _hasPrevious;
    private Vector3 _previousBase;
    private Vector3 _previousTip;

    private void Awake()
    {
        if (characterCombo == null) characterCombo = GetComponent<CharacterCombo>();
        if (weaponHolder == null) weaponHolder = GetComponent<WeaponHolder>();
        if (cameraFeedback == null) cameraFeedback = GetComponent<ComboCameraFeedback>();
        if (effects == null) effects = GetComponent<CombatEffects>();
        _character = GetComponentInParent<CharController>();

        Animator animator = characterCombo.animator;
        _kneeBones = new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
        _footBones = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
        _elbowBones = new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.RightLowerArm) };
        _handBones = new[] { animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.RightHand) };
    }

    private bool IsFist => characterCombo.CurrentWeapon != null && characterCombo.CurrentWeapon.Type == WeaponType.Fist;

    private void LateUpdate()
    {
        if (IsFist)
        {
            UpdateFists();
            return;
        }
        _hasPreviousFists = false;

        if (!TryGetBlade(out Vector3 bladeBase, out Vector3 bladeTip))
        {
            _hasPrevious = false;
            return;
        }

        AttackStep step = characterCombo.CurrentStep;
        if (step != _step)
        {
            _step = step;
            _windowIndex = -1;
            _slamPlayed = false;
            _swingEffectIndex = -1;
            _kickIndex = -1;
            _finalSource = -1;
        }

        int window = characterCombo.ActiveHitIndex(WindowBefore, WindowAfter);
        if (window != _windowIndex)
        {
            _windowIndex = window;
            _hitThisWindow.Clear();
            _feedbackPlayed = false;
            if (window >= 0 && effects != null && _hasPrevious) effects.PlaySwingSound(KindOf(window, bladeBase, bladeTip));
        }

        foreach (TrailRenderer trail in BladeTrails)
        {
            if (trail == null) continue;
            trail.transform.position = bladeTip;
            trail.emitting = window >= 0;
        }

        if (_hasPrevious && window >= 0)
        {
            Sweep(_previousBase, _previousTip, bladeBase, bladeTip, BladeRadius, window, false);
            TryPlaySwingEffect(step, window, bladeBase, bladeTip);
        }
        UpdateKick();
        if (step != null) CheckSlam(step, bladeTip);

        _previousBase = bladeBase;
        _previousTip = bladeTip;
        _hasPrevious = true;
    }

    private SwingKind KindOf(int hitIndex, Vector3 bladeBase, Vector3 bladeTip)
    {
        Vector3 motion = bladeTip - _previousTip;
        bool thrust = motion.sqrMagnitude > 0.000001f && Mathf.Abs(Vector3.Dot(motion.normalized, (bladeTip - bladeBase).normalized)) >= ThrustAlignment;
        return effects.KindFor(ComboCameraFeedback.HitStrength(characterCombo.BuildHitInfo(hitIndex)), thrust);
    }

    // phát đúng mốc HitTimes và dựng theo mặt phẳng vung thật của lưỡi kiếm, nên cung luôn khớp động tác
    private void TryPlaySwingEffect(AttackStep step, int hitIndex, Vector3 bladeBase, Vector3 bladeTip)
    {
        if (effects == null || hitIndex == _swingEffectIndex || characterCombo.StepNormalizedTime < step.HitTimes[hitIndex]) return;

        Vector3 motion = bladeTip - _previousTip;
        Vector3 pivot = transform.position + Vector3.up * SwingPivotHeight;
        Vector3 toTip = bladeTip - pivot;
        Vector3 axis = Vector3.Cross(toTip, motion);
        // frame hitstop gần như không có chuyển động để suy ra mặt phẳng, chờ frame sau
        if (axis.sqrMagnitude < 0.0000001f) return;
        _swingEffectIndex = hitIndex;

        SwingKind kind = KindOf(hitIndex, bladeBase, bladeTip);
        if (kind == SwingKind.Thrust)
        {
            // gốc prefab đâm là điểm chạm, vệt sáng kéo dài về phía sau nó
            effects.PlaySwingEffect(kind, bladeTip, Quaternion.LookRotation(bladeTip - bladeBase), ThrustScale);
            return;
        }

        // cung trong mesh quay theo chiều âm quanh trục Y của nó, nên trục Y phải ngược chiều vung
        Vector3 up = -axis.normalized;
        Vector3 radial = Vector3.ProjectOnPlane(toTip, up);
        float radius = radial.magnitude;
        if (radius < 0.01f) return;
        float scale = Mathf.Clamp(radius / SlashMeshRadius, MinSlashScale, MaxSlashScale);
        effects.PlaySwingEffect(kind, bladeTip - radial, Quaternion.LookRotation(radial / radius, up), scale);
    }

    // quyền: chỉ cánh tay đang vung nhanh hơn là tay đấm (tay thủ thế đứng sát mục tiêu không tính)
    private void UpdateFists()
    {
        AttackStep step = characterCombo.CurrentStep;
        if (step != _step)
        {
            _step = step;
            _windowIndex = -1;
            _slamPlayed = false;
            _kickIndex = -1;
            _finalSource = -1;
        }

        Vector3[] elbows = _elbows;
        Vector3[] fists = _fists;
        int striker = 1;
        float fastest = -1f;
        for (int arm = 0; arm < 2; arm++)
        {
            if (_elbowBones[arm] == null || _handBones[arm] == null) return;
            elbows[arm] = _elbowBones[arm].position;
            Vector3 hand = _handBones[arm].position;
            fists[arm] = hand + (hand - elbows[arm]).normalized * FistLength;
            float moved = _hasPreviousFists ? (fists[arm] - _previousFists[arm]).sqrMagnitude : 0f;
            if (moved > fastest) { fastest = moved; striker = arm; }
        }

        int window = characterCombo.ActiveHitIndex(WindowBefore, WindowAfter);
        if (window != _windowIndex)
        {
            _windowIndex = window;
            _hitThisWindow.Clear();
            _feedbackPlayed = false;
        }

        foreach (TrailRenderer trail in BladeTrails)
        {
            if (trail == null) continue;
            trail.transform.position = fists[striker];
            trail.emitting = window >= 0;
        }

        if (_hasPreviousFists && window >= 0)
            Sweep(_previousElbows[striker], _previousFists[striker], elbows[striker], fists[striker], FistRadius, window, false);
        UpdateKick();
        if (step != null) CheckSlam(step, fists[striker]);

        for (int arm = 0; arm < 2; arm++)
        {
            _previousElbows[arm] = elbows[arm];
            _previousFists[arm] = fists[arm];
        }
        _hasPreviousFists = true;
    }

    private void UpdateKick()
    {
        int kick = characterCombo.ActiveKickIndex(KickWindowBefore, KickWindowAfter);
        if (kick != _kickIndex)
        {
            _kickIndex = kick;
            _hitThisKick.Clear();
            _kickFeedbackPlayed = false;
            if (kick >= 0 && effects != null) effects.PlayKickSwing();
        }

        for (int leg = 0; leg < _footBones.Length; leg++)
        {
            if (_footBones[leg] == null || _kneeBones[leg] == null) continue;
            // chỉ chân đang nhấc cao mới là chân đá, chân trụ đứng cạnh mục tiêu không tính
            bool kickingLeg = _footBones[leg].position.y >= _footBones[1 - leg].position.y;
            if (kick >= 0 && kickingLeg) Sweep(_previousKnees[leg], _previousFeet[leg], _kneeBones[leg].position, _footBones[leg].position, KickRadius, kick, true);
            _previousKnees[leg] = _kneeBones[leg].position;
            _previousFeet[leg] = _footBones[leg].position;
        }
    }

    private void Sweep(Vector3 previousA, Vector3 previousB, Vector3 currentA, Vector3 currentB, float radius, int hitIndex, bool kick)
    {
        HashSet<HitReaction> alreadyHit = kick ? _hitThisKick : _hitThisWindow;
        int steps = Mathf.Max(SweepSteps, 1);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 a = Vector3.Lerp(previousA, currentA, t);
            Vector3 b = Vector3.Lerp(previousB, currentB, t);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, _overlaps, HitLayers, QueryTriggerInteraction.Ignore);
            for (int c = 0; c < count; c++)
            {
                HitReaction target = _overlaps[c].GetComponentInParent<HitReaction>();
                if (target != null && alreadyHit.Add(target)) ApplyHit(target, _overlaps[c], hitIndex, a, b, kick);
            }
        }
    }

    private void ApplyHit(HitReaction target, Collider collider, int hitIndex, Vector3 a, Vector3 b, bool kick)
    {
        ComboHitInfo info = kick ? characterCombo.BuildKickInfo(hitIndex) : characterCombo.BuildHitInfo(hitIndex);
        if (info.IsFinalHit)
        {
            int source = kick ? 1000 + hitIndex : hitIndex;
            if (_finalSource < 0) _finalSource = source;
            else if (_finalSource != source) info.IsFinalHit = false;
        }
        float strength = ComboCameraFeedback.HitStrength(info);
        Vector3 point = collider.ClosestPoint(ClosestOnSegment(a, b, collider.bounds.center));
        Vector3 attackDirection = Vector3.ProjectOnPlane(point - transform.position, Vector3.up).normalized;

        target.TakeHit(attackDirection, strength, kick ? KickKnockbackScale : 1f, info.Context == AttackContext.Launcher);
        if (effects != null)
        {
            // đấm là va chạm cùn như đá, không dùng tiếng chém của kiếm
            if (kick || IsFist) effects.PlayKickHit(point, -attackDirection, strength);
            else effects.PlayHit(point, -attackDirection, strength);
        }

        bool feedbackPlayed = kick ? _kickFeedbackPlayed : _feedbackPlayed;
        if (kick) _kickFeedbackPlayed = true;
        else _feedbackPlayed = true;
        if (!feedbackPlayed)
        {
            if (cameraFeedback != null)
            {
                // nhìn vào giữa người chém và điểm chạm để thấy cả hai
                Vector3 attacker = new Vector3(transform.position.x, point.y, transform.position.z);
                cameraFeedback.PlayHit(info, Vector3.Lerp(attacker, point, 0.5f), attackDirection);
            }
        }

        if (LogHits) Debug.Log($"[MeleeHitDetector] {(kick ? "đá" : "chém")} trúng {target.name} step={_step?.Name} hit={hitIndex} final={info.IsFinalHit} strength={strength:F2}", this);
    }

    // SlamTime bake sẵn từ anim: đo lúc chạy sẽ báo nhầm vì crossfade và blend cách cầm làm mũi kiếm giật
    private void CheckSlam(AttackStep step, Vector3 bladeTip)
    {
        if (_slamPlayed || step.SlamTime < 0f || _character == null || !_character.IsGrounded) return;

        float normalized = characterCombo.StepNormalizedTime;
        if (normalized < step.SlamTime) return;
        _slamPlayed = true;
        // vào đòn bằng khớp tư thế có thể bỏ qua mốc này, khi đó kiếm không hề đập xuống trên màn hình
        if (normalized > step.SlamTime + 0.1f) return;

        float feet = _character.transform.position.y;
        if (effects != null) effects.PlaySlam(new Vector3(bladeTip.x, feet, bladeTip.z));
        if (cameraFeedback != null) cameraFeedback.PlaySlam();
        if (LogHits) Debug.Log($"[MeleeHitDetector] đập đất step={step.Name}", this);
    }

    /// <summary>
    /// Điểm xa nhất phía trước (m, tính từ gốc nhân vật) mà lưỡi kiếm quét qua trong các cửa sổ chém của đòn,
    /// lấy mẫu từ chính anim của đòn nên đúng với từng skill. Trả về -1 nếu không đo được.
    /// </summary>
    public float BladeReach(AttackStep step)
    {
        if (step == null || step.Clip == null || step.HitTimes.Count == 0) return -1f;
        if (_reach.TryGetValue(step, out float reach)) return reach;
        reach = MeasureReach(step);
        _reach[step] = reach;
        return reach;
    }

    // lấy mẫu anim lên chính bộ xương rồi trả lại: Animator ghi đè tư thế ở lần evaluate kế tiếp trước khi vẽ
    private float MeasureReach(AttackStep step)
    {
        WeaponData weapon = characterCombo.CurrentWeapon;
        bool fist = IsFist;
        if (weapon == null || (!fist && !TryGetBlade(out _, out _))) return -1f;

        Transform model = characterCombo.animator.transform;
        Transform hand = fist ? null : weaponHolder.Socket.parent;
        Transform root = _character != null ? _character.transform : transform;
        if (!fist && hand == null) return -1f;

        Vector3 restPosition = model.localPosition;
        Quaternion restRotation = model.localRotation;
        Matrix4x4 restModel = model.localToWorldMatrix;
        float length = Mathf.Max(step.Length, 0.0001f);
        float best = -1f;

        foreach (float hitTime in step.HitTimes)
        {
            for (int i = 0; i <= 4; i++)
            {
                float normalized = Mathf.Clamp01(hitTime - WindowBefore + (WindowBefore + WindowAfter) * i / 4f);
                float time = Mathf.Min(normalized * length, step.Clip.length);
                step.Clip.SampleAnimation(model.gameObject, time);
                if (fist)
                {
                    for (int arm = 0; arm < 2; arm++)
                    {
                        if (_elbowBones[arm] == null || _handBones[arm] == null) continue;
                        Vector3 wrist = _handBones[arm].position;
                        Vector3 knuckle = wrist + (wrist - _elbowBones[arm].position).normalized * FistLength;
                        best = Mathf.Max(best, ForwardReach(root, restModel, model, _elbowBones[arm].position, knuckle));
                    }
                    continue;
                }
                if (!weapon.TrySampleAttackGrip(step, time, out Vector3 gripPosition, out Quaternion gripRotation))
                {
                    gripPosition = weaponHolder.Socket.localPosition;
                    gripRotation = weaponHolder.Socket.localRotation;
                }

                // anim humanoid có thể dời cả model: đổi về khung model lúc chưa lấy mẫu
                Vector3 bladeBase = root.InverseTransformPoint(restModel.MultiplyPoint3x4(model.InverseTransformPoint(hand.TransformPoint(gripPosition))));
                Vector3 bladeTip = root.InverseTransformPoint(restModel.MultiplyPoint3x4(model.InverseTransformPoint(hand.TransformPoint(gripPosition + gripRotation * _localTip))));
                for (int s = 0; s <= 10; s++)
                {
                    Vector3 point = Vector3.Lerp(bladeBase, bladeTip, s / 10f);
                    if (Mathf.Abs(point.x) <= ReachHalfWidth && point.y >= -0.1f && point.y <= ReachMaxHeight) best = Mathf.Max(best, point.z);
                }
            }
        }

        model.localPosition = restPosition;
        model.localRotation = restRotation;
        return best;
    }

    // điểm xa nhất phía trước trên đoạn a-b (world lúc lấy mẫu), quy về khung nhân vật lúc chưa lấy mẫu
    private float ForwardReach(Transform root, Matrix4x4 restModel, Transform model, Vector3 a, Vector3 b)
    {
        float best = -1f;
        Vector3 localA = root.InverseTransformPoint(restModel.MultiplyPoint3x4(model.InverseTransformPoint(a)));
        Vector3 localB = root.InverseTransformPoint(restModel.MultiplyPoint3x4(model.InverseTransformPoint(b)));
        for (int s = 0; s <= 10; s++)
        {
            Vector3 point = Vector3.Lerp(localA, localB, s / 10f);
            if (Mathf.Abs(point.x) <= ReachHalfWidth && point.y >= -0.1f && point.y <= ReachMaxHeight) best = Mathf.Max(best, point.z);
        }
        return best;
    }

    // mũi kiếm = đầu xa tay cầm theo trục dài nhất của mesh, không cần đặt tay cho từng vũ khí
    private bool TryGetBlade(out Vector3 bladeBase, out Vector3 bladeTip)
    {
        bladeBase = bladeTip = Vector3.zero;
        if (weaponHolder == null || characterCombo == null || weaponHolder.WeaponInstance == null) return false;

        Transform socket = weaponHolder.Socket;
        if (_bladeOwner != weaponHolder.WeaponInstance)
        {
            Renderer renderer = weaponHolder.WeaponInstance.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"[MeleeHitDetector] vũ khí {weaponHolder.WeaponInstance.name} không có Renderer để tính lưỡi kiếm", this);
                return false;
            }
            MeasureBlade(renderer, socket);
            _bladeOwner = weaponHolder.WeaponInstance;
            _hasPrevious = false;
        }

        bladeBase = socket.TransformPoint(_localBase);
        bladeTip = socket.TransformPoint(_localTip);
        return true;
    }

    private void MeasureBlade(Renderer renderer, Transform socket)
    {
        Bounds bounds = renderer.localBounds;
        Vector3 extents = bounds.extents;
        Vector3 axis = extents.x >= extents.y && extents.x >= extents.z ? new Vector3(extents.x, 0f, 0f)
            : extents.y >= extents.z ? new Vector3(0f, extents.y, 0f)
            : new Vector3(0f, 0f, extents.z);

        Matrix4x4 toSocket = socket.worldToLocalMatrix * renderer.localToWorldMatrix;
        Vector3 end1 = toSocket.MultiplyPoint3x4(bounds.center + axis);
        Vector3 end2 = toSocket.MultiplyPoint3x4(bounds.center - axis);
        _localTip = end1.sqrMagnitude >= end2.sqrMagnitude ? end1 : end2;
        _localBase = Vector3.zero;
    }

    private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return a + ab * t;
    }
}
