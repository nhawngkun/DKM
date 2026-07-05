using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller lấy cảm hứng từ cảm giác điều khiển camera của Devil May Cry 5:
/// - Follow có độ trễ (lag) tách riêng theo chiều ngang / chiều dọc, tạo cảm giác trọng lượng.
/// - Auto-align phía sau lưng nhân vật khi di chuyển, free-look khi đứng yên hoặc đang xoay tay.
/// - Auto zoom-out khi combat / di chuyển nhanh.
/// - Hỗ trợ Lock-On đơn giản (khóa mục tiêu, giữ cả player + enemy trong khung hình).
/// - FOV kick khi ra đòn, camera shake khi trúng đòn / nhận damage.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("References")]
    public Camera Camera;

    [Header("Framing")]
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    [Tooltip("Độ mượt khi camera bám theo nhân vật theo chiều NGANG (planar). Giá trị thấp hơn = độ trễ rõ hơn, cảm giác 'nặng' hơn.")]
    public float FollowingSharpnessPlanar = 8f;
    [Tooltip("Độ mượt khi camera bám theo nhân vật theo chiều DỌC (lên/xuống). Thường giữ cao hơn planar để tránh camera bị 'say sóng'.")]
    public float FollowingSharpnessVertical = 15f;

    [Header("Distance / Zoom")]
    public float DefaultDistance = 6f;
    public float MinDistance = 0f;
    public float MaxDistance = 10f;
    public float DistanceMovementSpeed = 5f;
    public float DistanceMovementSharpness = 10f;
    [Tooltip("Camera lùi ra xa hơn bao nhiêu lần khi combat/di chuyển nhanh")]
    public float CombatZoomOutMultiplier = 1.4f;
    [Tooltip("Tốc độ chuyển đổi zoom khi vào/ra combat")]
    public float SpeedZoomSharpness = 3f;
    [Range(0f, 1f)]
    [Tooltip("0 = bình thường, 1 = full combat zoom-out. Set từ hệ thống combat bên ngoài.")]
    public float CombatBlend = 0f;

    [Header("Rotation")]
    public bool InvertX = false;
    public bool InvertY = false;
    [Range(-90f, 90f)]
    public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f)]
    public float MinVerticalAngle = -90f;
    [Range(-90f, 90f)]
    public float MaxVerticalAngle = 90f;
    public float RotationSpeed = 1f;
    public float RotationSharpness = 15f;
    public bool RotateWithPhysicsMover = false;

    [Header("Auto-Align (đặc trưng DMC5)")]
    [Tooltip("Bật/tắt tự động xoay camera về sau lưng nhân vật khi di chuyển")]
    public bool EnableAutoAlign = true;
    [Tooltip("Tốc độ camera tự xoay về sau lưng nhân vật")]
    public float AutoAlignSharpness = 3.5f;
    [Tooltip("Thời gian chờ sau lần input xoay tay cuối cùng trước khi bắt đầu auto-align")]
    public float AutoAlignDelay = 0.4f;
    [Tooltip("Ngưỡng tốc độ di chuyển tối thiểu để kích hoạt auto-align")]
    public float AutoAlignMinMoveSpeed = 0.5f;

    [Header("Lock-On")]
    [Tooltip("Mục tiêu đang khóa. Để null nếu không lock-on.")]
    public Transform LockOnTarget;
    public float LockOnRotationSharpness = 12f;
    [Range(0f, 1f)]
    [Tooltip("Tỉ lệ khung hình dành cho mục tiêu so với nhân vật (0.5 = ở giữa)")]
    public float LockOnFrameBias = 0.4f;

    [Header("FOV Kick & Shake")]
    public float BaseFov = 60f;
    public AnimationCurve FovKickCurve = AnimationCurve.EaseInOut(0f, 6f, 0.15f, 0f);
    public float ShakeFrequency = 25f;
    public AnimationCurve ShakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 0.25f, 0f);

    [Header("Obstruction")]
    public float ObstructionCheckRadius = 0.2f;
    public LayerMask ObstructionLayers = -1;
    public float ObstructionSharpness = 10000f;
    public List<Collider> IgnoredColliders = new List<Collider>();

    public Transform Transform { get; private set; }
    public Transform FollowTransform { get; private set; }

    public Vector3 PlanarDirection { get; set; }
    public float TargetDistance { get; set; }

    private bool _distanceIsObstructed;
    private float _currentDistance;
    private float _targetVerticalAngle;
    private int _obstructionCount;
    private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
    private Vector3 _currentFollowPosition;
    private Vector3 _lastFollowPosition;
    private Vector3 _followVelocity;

    // Auto-align
    private float _timeSinceManualRotation;

    // FOV kick
    private float _fovKickTime = -1f;

    // Shake
    private float _shakeTime = -1f;
    private float _shakeMagnitude;
    private Vector3 _shakeSeed;

    private const int MaxObstructions = 32;

    void OnValidate()
    {
        DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
        DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
    }

    void Awake()
    {
        Transform = this.transform;

        _currentDistance = DefaultDistance;
        TargetDistance = _currentDistance;

        _targetVerticalAngle = DefaultVerticalAngle;

        PlanarDirection = Vector3.forward;

        if (Camera == null) Camera = GetComponentInChildren<Camera>();
        if (Camera != null) BaseFov = Camera.fieldOfView;

        _shakeSeed = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) * 100f;
    }

    // Set the transform that the camera will orbit around
    public void SetFollowTransform(Transform t)
    {
        FollowTransform = t;
        PlanarDirection = FollowTransform.forward;
        _currentFollowPosition = FollowTransform.position;
        _lastFollowPosition = _currentFollowPosition;
    }

    /// <summary>
    /// Kích hoạt hiệu ứng FOV giật (gọi khi nhân vật ra đòn mạnh / dash / finisher)
    /// </summary>
    public void TriggerFovKick()
    {
        _fovKickTime = 0f;
    }

    /// <summary>
    /// Kích hoạt camera shake (gọi khi trúng đòn / nhận damage / impact lớn)
    /// </summary>
    public void TriggerShake(float magnitude)
    {
        _shakeTime = 0f;
        _shakeMagnitude = magnitude;
    }

    /// <summary>
    /// Bật lock-on lên một mục tiêu cụ thể
    /// </summary>
    public void SetLockOnTarget(Transform target)
    {
        LockOnTarget = target;
    }

    public void ClearLockOnTarget()
    {
        LockOnTarget = null;
    }

    /// <param name="deltaTime">Delta time của frame</param>
    /// <param name="zoomInput">Input zoom (scroll wheel, ví dụ -1..1)</param>
    /// <param name="rotationInput">Input xoay tay (x = ngang, y = dọc)</param>
    /// <param name="characterPlanarVelocity">Vận tốc di chuyển của nhân vật theo mặt phẳng (dùng cho auto-align + combat zoom). Có thể truyền Vector3.zero nếu không dùng.</param>
    public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput, Vector3 characterPlanarVelocity = default)
    {
        if (!FollowTransform) return;

        if (InvertX) rotationInput.x *= -1f;
        if (InvertY) rotationInput.y *= -1f;

        bool hasManualRotationInput = Mathf.Abs(rotationInput.x) > 0.01f || Mathf.Abs(rotationInput.y) > 0.01f;

        // ------------------------------------------------------------------
        // 1) ROTATION: input tay + auto-align + lock-on
        // ------------------------------------------------------------------
        if (hasManualRotationInput)
        {
            _timeSinceManualRotation = 0f;

            Quaternion rotationFromInput = Quaternion.Euler(FollowTransform.up * (rotationInput.x * RotationSpeed));
            PlanarDirection = rotationFromInput * PlanarDirection;
            PlanarDirection = Vector3.Cross(FollowTransform.up, Vector3.Cross(PlanarDirection, FollowTransform.up));

            _targetVerticalAngle -= (rotationInput.y * RotationSpeed);
            _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
        }
        else
        {
            _timeSinceManualRotation += deltaTime;
        }

        // Auto-align: chỉ áp dụng khi không lock-on, không có input tay, và nhân vật đang di chuyển đủ nhanh
        float planarSpeed = Vector3.ProjectOnPlane(characterPlanarVelocity, FollowTransform.up).magnitude;
        bool shouldAutoAlign = EnableAutoAlign
            && LockOnTarget == null
            && !hasManualRotationInput
            && _timeSinceManualRotation > AutoAlignDelay
            && planarSpeed > AutoAlignMinMoveSpeed;

        if (shouldAutoAlign)
        {
            Vector3 moveDir = Vector3.ProjectOnPlane(characterPlanarVelocity, FollowTransform.up).normalized;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                PlanarDirection = Vector3.Slerp(
                    PlanarDirection.normalized,
                    moveDir,
                    1f - Mathf.Exp(-AutoAlignSharpness * deltaTime));
            }
        }

        Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, FollowTransform.up);
        Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
        Quaternion desiredRotation = planarRot * verticalRot;

        // Lock-on: ép hướng nhìn để giữ cả player + target trong khung hình, blend chồng lên rotation thường
        if (LockOnTarget != null)
        {
            Vector3 midPoint = Vector3.Lerp(FollowTransform.position, LockOnTarget.position, LockOnFrameBias);
            Vector3 dirToMid = midPoint - _currentFollowPosition;
            // Giữ camera lùi ra sau nhân vật nhưng hướng nhìn xoay về phía target
            Vector3 lookDir = (LockOnTarget.position - FollowTransform.position);
            lookDir.y *= 0.35f; // giảm ảnh hưởng chênh lệch độ cao để đỡ bị "ngước" quá đà
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion lockRotation = Quaternion.LookRotation(lookDir.normalized, FollowTransform.up);
                desiredRotation = Quaternion.Slerp(desiredRotation, lockRotation, 1f); // full lock theo hướng ngang/dọc đã tính
            }
        }

        float rotSharpnessThisFrame = LockOnTarget != null ? LockOnRotationSharpness : RotationSharpness;
        Transform.rotation = Quaternion.Slerp(Transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotSharpnessThisFrame * deltaTime));

        // ------------------------------------------------------------------
        // 2) DISTANCE / ZOOM: input tay + auto combat zoom-out
        // ------------------------------------------------------------------
        if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance = _currentDistance;
        }
        if (Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance += zoomInput * DistanceMovementSpeed * deltaTime;
            TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);
        }

        // Combat/lock-on tự lùi camera ra xa hơn để bao quát trận đấu
        float effectiveCombatBlend = LockOnTarget != null ? Mathf.Max(CombatBlend, 0.5f) : CombatBlend;
        float dynamicTargetDistance = Mathf.Lerp(TargetDistance, TargetDistance * CombatZoomOutMultiplier, effectiveCombatBlend);
        dynamicTargetDistance = Mathf.Clamp(dynamicTargetDistance, MinDistance, MaxDistance);

        // ------------------------------------------------------------------
        // 3) FOLLOW POSITION: lag tách riêng planar / vertical
        // ------------------------------------------------------------------
        Vector3 targetFollow = FollowTransform.position;
        Vector3 toTarget = targetFollow - _currentFollowPosition;
        Vector3 planarDelta = Vector3.ProjectOnPlane(toTarget, FollowTransform.up);
        Vector3 vertDelta = toTarget - planarDelta;

        float planarLerp = 1f - Mathf.Exp(-FollowingSharpnessPlanar * deltaTime);
        float vertLerp = 1f - Mathf.Exp(-FollowingSharpnessVertical * deltaTime);

        _currentFollowPosition += planarDelta * planarLerp + vertDelta * vertLerp;

        // ------------------------------------------------------------------
        // 4) OBSTRUCTION
        // ------------------------------------------------------------------
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            _obstructionCount = Physics.SphereCastNonAlloc(
                _currentFollowPosition, ObstructionCheckRadius, -Transform.forward,
                _obstructions, dynamicTargetDistance, ObstructionLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < _obstructionCount; i++)
            {
                bool isIgnored = false;
                for (int j = 0; j < IgnoredColliders.Count; j++)
                {
                    if (IgnoredColliders[j] == _obstructions[i].collider)
                    {
                        isIgnored = true;
                        break;
                    }
                }

                if (!isIgnored && _obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
                {
                    closestHit = _obstructions[i];
                }
            }

            if (closestHit.distance < Mathf.Infinity)
            {
                _distanceIsObstructed = true;
                _currentDistance = Mathf.Lerp(_currentDistance, closestHit.distance, 1f - Mathf.Exp(-ObstructionSharpness * deltaTime));
            }
            else
            {
                _distanceIsObstructed = false;
                _currentDistance = Mathf.Lerp(_currentDistance, dynamicTargetDistance, 1f - Mathf.Exp(-DistanceMovementSharpness * deltaTime));
            }
        }

        // ------------------------------------------------------------------
        // 5) FINAL POSITION + FRAMING
        // ------------------------------------------------------------------
        Vector3 finalPosition = _currentFollowPosition - (Transform.rotation * Vector3.forward * _currentDistance);
        finalPosition += Transform.right * FollowPointFraming.x;
        finalPosition += Transform.up * FollowPointFraming.y;

        // ------------------------------------------------------------------
        // 6) SHAKE (offset thêm vào vị trí cuối, không ảnh hưởng logic follow/obstruction)
        // ------------------------------------------------------------------
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
                Vector3 shakeOffset = (Transform.right * nx + Transform.up * ny) * (_shakeMagnitude * falloff);
                finalPosition += shakeOffset;
                _shakeTime += deltaTime;
            }
        }

        Transform.position = finalPosition;

        // ------------------------------------------------------------------
        // 7) FOV KICK
        // ------------------------------------------------------------------
        if (Camera != null)
        {
            if (_fovKickTime >= 0f)
            {
                float lastKeyTime = FovKickCurve.length > 0 ? FovKickCurve.keys[FovKickCurve.length - 1].time : 0f;
                if (_fovKickTime > lastKeyTime)
                {
                    _fovKickTime = -1f;
                    Camera.fieldOfView = BaseFov;
                }
                else
                {
                    Camera.fieldOfView = BaseFov + FovKickCurve.Evaluate(_fovKickTime);
                    _fovKickTime += deltaTime;
                }
            }
            else
            {
                Camera.fieldOfView = BaseFov;
            }
        }

        _lastFollowPosition = _currentFollowPosition;
    }
}