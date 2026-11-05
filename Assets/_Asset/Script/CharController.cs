using KinematicCharacterController;
using System.Collections.Generic;
using UnityEngine;

public class CharController : MonoBehaviour,ICharacterController
{
    public KinematicCharacterMotor Motor;
    public CharacterAnim characterAnim;
    [Header("Stable Movement")]
    public float StableMovementSharpness = 15f;
    public float OrientationSharpness = 10f;
    public OrientationMethod OrientationMethod = OrientationMethod.TowardsMovement;

    [Header("Locomotion (DMC)")]
    public float WalkSpeed = 2f;            // Stick đẩy nhẹ
    public float RunSpeed = 5.5f;           // Stick đẩy mạnh / bàn phím
    public float SprintSpeed = 8f;          // Đang chạy + giữ Shift (tay cầm: nhấn L3) -> tăng tốc
    public float LockOnMoveSpeed = 2.5f;    // Strafe khi lock-on
    [Range(0f, 1f)] public float WalkInputThreshold = 0.5f;

    [Header("Anim Reference Speeds (m/s bước chân của clip Inplace)")]
    [Tooltip("Phải khớp threshold của blend tree Locomotion trong Animator")]
    public float WalkClipSpeed = 1.6f;
    public float RunClipSpeed = 4.1f;
    public float RunFastClipSpeed = 5.9f;
    public float StrafeClipSpeed = 1.6f;

    [Header("Air Movement")]
    public float MaxAirMoveSpeed = 15f;
    public float AirAccelerationSpeed = 15f;
    public float Drag = 0.1f;

    [Header("Jumping")]
    public bool AllowJumpingWhenSliding = false;
    public float JumpUpSpeed = 10f;
    public float JumpScalableForwardSpeed = 10f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0f;
    public int MaxAirJumps = 1; // Số lần nhảy phụ tối đa khi đang bay (double jump = 1)
    private int _airJumpsRemaining = 0;

    [Header("Jump Feel")]
    [Tooltip("Lực nhảy phụ (double jump). <= 0 thì dùng JumpUpSpeed")]
    public float AirJumpUpSpeed = 12f;
    [Tooltip("Double jump bẻ hướng bay theo stick (DMC), tỉ lệ giữ lại vận tốc ngang cũ")]
    [Range(0f, 1f)] public float AirJumpKeepHorizontal = 0.3f;
    [Tooltip("Nhân trọng lực khi đang rơi (< 1 = rơi chậm hơn lúc bay lên)")]
    public float FallGravityMultiplier = 0.3f;
    public float MaxFallSpeed = 6.25f;
    [Tooltip("Rời mép đất lâu hơn khoảng này (giây) mới vào anim rơi (tránh giật khi xuống bậc thang)")]
    public float FallAnimDelay = 0.15f;
    private bool _fallAnimPlayed = false;
    private JumpProfile _jumpProfile; // thông số nhảy của vũ khí đang cầm (null = dùng thông số chung ở trên)
    private bool _usedAirJump = false;

    [Header("Dash")]
    public float DashSpeed = 25f;
    public float DashDuration = 0.15f;     // Thời gian duy trì lướt
    public float DashCooldown = 1f;        // Thời gian hồi chiêu
    public bool CanDashInAir = true;       // Có cho phép dash khi đang trên không không
    private bool _dashRequested = false;
    private bool _isDashing = false;
    private bool _jumpCancelledDash = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private Vector3 _dashDirection = Vector3.zero;

    [Header("Combat (DMC)")]
    public CharacterCombo characterCombo;
    public float AttackLungeSpeed = 7f;          // Tốc độ lướt tới trước mỗi khi 1 đòn mới bắt đầu
    public float AttackLungeDuration = 0.12f;
    public float AttackStopDistance = 1.6f;      // Khi lock-on: không lướt vào sát mục tiêu hơn khoảng này
    public float AttackTurnSharpness = 40f;      // Xoay gần như tức thì về hướng đánh
    public float AttackMoveCancelThreshold = 0.3f; // Lực stick tối thiểu để hủy giai đoạn hồi chiêu bằng di chuyển
    public bool CanCancelAttackWithDash = true;
    public bool CanCancelAttackWithJump = true;
    [Tooltip("Trọng lực khi đang chém trên không (DMC: nhân vật lơ lửng khi air combo)")]
    public float AirAttackGravityScale = 0.1f;
    public float AirAttackHangTime = 0.35f;
    [Tooltip("Anim tự lướt ít hơn quãng này (m) thì mới lướt thêm bằng AttackLunge (tránh lướt 2 lần)")]
    public float LungeRootTravelThreshold = 0.5f;
    [Tooltip("Độ mượt khi bám theo root motion của anim")]
    public float RootMotionSharpness = 30f;
    [Tooltip("Giới hạn tốc độ lên/xuống khi bám độ cao anim (tránh giật khi anim nhảy key)")]
    public float MaxRootVerticalSpeed = 25f;
    [Tooltip("Bấm đánh trong khoảng này (giây) sau khi dash -> ra đòn Dash Attack")]
    public float DashAttackWindow = 0.35f;
    [Range(0f, 1f)] public float DirectionalAttackThreshold = 0.6f; // Lock-on: lực stick tới/lùi để ra Stinger/Launcher

