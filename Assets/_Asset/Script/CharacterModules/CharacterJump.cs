using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Giữ jump buffer, coyote time, double jump, air timer và toàn bộ physics trên không.
/// CharController vẫn quyết định thời điểm gọi và tự set state/animation để giữ nguyên thứ tự callback của KCC.
/// </summary>
internal sealed class CharacterJump
{
    private readonly CharController _character;

    private JumpProfile _profile;
    private bool _requested;
    private bool _consumed;
    private bool _jumpedThisTick;
    private float _sinceRequested;
    private float _sinceLastAble;
    private int _airJumpsLeft;
    private bool _usedAirJump;
    private float _airborneTime;

    public bool JumpConsumed => _consumed;
    public float AirborneTime => _airborneTime;

    public CharacterJump(CharController character)
    {
        _character = character;
        _airJumpsLeft = character.MaxAirJumps;
    }

    public void SetProfile(JumpProfile profile)
    {
        _profile = profile;
    }

    // giữ input nhảy lại trong JumpPreGroundingGraceTime: bấm hơi sớm trước khi chạm đất vẫn nhảy được
    public void Request()
    {
        _sinceRequested = 0f;
        _requested = true;
    }

    public void MarkConsumed()
    {
        _consumed = true;
    }

    public void MarkAirJump(bool airJump)
    {
        _usedAirJump = airJump;
    }

    // đọc xong thì xoá: chỉ dùng một lần cho clip tiếp đất
    public bool TakeAirJumpFlag()
    {
        bool wasAirJump = _usedAirJump;
        _usedAirJump = false;
        return wasAirJump;
    }

    public void AddAirborneTime(float deltaTime)
    {
        _airborneTime += deltaTime;
    }

    public void ResetAirborneTime()
    {
        _airborneTime = 0f;
    }

    /// <returns>0 = không nhảy, 1 = nhảy từ đất, 2 = nhảy trên không.</returns>
    public int HandleJump(ref Vector3 currentVelocity, float deltaTime)
    {
        _jumpedThisTick = false;
        _sinceRequested += deltaTime;
        if (!_requested) return 0;

        bool canGroundJump = !_consumed && (_character.IsGrounded || _sinceLastAble <= _character.JumpPostGroundingGraceTime);
        bool canAirJump = !canGroundJump && !_character.IsGrounded && _airJumpsLeft > 0;
        if (!canGroundJump && !canAirJump) return 0;

        Vector3 up = _character.Motor.CharacterUp;
        _character.Motor.ForceUnground();

        float upSpeed = (_profile != null ? _profile.UpSpeed : _character.JumpUpSpeed) * Mathf.Sqrt(_character.JumpHeightScale);
        if (canAirJump)
        {
            // double jump: bẻ hướng theo stick, giữ lại một phần quán tính cũ
            if (_character.HasMoveInput)
            {
                Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, up);
                Vector3 steered = _character.MoveInputDirection * Mathf.Max(planar.magnitude, _character.RunSpeed);
                currentVelocity += Vector3.Lerp(steered, planar, _character.AirJumpKeepHorizontal) - planar;
            }
            upSpeed = (_profile != null ? _profile.AirUpSpeed : _character.AirJumpUpSpeed) * Mathf.Sqrt(_character.AirJumpHeightScale);
            _airJumpsLeft--;
        }

        currentVelocity += up * upSpeed - Vector3.Project(currentVelocity, up);
        _requested = false;
        _consumed = true;
        _jumpedThisTick = true;
        return canAirJump ? 2 : 1;
    }

    // hết frame: bỏ input nhảy quá hạn, và khi chạm đất thì reset buffer + số lần nhảy trên không
    public void AfterCharacterUpdate(bool grounded, float deltaTime)
    {
        if (_requested && _sinceRequested > _character.JumpPreGroundingGraceTime) _requested = false;

        if (grounded)
        {
            if (!_jumpedThisTick) _consumed = false;
            _sinceLastAble = 0f;
            _airJumpsLeft = _character.MaxAirJumps;
            _airborneTime = 0f;
        }
        else
        {
            _sinceLastAble += deltaTime;
        }
    }

    // điều khiển trên không: bẻ lái theo input, trọng lực riêng lúc lên/xuống, drag và chặn tốc độ rơi
    public void UpdateAirVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector3 up = _character.Motor.CharacterUp;
        if (_character.HasMoveInput)
        {
            Vector3 added = _character.MoveInputDirection * _character.AirAccelerationSpeed * deltaTime;
            Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, up);
            float maxSpeed = Mathf.Max(_character.MaxAirMoveSpeed, planar.magnitude);
            added = Vector3.ClampMagnitude(planar + added, maxSpeed) - planar;

            KinematicCharacterMotor motor = _character.Motor;
            if (motor.GroundingStatus.FoundAnyGround && Vector3.Dot(currentVelocity + added, added) > 0f)
            {
                Vector3 obstruction = Vector3.Cross(Vector3.Cross(up, motor.GroundingStatus.GroundNormal), up).normalized;
                added = Vector3.ProjectOnPlane(added, obstruction);
            }
            currentVelocity += added;
        }

        currentVelocity += GetAirGravity(currentVelocity) * deltaTime;
        currentVelocity *= 1f / (1f + _character.Drag * deltaTime);

        float maxFall = _profile != null ? _profile.MaxFallSpeed : _character.MaxFallSpeed;
        Vector3 gravityDirection = _character.Gravity.normalized;
        float fallSpeed = Vector3.Dot(currentVelocity, gravityDirection);
        if (fallSpeed > maxFall) currentVelocity -= gravityDirection * (fallSpeed - maxFall);
    }

    private Vector3 GetAirGravity(Vector3 velocity)
    {
        Vector3 gravityDirection = _character.Gravity.normalized;
        bool falling = Vector3.Dot(velocity, gravityDirection) > 0f;
        if (_profile == null) return _character.Gravity * (falling ? _character.FallGravityMultiplier : 1f);

        float rise = _usedAirJump ? _profile.AirRiseGravity : _profile.RiseGravity;
        float fallMultiplier = _usedAirJump ? _profile.AirFallGravityMultiplier : _profile.FallGravityMultiplier;
        return gravityDirection * rise * (falling ? fallMultiplier : 1f);
    }
}
