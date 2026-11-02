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

    public Vector3 LockPosition => LockPoint != null ? LockPoint.position : transform.position + Vector3.up * LockOnHeight;

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }
}
