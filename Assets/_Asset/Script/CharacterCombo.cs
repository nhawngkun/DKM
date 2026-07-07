using System.Collections.Generic;
using UnityEngine;

public class CharacterCombo : MonoBehaviour
{
    public CharacterAnim characterAnim;

    [Header("Combo Attack")]
    public KeyCode AttackKey = KeyCode.K;
    public float ComboResetTime = 1.2f;      // Quá thời gian này mà không đánh tiếp -> reset về đòn 1

    [Tooltip("Số đòn (MaxCombo) của từng combo. Index 0-5 tương ứng Combo1 -> Combo6")]
    public List<int> ComboLengths = new List<int> { 3, 3,3 };

    [Tooltip("Layer trên Animator đang chạy animation đánh, dùng để kiểm tra khi nào clip thật sự kết thúc")]
    public int ComboAnimatorLayer = 0;

    private int _selectedCombo = 0;          // Combo (0-5) đang được random và sử dụng
    private int _comboIndex = 0;             // Đòn hiện tại trong combo đã chọn (0 = đòn 1, ...)
    private bool _isAttacking = false;       // Đang trong lúc chạy anim của 1 đòn đánh (chưa cho phép bắt đầu đòn mới)
    private bool _queuedAttack = false;      // Người chơi đã bấm phím trong lúc đòn hiện tại đang chạy
    private float _lastAttackTime = -999f;   // Thời điểm bắt đầu đòn đánh gần nhất
    private bool _needReroll = true;         // true = lần StartAttack tới phải random lại combo mới

    // OnComboAnimEnd() là Animation Event đặt ở GIỮA clip (mở cửa sổ để đánh tiếp/hủy đòn sớm),
    // không phải lúc clip thật sự kết thúc. Nếu hết ComboResetTime mà không bấm tiếp thì KHÔNG
    // được cắt ngang animation đang chạy ngay lúc đó -> phải chờ animation chạy hết rồi mới reset thật sự.
    private bool _waitingForAnimFinish = false;

    public void Update()
    {
        if (Input.GetKeyDown(AttackKey))
        {
            OnAttackInput();
        }

        if (_waitingForAnimFinish)
        {
            CheckAnimFinished();
        }
    }

    public void OnAttackInput()
    {
        if (Time.time - _lastAttackTime > ComboResetTime)
        {
            // Hết thời gian combo -> reset về đòn 1 và random lại combo mới
            _comboIndex = 0;
            _queuedAttack = false;
            _needReroll = true;
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

    private void StartAttack()
    {
        _waitingForAnimFinish = false;

        if (_needReroll)
        {
            // Chỉ random combo mới khi bắt đầu 1 chuỗi mới (đòn đầu tiên)
            _selectedCombo = Random.Range(0, ComboLengths.Count);
            _comboIndex = 0;
            _needReroll = false;
        }

        _isAttacking = true;
        _queuedAttack = false;
        _lastAttackTime = Time.time;

        SetActiveCombo(_selectedCombo, true);
        characterAnim._ComboId = _comboIndex; // 0 = đòn 1, 1 = đòn 2, ...
    }

    public void OnComboAnimEnd()
    {
        bool stillInComboWindow = Time.time - _lastAttackTime <= ComboResetTime;
        int maxCombo = ComboLengths[_selectedCombo];

        if (_queuedAttack && stillInComboWindow)
        {
            // Có bấm tiếp trong lúc đòn vừa rồi đang chạy -> hủy ngay, chuyển qua đòn kế
            _isAttacking = false;

            _comboIndex++;

            if (_comboIndex >= maxCombo)
            {
                // Đánh hết đòn cuối rồi quay lại đòn đầu -> random combo mới
                SetActiveCombo(_selectedCombo, false);
                _needReroll = true;
            }

            StartAttack();
        }
        else
        {
            // Không bấm tiếp (hoặc bấm quá trễ) -> KHÔNG cắt ngang animation đang chạy.
            // Vẫn giữ _isAttacking = true để chặn đòn mới bắt đầu ngay lập tức,
            // chờ animation hiện tại chạy hết thật sự rồi mới reset combo trong CheckAnimFinished().
            _waitingForAnimFinish = true;
        }
    }

    // Kiểm tra animation của đòn đánh hiện tại đã chạy hết (normalizedTime >= 1 và không còn transition dở dang)
    private void CheckAnimFinished()
    {
        if (characterAnim == null || characterAnim.animator == null) return;

        Animator animator = characterAnim.animator;
        if (animator.IsInTransition(ComboAnimatorLayer)) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(ComboAnimatorLayer);
        if (state.normalizedTime >= 1f)
        {
            FinishComboChain();
        }
    }

    private void FinishComboChain()
    {
        _waitingForAnimFinish = false;
        _isAttacking = false;

        SetActiveCombo(_selectedCombo, false); // ApplyToAnimator() sẽ tự đẩy giá trị này vào Animator ở frame kế tiếp
        _comboIndex = 0;
        _needReroll = true; // lần đánh tiếp theo (đòn 1 mới) sẽ random lại combo

        if (_queuedAttack)
        {
            // Người chơi đã bấm đánh trong lúc chờ animation cũ chạy hết -> bắt đầu ngay chuỗi combo mới
            StartAttack();
        }
    }

    // Bật/tắt đúng bool isComboX trên CharacterAnim tương ứng với combo đã chọn (0-5 -> Combo1-Combo6)
    private void SetActiveCombo(int comboIndex, bool value)
    {
        switch (comboIndex)
        {
            case 0: characterAnim.isCombo1 = value; break;
            case 1: characterAnim.isCombo2 = value; break;
            case 2: characterAnim.isCombo3 = value; break;
            //case 3: characterAnim.isCombo4 = value; break;
            //case 4: characterAnim.isCombo5 = value; break;
            //case 5: characterAnim.isCombo6 = value; break;
        }
    }
}