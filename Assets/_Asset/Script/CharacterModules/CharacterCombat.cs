using UnityEngine;

/// <summary>
/// Giữ hướng đánh, mục tiêu hỗ trợ (attack assist), lunge và vận tốc của đòn đánh.
/// CharController vẫn sở hữu subscription của CharacterCombo và thứ tự callback của KCC.
/// </summary>
internal sealed class CharacterCombat
{
    // giá trị khởi điểm để tune; muốn chỉnh trong Inspector thì chuyển sang CharController
    private const float SkillMaxLunge = 2f;          // skill / dash attack được lướt bù tối đa bấy nhiêu m trước nhát đầu
    private const float MaxLungeSpeed = 12f;
    private const float ReachPenetration = 0.1f;     // tâm mục tiêu đứng xa hơn điểm xa nhất của lưỡi kiếm bấy nhiêu m vẫn trúng
    private const float MinStopDistance = 0.8f;
    private const float AirMaxHeadAboveTarget = 0.5f; // đòn trên không: đỉnh đầu không cao hơn đỉnh mục tiêu quá bấy nhiêu m
    private const float AirDescendTime = 0.2f;        // hạ về độ cao đó trong khoảng bấy nhiêu giây
    private const float AirMaxDescendSpeed = 15f;
    private const float AirPullSpeed = 6f;            // kéo về phía mục tiêu khi đánh trên không (m/s)

    private readonly CharController _character;
    private MeleeHitDetector _hitDetector;

    private Vector3 _facing = Vector3.forward;
    private float _lungeTimer;
    private float _lungeDelay;
    private float _lungeSpeed;
    private float _stopDistance = 1.6f;
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
        // đang bay vẫn dùng chuỗi thường / kỹ năng (đánh thân trên), không ép sang air combo
        if (!c.IsGrounded && !c.IsFlying) return AttackContext.Air;
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
        bool groundedContext = info.Context != AttackContext.Air && info.Context != AttackContext.Launcher;
        // phép bắn từ xa: chỉ xoay về mục tiêu, không lướt áp sát
        bool ranged = c.characterCombo.CurrentWeapon != null && c.characterCombo.CurrentWeapon.Type == WeaponType.Magic;
        AttackStep step = c.characterCombo.CurrentStep;
        _stopDistance = StopDistanceFor(step);

        float lungeDistance = 0f;
        _target = FindAttackTarget(facing);
        if (_target != null)
        {
            Vector3 toTarget = PlanarTo(_target);
            float distance = toTarget.magnitude;
            if (distance > 0.01f) facing = toTarget / distance;
            // chỉ lướt phần anim tự bước chưa tới: đứng đủ gần thì nhát đầu chạm đúng tầm lưỡi kiếm
            if (groundedContext && !ranged)
            {
                bool longLunge = info.Context == AttackContext.Skill || info.Context == AttackContext.Dash;
                float maxLunge = longLunge ? SkillMaxLunge : c.AttackLungeSpeed * c.AttackLungeDuration;
                lungeDistance = Mathf.Clamp(distance - _stopDistance - AnimForwardBeforeFirstHit(step), 0f, maxLunge);
            }
        }
        else if (groundedContext && !ranged && info.RootTravel < c.LungeRootTravelThreshold)
        {
            // chém vào khoảng không: anim đứng yên thì vẫn nhích tới một bước cho có lực
            lungeDistance = c.AttackLungeSpeed * c.AttackLungeDuration;
        }

        _facing = facing;
        float lungeTime = Mathf.Max(c.AttackLungeDuration, lungeDistance / MaxLungeSpeed);
        _lungeSpeed = lungeDistance / lungeTime;
        _lungeTimer = lungeDistance > 0f ? lungeTime : 0f;
        _lungeDelay = Mathf.Max(0f, info.TimeToFirstHit - lungeTime);
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

        // lướt theo hướng đòn (về mục tiêu), không theo thân đang xoay dần: đòn lướt nhanh lúc thân còn lệch sẽ trượt ngang qua mục tiêu
        Quaternion attackFrame = _facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(_facing, up) : c.Motor.TransientRotation;
        Vector3 rootVelocity = attackFrame * c.characterCombo.GetRootMotionVelocity();
        Vector3 rootVertical = Vector3.Project(rootVelocity, up);
        Vector3 rootPlanar = rootVelocity - rootVertical;

