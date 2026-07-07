using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

public class CharacterAnim : MonoBehaviour
{
    public Animator animator;
    public bool isMove;
    public bool isRun;
    public bool isGrounded;
    public bool isCombo1;
    public bool isCombo2;
    public bool isCombo3;
    public bool isCombo4;
    public bool isCombo5;
    public bool isCombo6;

    public float _IdleId;
    public float _MoveId;
    public float _RunId;
    public int _ComboId;


    public float _moveX;
    public float _moveY;


    public void Update()
    {
        ApplyToAnimator();
    }
    public void Dash()
    {
        animator.SetTrigger(AnimatorParameters.DASHING);
    }
    public void EndDash()
    {
        animator.SetTrigger(AnimatorParameters.END_DASH);
    }
    public void Jump()
    {
        animator.SetTrigger(AnimatorParameters.JUMP);
    }
    public void OnLanding()
    {
        animator.SetTrigger(AnimatorParameters.ON_LANDING);
        animator.ResetTrigger(AnimatorParameters.JUMP);
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


    //public void SetRun(bool value)
    //{
    //    if (isRun == value) return;
    //    isRun = value;
    //    animator.SetBool(AnimatorParameters.IS_RUN, isRun);
    //}

    public  void ApplyToAnimator()
    {
        float deltaTime = Time.deltaTime;
        animator.SetFloat(AnimatorParameters.MOVE__X, _moveX, 0f, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE__Y, _moveY, 0f, deltaTime);
        animator.SetFloat(AnimatorParameters.IDLE_ID, _IdleId, 0f, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE_ID, _MoveId, 0f, deltaTime);
        animator.SetBool(AnimatorParameters.IS_MOVE, isMove);
    }



    public void Move(float horizontalInput)
    {
        isMove = horizontalInput != 0;
        animator.SetBool(AnimatorParameters.IS_MOVE, isMove);
    }

}