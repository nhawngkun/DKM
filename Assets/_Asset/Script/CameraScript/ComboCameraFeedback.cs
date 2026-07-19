using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Kết nối CharacterCombo (logic combo) với CameraController (cảm giác camera).
///
/// Bản nâng cấp so với bản gốc, lấy cảm hứng từ DMC5 / God of War / Sekiro / Bayonetta:
///   - Shake + FOV kick tăng dần theo tiến trình combo (giữ nguyên ý tưởng gốc).
///   - Hitstop không chỉ ở đòn CUỐI mà có thể áp dụng nhẹ ở MỌI đòn (kiểu Sekiro: mỗi
///     nhát chém đều có 1 khoảnh khắc "khựng" cực ngắn để tạo cảm giác "ăn đòn thật").
///   - Combo Chain Escalation: đánh càng liên tục (không hở nhịp) thì cường độ rung/FOV
///     càng được cộng thêm dần, giống cảm giác "được đà" trong Bayonetta/DMC.
///   - Rumble tay cầm (New Input System) đồng bộ với đòn đánh.
///   - 1 UnityEvent (OnImpactFeedback) để bắn hook ra ngoài cho VFX / Post-processing
///     (chromatic aberration pulse, vignette flash, hit-stop trắng màn hình...) hoặc SFX,
///     mà không cần sửa lại script này.
///
/// QUAN TRỌNG VỀ ANIMATION EVENT:
/// Unity Animation Event CHỈ gọi được hàm có tham số là float / int / string / Object / AnimationEvent,
/// KHÔNG gọi được hàm nhận struct tuỳ biến như ComboHitInfo. Vì vậy các hàm dùng để gắn
/// trực tiếp vào Animation Event đều có tiền tố "AnimEvent_" và chỉ nhận float hoặc không
/// tham số. Cách gắn: mở Animation window -> chọn clip -> thêm Event tại đúng frame vũ khí
/// chạm địch -> Function chọn AnimEvent_Hit -> ô Float điền tiến trình đòn (0 = đòn đầu,
/// 1 = đòn chốt combo). Với đòn chốt có thể dùng thẳng AnimEvent_Finisher() (không cần điền gì).
/// </summary>
[RequireComponent(typeof(CharacterCombo))]
public class ComboCameraFeedback : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;
    public CharacterCombo characterCombo;

    [Header("Shake theo từng đòn")]
    [Tooltip("Biên độ shake ở đòn đầu tiên của combo")]
    public float BaseShakeMagnitude = 0.08f;
    [Tooltip("Biên độ shake ở đòn CUỐI (finisher) của combo")]
    public float FinalHitShakeMagnitude = 0.22f;
    [Tooltip("Đường cong nội suy biên độ shake theo tiến trình combo (0 = đòn 1, 1 = đòn cuối)")]
    public AnimationCurve ShakeProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("FOV Kick theo từng đòn")]
    [Tooltip("Hệ số FOV kick ở đòn đầu tiên")]
    public float BaseFovKickMultiplier = 0.6f;
    [Tooltip("Hệ số FOV kick ở đòn CUỐI (finisher)")]
    public float FinalHitFovKickMultiplier = 1.6f;

    [Header("Hitstop / Impact Freeze")]
    [Tooltip("Bật hitstop cho MỌI đòn (không chỉ đòn chốt), kiểu Sekiro. Nếu tắt, chỉ đòn chốt mới có hitstop.")]
    public bool HitStopOnEveryHit = false;
    [Tooltip("Bật/tắt hiệu ứng khựng hình ở đòn chốt hạ (finisher)")]
    public bool EnableHitStopOnFinalHit = true;
    [Tooltip("Thời lượng khựng hình ở đòn thường (giây, real time). Chỉ áp dụng nếu HitStopOnEveryHit = true.")]
    public float MinHitStopDuration = 0.015f;
    [Tooltip("Thời lượng khựng hình ở đòn CHỐT (giây, real time - không bị ảnh hưởng bởi Time.timeScale)")]
    public float HitStopDuration = 0.05f;
    [Tooltip("Đường cong nội suy thời lượng hitstop theo tiến trình combo (dùng khi HitStopOnEveryHit = true)")]
    public AnimationCurve HitStopDurationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Time.timeScale trong lúc khựng hình. 0 = đứng hình hẳn, 0.05-0.1 = vẫn có 1 chút chuyển động chậm")]
    public float HitStopTimeScale = 0.02f;

    [Header("Combo Chain Escalation (Bayonetta / DMC style)")]
    [Tooltip("Bật cộng dồn cường độ khi đánh liên tục không hở nhịp")]
    public bool EnableChainEscalation = true;
    [Tooltip("Khoảng thời gian tối đa giữa 2 đòn để vẫn tính là 'liên tục' (giây)")]
    public float ChainWindowSeconds = 1.0f;
    [Tooltip("Số đòn liên tục cần để đạt bonus tối đa")]
    public int HitsForMaxChainBonus = 8;
    [Tooltip("Bonus tối đa cộng thêm vào shake/FOV kick khi đang 'ăn theo đà' (0.35 = +35%)")]
    public float MaxChainBonus = 0.35f;

    [Header("Rumble tay cầm (tuỳ chọn, New Input System)")]
    public bool EnableControllerRumble = false;
    [Tooltip("Cường độ motor tần số thấp (rung 'nặng') theo tiến trình combo, 0 = đòn đầu, 1 = đòn chốt")]
    public AnimationCurve RumbleLowFreqCurve = AnimationCurve.Linear(0f, 0.15f, 1f, 0.6f);
    [Tooltip("Cường độ motor tần số cao (rung 'sắc') theo tiến trình combo")]
    public AnimationCurve RumbleHighFreqCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 0.4f);
    public float RumbleDuration = 0.08f;
    public float FinisherRumbleDuration = 0.18f;

    [Header("Ngưỡng coi là đòn chốt khi gọi qua AnimEvent_Hit(float)")]
    [Range(0.9f, 1f)]
    public float FinisherProgressThreshold = 0.999f;

    [Header("Hook ra ngoài cho VFX / Post-processing / SFX")]
    [Tooltip("Bắn ra mỗi khi có đòn trúng, kèm cường độ chuẩn hoá 0..1 (1 = đòn chốt). Gắn hàm nhận cường độ này để làm chromatic aberration pulse, vignette flash, hit-flash trắng, SFX impact...")]
    public FloatUnityEvent OnImpactFeedback;

    [System.Serializable]
    public class FloatUnityEvent : UnityEvent<float> { }

    private Coroutine _hitStopRoutine;
    private Coroutine _rumbleRoutine;
    private float _lastHitRealtime = -999f;
    private int _chainCount = 0;
    private float _preHitStopTimeScale = 1f;

    private void Reset()
    {
        characterCombo = GetComponent<CharacterCombo>();
    }

    // An toàn: nếu object bị tắt đúng lúc đang hitstop/rumble, đảm bảo trả timeScale
    // về bình thường và tắt rumble, tránh bị kẹt game ở trạng thái slow-mo mãi mãi.
    private void OnDisable()
    {
        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = null;
            Time.timeScale = _preHitStopTimeScale;
        }
        if (_rumbleRoutine != null)
        {
            StopCoroutine(_rumbleRoutine);
            _rumbleRoutine = null;
#if ENABLE_INPUT_SYSTEM
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
#endif
        }
    }

    // GHI CHÚ: Trước đây script này tự subscribe vào characterCombo.OnAttackHit và bắn
    // hiệu ứng camera ngay lúc input được chấp nhận (lúc bấm nút, chưa chắc trúng địch).
    // Đã BỎ đường tự động này. Bây giờ hiệu ứng camera CHỈ chạy khi có Animation Event
    // gọi các hàm AnimEvent_... bên dưới, để đảm bảo khớp đúng frame vũ khí chạm địch.
    // Nếu vẫn cần Play(ComboHitInfo) để gọi tay từ nơi khác (không qua Anim Event), xem hàm bên dưới.

    /// <summary>
    /// Áp dụng hiệu ứng camera cho 1 đòn đánh cụ thể. Chỉ nên gọi thủ công từ code nếu
    /// bạn CHỦ ĐỘNG muốn bỏ qua Animation Event (ví dụ hit-detection riêng bằng code,
    /// không dùng animation). Trong luồng chuẩn (khớp frame va chạm), dùng các hàm
    /// AnimEvent_... gắn trong Animation window thay vì gọi hàm này.
    /// </summary>
    public void Play(ComboHitInfo hit)
    {
        ApplyHitFeedback(hit.Progress01, hit.IsFinalHit);
    }

    /// <summary>
    /// Hiệu ứng bổ sung riêng cho đòn chốt hạ (hitstop). Có thể gọi thủ công từ Animation Event
    /// nếu bạn không muốn dùng AnimEvent_Finisher() (tương đương nhau).
    /// </summary>
    public void PlayFinalHitExtra()
    {
        TriggerHitStop(HitStopDuration);
        TriggerRumble(1f, FinisherRumbleDuration);
    }

    // ------------------------------------------------------------------
    // CÁC HÀM DÀNH RIÊNG CHO ANIMATION EVENT
    // Gắn trực tiếp vào Animation Event (Function = tên hàm, Float = giá trị nếu cần).
    // ------------------------------------------------------------------

    /// <summary>
    /// Gọi từ Animation Event tại đúng frame vũ khí chạm địch.
    /// Float truyền vào = tiến trình combo (0 = đòn đầu tiên, 1 = đòn chốt).
    /// Ví dụ combo 4 đòn: đòn 1 = 0, đòn 2 = 0.33, đòn 3 = 0.66, đòn 4 (chốt) = 1.
    /// </summary>
    public void AnimEvent_Hit(float progress01)
    {
        bool isFinal = progress01 >= FinisherProgressThreshold;
        ApplyHitFeedback(progress01, isFinal);
    }

    /// <summary>Gọi từ Animation Event ở đòn chốt combo (finisher). Không cần tham số.</summary>
    public void AnimEvent_Finisher()
    {
        ApplyHitFeedback(1f, true);
    }

    /// <summary>Tiện ích: đòn nhẹ đầu combo (progress = 0).</summary>
    public void AnimEvent_LightHit()
    {
        ApplyHitFeedback(0f, false);
    }

    /// <summary>Tiện ích: đòn nặng giữa/cuối combo nhưng chưa phải finisher (progress = 0.75).</summary>
    public void AnimEvent_HeavyHit()
    {
        ApplyHitFeedback(0.75f, false);
    }

    // ------------------------------------------------------------------
    // LOGIC CHUNG
    // ------------------------------------------------------------------

    private void ApplyHitFeedback(float progress01, bool isFinal)
    {
        if (cameraController == null) return;

        progress01 = Mathf.Clamp01(progress01);

        // --- Chain escalation: đánh càng liên tục càng "được đà" ---
        float chainBonus = 0f;
        if (EnableChainEscalation)
        {
            float dt = Time.unscaledTime - _lastHitRealtime;
            _chainCount = dt <= ChainWindowSeconds ? _chainCount + 1 : 1;
            _lastHitRealtime = Time.unscaledTime;

            float chainT = HitsForMaxChainBonus > 0
                ? Mathf.Clamp01((float)(_chainCount - 1) / HitsForMaxChainBonus)
                : 0f;
            chainBonus = chainT * MaxChainBonus;
        }

        float t = ShakeProgressCurve.Evaluate(progress01);
        float shakeMag = Mathf.Lerp(BaseShakeMagnitude, FinalHitShakeMagnitude, t) * (1f + chainBonus);
        float fovMult = Mathf.Lerp(BaseFovKickMultiplier, FinalHitFovKickMultiplier, t) * (1f + chainBonus);

        cameraController.TriggerShake(shakeMag);
        //cameraController.TriggerFovKick(fovMult);

        // --- Hitstop ---
        if (isFinal && EnableHitStopOnFinalHit)
        {
            TriggerHitStop(HitStopDuration);
        }
        else if (!isFinal && HitStopOnEveryHit)
        {
            float dur = Mathf.Lerp(MinHitStopDuration, HitStopDuration, HitStopDurationCurve.Evaluate(progress01));
            TriggerHitStop(dur);
        }

        // --- Rumble ---
        if (isFinal)
        {
            TriggerRumble(1f, FinisherRumbleDuration);
        }
        else
        {
            TriggerRumble(progress01, RumbleDuration);
        }

        // --- Hook ra ngoài cho VFX/Post-processing/SFX ---
        OnImpactFeedback?.Invoke(isFinal ? 1f : t);

        if (isFinal)
        {
            PlayFinalHitExtraInternal();
        }
    }

    // Giữ tách riêng để PlayFinalHitExtra() (public, gọi tay) không bị double-trigger rumble/hitstop
    // khi ApplyHitFeedback đã tự lo phần đó cho trường hợp isFinal.
    private void PlayFinalHitExtraInternal()
    {
        // Chỗ này để trống / mở rộng thêm cho các hiệu ứng CHỈ dành riêng cho finisher
        // mà không muốn lặp lại nếu người dùng tự gọi PlayFinalHitExtra() thủ công từ nơi khác
        // (ví dụ: slow-mo kéo dài hơn, kích hoạt VFX finisher riêng, v.v.)
    }

    private void TriggerHitStop(float duration)
    {
        if (duration <= 0f) return;
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        _preHitStopTimeScale = Time.timeScale;
        Time.timeScale = HitStopTimeScale;

        // Dùng WaitForSecondsRealtime để thời lượng khựng hình không bị chính nó làm chậm theo
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = _preHitStopTimeScale;
        _hitStopRoutine = null;
    }

    private void TriggerRumble(float progress01, float duration)
    {
        if (!EnableControllerRumble) return;
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        if (pad == null) return;

        float low = RumbleLowFreqCurve.Evaluate(progress01);
        float high = RumbleHighFreqCurve.Evaluate(progress01);

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