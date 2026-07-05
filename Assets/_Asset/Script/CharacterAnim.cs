using UnityEngine;

public class CharacterAnim : MonoBehaviour
{
    public Animator animator;
    public bool isMove;
    public bool isRun;

    public float _IdleId;
    public float _MoveId;
    public float _RunId;


    public float _moveX;
    public float _moveY;


    public void Update()
    {
        ApplyToAnimator();  
    }

    public void Jump()
    {
        animator.SetTrigger(AnimatorParameters.JUMP);
    }
    public void OnLanding()
    {
        animator.SetTrigger(AnimatorParameters.ON_LANDING);
    }
    public void SetRun() 
    {
        isRun = !isRun;
        animator.SetBool(AnimatorParameters.IS_RUN, isRun);
    }
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
