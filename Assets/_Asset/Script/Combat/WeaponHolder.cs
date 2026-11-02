using UnityEngine;

/// <summary>
/// Cho Dante cầm vũ khí đang chọn ở bàn tay phải.
/// Cách cầm (xoay cổ tay, lật kiếm...) lấy theo xương vũ khí trong anim gốc của gói Grruzam
/// (WeaponComboBuilder bake sẵn vào từng AttackStep), nên kiếm ăn khớp với động tác chém.
/// Đặt cùng GameObject với Animator.
/// </summary>
[DefaultExecutionOrder(100)]
public class WeaponHolder : MonoBehaviour
{
    public Animator animator;
    public CharacterCombo characterCombo;
    [Tooltip("Để trống = xương RightHand của humanoid")]
    public Transform Hand;

    [Header("Blend cách cầm")]
    [Tooltip("Thời gian chuyển giữa cách cầm lúc đứng và lúc chém (giây)")]
    public float GripBlendTime = 0.12f;

    private Transform _socket;
    private GameObject _weaponInstance;
    private WeaponData _weapon;

    private Vector3 _fromPos;
    private Quaternion _fromRot = Quaternion.identity;
    private float _blendTimer;
    private bool _wasAttacking;
    private AttackStep _lastStep;

    public Transform Socket => _socket;
    public GameObject WeaponInstance => _weaponInstance;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (characterCombo == null) characterCombo = GetComponent<CharacterCombo>();
        if (Hand == null && animator != null) Hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        _socket = new GameObject("WeaponSocket").transform;
        _socket.SetParent(Hand, false);

        if (characterCombo != null)
        {
            characterCombo.OnWeaponChanged += Equip;
        }
    }

    private void OnDestroy()
    {
        if (characterCombo != null)
        {
            characterCombo.OnWeaponChanged -= Equip;
        }
    }

    public void Equip(WeaponData weapon)
    {
        if (_weapon == weapon && _weaponInstance != null) return;

        if (_weaponInstance != null) Destroy(_weaponInstance);
        _weapon = weapon;
        if (weapon == null || weapon.WeaponPrefab == null) return;

        // Trong scene mẫu của gói, model vũ khí nằm dưới xương ik_hand_gun với transform gốc
        _weaponInstance = Instantiate(weapon.WeaponPrefab, _socket, false);
        _weaponInstance.name = weapon.WeaponPrefab.name;
        _weaponInstance.transform.localPosition = Vector3.zero;
        _weaponInstance.transform.localRotation = Quaternion.identity;
        foreach (Collider col in _weaponInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false; // không để kiếm đẩy capsule KCC
        }

        _socket.localPosition = weapon.IdleGripPosition;
        _socket.localRotation = weapon.IdleGripRotation;
    }

    // Sau Animator (LateUpdate) để ghi đè lên pose tay mới nhất
    private void LateUpdate()
    {
        if (_weapon == null) return;

        Vector3 targetPos = _weapon.IdleGripPosition;
        Quaternion targetRot = _weapon.IdleGripRotation;
        bool attacking = characterCombo != null && characterCombo.TryGetAttackGrip(out targetPos, out targetRot);
        if (!attacking)
        {
            targetPos = _weapon.IdleGripPosition;
            targetRot = _weapon.IdleGripRotation;
        }

        // Đổi đòn / vào-ra trạng thái đánh -> blend từ cách cầm hiện tại sang cách cầm mới
        AttackStep step = characterCombo != null ? characterCombo.CurrentStep : null;
        if (attacking != _wasAttacking || step != _lastStep)
        {
            _fromPos = _socket.localPosition;
            _fromRot = _socket.localRotation;
            _blendTimer = step != null ? Mathf.Max(characterCombo.CurrentFade, 0.01f) : Mathf.Max(GripBlendTime, 0.01f);
            _wasAttacking = attacking;
            _lastStep = step;
        }

        float t = 1f;
        if (_blendTimer > 0f)
        {
            float duration = step != null ? Mathf.Max(characterCombo.CurrentFade, 0.01f) : Mathf.Max(GripBlendTime, 0.01f);
            _blendTimer -= Time.deltaTime;
            t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(_blendTimer / duration));
        }

        _socket.localPosition = Vector3.Lerp(_fromPos, targetPos, t);
        _socket.localRotation = Quaternion.Slerp(_fromRot, targetRot, t);
    }
}
