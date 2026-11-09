using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đòn đánh tay không bắn phép (Trish): mỗi mốc HitTimes của đòn (CharacterCombo.OnAttackImpact) bắn 1 quả cầu phép
/// từ bàn tay đang vung ra trước về phía mục tiêu. Chỉ chạy khi vũ khí hiện tại là WeaponType.Magic, nên đặt chung
/// prefab với MeleeHitDetector cũng không đụng nhau (vũ khí phép không có model -> MeleeHitDetector không quét).
/// - Đòn thường: đạn bay thẳng, hơi dò theo mục tiêu, trúng 1 mục tiêu.
/// - Đòn chốt: đạn to hơn, nổ lan khi chạm.
/// - Skill (buff): vòng nổ quanh người, không bắn đạn.
/// Rung camera / hitstop chỉ khi phép trúng thật, giống kiếm.
/// </summary>
public class SpellCaster : MonoBehaviour
{
    public CharacterCombo characterCombo;
    public ComboCameraFeedback cameraFeedback;
    public CombatEffects effects;

    [Header("Ngắm")]
    [Tooltip("Tìm mục tiêu trong bán kính này khi không lock-on (m)")]
    public float AimRadius = 18f;
    [Tooltip("Góc tối đa so với hướng nhân vật để tự ngắm (độ)")]
    public float AimAngle = 50f;
    [Tooltip("Ngắm vào điểm cao bấy nhiêu so với gốc mục tiêu (m)")]
    public float AimHeight = 1f;

    [Header("Đạn phép")]
    public float Speed = 28f;
    [Tooltip("Độ dò mục tiêu (độ/giây). 0 = bay thẳng")]
    public float HomingTurnRate = 240f;
    public float Radius = 0.3f;
    public float Lifetime = 1.2f;
    [Tooltip("Hình ảnh quả cầu (tuỳ chọn). Trống = chỉ có vệt sáng")]
    public GameObject ProjectileVisual;
    [Tooltip("Material vệt sáng. Trống = lấy của vệt kiếm trên MeleeHitDetector")]
    public Material TrailMaterial;
    [ColorUsage(true, true)] public Color TrailColor = new Color(0.6f, 0.7f, 4f, 1f);
    public float TrailWidth = 0.25f;
    public float TrailTime = 0.18f;
    public LayerMask HitLayers = ~0;

    [Header("Đòn chốt / Skill")]
    public float FinisherScale = 1.8f;
    [Tooltip("Đòn chốt nổ lan bán kính này khi chạm (m)")]
    public float FinisherBlastRadius = 2.5f;
    [Tooltip("Skill: nổ quanh người bán kính này (m)")]
    public float NovaRadius = 4f;
    public float KnockbackScale = 1.3f;

    public bool LogHits;

    private class Spell
    {
        public Transform Transform;
        public TrailRenderer Trail;
        public GameObject Visual;
        public LockOnTarget Target;
        public Vector3 Velocity;
        public float Age;
        public float Radius;
        public float BlastRadius;
        public ComboHitInfo Info;
        public bool Active;
    }

    private readonly List<Spell> _spells = new List<Spell>();
    private readonly Collider[] _overlaps = new Collider[32];
    private readonly HashSet<HitReaction> _blastHits = new HashSet<HitReaction>();
    private CharController _character;
    private Transform _leftHand;
    private Transform _rightHand;

    #region Vòng đời

    private void Awake()
    {
        if (characterCombo == null) characterCombo = GetComponent<CharacterCombo>();
        if (cameraFeedback == null) cameraFeedback = GetComponent<ComboCameraFeedback>();
        if (effects == null) effects = GetComponent<CombatEffects>();
        _character = GetComponentInParent<CharController>();

        Animator animator = characterCombo.animator;
        _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (TrailMaterial == null)
        {
            MeleeHitDetector melee = GetComponent<MeleeHitDetector>();
            if (melee != null && melee.BladeTrails != null && melee.BladeTrails.Length > 0 && melee.BladeTrails[0] != null)
                TrailMaterial = melee.BladeTrails[0].sharedMaterial;
        }
        if (TrailMaterial == null) Debug.LogWarning("[SpellCaster] Chưa có TrailMaterial, đạn phép sẽ không có vệt sáng.", this);
    }