    [Tooltip("Hết đánh / hết lock-on bao lâu (giây) thì ra khỏi chế độ chiến đấu: về idle thường, cất vũ khí")]
    public float CombatHoldTime = 6f;
    private float _combatTimer = 0f;

    [Header("Lock On")]
    public float LockOnRadius = 15f;
    public float LockOnBreakRadius = 22f;

    [Header("IK")]
    public bool AutoAddIK = true;

    [Header("Misc")]
    public List<Collider> IgnoredColliders = new List<Collider>();
    public BonusOrientationMethod BonusOrientationMethod = BonusOrientationMethod.None;
    public float BonusOrientationSharpness = 10f;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public Transform MeshRoot;
    public Transform CameraFollowPoint;
    public float CrouchedCapsuleHeight = 1f;

    public CharacterState CurrentCharacterState { get; private set; }
    public LockOnTarget CurrentLockTarget { get; private set; }
    public bool IsLockedOn => CurrentLockTarget != null;
    public bool IsGrounded => Motor.GroundingStatus.IsStableOnGround;
    public bool IsDashing => _isDashing;
    public bool IsAttacking => characterCombo != null && characterCombo.IsAttacking;
    public bool HasMoveInput => _moveInputVector.sqrMagnitude > 0.0001f;
    public bool IsInCombat => _combatTimer > 0f;

    private Vector3 _cameraForward = Vector3.forward;
    private float _targetMoveSpeed = 0f;
    private bool _isSprinting = false;
    private Vector3 _attackFacing = Vector3.forward;
    private float _attackLungeTimer = 0f;
    private float _attackLungeDelay = 0f;
    private float _attackLungeScale = 1f;
    private float _attackStartHeight = 0f;
    private float _airHangTimer = 0f;
    private bool _airAttackStarted = false;
    private bool _wasAttacking = false;
    private float _lastDashEndTime = -999f;

    private Collider[] _probedColliders = new Collider[8];
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;
    private Vector3 _internalVelocityAdd = Vector3.zero;
    private bool _shouldBeCrouching = false;
    private bool _isCrouching = false;

    private void Awake()
    {
        // Handle initial state
        TransitionToState(CharacterState.Default);

        // Assign the characterController to the motor
        Motor.CharacterController = this;
        _airJumpsRemaining = MaxAirJumps;

        if (characterCombo == null)
        {
            characterCombo = GetComponentInChildren<CharacterCombo>();
        }
        if (characterCombo != null)
        {
            characterCombo.OnAttackHit += OnAttackStarted;
            characterCombo.OnWeaponChanged += OnWeaponChanged;
        }

        // IK phải nằm cùng GameObject với Animator thì OnAnimatorIK mới được gọi
        if (AutoAddIK && characterAnim != null && characterAnim.animator != null)
        {
            PlayerIK ik = characterAnim.animator.GetComponent<PlayerIK>();
            if (ik == null)
            {
                ik = characterAnim.animator.gameObject.AddComponent<PlayerIK>();
            }
            ik.Init(this);
        }
    }

    private void OnDestroy()
    {
        if (characterCombo != null)
        {
            characterCombo.OnAttackHit -= OnAttackStarted;
            characterCombo.OnWeaponChanged -= OnWeaponChanged;
        }
    }

    private void OnWeaponChanged(WeaponData weapon)
    {
        _jumpProfile = weapon != null && weapon.Jump.Enabled ? weapon.Jump : null;
        if (weapon != null) characterAnim.SetWeapon(weapon.AnimSetId, weapon.IdleId);
    }

    /// <summary>Vector (trên mặt phẳng nhân vật) từ nhân vật tới mục tiêu lock-on.</summary>
    private Vector3 PlanarToLockTarget()
    {
        return Vector3.ProjectOnPlane(CurrentLockTarget.transform.position - Motor.TransientPosition, Motor.CharacterUp);
    }

    /// <summary>Kết thúc dash: cancelled = bị nhảy hủy (anim CancelDash), ngược lại EndDash.</summary>
    private void StopDash(bool cancelled = false)
    {
        _isDashing = false;
        _lastDashEndTime = Time.time;
        if (cancelled) characterAnim.CancelDash();
        else characterAnim.EndDash();
    }

