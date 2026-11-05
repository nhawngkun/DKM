using UnityEngine;

public class CharacterAnim : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Thời gian làm mượt Move_X/Move_Y (giây). 0 = giật theo input")]
    public float LocomotionDampTime = 0.1f;
    public bool isMove;
    public bool isRun;
    public bool isGrounded;
    

    public float _IdleId;
    [Tooltip("Thời gian blend idle khi đổi vũ khí (giây)")]
    public float IdleBlendTime = 0.25f;

    [Header("Idle thường (ngoài chiến đấu)")]
    public bool inCombat;
    [Tooltip("Số clip trong blend tree idle thường")]
    public int NormalIdleCount = 3;
    public float NormalIdleMinTime = 8f;
    public float NormalIdleMaxTime = 15f;
    public float CombatBlendTime = 0.3f;
    private int _normalIdleId;
    private float _normalIdleTimer;

    public float _MoveId;
    public float _RunId;
   


    public float _moveX;
    public float _moveY;

    [Header("Locomotion")]
    public float moveSpeed;          // m/s thật -> blend Walk / Run / RunFast
    public float moveAnimSpeed = 1f; // hệ số phát anim để bước chân khớp tốc độ (anim là Inplace)
    public bool towardMove = true;   // true = chạy tự do (quay theo hướng chạy), false = strafe 8 hướng (lock-on)
    public float SpeedDampTime = 0.15f;

    [Header("Air")]
    public string JumpLayerName = "Jump";
    [Tooltip("State vòng lặp trên không của từng bộ anim nhảy (index = WeaponData.AnimSetId)")]
    public string[] JumpLoopStates = { "Jump_Loop_ZeroHeight", "GS_Jump_Loop" };
    private int _animSet;

    [Header("Dash")]
    public string DashLayerName = "Dash";
    public float DashCancelFade = 0.05f;
    private static readonly int DashEmptyState = Animator.StringToHash("New State");


    public void Update()
    {
        UpdateNormalIdle();
        ApplyToAnimator();
    }

    /// <summary>Đổi idle thường khi hết thời gian, hoặc khi vừa chạy xong dừng lại.</summary>
    private void UpdateNormalIdle()
    {
        if (isMove || inCombat)
        {
            _normalIdleTimer = 0f;
            return;
        }
        _normalIdleTimer -= Time.deltaTime;
        if (_normalIdleTimer > 0f) return;

        _normalIdleTimer = Random.Range(NormalIdleMinTime, NormalIdleMaxTime);
        if (NormalIdleCount > 1)
        {
            _normalIdleId = (_normalIdleId + Random.Range(1, NormalIdleCount)) % NormalIdleCount;
        }
    }
    public void Dash()
    {
        animator.SetTrigger(AnimatorParameters.DASHING);
    }
    public void EndDash()
    {
        animator.SetTrigger(AnimatorParameters.END_DASH);
    }
    /// <summary>Nhảy hủy dash: tắt nhanh layer Dash để anim nhảy hiện ra ngay.</summary>
    public void CancelDash()
    {
        int layer = animator.GetLayerIndex(DashLayerName);
        if (layer < 0) { EndDash(); return; }
        animator.ResetTrigger(AnimatorParameters.DASHING);
        animator.CrossFadeInFixedTime(DashEmptyState, DashCancelFade, layer);
    }
    public void Jump()
    {
        animator.ResetTrigger(AnimatorParameters.ON_LANDING);
        animator.SetTrigger(AnimatorParameters.JUMP);
    }
    public void AirJump()
    {
        animator.SetTrigger(AnimatorParameters.DOUBLE_JUMP);
        OnGround(false);
    }
    /// <summary>Rơi tự do không qua nhảy (sau đòn hất tung / air combo): vào thẳng vòng lặp trên không.</summary>
    public void Fall()
    {
        int layer = animator.GetLayerIndex(JumpLayerName);
        if (layer < 0 || JumpLoopStates.Length == 0) return;
        string state = JumpLoopStates[Mathf.Clamp(_animSet, 0, JumpLoopStates.Length - 1)];
        animator.CrossFadeInFixedTime(Animator.StringToHash(state), 0.15f, layer);
        OnGround(false);
    }

    /// <summary>Đổi bộ anim idle/nhảy theo vũ khí đang cầm.</summary>
    public void SetWeapon(int animSet, float idleId)
    {
        _animSet = animSet;
        _IdleId = idleId;
        animator.SetInteger(AnimatorParameters.WEAPON_ID, animSet);
    }

    public void OnLanding()
    {
        animator.SetTrigger(AnimatorParameters.ON_LANDING);
        animator.ResetTrigger(AnimatorParameters.JUMP);
        animator.ResetTrigger(AnimatorParameters.DOUBLE_JUMP);
    }

    public void OnGround(bool value)
    {
        if (isGrounded == value) return;
        isGrounded = value;
        animator.SetBool(AnimatorParameters.IS_GROUND, isGrounded);
    }


    public bool ChangeOrientation(bool method)
    {
        animator.SetBool(AnimatorParameters.TOWARD_MOVE, method);
        return method;
    }

    public  void ApplyToAnimator()
    {
        float deltaTime = Time.deltaTime;
        animator.SetFloat(AnimatorParameters.MOVE__X, _moveX, LocomotionDampTime, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE__Y, _moveY, LocomotionDampTime, deltaTime);
        animator.SetFloat(AnimatorParameters.IDLE_ID, _IdleId, IdleBlendTime, deltaTime);
        animator.SetFloat(AnimatorParameters.NORMAL_IDLE_ID, _normalIdleId, IdleBlendTime, deltaTime);
        animator.SetFloat(AnimatorParameters.IN_COMBAT, inCombat ? 1f : 0f, CombatBlendTime, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE_ID, _MoveId, 0f, deltaTime);
        animator.SetBool(AnimatorParameters.IS_MOVE, isMove);
        animator.SetFloat(AnimatorParameters.MOVE_SPEED, moveSpeed, SpeedDampTime, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE_ANIM_SPEED, moveAnimSpeed, SpeedDampTime, deltaTime);
        animator.SetBool(AnimatorParameters.TOWARD_MOVE, towardMove);
    }




    public void Move(float horizontalInput)
    {
        isMove = horizontalInput != 0;
        animator.SetBool(AnimatorParameters.IS_MOVE, isMove);
    }

}