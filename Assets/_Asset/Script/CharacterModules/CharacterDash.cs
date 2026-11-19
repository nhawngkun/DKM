using UnityEngine;

/// <summary>
/// Giữ cooldown, hướng, thời gian dash, buffer lần bấm né và i-frames. Mọi state/animation vẫn được yêu cầu qua CharController.
/// Đang bay (Trish) thì né là một cú lướt ngắn bằng code, không đổi state / anim bay.
/// </summary>
internal sealed class CharacterDash
{
    private readonly CharController _character;

    private Vector3 _facing = Vector3.forward;
    private float _cooldownTimer;
    private float _airTimer;
    private float _flyTimer;
    private Vector3 _direction;
    private float _lastEndTime = -999f;
    private float _startTime = -999f;
    private float _bufferedAt = -999f;
    private bool _bufferedInDash;
    private bool _hasBuffered;

    public Vector3 Facing => _facing;
    public Vector3 Direction => _direction;
    public float LastEndTime => _lastEndTime;
    public bool IsFlyDashing => _flyTimer > 0f;

    // bất tử trong một khoảng ngắn đầu cú né (tính từ lúc bấm, kể cả lúc nối né)
    public bool IsInvulnerable
    {
        get
        {
            if (!_character.IsDashing) return false;
            float elapsed = Time.time - _startTime;
            return elapsed >= _character.DodgeIFrameStart && elapsed <= _character.DodgeIFrameEnd;
        }
    }

    public CharacterDash(CharController character)
    {
        _character = character;
    }

    public void TickCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= deltaTime;
    }

    // bấm né lúc chưa né được (đang né, cooldown) thì giữ lại: bấm trong lúc né được giữ tới khi nối được
    public void Buffer()
    {
        _hasBuffered = true;
        _bufferedAt = Time.time;
        _bufferedInDash = _character.IsDashing;
    }

    public bool TryConsumeBuffer()
    {
        if (!_hasBuffered) return false;
        bool alive = _bufferedInDash
            ? _character.IsDashing || Time.time - _lastEndTime <= _character.DodgeBufferTime
            : Time.time - _bufferedAt <= _character.DodgeBufferTime;
        if (!alive)
        {
            _hasBuffered = false;
            return false;
        }
        if (!CanBegin()) return false;
        _hasBuffered = false;
        return true;
    }

    public bool CanBegin()
    {
        if (_character.IsAttacking && !_character.CanCancelAttackWithDash) return false;
        if (_character.IsDashing) return CanChain();
        if (_cooldownTimer > 0f) return false;
        return _character.IsFlying || _character.IsGrounded || _character.CanDashInAir;
    }

    // nối né: né mặt đất / khi bay qua mốc DodgeChainTime; né trên không chờ hết cú lướt
    private bool CanChain()
    {
        CharController c = _character;
        if (IsFlyDashing) return Time.time - _startTime >= c.DodgeChainTime * c.FlyDashDuration;
        if (c.CurrentCharacterState != MoveState.Dodge) return false;
        return c.characterAnim.GetStateProgress(c.CurrentClip, out _) >= c.DodgeChainTime;
    }

    public void Begin()
    {
        CharController c = _character;
        c.AbortTurn();
        _cooldownTimer = c.DashCooldown;
        _startTime = Time.time;

        if (c.IsFlying)
        {
            _direction = c.HasMoveInput ? c.MoveInputDirection.normalized : -c.FacingTarget();
            _flyTimer = c.FlyDashDuration;
            return;
        }

        if (!c.IsGrounded)
        {
            _direction = c.HasMoveInput ? c.MoveInputDirection.normalized : c.Motor.CharacterForward;
            _facing = _direction;
            _airTimer = c.DashDuration;
            c.SetState(MoveState.AirDash, LocoState.DodgeFront, 0.06f);
            return;
        }

        bool chained = c.CurrentCharacterState == MoveState.Dodge;
        LocoState previous = c.CurrentClip;
        LocoState clip = PickClip();
        // nối né cùng clip: crossfade vào chính state đang phát không chạy lại clip -> phát lại tức thì, PoseBlend che mối nối
        if (chained && clip == previous) c.RequestInstantClip();
        c.SetState(MoveState.Dodge, clip, 0.06f);
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

    public void TickFly(float deltaTime)
    {
        if (_flyTimer <= 0f) return;
        _flyTimer -= deltaTime;
        if (_flyTimer <= 0f) _lastEndTime = Time.time;
    }

    public bool End(MoveState state)
    {
        _lastEndTime = Time.time;
        _flyTimer = 0f;
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
