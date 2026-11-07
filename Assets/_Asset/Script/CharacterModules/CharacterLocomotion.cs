using UnityEngine;

/// <summary>
/// Giữ state machine locomotion, turn-snap/turn-warp, phần xoay và vận tốc mặt đất lấy từ root motion.
/// CharController vẫn là facade: nhận input, gọi KCC theo đúng thứ tự cũ và sở hữu animation bridge (SetState).
/// </summary>
internal sealed class CharacterLocomotion
{
    private readonly CharController _character;
    private readonly CharacterDash _dash;
    private readonly CharacterJump _jump;
    private readonly CharacterCrouch _crouch;
    private readonly CharacterCombat _combat;

    private int _lastGait = CharController.GaitWalk;
    private float _smoothedRootSpeed;
    private float _turnSnapYaw;
    private float _turnWarpYaw;
    private float _turnAppliedWarp;
    private Vector3 _turnStartForward = Vector3.forward;
    private bool _turnSnapDone = true;
    private bool _interruptibleTurn;

    public CharacterLocomotion(CharController character, CharacterDash dash, CharacterJump jump, CharacterCrouch crouch, CharacterCombat combat)
    {
        _character = character;
        _dash = dash;
        _jump = jump;
        _crouch = crouch;
        _combat = combat;
    }

    #region Gait / hướng

    public int DesiredGait()
    {
        CharController c = _character;
        if (c.Stick < c.WalkInputThreshold) return CharController.GaitWalk;
        if (c.IsStrafing || _crouch.IsCrouching) return CharController.GaitJog;
        if (c.SprintHeld) return CharController.GaitFast;
        return c.Stick < c.JogInputThreshold ? CharController.GaitJog : CharController.GaitRun;
    }

    public float SignedAngleToInput()
    {
        return Vector3.SignedAngle(_character.Motor.CharacterForward, _character.MoveInputDirection, _character.Motor.CharacterUp);
    }

    public void UpdateDirectionParams()
    {
        CharController c = _character;
        Vector3 local = Quaternion.Inverse(c.Motor.TransientRotation) * c.MoveInputDirection;
        // vòng trong blend tree = đi, vòng ngoài = jog
        float ring = DesiredGait() == CharController.GaitWalk ? 0.5f : 1f;
        Vector3 planar = new Vector3(local.x, 0f, local.z).normalized * ring;
        c.characterAnim.SetDirection(c.HasMoveInput ? planar.x : 0f, c.HasMoveInput ? planar.z : 0f);
    }

    #endregion

    #region FSM

    // gọi mỗi frame khi không attack: cooldown dash, phát hiện rơi, rồi tick state hiện tại
    public void Tick(float deltaTime)
    {
        _dash.TickCooldown(deltaTime);

        if (!_character.IsGrounded && _character.CurrentCharacterState != MoveState.Air && _character.CurrentCharacterState != MoveState.AirDash)
        {
            _jump.AddAirborneTime(deltaTime);
            if (_jump.AirborneTime > _character.FallAnimDelay || _character.CurrentCharacterState == MoveState.Dodge) _character.EnterFall();
            return;
        }

        switch (_character.CurrentCharacterState)
        {
            case MoveState.Idle: TickIdle(); break;
            case MoveState.Move: TickMove(); break;
            case MoveState.Turn: TickTurn(); break;
            case MoveState.Stop: TickStop(); break;
            case MoveState.Land: TickStop(); break;
            case MoveState.Dodge: _dash.TickGround(_character.CurrentClip); break;
            case MoveState.Air: TickAir(); break;
            case MoveState.AirDash: _dash.TickAir(deltaTime); break;
            case MoveState.Crouch: _crouch.Tick(_character.CurrentClip); break;
            default: throw new System.ArgumentOutOfRangeException(nameof(CharController.CurrentCharacterState), _character.CurrentCharacterState, null);
        }
    }

    private void TickIdle()
    {
        CharController c = _character;
        if (c.HasMoveInput)
        {
            BeginMove();
            return;
        }
        if (!c.IsStrafing) return;

        Vector3 look = c.FacingTarget();
        float angle = Vector3.SignedAngle(c.Motor.CharacterForward, look, c.Motor.CharacterUp);
        if (Mathf.Abs(angle) < c.StartTurnAngle) return;
        if (Mathf.Abs(angle) >= c.ReverseAngle) BeginTurn(LocoState.IdleTurn180, 180f, angle, false);
        else BeginTurn(angle < 0f ? LocoState.IdleTurnL90 : LocoState.IdleTurnR90, angle < 0f ? -90f : 90f, angle, false);
    }