    private void OnDestroy()
    {
        foreach (Spell spell in _spells)
        {
            if (spell.Transform != null) Destroy(spell.Transform.gameObject);
        }
    }

    private void OnEnable() => characterCombo.OnAttackImpact += OnImpact;

    private void OnDisable()
    {
        characterCombo.OnAttackImpact -= OnImpact;
        foreach (Spell spell in _spells) Despawn(spell);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        foreach (Spell spell in _spells)
        {
            if (spell.Active) Tick(spell, deltaTime);
        }
    }

    #endregion

    #region Ra phép

    private bool IsCasting => characterCombo.CurrentWeapon != null && characterCombo.CurrentWeapon.Type == WeaponType.Magic;

    private void OnImpact(ComboHitInfo info)
    {
        if (!IsCasting) return;

        if (effects != null) effects.PlaySwingSound(effects.KindFor(ComboCameraFeedback.HitStrength(info), false));

        if (info.Context == AttackContext.Skill)
        {
            Nova(info);
            return;
        }

        Vector3 origin = CastHand().position;
        LockOnTarget target = FindTarget();
        Vector3 aim = target != null ? AimPoint(target) - origin : _character != null ? _character.Motor.CharacterForward : transform.forward;
        Spawn(origin, aim.normalized, target, info);
    }

    // tay đang vung ra trước nhất là tay ra phép
    private Transform CastHand()
    {
        if (_leftHand == null) return _rightHand != null ? _rightHand : transform;
        if (_rightHand == null) return _leftHand;
        Vector3 forward = transform.forward;
        return Vector3.Dot(_leftHand.position - transform.position, forward) > Vector3.Dot(_rightHand.position - transform.position, forward) ? _leftHand : _rightHand;
    }

    private LockOnTarget FindTarget()
    {
        if (_character != null && _character.IsLockedOn) return _character.CurrentLockTarget;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        LockOnTarget best = null;
        float bestScore = float.MaxValue;
        foreach (LockOnTarget target in LockOnTarget.All)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            float distance = toTarget.magnitude;
            float angle = Vector3.Angle(forward, toTarget);
            if (distance > AimRadius || angle > AimAngle) continue;

            float score = distance * (1f + angle / 90f);
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        return best;
    }

    private Vector3 AimPoint(LockOnTarget target) => target.transform.position + Vector3.up * AimHeight;

    private void Nova(ComboHitInfo info)
    {
        Vector3 center = transform.position + Vector3.up * AimHeight;
        if (effects != null) effects.PlaySlam(transform.position);
        if (cameraFeedback != null) cameraFeedback.PlaySlam();
        Blast(center, NovaRadius, info, null);
    }

    #endregion

    #region Đạn

    private void Spawn(Vector3 origin, Vector3 direction, LockOnTarget target, ComboHitInfo info)
    {
        Spell spell = _spells.Find(s => !s.Active) ?? Create();
        float scale = info.IsFinalHit ? FinisherScale : 1f;

        spell.Transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        spell.Transform.localScale = Vector3.one * scale;
        spell.Target = target;
        spell.Velocity = direction * Speed;
        spell.Age = 0f;
        spell.Radius = Radius * scale;
        spell.BlastRadius = info.IsFinalHit ? FinisherBlastRadius : 0f;
        spell.Info = info;
        spell.Active = true;
        spell.Trail.widthMultiplier = TrailWidth * scale;
        spell.Trail.Clear();
        spell.Trail.emitting = true;
        spell.Transform.gameObject.SetActive(true);
    }

    private Spell Create()
    {
        var go = new GameObject("Spell");
        // không làm con nhân vật: đạn bay độc lập khi nhân vật xoay / di chuyển
        go.transform.SetParent(null, false);
        go.SetActive(false);

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = TrailMaterial;
        trail.time = TrailTime;
        trail.minVertexDistance = 0.05f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.startColor = TrailColor;
        trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        var spell = new Spell { Transform = go.transform, Trail = trail };
        if (ProjectileVisual != null) spell.Visual = Instantiate(ProjectileVisual, go.transform, false);
        _spells.Add(spell);
        return spell;
    }

