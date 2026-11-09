 using KinematicCharacterController;
using System.Collections.Generic;
using UnityEngine;

// Di chuyển dựa trên root motion: trên đất vận tốc lấy từ chính anim nên chân không trượt, chỉ xoay + trọng lực là do code.
// Anim one-shot (khởi động, quay, dừng, né) bake góc xoay trong pose; hết clip motor xoay bù đúng góc đó rồi vào vòng lặp ngay.
public class CharController : MonoBehaviour, ICharacterController
{
    internal const int GaitWalk = 1;
    internal const int GaitJog = 2;
    internal const int GaitRun = 3;
    internal const int GaitFast = 4;

    public KinematicCharacterMotor Motor;
    public CharacterAnim characterAnim;

    [Header("Gait theo lực stick")]
    [Range(0f, 1f)] public float WalkInputThreshold = 0.35f;
    [Range(0f, 1f)] public float JogInputThreshold = 0.75f;
    [Tooltip("Tốc độ tham chiếu (m/s) của clip, PlayerIK dùng")]
    public float WalkSpeed = 1.86f;
    public float RunSpeed = 5.72f;
    public float SprintSpeed = 7.06f;
    [Tooltip("Bộ anim dùng clip Inplace (không root motion): vận tốc đi/chạy do code theo Walk/Jog/Run/SprintSpeed, " +
             "bỏ clip khởi động / quay / dừng")]
    public bool InPlaceLocomotion;
    [Tooltip("Tốc độ jog (m/s), chỉ dùng khi InPlaceLocomotion")]
    public float JogSpeed = 3.8f;

    [Header("Xoay")]
    public OrientationMethod OrientationMethod = OrientationMethod.TowardsMovement;
    [Tooltip("Độ/giây khi bẻ hướng trong lúc chạy")]
    public float MoveTurnRate = 540f;
    [Tooltip("Bẻ lái chậm dần khi gần tới hướng input")]
    public float MoveTurnSharpness = 12f;
    [Tooltip("Góc tối đa được xoay bù thêm trong lúc phát clip quay")]
    public float TurnWarpLimit = 75f;
    [Range(0f, 1f), Tooltip("Nhả phím trước mốc này (normalized) thì bỏ clip quay, về idle ngay")]
    public float TurnTapCancelTime = 0.35f;
    public float StrafeTurnSharpness = 12f;
    [Tooltip("Lệch hơn góc này so với hướng nhìn thì khởi động bằng clip quay 90 độ")]
    public float StartTurnAngle = 45f;
    [Tooltip("Lệch hơn góc này thì dùng clip quay 180 độ")]
    public float ReverseAngle = 135f;
    [Tooltip("Đang đi / jog mà lệch hơn góc này thì dùng clip quay 90 độ thay vì bẻ lái")]
    public float SideTurnAngle = 100f;
    [Tooltip("Tắt khi bộ anim không có clip quay riêng (Vergil mượn clip của Dante: khác dáng, khác tốc độ) -> đang chạy chỉ bẻ lái")]
    public bool MoveTurnClips = true;

    [Header("Bám root motion")]
    public float GroundSharpness = 18f;
    [Tooltip("Thấp hơn GroundSharpness: dừng có đà (clip dừng không chứa quãng trượt)")]
    public float StopSharpness = 7f;
    [Tooltip("Làm mượt độ lớn tốc độ trong vòng lặp chạy: root motion của clip dao động theo từng bước chân")]
    public float LoopSpeedSharpness = 5f;
    public float LoopSpeedCatchUpSharpness = 16f;

    [Header("Air")]
    public float MaxAirMoveSpeed = 8f;
    public float AirAccelerationSpeed = 15f;
    public float AirTurnSharpness = 6f;
    public float Drag = 0.1f;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public float MaxFallSpeed = 18f;
    public float FallGravityMultiplier = 1.5f;
    [Tooltip("Rời mép đất lâu hơn khoảng này (giây) mới vào anim rơi (tránh giật khi xuống bậc thang)")]
    public float FallAnimDelay = 0.15f;

    [Header("Nhảy")]
    public float JumpUpSpeed = 10f;
    public float AirJumpUpSpeed = 9f;
    [Tooltip("Nhân độ cao nhảy so với thông số gốc (JumpProfile của vũ khí)")]
    public float JumpHeightScale = 1.7f;
    public float AirJumpHeightScale = 1.4f;
    [Range(0f, 1f)] public float AirJumpKeepHorizontal = 0.3f;
    public int MaxAirJumps = 1;
    public float JumpPreGroundingGraceTime = 0.1f;
    public float JumpPostGroundingGraceTime = 0.1f;