    private void BeginMove()
    {
        CharController c = _character;
        if (c.IsStrafing)
        {
            UpdateDirectionParams();
            c.SetState(MoveState.Move, LocoState.Strafe);
            return;
        }

        int gait = DesiredGait();
        _lastGait = gait;
        c.characterAnim.SetGait(gait);
        float angle = SignedAngleToInput();
        float abs = Mathf.Abs(angle);

        if (abs >= c.ReverseAngle)
        {
            BeginTurn(gait == CharController.GaitWalk ? LocoState.StartWalk180 : gait == CharController.GaitJog ? LocoState.StartJog180 : LocoState.StartRun180, 180f, angle, true);
        }
        else if (abs >= c.StartTurnAngle)
        {
            LocoState clip = gait == CharController.GaitWalk
                ? (angle < 0f ? LocoState.StartWalkL90 : LocoState.StartWalkR90)
                : gait == CharController.GaitJog
                    ? (angle < 0f ? LocoState.StartJogL90 : LocoState.StartJogR90)
                    : (angle < 0f ? LocoState.StartRunL90 : LocoState.StartRunR90);
            BeginTurn(clip, angle < 0f ? -90f : 90f, angle, true);
        }
        else
        {
            BeginTurn(gait == CharController.GaitWalk ? LocoState.StartWalk : gait == CharController.GaitJog ? LocoState.StartJog : LocoState.StartRun, 0f, angle, true);
        }
    }

    private void TickMove()
    {
        CharController c = _character;
        if (!c.HasMoveInput)
        {
            BeginStop();
            return;
        }
        if (_crouch.IsCrouching)
        {
            c.SetState(MoveState.Crouch, LocoState.CrouchMove);
            return;
        }

        if (c.IsStrafing)
        {
            if (c.CurrentClip != LocoState.Strafe) c.SetState(MoveState.Move, LocoState.Strafe);
            UpdateDirectionParams();
            return;
        }
        if (c.CurrentClip != LocoState.Loop) c.SetState(MoveState.Move, LocoState.Loop);

        int gait = DesiredGait();
        _lastGait = gait;
        c.characterAnim.SetGait(gait);

        float angle = SignedAngleToInput();
        float abs = Mathf.Abs(angle);
        if (abs >= c.ReverseAngle)
        {
            BeginTurn(gait == CharController.GaitWalk ? LocoState.TurnWalk180 : gait == CharController.GaitJog ? LocoState.TurnJog180 : LocoState.TurnRun180, 180f, angle, false);
        }
        else if (abs >= c.SideTurnAngle && gait == CharController.GaitWalk)
        {
            BeginTurn(angle < 0f ? LocoState.TurnWalkL90 : LocoState.TurnWalkR90, angle < 0f ? -90f : 90f, angle, false);
        }
        else if (abs >= c.SideTurnAngle && gait == CharController.GaitJog)
        {
            BeginTurn(angle < 0f ? LocoState.TurnJogL90 : LocoState.TurnJogR90, angle < 0f ? -90f : 90f, angle, false);
        }
    }

    // clip bake góc clipYaw trong pose; desiredYaw - clipYaw được xoay dần trong lúc phát để hết clip nhìn đúng hướng input
    private void BeginTurn(LocoState clip, float clipYaw, float desiredYaw, bool interruptible)
    {
        _turnSnapYaw = clipYaw;
        _turnWarpYaw = Mathf.Clamp(Mathf.DeltaAngle(clipYaw, desiredYaw), -_character.TurnWarpLimit, _character.TurnWarpLimit);
        _turnAppliedWarp = 0f;
        _turnStartForward = _character.Motor.CharacterForward;
        _turnSnapDone = false;
        _interruptibleTurn = interruptible;
        _character.SetState(MoveState.Turn, clip, 0.1f);
    }

