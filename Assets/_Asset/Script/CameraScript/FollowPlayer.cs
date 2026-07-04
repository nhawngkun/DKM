
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class FollowPlayer : MonoBehaviour
{

    [SerializeField] private Transform cam;
    [SerializeField] private CameraCollider _cameraCollider;
    [SerializeField] private Transform target;
    [SerializeField] public Transform po;
    [SerializeField] private Transform p1;
    [SerializeField] private float smooth_speed;
    [SerializeField] private float speedRotate;
    [SerializeField] private float minY;
    [SerializeField] private float maxY;
    //private Transform trans;
    [SerializeField] float mouseSensitivity = 3f;


    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        transform.position = Vector3.Lerp(transform.position, target.position, smooth_speed);

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float h = mouseDelta.x;
        float v = mouseDelta.y;

        // Scale giống touch (có thể tweak thêm multiplier nếu cần)
        float yawDelta = h * speedRotate * mouseSensitivity;
        float pitchDelta = -v * speedRotate * mouseSensitivity;

        // Rotate
        transform.Rotate(0f, yawDelta, 0f, Space.World);
        po.Rotate(pitchDelta, 0f, 0f, Space.Self);

        // Clamp pitch
        Vector3 euler = transform.localEulerAngles;
        Vector3 eulerPo = po.localEulerAngles;

        eulerPo.x = ClampSigned(eulerPo.x, minY, maxY);

        transform.localEulerAngles = new Vector3(0f, euler.y, 0f);
        po.localEulerAngles = new Vector3(eulerPo.x, 0f, 0f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float ClampSigned(float angle, float min, float max)
    {
        // 0..360 -> -180..180 rồi mới clamp để tránh nhảy góc
        angle = (angle > 180f) ? angle - 360f : angle;
        return Mathf.Clamp(angle, min, max);
    }

    public void SetTarget(Transform _target)
    {
        target = _target;
        _cameraCollider.SetTarget(_target);
    }
}