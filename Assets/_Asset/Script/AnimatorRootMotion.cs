using UnityEngine;

// nằm cùng GameObject với Animator: có OnAnimatorMove thì Unity không tự dời transform, delta được tích lại cho KCC
[RequireComponent(typeof(Animator))]
public class AnimatorRootMotion : MonoBehaviour
{
    private Animator _animator;
    private Vector3 _position;
    private float _time;
    private Vector3 _lastVelocity;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnAnimatorMove()
    {
        _position += _animator.deltaPosition;
        _time += Time.deltaTime;
    }

    // motor chạy theo FixedUpdate, FPS thấp có thể không có delta mới giữa 2 tick -> giữ vận tốc cũ để khỏi giật
    public Vector3 ConsumeVelocity()
    {
        if (_time <= 0f) return _lastVelocity;
        _lastVelocity = _position / _time;
        _position = Vector3.zero;
        _time = 0f;
        return _lastVelocity;
    }
}
