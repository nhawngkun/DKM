using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông tin về 1 đòn đánh trong combo, phát ra qua event OnAttackHit
/// để các hệ thống khác (camera, VFX, SFX, rumble...) lắng nghe mà không cần
/// CharacterCombo phải biết/tham chiếu trực tiếp tới chúng.
/// </summary>
public struct ComboHitInfo
{
    public int SelectedCombo;   // Combo nào đang được chạy (0 = Combo1, 1 = Combo2, ...)
    public int HitIndex;        // Đòn thứ mấy trong combo (0 = đòn 1, 1 = đòn 2, ...)
    public int MaxCombo;        // Tổng số đòn của combo đang chạy
    public bool IsFinalHit;     // true nếu đây là đòn cuối cùng của combo (đòn "chốt hạ")

    // 0..1, dùng để nội suy độ mạnh hiệu ứng: đòn đầu nhẹ, đòn cuối mạnh nhất
    public float Progress01 => MaxCombo <= 1 ? 1f : (float)HitIndex / (MaxCombo - 1);
}

public class CharacterCombo : MonoBehaviour
{
    /// <summary>
    /// Bắn ra mỗi khi 1 đòn đánh MỚI thực sự bắt đầu (StartAttack()).
    /// Camera / VFX / SFX nên subscribe vào event này thay vì poll biến nội bộ.
    /// </summary>
    public event Action<ComboHitInfo> OnAttackHit;

    public Animator animator;
    public int _ComboId;
    public bool isCombo1;
    public bool isCombo2;
    public bool isCombo3;
    //public bool isCombo4;
    //public bool isCombo5;
    //public bool isCombo6;

    [Header("Combo Attack")]
    public KeyCode AttackKey = KeyCode.K;
    public float ComboResetTime = 1.2f;      // Quá thời gian này mà không đánh tiếp -> reset về đòn 1

    [Tooltip("Số đòn (MaxCombo) của từng combo. Index 0-5 tương ứng Combo1 -> Combo6")]
    public List<int> ComboLengths = new List<int> { 3, 3, 3 };

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

        // Luôn đẩy giá trị isCombo1/2/3 (và Combo_ID) sang Animator mỗi frame,
        // để khi FinishComboChain() set các bool này về false (trường hợp hết
        // ComboResetTime mà không bấm K tiếp) thì Animator cũng được cập nhật
        // ngay, không cần chờ tới lần bấm phím kế tiếp.
        ApplyToAnimatorCombo();
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
            _selectedCombo = UnityEngine.Random.Range(0, ComboLengths.Count);
            _comboIndex = 0;
            _needReroll = false;
        }

        _isAttacking = true;
        _queuedAttack = false;
        _lastAttackTime = Time.time;

        SetActiveCombo(_selectedCombo, true);
        _ComboId = _comboIndex; // 0 = đòn 1, 1 = đòn 2, ...

        int maxCombo = ComboLengths[_selectedCombo];
        OnAttackHit?.Invoke(new ComboHitInfo
        {
            SelectedCombo = _selectedCombo,
            HitIndex = _comboIndex,
            MaxCombo = maxCombo,
            IsFinalHit = _comboIndex >= maxCombo - 1
        });
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
        if (animator == null)
        {
            return;
        }

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
            case 0: isCombo1 = value; break;
            case 1: isCombo2 = value; break;
            case 2: isCombo3 = value; break;
                //case 3: characterAnim.isCombo4 = value; break;
                //case 4: characterAnim.isCombo5 = value; break;
                //case 5: characterAnim.isCombo6 = value; break;
        }
    }

    public void ApplyToAnimatorCombo()
    {
        animator.SetInteger(AnimatorParameters.Combo_ID, _ComboId);
        animator.SetBool(AnimatorParameters.IS_Combo1, isCombo1);
        animator.SetBool(AnimatorParameters.IS_Combo2, isCombo2);
        animator.SetBool(AnimatorParameters.IS_Combo3, isCombo3);


    }
}