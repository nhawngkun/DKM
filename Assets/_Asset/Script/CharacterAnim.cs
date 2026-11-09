using System;
using UnityEngine;

public enum LocoState
{
    Idle, IdleTurnL90, IdleTurnR90, IdleTurn180,
    Loop, Strafe,
    StartWalk, StartJog, StartRun,
    StartWalkL90, StartWalkR90, StartJogL90, StartJogR90, StartRunL90, StartRunR90,
    StartWalk180, StartJog180, StartRun180,
    StopWalk, StopRun, StopFast,
    TurnWalkL90, TurnWalkR90, TurnJogL90, TurnJogR90,
    TurnWalk180, TurnJog180, TurnRun180,
    JumpStart, JumpLoop, JumpEnd, AirJumpStart, AirJumpLoop, AirJumpEnd,
    DodgeFront, DodgeBack, DodgeLeft, DodgeRight, RunDodgeLeft, RunDodgeRight,
    CrouchEnter, CrouchMove,
    // mode bay (Trish): thêm ở cuối để giá trị int của các state cũ trong AnimSetLibrary không đổi
    FlyStart, FlyIdle, FlyMove, FlyEnd,
}

public static class LocoStates
{
    public const int Layer = 0;
    // Danh sách prefix do tool sinh ra từ AnimSetLibrary (LocoSetPrefixes.Generated.cs), thứ tự khớp WeaponData.AnimSetId
    public static readonly string[] SetPrefixes = LocoSetPrefixes.Values;
    private static readonly int[,] Hashes = BuildHashes();

    public static int Hash(int set, LocoState state) => Hashes[set, (int)state];

    private static int[,] BuildHashes()
    {
        string[] names = Enum.GetNames(typeof(LocoState));
        int[,] hashes = new int[SetPrefixes.Length, names.Length];
        for (int set = 0; set < SetPrefixes.Length; set++)
        {
            for (int i = 0; i < names.Length; i++) hashes[set, i] = Animator.StringToHash(SetPrefixes[set] + names[i]);
        }
        return hashes;
    }
}

public class CharacterAnim : MonoBehaviour
{
    public Animator animator;
    public bool towardMove = true; // true = quay theo hướng chạy, false = strafe (PlayerIK đọc)
    public float DefaultFade = 0.18f;
    public float GaitDampTime = 0.2f;
    public float DirectionDampTime = 0.12f;

    public AnimatorRootMotion RootMotion { get; private set; }
    private PoseBlend _poseBlend;

    private int _set;
    private float _gait = 1f;
    private float _moveX;
    private float _moveY;
    private bool _poseCaptured;

    private void Awake()
    {
        RootMotion = animator.GetComponent<AnimatorRootMotion>();
        if (RootMotion == null) Debug.LogError("[CharacterAnim] Thiếu AnimatorRootMotion trên Animator. Chạy Tools/Combat/Build Locomotion Animator.", this);
        _poseBlend = animator.GetComponent<PoseBlend>();
        if (_poseBlend == null) Debug.LogError("[CharacterAnim] Thiếu PoseBlend trên Animator. Chạy Tools/Combat/Build Locomotion Animator.", this);
        animator.applyRootMotion = true;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        animator.SetFloat(AnimatorParameters.GAIT, _gait, GaitDampTime, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE__X, _moveX, DirectionDampTime, deltaTime);
        animator.SetFloat(AnimatorParameters.MOVE__Y, _moveY, DirectionDampTime, deltaTime);
    }

    public void SetGait(float gait) => _gait = gait;

    public void SetDirection(float x, float y)
    {
        _moveX = x;
        _moveY = y;
    }

    // đổi bộ anim giữa chừng thì phát lại state hiện tại của bộ mới để không kẹt ở bộ cũ
    public void SetWeapon(int animSet, LocoState current)
    {
        if (animSet < 0 || animSet >= LocoStates.SetPrefixes.Length)
        {
            Debug.LogError($"[CharacterAnim] WeaponData.AnimSetId không hợp lệ: {animSet}", this);
            return;
        }
        if (_set == animSet) return;
        _set = animSet;
        Play(current);
    }

    public bool HasState(LocoState state) => animator.HasState(LocoStates.Layer, LocoStates.Hash(_set, state));

    public void Play(LocoState state, float fade = -1f)
    {
        animator.CrossFadeInFixedTime(LocoStates.Hash(_set, state), fade < 0f ? DefaultFade : fade, LocoStates.Layer);
    }

    // gọi trước khi xoay motor: pose phải được chụp lúc còn đúng hướng cũ
    public void CapturePose()
    {
        if (_poseBlend != null) _poseBlend.Capture();
        _poseCaptured = true;
    }

    // chuyển tức thì: dùng sau khi xoay motor bù góc đã bake trong pose, crossfade sẽ quay lộn cả người
    public void PlayInstant(LocoState state)
    {
        if (_poseBlend != null && !_poseCaptured) _poseBlend.Capture();
        _poseCaptured = false;
        animator.Play(LocoStates.Hash(_set, state), LocoStates.Layer, 0f);
    }

    // < 0 khi animator chưa vào state này (crossfade vừa gọi, chưa tới frame cập nhật)
    public float GetStateProgress(LocoState state, out float remainingSeconds)
    {
        AnimatorStateInfo info = animator.IsInTransition(LocoStates.Layer)
            ? animator.GetNextAnimatorStateInfo(LocoStates.Layer)
            : animator.GetCurrentAnimatorStateInfo(LocoStates.Layer);
        remainingSeconds = float.MaxValue;
        if (info.shortNameHash != LocoStates.Hash(_set, state)) return -1f;
        remainingSeconds = (1f - info.normalizedTime) * info.length;
        return info.normalizedTime;
    }
}
