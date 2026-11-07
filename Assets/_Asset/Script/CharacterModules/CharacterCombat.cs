using UnityEngine;

/// <summary>
/// Giữ hướng đánh, mục tiêu hỗ trợ (attack assist), lunge và vận tốc của đòn đánh.
/// CharController vẫn sở hữu subscription của CharacterCombo và thứ tự callback của KCC.
/// </summary>
internal sealed class CharacterCombat
{
    private readonly CharController _character;

    private Vector3 _facing = Vector3.forward;
    private float _lungeTimer;
    private float _lungeDelay;
    private float _lungeScale = 1f;
    private float _startHeight;
    private LockOnTarget _target;
    private float _airHangTimer;
    private bool _airAttackStarted;
    private bool _wasAttacking;

    public Vector3 Facing => _facing;

    public CharacterCombat(CharController character)
    {
        _character = character;
    }

    /// <returns>-1 = vừa kết thúc đòn, 0 = không đổi, 1 = vừa bắt đầu đòn.</returns>
    public int UpdateEdge(bool attacking)
    {
        int edge = 0;
        if (attacking && !_wasAttacking) edge = 1;
        else if (!attacking && _wasAttacking) edge = -1;
        _wasAttacking = attacking;
        return edge;
    }

    // trên không = air combo; lock-on + kéo lùi = hất tung; lock-on + đẩy tới / chạy nhanh / vừa né xong = dash attack
    public AttackContext ResolveContext(bool skill)
    {
        CharController c = _character;
        if (!c.IsGrounded) return AttackContext.Air;
        if (skill) return AttackContext.Skill;
        if (c.characterCombo.IsContinuingGroundChain) return AttackContext.Ground;

        if (c.IsLockedOn && c.MoveInputDirection.sqrMagnitude > c.DirectionalAttackThreshold * c.DirectionalAttackThreshold)
        {
            float dot = Vector3.Dot(c.MoveInputDirection.normalized, c.PlanarToLockTarget().normalized);
            if (dot < -c.DirectionalAttackThreshold) return AttackContext.Launcher;
            if (dot > c.DirectionalAttackThreshold) return AttackContext.Dash;
        }

        bool sprinting = c.CurrentCharacterState == MoveState.Move && c.DesiredGait() == CharController.GaitFast;
        if (c.IsDashing || sprinting || Time.time - c.DashLastEndTime <= c.DashAttackWindow) return AttackContext.Dash;
        return AttackContext.Ground;
    }

    /// <summary>Chuẩn bị hướng đánh / lunge / mục tiêu cho đòn mới.</summary>
    /// <returns>true nếu đây là đòn hất tung (launcher) và cần khoá lượt nhảy.</returns>
    public bool OnAttackStarted(ComboHitInfo info)
    {
        CharController c = _character;
        Vector3 facing = c.HasMoveInput ? c.MoveInputDirection.normalized : c.Motor.CharacterForward;
        bool animTravels = info.RootTravel >= c.LungeRootTravelThreshold;
        bool groundedContext = info.Context != AttackContext.Air && info.Context != AttackContext.Launcher;
        _lungeScale = animTravels || !groundedContext ? 0f : 1f;

        _target = FindAttackTarget(facing);
        if (_target != null)
        {
            Vector3 toTarget = PlanarTo(_target);
            float distance = toTarget.magnitude;
            if (distance > 0.01f) facing = toTarget / distance;
            // đã đứng sát mục tiêu thì không lướt thêm
            float lungeDistance = Mathf.Max(c.AttackLungeSpeed * c.AttackLungeDuration, 0.01f);
            _lungeScale *= Mathf.Clamp01((distance - CurrentStopDistance) / lungeDistance);
        }

        _facing = facing;
        _lungeTimer = c.AttackLungeDuration;
        _lungeDelay = Mathf.Max(0f, info.TimeToFirstHit - c.AttackLungeDuration);
        _startHeight = Vector3.Dot(c.Motor.TransientPosition, c.Motor.CharacterUp);

        if (info.Context == AttackContext.Launcher) return true;
        if (!c.IsGrounded)
        {
            _airHangTimer = c.AirAttackHangTime;
            _airAttackStarted = true;
        }
        return false;
    }

    // anim đánh là bản Inplace, bước lướt / bật nhảy lấy từ RootSamples của CharacterCombo
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        CharController c = _character;
        Vector3 up = c.Motor.CharacterUp;
        Vector3 verticalVelocity = Vector3.Project(currentVelocity, up);
        Vector3 planarVelocity = currentVelocity - verticalVelocity;

