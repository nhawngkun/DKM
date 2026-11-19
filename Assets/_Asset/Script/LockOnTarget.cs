using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn lên kẻ địch / boss để player có thể lock-on (kiểu DMC: giữ nút lock-on).
/// Tự đăng ký vào danh sách tĩnh khi enable -> CharController không cần FindObjectsOfType hay Layer riêng.
/// </summary>
public class LockOnTarget : MonoBehaviour
{
    public static readonly List<LockOnTarget> All = new List<LockOnTarget>();

    [Tooltip("Điểm để camera/IK nhìn vào (ngực/đầu). Để trống = transform + LockOnHeight")]
    public Transform LockPoint;
    public float LockOnHeight = 1.2f;

    private Collider[] _colliders;

    public Vector3 LockPosition => LockPoint != null ? LockPoint.position : transform.position + Vector3.up * LockOnHeight;

    /// <summary>Độ cao đỉnh đầu (y thế giới) theo collider, để đòn trên không không bay cao quá mục tiêu.</summary>
    public float Top
    {
        get
        {
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>();
            float top = float.MinValue;
            foreach (Collider collider in _colliders)
            {
                if (collider != null && collider.enabled && !collider.isTrigger) top = Mathf.Max(top, collider.bounds.max.y);
            }
            return top > float.MinValue ? top : LockPosition.y;
        }
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }
}