    private void Tick(Spell spell, float deltaTime)
    {
        spell.Age += deltaTime;
        if (spell.Age >= Lifetime)
        {
            Despawn(spell);
            return;
        }

        if (spell.Target != null && spell.Target.isActiveAndEnabled && HomingTurnRate > 0f)
        {
            Vector3 desired = (AimPoint(spell.Target) - spell.Transform.position).normalized * Speed;
            spell.Velocity = Vector3.RotateTowards(spell.Velocity, desired, HomingTurnRate * Mathf.Deg2Rad * deltaTime, 0f);
        }

        Vector3 from = spell.Transform.position;
        Vector3 step = spell.Velocity * deltaTime;
        float distance = step.magnitude;
        if (distance > 0.0001f
            && Physics.SphereCast(from, spell.Radius, step / distance, out RaycastHit hit, distance, HitLayers, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(_character != null ? _character.transform : transform))
        {
            Explode(spell, hit.point, hit.collider);
            return;
        }

        spell.Transform.position = from + step;
        if (spell.Velocity.sqrMagnitude > 0.0001f) spell.Transform.rotation = Quaternion.LookRotation(spell.Velocity);
    }

    private void Explode(Spell spell, Vector3 point, Collider collider)
    {
        HitReaction direct = collider.GetComponentInParent<HitReaction>();
        Vector3 direction = Vector3.ProjectOnPlane(spell.Velocity, Vector3.up).normalized;

        if (spell.BlastRadius > 0f)
        {
            if (effects != null) effects.PlaySlam(point);
            Blast(point, spell.BlastRadius, spell.Info, direct);
        }
        else if (direct != null)
        {
            ApplyHit(direct, point, direction, spell.Info);
            PlayFeedback(spell.Info, point, direction);
        }
        else if (effects != null)
        {
            effects.PlayHit(point, -direction, ComboCameraFeedback.HitStrength(spell.Info) * 0.5f);
        }
        Despawn(spell);
    }

    private void Despawn(Spell spell)
    {
        spell.Active = false;
        spell.Target = null;
        if (spell.Trail != null) spell.Trail.emitting = false;
        if (spell.Transform != null) spell.Transform.gameObject.SetActive(false);
    }

    #endregion

    #region Trúng đòn

    // nổ lan: mỗi mục tiêu trong bán kính trúng 1 lần, bị đẩy ra xa tâm nổ
    private void Blast(Vector3 center, float radius, ComboHitInfo info, HitReaction direct)
    {
        _blastHits.Clear();
        if (direct != null) _blastHits.Add(direct);

        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlaps, HitLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            HitReaction target = _overlaps[i].GetComponentInParent<HitReaction>();
            if (target != null) _blastHits.Add(target);
        }

        foreach (HitReaction target in _blastHits)
        {
            Vector3 away = Vector3.ProjectOnPlane(target.transform.position - center, Vector3.up);
            Vector3 direction = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            ApplyHit(target, target.transform.position + Vector3.up * AimHeight, direction, info);
        }
        if (_blastHits.Count > 0) PlayFeedback(info, center, transform.forward);
    }

    private void ApplyHit(HitReaction target, Vector3 point, Vector3 direction, ComboHitInfo info)
    {
        float strength = ComboCameraFeedback.HitStrength(info);
        target.TakeHit(direction, strength, KnockbackScale);
        if (effects != null) effects.PlayHit(point, -direction, strength);
        if (LogHits) Debug.Log($"[SpellCaster] phép trúng {target.name} step={info.HitIndex} final={info.IsFinalHit} strength={strength:F2}", this);
    }

    private void PlayFeedback(ComboHitInfo info, Vector3 point, Vector3 direction)
    {
        if (cameraFeedback == null) return;
        Vector3 caster = new Vector3(transform.position.x, point.y, transform.position.z);
        cameraFeedback.PlayHit(info, Vector3.Lerp(caster, point, 0.5f), direction);
    }

    #endregion
}
