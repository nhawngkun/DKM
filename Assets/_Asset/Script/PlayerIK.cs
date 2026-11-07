using UnityEngine;

/// <summary>
/// Foot IK cho nhân vật humanoid (phải nằm CÙNG GameObject với Animator).
/// - Bám chân xuống mặt đất (bậc thang, dốc), hạ hông cho chân thấp hơn chạm đất, xoay bàn chân theo mặt phẳng.
/// - Chỉ chạy trên 1 layer duy nhất có IK Pass (mặc định layer "Idle"). Không dùng layer rỗng: ở đó
///   GetIKPosition không trả về pose chân của anim -> chỗ ghim chân bị sai (chân dang rộng, lơ lửng).
///   Nếu bật IK Pass trên nhiều layer, mỗi lần gọi sẽ đọc lại vị trí chân đã bị nâng -> chân bị cộng dồn và lơ lửng.
/// CharController tự AddComponent + gọi Init() trong Awake nếu chưa có.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerIK : MonoBehaviour
{
    [Header("Foot IK")]
    public bool EnableFootIK = true;
    [Tooltip("Tên layer Animator dùng để chạy IK (phải bật IK Pass, chỉ 1 layer). Layer phải có anim thật: " +
             "layer rỗng không cho đọc đúng pose chân của anim. Không tìm thấy thì dùng layer 0")]
    public string IKLayerName = "Idle";
    public LayerMask GroundLayers = ~0;
    public float RaycastUp = 0.5f;          // Bắn tia từ trên bàn chân bao nhiêu
    public float RaycastDown = 0.6f;        // Dò xuống dưới bàn chân bao nhiêu
    public float ToeProbeDistance = 0.18f;  // Dò thêm 1 tia trước cổ chân: mũi chân còn trên bậc thì vẫn tính là đứng trên bậc đó
    public float PelvisMaxDrop = 0.45f;     // Hông hạ tối đa (tránh gập gối quá sâu)
    public float PelvisSharpness = 10f;
    public float FootSharpness = 18f;
    public float WeightSharpness = 10f;
    [Range(0f, 1f)] public float AttackFootWeight = 0.6f;
    [Tooltip("Dốc nhỏ hơn góc này coi là đất bằng: không xoay bàn chân theo mặt đất")]
    public float FlatGroundAngle = 4f;
    [Tooltip("Khi chạy tự do: mỗi bàn chân cách đường giữa thân ít nhất bao nhiêu (m), tránh 2 chân vắt chéo. Goal IK lấy theo tỉ lệ nhân vật gốc của clip nên hẹp hơn pose thật")]
    public float RunMinFootSide = 0.08f;
    public float FootSideSharpness = 10f;
    private float _footSideWeight;

    [Header("Plant Feet (đứng yên)")]
    [Tooltip("Khi đứng yên: ghim bàn chân xuống đất, trụ là bàn chân -> hông/thân nhấp nhô khi thở, chân không bị kéo lên xuống")]
    public bool PlantFeetWhenIdle = true;
    public float PlantSharpness = 8f;
    public float PlantMaxIdleSpeed = 0.3f;  // Vận tốc dưới mức này mới coi là đứng yên
    public float PlantDelay = 0.25f;        // Chờ anim chuyển về Idle xong mới ghim
    public float PlantReleaseDistance = 0.08f; // Chân anim lệch xa chỗ ghim hơn mức này (bị đẩy/xoay/đổi anim) -> ghim lại
    public float PlantMaxLift = 0.03f;         // Chỗ ghim cao hơn chân anim quá mức này -> ghim lại (tránh chân lơ lửng)
    public float PlantReleaseAngle = 30f;

    [Header("Stair Stance (đứng yên trên bậc thang)")]
    [Tooltip("Chân đứng lệch đáy capsule quá mức này -> kéo chân về phía thân để cùng đứng một bậc, không dạng chân qua nhiều bậc")]
    public float StanceMaxHeightDiff = 0.1f;
    public float StanceMaxShift = 0.45f;    // Kéo chân về phía thân tối đa bao nhiêu (idle dạng chân ~0.95m)
    public float StanceSharpness = 8f;
    public float StanceDelay = 0.4f;
    [Tooltip("Chân phải cách đường giữa thân ít nhất bao nhiêu (m) sau khi kéo, tránh 2 chân bắt chéo")]
    public float StanceMinSideGap = 0.08f;

    private bool _stanceSolved;
    private Vector3 _leftShiftTarget;
    private Vector3 _rightShiftTarget;
    private Vector3 _leftShift;
    private Vector3 _rightShift;

    [Header("Ground Clamp (chống xuyên đất)")]
    [Tooltip("Sau Animator + IK: nếu mũi chân / đế giày thấp hơn mặt đất (trên không: thấp hơn đáy capsule) thì nâng cả model lên đúng phần bị xuyên")]
    public bool EnableGroundClamp = true;
    public float ToeMargin = 0.01f;        // mũi chân (xương toe) phải cao hơn mặt đất ít nhất
    public float AnkleMargin = 0.07f;      // cổ chân phải cao hơn mặt đất ít nhất (≈ độ dày đế giày)

    private Vector3 _modelBaseLocalPos;
    private float _clampLift;

    [Header("Head Level (ngẩng đầu)")]
    [Tooltip("Anim gốc cúi mặt xuống đất (Idle ~28°, chạy ~60°). Sau Animator, xoay ngực + đầu ngẩng lên về góc mục tiêu")]
    public bool EnableHeadLevel = true;
    public float TargetFacePitch = 5f;       // Góc mặt mong muốn (+ = nhìn xuống), 0 = nhìn thẳng
    public float MaxHeadCorrection = 45f;
    [Range(0f, 1f)] public float ChestShare = 0.3f;   // Phần ngẩng chia cho ngực (còn lại cho đầu) -> tự nhiên hơn
    [Range(0f, 1f)] public float IdleHeadWeight = 1f;
    [Range(0f, 1f)] public float MoveHeadWeight = 0.7f;
    [Range(0f, 1f)] public float AirHeadWeight = 0.5f;
    [Range(0f, 1f)] public float AttackHeadWeight = 0f;  // Combo giữ nguyên chuyển động đầu của anim
    public float HeadWeightSharpness = 6f;
    [Tooltip("Khi chạy: gập thêm lưng trên về trước (độ). Tư thế gốc của Dante ưỡn ngực hơn model Mixamo ~26°, retarget không bù nên lưng thẳng như tấm ván")]
    public float RunSpineBend = 13f;
    private float _runBendWeight;

    private Transform _head;
    private Transform _chest;
    private Vector3 _headFaceLocal = Vector3.forward; // trục "mặt" của xương đầu, lấy từ pose gốc lúc Awake
    private float _headWeight;

    private Animator _animator;
    private CharController _character;
    private PlayerBodyDynamics _bodyDynamics;
    private Collider[] _ownColliders;
    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private int _ikLayer = -1;

    private float _footWeight;
    private float _plantWeight;
    private float _slope;   // độ dốc lớn nhất dưới 2 bàn chân trong frame này (độ)
    private float _idleTime;
    private bool _planted;
    private Pose _leftPlant;    // Vị trí + hướng bàn chân đang bị ghim (world)
    private Pose _rightPlant;
    private float _pelvisOffset;
    private float _leftFootOffset;
    private float _rightFootOffset;

    public void Init(CharController character)
    {
        _character = character;
        _ownColliders = character.GetComponentsInChildren<Collider>();
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman) return;
        _bodyDynamics = GetComponent<PlayerBodyDynamics>();
        if (_bodyDynamics == null) _bodyDynamics = gameObject.AddComponent<PlayerBodyDynamics>();
        _bodyDynamics.Init(character);
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _modelBaseLocalPos = transform.localPosition;
        if (_animator != null && _animator.isHuman)
        {
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null) _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            // Lúc Awake Animator chưa chạy -> xương đang ở pose gốc, mặt nhìn thẳng về phía trước nhân vật
            if (_head != null) _headFaceLocal = _head.InverseTransformDirection(transform.forward).normalized;
        }
    }

    // bỏ phần nâng của frame trước trước khi Animator chạy: chỗ ghim chân là toạ độ world,
    // đọc IK khi model còn đang nâng thì mỗi frame chân tụt thêm rồi lại bị kẹp nâng lên (giật lên xuống)
    private void Update()
    {
        transform.localPosition = _modelBaseLocalPos;
    }

    /// <summary>Chạy sau Animator + IK: ngẩng ngực/đầu để mặt về TargetFacePitch.</summary>
    private void LateUpdate()
    {
        if (_bodyDynamics != null) _bodyDynamics.Solve();
        UpdateGroundClamp();
        UpdateHeadLevel();
    }

    /// <summary>
    /// Chống xuyên đất: anim (nhất là chạy) có lúc chúi mũi chân thấp hơn đế giày, Root Y "Based Upon Feet"
    /// không tính tới mũi chân. Đo pose cuối cùng của frame này, nâng model lên đúng phần bị xuyên
    /// (xương con đi theo ngay trong frame, không trễ). Chỉ nâng, không bao giờ hạ thấp hơn vị trí gốc.
    /// </summary>
    private void UpdateGroundClamp()
    {
        if (_animator == null || !_animator.isHuman) return;
        Vector3 up = transform.up;
        // bỏ phần nâng của frame trước để đo pose thật
        transform.localPosition = _modelBaseLocalPos;
        float target = 0f;
        // Trên không cũng kẹp theo đáy capsule: anim nhảy / air combo (bản ZeroHeight) đặt hông ngang gốc,
        // chân thòng dưới capsule ~1m -> không kẹp thì mesh cắm xuống đất trước khi capsule chạm đất
        if (EnableGroundClamp && _character != null)
        {
            float ground = Vector3.Dot(_character.transform.position, up);
            // Trên đất: so với mặt đất dưới từng bàn chân, không phải đáy capsule. Bậc thang có chân đứng
            // thấp hơn đáy capsule, kẹp theo capsule sẽ nhấc cả model lên làm chân lơ lửng
            float leftGround = FootGround(HumanBodyBones.LeftFoot, ground, up);
            float rightGround = FootGround(HumanBodyBones.RightFoot, ground, up);
            float need = 0f;
            need = Mathf.Max(need, Lift(HumanBodyBones.LeftToes, leftGround, ToeMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.RightToes, rightGround, ToeMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.LeftFoot, leftGround, AnkleMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.RightFoot, rightGround, AnkleMargin, up));
            target = need;
        }
        // Bám đúng pose của frame này (không làm mượt): hạ chậm thì chân lơ lửng rồi tụt dần xuống, trông như lún
        _clampLift = target;
        if (_clampLift > 0.0001f) transform.position += up * _clampLift;
    }

    private float FootGround(HumanBodyBones foot, float capsuleGround, Vector3 up)
    {
        if (!_character.IsGrounded) return capsuleGround;
        var t = _animator.GetBoneTransform(foot);
        if (t == null) return capsuleGround;
        return Raycast(t.position + up * RaycastUp, -up, RaycastUp + RaycastDown, out RaycastHit hit)
            ? Vector3.Dot(hit.point, up)
            : capsuleGround;
    }

    private float Lift(HumanBodyBones bone, float ground, float margin, Vector3 up)
    {
        var t = _animator.GetBoneTransform(bone);
        if (t == null) return 0f;
        return ground + margin - Vector3.Dot(t.position, up);
    }

    private void UpdateHeadLevel()
    {
        if (_head == null || _character == null) return;

        Vector3 right = transform.right;
        bool running = _character.HasMoveInput && _character.IsGrounded && !_character.IsAttacking && !_character.IsDashing;
        float planarSpeed = Vector3.ProjectOnPlane(_character.Motor.BaseVelocity, transform.up).magnitude;
        float bendTarget = running ? Mathf.InverseLerp(_character.WalkSpeed, _character.RunSpeed, planarSpeed) : 0f;
        _runBendWeight = Mathf.Lerp(_runBendWeight, bendTarget, 1f - Mathf.Exp(-HeadWeightSharpness * Time.deltaTime));
        if (_chest != null && _runBendWeight > 0.001f)
        {
            _chest.rotation = Quaternion.AngleAxis(RunSpineBend * _runBendWeight, right) * _chest.rotation;
        }

        float target = 0f;
        if (EnableHeadLevel && !_character.IsDashing)
        {
            if (_character.IsAttacking) target = AttackHeadWeight;
            else if (!_character.IsGrounded) target = AirHeadWeight;
            else target = _character.HasMoveInput ? MoveHeadWeight : IdleHeadWeight;
        }
        _headWeight = Mathf.Lerp(_headWeight, target, 1f - Mathf.Exp(-HeadWeightSharpness * Time.deltaTime));
        if (_headWeight < 0.001f) return;

        float correction = Mathf.Clamp(FacePitch(right) - TargetFacePitch, -MaxHeadCorrection, MaxHeadCorrection) * _headWeight;

        // Đang di chuyển chỉ ngẩng đầu: ngẩng cả ngực sẽ triệt tiêu dáng ngả người của anim chạy
        if (_chest != null && ChestShare > 0f && !_character.HasMoveInput)
        {
            _chest.rotation = Quaternion.AngleAxis(-correction * ChestShare, right) * _chest.rotation;
            // Đo lại sau khi ngực đã ngẩng, phần còn thiếu giao cho đầu
            correction = Mathf.Clamp(FacePitch(right) - TargetFacePitch, -MaxHeadCorrection, MaxHeadCorrection) * _headWeight;
        }
        _head.rotation = Quaternion.AngleAxis(-correction, right) * _head.rotation;
    }

    /// <returns>Góc mặt so với phương ngang (+ = nhìn xuống)</returns>
    private float FacePitch(Vector3 right)
    {
        Vector3 face = _head.TransformDirection(_headFaceLocal);
        Vector3 flat = Vector3.ProjectOnPlane(face, transform.up);
        if (flat.sqrMagnitude < 0.0001f) return 0f;
        return Vector3.SignedAngle(flat, face, right);
    }

    private void Reset()
    {
        // Mặc định bỏ qua layer Player
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            GroundLayers = ~(1 << playerLayer);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || !_animator.isHuman) return;

        if (_ikLayer < 0)
        {
            _ikLayer = _animator.GetLayerIndex(IKLayerName);
            if (_ikLayer < 0) _ikLayer = 0;
        }
        if (layerIndex != _ikLayer) return;

        UpdateFootIK(Time.deltaTime);
    }

    private void UpdateFootIK(float deltaTime)
    {
        float targetWeight = 0f;
        if (EnableFootIK && _character != null && _character.IsGrounded && !_character.IsDashing)
        {
            targetWeight = _character.IsAttacking ? AttackFootWeight : 1f;
        }
        _footWeight = Mathf.Lerp(_footWeight, targetWeight, 1f - Mathf.Exp(-WeightSharpness * deltaTime));

        bool idle = PlantFeetWhenIdle && _character != null && _character.IsGrounded
            && !_character.HasMoveInput && !_character.IsAttacking && !_character.IsDashing
            && _character.Motor.BaseVelocity.sqrMagnitude < PlantMaxIdleSpeed * PlantMaxIdleSpeed
            && OnlyIdleLayerPlaying();
        _idleTime = idle ? _idleTime + deltaTime : 0f;
        if (!idle) _planted = false;
        Vector3 up = transform.up;
        bool stanceSettled = UpdateStance(idle, up, deltaTime);
        // Chờ PlantDelay cho anim dừng/chuyển về Idle xong rồi mới chụp vị trí chân để ghim
        bool canPlant = idle && _stanceSolved && stanceSettled;
        _plantWeight = Mathf.Lerp(_plantWeight, canPlant ? 1f : 0f, 1f - Mathf.Exp(-PlantSharpness * deltaTime));

        float pelvisT = 1f - Mathf.Exp(-PelvisSharpness * deltaTime);

        if (_footWeight < 0.001f)
        {
            _pelvisOffset = Mathf.Lerp(_pelvisOffset, 0f, pelvisT);
            _leftFootOffset = 0f;
            _rightFootOffset = 0f;
            SetFootWeight(AvatarIKGoal.LeftFoot, 0f);
            SetFootWeight(AvatarIKGoal.RightFoot, 0f);
            return;
        }

        // Mặt đất nhân vật đang đứng = đáy capsule của KCC (gốc CharController)
        float rootHeight = Vector3.Dot(_character.transform.position, up);
        _slope = 0f;
        bool leftHit = SolveFoot(AvatarIKGoal.LeftFoot, _leftShift, up, rootHeight, out Vector3 leftPos, out Quaternion leftRot, out float leftDelta, out float leftGround);
        bool rightHit = SolveFoot(AvatarIKGoal.RightFoot, _rightShift, up, rootHeight, out Vector3 rightPos, out Quaternion rightRot, out float rightDelta, out float rightGround);

        float footT = 1f - Mathf.Exp(-FootSharpness * deltaTime);
        // Đã ghim thì giữ nguyên: nhịp thở làm tia dò chạm mép bậc lúc được lúc không, chân sẽ giật lên xuống
        if (!_planted)
        {
            _leftFootOffset = Mathf.Lerp(_leftFootOffset, leftHit ? leftDelta : 0f, footT);
            _rightFootOffset = Mathf.Lerp(_rightFootOffset, rightHit ? rightDelta : 0f, footT);
        }

        // Hạ hông theo chân thấp hơn để chân đó với tới đất (chỉ theo địa hình); không bao giờ nâng hông lên
        float targetPelvis = Mathf.Clamp(Mathf.Min(_leftFootOffset, _rightFootOffset, 0f), -PelvisMaxDrop, 0f);
        _pelvisOffset = Mathf.Lerp(_pelvisOffset, targetPelvis, pelvisT);

        _animator.bodyPosition += up * (_pelvisOffset * _footWeight);

        Vector3 leftTarget = leftPos + up * _leftFootOffset;
        Vector3 rightTarget = rightPos + up * _rightFootOffset;

        // Strafe / đòn đánh có bước chéo chân thật trong anim nên không kẹp
        bool keepSide = _character.HasMoveInput && !_character.IsAttacking && _character.characterAnim.towardMove;
        _footSideWeight = Mathf.Lerp(_footSideWeight, keepSide ? 1f : 0f, 1f - Mathf.Exp(-FootSideSharpness * deltaTime));
        Vector3 bodyRight = _character.transform.right;
        leftTarget = KeepFootSide(leftTarget, -bodyRight);
        rightTarget = KeepFootSide(rightTarget, bodyRight);

        // Đứng yên: ghim CHẶT bàn chân (vị trí + hướng) tại chỗ -> chân trụ đứng im,
        // nhịp thở / đung đưa của anim chỉ còn ở hông, gối và thân trên
        if (canPlant && leftHit && rightHit)
        {
            // Lệch quá xa (kể cả chỗ ghim cao hơn chân anim -> chân lơ lửng) thì ghim lại theo pose hiện tại
            bool drifted = _planted && (
                Vector3.ProjectOnPlane(leftTarget - _leftPlant.position, up).magnitude > PlantReleaseDistance ||
                Vector3.ProjectOnPlane(rightTarget - _rightPlant.position, up).magnitude > PlantReleaseDistance ||
                Vector3.Dot(_leftPlant.position - leftTarget, up) > PlantMaxLift ||
                Vector3.Dot(_rightPlant.position - rightTarget, up) > PlantMaxLift ||
                Quaternion.Angle(leftRot, _leftPlant.rotation) > PlantReleaseAngle ||
                Quaternion.Angle(rightRot, _rightPlant.rotation) > PlantReleaseAngle);
            if (!_planted || drifted)
            {
                // Nhân vật bị đẩy/xoay quá xa so với chỗ ghim -> chụp lại chỗ ghim mới
                _leftPlant = new Pose(leftTarget, leftRot);
                _rightPlant = new Pose(rightTarget, rightRot);
                _planted = true;
            }
            // Hạ dần về độ cao thấp nhất của anim (lúc bàn chân thật sự chạm đất), không bao giờ nâng lên
            _leftPlant.position = KeepLowest(_leftPlant.position, leftTarget, up);
            _rightPlant.position = KeepLowest(_rightPlant.position, rightTarget, up);
        }

        if (_planted && _plantWeight > 0.001f)
        {
            leftTarget = Vector3.Lerp(leftTarget, _leftPlant.position, _plantWeight);
            rightTarget = Vector3.Lerp(rightTarget, _rightPlant.position, _plantWeight);
            leftRot = Quaternion.Slerp(leftRot, _leftPlant.rotation, _plantWeight);
            rightRot = Quaternion.Slerp(rightRot, _rightPlant.rotation, _plantWeight);
        }

        ApplyFoot(AvatarIKGoal.LeftFoot, leftHit, leftTarget, leftRot);
        ApplyFoot(AvatarIKGoal.RightFoot, rightHit, rightTarget, rightRot);
    }

    private Vector3 KeepFootSide(Vector3 target, Vector3 side)
    {
        float missing = RunMinFootSide - Vector3.Dot(target - _character.transform.position, side);
        return missing > 0f ? target + side * (missing * _footSideWeight) : target;
    }

    /// <summary>Chỉ ghim khi anim đã về hẳn Idle: các layer phía trên (Move/Jump/Dash/Combo) không phát clip nào và không đang chuyển tiếp.</summary>
    private bool OnlyIdleLayerPlaying()
    {
        for (int l = 0; l < _animator.layerCount; l++)
        {
            if (_animator.IsInTransition(l)) return false;
            if (l > 0 && _animator.GetCurrentAnimatorClipInfoCount(l) > 0) return false;
        }
        return true;
    }

    private static Vector3 KeepLowest(Vector3 planted, Vector3 current, Vector3 up)
    {
        float diff = Vector3.Dot(current - planted, up);
        return diff < 0f ? planted + up * diff : planted;
    }

    /// <summary>Đứng yên trên thang: chọn 1 lần chỗ đặt chân gần thân hơn, chạy lại thì chân về đúng vị trí của anim.</summary>
    /// <returns>Chân đã tới chỗ mới hay chưa (chưa tới thì chưa ghim)</returns>
    private bool UpdateStance(bool idle, Vector3 up, float deltaTime)
    {
        if (!idle)
        {
            _stanceSolved = false;
            _leftShiftTarget = Vector3.zero;
            _rightShiftTarget = Vector3.zero;
        }
        // Chờ capsule dừng hẳn trên bậc: dò lúc còn đang trôi thì mốc độ cao sai, chân bị đặt nhầm bậc
        else if (!_stanceSolved && _idleTime >= StanceDelay)
        {
            _stanceSolved = true;
            // So với đáy capsule (cùng mốc với hạ hông): chân lệch ít thì hông không phải hạ sâu
            Vector3 root = _character.transform.position;
            float ground = Vector3.Dot(root, up);
            _leftShiftTarget = FindStanceShift(AvatarIKGoal.LeftFoot, root, ground, up);
            _rightShiftTarget = FindStanceShift(AvatarIKGoal.RightFoot, root, ground, up);
        }

        float t = 1f - Mathf.Exp(-StanceSharpness * deltaTime);
        _leftShift = Vector3.Lerp(_leftShift, _leftShiftTarget, t);
        _rightShift = Vector3.Lerp(_rightShift, _rightShiftTarget, t);
        return (_leftShift - _leftShiftTarget).sqrMagnitude < 0.0004f && (_rightShift - _rightShiftTarget).sqrMagnitude < 0.0004f;
    }

    private Vector3 FindStanceShift(AvatarIKGoal goal, Vector3 root, float ground, Vector3 up)
    {
        const float step = 0.05f;
        const int directions = 8;
        Vector3 foot = _animator.GetIKPosition(goal);
        Quaternion rot = _animator.GetIKRotation(goal);
        if (FootHeightDiff(foot, rot, ground, up) <= StanceMaxHeightDiff) return Vector3.zero;

        Vector3 toRoot = Vector3.ProjectOnPlane(root - foot, up).normalized;
        Vector3 forward = _character.transform.forward;
        Vector3 side = _character.transform.right * (goal == AvatarIKGoal.LeftFoot ? -1f : 1f);
        float minSide = Mathf.Min(Vector3.Dot(foot - root, side), StanceMinSideGap);
        Vector3 closest = Vector3.zero;
        float closestDiff = float.MaxValue;
        // Dò từng vòng từ gần ra xa theo mọi hướng: thang có thể chạy chéo so với hướng dạng chân,
        // lấy chỗ gần nhất đứng được để giữ dáng chân, cùng vòng thì ưu tiên hướng về phía thân
        for (float s = step; s <= StanceMaxShift; s += step)
        {
            Vector3 best = Vector3.zero;
            float bestDot = float.MinValue;
            for (int i = 0; i < directions; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(360f / directions * i, up) * forward;
                if (Vector3.Dot(foot + dir * s - root, side) < minSide) continue;
                float diff = FootHeightDiff(foot + dir * s, rot, ground, up);
                if (diff < closestDiff - 0.01f)
                {
                    closestDiff = diff;
                    closest = dir * s;
                }
                float dot = Vector3.Dot(dir, toRoot);
                if (diff <= StanceMaxHeightDiff && dot > bestDot)
                {
                    bestDot = dot;
                    best = dir * s;
                }
            }
            if (bestDot > float.MinValue) return best;
        }
        return closest;
    }

    private float FootHeightDiff(Vector3 footPos, Quaternion footRot, float ground, Vector3 up)
    {
        return ProbeFoot(footPos, footRot, up, out RaycastHit hit)
            ? Mathf.Abs(Vector3.Dot(hit.point, up) - ground)
            : float.MaxValue;
    }

    private bool ProbeFoot(Vector3 footPos, Quaternion footRot, Vector3 up, out RaycastHit hit)
    {
        Vector3 toePos = footPos + Vector3.ProjectOnPlane(footRot * Vector3.forward, up).normalized * ToeProbeDistance;
        bool ankleHit = Raycast(footPos + up * RaycastUp, -up, RaycastUp + RaycastDown, out hit);
        bool toeHit = Raycast(toePos + up * RaycastUp, -up, RaycastUp + RaycastDown, out RaycastHit toe);
        if (!ankleHit && !toeHit)
        {
            return false;
        }
        // Cổ chân nằm ngay mép bậc: tia cổ chân rơi xuống bậc dưới theo từng frame, lấy mặt cao hơn để chân không nhảy qua lại
        if (!ankleHit || (toeHit && Vector3.Dot(toe.point, up) > Vector3.Dot(hit.point, up)))
        {
            hit = toe;
        }
        return true;
    }

    private bool SolveFoot(AvatarIKGoal goal, Vector3 shift, Vector3 up, float rootHeight, out Vector3 footPos, out Quaternion footRot, out float heightDelta, out float groundHeight)
    {
        footPos = _animator.GetIKPosition(goal) + shift;
        footRot = _animator.GetIKRotation(goal);
        heightDelta = 0f;
        groundHeight = rootHeight;

        if (!ProbeFoot(footPos, footRot, up, out RaycastHit hit))
        {
            return false;
        }
        groundHeight = Vector3.Dot(hit.point, up);

        // Giữ nguyên độ cao chân của anim (đứng, nhấc chân khi bước), chỉ dời đúng phần chênh
        // giữa mặt đất dưới bàn chân và mặt đất nhân vật đang đứng (dốc, bậc thang). Đất phẳng -> 0.
        heightDelta = Vector3.Dot(hit.point, up) - rootHeight;

        footRot = Quaternion.FromToRotation(up, hit.normal) * footRot;
        _slope = Mathf.Max(_slope, Vector3.Angle(up, hit.normal));
        return true;
    }

    private void ApplyFoot(AvatarIKGoal goal, bool hasHit, Vector3 footPos, Quaternion footRot)
    {
        if (!hasHit)
        {
            SetFootWeight(goal, 0f);
            return;
        }

        _animator.SetIKPosition(goal, footPos);
        _animator.SetIKRotation(goal, footRot);
        _animator.SetIKPositionWeight(goal, _footWeight);
        // Đất bằng: không xoay bàn chân (giữ nguyên hướng của anim) -> tránh bàn chân bị bẻ gập.
        // Chỉ xoay theo mặt dốc khi dốc rõ ràng.
        _animator.SetIKRotationWeight(goal, _footWeight * Mathf.InverseLerp(FlatGroundAngle, FlatGroundAngle + 8f, _slope));
    }

    private void SetFootWeight(AvatarIKGoal goal, float weight)
    {
        _animator.SetIKPositionWeight(goal, weight);
        _animator.SetIKRotationWeight(goal, weight);
    }

    private bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, GroundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (IsOwnCollider(_hits[i].collider)) continue;
            if (_hits[i].distance < best)
            {
                best = _hits[i].distance;
                closest = _hits[i];
                found = true;
            }
        }
        return found;
    }

    private bool IsOwnCollider(Collider col)
    {
        if (_ownColliders == null) return false;
        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] == col) return true;
        }
        return false;
    }
}
