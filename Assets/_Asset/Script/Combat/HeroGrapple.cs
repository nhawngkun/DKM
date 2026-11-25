using UnityEngine;

// Túm - ném (D-016): bấm nút cơ chế riêng (B / Q) khi có địch sát trước mặt -> chuỗi Skill ChainName (túm -> ném).
// Lúc túm kéo địch về chỗ giữ trước mặt; tới mốc đánh cuối của đòn ném thì thả ra, hất văng theo hướng nhân vật.
[RequireComponent(typeof(CharController))]
public class HeroGrapple : MonoBehaviour
{
    [Tooltip("Tên chuỗi Skill trong WeaponData: bước 1 = túm, bước 2 = ném")]
    public string ChainName = "Tum Nem";
    public float GrabRange = 1.8f;
    public float GrabAngle = 70f;
    [Tooltip("Chỗ giữ địch so với nhân vật (x phải, y lên, z trước)")]
    public Vector3 HoldOffset = new Vector3(0f, 0.25f, 0.75f);
    public float HoldSharpness = 18f;
    [Range(0f, 1f)] public float ThrowStrength = 1f;
    [Tooltip("Quãng ném văng ngang (m)")]
    public float ThrowDistance = 3.5f;
    public bool ThrowLaunch = true;

    private CharController _character;
    private HitReaction _victim;
    private bool _throwQueued;
    private float _startDeadline;
    private const float StartTimeout = 0.2f;

    public bool IsGrabbing => _victim != null;

    private void Awake()
    {
        _character = GetComponent<CharController>();
        _character.OnSpecial += TryGrab;
    }

    private void OnDestroy()
    {
        if (_character != null) _character.OnSpecial -= TryGrab;
    }

    private void TryGrab()
    {
        CharacterCombo combo = _character.characterCombo;
        if (_victim != null || combo == null || !_character.IsGrounded) return;
        if (combo.IsAttacking && !combo.IsInRecovery) return;

        int slot = SkillSlotOf(combo.CurrentWeapon);
        HitReaction target = FindVictim();
        if (slot < 0 || target == null) return;

        // lệnh đánh vào hàng đợi, đòn túm bắt đầu ở Update kế tiếp -> giữ mục tiêu chờ tối đa StartTimeout
        combo.OnAttackInput(AttackContext.Skill, slot);
        _victim = target;
        _throwQueued = false;
        _startDeadline = Time.time + StartTimeout;
    }

    private void LateUpdate()
    {
        if (_victim == null) return;

        CharacterCombo combo = _character.characterCombo;
        bool grabbing = combo.CurrentChainName == ChainName && combo.CurrentStep != null;
        if (!grabbing && Time.time < _startDeadline) return;
        if (!_victim.isActiveAndEnabled || !grabbing)
        {
            _victim = null;
            return;
        }
        _startDeadline = 0f;

        int stepIndex = StepIndex(combo);
        // bước túm: nhấn hộ lần 2 để đòn ném nối liền khi tới mốc nối
        if (stepIndex == 0 && !_throwQueued && combo.StepNormalizedTime >= combo.CurrentStep.LinkTime * 0.5f)
        {
            _throwQueued = true;
            combo.OnAttackInput(AttackContext.Skill, SkillSlotOf(combo.CurrentWeapon));
        }

        Transform self = _character.transform;
        Vector3 hold = self.TransformPoint(HoldOffset);
        Transform victim = _victim.transform;
        victim.position = Vector3.Lerp(victim.position, hold, 1f - Mathf.Exp(-HoldSharpness * Time.deltaTime));
        victim.rotation = Quaternion.LookRotation(-Vector3.ProjectOnPlane(self.forward, Vector3.up), Vector3.up);

        // mốc cuối của đòn ném = lúc vung tay ném (mốc đầu thường là lúc mới nhấc người lên)
        var hits = combo.CurrentStep.HitTimes;
        float release = hits.Count > 0 ? hits[hits.Count - 1] : 0.5f;
        if (stepIndex >= 1 && combo.StepNormalizedTime >= release)
        {
            // HitReaction đẩy MaxKnockback * scale (bị hất lên thì nhân thêm AirKnockbackScale) -> quy ngược ra scale cho đúng ThrowDistance
            float push = Mathf.Lerp(_victim.MinKnockback, _victim.MaxKnockback, ThrowStrength) * (ThrowLaunch ? _victim.AirKnockbackScale : 1f);
            _victim.TakeHit(self.forward, ThrowStrength, ThrowDistance / Mathf.Max(push, 0.01f), ThrowLaunch);
            _victim = null;
        }
    }

    private HitReaction FindVictim()
    {
        Transform self = _character.transform;
        HitReaction best = null;
        float bestDistance = GrabRange;
        foreach (LockOnTarget target in LockOnTarget.All)
        {
            HitReaction reaction = target.GetComponentInParent<HitReaction>();
            if (reaction == null || reaction.IsAirborne) continue;
            Vector3 to = Vector3.ProjectOnPlane(target.transform.position - self.position, Vector3.up);
            float distance = to.magnitude;
            if (distance > bestDistance || Vector3.Angle(self.forward, to) > GrabAngle) continue;
            bestDistance = distance;
            best = reaction;
        }
        return best;
    }

    private int SkillSlotOf(WeaponData weapon) => weapon != null ? weapon.SkillSlotOf(ChainName) : -1;

    private int StepIndex(CharacterCombo combo)
    {
        foreach (ComboChain chain in combo.CurrentWeapon.Chains)
        {
            if (chain.Name == ChainName) return chain.Steps.IndexOf(combo.CurrentStep);
        }
        return -1;
    }
}