    /// <summary>Cộng lực đẩy ngoài (knockback...) đã tích lũy qua AddVelocity.</summary>
    private void ApplyInternalVelocityAdd(ref Vector3 currentVelocity)
    {
        if (_internalVelocityAdd.sqrMagnitude > 0f)
        {
            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }
    }

    /// <summary>Gọi từ ExamplePlayer.LateUpdate: phát impact của đòn chém đã tới frame này.</summary>
    public void FlushCombatFeedback()
    {
        if (characterCombo != null) characterCombo.FlushImpacts();
    }

    /// <summary>
    /// Mỗi đòn mới: xoay ngay về hướng đánh (mục tiêu lock-on > hướng đang nhìn)
    /// và lướt nhẹ tới trước giống DMC. Trên không thì treo lơ lửng một chút.
    /// </summary>
    private void OnAttackStarted(ComboHitInfo info)
    {
        Vector3 facing = Motor.CharacterForward;
        // Đòn mà anim đã tự lướt đủ xa (dash attack, skill...) hoặc trên không thì không lướt thêm
        bool animTravels = info.RootTravel >= LungeRootTravelThreshold;
        bool groundedContext = info.Context != AttackContext.Air && info.Context != AttackContext.Launcher;
        _attackLungeScale = animTravels || !groundedContext ? 0f : 1f;

        if (IsLockedOn)
        {
            Vector3 toTarget = PlanarToLockTarget();
            float distance = toTarget.magnitude;
            if (distance > 0.01f)
            {
                facing = toTarget / distance;
            }
            // Đã đứng sát mục tiêu thì không lướt thêm (tránh đẩy xuyên qua địch)
            float lungeDistance = Mathf.Max(AttackLungeSpeed * AttackLungeDuration, 0.01f);
            _attackLungeScale *= Mathf.Clamp01((distance - AttackStopDistance) / lungeDistance);
        }

        _attackFacing = facing;
        _attackLungeTimer = AttackLungeDuration;
        // Lướt bù kết thúc đúng lúc chém, không lướt trong lúc còn đang vung kiếm lên
        _attackLungeDelay = Mathf.Max(0f, info.TimeToFirstHit - AttackLungeDuration);
        _attackStartHeight = Vector3.Dot(Motor.TransientPosition, Motor.CharacterUp);

        if (info.Context == AttackContext.Launcher)
        {
            // Hất tung: anim tự đưa nhân vật lên (root motion dọc), tiếp đất sẽ chạy anim landing
            _jumpConsumed = true;
        }
        else if (!Motor.GroundingStatus.IsStableOnGround)
        {
            _airHangTimer = AirAttackHangTime;
            _airAttackStarted = true;
        }
    }

    /// <summary>
    /// Chọn ngữ cảnh ra đòn kiểu DMC:
    /// trên không = air combo; lock-on + kéo lùi = hất tung; lock-on + đẩy tới / đang chạy nhanh /
    /// vừa dash xong = dash attack; còn lại combo thường. Nút skill trên đất = skill.
    /// </summary>
    private AttackContext ResolveAttackContext(bool skill)
    {
        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            return AttackContext.Air;
        }
        if (skill)
        {
            return AttackContext.Skill;
        }

        if (IsLockedOn && _moveInputVector.sqrMagnitude > DirectionalAttackThreshold * DirectionalAttackThreshold)
        {
            Vector3 toTarget = PlanarToLockTarget().normalized;
            float dot = Vector3.Dot(_moveInputVector.normalized, toTarget);
            if (dot < -DirectionalAttackThreshold) return AttackContext.Launcher;
            if (dot > DirectionalAttackThreshold) return AttackContext.Dash;
        }

