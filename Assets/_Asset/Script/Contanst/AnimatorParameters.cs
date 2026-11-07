using UnityEngine;

public static class AnimatorParameters
{
	public static readonly int GAIT = Animator.StringToHash ("Gait");     // 1 = đi, 2 = jog, 3 = chạy, 4 = chạy nhanh
	public static readonly int MOVE__X = Animator.StringToHash ("Move_X"); // hướng di chuyển theo local nhân vật (strafe, crouch)
	public static readonly int MOVE__Y = Animator.StringToHash ("Move_Y");
}
