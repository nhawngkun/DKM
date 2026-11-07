using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;

/// <summary>
/// Quán tính toàn thân bằng Final IK: đổ người theo gia tốc, ngả theo vận tốc khi bay, nhún gối khi tiếp đất,
/// đầu nhìn trước vào hướng cua. Tay giữ nguyên anim (không map IK). Bàn chân giữ nguyên chỗ PlayerIK đã đặt (ghim bằng effector)
/// nên hông đổ được mà chân không trượt. PlayerIK gọi Solve() trong LateUpdate, trước kẹp đất và ngẩng đầu.
/// </summary>
[RequireComponent(typeof(FullBodyBipedIK))]
public class PlayerBodyDynamics : MonoBehaviour
{
    [Header("Đổ người khi chạy")]
    [Tooltip("Độ nghiêng trên mỗi m/s² gia tốc")]
    public float LeanPerAcceleration = 1.1f;
    public float MaxSideLean = 20f;
    public float MaxForwardLean = 14f;
    public float MaxBackLean = 16f;
    [Tooltip("Ngả thêm về trước khi chạy hết tốc độ (độ)")]
    public float RunForwardLean = 5f;
    [Range(0f, 1f)] public float ChestLeanShare = 0.35f;
    public float AccelerationFilter = 9f;
    public float LeanFrequency = 11f;
    [Range(0.1f, 1f)] public float LeanDamping = 0.55f;

    [Header("Trên không")]
    [Tooltip("Độ ngả trên mỗi m/s vận tốc ngang")]
    public float AirLeanPerSpeed = 1.3f;
    public float MaxAirLean = 20f;

    [Header("Tiếp đất")]
    [Tooltip("Vận tốc nhún (m/s) trên mỗi m/s tốc độ rơi")]
    public float LandImpulse = 0.45f;
    [Tooltip("Vận tốc gập người (độ/s) trên mỗi m/s tốc độ rơi")]
    public float LandPitchImpulse = 15f;
    public float LandMaxDrop = 0.35f;
    public float LandFrequency = 14f;
    [Range(0.1f, 1f)] public float LandDamping = 0.5f;


    [Header("Đầu")]
    public float MaxHeadYaw = 40f;
    [Range(0f, 1f)] public float ChestYawShare = 0.3f;
    public float HeadYawSharpness = 10f;
    [Range(0f, 1f)] public float HeadCounterRoll = 0.45f;

    [Header("Solver")]
    public float WeightSharpness = 12f;
    public float GroundWeightSharpness = 14f;

    private const float MaxSpringDeltaTime = 1f / 30f;

    private FullBodyBipedIK _ik;
    private CharController _character;
    private Transform _pelvis;
    private Transform _chest;
    private Transform _head;

    private float _weight;
    private float _groundWeight = 1f;
    private bool _wasGrounded = true;
    private float _fallSpeed;
    private Vector3 _filteredVelocity;
    private Vector3 _acceleration;
    private Vector2 _lean;       // x = đổ trước, y = đổ phải (độ)
    private Vector2 _leanVelocity;
    private float _landOffset;
    private float _landVelocity;
    private float _headYaw;

    public void Init(CharController character)
    {
        _character = character;
        _ik = GetComponent<FullBodyBipedIK>();

        BipedReferences references = _ik.references;
        if (references == null || !references.isFilled)
        {
            BipedReferences.AutoDetectReferences(ref references, transform, BipedReferences.AutoDetectParams.Default);
            _ik.SetReferences(references, null);
        }

        // Giải tay trong PlayerIK để đúng thứ tự: Animator IK -> Final IK -> kẹp đất -> ngẩng đầu -> vũ khí, áo
        _ik.enabled = false;
        _ik.fixTransforms = false;
        IKSolverFullBodyBiped solver = _ik.solver;
        solver.iterations = 2;
        solver.SetChainWeights(FullBodyBipedChain.LeftLeg, 1f, 0f);
        solver.SetChainWeights(FullBodyBipedChain.RightLeg, 1f, 0f);
        // tay đi theo vai đúng như anim: để solver giải tay thì khuỷu bị bẻ tới 79° mỗi khi hông đổ
        solver.SetChainWeights(FullBodyBipedChain.LeftArm, 0f, 0f);
        solver.SetChainWeights(FullBodyBipedChain.RightArm, 0f, 0f);
        solver.leftArmMapping.weight = 0f;
        solver.rightArmMapping.weight = 0f;
        solver.OnPreUpdate = ApplyBeforeSolve;
        solver.OnPostUpdate = ApplyAfterSolve;

        _pelvis = references.pelvis;
        _head = references.head;
        Animator animator = GetComponent<Animator>();
        _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        _filteredVelocity = character.Motor.BaseVelocity;
    }

    public void Solve()
    {
        if (_ik == null) return;

        float deltaTime = Time.deltaTime;
        UpdateMotion(deltaTime);

        bool usable = !_character.IsAttacking && !_character.IsDashing;
        _weight = Mathf.Lerp(_weight, usable ? 1f : 0f, 1f - Mathf.Exp(-WeightSharpness * deltaTime));
        if (_weight < 0.001f)
        {
            _ik.solver.IKPositionWeight = 0f;
            return;
        }

        _ik.solver.IKPositionWeight = _weight;
        _ik.solver.Update();
    }