    [Header("Né (dash)")]
    [Tooltip("Nhân quãng đường root motion của clip né")]
    public float DodgeDistanceScale = 1.3f;
    public float DodgeTurnSharpness = 40f;
    public float DashCooldown = 0.35f;
    public bool CanDashInAir = true;
    public float DashSpeed = 18f;
    public float DashDuration = 0.18f;
    [Range(0f, 1f), Tooltip("Sau mốc này (normalized) đẩy stick là hủy phần hồi của clip né")]
    public float DodgeCancelTime = 0.65f;

    [Header("Combat")]
    public CharacterCombo characterCombo;
    public float AttackLungeSpeed = 7f;
    public float AttackLungeDuration = 0.12f;
    public float AttackStopDistance = 1.6f;
    [Tooltip("Đòn có đá phải đứng gần hơn, chân ngắn hơn kiếm (m)")]
    public float KickStopDistance = 1.15f;
    [Tooltip("Không lock-on vẫn tự nhắm mục tiêu trong bán kính này (m)")]
    public float AttackAssistRadius = 5f;
    [Tooltip("Mục tiêu lệch quá góc này so với hướng đang đẩy / đang nhìn thì không nhắm (độ)")]
    public float AttackAssistAngle = 75f;
    public float AttackTurnSharpness = 40f;
    public float AttackMoveCancelThreshold = 0.3f;
    public bool CanCancelAttackWithDash = true;
    public bool CanCancelAttackWithJump = true;
    public float AirAttackGravityScale = 0.1f;
    public float AirAttackHangTime = 0.35f;
    public float LungeRootTravelThreshold = 0.5f;
    public float RootMotionSharpness = 30f;
    public float MaxRootVerticalSpeed = 25f;
    public float DashAttackWindow = 0.35f;
    [Range(0f, 1f)] public float DirectionalAttackThreshold = 0.6f;
    [Tooltip("Tắt = cất vũ khí sau CombatHoldTime. Bộ anim Grruzam luôn ở tư thế cầm kiếm nên mặc định luôn cầm")]
    public bool AlwaysDrawWeapon = true;
    public float CombatHoldTime = 6f;

    [Header("Bay (Q)")]
    [Tooltip("Nhân vật có mode bay (bộ anim phải có state FlyStart/FlyIdle/FlyMove/FlyEnd)")]
    public bool CanFly;
    [Tooltip("Luôn bay, không có mode đi bộ: vào game là bay ngay, Q không hạ cánh")]
    public bool AlwaysFly;
    [Tooltip("Nâng capsule thêm so với mặt đất khi bay (m). 0 = capsule ở đất, độ cao bay lấy từ pose clip Inplace")]
    public float FlyHeight = 0f;
    public float FlySpeed = 7f;
    public float FlyAcceleration = 5f;
    [Tooltip("Bám độ cao nhanh cỡ nào: lớn = bám sát địa hình")]
    public float FlyHeightSharpness = 4f;
    public float FlyMaxVerticalSpeed = 8f;
    [Tooltip("Tốc độ hạ xuống khi tắt bay (m/s)")]
    public float FlyLandSpeed = 5f;

    [Header("Lock On")]
    public float LockOnRadius = 15f;
    public float LockOnBreakRadius = 22f;

    [Header("IK")]
    public bool AutoAddIK = true;

    [Header("Misc")]
    public List<Collider> IgnoredColliders = new List<Collider>();
    public Transform MeshRoot;
    public Transform CameraFollowPoint;
    public float CrouchedCapsuleHeight = 1f;

    public MoveState CurrentCharacterState { get; internal set; } = MoveState.Idle;
    public LockOnTarget CurrentLockTarget { get; private set; }
    public bool IsLockedOn => CurrentLockTarget != null;
    public bool IsGrounded => Motor.GroundingStatus.IsStableOnGround;
    public bool IsDashing => CurrentCharacterState == MoveState.Dodge || CurrentCharacterState == MoveState.AirDash;
    public bool IsAttacking => characterCombo != null && characterCombo.IsAttacking;
    public bool HasMoveInput => _moveInputVector.sqrMagnitude > 0.0001f;
    public Vector3 MoveInputDirection => _moveInputVector;
    public bool IsInCombat => AlwaysDrawWeapon || _combatTimer > 0f;
    public bool IsFlying => _flight.IsFlying;

