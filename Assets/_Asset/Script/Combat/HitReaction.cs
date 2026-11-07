using UnityEngine;

/// <summary>
/// Đánh dấu 1 vật chém được và cho nó phản ứng: nháy sáng + lắc lò xo + bị đẩy lùi theo hướng chém.
/// MeleeHitDetector tìm component này trên collider bị quét. Bù nhìn dùng nó, địch sau này cũng gắn được.
/// </summary>
public class HitReaction : MonoBehaviour
{
    private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

    [Tooltip("Phần bị lắc khi trúng đòn (gốc đặt ở điểm tựa)")]
    public Transform Pivot;
    [Tooltip("Mesh nháy sáng, material cần có _EmissiveColor")]
    public Renderer[] FlashRenderers;

    [Header("Nháy sáng")]
    [ColorUsage(false, true)] public Color FlashColor = new Color(6f, 2.4f, 0.8f);
    public float FlashDuration = 0.14f;

    [Header("Lắc lò xo")]
    [Tooltip("Vận tốc lắc (độ/giây) ở đòn yếu nhất")]
    public float MinImpulse = 200f;
    [Tooltip("Vận tốc lắc (độ/giây) ở đòn mạnh nhất")]
    public float MaxImpulse = 520f;
    public float MaxTilt = 38f;
    public float SpringFrequency = 9f;
    [Range(0f, 1f)] public float SpringDamping = 0.22f;

    [Header("Knockback")]
    [Tooltip("Quãng đường bị đẩy lùi (m) ở đòn yếu nhất")]
    public float MinKnockback = 0.15f;
    [Tooltip("Quãng đường bị đẩy lùi (m) ở đòn mạnh nhất")]
    public float MaxKnockback = 0.7f;
    [Tooltip("Càng lớn càng dừng nhanh: đẩy mạnh lúc đầu rồi hãm lại")]
    public float KnockbackDamping = 9f;
    [Tooltip("Dò vật cản theo khối cầu bán kính này")]
    public float KnockbackRadius = 0.4f;
    [Tooltip("Độ cao chênh tối đa được bám theo mặt đất, sâu hơn thì coi là mép vực và dừng (m)")]
    public float KnockbackMaxStep = 0.5f;
    public LayerMask KnockbackObstacles = ~0;

    [Header("Về chỗ cũ (bù nhìn tập luyện)")]
    public bool ReturnHome;
    [Tooltip("Không bị đánh bấy nhiêu giây thì quay về vị trí ban đầu")]
    public float ReturnDelay = 3f;
    public float ReturnSpeed = 1.5f;
    [Tooltip("Bị đẩy xa chỗ cũ tối đa bấy nhiêu m, để bù nhìn không trôi khỏi khu tập")]
    public float MaxHomeDistance = 3f;

    public int HitCount { get; private set; }

    private Vector3 _home;
    private Vector3 _knockVelocity;
    private float _lastHitTime;
    private MaterialPropertyBlock _block;
    private Quaternion _restRotation;
    private Vector3 _tilt;
    private Vector3 _tiltVelocity;
    private float _flash;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        if (Pivot != null) _restRotation = Pivot.rotation;
        _home = transform.position;
    }

    public void TakeHit(Vector3 attackDirection, float strength, float knockbackScale = 1f)
    {
        HitCount++;
        _flash = 1f;
        // trục nằm ngang vuông góc hướng chém: đổ theo đúng chiều lưỡi kiếm đẩy
        _tiltVelocity += Vector3.Cross(Vector3.up, attackDirection) * Mathf.Lerp(MinImpulse, MaxImpulse, strength);
        ApplyFlash();

        // quãng đường của vận tốc giảm dần theo e^(-k*t) là v0/k, nên v0 = quãng đường * k
        _knockVelocity = Vector3.ProjectOnPlane(attackDirection, Vector3.up).normalized * (Mathf.Lerp(MinKnockback, MaxKnockback, strength) * knockbackScale * KnockbackDamping);
        _lastHitTime = Time.time;
    }

    private void Update()
    {
        UpdateKnockback(Time.deltaTime);

        if (_flash > 0f)
        {
            // thời gian thực để vẫn thấy nháy trong lúc hitstop
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / Mathf.Max(FlashDuration, 0.01f));
            ApplyFlash();
        }

        if (Pivot == null) return;

        float dt = Time.deltaTime;
        _tiltVelocity += (-_tilt * (SpringFrequency * SpringFrequency) - _tiltVelocity * (2f * SpringDamping * SpringFrequency)) * dt;
        _tilt = Vector3.ClampMagnitude(_tilt + _tiltVelocity * dt, MaxTilt);
        float angle = _tilt.magnitude;
        Pivot.rotation = angle > 0.001f ? Quaternion.AngleAxis(angle, _tilt / angle) * _restRotation : _restRotation;
    }

    private void UpdateKnockback(float deltaTime)
    {
        Vector3 motion = _knockVelocity * deltaTime;
        _knockVelocity *= Mathf.Exp(-KnockbackDamping * deltaTime);

        if (ReturnHome && Time.time - _lastHitTime >= ReturnDelay)
        {
            motion += Vector3.ClampMagnitude(Vector3.ProjectOnPlane(_home - transform.position, Vector3.up), ReturnSpeed * deltaTime);
        }
        if (motion.sqrMagnitude < 0.00000001f) return;
        TryMove(motion);
    }

    // dừng khi vướng tường hoặc tới mép vực, bám theo mặt đất khi đi qua bậc thấp
    private void TryMove(Vector3 motion)
    {
        float distance = motion.magnitude;
        Vector3 direction = motion / distance;
        Vector3 center = transform.position + Vector3.up * (KnockbackMaxStep + KnockbackRadius);
        if (Physics.SphereCast(center, KnockbackRadius, direction, out _, distance, KnockbackObstacles, QueryTriggerInteraction.Ignore))
        {
            _knockVelocity = Vector3.zero;
            return;
        }

        Vector3 next = transform.position + motion;
        if (ReturnHome && Vector3.ProjectOnPlane(next - _home, Vector3.up).magnitude > MaxHomeDistance
            && Vector3.Dot(motion, next - _home) > 0f)
        {
            _knockVelocity = Vector3.zero;
            return;
        }
        // tia bắt đầu từ trong collider của chính mục tiêu nên không tự trúng nó
        if (!Physics.Raycast(next + Vector3.up * KnockbackMaxStep, Vector3.down, out RaycastHit ground, KnockbackMaxStep * 2f, KnockbackObstacles, QueryTriggerInteraction.Ignore))
        {
            _knockVelocity = Vector3.zero;
            return;
        }
        next.y = ground.point.y;
        transform.position = next;
    }

    private void ApplyFlash()
    {
        if (FlashRenderers == null) return;
        _block.SetColor(EmissiveColor, FlashColor * (_flash * _flash));
        foreach (Renderer renderer in FlashRenderers)
        {
            if (renderer != null) renderer.SetPropertyBlock(_block);
        }
    }
}
