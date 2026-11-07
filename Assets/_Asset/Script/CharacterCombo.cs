using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông tin về 1 đòn đánh trong combo, phát ra qua event OnAttackHit / OnAttackImpact
/// để các hệ thống khác (camera, VFX, SFX, rumble...) lắng nghe mà không cần
/// CharacterCombo phải biết/tham chiếu trực tiếp tới chúng.
/// </summary>
public struct ComboHitInfo
{
    public int SelectedCombo;   // Chuỗi combo nào đang chạy (index trong WeaponData.Chains)
    public int HitIndex;        // Đòn thứ mấy trong chuỗi (0 = đòn 1, 1 = đòn 2, ...)
    public int MaxCombo;        // Tổng số đòn của chuỗi đang chạy
    public bool IsFinalHit;     // true nếu đây là đòn chốt hạ
    public AttackContext Context;
    public float RootTravel;    // Quãng đường anim tự lướt (m) -> CharController không cần lunge thêm
    public float Weight;        // 0 = đòn nhẹ, 1 = đòn nặng nhất của vũ khí
    public float TimeToFirstHit; // Giây (đã tính Speed) từ lúc ra đòn tới nhát chém đầu -> canh lúc lướt bù

    // 0..1, dùng để nội suy độ mạnh hiệu ứng: đòn đầu nhẹ, đòn cuối mạnh nhất
    public float Progress01 => MaxCombo <= 1 ? 1f : (float)HitIndex / (MaxCombo - 1);
}

/// <summary>
/// Hệ combo theo vũ khí (data-driven bằng WeaponData).
/// - Mỗi vũ khí có nhiều chuỗi combo theo ngữ cảnh: đất, dash, trên không, hất tung, skill.
/// - Thời điểm chém / nối đòn / hủy hồi chiêu được phân tích sẵn từ chuyển động mũi kiếm của anim
///   (WeaponComboBuilder), nên không cần Animation Event trên từng clip.
/// - Anim phát bản Inplace, còn chuyển động thân người lấy từ bản có root motion và đẩy qua
///   CharController (KCC) -> bước lướt khớp đúng với động tác, vẫn có va chạm.
/// </summary>
public class CharacterCombo : MonoBehaviour
{
    /// <summary>Bắn ra mỗi khi 1 đòn đánh MỚI bắt đầu (dùng để xoay hướng, lướt...).</summary>
    public event Action<ComboHitInfo> OnAttackHit;
    /// <summary>Bắn ra đúng frame vũ khí chém (tốc độ mũi kiếm cao nhất).</summary>
    public event Action<ComboHitInfo> OnAttackImpact;
    public event Action<WeaponData> OnWeaponChanged;

    public Animator animator;

    [Header("Vũ khí")]
    public List<WeaponData> Weapons = new List<WeaponData>();
    public int CurrentWeaponIndex = 0;

    [Header("Animator")]
    public string AttackLayerName = "WeaponAttack";
    public string EmptyStateName = "Empty";
    [Tooltip("Thời gian blend về locomotion khi hết đòn (giây)")]
    public float FadeOut = 0.25f;
    [Tooltip("Blend khi bị hủy bằng dash/nhảy/di chuyển (giây)")]
    public float CancelFade = 0.1f;
    [Range(0f, 1f), Tooltip("Giữ di chuyển chỉ hủy được đòn sau mốc này (normalized), chừa thời gian bấm nối đòn")]
    public float MoveCancelMinTime = 0.75f;

    [Header("Combo")]
    [Tooltip("Chuỗi đầu tiên được chọn ngẫu nhiên (false = lần lượt)")]
    public bool RandomGroundChain = true;
    [Tooltip("Ngừng nhịp rồi mới bấm tiếp (sau khi đòn kết thúc) -> rẽ sang chuỗi khác ở đòn kế (kiểu delayed combo của DMC)")]
    public bool DelayedComboBranch = true;