        if (_lungeDelay <= 0f && _lungeTimer > 0f)
        {
            planarVelocity = _facing * _lungeSpeed + rootPlanar;
            _lungeTimer -= deltaTime;
        }
        else
        {
            if (_lungeDelay > 0f) _lungeDelay -= deltaTime;
            planarVelocity = Vector3.Lerp(planarVelocity, rootPlanar, 1f - Mathf.Exp(-c.RootMotionSharpness * deltaTime));
        }
        bool airAttack = !c.IsGrounded && !c.IsFlying && (c.characterCombo.CurrentStep == null || !c.characterCombo.CurrentStep.UseVerticalRootMotion);
        if (airAttack) planarVelocity = PullTowardTarget(planarVelocity, deltaTime);
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
        if (!c.IsFlying) verticalVelocity = LimitAirHeight(verticalVelocity, up);
        currentVelocity = planarVelocity + verticalVelocity;
    }

    // nhảy cao hơn mục tiêu thì lưỡi kiếm chém qua đầu nó: hạ xuống để đỉnh đầu ngang đỉnh mục tiêu
    private Vector3 LimitAirHeight(Vector3 verticalVelocity, Vector3 up)
    {
        if (!HasLiveTarget) return verticalVelocity;

        CharController c = _character;
        float ceiling = _target.Top + AirMaxHeadAboveTarget - c.Motor.Capsule.height;
        float excess = Vector3.Dot(c.Motor.TransientPosition, up) - ceiling;
        if (excess <= 0f) return verticalVelocity;

        float descend = Mathf.Min(excess / AirDescendTime, AirMaxDescendSpeed);
        return Vector3.Dot(verticalVelocity, up) > -descend ? up * -descend : verticalVelocity;
    }

    // đòn trên không không có lunge: kéo nhẹ tới khoảng dừng để nhát chém với tới
    private Vector3 PullTowardTarget(Vector3 planarVelocity, float deltaTime)
    {
        if (!HasLiveTarget) return planarVelocity;

        Vector3 toTarget = PlanarTo(_target);
        float gap = toTarget.magnitude - _stopDistance;
        if (gap <= 0.01f) return planarVelocity;
        return planarVelocity + toTarget.normalized * Mathf.Min(AirPullSpeed, gap / Mathf.Max(deltaTime, 0.0001f));
    }

    private bool HasLiveTarget => _target != null && _target.isActiveAndEnabled;

    // đòn đá dừng gần hơn; đòn chém dừng trong tầm thật của lưỡi kiếm (đo từ anim), không xa hơn AttackStopDistance
    private float StopDistanceFor(AttackStep step)
    {
        CharController c = _character;
        bool kick = step != null && step.KickTimes.Count > 0;
        WeaponData weapon = c.characterCombo.CurrentWeapon;
        bool fist = weapon != null && weapon.Type == WeaponType.Fist;
        if (kick && !fist) return c.KickStopDistance;

        if (_hitDetector == null) _hitDetector = c.characterCombo.GetComponent<MeleeHitDetector>();
        float reach = _hitDetector != null ? _hitDetector.BladeReach(step) : -1f;
        float stop = reach <= 0f ? c.AttackStopDistance : Mathf.Clamp(reach + ReachPenetration, MinStopDistance, c.AttackStopDistance);
        // quyền cước vừa đấm vừa đá: dừng ở tầm ngắn hơn để cả 2 cùng với tới
        return kick ? Mathf.Min(stop, c.KickStopDistance) : stop;
    }

    // quãng anim tự bước tới trước nhát chém đầu (m)
    private float AnimForwardBeforeFirstHit(AttackStep step)
    {
        if (step == null || !step.UsePlanarRootMotion || step.HitTimes.Count == 0) return 0f;
        float time = step.HitTimes[0] * step.Length;
        float forward = WeaponData.SampleVector(step.RootSamples, time).z - WeaponData.SampleVector(step.RootSamples, 0f).z;
        return Mathf.Max(0f, forward * _character.characterCombo.animator.humanScale * step.RootMotionScale);
    }

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
            // sát người thì lệch ngang chút đã quá góc: vẫn phải nhận để ClampApproach chặn đòn lướt xuyên qua
            bool close = distance <= c.AttackStopDistance;
            if (distance > c.AttackAssistRadius || (!close && angle > c.AttackAssistAngle)) continue;

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
        if (!HasLiveTarget) return planarVelocity;

        Vector3 toTarget = PlanarTo(_target);
        float distance = toTarget.magnitude;
        if (distance < 0.01f) return planarVelocity;

        Vector3 direction = toTarget / distance;
        float approach = Vector3.Dot(planarVelocity, direction);
        float allowed = Mathf.Max(0f, distance - _stopDistance) / Mathf.Max(deltaTime, 0.0001f);
        return approach > allowed ? planarVelocity - direction * (approach - allowed) : planarVelocity;
    }
}