        Vector3 rootVelocity = c.Motor.TransientRotation * c.characterCombo.GetRootMotionVelocity();
        Vector3 rootVertical = Vector3.Project(rootVelocity, up);
        Vector3 rootPlanar = rootVelocity - rootVertical;

        if (_lungeDelay <= 0f && _lungeTimer > 0f)
        {
            planarVelocity = _facing * c.AttackLungeSpeed * _lungeScale + rootPlanar;
            _lungeTimer -= deltaTime;
        }
        else
        {
            if (_lungeDelay > 0f) _lungeDelay -= deltaTime;
            planarVelocity = Vector3.Lerp(planarVelocity, rootPlanar, 1f - Mathf.Exp(-c.RootMotionSharpness * deltaTime));
        }
        planarVelocity = ClampApproach(planarVelocity, deltaTime);

        // hất tung: bám độ cao của anim so với lúc ra đòn, anim đáp xuống thì nhân vật cũng về mặt đất
        if (c.characterCombo.CurrentStep != null && c.characterCombo.CurrentStep.UseVerticalRootMotion)
        {
            float targetHeight = _startHeight + c.characterCombo.GetRootOffset().y;
            float verticalSpeed = (targetHeight - Vector3.Dot(c.Motor.TransientPosition, up)) / Mathf.Max(deltaTime, 0.0001f);
            verticalSpeed = Mathf.Clamp(verticalSpeed, -c.MaxRootVerticalSpeed, c.MaxRootVerticalSpeed);

            if (verticalSpeed > 0.01f && c.IsGrounded) c.Motor.ForceUnground();
            if (!c.IsGrounded || verticalSpeed > 0.01f)
            {
                currentVelocity = planarVelocity + up * verticalSpeed;
                return;
            }
        }

        if (c.IsGrounded)
        {
            currentVelocity = c.Motor.GetDirectionTangentToSurface(planarVelocity, c.Motor.GroundingStatus.GroundNormal) * planarVelocity.magnitude;
            return;
        }

        if (_airAttackStarted)
        {
            // đòn đầu trên không: xóa vận tốc rơi để nhân vật khựng lại giữa không trung
            _airAttackStarted = false;
            if (Vector3.Dot(verticalVelocity, up) < 0f) verticalVelocity = Vector3.zero;
        }

        float gravityScale = _airHangTimer > 0f ? c.AirAttackGravityScale : 1f;
        _airHangTimer -= deltaTime;
        verticalVelocity += c.Gravity * gravityScale * deltaTime;
        currentVelocity = planarVelocity + verticalVelocity;
    }

    private float CurrentStopDistance => _character.characterCombo.CurrentStep != null && _character.characterCombo.CurrentStep.KickTimes.Count > 0
        ? _character.KickStopDistance
        : _character.AttackStopDistance;

    private Vector3 PlanarTo(LockOnTarget target)
    {
        return Vector3.ProjectOnPlane(target.transform.position - _character.Motor.TransientPosition, _character.Motor.CharacterUp);
    }

    // đang lock-on thì đánh đúng mục tiêu đó; không thì lấy mục tiêu gần và thuận hướng nhất
    private LockOnTarget FindAttackTarget(Vector3 aim)
    {
        CharController c = _character;
        if (c.IsLockedOn) return c.CurrentLockTarget;

        LockOnTarget best = null;
        float bestScore = float.MaxValue;
        foreach (LockOnTarget target in LockOnTarget.All)
        {
            Vector3 toTarget = PlanarTo(target);
            float distance = toTarget.magnitude;
            float angle = Vector3.Angle(aim, toTarget);
            if (distance > c.AttackAssistRadius || angle > c.AttackAssistAngle) continue;

            float score = distance * (1f + angle / 90f);
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        return best;
    }

    // chỉ cắt phần vận tốc lao vào mục tiêu nên anim vẫn bước và lách ngang được, nhưng không thể xuyên qua
    private Vector3 ClampApproach(Vector3 planarVelocity, float deltaTime)
    {
        if (_target == null || !_target.isActiveAndEnabled) return planarVelocity;

        Vector3 toTarget = PlanarTo(_target);
        float distance = toTarget.magnitude;
        if (distance < 0.01f) return planarVelocity;

        Vector3 direction = toTarget / distance;
        float approach = Vector3.Dot(planarVelocity, direction);
        float allowed = Mathf.Max(0f, distance - CurrentStopDistance) / Mathf.Max(deltaTime, 0.0001f);
        return approach > allowed ? planarVelocity - direction * (approach - allowed) : planarVelocity;
    }
}
