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
    [Tooltip("Tay trái cầm vỏ kiếm / vũ khí phụ. Để trống = xương LeftHand của humanoid")]
    public Transform OffHand;

    [Header("Vũ khí cầm sẵn")]
    [Tooltip("Nhân vật chưa có CharacterCombo (chưa build layer đánh) vẫn cầm được vũ khí này")]
    public WeaponData StarterWeapon;

    [Header("Blend cách cầm")]
    [Tooltip("Thời gian chuyển giữa cách cầm lúc đứng và lúc chém (giây)")]
    public float GripBlendTime = 0.12f;

    [Header("Cất vũ khí sau lưng (ngoài chiến đấu)")]
    [Tooltip("Để trống = xương ngực của humanoid")]
    public Transform BackBone;
    public Vector3 BackPosition;
    public Vector3 BackRotation;
    public float SheathBlendTime = 0.25f;

    private CharController _character;
    private bool _sheathed;
    private float _blendDuration;
    private Transform _socket;
    private GameObject _weaponInstance;
    private WeaponData _weapon;

    private Transform _offSocket;
    private GameObject _offInstance;

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
        if (OffHand == null && animator != null) OffHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (BackBone == null && animator != null)
        {
            BackBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (BackBone == null) BackBone = animator.GetBoneTransform(HumanBodyBones.Chest);
        }
        _character = GetComponentInParent<CharController>();

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

    private void Start()
    {
        // Nhân vật chưa gắn CharacterCombo (chưa build layer đánh) vẫn cầm được vũ khí
        if (characterCombo == null && StarterWeapon != null) Equip(StarterWeapon);
    }

    public void Equip(WeaponData weapon)
    {
        if (_weapon == weapon && _weaponInstance != null) return;

        if (_weaponInstance != null) Destroy(_weaponInstance);
        _weapon = weapon;
        EquipOffHand(weapon);
        if (weapon == null || weapon.WeaponPrefab == null) return;

        // Trong scene mẫu của gói, model vũ khí nằm dưới xương ik_hand_gun với transform gốc
        _weaponInstance = Instantiate(weapon.WeaponPrefab, _socket, false);
        _weaponInstance.name = weapon.WeaponPrefab.name;
        _weaponInstance.transform.localPosition = Vector3.zero;
        _weaponInstance.transform.localRotation = Quaternion.identity;
        _weaponInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, weapon.WeaponScale);
        foreach (Collider col in _weaponInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false; // không để kiếm đẩy capsule KCC
        }

        _socket.localPosition = weapon.IdleGripPosition;
        _socket.localRotation = weapon.IdleGripRotation;
    }

    // Vỏ kiếm / vũ khí phụ ở tay trái: giữ cố định theo cách cầm, không blend theo đòn đánh
    private void EquipOffHand(WeaponData weapon)
    {
        if (_offInstance != null)
        {
            Destroy(_offInstance);
            _offInstance = null;
        }
        if (weapon == null || weapon.OffHandPrefab == null || OffHand == null) return;

        if (_offSocket == null)
        {
            _offSocket = new GameObject("OffHandSocket").transform;
            _offSocket.SetParent(OffHand, false);
        }
        _offSocket.localPosition = weapon.OffHandGripPosition;
        _offSocket.localRotation = weapon.OffHandGripRotation;

        _offInstance = Instantiate(weapon.OffHandPrefab, _offSocket, false);
        _offInstance.name = weapon.OffHandPrefab.name;
        _offInstance.transform.localPosition = Vector3.zero;
        _offInstance.transform.localRotation = Quaternion.identity;
        _offInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, weapon.OffHandScale);
        foreach (Collider col in _offInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
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

        bool sheathed = BackBone != null && _character != null && !attacking && !_character.IsInCombat;
        if (sheathed)
        {
            targetPos = BackPosition;
            targetRot = Quaternion.Euler(BackRotation);
        }

        // Đổi đòn / vào-ra trạng thái đánh / rút-cất kiếm -> blend từ vị trí hiện tại sang vị trí mới
        AttackStep step = characterCombo != null ? characterCombo.CurrentStep : null;
        if (attacking != _wasAttacking || step != _lastStep || sheathed != _sheathed)
        {
            if (sheathed != _sheathed)
            {
                _socket.SetParent(sheathed ? BackBone : Hand, true);
            }
            _fromPos = _socket.localPosition;
            _fromRot = _socket.localRotation;
            _blendDuration = sheathed != _sheathed && step == null ? SheathBlendTime
                : step != null ? characterCombo.CurrentFade : GripBlendTime;
            _blendDuration = Mathf.Max(_blendDuration, 0.01f);
            _blendTimer = _blendDuration;
            _wasAttacking = attacking;
            _lastStep = step;
            _sheathed = sheathed;
        }

        float t = 1f;
        if (_blendTimer > 0f)
        {
            _blendTimer -= Time.deltaTime;
            t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(_blendTimer / _blendDuration));
        }

        _socket.localPosition = Vector3.Lerp(_fromPos, targetPos, t);
        _socket.localRotation = Quaternion.Slerp(_fromRot, targetRot, t);

        // Cho phép chỉnh cách cầm tay trái trực tiếp trong Inspector lúc đang chạy
        if (_offInstance != null)
        {
            _offSocket.localPosition = _weapon.OffHandGripPosition;
            _offSocket.localRotation = _weapon.OffHandGripRotation;
        }
    }
}
