using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Module ĐỘC LẬP: shake, FOV kick, slow motion.
/// Không tham chiếu Focus hay Follow. Tự cộng shake offset vào Transform.position
/// của chính GameObject nó gắn vào — miễn là được gọi SAU Follow trong cùng frame.
/// </summary>
public class CameraEffects: MonoBehaviour
{
    [Header("References")]
    public Camera Camera;

    [Header("FOV Kick")]
    public float BaseFov = 60f;
    public AnimationCurve FovKickCurve = AnimationCurve.EaseInOut(0f, 6f, 0.15f, 0f);

    [Header("Shake")]
    public float ShakeFrequency = 25f;
    public AnimationCurve ShakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 0.25f, 0f);

    [Header("Slow Motion")]
    public float SlowMotionTransitionSharpness = 10f;

    [Header("Camera cinematic (đòn chốt)")]
    [Tooltip("Độ hoà sang góc cinematic theo giây thực")]
    public AnimationCurve CinematicBlend = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.42f, 1f), new Keyframe(0.7f, 0f));
    [Tooltip("Lệch ngang so với hướng chém (m)")]
    public float CinematicSide = 2.2f;
    [Tooltip("Lùi về sau người chém (m)")]
    public float CinematicBehind = 0.8f;
    [Tooltip("Cao hơn điểm nhìn (m), âm = góc thấp")]
    public float CinematicHeight = -0.35f;
    [Tooltip("FOV cộng thêm lúc cinematic, âm = zoom vào")]
    public float CinematicFov = -14f;
    [Tooltip("Cả 2 bên đều không đủ khoảng trống này (m) thì giữ nguyên góc máy, chỉ zoom và hậu kỳ")]
    public float CinematicMinDistance = 2.2f;
    public float CinematicCollisionRadius = 0.2f;
    public LayerMask CinematicObstacles = ~0;

    [Header("Xung hậu kỳ khi va chạm")]
    [Tooltip("Volume global chứa chromatic aberration / lens distortion; weight do script điều khiển")]
    public Volume ImpactVolume;
    public float ImpactPulseDuration = 0.25f;

    [Header("Cảm giác tốc độ")]
    [Tooltip("FOV cộng thêm khi đạt SpeedFullSpeed")]
    public float SpeedFovBonus = 16f;
    public float SpeedStartSpeed = 3.5f;
    public float SpeedFullSpeed = 10f;
    public float SpeedBlendSharpness = 3.5f;
    [Tooltip("Rung nhẹ cầm tay khi chạy hết tốc độ (m)")]
    public float SpeedSwayMagnitude = 0.025f;
    public float SpeedSwayFrequency = 2.2f;

    [Header("Nghiêng camera theo cua")]
    public float MaxRoll = 2.5f;
    [Tooltip("Độ roll trên mỗi m/s² gia tốc ngang")]
    public float RollPerAcceleration = 0.1f;
    [Tooltip("Độ roll trên mỗi m/s chạy ngang so với camera")]
    public float RollPerSideSpeed = 0.15f;
    public float RollFilter = 7f;
    public float RollSharpness = 6f;
    [Range(0f, 1f)] public float LockOnRollScale = 0.3f;

    [Header("Nhún camera khi tiếp đất")]
    [Tooltip("Vận tốc nhún (m/s) trên mỗi m/s tốc độ rơi")]
    public float LandDipImpulse = 0.22f;
    public float LandDipMax = 0.3f;
    public float LandDipFrequency = 10f;
    [Range(0.1f, 1f)] public float LandDipDamping = 0.55f;

    private float _speedBlend;
    private float _roll;
    private Vector3 _filteredVelocity;
    private bool _wasGrounded = true;
    private float _fallSpeed;
    private float _landDip;
    private float _landDipVelocity;

    private float _fovKickTime = -1f;
    private float _shakeTime = -1f;
    private float _shakeMagnitude;
    private Vector3 _shakeSeed;
    private float _targetTimeScale = 1f;
    private float _slowMotionEndTime = -1f;
    private float _hitStopEndTime = -1f;
    private float _hitStopScale;
    private bool _inHitStop;
    private float _cinematicTime = -1f;
    private float _cinematicLength;
    private Vector3 _cinematicPosition;
    private Quaternion _cinematicRotation;
    private bool _cinematicMoves;
    private float _impactPulse;

    void Awake()
    {
        if (Camera == null) Camera = GetComponentInChildren<Camera>();
        if (Camera != null) BaseFov = Camera.fieldOfView;
        _shakeSeed = new Vector3(Random.value, Random.value, Random.value) * 100f;
        _cinematicLength = CinematicBlend.length > 0 ? CinematicBlend.keys[CinematicBlend.length - 1].time : 0f;
    }

    // đang hitstop mà tắt camera thì không được kẹt game ở timeScale thấp
    void OnDisable()
    {
        if (_inHitStop) Time.timeScale = _targetTimeScale;
        _inHitStop = false;
    }

    public void TriggerFovKick() => _fovKickTime = 0f;

    public void TriggerShake(float magnitude)
    {
        // đang rung mạnh hơn thì giữ, đòn nhẹ không được cắt ngang cú rung nặng
        if (_shakeTime >= 0f && _shakeMagnitude * ShakeFalloff.Evaluate(_shakeTime) > magnitude) return;
        _shakeTime = 0f;
        _shakeMagnitude = magnitude;
    }

    public void TriggerSlowMotion(float scale, float duration)
    {
        _targetTimeScale = scale;
        _slowMotionEndTime = Time.unscaledTime + duration;
    }

    /// <summary>Khựng hình theo thời gian thực. CameraEffects là chủ duy nhất của Time.timeScale.</summary>
    public void TriggerHitStop(float duration, float scale)
    {
        _hitStopEndTime = Mathf.Max(_hitStopEndTime, Time.unscaledTime + duration);
        _hitStopScale = scale;
        _inHitStop = true;
        Time.timeScale = scale;
    }

    public void TriggerImpactPulse(float strength) => _impactPulse = Mathf.Max(_impactPulse, strength);

    /// <summary>Chốt 1 góc máy bên hông pha chém, cách vật cản, rồi hoà qua lại theo CinematicBlend.</summary>
    public void TriggerCinematic(Vector3 focus, Vector3 attackDirection)
    {
        Vector3 forward = Vector3.ProjectOnPlane(attackDirection, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        // ưu tiên bên đang đứng để camera không quay ngược 180 độ
        if (Vector3.Dot(side, transform.position - focus) < 0f) side = -side;

        Vector3 offset = side * CinematicSide - forward * CinematicBehind + Vector3.up * CinematicHeight;
        Vector3 otherOffset = -side * CinematicSide - forward * CinematicBehind + Vector3.up * CinematicHeight;
        float free = FreeDistance(focus, offset);
        if (free < offset.magnitude)
        {
            float otherFree = FreeDistance(focus, otherOffset);
            if (otherFree > free)
            {
                free = otherFree;
                offset = otherOffset;
            }
        }

        _cinematicMoves = free >= CinematicMinDistance;
        _cinematicPosition = focus + offset.normalized * free;
        _cinematicRotation = Quaternion.LookRotation(focus - _cinematicPosition, Vector3.up);
        _cinematicTime = 0f;
    }

    private float FreeDistance(Vector3 focus, Vector3 offset)
    {
        float distance = offset.magnitude;
        Vector3 direction = offset / distance;
        // bắt đầu dò từ ngoài thân 2 nhân vật, nếu không SphereCast sẽ chạm chính họ
        const float clearOfBodies = 1f;
        if (Physics.SphereCast(focus + direction * clearOfBodies, CinematicCollisionRadius, direction, out RaycastHit hit, distance - clearOfBodies, CinematicObstacles, QueryTriggerInteraction.Ignore))
        {
            return clearOfBodies + hit.distance;
        }
        return distance;
    }

    public void CancelSlowMotion()
    {
        _targetTimeScale = 1f;
        _slowMotionEndTime = -1f;
    }

    /// <summary>Đo chuyển động nhân vật, gọi trước ApplyEffects (Transform lúc này chưa bị roll).</summary>
    public void UpdateMotion(float deltaTime, Vector3 planarVelocity, float verticalVelocity, bool isGrounded, bool hasLockOn)
    {
        float speed = Mathf.Sqrt(planarVelocity.sqrMagnitude + (isGrounded ? verticalVelocity * verticalVelocity : 0f));
        float speedTarget = Mathf.InverseLerp(SpeedStartSpeed, SpeedFullSpeed, speed);
        _speedBlend = Mathf.Lerp(_speedBlend, speedTarget, 1f - Mathf.Exp(-SpeedBlendSharpness * deltaTime));

        Vector3 acceleration = (planarVelocity - _filteredVelocity) * RollFilter;
        _filteredVelocity = Vector3.Lerp(_filteredVelocity, planarVelocity, 1f - Mathf.Exp(-RollFilter * deltaTime));
        Vector3 right = transform.right;
        float side = Vector3.Dot(acceleration, right) * RollPerAcceleration + Vector3.Dot(planarVelocity, right) * RollPerSideSpeed;
        // z âm = camera nghiêng sang phải
        float rollTarget = -Mathf.Clamp(side, -MaxRoll, MaxRoll) * (hasLockOn ? LockOnRollScale : 1f);
        _roll = Mathf.Lerp(_roll, rollTarget, 1f - Mathf.Exp(-RollSharpness * deltaTime));

        if (!isGrounded)
        {
            _fallSpeed = Mathf.Max(_fallSpeed, -verticalVelocity);
        }
        else if (!_wasGrounded)
        {
            _landDipVelocity -= _fallSpeed * LandDipImpulse;
            _fallSpeed = 0f;
        }
        _wasGrounded = isGrounded;

        float force = -_landDip * (LandDipFrequency * LandDipFrequency) - _landDipVelocity * (2f * LandDipDamping * LandDipFrequency);
        _landDipVelocity += force * deltaTime;
        _landDip += _landDipVelocity * deltaTime;
        if (_landDip < -LandDipMax)
        {
            _landDip = -LandDipMax;
            _landDipVelocity = Mathf.Max(_landDipVelocity, 0f);
        }
    }

    /// <summary>Gọi mỗi frame, SAU KHI CameraFollowComponent đã đặt xong Transform.position.</summary>
    public void ApplyEffects(float deltaTime)
    {
        float cinematic = 0f;
        if (_cinematicTime >= 0f)
        {
            cinematic = Mathf.Clamp01(CinematicBlend.Evaluate(_cinematicTime));
            if (_cinematicMoves)
            {
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, _cinematicPosition, cinematic),
                    Quaternion.Slerp(transform.rotation, _cinematicRotation, cinematic));
            }
            _cinematicTime += Time.unscaledDeltaTime;
            if (_cinematicTime > _cinematicLength) _cinematicTime = -1f;
        }

        float swayTime = Time.time * SpeedSwayFrequency;
        float swayX = (Mathf.PerlinNoise(_shakeSeed.x, swayTime) - 0.5f) * 2f;
        float swayY = (Mathf.PerlinNoise(_shakeSeed.y, swayTime) - 0.5f) * 2f;
        Vector3 cameraUp = transform.up;
        transform.position += (transform.right * swayX + cameraUp * swayY) * (SpeedSwayMagnitude * _speedBlend) + cameraUp * _landDip;
        transform.rotation *= Quaternion.Euler(0f, 0f, _roll);

        if (_shakeTime >= 0f)
        {
            float t = _shakeTime;
            float falloff = ShakeFalloff.Evaluate(t);
            if (falloff <= 0f && t > 0f)
            {
                _shakeTime = -1f;
            }
            else
            {
                float nx = (Mathf.PerlinNoise(_shakeSeed.x, t * ShakeFrequency) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(_shakeSeed.y, t * ShakeFrequency) - 0.5f) * 2f;
                Vector3 shakeOffset = (transform.right * nx + transform.up * ny) * (_shakeMagnitude * falloff);
                transform.position += shakeOffset;
                _shakeTime += Time.unscaledDeltaTime; // rung xuyên qua hitstop
            }
        }

        if (Camera != null)
        {
            float speedFov = BaseFov + SpeedFovBonus * _speedBlend;
            if (_fovKickTime >= 0f)
            {
                float lastKeyTime = FovKickCurve.length > 0 ? FovKickCurve.keys[FovKickCurve.length - 1].time : 0f;
                if (_fovKickTime > lastKeyTime)
                {
                    _fovKickTime = -1f;
                    Camera.fieldOfView = speedFov;
                }
                else
                {
                    Camera.fieldOfView = speedFov + FovKickCurve.Evaluate(_fovKickTime);
                    _fovKickTime += Time.unscaledDeltaTime;
                }
            }
            else
            {
                Camera.fieldOfView = speedFov;
            }
            Camera.fieldOfView += CinematicFov * cinematic;
        }

        if (ImpactVolume != null)
        {
            _impactPulse = Mathf.MoveTowards(_impactPulse, 0f, Time.unscaledDeltaTime / Mathf.Max(ImpactPulseDuration, 0.01f));
            float weight = Mathf.Max(cinematic, _impactPulse);
            ImpactVolume.weight = weight;
            ImpactVolume.enabled = weight > 0.001f;
        }

        if (_slowMotionEndTime >= 0f && Time.unscaledTime >= _slowMotionEndTime)
        {
            _targetTimeScale = 1f;
            _slowMotionEndTime = -1f;
        }
        if (_inHitStop)
        {
            if (Time.unscaledTime < _hitStopEndTime)
            {
                Time.timeScale = _hitStopScale;
                return;
            }
            // hết khựng thì nhả ngay, ramp lại sẽ mất độ "gắt" của cú chém
            _inHitStop = false;
            Time.timeScale = _targetTimeScale;
            return;
        }
        Time.timeScale = Mathf.Lerp(Time.timeScale, _targetTimeScale, 1f - Mathf.Exp(-SlowMotionTransitionSharpness * Time.unscaledDeltaTime));
        if (Mathf.Abs(Time.timeScale - _targetTimeScale) < 0.001f) Time.timeScale = _targetTimeScale;
    }
}