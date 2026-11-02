using UnityEngine;

public static class AnimatorParameters
{
	//Dante
	public static readonly int MOVE_ID = Animator.StringToHash ("MoveID");
	public static readonly int IDLE_ID = Animator.StringToHash ("IdleID");
	public static readonly int MOVE__X = Animator.StringToHash ("Move_X");
	public static readonly int MOVE__Y = Animator.StringToHash ("Move_Y");
	public static readonly int IS_MOVE = Animator.StringToHash ("IsMove");
	public static readonly int MOVE_SPEED = Animator.StringToHash ("MoveSpeed");          // m/s thật của nhân vật (blend Walk/Run/RunFast)
	public static readonly int MOVE_ANIM_SPEED = Animator.StringToHash ("MoveAnimSpeed"); // hệ số tốc độ phát anim di chuyển
	public static readonly int TOWARD_MOVE = Animator.StringToHash ("TowardMove");
	public static readonly int JUMP = Animator.StringToHash ("Jump");
	public static readonly int DOUBLE_JUMP = Animator.StringToHash ("DoubleJump");
	public static readonly int WEAPON_ID = Animator.StringToHash ("WeaponID");          // bộ anim nhảy theo vũ khí
	public static readonly int ON_LANDING = Animator.StringToHash ("OnLanding");
	public static readonly int IS_GROUND = Animator.StringToHash ("IsGround");
	public static readonly int DASHING = Animator.StringToHash ("Dashing");
	public static readonly int END_DASH = Animator.StringToHash ("EndDash");
}