        if (_isDashing || _isSprinting || Time.time - _lastDashEndTime <= DashAttackWindow)
        {
            return AttackContext.Dash;
        }
        return AttackContext.Ground;
    }

    private void UpdateLockOn(bool lockOnHeld)
    {
        if (!lockOnHeld)
        {
            CurrentLockTarget = null;
            return;
        }

        if (CurrentLockTarget != null)
        {
            bool invalid = !CurrentLockTarget.isActiveAndEnabled
                || (CurrentLockTarget.transform.position - Motor.TransientPosition).sqrMagnitude > LockOnBreakRadius * LockOnBreakRadius;
            if (!invalid) return;
            CurrentLockTarget = null;
        }

        // Ưu tiên mục tiêu gần và nằm phía trước camera
        float bestScore = float.MaxValue;
        foreach (LockOnTarget target in LockOnTarget.All)
        {
            Vector3 toTarget = target.transform.position - Motor.TransientPosition;
            if (toTarget.sqrMagnitude > LockOnRadius * LockOnRadius) continue;

            float distance = toTarget.magnitude;
            float angle = Vector3.Angle(_cameraForward, Vector3.ProjectOnPlane(toTarget, Motor.CharacterUp));
            float score = distance * (1f + angle / 90f);
            if (score < bestScore)
            {
                bestScore = score;
                CurrentLockTarget = target;
            }
        }
    }

    /// <summary>
    /// Handles movement state transitions and enter/exit callbacks
    /// </summary>
    public void TransitionToState(CharacterState newState)
    {
        CharacterState tmpInitialState = CurrentCharacterState;
        OnStateExit(tmpInitialState, newState);
        CurrentCharacterState = newState;
        OnStateEnter(newState, tmpInitialState);
    }

    /// <summary>
    /// Event when entering a state
    /// </summary>
    public void OnStateEnter(CharacterState state, CharacterState fromState)
    {
        switch (state)
        {
            case CharacterState.Default:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// Event when exiting a state
    /// </summary>
    public void OnStateExit(CharacterState state, CharacterState toState)
    {
        switch (state)
        {
            case CharacterState.Default:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// This is called every frame by ExamplePlayer in order to tell the character what its inputs are
    /// </summary>
    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        // Clamp input
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);
        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        }
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
        _cameraForward = cameraPlanarDirection;

        UpdateLockOn(inputs.LockOn);

        // Hết đòn mà vẫn đang trên không (sau hất tung / air combo) -> về anim rơi, tiếp đất có landing
        bool attacking = IsAttacking;
        if (_wasAttacking && !attacking && !Motor.GroundingStatus.IsStableOnGround)
        {
            _jumpConsumed = true;
            characterAnim.Fall();
        }
        _wasAttacking = attacking;

        _combatTimer = attacking || IsLockedOn || inputs.Attack || inputs.Skill ? CombatHoldTime : _combatTimer - Time.deltaTime;
        characterAnim.inCombat = IsInCombat;

        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Move and look inputs
                    _moveInputVector = cameraPlanarRotation * moveInputVector;

                    // Blend tree di chuyển là 2D (Move_X/Move_Y) theo hướng CỦA NHÂN VẬT,
                    // nên phải đổi input (đang theo camera) sang local space của nhân vật
                    Vector3 localMove = Quaternion.Inverse(Motor.TransientRotation) * _moveInputVector;
                    characterAnim.isMove = moveInputVector.sqrMagnitude > 0f;
                    characterAnim._moveX = localMove.x;
                    characterAnim._moveY = localMove.z;
                    UpdateLocomotionSpeed(moveInputVector.magnitude, inputs.Sprint);

                    switch (OrientationMethod)
                    {
                        case OrientationMethod.TowardsCamera:
                            _lookInputVector = cameraPlanarDirection;
                            break;
                        case OrientationMethod.TowardsMovement:
                            _lookInputVector = _moveInputVector.normalized;
                            break;
                    }

                    // Tấn công: đẩy input sang CharacterCombo (combo tự xếp hàng đòn kế tiếp)
                    if (characterCombo != null)
                    {
                        if (inputs.WeaponSwitch != 0)
                        {
                            characterCombo.NextWeapon(inputs.WeaponSwitch);
                        }

                        if (inputs.Attack || inputs.Skill)
                        {
                            AttackContext context = ResolveAttackContext(inputs.Skill && !inputs.Attack);
                            if (context == AttackContext.Dash && _isDashing)
                            {
                                // Đánh ngay trong lúc dash -> dừng lướt, chuyển thẳng sang dash attack
                                StopDash();
                            }
                            characterCombo.OnAttackInput(context);
                        }
                    }

                    // Hồi chiêu sau đòn cuối (DMC): đẩy stick là hủy để di chuyển ngay
                    if (IsAttacking && characterCombo.IsInRecovery && moveInputVector.magnitude > AttackMoveCancelThreshold)
                    {
                        characterCombo.CancelCombo();
                    }

                    // Jumping input
                    if (inputs.JumpDown)
                    {
                        bool canJump = !IsAttacking || CanCancelAttackWithJump;
                        if (canJump)
                        {
                            if (IsAttacking)
                            {
                                characterCombo.CancelCombo();
                            }
                            // Nhảy hủy dash ngay (không chờ hết dash mới bật lên)
                            if (_isDashing && (Motor.GroundingStatus.IsStableOnGround || _airJumpsRemaining > 0))
                            {
                                StopDash(cancelled: true);
                                _jumpCancelledDash = true;
                            }
                            // Anim chỉ chạy khi nhảy thật sự (UpdateVelocity), tránh trigger thừa khi hết lượt nhảy
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }
                    }

                    if (inputs.Dash)
                    {
                        // Chỉ cho dash nếu hết cooldown, chưa đang dash, và (nếu đang trên không thì phải cho phép dash trên không)
                        bool groundedOk = Motor.GroundingStatus.IsStableOnGround || CanDashInAir;
                        bool attackOk = !IsAttacking || CanCancelAttackWithDash;

                        if (!_isDashing && _dashCooldownTimer <= 0f && groundedOk && attackOk)
                        {
                            if (IsAttacking)
                            {
                                characterCombo.CancelCombo();
                            }
                            _dashRequested = true;
                        }
                    }

                    if (inputs.OrientationSwitch)
                    {
                        SwitchOrientation();
                    }

                    // Crouching input
                    if (inputs.CrouchDown)
                    {
                        _shouldBeCrouching = true;

                        if (!_isCrouching)
                        {
                            _isCrouching = true;
                            Motor.SetCapsuleDimensions(Motor.Capsule.radius,CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                            MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                        }
                    }
                    else if (inputs.CrouchUp)
                    {
                        _shouldBeCrouching = false;
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// Chọn tốc độ đi / chạy / chạy nhanh theo lực stick và thời gian chạy liên tục,
    /// rồi đẩy tốc độ THẬT sang Animator để blend Walk/Run/RunFast và chỉnh tốc độ phát anim
    /// (anim là Inplace) -> bước chân khớp mặt đất, không bị trượt.
    /// </summary>
    private void UpdateLocomotionSpeed(float stick, bool sprintHeld)
    {
        bool strafing = IsLockedOn || OrientationMethod == OrientationMethod.TowardsCamera;

        _isSprinting = false;
        if (stick < 0.01f)
        {
            _targetMoveSpeed = 0f;
        }
        else if (strafing)
        {
            _targetMoveSpeed = LockOnMoveSpeed;
        }
        else if (stick < WalkInputThreshold)
        {
            _targetMoveSpeed = WalkSpeed;
        }
        else
        {
            _isSprinting = sprintHeld;
            _targetMoveSpeed = sprintHeld ? SprintSpeed : RunSpeed;
        }

        float speed = Vector3.ProjectOnPlane(Motor.BaseVelocity, Motor.CharacterUp).magnitude;
        characterAnim.towardMove = !strafing;
        characterAnim.moveSpeed = speed;

        // Hệ số phát anim: nhanh hơn clip nhanh nhất / chậm hơn clip chậm nhất thì tua anim cho khớp
        float animSpeed = 1f;
        if (strafing)
        {
            animSpeed = speed / Mathf.Max(StrafeClipSpeed, 0.01f);
        }
        else if (speed > RunFastClipSpeed)
        {
            animSpeed = speed / RunFastClipSpeed;
        }
        else if (speed < WalkClipSpeed)
        {
            animSpeed = speed / Mathf.Max(WalkClipSpeed, 0.01f);
        }
        characterAnim.moveAnimSpeed = Mathf.Clamp(animSpeed, 0.6f, 1.8f);
    }

    public void SwitchOrientation()
    {
        OrientationMethod = OrientationMethod == OrientationMethod.TowardsCamera
            ? OrientationMethod.TowardsMovement
            : OrientationMethod.TowardsCamera;
    }

    /// <summary>
    /// This is called every frame by the AI script in order to tell the character what its inputs are
    /// </summary>
    public void SetInputs(ref AICharacterInputs inputs)
    {
        _moveInputVector = inputs.MoveVector;
        _lookInputVector = inputs.LookVector;
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called before the character begins its movement update
    /// </summary>
    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its rotation should be right now. 
    /// This is the ONLY place where you should set the character's rotation
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Ưu tiên hướng nhìn: đang đánh > đang dash (không lock) > lock-on > input
                    Vector3 desiredLook = _lookInputVector;
                    float sharpness = OrientationSharpness;
                    if (IsAttacking)
                    {
                        desiredLook = _attackFacing;
                        sharpness = AttackTurnSharpness;
                    }
                    else if (_isDashing && !IsLockedOn)
                    {
                        desiredLook = _dashDirection;
                        sharpness = AttackTurnSharpness;
                    }
                    else if (IsLockedOn)
                    {
                        Vector3 toTarget = PlanarToLockTarget();
                        if (toTarget.sqrMagnitude > 0.01f)
                        {
                            desiredLook = toTarget.normalized;
                        }
                    }

                    if (desiredLook.sqrMagnitude > 0f && sharpness > 0f)
                    {
                        // Smoothly interpolate from current to target look direction
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, desiredLook, 1 - Mathf.Exp(-sharpness * deltaTime)).normalized;

                        // Set the current rotation (which will be used by the KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                    }

                    Vector3 currentUp = (currentRotation * Vector3.up);
                    float bonusT = 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime);
                    if (BonusOrientationMethod == BonusOrientationMethod.TowardsGroundSlopeAndGravity && Motor.GroundingStatus.IsStableOnGround)
                    {
                        Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);

                        Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, bonusT);
                        currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                        // Move the position to create a rotation around the bottom hemi center instead of around the pivot
                        Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
                    }
                    else
                    {
                        // Xoay up hiện tại về ngược trọng lực (None: về Vector3.up)
                        Vector3 targetUp = BonusOrientationMethod == BonusOrientationMethod.None ? Vector3.up : -Gravity.normalized;
                        Vector3 smoothedUp = Vector3.Slerp(currentUp, targetUp, bonusT);
                        currentRotation = Quaternion.FromToRotation(currentUp, smoothedUp) * currentRotation;
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its velocity should be right now. 
    /// This is the ONLY place where you can set the character's velocity
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Giảm dần cooldown dash mỗi frame
                    if (_dashCooldownTimer > 0f)
                    {
                        _dashCooldownTimer -= deltaTime;
                    }

                    // Bắt đầu dash nếu được yêu cầu
                    if (_dashRequested && !_isDashing)
                    {
                        _dashRequested = false;
                        _isDashing = true;
                        _dashTimer = DashDuration;
                        _dashCooldownTimer = DashCooldown;

                        // Hướng dash: theo input di chuyển, nếu không có input thì theo hướng đang nhìn
                        Vector3 dashDir = _moveInputVector.sqrMagnitude > 0.01f
                            ? _moveInputVector.normalized
                            : Motor.CharacterForward;

                        _dashDirection = dashDir;

                        characterAnim.Dash(); // gọi animation dash (thêm hàm này trong CharacterAnim nếu chưa có)
                    }

                    // Nếu đang dash, ghi đè hoàn toàn velocity, bỏ qua di chuyển/trọng lực bình thường
                    if (_isDashing)
                    {
                        currentVelocity = _dashDirection * DashSpeed;

                        _dashTimer -= deltaTime;
                        if (_dashTimer <= 0f)
                        {
                            StopDash();
                        }

                        // Vẫn cho phép lực đẩy ngoài (knockback...) cộng vào trong lúc dash
                        ApplyInternalVelocityAdd(ref currentVelocity);

                        break; // Bỏ qua toàn bộ logic ground/air movement + jump bên dưới trong lúc đang dash
                    }

                    if (IsAttacking)
                    {
                        UpdateAttackVelocity(ref currentVelocity, deltaTime);
                    }
                    // Ground movement
                    else if (Motor.GroundingStatus.IsStableOnGround)
                    {
                        float currentVelocityMagnitude = currentVelocity.magnitude;

                        Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                        currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                        Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                        // Tốc độ do UpdateLocomotionSpeed chọn (đi/chạy/chạy nhanh/strafe), không nhân lực stick nữa
                        Vector3 targetMovementVelocity = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _targetMoveSpeed;

                        currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
                    }
                    else
                    {
                        if (_moveInputVector.sqrMagnitude > 0f)
                        {
                            Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;
                            Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                            if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                            {
                                Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                                addedVelocity = newTotal - currentVelocityOnInputsPlane;
                            }
                            else
                            {
                                if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                                {
                                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                                }
                            }

                            if (Motor.GroundingStatus.FoundAnyGround)
                            {
                                if (Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                                {
                                    Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpenticularObstructionNormal);
                                }
                            }

                            currentVelocity += addedVelocity;
                        }

                        currentVelocity += GetAirGravity(currentVelocity) * deltaTime;
                        currentVelocity *= (1f / (1f + (Drag * deltaTime)));

                        float maxFall = _jumpProfile != null ? _jumpProfile.MaxFallSpeed : MaxFallSpeed;
                        float fallSpeed = Vector3.Dot(currentVelocity, Gravity.normalized);
                        if (fallSpeed > maxFall)
                        {
                            currentVelocity -= Gravity.normalized * (fallSpeed - maxFall);
                        }
                    }

                    _jumpedThisFrame = false;
                    _timeSinceJumpRequested += deltaTime;
                    if (_jumpRequested)
                    {
                        bool canGroundJump = !_jumpConsumed &&
                            ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                             || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime);

                        bool canAirJump = !canGroundJump && !Motor.GroundingStatus.IsStableOnGround && _airJumpsRemaining > 0;

                        if (canGroundJump || canAirJump)
                        {
                            Vector3 jumpDirection = Motor.CharacterUp;
                            if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                            {
                                jumpDirection = Motor.GroundingStatus.GroundNormal;
                            }

                            Motor.ForceUnground();

                            float upSpeed = _jumpProfile != null ? _jumpProfile.UpSpeed : JumpUpSpeed;
                            if (_jumpCancelledDash)
                            {
                                // Bỏ bớt quán tính dash, không thì bay quá xa
                                Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
                                currentVelocity += Vector3.ClampMagnitude(planar, MaxAirMoveSpeed) - planar;
                                _jumpCancelledDash = false;
                            }
                            if (canAirJump)
                            {
                                // Double jump: bẻ hướng theo stick, giữ lại một phần quán tính cũ
                                Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
                                Vector3 steered = _moveInputVector * Mathf.Max(planar.magnitude, _targetMoveSpeed);
                                currentVelocity += Vector3.Lerp(steered, planar, AirJumpKeepHorizontal) - planar;
                                if (_jumpProfile != null) upSpeed = _jumpProfile.AirUpSpeed;
                                else if (AirJumpUpSpeed > 0f) upSpeed = AirJumpUpSpeed;
                            }

                            currentVelocity += (jumpDirection * upSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                            currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
                            _jumpRequested = false;
                            _jumpConsumed = true;
                            _jumpedThisFrame = true;
                            _fallAnimPlayed = false;
                            _usedAirJump = canAirJump;

                            if (canAirJump)
                            {
                                _airJumpsRemaining--;
                                characterAnim.AirJump();
                            }
                            else
                            {
                                characterAnim.Jump();
                            }
                        }
                    }

                    ApplyInternalVelocityAdd(ref currentVelocity);
                    break;
                }
        }
    }

    /// <summary>
    /// Vận tốc khi đang đánh: anim phát bản Inplace, còn bước lướt / bật nhảy của động tác
    /// lấy từ root motion của bản anim gốc (CharacterCombo.GetRootMotionVelocity) nên chân
    /// không trượt. Đòn đứng yên thì lướt ngắn bù (AttackLunge). Trên không thì treo lơ lửng (air combo).
    /// </summary>
    private void UpdateAttackVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector3 up = Motor.CharacterUp;
        Vector3 verticalVelocity = Vector3.Project(currentVelocity, up);
        Vector3 planarVelocity = currentVelocity - verticalVelocity;

        Vector3 rootVelocity = Motor.TransientRotation * characterCombo.GetRootMotionVelocity();
        Vector3 rootVertical = Vector3.Project(rootVelocity, up);
        Vector3 rootPlanar = rootVelocity - rootVertical;

        // Lock-on: không để anim đẩy xuyên qua mục tiêu
        if (IsLockedOn)
        {
            Vector3 toTarget = PlanarToLockTarget();
            float distance = toTarget.magnitude;
            if (distance > 0.01f && distance < AttackStopDistance)
            {
                Vector3 dir = toTarget / distance;
                float approach = Vector3.Dot(rootPlanar, dir);
                if (approach > 0f) rootPlanar -= dir * approach;
            }
        }

        if (_attackLungeDelay <= 0f && _attackLungeTimer > 0f)
        {
            planarVelocity = _attackFacing * AttackLungeSpeed * _attackLungeScale + rootPlanar;
            _attackLungeTimer -= deltaTime;
        }
        else
        {
            if (_attackLungeDelay > 0f) _attackLungeDelay -= deltaTime;
            planarVelocity = Vector3.Lerp(planarVelocity, rootPlanar, 1f - Mathf.Exp(-RootMotionSharpness * deltaTime));
        }

        // Đòn hất tung: bám đúng độ cao của anim (so với lúc ra đòn) thay vì cộng dồn vận tốc,
        // nên anim đáp xuống thì nhân vật cũng về mặt đất, không bị treo lơ lửng
        if (characterCombo.CurrentStep != null && characterCombo.CurrentStep.UseVerticalRootMotion)
        {
            float targetHeight = _attackStartHeight + characterCombo.GetRootOffset().y;
            float verticalSpeed = (targetHeight - Vector3.Dot(Motor.TransientPosition, up)) / Mathf.Max(deltaTime, 0.0001f);
            verticalSpeed = Mathf.Clamp(verticalSpeed, -MaxRootVerticalSpeed, MaxRootVerticalSpeed);

            if (verticalSpeed > 0.01f && Motor.GroundingStatus.IsStableOnGround)
            {
                Motor.ForceUnground();
            }
            if (!Motor.GroundingStatus.IsStableOnGround || verticalSpeed > 0.01f)
            {
                currentVelocity = planarVelocity + up * verticalSpeed;
                return;
            }
        }

        if (Motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity = Motor.GetDirectionTangentToSurface(planarVelocity, Motor.GroundingStatus.GroundNormal) * planarVelocity.magnitude;
            return;
        }

        if (_airAttackStarted)
        {
            // Đòn đầu trên không: xóa vận tốc rơi để nhân vật "khựng" lại giữa không trung
            _airAttackStarted = false;
            if (Vector3.Dot(verticalVelocity, up) < 0f)
            {
                verticalVelocity = Vector3.zero;
            }
        }

        float gravityScale = _airHangTimer > 0f ? AirAttackGravityScale : 1f;
        _airHangTimer -= deltaTime;
        verticalVelocity += Gravity * gravityScale * deltaTime;
        currentVelocity = planarVelocity + verticalVelocity;
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called after the character has finished its movement update
    /// </summary>
    public void AfterCharacterUpdate(float deltaTime)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    // Handle jumping pre-ground grace period
                    if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                    {
                        _jumpRequested = false;
                        _jumpCancelledDash = false;
                    }

                    if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                    {
                        // If we're on a ground surface, reset jumping values
                        if (!_jumpedThisFrame)
                        {
                            _jumpConsumed = false;
                        }
                        _timeSinceLastAbleToJump = 0f;
                        _airJumpsRemaining = MaxAirJumps; // reset lượt nhảy phụ khi vừa chạm đất
                    }
                    else
                    {
                        _timeSinceLastAbleToJump += deltaTime;

                        // Đi khỏi mép (không nhảy) -> anim rơi, tiếp đất có landing
                        if (!_jumpConsumed && !_fallAnimPlayed && !IsAttacking && !_isDashing && _timeSinceLastAbleToJump > FallAnimDelay)
                        {
                            _fallAnimPlayed = true;
                            characterAnim.Fall();
                        }
                    }

                    // Handle uncrouching
                    if (_isCrouching && !_shouldBeCrouching)
                    {
                        // Do an overlap test with the character's standing height to see if there are any obstructions
                        Motor.SetCapsuleDimensions(Motor.Capsule.radius,2f, 1f);
                        if (Motor.CharacterOverlap(
                            Motor.TransientPosition,
                            Motor.TransientRotation,
                            _probedColliders,
                            Motor.CollidableLayers,
                            QueryTriggerInteraction.Ignore) > 0)
                        {
                            // If obstructions, just stick to crouching dimensions
                            Motor.SetCapsuleDimensions(Motor.Capsule.radius,CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                        }
                        else
                        {
                            // If no obstructions, uncrouch
                            MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                            _isCrouching = false;
                        }
                    }
                    break;
                }
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // Handle landing and leaving ground
        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLanded();
        }
        else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLeaveStableGround();
        }
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return !IgnoredColliders.Contains(coll);
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        characterAnim.OnGround(true);
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void AddVelocity(Vector3 velocity)
    {
        switch (CurrentCharacterState)
        {
            case CharacterState.Default:
                {
                    _internalVelocityAdd += velocity;
                    break;
                }
        }
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    protected void OnLanded()
    {
        // Air combo chạm đất thì dừng ngay (DMC), chuyển sang anim tiếp đất
        if (IsAttacking && characterCombo.CurrentContext == AttackContext.Air)
        {
            characterCombo.CancelCombo();
            _jumpConsumed = true;
        }
        bool wasAirborne = _jumpConsumed || _fallAnimPlayed;
        _fallAnimPlayed = false;
        _usedAirJump = false;
        if (!wasAirborne) return;
        characterAnim.OnLanding();
    }

    protected void OnLeaveStableGround()
    {
        characterAnim.OnGround(false);
    }

    /// <summary>Trọng lực trên không: theo JumpProfile của vũ khí (nhảy đất / double jump), lúc rơi nhân thêm hệ số rơi.</summary>
    private Vector3 GetAirGravity(Vector3 velocity)
    {
        bool falling = Vector3.Dot(velocity, Gravity.normalized) > 0f;
        if (_jumpProfile == null) return Gravity * (falling ? FallGravityMultiplier : 1f);

        float rise = _usedAirJump ? _jumpProfile.AirRiseGravity : _jumpProfile.RiseGravity;
        float fallMultiplier = _usedAirJump ? _jumpProfile.AirFallGravityMultiplier : _jumpProfile.FallGravityMultiplier;
        return Gravity.normalized * rise * (falling ? fallMultiplier : 1f);
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
}

public enum CharacterState
{
    Default,
}

public enum OrientationMethod
{
    TowardsCamera,
    TowardsMovement,
}

public struct PlayerCharacterInputs
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
    public bool CrouchDown;
    public bool CrouchUp;
    public bool OrientationSwitch;
    public bool Dash;
    public bool Attack;
    public bool Skill;
    public int WeaponSwitch; // -1 / +1 = đổi vũ khí trước / sau
    public bool LockOn; // Giữ nút = lock-on
    public bool Sprint; // Giữ nút khi đang chạy = chạy nhanh
}

public struct AICharacterInputs
{
    public Vector3 MoveVector;
    public Vector3 LookVector;
}

public enum BonusOrientationMethod
{
    None,
    TowardsGravity,
    TowardsGroundSlopeAndGravity,
}
