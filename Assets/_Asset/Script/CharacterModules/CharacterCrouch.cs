using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Lưu runtime state và capsule clearance của crouch.
/// CharController vẫn quyết định thời điểm gọi để giữ nguyên KCC callback order.
/// </summary>
internal sealed class CharacterCrouch
{
    private readonly CharController _character;
    private readonly Collider[] _probedColliders = new Collider[8];
    private readonly float _standingCapsuleHeight;

    private bool _isCrouching;
    private bool _shouldBeCrouching;

    public bool IsCrouching => _isCrouching;

    public CharacterCrouch(CharController character, float standingCapsuleHeight)
    {
        _character = character;
        _standingCapsuleHeight = standingCapsuleHeight;
    }

    public void SetDesired(bool shouldBeCrouching)
    {
        _shouldBeCrouching = shouldBeCrouching;
    }

    public void Begin()
    {
        if (_isCrouching || !_character.IsGrounded) return;
        if (_character.CurrentCharacterState != MoveState.Idle && _character.CurrentCharacterState != MoveState.Move
            && _character.CurrentCharacterState != MoveState.Stop && _character.CurrentCharacterState != MoveState.Land) return;
        _isCrouching = true;
        _character.Motor.SetCapsuleDimensions(_character.Motor.Capsule.radius, _character.CrouchedCapsuleHeight, _character.CrouchedCapsuleHeight * 0.5f);
        _character.characterAnim.SetDirection(0f, 0f);
        _character.SetState(MoveState.Crouch, _character.HasMoveInput ? LocoState.CrouchMove : LocoState.CrouchEnter, 0.15f);
    }

    public void Tick(LocoState currentClip)
    {
        if (!_isCrouching)
        {
            _character.ResumeAfterAttack();
            return;
        }
        if (currentClip == LocoState.CrouchEnter)
        {
            _character.characterAnim.GetStateProgress(currentClip, out float remaining);
            if (!_character.HasMoveInput && remaining > _character.characterAnim.DefaultFade) return;
            _character.SetState(MoveState.Crouch, LocoState.CrouchMove);
        }
        _character.UpdateDirectionParams();
    }

    public void TryUncrouch()
    {
        if (!_isCrouching || _shouldBeCrouching) return;
        KinematicCharacterMotor motor = _character.Motor;
        motor.SetCapsuleDimensions(motor.Capsule.radius, _standingCapsuleHeight, _standingCapsuleHeight * 0.5f);
        if (motor.CharacterOverlap(motor.TransientPosition, motor.TransientRotation, _probedColliders, motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
        {
            motor.SetCapsuleDimensions(motor.Capsule.radius, _character.CrouchedCapsuleHeight, _character.CrouchedCapsuleHeight * 0.5f);
            return;
        }
        _isCrouching = false;
    }
}
