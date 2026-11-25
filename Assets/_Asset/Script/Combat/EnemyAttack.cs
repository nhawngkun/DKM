using UnityEngine;

/// <summary>
/// Đòn đánh tối giản của địch: nháy báo trước, sau Windup giây thì trúng người chơi trong tầm.
/// Báo màu (D-020): xanh = đỡ được (HeroParry), đỏ = không đỡ được, chỉ né. Lúc đòn chạm: đỡ trúng -> bị đỡ;
/// người chơi đang trong i-frames của cú né -> né chuẩn (PerfectDodge). Chưa có máu người chơi, trúng thật chỉ ghi log.
/// AI sau này gọi StartAttack.
/// </summary>
[RequireComponent(typeof(HitReaction))]
public class EnemyAttack : MonoBehaviour
{
    [Tooltip("Từ lúc nháy báo trước tới lúc đòn chạm (giây)")]
    public float Windup = 0.6f;
    public float Range = 2.5f;
    [Range(0f, 180f), Tooltip("Nửa góc trúng quanh hướng mặt, 180 = mọi hướng")]
    public float HalfAngle = 180f;
    [ColorUsage(false, true)] public Color ParryableColor = new Color(0.3f, 1.4f, 8f);
    [ColorUsage(false, true)] public Color UnparryableColor = new Color(8f, 0.3f, 0.2f);
    [Range(0f, 1f), Tooltip("Tỉ lệ đòn đỏ (không đỡ được) khi StartAttack không chỉ định")]
    public float UnparryableChance = 0.3f;
    [Tooltip("Bù nhìn tập né: tự ra đòn mỗi bấy nhiêu giây khi người chơi trong tầm, 0 = tắt")]
    public float AutoAttackInterval = 2.5f;
    public bool LogHits = true;

    private HitReaction _reaction;
    private float _impactTime = -1f;
    private float _nextAutoAttack;
    private bool _parryable;

    public bool IsWindingUp => _impactTime >= 0f;
    public bool IsParryable => _parryable;

    private void Awake()
    {
        _reaction = GetComponent<HitReaction>();
    }

    public void StartAttack() => StartAttack(Random.value >= UnparryableChance);

    public void StartAttack(bool parryable)
    {
        if (IsWindingUp || _reaction.IsBroken) return;
        _parryable = parryable;
        _impactTime = Time.time + Windup;
        _reaction.Flash(parryable ? ParryableColor : UnparryableColor);
    }

    /// <summary>Bị đỡ / bị ngắt: bỏ đòn đang vung, chậm lại bấy nhiêu giây mới đánh tiếp.</summary>
    public void Stagger(float duration)
    {
        _impactTime = -1f;
        _nextAutoAttack = Mathf.Max(_nextAutoAttack, Time.time + duration);
    }

    private void Update()
    {
        if (_reaction.IsBroken)
        {
            _impactTime = -1f;
            return;
        }
        if (IsWindingUp)
        {
            if (Time.time < _impactTime) return;
            _impactTime = -1f;
            Resolve();
            return;
        }
        if (AutoAttackInterval > 0f && Time.time >= _nextAutoAttack && FindPlayerInRange() != null)
        {
            _nextAutoAttack = Time.time + AutoAttackInterval;
            StartAttack();
        }
    }

    private void Resolve()
    {
        CharController player = FindPlayerInRange();
        if (player == null) return;

        if (_parryable)
        {
            HeroParry parry = player.GetComponent<HeroParry>();
            if (parry != null && parry.TryDeflect(this)) return;
        }
        if (player.IsInvulnerable)
        {
            PerfectDodge dodge = player.GetComponent<PerfectDodge>();
            if (dodge != null) dodge.Trigger();
            return;
        }
        if (LogHits) Debug.Log($"[EnemyAttack] {name} đánh trúng {player.name} ({(_parryable ? "xanh" : "đỏ")})", this);
    }

    private CharController FindPlayerInRange()
    {
        // scene có thể có nhiều CharController -> lấy người gần nhất trong tầm
        CharController best = null;
        float bestDistance = Range;
        foreach (CharController player in FindObjectsByType<CharController>(FindObjectsInactive.Exclude))
        {
            Vector3 offset = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            float distance = offset.magnitude;
            if (distance > bestDistance) continue;
            if (HalfAngle < 180f && Vector3.Angle(transform.forward, offset) > HalfAngle) continue;
            bestDistance = distance;
            best = player;
        }
        return best;
    }
}