    [Header("Blend nối đòn")]
    [Tooltip("Nối đòn: vào đòn kế ở frame có tư thế khớp nhất với đòn đang chém (bỏ bớt đoạn thủ thế đầu clip) thay vì luôn từ frame 0")]
    public bool PoseMatchLink = true;
    [Tooltip("Đòn có điểm nối liền mạch (tư thế trùng frame đầu đòn kế) nằm sau LinkTime không quá bấy nhiêu giây -> chờ tới điểm nối mới chuyển; xa hơn thì nối ngay ở LinkTime cho nhạy")]
    public float MaxSeamWait = 0.35f;
    [Tooltip("Bỏ tối đa bấy nhiêu giây đầu của đòn kế")]
    public float MaxEntrySkip = 0.3f;
    [Tooltip("Luôn chừa ít nhất bấy nhiêu giây thủ thế trước nhát chém đầu của đòn kế")]
    public float MinWindup = 0.08f;
    [Tooltip("Thời gian blend khi 2 tư thế gần như trùng nhau (giây)")]
    public float MinLinkFade = 0.06f;
    [Tooltip("Thời gian blend khi 2 tư thế lệch nhiều (giây)")]
    public float MaxLinkFade = 0.25f;
    [Tooltip("Độ lệch tư thế trung bình (m, đã chia humanScale) ứng với MaxLinkFade")]
    public float PoseErrorForMaxFade = 0.35f;

    private int _layer = -1;
    private int _emptyHash;

    private ComboChain _chain;
    private int _chainIndex = -1;
    private int _stepIndex;
    private AttackStep _step;
    private float _stepTime;             // giây tính theo thời gian clip
    private float _entryTime;            // frame vào đòn (giây clip), > 0 khi nối đòn có khớp tư thế
    private float _fade;                 // thời gian crossfade vào đòn hiện tại (giây)
    private int _nextImpact;
    private readonly List<ComboHitInfo> _pendingImpacts = new List<ComboHitInfo>();
    private bool _attacking;
    private float _lastStepEndTime = -999f;
    private ComboChain _lastChain;       // chuỗi vừa kết thúc (để nối tiếp khi bấm trễ)
    private int _lastStepIndex;

    private bool _hasBuffered;
    private AttackContext _bufferedContext;

    private int _groundCursor;
    private int _skillCursor;

    public WeaponData CurrentWeapon => Weapons.Count > 0 ? Weapons[Mathf.Clamp(CurrentWeaponIndex, 0, Weapons.Count - 1)] : null;
    public AttackStep CurrentStep => _attacking ? _step : null;
    public AttackContext CurrentContext => _chain != null ? _chain.Context : AttackContext.Ground;
    public float StepTime => _stepTime;
    public float StepNormalizedTime => _step != null ? _stepTime / Mathf.Max(_step.Length, 0.0001f) : 0f;
    public float CurrentFade => _fade;

    // Crossfade vào đòn hiện tại đã xong: chen crossfade mới giữa chừng thì Animator bỏ pose đang blend -> giật
    private bool FadeInDone => _stepTime - _entryTime >= _fade * _step.Speed;

    /// <summary>Đang trong 1 đòn đánh (kể cả giai đoạn hồi chiêu chờ anim chạy hết).</summary>
    public bool IsAttacking => _attacking;

    /// <summary>Đã qua mốc hủy hồi chiêu -> có thể hủy bằng di chuyển.</summary>
    public bool IsInRecovery => _attacking && !_hasBuffered && StepNormalizedTime >= Mathf.Max(_step.MoveCancelTime, MoveCancelMinTime);

