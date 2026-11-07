using UnityEngine;

// Dead blending: chụp pose đang hiển thị rồi hòa dần về pose của Animator, dùng cho chỗ không crossfade được
// (motor vừa xoay bù góc đã bake trong clip). Chạy đầu LateUpdate để IK vẫn tính trên pose đã hòa.
[DefaultExecutionOrder(-300)]
[RequireComponent(typeof(Animator))]
public class PoseBlend : MonoBehaviour
{
    private const int LastBodyBone = (int)HumanBodyBones.RightToes;

    public float FadeTime = 0.18f;

    private Transform[] _bones;
    private Quaternion[] _startRotations;
    private Transform _root;
    private Quaternion _hipsStartRotation;
    private Vector3 _hipsStartOffset;
    private float _time = float.MaxValue;

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        _root = transform.parent != null ? transform.parent : transform;
        _bones = new Transform[LastBodyBone + 2];
        for (int i = 0; i <= LastBodyBone; i++) _bones[i] = animator.GetBoneTransform((HumanBodyBones)i);
        _bones[LastBodyBone + 1] = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        _startRotations = new Quaternion[_bones.Length];
    }

    public void Capture()
    {
        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] != null) _startRotations[i] = _bones[i].localRotation;
        }
        Transform hips = _bones[(int)HumanBodyBones.Hips];
        _hipsStartRotation = hips.rotation;
        _hipsStartOffset = hips.position - _root.position;
        _time = 0f;
    }

    private void LateUpdate()
    {
        if (_time >= FadeTime) return;
        _time += Time.deltaTime;
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_time / FadeTime));

        // hông là phần tử 0, tính riêng theo thế giới vì gốc nhân vật vừa bị xoay bù: local cũ lệch cả góc bù
        for (int i = 1; i < _bones.Length; i++)
        {
            if (_bones[i] != null) _bones[i].localRotation = Quaternion.Slerp(_startRotations[i], _bones[i].localRotation, weight);
        }
        Transform hips = _bones[(int)HumanBodyBones.Hips];
        hips.rotation = Quaternion.Slerp(_hipsStartRotation, hips.rotation, weight);
        hips.position = Vector3.Lerp(_root.position + _hipsStartOffset, hips.position, weight);
    }
}