    private void TickTurn()
    {
        CharController c = _character;
        float progress = c.characterAnim.GetStateProgress(c.CurrentClip, out float remaining);
        if (progress < 0f) return;

        if (!c.HasMoveInput && _interruptibleTurn)
        {
            // clip góc 0 nhả input thì dừng ngay; clip có xoay chỉ bỏ được lúc pose chưa quay nhiều
            if (Mathf.Approximately(_turnSnapYaw, 0f))
            {
                BeginStop();
                return;
            }
            if (progress < c.TurnTapCancelTime)
            {
                _turnSnapDone = true;
                EnterGroundIdle(0.2f);
                return;
            }
        }

        if (remaining > 0.02f) return;

        FinishTurn();
        c.RequestInstantClip();
        if (!c.HasMoveInput) c.SetState(MoveState.Idle, c.IsStrafing ? LocoState.Strafe : LocoState.Idle);
        else c.SetState(MoveState.Move, c.IsStrafing ? LocoState.Strafe : LocoState.Loop);
    }

    private void FinishTurn()
    {
        if (_turnSnapDone) return;
        _turnSnapDone = true;
        if (!Mathf.Approximately(_turnSnapYaw, 0f))
        {
            _character.characterAnim.CapturePose();
            _character.Motor.SetRotation(Quaternion.AngleAxis(_turnSnapYaw, _character.Motor.CharacterUp) * _character.Motor.TransientRotation);
            _character.RequestInstantClip();
        }
    }

    // bị hành động khác cắt ngang: qua nửa clip thì pose đã xoay gần hết, chốt góc để không quay ngược lại
    public void AbortTurn()
    {
        if (_turnSnapDone) return;
        float progress = _character.characterAnim.GetStateProgress(_character.CurrentClip, out _);
        if (progress > 0.5f) FinishTurn();
        _turnSnapDone = true;
    }

    private void BeginStop()
    {
        CharController c = _character;
        if (c.IsStrafing)
        {
            EnterGroundIdle(0.2f);
            return;
        }
        LocoState clip = _lastGait == CharController.GaitFast ? LocoState.StopFast
            : _lastGait == CharController.GaitWalk ? LocoState.StopWalk
            : LocoState.StopRun;
        c.SetState(MoveState.Stop, clip, 0.15f);
    }

    // Stop và Land giống nhau: có input là cắt ngay vào chạy, hết clip về idle
    private void TickStop()
    {
        CharController c = _character;
        if (c.HasMoveInput)
        {
            c.CurrentCharacterState = MoveState.Idle;
            BeginMove();
            return;
        }
        c.characterAnim.GetStateProgress(c.CurrentClip, out float remaining);
        if (remaining <= c.characterAnim.DefaultFade) EnterGroundIdle();
    }

    public void EnterGroundIdle(float fade = -1f)
    {
        CharController c = _character;
        AbortTurn();
        c.SetState(MoveState.Idle, _crouch.IsCrouching ? LocoState.CrouchMove : c.IsStrafing ? LocoState.Strafe : LocoState.Idle, fade);
        if (_crouch.IsCrouching) c.CurrentCharacterState = MoveState.Crouch;
        c.characterAnim.SetDirection(0f, 0f);
    }

    public void ResumeAfterAttack()
    {
        CharController c = _character;
        if (!c.IsGrounded)
        {
            c.EnterFall();
            return;
        }
        if (c.HasMoveInput && !_crouch.IsCrouching)
        {
            c.SetState(MoveState.Move, c.IsStrafing ? LocoState.Strafe : LocoState.Loop, 0.15f);
            return;
        }
        EnterGroundIdle(0.15f);
    }

    private void TickAir()
    {
        CharController c = _character;
        LocoState loop = c.CurrentClip == LocoState.JumpStart ? LocoState.JumpLoop
            : c.CurrentClip == LocoState.AirJumpStart ? LocoState.AirJumpLoop
            : c.CurrentClip;
        if (loop == c.CurrentClip) return;
        c.characterAnim.GetStateProgress(c.CurrentClip, out float remaining);
        if (remaining <= 0.1f) c.SetState(MoveState.Air, loop, 0.1f);
    }

    #endregion

    #region Rotation

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        CharController c = _character;
        Vector3 up = c.Motor.CharacterUp;
        Vector3 forward = c.Motor.CharacterForward;

        if (c.IsAttacking)
        {
            currentRotation = SmoothLook(forward, _combat.Facing, c.AttackTurnSharpness, deltaTime, up);
            return;
        }

