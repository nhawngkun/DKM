using UnityEngine;

/// <summary>
/// MEDIATOR: nắm cả 3 module (Focus/Follow/Effects), tự chuyền dữ liệu qua lại giữa chúng
/// (rotation, air blend, follow position...). Đây là API DUY NHẤT mà player controller
/// hay combat system nên gọi vào — không gọi trực tiếp vào 3 module con.
/// 3 module con không hề biết tới nhau hay tới class này.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Modules")]
    public CameraFocus Focus;
    public CameraFollow Follow;
    public CameraEffects Effects;
    public bool RotateWithPhysicsMover;
    private Transform _followTransform;

    void Awake()
    {
        if (Focus == null) Focus = GetComponent<CameraFocus>();
        if (Follow == null) Follow = GetComponent<CameraFollow>();
        if (Effects == null) Effects = GetComponent<CameraEffects>();
    }

    /// <summary>Bắt đầu follow 1 nhân vật. Gọi 1 lần lúc spawn/switch character.</summary>
    public void SetFollowTransform(Transform target)
    {
        _followTransform = target;
        Focus.ResetPlanarDirection(target.forward);
        Follow.SnapTo(target.position);
    }

    public void SetLockOnTarget(Transform target) => Focus.SetLockOnTarget(target);
    public void ClearLockOnTarget() => Focus.ClearLockOnTarget();

    public void TriggerShake(float magnitude) => Effects.TriggerShake(magnitude);
    public void TriggerFovKick() => Effects.TriggerFovKick();
    public void TriggerSlowMotion(float scale, float duration) => Effects.TriggerSlowMotion(scale, duration);
    public void CancelSlowMotion() => Effects.CancelSlowMotion();

    /// <param name="rotationInput">x = ngang, y = dọc</param>
    /// <param name="characterPlanarVelocity">Vận tốc mặt phẳng của nhân vật</param>
    /// <param name="characterVerticalVelocity">Vận tốc trục Y của nhân vật</param>
    /// <param name="isGrounded">Nhân vật có đang đứng đất không</param>
    public void UpdateCamera(float deltaTime, float zoomInput, Vector3 rotationInput,
        Vector3 characterPlanarVelocity = default, float characterVerticalVelocity = 0f, bool isGrounded = true)
    {
        if (_followTransform == null) return;

        Vector3 followUp = _followTransform.up;
        Vector3 followPos = _followTransform.position;

        // 1) FOCUS: tính rotation trước
        Focus.UpdateRotation(deltaTime, rotationInput, characterPlanarVelocity, characterVerticalVelocity, isGrounded, followUp, followPos);

        // 2) FOLLOW: nhận rotation + airBlend từ Focus qua tham số, tự tính vị trí
        bool hasLockOn = Focus.LockOnTarget != null;
        Follow.UpdateFollow(deltaTime, zoomInput, Focus.CurrentRotation, Focus.AirBlend, hasLockOn, followPos, followUp);

        // 3) EFFECTS: áp dụng sau cùng (shake/FOV/slowmo), không làm nhiễu phép đo của Follow
        Effects.ApplyEffects(deltaTime);
    }
}