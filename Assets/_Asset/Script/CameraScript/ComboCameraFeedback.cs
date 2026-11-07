using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cảm giác camera khi kiếm THẬT SỰ va chạm. MeleeHitDetector gọi vào đây:
///   - PlayHit: chém trúng địch -> rung + hitstop theo độ nặng đòn; đòn chốt có thêm góc máy cinematic.
///   - PlaySlam: đập xuống đất -> rung nặng + hitstop + xung hậu kỳ.
/// Vung kiếm vào khoảng không thì không có gì xảy ra, anim cũng không tự rung camera nữa.
/// </summary>
[RequireComponent(typeof(CharacterCombo))]
public class ComboCameraFeedback : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;

    [Header("Shake khi chém trúng")]
    [Tooltip("Biên độ shake ở đòn nhẹ / đầu combo")]
    public float BaseShakeMagnitude = 0.08f;
    [Tooltip("Biên độ shake ở đòn nặng nhất / đòn chốt")]
    public float FinalHitShakeMagnitude = 0.22f;

    [Header("Hitstop khi chém trúng")]
    [Tooltip("Thời lượng khựng ở đòn nhẹ nhất (giây thực)")]
    public float MinHitStopDuration = 0.035f;
    [Tooltip("Thời lượng khựng ở đòn nặng nhất / đòn chốt (giây thực)")]
    public float HitStopDuration = 0.09f;
    [Range(0f, 1f)]
    [Tooltip("Time.timeScale trong lúc khựng hình. 0 = đứng hình hẳn, 0.05-0.1 = vẫn có 1 chút chuyển động chậm")]
    public float HitStopTimeScale = 0.02f;

    [Header("Đập xuống đất")]
    public float SlamShakeMagnitude = 0.3f;
    public float SlamHitStopDuration = 0.07f;
    [Range(0f, 1f)] public float SlamPulse = 0.6f;

    [Header("Camera cinematic ở đòn chốt")]
    public bool EnableCinematic = true;
    [Tooltip("Chuỗi đánh phải dài ít nhất bấy nhiêu đòn mới có cinematic (skill luôn có)")]
    public int CinematicMinCombo = 3;
    [Tooltip("Không lặp lại trong bấy nhiêu giây thực, tránh chóng mặt")]
    public float CinematicCooldown = 2.5f;
    [Range(0.05f, 1f)] public float CinematicTimeScale = 0.3f;
    [Tooltip("Thời lượng slow motion đi kèm (giây thực)")]
    public float CinematicSlowDuration = 0.4f;

    [Header("Combo Chain Escalation (Bayonetta / DMC style)")]
    [Tooltip("Khoảng thời gian tối đa giữa 2 lần trúng để vẫn tính là 'liên tục' (giây)")]
    public float ChainWindowSeconds = 1.0f;
    [Tooltip("Số lần trúng liên tục cần để đạt bonus tối đa")]
    public int HitsForMaxChainBonus = 8;
    [Tooltip("Bonus tối đa cộng thêm vào shake (0.35 = +35%)")]
    public float MaxChainBonus = 0.35f;

    [Header("Rumble tay cầm (tuỳ chọn, New Input System)")]
    public bool EnableControllerRumble = false;
    [Tooltip("Cường độ motor tần số thấp (rung 'nặng') theo độ mạnh đòn")]
    public AnimationCurve RumbleLowFreqCurve = AnimationCurve.Linear(0f, 0.15f, 1f, 0.6f);
    [Tooltip("Cường độ motor tần số cao (rung 'sắc') theo độ mạnh đòn")]
    public AnimationCurve RumbleHighFreqCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 0.4f);
    public float RumbleDuration = 0.08f;
    public float FinisherRumbleDuration = 0.18f;

    [Header("Hook ra ngoài cho VFX / Post-processing / SFX")]
    [Tooltip("Bắn ra mỗi khi có đòn trúng, kèm cường độ chuẩn hoá 0..1 (1 = đòn chốt)")]
    public FloatUnityEvent OnImpactFeedback;

    [System.Serializable]
    public class FloatUnityEvent : UnityEvent<float> { }

    private Coroutine _rumbleRoutine;
    private float _lastHitRealtime = -999f;
    private float _lastCinematicRealtime = -999f;
    private int _chainCount;

    private void OnDisable()
    {
        if (_rumbleRoutine == null) return;
        StopCoroutine(_rumbleRoutine);
        _rumbleRoutine = null;
#if ENABLE_INPUT_SYSTEM
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
#endif
    }

    /// <summary>Kiếm vừa chém trúng địch. Gọi 1 lần cho mỗi nhát, dù trúng nhiều mục tiêu.</summary>
    public void PlayHit(ComboHitInfo hit, Vector3 focus, Vector3 attackDirection)
    {
        if (cameraController == null) return;

        float strength = HitStrength(hit);
        float dt = Time.unscaledTime - _lastHitRealtime;
        _chainCount = dt <= ChainWindowSeconds ? _chainCount + 1 : 1;
        _lastHitRealtime = Time.unscaledTime;
        float chainT = HitsForMaxChainBonus > 0 ? Mathf.Clamp01((float)(_chainCount - 1) / HitsForMaxChainBonus) : 0f;

        cameraController.TriggerShake(Mathf.Lerp(BaseShakeMagnitude, FinalHitShakeMagnitude, strength) * (1f + chainT * MaxChainBonus));
        cameraController.TriggerHitStop(Mathf.Lerp(MinHitStopDuration, HitStopDuration, strength), HitStopTimeScale);
        TriggerRumble(strength, hit.IsFinalHit ? FinisherRumbleDuration : RumbleDuration);
        OnImpactFeedback?.Invoke(strength);

        if (ShouldPlayCinematic(hit))
        {
            _lastCinematicRealtime = Time.unscaledTime;
            cameraController.TriggerSlowMotion(CinematicTimeScale, CinematicSlowDuration);
            cameraController.TriggerCinematic(focus, attackDirection);
        }
    }

    /// <summary>Kiếm vừa đập xuống đất.</summary>
    public void PlaySlam()
    {
        if (cameraController == null) return;

        cameraController.TriggerShake(SlamShakeMagnitude);
        cameraController.TriggerHitStop(SlamHitStopDuration, HitStopTimeScale);
        cameraController.TriggerImpactPulse(SlamPulse);
        TriggerRumble(1f, FinisherRumbleDuration);
        OnImpactFeedback?.Invoke(1f);
    }

    // đòn chốt luôn mạnh nhất; đòn thường bị chặn dưới 1 để luôn còn khoảng cách với đòn chốt
    public static float HitStrength(ComboHitInfo hit)
    {
        return hit.IsFinalHit ? 1f : Mathf.Clamp01(Mathf.Max(hit.Weight * 0.6f, hit.Progress01 * 0.8f));
    }

    private bool ShouldPlayCinematic(ComboHitInfo hit)
    {
        if (!EnableCinematic || !hit.IsFinalHit) return false;
        if (hit.MaxCombo < CinematicMinCombo && hit.Context != AttackContext.Skill) return false;
        return Time.unscaledTime - _lastCinematicRealtime >= CinematicCooldown;
    }

    private void TriggerRumble(float strength, float duration)
    {
        if (!EnableControllerRumble) return;
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        if (pad == null) return;

        float low = RumbleLowFreqCurve.Evaluate(strength);
        float high = RumbleHighFreqCurve.Evaluate(strength);

        if (_rumbleRoutine != null) StopCoroutine(_rumbleRoutine);
        _rumbleRoutine = StartCoroutine(RumbleRoutine(pad, low, high, duration));
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float duration)
    {
        pad.SetMotorSpeeds(low, high);
        yield return new WaitForSecondsRealtime(duration);
        pad.SetMotorSpeeds(0f, 0f);
        _rumbleRoutine = null;
    }
#endif
}
