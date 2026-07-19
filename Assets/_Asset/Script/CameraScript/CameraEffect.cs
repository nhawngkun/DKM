using UnityEngine;

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

    private float _fovKickTime = -1f;
    private float _shakeTime = -1f;
    private float _shakeMagnitude;
    private Vector3 _shakeSeed;
    private float _targetTimeScale = 1f;
    private float _slowMotionEndTime = -1f;

    void Awake()
    {
        if (Camera == null) Camera = GetComponentInChildren<Camera>();
        if (Camera != null) BaseFov = Camera.fieldOfView;
        _shakeSeed = new Vector3(Random.value, Random.value, Random.value) * 100f;
    }

    public void TriggerFovKick() => _fovKickTime = 0f;

    public void TriggerShake(float magnitude)
    {
        _shakeTime = 0f;
        _shakeMagnitude = magnitude;
    }

    public void TriggerSlowMotion(float scale, float duration)
    {
        _targetTimeScale = scale;
        _slowMotionEndTime = Time.unscaledTime + duration;
    }

    public void CancelSlowMotion()
    {
        _targetTimeScale = 1f;
        _slowMotionEndTime = -1f;
    }

    /// <summary>Gọi mỗi frame, SAU KHI CameraFollowComponent đã đặt xong Transform.position.</summary>
    public void ApplyEffects(float deltaTime)
    {
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
                _shakeTime += deltaTime;
            }
        }

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

        if (_slowMotionEndTime >= 0f && Time.unscaledTime >= _slowMotionEndTime)
        {
            _targetTimeScale = 1f;
            _slowMotionEndTime = -1f;
        }
        Time.timeScale = Mathf.Lerp(Time.timeScale, _targetTimeScale, 1f - Mathf.Exp(-SlowMotionTransitionSharpness * Time.unscaledDeltaTime));
        if (Mathf.Abs(Time.timeScale - _targetTimeScale) < 0.001f) Time.timeScale = _targetTimeScale;
    }
}