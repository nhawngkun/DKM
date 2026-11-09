using UnityEngine;

/// <summary>
/// Mode bay (Trish): bấm Q cất cánh, bay mặt theo hướng di chuyển (lock-on thì nhìn mục tiêu), bấm lại để hạ cánh.
/// AlwaysFly thì bay luôn, không hạ cánh.
/// Clip bay là bản Inplace, độ cao bay nằm sẵn trong pose: FlyHeight = 0 thì capsule vẫn chạy trên đất
/// (bám địa hình như đi bộ), > 0 thì nâng capsule thêm chừng đó. Vận tốc do code, đánh dùng chuỗi Air của vũ khí.
/// </summary>
internal sealed class CharacterFlight
{
    private enum Phase { None, TakeOff, Hover, Landing }

    private readonly CharController _character;
    private Phase _phase;
    private float _phaseTime;
    private Vector3 _planarVelocity;

    public CharacterFlight(CharController character)
    {
        _character = character;
    }

    public bool IsFlying => _phase != Phase.None;

    // capsule có rời đất không: FlyHeight = 0 -> độ cao chỉ nằm trong anim
    private bool Hovers => _character.FlyHeight > 0.01f;

    public void Toggle()
    {
        CharController c = _character;
        if (_phase == Phase.None)
        {
            if (!c.CanFly || c.IsAttacking) return;
            if (Hovers) c.Motor.ForceUnground();
            _planarVelocity = Vector3.ProjectOnPlane(c.Motor.BaseVelocity, c.Motor.CharacterUp);
            Enter(Phase.TakeOff, LocoState.FlyStart, 0.1f);
        }
        else if (_phase != Phase.Landing)
        {
            if (c.IsAttacking) c.characterCombo.CancelCombo();
            Enter(Phase.Landing, LocoState.FlyEnd, 0.15f);
        }
    }

    // nhân vật luôn bay (AlwaysFly): vào thẳng bay lơ lửng, không phát anim cất cánh
    public void EnterHover()
    {
        CharController c = _character;
        if (Hovers) c.Motor.ForceUnground();
        _planarVelocity = Vector3.ProjectOnPlane(c.Motor.BaseVelocity, c.Motor.CharacterUp);
        Enter(Phase.Hover, c.HasMoveInput ? LocoState.FlyMove : LocoState.FlyIdle, 0.2f);
    }

    // nhân vật bị tắt / đổi nhân vật: về đất ngay, không chạy anim hạ cánh
    public void Reset()
    {
        _phase = Phase.None;
        _planarVelocity = Vector3.zero;
    }

    private void Enter(Phase phase, LocoState clip, float fade)
    {
        _phase = phase;
        _phaseTime = 0f;
        _character.SetState(MoveState.Fly, clip, fade);
    }

    // thay cho FSM mặt đất khi đang bay và không đánh
    public void Tick(float deltaTime)
    {
        CharController c = _character;
        _phaseTime += deltaTime;
        c.UpdateDirectionParams();

        switch (_phase)
        {
            case Phase.TakeOff:
                c.characterAnim.GetStateProgress(LocoState.FlyStart, out float remaining);
                if (_phaseTime > 0.2f && remaining <= 0.15f) _phase = Phase.Hover;
                break;
            case Phase.Hover:
                LocoState wanted = c.HasMoveInput ? LocoState.FlyMove : LocoState.FlyIdle;
                // vừa đánh xong / né xong thì FSM khác đã đổi state -> phát lại anim bay
                if (c.CurrentCharacterState != MoveState.Fly || c.CurrentClip != wanted) c.SetState(MoveState.Fly, wanted, 0.25f);
                break;
            case Phase.Landing:
                // capsule đã ở đất -> chờ anim hạ xuống chạy gần hết; capsule đang lơ lửng -> chờ chạm đất
                bool down;
                if (Hovers) down = c.IsGrounded && _phaseTime > 0.1f;
                else
                {
                    c.characterAnim.GetStateProgress(LocoState.FlyEnd, out float landRemaining);
                    down = _phaseTime > 0.2f && landRemaining <= 0.15f;
                }
                if (down)
                {
                    _phase = Phase.None;
                    c.EnterGroundIdle(0.25f);
                }
                break;
        }
    }

    // mặt theo hướng bay (lock-on thì nhìn mục tiêu), không xoay theo camera; đứng yên thì giữ hướng cũ
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        CharController c = _character;
        Vector3 facing = c.IsLockedOn ? c.FacingTarget() : Vector3.ProjectOnPlane(c.MoveInputDirection, c.Motor.CharacterUp);
        if (facing.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(facing, c.Motor.CharacterUp);
        currentRotation = Quaternion.Slerp(currentRotation, target, 1f - Mathf.Exp(-c.StrafeTurnSharpness * deltaTime));
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime, bool attacking)
    {
        CharController c = _character;
        Vector3 up = c.Motor.CharacterUp;

        if (!attacking)
        {
            float speedScale = _phase == Phase.Hover ? 1f : 0.35f;
            Vector3 desired = c.MoveInputDirection * c.FlySpeed * speedScale;
            _planarVelocity = Vector3.Lerp(_planarVelocity, desired, 1f - Mathf.Exp(-c.FlyAcceleration * deltaTime));
            currentVelocity = _planarVelocity;
        }
        else
        {
            // đòn trên không tự lo hướng / lunge, bay chỉ giữ độ cao
            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
            _planarVelocity = currentVelocity;
        }

        if (!Hovers)
        {
            // chạy trên đất như đi bộ: bám mặt dốc / bậc thang, rời mép thì rơi bằng trọng lực
            if (c.IsGrounded)
            {
                Vector3 planar = currentVelocity;
                currentVelocity = c.Motor.GetDirectionTangentToSurface(planar, c.Motor.GroundingStatus.GroundNormal) * planar.magnitude;
            }
            else
            {
                currentVelocity += Vector3.Project(c.Motor.BaseVelocity, up) + c.Gravity * deltaTime;
            }
            return;
        }

        float verticalSpeed;
        if (_phase == Phase.Landing)
        {
            verticalSpeed = -c.FlyLandSpeed;
        }
        else
        {
            float height = Vector3.Dot(c.Motor.TransientPosition, up);
            float targetHeight = GroundHeight(height) + c.FlyHeight;
            verticalSpeed = Mathf.Clamp((targetHeight - height) * c.FlyHeightSharpness, -c.FlyMaxVerticalSpeed, c.FlyMaxVerticalSpeed);
            if (c.IsGrounded) c.Motor.ForceUnground();
        }
        currentVelocity += up * verticalSpeed;
    }

    // độ cao mặt đất ngay dưới nhân vật; không thấy đất thì giữ độ cao đang bay
    private float GroundHeight(float currentHeight)
    {
        CharController c = _character;
        Vector3 up = c.Motor.CharacterUp;
        Vector3 origin = c.Motor.TransientPosition + up * 0.5f;
        int count = Physics.RaycastNonAlloc(origin, -up, _hits, c.FlyHeight + 30f, c.Motor.CollidableLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float ground = currentHeight - c.FlyHeight;
        for (int i = 0; i < count; i++)
        {
            // bỏ collider của chính nhân vật (hitbox, vũ khí...)
            if (_hits[i].collider.transform.IsChildOf(c.transform) || _hits[i].distance >= best) continue;
            best = _hits[i].distance;
            ground = Vector3.Dot(_hits[i].point, up);
        }
        return ground;
    }

    private readonly RaycastHit[] _hits = new RaycastHit[8];
}
