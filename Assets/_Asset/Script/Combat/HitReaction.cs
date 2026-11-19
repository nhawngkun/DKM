using UnityEngine;

/// <summary>
/// Đánh dấu 1 vật chém được và cho nó phản ứng: nháy sáng + lắc lò xo + bị đẩy lùi theo hướng chém.
/// Đòn hất (launcher) tung nó lên không, bị đánh tiếp trên không thì treo lại, rơi xuống thì đáp đất.
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

    [Header("Bị hất (airborne)")]
    [Tooltip("Đòn hất tung mục tiêu lên cao bấy nhiêu m so với chỗ đang đứng")]
    public float LaunchHeight = 2.5f;
    [Tooltip("Thời gian bay lên tới đỉnh (giây)")]
    public float LaunchTime = 0.35f;
    [Tooltip("Lơ lửng ở đỉnh bấy nhiêu giây để người chơi kịp nhảy theo")]
    public float LaunchApexHang = 0.5f;
    [Tooltip("Mỗi nhát trúng khi đang trên không treo mục tiêu lại bấy nhiêu giây")]
    public float HangTime = 0.3f;
    [Range(0f, 1f), Tooltip("Trọng lực còn lại khi đang treo")]
    public float HangGravityScale = 0.15f;
    public float FallGravity = 20f;
    public float MaxFallSpeed = 20f;
    [Tooltip("Không bị hất cao quá bấy nhiêu m so với mặt đất lúc bị hất đầu tiên")]
    public float MaxAirHeight = 4f;
    [Tooltip("Trên không bị đẩy lùi ít hơn để air combo còn với tới")]
    public float AirKnockbackScale = 0.35f;

    [Header("Về chỗ cũ (bù nhìn tập luyện)")]
    public bool ReturnHome;
    [Tooltip("Không bị đánh bấy nhiêu giây thì quay về vị trí ban đầu")]
    public float ReturnDelay = 3f;
    public float ReturnSpeed = 1.5f;
    [Tooltip("Bị đẩy xa chỗ cũ tối đa bấy nhiêu m, để bù nhìn không trôi khỏi khu tập")]
    public float MaxHomeDistance = 3f;

    public int HitCount { get; private set; }
    public bool IsAirborne => _airborne;

    private Vector3 _home;
    private Vector3 _knockVelocity;
    private float _lastHitTime;
    private MaterialPropertyBlock _block;
    private Quaternion _restRotation;
    private Vector3 _tilt;
    private Vector3 _tiltVelocity;
    private float _flash;

    private bool _airborne;
    private bool _airRising;
    private float _airVerticalSpeed;
    private float _airRiseGravity;
    private float _hangTimer;
    private float _airGroundHeight;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        if (Pivot != null) _restRotation = Pivot.rotation;
        _home = transform.position;
    }

    public void TakeHit(Vector3 attackDirection, float strength, float knockbackScale = 1f, bool launch = false)
    {
        HitCount++;
        _flash = 1f;
        // trục nằm ngang vuông góc hướng chém: đổ theo đúng chiều lưỡi kiếm đẩy
        _tiltVelocity += Vector3.Cross(Vector3.up, attackDirection) * Mathf.Lerp(MinImpulse, MaxImpulse, strength);
        ApplyFlash();

        if (launch || _airborne) knockbackScale *= AirKnockbackScale;
        // quãng đường của vận tốc giảm dần theo e^(-k*t) là v0/k, nên v0 = quãng đường * k
        _knockVelocity = Vector3.ProjectOnPlane(attackDirection, Vector3.up).normalized * (Mathf.Lerp(MinKnockback, MaxKnockback, strength) * knockbackScale * KnockbackDamping);
        _lastHitTime = Time.time;

        if (launch) Launch();
        else if (_airborne) Hang(HangTime);
    }

    // bay lên với vận tốc giảm đều: tới đỉnh đúng sau LaunchTime, v0 = 2h/t, gia tốc hãm = 2h/t²
    private void Launch()
    {
        if (!_airborne) _airGroundHeight = transform.position.y;
        float height = Mathf.Min(LaunchHeight, MaxAirHeight - (transform.position.y - _airGroundHeight));
        if (height <= 0.05f)
        {
            Hang(LaunchApexHang);
            return;
        }
        float time = Mathf.Max(LaunchTime, 0.05f);
        _airborne = true;
        _airRising = true;
        _airVerticalSpeed = 2f * height / time;
        _airRiseGravity = 2f * height / (time * time);
        _hangTimer = LaunchApexHang;
    }

    private void Hang(float duration)
    {
        _hangTimer = Mathf.Max(_hangTimer, duration);
        if (!_airRising) _airVerticalSpeed = Mathf.Max(_airVerticalSpeed, 0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (_airborne) UpdateAirborne(dt);
        else UpdateKnockback(dt);

        if (_flash > 0f)
        {
            // thời gian thực để vẫn thấy nháy trong lúc hitstop
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / Mathf.Max(FlashDuration, 0.01f));
            ApplyFlash();
        }

        if (Pivot == null) return;

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

    private void UpdateAirborne(float deltaTime)
    {
        if (_airRising)
        {
            _airVerticalSpeed -= _airRiseGravity * deltaTime;
            if (_airVerticalSpeed <= 0f)
            {
                _airVerticalSpeed = 0f;
                _airRising = false;
            }
        }
        else
        {
            float gravity = _hangTimer > 0f ? FallGravity * HangGravityScale : FallGravity;
            _hangTimer -= deltaTime;
            _airVerticalSpeed = Mathf.Max(_airVerticalSpeed - gravity * deltaTime, -MaxFallSpeed);
        }

        Vector3 planar = _knockVelocity * deltaTime;
        _knockVelocity *= Mathf.Exp(-KnockbackDamping * deltaTime);
        if (planar.sqrMagnitude > 0.00000001f && !Blocked(planar)) transform.position += planar;

        float drop = _airVerticalSpeed * deltaTime;
        Vector3 next = transform.position + Vector3.up * drop;
        if (drop < 0f)
        {
            // tia bắt đầu từ trong collider của chính mục tiêu nên không tự trúng nó
            Vector3 origin = transform.position + Vector3.up * KnockbackMaxStep;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, KnockbackMaxStep - drop + 0.01f, KnockbackObstacles, QueryTriggerInteraction.Ignore)
                && ground.point.y >= next.y)
            {
                next.y = ground.point.y;
                Land();
            }
            else if (next.y < _airGroundHeight - 30f)
            {
                // rơi khỏi map: về lại độ cao lúc bị hất
                next.y = _airGroundHeight;
                Land();
            }
        }
        transform.position = next;
    }

    private void Land()
    {
        _airborne = false;
        _airRising = false;
        _airVerticalSpeed = 0f;
        _hangTimer = 0f;
        _knockVelocity = Vector3.zero;
        _lastHitTime = Time.time;
    }

    private bool Blocked(Vector3 motion)
    {
        float distance = motion.magnitude;
        Vector3 center = transform.position + Vector3.up * (KnockbackMaxStep + KnockbackRadius);
        if (Physics.SphereCast(center, KnockbackRadius, motion / distance, out _, distance, KnockbackObstacles, QueryTriggerInteraction.Ignore)
            || (ReturnHome && Vector3.ProjectOnPlane(transform.position + motion - _home, Vector3.up).magnitude > MaxHomeDistance
                && Vector3.Dot(motion, transform.position + motion - _home) > 0f))
        {
            _knockVelocity = Vector3.zero;
            return true;
        }
        return false;
    }

    // dừng khi vướng tường hoặc tới mép vực, bám theo mặt đất khi đi qua bậc thấp
    private void TryMove(Vector3 motion)
    {
        if (Blocked(motion)) return;

        Vector3 next = transform.position + motion;
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
