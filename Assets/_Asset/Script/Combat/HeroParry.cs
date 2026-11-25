using UnityEngine;

/// <summary>
/// Đỡ đòn (D-020): bấm LB / R ngay trước khi đòn xanh của địch chạm. Bấm huỷ đòn đang đánh, mở cửa sổ ParryWindow.
/// Đỡ trúng: địch mất thăng bằng + lỡ nhịp, khựng + tia lửa, rồi tự ra đòn phản kích (chuỗi Skill CounterChain).
/// Bấm trượt bị khoá WhiffLockout giây (chống bấm loạn). EnemyAttack gọi TryDeflect lúc đòn chạm.
/// </summary>
[RequireComponent(typeof(CharController))]
public class HeroParry : MonoBehaviour
{
    public ComboCameraFeedback cameraFeedback;
    [Tooltip("Từ lúc bấm, đòn chạm trong bấy nhiêu giây thì đỡ được")]
    public float ParryWindow = 0.2f;
    [Tooltip("Bấm trượt thì không đỡ lại được trong bấy nhiêu giây")]
    public float WhiffLockout = 0.35f;
    [Tooltip("Thăng bằng địch mất mỗi lần bị đỡ (bù nhìn có 100)")]
    public float BalanceDamage = 45f;
    [Tooltip("Địch bị đỡ thì chậm lại bấy nhiêu giây mới đánh tiếp")]
    public float AttackerStagger = 1.2f;
    [Tooltip("Chuỗi Skill tự ra sau khi đỡ trúng, trống = không phản kích")]
    public string CounterChain = "Phan Don";
    public bool LogParries = true;

    private CharController _character;
    private HeroGrapple _grapple;
    private CombatEffects _effects;
    private float _windowUntil = -1f;
    private float _lockedUntil = -1f;

    public bool IsParrying => Time.time <= _windowUntil;

    private void Awake()
    {
        _character = GetComponent<CharController>();
        _grapple = GetComponent<HeroGrapple>();
        _character.OnParry += Press;
    }

    private void Start()
    {
        if (cameraFeedback == null) cameraFeedback = GetComponentInChildren<ComboCameraFeedback>();
        _effects = GetComponentInChildren<CombatEffects>();
        if (cameraFeedback == null) Debug.LogWarning("[HeroParry] thiếu ComboCameraFeedback, đỡ trúng sẽ không có khựng / zoom", this);
        WeaponData weapon = _character.characterCombo != null ? _character.characterCombo.CurrentWeapon : null;
        if (!string.IsNullOrEmpty(CounterChain) && (weapon == null || weapon.SkillSlotOf(CounterChain) < 0))
            Debug.LogWarning($"[HeroParry] vũ khí hiện tại không có chuỗi Skill '{CounterChain}', đỡ trúng sẽ không phản kích", this);
    }

    private void OnDestroy()
    {
        if (_character != null) _character.OnParry -= Press;
    }

    private void Update()
    {
        // hết cửa sổ mà chưa đỡ được gì = bấm trượt
        if (_windowUntil >= 0f && Time.time > _windowUntil)
        {
            _windowUntil = -1f;
            _lockedUntil = Time.time + WhiffLockout;
        }
    }

    private void Press()
    {
        if (Time.time < _lockedUntil || IsParrying) return;
        if (!_character.IsGrounded || _character.IsDashing) return;
        if (_grapple != null && _grapple.IsGrabbing) return;

        // phòng thủ huỷ được tấn công (Pillar 2)
        if (_character.characterCombo != null) _character.characterCombo.CancelCombo();
        _windowUntil = Time.time + ParryWindow;
    }

    /// <summary>Đòn đỡ được của attacker vừa chạm: true = đã đỡ (đòn không trúng).</summary>
    public bool TryDeflect(EnemyAttack attacker)
    {
        if (!IsParrying) return false;
        _windowUntil = -1f;

        Vector3 toAttacker = Vector3.ProjectOnPlane(attacker.transform.position - transform.position, Vector3.up);
        if (toAttacker.sqrMagnitude > 0.0001f) _character.Motor.SetRotation(Quaternion.LookRotation(toAttacker.normalized, Vector3.up));

        attacker.Stagger(AttackerStagger);
        HitReaction reaction = attacker.GetComponent<HitReaction>();
        if (reaction != null) reaction.DrainBalance(BalanceDamage);

        if (_effects != null)
        {
            Vector3 spark = transform.position + Vector3.up * 1.3f + toAttacker.normalized * 0.5f;
            _effects.PlayHit(spark, -toAttacker.normalized, 1f);
        }
        if (cameraFeedback != null) cameraFeedback.PlayParry();
        Counter();
        if (LogParries) Debug.Log($"[HeroParry] đỡ {attacker.name}, thăng bằng địch còn {(reaction != null ? reaction.Balance01 : 1f):F2}", this);
        return true;
    }

    private void Counter()
    {
        CharacterCombo combo = _character.characterCombo;
        if (combo == null || combo.CurrentWeapon == null || string.IsNullOrEmpty(CounterChain)) return;
        int slot = combo.CurrentWeapon.SkillSlotOf(CounterChain);
        if (slot >= 0) combo.OnAttackInput(AttackContext.Skill, slot);
    }
}