    // giữ sprint khi đang chạy thì bỏ strafe theo camera, chạy theo hướng di chuyển: blend strafe không có clip sprint. Lock-on vẫn strafe
    internal bool IsStrafing => IsLockedOn || (OrientationMethod == OrientationMethod.TowardsCamera && !(_sprintHeld && HasMoveInput));
    internal float DashLastEndTime => _dash.LastEndTime;
    internal float Stick => _stick;
    internal bool SprintHeld => _sprintHeld;
    internal bool FlyingUpperAttack => _flight.IsFlying && IsAttacking && characterCombo.PlaysUpperBody;
    internal LocoState CurrentClip => _currentClip;

    private Vector3 _moveInputVector;
    private Vector3 _cameraForward = Vector3.forward;
    private float _stick;
    private bool _sprintHeld;
    private float _combatTimer;
    private Vector3 _internalVelocityAdd;

    private CharacterCrouch _crouch;
    private CharacterDash _dash;
    private CharacterJump _jump;
    private CharacterCombat _combat;
    private CharacterLocomotion _loco;
    private CharacterFlight _flight;

    private LocoState _currentClip = LocoState.Idle;
    private bool _forceInstantClip;

    private void Awake()
    {
        Motor.CharacterController = this;
        _crouch = new CharacterCrouch(this, Motor.Capsule.height);
        _dash = new CharacterDash(this);
        _jump = new CharacterJump(this);
        _combat = new CharacterCombat(this);
        _loco = new CharacterLocomotion(this, _dash, _jump, _crouch, _combat);
        _flight = new CharacterFlight(this);

        if (characterCombo == null) characterCombo = GetComponentInChildren<CharacterCombo>();
        if (characterCombo != null)
        {
            characterCombo.OnAttackHit += OnAttackStarted;
            characterCombo.OnWeaponChanged += OnWeaponChanged;
        }

        // IK phải nằm cùng GameObject với Animator thì OnAnimatorIK mới được gọi
        if (AutoAddIK && characterAnim != null && characterAnim.animator != null)
        {
            PlayerIK ik = characterAnim.animator.GetComponent<PlayerIK>();
            if (ik == null) ik = characterAnim.animator.gameObject.AddComponent<PlayerIK>();
            ik.Init(this);
        }
    }

    // bị tắt giữa lúc bay (đổi nhân vật): bật lại thì đứng trên đất như bình thường
    private void OnDisable()
    {
        if (_flight == null || !_flight.IsFlying) return;
        _flight.Reset();
        CurrentCharacterState = MoveState.Idle;
        _currentClip = LocoState.Idle;
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
        if (weapon == null) return;
        _jump.SetProfile(weapon.Jump.Enabled ? weapon.Jump : null);
        characterAnim.SetWeapon(weapon.AnimSetId, _currentClip);
    }

    // gọi từ ExamplePlayer.LateUpdate: phát impact của đòn chém đã tới frame này
    public void FlushCombatFeedback()
    {
        if (characterCombo != null) characterCombo.FlushImpacts();
    }

    public void AddVelocity(Vector3 velocity)
    {
        _internalVelocityAdd += velocity;
    }

    public void SwitchOrientation()
    {
        OrientationMethod = OrientationMethod == OrientationMethod.TowardsCamera
            ? OrientationMethod.TowardsMovement
            : OrientationMethod.TowardsCamera;
    }