        switch (c.CurrentCharacterState)
        {
            case MoveState.Move:
            case MoveState.Crouch:
                if (c.IsStrafing)
                {
                    currentRotation = SmoothLook(forward, c.FacingTarget(), c.StrafeTurnSharpness, deltaTime, up);
                }
                else if (c.HasMoveInput)
                {
                    float error = Vector3.SignedAngle(forward, c.MoveInputDirection, up);
                    float eased = error * (1f - Mathf.Exp(-c.MoveTurnSharpness * deltaTime));
                    float step = Mathf.Clamp(eased, -c.MoveTurnRate * deltaTime, c.MoveTurnRate * deltaTime);
                    currentRotation = Quaternion.AngleAxis(step, up) * currentRotation;
                }
                break;
            case MoveState.Idle:
                if (c.IsStrafing) currentRotation = SmoothLook(forward, c.FacingTarget(), c.StrafeTurnSharpness, deltaTime, up);
                break;
            case MoveState.Turn:
                if (!_turnSnapDone)
                {
                    // bám theo input hiện tại, không phải input lúc bắt đầu clip
                    if (c.HasMoveInput)
                    {
                        float wanted = Vector3.SignedAngle(_turnStartForward, c.MoveInputDirection, up);
                        _turnWarpYaw = Mathf.Clamp(Mathf.DeltaAngle(_turnSnapYaw, wanted), -c.TurnWarpLimit, c.TurnWarpLimit);
                    }
                    float progress = Mathf.Clamp01(c.characterAnim.GetStateProgress(c.CurrentClip, out _));
                    float warp = _turnWarpYaw * progress - _turnAppliedWarp;
                    _turnAppliedWarp += warp;
                    currentRotation = Quaternion.AngleAxis(warp, up) * currentRotation;
                }
                break;
            case MoveState.Dodge:
            case MoveState.AirDash:
                currentRotation = SmoothLook(forward, _dash.Facing, c.DodgeTurnSharpness, deltaTime, up);
                break;
            case MoveState.Air:
                if (c.HasMoveInput) currentRotation = SmoothLook(forward, c.MoveInputDirection.normalized, c.AirTurnSharpness, deltaTime, up);
                break;
        }
    }

    private static Quaternion SmoothLook(Vector3 from, Vector3 to, float sharpness, float deltaTime, Vector3 up)
    {
        Vector3 look = Vector3.Slerp(from, to, 1f - Mathf.Exp(-sharpness * deltaTime)).normalized;
        return Quaternion.LookRotation(look, up);
    }

    #endregion

    #region Vận tốc mặt đất

    public void UpdateGroundVelocity(ref Vector3 currentVelocity, Vector3 rootVelocity, float deltaTime)
    {
        CharController c = _character;
        Vector3 up = c.Motor.CharacterUp;
        Vector3 planarRoot = Vector3.ProjectOnPlane(rootVelocity, up);
        float sharpness = c.GroundSharpness;
        if (c.CurrentCharacterState == MoveState.Dodge) planarRoot *= c.DodgeDistanceScale;
        else if (c.CurrentCharacterState == MoveState.Stop) sharpness = c.StopSharpness;

        if (c.CurrentCharacterState == MoveState.Move && !c.IsStrafing)
        {
            // thấp hơn nhiều so với anim (vừa hết clip quay, chân đã chạy) thì đuổi nhanh để không trượt chân
            float speedSharpness = _smoothedRootSpeed < planarRoot.magnitude * 0.7f ? c.LoopSpeedCatchUpSharpness : c.LoopSpeedSharpness;
            _smoothedRootSpeed = Mathf.Lerp(_smoothedRootSpeed, planarRoot.magnitude, 1f - Mathf.Exp(-speedSharpness * deltaTime));
            planarRoot = c.Motor.CharacterForward * _smoothedRootSpeed;
        }
        else
        {
            // vào vòng lặp từ tốc độ đang có, không tụt về 0
            _smoothedRootSpeed = Vector3.ProjectOnPlane(currentVelocity, up).magnitude;
        }

        Vector3 groundNormal = c.Motor.GroundingStatus.GroundNormal;
        Vector3 target = c.Motor.GetDirectionTangentToSurface(planarRoot, groundNormal) * planarRoot.magnitude;
        currentVelocity = Vector3.Lerp(currentVelocity, target, 1f - Mathf.Exp(-sharpness * deltaTime));
    }

    #endregion
}
