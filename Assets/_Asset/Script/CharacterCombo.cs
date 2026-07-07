using UnityEngine;

public class CharacterCombo : MonoBehaviour
{
    public CharacterAnim characterAnim;

    [Header("Combo Attack")]
    public KeyCode AttackKey = KeyCode.K;

    [Tooltip("Số đòn tối đa của từng combo. Index 0 = Combo1, index 1 = Combo2, ... index 5 = Combo6")]
    public int[] ComboLengths = new int[6] { 3, 3, 3, 3, 3, 3 };

    public float ComboResetTime = 1.2f;      // Quá thời gian này mà không đánh tiếp -> reset về đòn 1 và random lại combo mới

    private int _comboIndex = 0;             // 0 = đòn 1, 1 = đòn 2, 2 = đòn 3, ...
    private int _currentComboGroup = -1;     // Combo đang được chọn (0-5). -1 = chưa chọn, cần random khi bắt đầu chuỗi mới
    private int _currentMaxCombo = 0;        // MaxCombo của combo đang chọn, lấy từ ComboLengths[_currentComboGroup]

    private bool _isAttacking = false;       // Đang trong lúc chạy anim của 1 đòn đánh (chưa nhận anim event kết thúc)
    private bool _queuedAttack = false;      // Người chơi đã bấm phím trong lúc đòn hiện tại đang chạy
    private float _lastAttackTime = -999f;   // Thời điểm bắt đầu đòn đánh gần nhất

    public void Update()
    {
        if (Input.GetKeyDown(AttackKey))
        {
            OnAttackInput();
        }
    }

    public void OnAttackInput()
    {
        // Hết thời gian combo -> reset về đòn 1, buộc random lại combo mới ở lần StartAttack kế tiếp
        if (Time.time - _lastAttackTime > ComboResetTime)
        {
            ResetCombo();
        }

        if (!_isAttacking)
        {
            StartAttack();
        }
        else
        {
            _queuedAttack = true;
        }
    }

    private void ResetCombo()
    {
        _comboIndex = 0;
        _queuedAttack = false;
        _currentComboGroup = -1; // -1 nghĩa là chưa chọn combo, StartAttack() sẽ random lại
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _queuedAttack = false;
        _lastAttackTime = Time.time;

        // Nếu chưa có combo nào đang active (đầu chuỗi mới, hoặc vừa reset) -> random 1 trong 6 combo
        if (_currentComboGroup == -1)
        {
            _currentComboGroup = Random.Range(0, ComboLengths.Length);
            _currentMaxCombo = Mathf.Max(1, ComboLengths[_currentComboGroup]);
            SetActiveComboFlag(_currentComboGroup);
        }

        characterAnim._ComboId = _comboIndex; // 0 = đòn 1, 1 = đòn 2, 2 = đòn 3, ...
    }

    // Bật đúng 1 cờ IsComboX tương ứng với combo đang được chọn, tắt các cờ còn lại
    private void SetActiveComboFlag(int group)
    {
        characterAnim.isCombo1 = group == 0;
        characterAnim.isCombo2 = group == 1;
        characterAnim.isCombo3 = group == 2;
        characterAnim.isCombo4 = group == 3;
        characterAnim.isCombo5 = group == 4;
        characterAnim.isCombo6 = group == 5;
    }

    private void ClearComboFlags()
    {
        characterAnim.isCombo1 = false;
        characterAnim.isCombo2 = false;
        characterAnim.isCombo3 = false;
        characterAnim.isCombo4 = false;
        characterAnim.isCombo5 = false;
        characterAnim.isCombo6 = false;
    }

    public void OnComboAnimEnd()
    {
        _isAttacking = false;

        bool stillInComboWindow = Time.time - _lastAttackTime <= ComboResetTime;

        if (_queuedAttack && stillInComboWindow)
        {
            int nextIndex = _comboIndex + 1;

            if (nextIndex >= _currentMaxCombo)
            {
                // Đã đánh hết đòn cuối cùng của combo hiện tại rồi quay lại đòn 1 -> random combo mới
                _comboIndex = 0;
                _currentComboGroup = -1; // buộc StartAttack() random lại combo khác
            }
            else
            {
                // Còn bấm tiếp trong lúc đòn vừa rồi đang chạy -> qua đòn kế của combo hiện tại
                _comboIndex = nextIndex;
            }

            StartAttack();
        }
        else
        {
            // Không bấm tiếp (hoặc bấm quá trễ) -> kết thúc chuỗi combo, trả về đòn 1
            _comboIndex = 0;
            _queuedAttack = false;
            _currentComboGroup = -1; // lần đánh tiếp theo sẽ random combo mới
            ClearComboFlags();       // ApplyToAnimator() sẽ tự đẩy giá trị này vào Animator ở frame kế tiếp
        }
    }
}