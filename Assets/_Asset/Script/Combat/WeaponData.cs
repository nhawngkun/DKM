using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Rebellion,
    Sparda,
}

/// <summary>
/// Ngữ cảnh ra đòn: quyết định chuỗi combo nào được chọn khi bấm nút.
/// </summary>
public enum AttackContext
{
    Ground,     // Đứng / đi trên đất -> chuỗi combo thường
    Dash,       // Đang chạy nhanh, vừa dash xong, hoặc lock-on + đẩy tới (kiểu Stinger)
    Air,        // Trên không -> air combo
    Launcher,   // Lock-on + kéo lùi + đánh -> hất tung (kiểu High Time)
    Skill,      // Nút kỹ năng
}

/// <summary>
/// 1 đòn trong chuỗi combo. Các mốc thời gian là normalized (0..1 theo độ dài clip)
/// và được WeaponComboBuilder tự phân tích từ chuyển động thật của mũi kiếm trong anim.
/// </summary>
[Serializable]
public class AttackStep
{
    public string Name;
    [Tooltip("Clip phát trên Animator (bản Inplace / ZeroHeight)")]
    public AnimationClip Clip;
    [Tooltip("Tên state trong layer tấn công của Animator (builder tự tạo)")]
    public string StateName;
    public float Speed = 1f;
    [Tooltip("Thời gian crossfade vào đòn này (giây)")]
    public float FadeIn = 0.12f;

    [Header("Timing (normalized, tự phân tích từ anim)")]
    [Tooltip("Các frame mũi kiếm đạt tốc độ cao nhất = lúc chém trúng")]
    public List<float> HitTimes = new List<float>();
    [Tooltip("Mốc nối đòn: đã bấm sẵn thì chuyển sang đòn kế ngay tại đây (hết vung, trước hồi thế)")]
    public float LinkTime = 0.6f;
    [Tooltip("Sau mốc này đẩy stick là hủy hồi chiêu để di chuyển")]
    public float MoveCancelTime = 0.8f;
    [Tooltip("Giây trong clip mà tư thế trùng frame đầu của đòn kế trong chuỗi (clip cắt ra từ 1 combo liền) -> nối tại đây là liền mạch. -1 = không có")]
    public float NextSeamTime = -1f;

    [Header("Root motion (lấy từ bản anim có di chuyển)")]
    public bool UsePlanarRootMotion = true;
    public bool UseVerticalRootMotion = false;
    public float RootMotionScale = 1f;
    [Tooltip("Độ dời gốc tính theo không gian nhân vật (đã chia humanScale), lấy mẫu đều theo RootSampleRate")]
    public Vector3[] RootSamples = new Vector3[0];
    [Tooltip("Quãng đường ngang tổng (m, theo humanScale = 1)")]
    public float RootTravel;

    [Tooltip("Tư thế tay/chân/đầu theo hông, PoseBoneCount điểm mỗi mẫu, lấy mẫu đều theo RootSampleRate -> khớp tư thế khi nối đòn")]
    [HideInInspector] public Vector3[] PoseSamples = new Vector3[0];

    [Header("Cách cầm vũ khí (local so với xương bàn tay phải của Dante)")]
    public Vector3[] GripPositions = new Vector3[0];
    public Quaternion[] GripRotations = new Quaternion[0];

    [Header("Cảm giác đòn")]
    [Range(0f, 1f)] public float Weight = 0.5f;   // 0 = đòn nhẹ, 1 = đòn nặng nhất của vũ khí
    public bool IsFinisher;
    [Tooltip("Normalized time mũi kiếm đập xuống đất (tự phân tích, chỉnh tay được). -1 = đòn này không đập đất")]
    public float SlamTime = -1f;
    [Tooltip("Các normalized time chân đá mạnh nhất (tự phân tích, chỉnh tay được). Rỗng = đòn này không đá")]
    public List<float> KickTimes = new List<float>();

    public float Length => Clip != null ? Clip.length : 1f;
}

/// <summary>
/// Thông số nhảy theo vũ khí, đo từ anim demo Jump_ALL / Double_Jump_ALL của gói Grruzam
/// (độ cao, thời gian lên, thời gian rơi của xương hông).
/// </summary>
[Serializable]
public class JumpProfile
{
    [Tooltip("Tắt = dùng thông số nhảy chung trên CharController")]
    public bool Enabled = false;

    [Header("Nhảy từ đất")]
    public float UpSpeed = 10f;
    [Tooltip("Trọng lực lúc bay lên (m/s²)")]
    public float RiseGravity = 37f;
    [Tooltip("Nhân trọng lực lúc rơi xuống")]
    public float FallGravityMultiplier = 1.5f;

    [Header("Double jump")]
    public float AirUpSpeed = 8f;
    public float AirRiseGravity = 22f;
    public float AirFallGravityMultiplier = 1.5f;

    public float MaxFallSpeed = 16f;
}

[Serializable]
public class ComboChain
{
    public string Name;
    public AttackContext Context = AttackContext.Ground;
    public List<AttackStep> Steps = new List<AttackStep>();
}

/// <summary>
/// Dữ liệu 1 loại vũ khí: model, cách cầm mặc định và toàn bộ chuỗi combo/skill.
/// Tạo/cập nhật bằng menu Tools/Combat/Build Weapon Combos.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Weapon Data", fileName = "WeaponData")]
public class WeaponData : ScriptableObject
{
    public const float RootSampleRate = 30f;
    public const int PoseBoneCount = 5; // tay trái, tay phải, chân trái, chân phải, đầu

    public WeaponType Type;
    public string DisplayName;
    public GameObject WeaponPrefab;

    [Header("Cách cầm khi không đánh (local so với bàn tay phải)")]
    public Vector3 IdleGripPosition;
    public Quaternion IdleGripRotation = Quaternion.identity;

    [Header("Anim di chuyển")]
    [Tooltip("Giá trị param WeaponID: chọn bộ state nhảy trong layer Jump")]
    public int AnimSetId;
    [Tooltip("Giá trị param IdleID: chọn clip idle trong blend tree Idle")]
    public float IdleId;
    public JumpProfile Jump = new JumpProfile();

    [Header("Combo")]
    [Tooltip("Hết thời gian này (giây, tính từ lúc đòn trước kết thúc) mà không bấm tiếp -> về đòn 1")]
    public float ComboResetTime = 0.6f;
    public List<ComboChain> Chains = new List<ComboChain>();

    public IEnumerable<ComboChain> ChainsFor(AttackContext context)
    {
        foreach (ComboChain chain in Chains)
        {
            if (chain.Context == context && chain.Steps.Count > 0) yield return chain;
        }
    }

    // Nội suy 1 mảng mẫu đều theo thời gian (giây trong clip)
    public static Vector3 SampleVector(Vector3[] samples, float time)
    {
        if (samples == null || samples.Length == 0) return Vector3.zero;
        float f = Mathf.Max(0f, time) * RootSampleRate;
        int i = Mathf.Min((int)f, samples.Length - 1);
        int j = Mathf.Min(i + 1, samples.Length - 1);
        return Vector3.Lerp(samples[i], samples[j], f - i);
    }

    public static Quaternion SampleRotation(Quaternion[] samples, float time)
    {
        if (samples == null || samples.Length == 0) return Quaternion.identity;
        float f = Mathf.Max(0f, time) * RootSampleRate;
        int i = Mathf.Min((int)f, samples.Length - 1);
        int j = Mathf.Min(i + 1, samples.Length - 1);
        return Quaternion.Slerp(samples[i], samples[j], f - i);
    }
}