    #region Input

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        Vector3 moveInput = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);
        _stick = moveInput.magnitude;

        Vector3 cameraPlanar = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanar.sqrMagnitude == 0f) cameraPlanar = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        _cameraForward = cameraPlanar;
        _moveInputVector = Quaternion.LookRotation(cameraPlanar, Motor.CharacterUp) * moveInput;
        _sprintHeld = inputs.Sprint;

        UpdateLockOn(inputs.LockOn);
        characterAnim.towardMove = !IsStrafing;

        bool attacking = IsAttacking;
        _combatTimer = attacking || IsLockedOn || inputs.Attack || inputs.Skill ? CombatHoldTime : _combatTimer - Time.deltaTime;

        if (characterCombo != null)
        {
            if (inputs.WeaponSwitch != 0) characterCombo.NextWeapon(inputs.WeaponSwitch);
            characterCombo.UpperBodyOnly = _flight.IsFlying;
            if (inputs.Attack || inputs.Skill)
            {
                AttackContext context = _combat.ResolveContext(inputs.Skill && !inputs.Attack);
                if (context == AttackContext.Dash && IsDashing) EndDash();
                characterCombo.OnAttackInput(context);
            }
        }

        if (IsAttacking && characterCombo.IsInRecovery && _stick > AttackMoveCancelThreshold) characterCombo.CancelForMove();

        if (AlwaysFly && CanFly)
        {
            if (!_flight.IsFlying && !IsAttacking) _flight.EnterHover();
        }
        else if (inputs.FlyToggle) _flight.Toggle();
        bool flying = _flight.IsFlying;

        if (inputs.JumpDown && !flying && (!IsAttacking || CanCancelAttackWithJump))
        {
            if (IsAttacking) characterCombo.CancelCombo();
            _jump.Request();
        }

        if (inputs.Dash && !flying && _dash.CanBegin())
        {
            if (IsAttacking) characterCombo.CancelCombo();
            _dash.Begin();
        }

        if (inputs.OrientationSwitch) SwitchOrientation();

        if (inputs.CrouchDown)
        {
            _crouch.SetDesired(true);
            _crouch.Begin();
        }
        else if (inputs.CrouchUp)
        {
            _crouch.SetDesired(false);
        }

        attacking = IsAttacking;
        int attackEdge = _combat.UpdateEdge(attacking);
        if (attackEdge == 1 && IsGrounded && !_flight.IsFlying) EnterGroundIdle(0.1f);
        else if (attackEdge == -1) ResumeAfterAttack();

        // đánh thân trên lúc bay: anim bay + di chuyển vẫn chạy
        if (!attacking || FlyingUpperAttack) TickLocomotion(Time.deltaTime);
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

        // ưu tiên mục tiêu gần và nằm phía trước camera
        float bestScore = float.MaxValue;
        foreach (LockOnTarget target in LockOnTarget.All)
        {
            Vector3 toTarget = target.transform.position - Motor.TransientPosition;
            if (toTarget.sqrMagnitude > LockOnRadius * LockOnRadius) continue;

            float angle = Vector3.Angle(_cameraForward, Vector3.ProjectOnPlane(toTarget, Motor.CharacterUp));
            float score = toTarget.magnitude * (1f + angle / 90f);
            if (score < bestScore)
            {
                bestScore = score;
                CurrentLockTarget = target;
            }
        }
    }

    #region Gait / hướng

    internal int DesiredGait() => _loco.DesiredGait();

    internal float SignedAngleToInput() => _loco.SignedAngleToInput();

    internal void UpdateDirectionParams() => _loco.UpdateDirectionParams();

    #endregion

    #region Locomotion FSM

    internal void SetState(MoveState state, LocoState clip, float fade = -1f)
    {
        CurrentCharacterState = state;
        _currentClip = clip;
        if (_forceInstantClip)
        {
            _forceInstantClip = false;
            characterAnim.PlayInstant(clip);
            return;
        }
        characterAnim.Play(clip, fade);
    }

    internal void RequestInstantClip() => _forceInstantClip = true;

    // FSM nằm trong CharacterLocomotion; facade chỉ forward để giữ nguyên thứ tự gọi từ input và KCC
    internal void TickLocomotion(float deltaTime)
    {
        if (_flight.IsFlying) _flight.Tick(deltaTime);
        else _loco.Tick(deltaTime);
    }

    internal void AbortTurn() => _loco.AbortTurn();

    internal void EnterGroundIdle(float fade = -1f) => _loco.EnterGroundIdle(fade);

    // đang bay thì CharacterFlight.Tick tự phát lại anim bay
    internal void ResumeAfterAttack()
    {
        if (!_flight.IsFlying) _loco.ResumeAfterAttack();
    }

    #endregion

    internal Vector3 FacingTarget()
    {
        if (IsLockedOn)
        {
            Vector3 toTarget = PlanarToLockTarget();
            if (toTarget.sqrMagnitude > 0.01f) return toTarget.normalized;
        }
        return _cameraForward;
    }

    internal Vector3 PlanarToLockTarget()
    {
        return Vector3.ProjectOnPlane(CurrentLockTarget.transform.position - Motor.TransientPosition, Motor.CharacterUp);
    }

    #endregion

    #region Dodge

    internal void EndDash()
    {
        if (_dash.End(CurrentCharacterState)) CurrentCharacterState = MoveState.Idle;
    }

    #endregion

    #region Air

    internal void EnterFall()
    {
        if (CurrentCharacterState == MoveState.Turn) AbortTurn();
        if (CurrentCharacterState == MoveState.Dodge) EndDash();
        _jump.MarkConsumed();
        SetState(MoveState.Air, LocoState.JumpLoop, 0.15f);
    }

    private void OnJumped(bool airJump)
    {
        if (CurrentCharacterState == MoveState.Turn) AbortTurn();
        if (CurrentCharacterState == MoveState.Dodge || CurrentCharacterState == MoveState.AirDash) EndDash();
        _jump.MarkAirJump(airJump);
        _jump.ResetAirborneTime();
        SetState(MoveState.Air, airJump ? LocoState.AirJumpStart : LocoState.JumpStart, 0.08f);
    }

    private void OnLanded()
    {
        _jump.ResetAirborneTime();
        if (IsAttacking && characterCombo.CurrentContext == AttackContext.Air) characterCombo.CancelCombo();
        if (CurrentCharacterState == MoveState.AirDash) EndDash();
        // mặt đất gồ ghề làm IsStableOnGround chập chờn: chỉ chạy anim tiếp đất khi thật sự vừa ở trên không
        bool fromAttack = CurrentCharacterState == MoveState.Idle && _jump.JumpConsumed;
        if (CurrentCharacterState != MoveState.Air && !fromAttack) return;

        bool wasAirJump = _jump.TakeAirJumpFlag();
        if (HasMoveInput)
        {
            SetState(MoveState.Move, IsStrafing ? LocoState.Strafe : LocoState.Loop, 0.2f);
            return;
        }
        SetState(MoveState.Land, wasAirJump ? LocoState.AirJumpEnd : LocoState.JumpEnd, 0.08f);
    }

    #endregion

    #region KCC callbacks

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_flight.IsFlying && !IsAttacking) _flight.UpdateRotation(ref currentRotation, deltaTime);
        else _loco.UpdateRotation(ref currentRotation, deltaTime);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector3 rootVelocity = characterAnim.RootMotion != null ? characterAnim.RootMotion.ConsumeVelocity() : Vector3.zero;

        if (_flight.IsFlying)
        {
            bool attacking = IsAttacking && !FlyingUpperAttack;
            if (attacking) _combat.UpdateVelocity(ref currentVelocity, deltaTime);
            _flight.UpdateVelocity(ref currentVelocity, deltaTime, attacking);
        }
        else if (CurrentCharacterState == MoveState.AirDash)
        {
            currentVelocity = _dash.Direction * DashSpeed;
        }
        else if (IsAttacking)
        {
            _combat.UpdateVelocity(ref currentVelocity, deltaTime);
        }
        else if (IsGrounded)
        {
            UpdateGroundVelocity(ref currentVelocity, rootVelocity, deltaTime);
        }
        else
        {
            _jump.UpdateAirVelocity(ref currentVelocity, deltaTime);
        }

        if (!_flight.IsFlying) HandleJump(ref currentVelocity, deltaTime);

        if (_internalVelocityAdd.sqrMagnitude > 0f)
        {
            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }
    }

    // vận tốc mặt đất lấy từ root motion nằm trong CharacterLocomotion
    private void UpdateGroundVelocity(ref Vector3 currentVelocity, Vector3 rootVelocity, float deltaTime)
    {
        _loco.UpdateGroundVelocity(ref currentVelocity, rootVelocity, deltaTime);
    }

    // module tự xử lý physics và jump buffer; facade chỉ báo animation theo kết quả trả về
    private void HandleJump(ref Vector3 currentVelocity, float deltaTime)
    {
        int jump = _jump.HandleJump(ref currentVelocity, deltaTime);
        if (jump != 0) OnJumped(jump == 2);
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        _jump.AfterCharacterUpdate(IsGrounded, deltaTime);
        _crouch.TryUncrouch();
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround) OnLanded();
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return !IgnoredColliders.Contains(coll);
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    #endregion

    #region Attack

    // CharacterCombo gọi khi đòn mới bắt đầu: facade giữ subscription, module lo hướng đánh / lunge / mục tiêu
    private void OnAttackStarted(ComboHitInfo info)
    {
        if (_combat.OnAttackStarted(info)) _jump.MarkConsumed();
    }

    #endregion
}

public enum MoveState
{
    Idle,
    Move,
    Turn,
    Stop,
    Land,
    Dodge,
    Air,
    AirDash,
    Crouch,
    Fly,
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
    public bool FlyToggle; // Bấm = bật / tắt bay (nhân vật CanFly)
}

// ExampleCharacterController của KCC dùng 2 kiểu này ở namespace toàn cục
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

public enum CharacterState
{
    Default,
}
