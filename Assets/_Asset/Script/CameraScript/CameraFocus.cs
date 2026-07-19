using UnityEngine;

/// <summary>
/// Module ĐỘC LẬP: chỉ tính toán HƯỚNG NHÌN camera (rotation).
/// Không tự đọc/ghi Transform.position, không tham chiếu tới Follow hay Effects.
/// CameraController sẽ gọi UpdateRotation() và lấy kết quả qua CurrentRotation, AirBlend
/// để truyền tiếp cho CameraFollowComponent.
/// </summary>
public class CameraFocus : MonoBehaviour
{
    [Header("Rotation")]
    public bool InvertX = false;
    public bool InvertY = false;
    [Range(-90f, 90f)] public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f)] public float MinVerticalAngle = -90f;
    [Range(-90f, 90f)] public float MaxVerticalAngle = 90f;
    public float RotationSpeed = 1f;
    public float RotationSharpness = 15f;

    [Header("Auto-Align (đặc trưng DMC5)")]
    public bool EnableAutoAlign = true;
    public float AutoAlignSharpness = 3.5f;
    public float AutoAlignDelay = 0.4f;
    public float AutoAlignMinMoveSpeed = 0.5f;

    [Header("Air Pitch (cúi xuống khi rơi)")]
    public bool EnableAirCamera = true;
    public float AirMaxVerticalSpeed = 14f;
    public float AirMaxPitchOffset = 18f;
    public AnimationCurve AirPitchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float AirBlendSharpness = 8f;
    [Range(0f, 1f)] public float AirLockOnDampen = 0.35f;

    [Header("Lock-On")]
    public Transform LockOnTarget;
    public float LockOnRotationSharpness = 12f;

    [Header("Debug")]
    public bool DrawDebugGizmos = true;

    /// <summary>Kết quả rotation sau khi tính xong frame này. CameraController đọc giá trị này.</summary>
    public Quaternion CurrentRotation { get; private set; }

    /// <summary>Blend 0..1 khi đang trên không. CameraController truyền cho Follow để tăng sharpness/zoom.</summary>
    public float AirBlend { get; private set; }

    public Vector3 PlanarDirection { get; set; } = Vector3.forward;

    private float _targetVerticalAngle;
    private float _timeSinceManualRotation;
    private float _currentAirPitchOffset;
    private Transform _debugFollowRef; // chỉ để vẽ gizmo, không phải dependency thật

    void Awake()
    {
        _targetVerticalAngle = DefaultVerticalAngle;
        CurrentRotation = transform.rotation;
    }

    void OnValidate()
    {
        DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
    }

    /// <summary>Gọi 1 lần khi bắt đầu follow 1 nhân vật mới, để PlanarDirection khởi tạo đúng hướng.</summary>
    public void ResetPlanarDirection(Vector3 forward)
    {
        PlanarDirection = forward;
    }

    public void SetLockOnTarget(Transform target) => LockOnTarget = target;
    public void ClearLockOnTarget() => LockOnTarget = null;

    /// <summary>
    /// Tính rotation mới dựa trên input + trạng thái nhân vật. Không đọc/ghi bất kỳ component nào khác.
    /// </summary>
    /// <param name="followUp">Trục up của nhân vật đang follow (thường là Vector3.up)</param>
    /// <param name="followPosition">Vị trí nhân vật đang follow (dùng cho lock-on)</param>
    public void UpdateRotation(float deltaTime, Vector3 rotationInput,
        Vector3 characterPlanarVelocity, float characterVerticalVelocity, bool isGrounded,
        Vector3 followUp, Vector3 followPosition)
    {
        _debugFollowRef = null; // không giữ reference lâu dài, chỉ dùng tạm cho phép tính trong hàm này

        if (InvertX) rotationInput.x *= -1f;
        if (InvertY) rotationInput.y *= -1f;

        bool hasManualRotationInput = Mathf.Abs(rotationInput.x) > 0.01f || Mathf.Abs(rotationInput.y) > 0.01f;
        bool isAirborne = EnableAirCamera && !isGrounded;

        if (hasManualRotationInput)
        {
            _timeSinceManualRotation = 0f;

            Quaternion rotationFromInput = Quaternion.Euler(followUp * (rotationInput.x * RotationSpeed));
            PlanarDirection = rotationFromInput * PlanarDirection;
            PlanarDirection = Vector3.Cross(followUp, Vector3.Cross(PlanarDirection, followUp));

            _targetVerticalAngle -= (rotationInput.y * RotationSpeed);
            _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
        }
        else
        {
            _timeSinceManualRotation += deltaTime;
        }

        float planarSpeed = Vector3.ProjectOnPlane(characterPlanarVelocity, followUp).magnitude;
        bool shouldAutoAlign = EnableAutoAlign
            && LockOnTarget == null
            && !hasManualRotationInput
            && _timeSinceManualRotation > AutoAlignDelay
            && planarSpeed > AutoAlignMinMoveSpeed;

        if (shouldAutoAlign)
        {
            Vector3 moveDir = Vector3.ProjectOnPlane(characterPlanarVelocity, followUp).normalized;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                PlanarDirection = Vector3.Slerp(
                    PlanarDirection.normalized, moveDir,
                    1f - Mathf.Exp(-AutoAlignSharpness * deltaTime));
            }
        }

        float targetPitch = 0f;
        float targetAirBlend = 0f;
        if (isAirborne)
        {
            if (characterVerticalVelocity < 0f)
            {
                float fallRatio = Mathf.Clamp01(-characterVerticalVelocity / AirMaxVerticalSpeed);
                targetPitch = AirPitchCurve.Evaluate(fallRatio) * AirMaxPitchOffset;
            }
            targetAirBlend = 1f;

            if (LockOnTarget != null)
            {
                targetPitch *= AirLockOnDampen;
                targetAirBlend *= AirLockOnDampen;
            }
        }

        _currentAirPitchOffset = Mathf.Lerp(_currentAirPitchOffset, targetPitch, 1f - Mathf.Exp(-AirBlendSharpness * deltaTime));
        AirBlend = Mathf.Lerp(AirBlend, targetAirBlend, 1f - Mathf.Exp(-AirBlendSharpness * deltaTime));

        float effectiveVerticalAngle = Mathf.Clamp(_targetVerticalAngle + _currentAirPitchOffset, MinVerticalAngle, MaxVerticalAngle);

        Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, followUp);
        Quaternion verticalRot = Quaternion.Euler(effectiveVerticalAngle, 0, 0);
        Quaternion desiredRotation = planarRot * verticalRot;

        if (LockOnTarget != null)
        {
            Vector3 lookDir = (LockOnTarget.position - followPosition);
            lookDir.y *= 0.35f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion lockRotation = Quaternion.LookRotation(lookDir.normalized, followUp);
                desiredRotation = Quaternion.Slerp(desiredRotation, lockRotation, 1f);
            }
        }

        float rotSharpnessThisFrame = LockOnTarget != null ? LockOnRotationSharpness : RotationSharpness;
        CurrentRotation = Quaternion.Slerp(CurrentRotation, desiredRotation, 1f - Mathf.Exp(-rotSharpnessThisFrame * deltaTime));
    }
}