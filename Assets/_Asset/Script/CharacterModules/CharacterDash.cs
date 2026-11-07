using UnityEngine;

/// <summary>
/// Giữ cooldown, hướng và thời gian dash. Mọi state/animation vẫn được yêu cầu qua CharController.
/// </summary>
internal sealed class CharacterDash
{
    private readonly CharController _character;

    private Vector3 _facing = Vector3.forward;
    private float _cooldownTimer;
    private float _airTimer;
    private Vector3 _direction;
    private float _lastEndTime = -999f;

    public Vector3 Facing => _facing;
    public Vector3 Direction => _direction;
    public float LastEndTime => _lastEndTime;

    public CharacterDash(CharController character)
    {
        _character = character;
    }

    public void TickCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= deltaTime;
    }

    public bool CanBegin()
    {
        if (_character.IsDashing || _cooldownTimer > 0f) return false;
        if (_character.IsAttacking && !_character.CanCancelAttackWithDash) return false;
        return _character.IsGrounded || _character.CanDashInAir;
    }

    public void Begin()
    {
        _character.AbortTurn();
        _cooldownTimer = _character.DashCooldown;

        if (!_character.IsGrounded)
        {
            _direction = _character.HasMoveInput ? _character.MoveInputDirection.normalized : _character.Motor.CharacterForward;
            _facing = _direction;
            _airTimer = _character.DashDuration;
            _character.SetState(MoveState.AirDash, LocoState.DodgeFront, 0.06f);
            return;
        }

        _character.SetState(MoveState.Dodge, PickClip(), 0.06f);
    }

    public void TickGround(LocoState currentClip)
    {
        float progress = _character.characterAnim.GetStateProgress(currentClip, out float remaining);
        if (progress < 0f) return;
        if (progress >= _character.DodgeCancelTime && _character.HasMoveInput)
        {
            _character.EndDash();
            _character.SetState(MoveState.Move, _character.IsStrafing ? LocoState.Strafe : LocoState.Loop, 0.2f);
            return;
        }
        if (remaining <= _character.characterAnim.DefaultFade)
        {
            _character.EndDash();
            _character.EnterGroundIdle();
        }
    }

    public void TickAir(float deltaTime)
    {
        _airTimer -= deltaTime;
        if (_airTimer > 0f) return;
        Vector3 planar = Vector3.ProjectOnPlane(_character.Motor.BaseVelocity, _character.Motor.CharacterUp);
        _character.AddVelocity(Vector3.ClampMagnitude(planar, _character.MaxAirMoveSpeed) - planar);
        _character.EndDash();
        _character.EnterFall();
    }

    public bool End(MoveState state)
    {
        _lastEndTime = Time.time;
        return state == MoveState.Dodge || state == MoveState.AirDash;
    }

    private LocoState PickClip()
    {
        _facing = _character.IsStrafing ? _character.FacingTarget() : _character.Motor.CharacterForward;
        if (!_character.HasMoveInput) return LocoState.DodgeBack;

        if (_character.IsStrafing)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(_facing, _character.Motor.CharacterUp)) * _character.MoveInputDirection;
            if (Mathf.Abs(local.z) >= Mathf.Abs(local.x)) return local.z >= 0f ? LocoState.DodgeFront : LocoState.DodgeBack;
            return local.x >= 0f ? LocoState.DodgeRight : LocoState.DodgeLeft;
        }

        float angle = _character.SignedAngleToInput();
        if (_character.DesiredGait() == CharController.GaitFast && Mathf.Abs(angle) >= _character.StartTurnAngle && Mathf.Abs(angle) < _character.ReverseAngle)
        {
            LocoState side = angle < 0f ? LocoState.RunDodgeLeft : LocoState.RunDodgeRight;
            if (_character.characterAnim.HasState(side)) return side;
        }
        _facing = _character.MoveInputDirection.normalized;
        return LocoState.DodgeFront;
    }
}
