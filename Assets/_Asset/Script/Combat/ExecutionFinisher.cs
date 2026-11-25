using UnityEngine;

/// <summary>
/// Kết liễu: bấm đánh khi gần 1 địch đang bị phá thăng bằng (HitReaction.IsBroken) -> ra đòn Context
/// kèm chuỗi shot camera + slow motion; nhát trúng kế tiếp là nhát kết liễu. CharController gọi TryExecute.
/// </summary>
[RequireComponent(typeof(CharController))]
public class ExecutionFinisher : MonoBehaviour
{
    public ComboCameraFeedback cameraFeedback;
    public float Range = 3f;
    [Tooltip("Chuỗi đòn dùng làm đòn kết liễu")]
    public AttackContext Context = AttackContext.Skill;
    public bool LogExecutions = true;

    private CharController _character;

    private void Start()
    {
        _character = GetComponent<CharController>();
        if (cameraFeedback == null) cameraFeedback = GetComponentInChildren<ComboCameraFeedback>();
        if (cameraFeedback == null) Debug.LogWarning("[ExecutionFinisher] thiếu ComboCameraFeedback, kết liễu sẽ không có góc máy", this);
    }

    public bool TryExecute()
    {
        if (_character == null || _character.characterCombo == null) return false;
        if (!_character.IsGrounded || _character.IsFlying || _character.IsAttacking) return false;
        HitReaction target = FindBrokenTarget();
        if (target == null) return false;

        target.BeginExecution();
        _character.characterCombo.OnAttackInput(Context);
        if (cameraFeedback != null) cameraFeedback.PlayExecution(transform, target.transform);
        if (LogExecutions) Debug.Log($"[ExecutionFinisher] kết liễu {target.name}", this);
        return true;
    }

    private HitReaction FindBrokenTarget()
    {
        HitReaction best = null;
        float bestDistance = Range;
        foreach (LockOnTarget lockTarget in LockOnTarget.All)
        {
            HitReaction reaction = lockTarget.GetComponentInParent<HitReaction>();
            if (reaction == null || !reaction.IsBroken) continue;
            float distance = Vector3.ProjectOnPlane(reaction.transform.position - transform.position, Vector3.up).magnitude;
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = reaction;
        }
        return best;
    }
}