    private void UpdateMotion(float deltaTime)
    {
        Vector3 up = _character.Motor.CharacterUp;
        Vector3 velocity = _character.Motor.BaseVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(velocity, up);
        bool grounded = _character.IsGrounded;

        // dốc/thang: tốc độ ngang tụt theo độ dốc dù vẫn chạy đều, đo theo mặt đất để không bị coi là phanh
        if (grounded) planar = planar.normalized * velocity.magnitude;

        // đạo hàm qua bộ lọc: KCC đổi vận tốc theo bước vật lý, chia cho deltaTime thì gia tốc giật từng frame
        Vector3 filteredPlanar = Vector3.ProjectOnPlane(_filteredVelocity, up);
        _acceleration = (planar - filteredPlanar) * AccelerationFilter;
        _filteredVelocity = Vector3.Lerp(filteredPlanar, planar, 1f - Mathf.Exp(-AccelerationFilter * deltaTime));

        if (!grounded)
        {
            _fallSpeed = Mathf.Max(_fallSpeed, -Vector3.Dot(velocity, up));
        }
        else if (!_wasGrounded)
        {
            _landVelocity -= _fallSpeed * LandImpulse;
            _leanVelocity.x += _fallSpeed * LandPitchImpulse;
            _fallSpeed = 0f;
        }
        _wasGrounded = grounded;
        _groundWeight = Mathf.Lerp(_groundWeight, grounded ? 1f : 0f, 1f - Mathf.Exp(-GroundWeightSharpness * deltaTime));

        Vector3 forward = _character.transform.forward;
        Vector3 right = _character.transform.right;
        Vector2 target = grounded
            ? GroundLean(planar, forward, right)
            : Vector2.ClampMagnitude(new Vector2(Vector3.Dot(planar, forward), Vector3.Dot(planar, right)) * AirLeanPerSpeed, MaxAirLean);

        // lò xo hơi non: vào cua đổ quá tay một chút rồi về, nhìn có trọng lượng hơn làm mượt thuần
        float springDelta = Mathf.Min(deltaTime, MaxSpringDeltaTime);
        Vector2 leanForce = (target - _lean) * (LeanFrequency * LeanFrequency) - _leanVelocity * (2f * LeanDamping * LeanFrequency);
        _leanVelocity += leanForce * springDelta;
        _lean += _leanVelocity * springDelta;

        float landForce = -_landOffset * (LandFrequency * LandFrequency) - _landVelocity * (2f * LandDamping * LandFrequency);
        _landVelocity += landForce * springDelta;
        _landOffset += _landVelocity * springDelta;
        if (_landOffset < -LandMaxDrop)
        {
            _landOffset = -LandMaxDrop;
            _landVelocity = Mathf.Max(_landVelocity, 0f);
        }

        Vector3 moveDirection = _character.MoveInputDirection;
        float targetYaw = 0f;
        if (grounded && moveDirection.sqrMagnitude > 0.01f)
        {
            targetYaw = Mathf.Clamp(Vector3.SignedAngle(forward, moveDirection, up), -MaxHeadYaw, MaxHeadYaw);
        }
        _headYaw = Mathf.Lerp(_headYaw, targetYaw, 1f - Mathf.Exp(-HeadYawSharpness * deltaTime));
    }

    private Vector2 GroundLean(Vector3 planar, Vector3 forward, Vector3 right)
    {
        float speed = planar.magnitude;
        float sideScale = Mathf.InverseLerp(_character.WalkSpeed, _character.RunSpeed, speed);
        float pitch = Mathf.Clamp(Vector3.Dot(_acceleration, forward) * LeanPerAcceleration, -MaxBackLean, MaxForwardLean);
        float side = Mathf.Clamp(Vector3.Dot(_acceleration, right) * LeanPerAcceleration, -MaxSideLean, MaxSideLean) * sideScale;
        pitch += RunForwardLean * Mathf.InverseLerp(_character.RunSpeed * 0.5f, _character.SprintSpeed, speed);
        return new Vector2(pitch, side);
    }

    private void ApplyBeforeSolve()
    {
        IKSolverFullBodyBiped solver = _ik.solver;
        Vector3 up = _character.Motor.CharacterUp;
        Transform root = _character.transform;

        // ghim trước khi đổ người: effector giữ bàn chân ở pose PlayerIK vừa đặt
        PinFoot(solver.leftFootEffector, _ik.references.leftFoot);
        PinFoot(solver.rightFootEffector, _ik.references.rightFoot);

        Quaternion tilt = Quaternion.AngleAxis(_lean.x * _weight, root.right) * Quaternion.AngleAxis(-_lean.y * _weight, root.forward);
        Vector3 pivot = Vector3.Lerp(_pelvis.position, root.position, _groundWeight);
        _pelvis.SetPositionAndRotation(pivot + tilt * (_pelvis.position - pivot), tilt * _pelvis.rotation);
        if (_chest != null)
        {
            _chest.rotation = Quaternion.Slerp(Quaternion.identity, tilt, ChestLeanShare) * _chest.rotation;
        }

        solver.bodyEffector.positionOffset += up * _landOffset;

    }

    private void PinFoot(IKEffector effector, Transform foot)
    {
        effector.position = foot.position;
        effector.rotation = foot.rotation;
        effector.positionWeight = _groundWeight;
        effector.rotationWeight = _groundWeight;
    }

    private void ApplyAfterSolve()
    {
        Transform root = _character.transform;
        Vector3 up = _character.Motor.CharacterUp;
        float yaw = _headYaw * _weight;

        if (_chest != null)
        {
            _chest.rotation = Quaternion.AngleAxis(yaw * ChestYawShare, up) * _chest.rotation;
        }
        if (_head != null)
        {
            float headYaw = _chest != null ? yaw * (1f - ChestYawShare) : yaw;
            _head.rotation = Quaternion.AngleAxis(headYaw, up)
                * Quaternion.AngleAxis(_lean.y * _weight * HeadCounterRoll, root.forward)
                * _head.rotation;
        }
    }
}