    /// <summary>Đang giữa chuỗi đánh thường (kể cả trong khoảng chờ nối đòn): lần bấm tới phải nối tiếp chuỗi này.</summary>
    public bool IsContinuingGroundChain => _attacking
        ? _chain.Context == AttackContext.Ground
        : _lastChain != null && _lastChain.Context == AttackContext.Ground && CurrentWeapon != null && Time.time - _lastStepEndTime <= CurrentWeapon.ComboResetTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        _emptyHash = Animator.StringToHash(EmptyStateName);
        _layer = animator != null ? animator.GetLayerIndex(AttackLayerName) : -1;
        if (_layer < 0)
        {
            Debug.LogError($"[CharacterCombo] Animator thiếu layer '{AttackLayerName}'. Chạy Tools/Combat/Build Weapon Combos.", this);
        }
    }

    private void Start()
    {
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }

    public void Update()
    {
        if (_attacking)
        {
            TickStep(Time.deltaTime);
        }

        // Bấm trong lúc đang chém thì được giữ tới mốc nối đòn (LinkTime) của đòn hiện tại
        if (!_attacking && _hasBuffered)
        {
            _hasBuffered = false;
            Begin(_bufferedContext);
        }
    }

    /// <summary>Gọi từ CharController khi người chơi bấm đánh / skill.</summary>
    public void OnAttackInput(AttackContext context)
    {
        if (_layer < 0 || CurrentWeapon == null) return;

        _hasBuffered = true;
        _bufferedContext = context;

        // Đang hồi chiêu sau mốc nối đòn -> vào đòn kế ngay, không chờ
        if (_attacking && StepNormalizedTime >= LinkPoint(context) && FadeInDone)
        {
            _hasBuffered = false;
            Begin(context);
        }
    }

    private void TickStep(float deltaTime)
    {
        _stepTime += deltaTime * _step.Speed;
        float normalized = StepNormalizedTime;

        while (_nextImpact < _step.HitTimes.Count && normalized >= _step.HitTimes[_nextImpact])
        {
            FireImpact(_nextImpact);
            _nextImpact++;
        }

        if (_hasBuffered && normalized >= LinkPoint(_bufferedContext) && FadeInDone)
        {
            _hasBuffered = false;
            Begin(_bufferedContext);
            return;
        }

        if (normalized >= 1f)
        {
            EndAttack(FadeOut);
        }
    }

    // Nối tiếp cùng chuỗi mà đòn có điểm nối -> chờ tới điểm nối (tư thế trùng frame đầu đòn kế) thay vì cắt ngang ở LinkTime
    private float LinkPoint(AttackContext context)
    {
        bool continues = context == _chain.Context && _stepIndex + 1 < _chain.Steps.Count;
        if (!continues || _step.NextSeamTime < 0f) return _step.LinkTime;
        float linkTime = _step.LinkTime * _step.Length;
        if (_step.NextSeamTime - linkTime > MaxSeamWait) return _step.LinkTime;
        return Mathf.Max(linkTime, _step.NextSeamTime) / Mathf.Max(_step.Length, 0.0001f);
    }

    // Chọn đòn kế: nối chuỗi hiện tại nếu cùng ngữ cảnh, không thì mở chuỗi mới
    private void Begin(AttackContext context)
    {
        WeaponData weapon = CurrentWeapon;
        if (weapon == null) return;

        bool inChainWindow = _attacking || Time.time - _lastStepEndTime <= weapon.ComboResetTime;
        ComboChain chain = _attacking ? _chain : _lastChain;
        int stepIndex = _attacking ? _stepIndex : _lastStepIndex;

        if (inChainWindow && chain != null && chain.Context == context && stepIndex + 1 < chain.Steps.Count)
        {
            ComboChain next = chain;
            if (!_attacking && DelayedComboBranch && context == AttackContext.Ground)
            {
                next = PickBranch(weapon, chain, stepIndex + 1);
            }
            PlayStep(weapon, next, stepIndex + 1);
            return;
        }

        ComboChain fresh = PickChain(weapon, context, chain);
        if (fresh == null) return;
        PlayStep(weapon, fresh, 0);
    }

    private ComboChain PickChain(WeaponData weapon, AttackContext context, ComboChain previous)
    {
        List<ComboChain> candidates = new List<ComboChain>(weapon.ChainsFor(context));

        // Vũ khí không có đòn cho ngữ cảnh này -> dùng combo thường (trên không thì bỏ)
        if (candidates.Count == 0 && context != AttackContext.Air && context != AttackContext.Ground)
        {
            candidates.AddRange(weapon.ChainsFor(AttackContext.Ground));
        }
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        if (context == AttackContext.Skill)
        {
            _skillCursor = (_skillCursor + 1) % candidates.Count;
            return candidates[_skillCursor];
        }

        if (context == AttackContext.Ground && !RandomGroundChain)
        {
            _groundCursor = (_groundCursor + 1) % candidates.Count;
            return candidates[_groundCursor];
        }

        // Ngẫu nhiên nhưng tránh lặp lại chuỗi vừa đánh
        candidates.Remove(previous);
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private ComboChain PickBranch(WeaponData weapon, ComboChain current, int nextIndex)
    {
        List<ComboChain> branches = new List<ComboChain>();
        foreach (ComboChain chain in weapon.ChainsFor(AttackContext.Ground))
        {
            if (chain != current && chain.Steps.Count > nextIndex) branches.Add(chain);
        }
        return branches.Count > 0 ? branches[UnityEngine.Random.Range(0, branches.Count)] : current;
    }

    private void PlayStep(WeaponData weapon, ComboChain chain, int stepIndex)
    {
        AttackStep next = chain.Steps[stepIndex];
        float entry = 0f;
        float fade = next.FadeIn;
        if (PoseMatchLink && _attacking)
        {
            MatchPose(_step, _stepTime, next, out entry, out fade);
        }

        _chain = chain;
        _chainIndex = weapon.Chains.IndexOf(chain);
        _stepIndex = stepIndex;
        _step = next;
        _stepTime = entry;
        _entryTime = entry;
        _fade = fade;
        _nextImpact = 0;
        while (_nextImpact < _step.HitTimes.Count && _step.HitTimes[_nextImpact] * _step.Length < entry) _nextImpact++;
        _attacking = true;

        animator.CrossFadeInFixedTime(Animator.StringToHash(_step.StateName), fade, _layer, entry);

        OnAttackHit?.Invoke(BuildInfo(false));
    }

    /// <summary>
    /// Tìm frame trong đoạn thủ thế đầu của đòn kế có tư thế (vị trí + hướng vung) khớp nhất với đòn đang chém,
    /// lệch càng nhiều thì blend càng lâu.
    /// </summary>
    private void MatchPose(AttackStep from, float fromTime, AttackStep to, out float entry, out float fade)
    {
        entry = 0f;
        fade = to.FadeIn;
        if (PoseFrames(from) < 2 || PoseFrames(to) < 2) return;

        float firstHit = to.HitTimes.Count > 0 ? to.HitTimes[0] * to.Length : to.Length;
        int lastFrame = Mathf.Min(Mathf.FloorToInt(Mathf.Min(MaxEntrySkip, firstHit - MinWindup) * WeaponData.RootSampleRate), PoseFrames(to) - 1);
        int fromFrame = Mathf.Clamp(Mathf.RoundToInt(fromTime * WeaponData.RootSampleRate), 0, PoseFrames(from) - 1);

        int bestFrame = 0;
        float bestCost = PoseCost(from, fromFrame, to, 0);
        for (int f = 1; f <= lastFrame; f++)
        {
            float cost = PoseCost(from, fromFrame, to, f);
            if (cost < bestCost) { bestCost = cost; bestFrame = f; }
        }

        entry = bestFrame / WeaponData.RootSampleRate;
        fade = Mathf.Lerp(MinLinkFade, MaxLinkFade, PoseError(from, fromFrame, to, bestFrame) / Mathf.Max(PoseErrorForMaxFade, 0.0001f));
    }

    // Lệch vị trí trung bình (m, theo humanScale) giữa 2 tư thế
    private static float PoseError(AttackStep a, int fa, AttackStep b, int fb)
    {
        int bones = WeaponData.PoseBoneCount;
        float sum = 0f;
        for (int i = 0; i < bones; i++) sum += (a.PoseSamples[fa * bones + i] - b.PoseSamples[fb * bones + i]).sqrMagnitude;
        return Mathf.Sqrt(sum / bones);
    }

    private static int PoseFrames(AttackStep step)
    {
        return step.PoseSamples != null ? step.PoseSamples.Length / WeaponData.PoseBoneCount : 0;
    }

    // Lệch vị trí lúc này + một phần lệch hướng vung trong ~0.1s (để kiếm không bị giật ngược)
    private const float VelocityWeight = 0.35f;

    private static float PoseCost(AttackStep a, int fa, AttackStep b, int fb)
    {
        const int ahead = 3;
        int bones = WeaponData.PoseBoneCount;
        int pa = Mathf.Max(fa - ahead, 0);                       // đòn đang chém: chỉ biết quá khứ
        int nb = Mathf.Min(fb + ahead, PoseFrames(b) - 1);       // đòn kế: nhìn tới trước
        float da = Mathf.Max(fa - pa, 1);
        float db = Mathf.Max(nb - fb, 1);

        float cost = 0f;
        for (int i = 0; i < bones; i++)
        {
            Vector3 posA = a.PoseSamples[fa * bones + i];
            Vector3 posB = b.PoseSamples[fb * bones + i];
            Vector3 velA = (posA - a.PoseSamples[pa * bones + i]) / da;
            Vector3 velB = (b.PoseSamples[nb * bones + i] - posB) / db;
            cost += (posA - posB).sqrMagnitude + ((velA - velB) * ahead * VelocityWeight).sqrMagnitude;
        }
        return cost;
    }

    private void FireImpact(int impactIndex)
    {
        bool lastImpact = impactIndex == _step.HitTimes.Count - 1;
        ComboHitInfo info = BuildInfo(_step.IsFinisher && lastImpact);

        // Xếp hàng, phát ở FlushImpacts (sau khi Animator đã dựng pose frame này)
        _pendingImpacts.Add(info);
    }

    /// <summary>
    /// Phát các đòn chém đã tới trong frame này. Gọi từ ExamplePlayer.LateUpdate để
    /// sự kiện trùng đúng frame lưỡi kiếm vung trên màn hình. Không rung camera ở đây:
    /// rung/hitstop chỉ xảy ra khi kiếm thật sự trúng (MeleeHitDetector).
    /// </summary>
    public void FlushImpacts()
    {
        for (int i = 0; i < _pendingImpacts.Count; i++)
        {
            OnAttackImpact?.Invoke(_pendingImpacts[i]);
        }
        _pendingImpacts.Clear();
    }

    /// <summary>Nhát chém nào đang trong cửa sổ (giây clip) quanh mốc của nó, -1 = lưỡi kiếm chưa/hết nguy hiểm.</summary>
    public int ActiveHitIndex(float before, float after)
    {
        return _attacking ? ActiveIndex(_step.HitTimes, before, after) : -1;
    }

    /// <summary>Cú đá nào đang trong cửa sổ (giây clip) quanh mốc của nó, -1 = chân không nguy hiểm.</summary>
    public int ActiveKickIndex(float before, float after)
    {
        return _attacking ? ActiveIndex(_step.KickTimes, before, after) : -1;
    }

    private int ActiveIndex(List<float> times, float before, float after)
    {
        for (int i = 0; i < times.Count; i++)
        {
            float time = times[i] * _step.Length;
            if (_stepTime >= time - before && _stepTime <= time + after) return i;
        }
        return -1;
    }

    public ComboHitInfo BuildHitInfo(int hitIndex)
    {
        return BuildInfo(_step.IsFinisher && hitIndex == _step.HitTimes.Count - 1);
    }

    public ComboHitInfo BuildKickInfo()
    {
        return BuildInfo(false);
    }

    private ComboHitInfo BuildInfo(bool isFinal)
    {
        return new ComboHitInfo
        {
            SelectedCombo = _chainIndex,
            HitIndex = _stepIndex,
            MaxCombo = _chain.Steps.Count,
            IsFinalHit = isFinal,
            Context = _chain.Context,
            RootTravel = _step.RootTravel * _step.RootMotionScale,
            Weight = _step.Weight,
            TimeToFirstHit = _step.HitTimes.Count > 0 ? Mathf.Max(0f, _step.HitTimes[0] * _step.Length - _stepTime) / Mathf.Max(_step.Speed, 0.01f) : 0f,
        };
    }

    private void EndAttack(float fade)
    {
        if (!_attacking) return;

        _attacking = false;
        _lastChain = _chain;
        _lastStepIndex = _stepIndex;
        _lastStepEndTime = Time.time;
        _chain = null;
        _step = null;

        if (_layer >= 0)
        {
            animator.CrossFadeInFixedTime(_emptyHash, fade, _layer);
        }
    }

    /// <summary>
    /// Hủy combo ngay lập tức (dash/nhảy/di chuyển/tiếp đất kiểu DMC).
    /// </summary>
    public void CancelCombo()
    {
        _hasBuffered = false;
        if (!_attacking) return;

        EndAttack(CancelFade);
        _lastChain = null; // bị hủy thì lần bấm sau mở chuỗi mới
    }

    /// <summary>Di chuyển hủy phần hồi chiêu: giữ chuỗi để bấm tiếp trong ComboResetTime vẫn ra đòn kế.</summary>
    public void CancelForMove()
    {
        _hasBuffered = false;
        EndAttack(CancelFade);
    }

    /// <summary>
    /// Vận tốc root motion của đòn hiện tại (m/s, local theo hướng nhân vật lúc ra đòn: +Z = phía trước).
    /// </summary>
    public Vector3 GetRootMotionVelocity()
    {
        if (!_attacking || _step.RootSamples == null || _step.RootSamples.Length < 2) return Vector3.zero;
        if (_stepTime >= _step.Length) return Vector3.zero;

        const float h = 0.5f / WeaponData.RootSampleRate;
        Vector3 a = WeaponData.SampleVector(_step.RootSamples, _stepTime - h);
        Vector3 b = WeaponData.SampleVector(_step.RootSamples, _stepTime + h);
        Vector3 velocity = (b - a) / (2f * h) * _step.Speed * animator.humanScale * _step.RootMotionScale;

        if (!_step.UsePlanarRootMotion) { velocity.x = 0f; velocity.z = 0f; }
        if (!_step.UseVerticalRootMotion) velocity.y = 0f;
        return velocity;
    }

    /// <summary>
    /// Độ dời gốc của đòn hiện tại tính từ lúc ra đòn (m, local theo hướng nhân vật).
    /// Dùng để bám đúng độ cao của anim (đòn nhảy lên rồi đáp xuống) thay vì cộng dồn vận tốc.
    /// </summary>
    public Vector3 GetRootOffset()
    {
        if (!_attacking || _step.RootSamples == null || _step.RootSamples.Length == 0) return Vector3.zero;
        Vector3 offset = WeaponData.SampleVector(_step.RootSamples, Mathf.Min(_stepTime, _step.Length))
                       - WeaponData.SampleVector(_step.RootSamples, _entryTime);
        return offset * animator.humanScale * _step.RootMotionScale;
    }

    /// <summary>Cách cầm vũ khí theo anim của đòn hiện tại (local so với bàn tay phải).</summary>
    public bool TryGetAttackGrip(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!_attacking || _step.GripRotations == null || _step.GripRotations.Length == 0) return false;

        position = WeaponData.SampleVector(_step.GripPositions, _stepTime);
        rotation = WeaponData.SampleRotation(_step.GripRotations, _stepTime);
        return true;
    }

    public void SetWeapon(int index)
    {
        if (Weapons.Count == 0) return;
        index = (index % Weapons.Count + Weapons.Count) % Weapons.Count;
        if (index == CurrentWeaponIndex) return;

        CancelCombo();
        _lastChain = null;
        CurrentWeaponIndex = index;
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }

    public void NextWeapon(int direction = 1)
    {
        SetWeapon(CurrentWeaponIndex + direction);
    }
}
