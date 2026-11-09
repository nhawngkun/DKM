using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------------
// Bộ anim nhân vật: mỗi nhân vật / mỗi bộ vũ khí là 1 AnimSetDefinition.
// LocomotionAnimatorBuilder đọc asset này thay vì bảng hard-code, nên thêm nhân vật
// mới chỉ cần thêm 1 bộ + trỏ thư mục anim, không sửa code.
// LocoState dùng đúng enum runtime nên state name sinh ra vẫn là "<Prefix><LocoState>".
// ------------------------------------------------------------------

[System.Serializable]
public class LocoClipDefinition
{
    public LocoState State;
    [Tooltip("Tên clip trong thư mục anim (đúng tên hiển thị trong FBX)")]
    public string Clip;
    public bool Loop;
    [Tooltip("Clip trên không: root bám theo bàn chân để pose không mang độ cao cú nhảy")]
    public bool Air;
    [Tooltip("Tốc độ phát của state (1 = bình thường)")]
    public float Speed = 1f;
    [Tooltip("Frame bắt đầu cắt clip. -1 = giữ nguyên")]
    public int FirstFrame = -1;
}

[System.Serializable]
public class GaitClipDefinition
{
    [Tooltip("Ngưỡng Gait trong blend tree 1D (1 = chậm nhất)")]
    public float Threshold = 1f;
    public string Clip;
}

[System.Serializable]
public class DirectionalClipDefinition
{
    [Tooltip("X: + = phải, - = trái")]
    public float X;
    [Tooltip("Y: + = tới, - = lui")]
    public float Y;
    public string Clip;
}

[System.Serializable]
public class AnimSetDefinition
{
    [Tooltip("Tiền tố state trong Animator, vd KT_ -> KT_Idle")]
    public string Prefix = "NV_";
    public string DisplayName = "Nhân vật mới";
    [Tooltip("Prefab nhân vật dùng bộ anim này (Character Studio dùng để preview + kiểm tra component)")]
    public GameObject Prefab;
    [Tooltip("Thư mục chứa clip movement (FBX)")]
    public string MovementFolder = "";
    [Tooltip("Thư mục clip dự phòng: state không tìm thấy clip trong thư mục chính sẽ lấy ở đây. " +
             "Dùng để giữ anim cũ của bộ template -> nhiều nhân vật dùng chung một bộ.")]
    public List<string> ExtraFolders = new List<string>();

    public List<LocoClipDefinition> OneShots = new List<LocoClipDefinition>();
    public List<GaitClipDefinition> Gaits = new List<GaitClipDefinition>();
    public List<DirectionalClipDefinition> Strafe = new List<DirectionalClipDefinition>();
    public List<DirectionalClipDefinition> Crouch = new List<DirectionalClipDefinition>();
    [Tooltip("Clip bay 8 hướng (state FlyMove). Để trống = nhân vật không bay")]
    public List<DirectionalClipDefinition> Fly = new List<DirectionalClipDefinition>();

    public LocoClipDefinition Idle
    {
        get
        {
            foreach (LocoClipDefinition row in OneShots)
            {
                if (row.State == LocoState.Idle) return row;
            }
            return null;
        }
    }
}

// AnimSetLibrary nằm ở AnimSetLibrary.cs: ScriptableObject phải ở file trùng tên class, không thì asset mất script

// ------------------------------------------------------------------
// Danh sách vũ khí: WeaponComboBuilder đọc asset này thay vì bảng hard-code.
// Thêm vũ khí mới = thêm 1 WeaponDefinition + trỏ thư mục anim, không sửa code.
// ------------------------------------------------------------------

[System.Serializable]
public class WeaponStepDefinition
{
    [Tooltip("Tên clip phát (bản Inplace / ZeroHeight)")]
    public string Clip;
    [Tooltip("Tên clip lấy root motion (bản có di chuyển). Để trống = chính clip")]
    public string Root;
    [Tooltip("Dùng root motion dọc (hất tung)")]
    public bool Vertical;
}

[System.Serializable]
public class WeaponChainDefinition
{
    public string Name;
    public AttackContext Context = AttackContext.Ground;
    public List<WeaponStepDefinition> Steps = new List<WeaponStepDefinition>();
}

[System.Serializable]
public class WeaponDefinition
{
    public WeaponType Type;
    [Tooltip("Tiền tố state + tên asset WD_<Key>")]
    public string Key = "NewWeapon";
    public string DisplayName = "Vũ khí mới";
    [Tooltip("Thư mục chứa clip đòn đánh (FBX)")]
    public string AnimFolder = "";
    [Tooltip("Prefab vũ khí gốc của pack anim — chỉ dùng để phân tích mũi kiếm")]
    public GameObject Prefab;
    [Tooltip("Model vũ khí cầm trong game. Trống = dùng Prefab")]
    public GameObject Model;
    public string IdleClip = "Idle";
    public float ComboResetTime = 0.5f;
    [Tooltip("Index bộ anim trong AnimSetLibrary mà vũ khí này dùng (0 = bộ đầu tiên)")]
    public int AnimSetId;
    public List<WeaponChainDefinition> Chains = new List<WeaponChainDefinition>();
}

// WeaponLibrary nằm ở WeaponLibrary.cs (cùng lý do)
