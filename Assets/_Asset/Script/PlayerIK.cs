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
    public float PelvisMaxDrop = 0.45f;     // Hông hạ tối đa (tránh gập gối quá sâu)
    public float PelvisSharpness = 10f;
    public float FootSharpness = 18f;
    public float WeightSharpness = 10f;
    [Range(0f, 1f)] public float AttackFootWeight = 0.6f;
    [Tooltip("Dốc nhỏ hơn góc này coi là đất bằng: không xoay bàn chân theo mặt đất")]
    public float FlatGroundAngle = 4f;

    [Header("Plant Feet (đứng yên)")]
    [Tooltip("Khi đứng yên: ghim bàn chân xuống đất, trụ là bàn chân -> hông/thân nhấp nhô khi thở, chân không bị kéo lên xuống")]
    public bool PlantFeetWhenIdle = true;
    public float PlantSharpness = 8f;
    public float PlantMaxIdleSpeed = 0.3f;  // Vận tốc dưới mức này mới coi là đứng yên
    public float PlantDelay = 0.25f;        // Chờ anim chuyển về Idle xong mới ghim
    public float PlantReleaseDistance = 0.08f; // Chân anim lệch xa chỗ ghim hơn mức này (bị đẩy/xoay/đổi anim) -> ghim lại
    public float PlantMaxLift = 0.03f;         // Chỗ ghim cao hơn chân anim quá mức này -> ghim lại (tránh chân lơ lửng)
    public float PlantReleaseAngle = 30f;

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

    private Transform _head;
    private Transform _chest;
    private Vector3 _headFaceLocal = Vector3.forward; // trục "mặt" của xương đầu, lấy từ pose gốc lúc Awake
    private float _headWeight;

    private Animator _animator;
    private CharController _character;
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

    /// <summary>Chạy sau Animator + IK: ngẩng ngực/đầu để mặt về TargetFacePitch.</summary>
    private void LateUpdate()
    {
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
            float need = 0f;
            need = Mathf.Max(need, Lift(HumanBodyBones.LeftToes, ground, ToeMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.RightToes, ground, ToeMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.LeftFoot, ground, AnkleMargin, up));
            need = Mathf.Max(need, Lift(HumanBodyBones.RightFoot, ground, AnkleMargin, up));
            target = need;
        }
        // Bám đúng pose của frame này (không làm mượt): hạ chậm thì chân lơ lửng rồi tụt dần xuống, trông như lún
        _clampLift = target;
        if (_clampLift > 0.0001f) transform.position += up * _clampLift;
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

        float target = 0f;
        if (EnableHeadLevel && !_character.IsDashing)
        {
            if (_character.IsAttacking) target = AttackHeadWeight;
            else if (!_character.IsGrounded) target = AirHeadWeight;
            else target = _character.HasMoveInput ? MoveHeadWeight : IdleHeadWeight;
        }
        _headWeight = Mathf.Lerp(_headWeight, target, 1f - Mathf.Exp(-HeadWeightSharpness * Time.deltaTime));
        if (_headWeight < 0.001f) return;

        Vector3 right = transform.right;
        float correction = Mathf.Clamp(FacePitch(right) - TargetFacePitch, -MaxHeadCorrection, MaxHeadCorrection) * _headWeight;

        if (_chest != null && ChestShare > 0f)
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
        // Chờ PlantDelay cho anim dừng/chuyển về Idle xong rồi mới chụp vị trí chân để ghim
        bool canPlant = idle && _idleTime >= PlantDelay;
        _plantWeight = Mathf.Lerp(_plantWeight, canPlant ? 1f : 0f, 1f - Mathf.Exp(-PlantSharpness * deltaTime));

        Vector3 up = transform.up;
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
        bool leftHit = SolveFoot(AvatarIKGoal.LeftFoot, up, rootHeight, out Vector3 leftPos, out Quaternion leftRot, out float leftDelta, out float leftGround);
        bool rightHit = SolveFoot(AvatarIKGoal.RightFoot, up, rootHeight, out Vector3 rightPos, out Quaternion rightRot, out float rightDelta, out float rightGround);

        float footT = 1f - Mathf.Exp(-FootSharpness * deltaTime);
        _leftFootOffset = Mathf.Lerp(_leftFootOffset, leftHit ? leftDelta : 0f, footT);
        _rightFootOffset = Mathf.Lerp(_rightFootOffset, rightHit ? rightDelta : 0f, footT);

        // Hạ hông theo chân thấp hơn để chân đó với tới đất (chỉ theo địa hình); không bao giờ nâng hông lên
        float targetPelvis = Mathf.Clamp(Mathf.Min(_leftFootOffset, _rightFootOffset, 0f), -PelvisMaxDrop, 0f);
        _pelvisOffset = Mathf.Lerp(_pelvisOffset, targetPelvis, pelvisT);

        _animator.bodyPosition += up * (_pelvisOffset * _footWeight);

        Vector3 leftTarget = leftPos + up * _leftFootOffset;
        Vector3 rightTarget = rightPos + up * _rightFootOffset;

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

    private bool SolveFoot(AvatarIKGoal goal, Vector3 up, float rootHeight, out Vector3 footPos, out Quaternion footRot, out float heightDelta, out float groundHeight)
    {
        footPos = _animator.GetIKPosition(goal);
        footRot = _animator.GetIKRotation(goal);
        heightDelta = 0f;
        groundHeight = rootHeight;

        if (!Raycast(footPos + up * RaycastUp, -up, RaycastUp + RaycastDown, out RaycastHit hit))
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
