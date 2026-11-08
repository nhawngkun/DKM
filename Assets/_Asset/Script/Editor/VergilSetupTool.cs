using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ------------------------------------------------------------------
// Dựng trọn bộ cho Vergil từ pack Sword_sheath:
//   1. Đổi tên clip trong Animation/Humanoid/Inplace thành "<tên>_Inplace" để builder phân biệt
//      bản Inplace (phát trên Animator) và bản có root motion.
//   2. Tạo/cập nhật bộ anim "VG_" trong AnimSetLibrary: lấy khuôn state/cờ/Gait/8 hướng của bộ Dante (KT_)
//      rồi thay bằng clip Sword_sheath; state nào pack không có thì giữ clip Dante (thư mục dự phòng).
//   3. Tạo/cập nhật WeaponLibrary + vũ khí "Yamato": 3 đòn cơ bản + 1 air attack + 8 chuỗi combo.
//   4. Build: Sinh prefix -> dựng layer Locomotion -> dựng layer WeaponAttack (Build Weapon Combos).
//
// Yêu cầu trước khi chạy bước 2: mở scene có 1 nhân vật gắn CharacterCombo (Dante/Player) —
// WeaponComboBuilder dùng Animator trong scene để lấy avatar + tỉ lệ scale khi phân tích clip.
// ------------------------------------------------------------------
public static class VergilSetupTool
{
    private const string SwordFolder = "Assets/Sword_sheath_AnimSet/Animation/Humanoid";
    private const string SwordInplaceFolder = "Assets/Sword_sheath_AnimSet/Animation/Humanoid/Inplace";
    private const string YamatoPrefab = "Assets/_Asset/3D/Yamato.Clean/Split/Yamato_Sword.prefab";
    private const string Prefix = "VG_";
    private const string WeaponKey = "Yamato";
    private const string TemplatePrefix = "KT_";

    private const string MenuPrepare = "Tools/Combat/Vergil/1. Chuẩn bị (Inplace + bộ anim VG_ + Yamato)";
    private const string MenuBuild = "Tools/Combat/Vergil/2. Build Locomotion + Weapon Combo";
    private const string MenuAll = "Tools/Combat/Vergil/Chạy tất cả";

    // State -> clip Sword_sheath. State không có trong bảng này giữ clip của Dante (thư mục dự phòng).
    private static readonly Dictionary<LocoState, string> StateClips = new Dictionary<LocoState, string>
    {
        { LocoState.Idle, "idle" },
        { LocoState.JumpStart, "jump_start" },
        { LocoState.JumpLoop, "jump_falling_loop" },
        { LocoState.JumpEnd, "jump_landing" },
        { LocoState.AirJumpStart, "jump_start" },
        { LocoState.AirJumpLoop, "jump_falling_loop" },
        { LocoState.AirJumpEnd, "jump_landing" },
        { LocoState.DodgeFront, "avoid_front" },
        { LocoState.DodgeBack, "avoid_back" },
        { LocoState.DodgeLeft, "avoid_left" },
        { LocoState.DodgeRight, "avoid_right" },
        { LocoState.RunDodgeLeft, "avoid_left" },
        { LocoState.RunDodgeRight, "avoid_right" },
    };

    private static readonly string[] GaitClips = { "move_walk_front", "move_run_front", "move_run02", "sprint" };

    #region Menu

    [MenuItem(MenuPrepare)]
    private static void PrepareMenu()
    {
        if (!EditorUtility.DisplayDialog("Chuẩn bị cho Vergil?",
            "Sẽ làm:\n" +
            "• Đổi tên clip trong Humanoid/Inplace thành *_Inplace\n" +
            "• Tạo/cập nhật bộ VG_ trong AnimSetLibrary (khuôn state của KT_)\n" +
            "• Tạo/cập nhật WeaponLibrary + vũ khí Yamato với 10 chuỗi\n\n" +
            "Không build Animator. Bấm '2. Build' sau khi xong.", "Làm", "Huỷ")) return;

        Prepare();
    }

    [MenuItem(MenuBuild)]
    private static void BuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Locomotion + Weapon Combo?",
            "Sẽ: Sinh prefix -> xoá & dựng lại layer Locomotion -> xoá & dựng lại layer WeaponAttack cho toàn bộ vũ khí trong WeaponLibrary.\n" +
            "Không được ở Play mode. Build có thể mất vài phút (reimport FBX).", "Build", "Huỷ")) return;

        Build();
    }

    [MenuItem(MenuAll)]
    private static void AllMenu()
    {
        if (!EditorUtility.DisplayDialog("Chạy tất cả cho Vergil?",
            "Chuẩn bị (Inplace, bộ VG_, Yamato) rồi build cả 2 layer.\n" +
            "Cần mở scene có nhân vật CharacterCombo. Không được ở Play mode.", "Chạy", "Huỷ")) return;

        if (!Prepare()) return;
        Build();
    }

    [MenuItem("Tools/Combat/Vergil/Kiểm tra kết quả")]
    private static void VerifyMenu() => Verify();

    #endregion

    #region Bước 1: chuẩn bị

    private static bool Prepare()
    {
        if (!AssetDatabase.IsValidFolder(SwordFolder))
        {
            Debug.LogError("[VergilSetup] Không thấy " + SwordFolder);
            return false;
        }

        int renamed = RenameInplaceClips();
        if (renamed > 0) Debug.Log($"[VergilSetup] Đã đổi tên clip Inplace cho {renamed} FBX.");

        if (!EnsureAnimSet(out int animSetIndex)) return false;
        if (!EnsureWeapon(animSetIndex)) return false;

        AssetDatabase.SaveAssets();
        Debug.Log($"[VergilSetup] Xong bước 1. Bộ VG_ = index {animSetIndex}. " +
                  "Chạy '2. Build Locomotion + Weapon Combo'.");
        return true;
    }

    // Clip Inplace đang trùng tên với bản root motion -> builder không tách được Clip/Root
    private static int RenameInplaceClips()
    {
        if (!AssetDatabase.IsValidFolder(SwordInplaceFolder)) return 0;

        int renamed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { SwordInplaceFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip.name.EndsWith("_Inplace", StringComparison.Ordinal)) continue;
                clip.name += "_Inplace";
                changed = true;
            }
            if (!changed) continue;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            renamed++;
        }
        return renamed;
    }

    private static bool EnsureAnimSet(out int index)
    {
        index = -1;

        AnimatorController controller = AnimSetCloneTool.FindController();
        if (controller == null)
        {
            Debug.LogError("[VergilSetup] Không thấy Animator Controller: " + LocomotionAnimatorBuilder.ControllerPath);
            return false;
        }

        AnimSetDefinition template = AnimSetCloneTool.ReadFromController(controller, TemplatePrefix, "Khuôn Dante");
        if (template.OneShots.Count == 0 && template.Gaits.Count == 0 && template.Strafe.Count == 0)
        {
            Debug.LogError($"[VergilSetup] Layer Locomotion không có state '{TemplatePrefix}*' để làm khuôn. " +
                           "Build lại bộ Dante (Tools/Combat/Build Locomotion Animator) trước.");
            return false;
        }

        AnimSetLibrary library = LocomotionAnimatorBuilder.FindLibrary();
        if (library == null) library = AnimSetCloneTool.CreateEmptyLibrary();

        AnimSetDefinition set = AnimSetCloneTool.CloneSet(template, Prefix, "Vergil (Sword Sheath)", SwordFolder);
        set.ExtraFolders.Clear();
        if (!string.IsNullOrEmpty(template.MovementFolder) && template.MovementFolder != SwordFolder)
            set.ExtraFolders.Add(template.MovementFolder);

        int mapped = RemapToSwordSheath(set);
        index = AnimSetCloneTool.Write(library, set, AnimSetCloneTool.IndexOfPrefix(library, Prefix));

        Debug.Log($"[VergilSetup] Bộ {Prefix} (index {index}): {set.OneShots.Count} state, {set.Gaits.Count} gait, " +
                  $"{set.Strafe.Count} hướng strafe; map mới {mapped} clip, còn lại dùng clip template trong {template.MovementFolder}.");
        return index >= 0;
    }

    // Thay clip Sword_sheath vào các state mà pack có anim tương ứng
    private static int RemapToSwordSheath(AnimSetDefinition set)
    {
        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(SwordFolder);
        if (!clips.IsValid)
        {
            Debug.LogError("[VergilSetup] Thư mục clip không có AnimationClip nào: " + SwordFolder);
            return 0;
        }

        int mapped = 0;
        var missing = new List<string>();

        void Use(LocoClipDefinition row, string wanted)
        {
            if (row == null || string.IsNullOrEmpty(wanted)) return;
            if (!clips.Contains(wanted))
            {
                missing.Add(wanted);
                return;
            }
            row.Clip = wanted;
            mapped++;
        }

        foreach (LocoClipDefinition row in set.OneShots)
        {
            if (StateClips.TryGetValue(row.State, out string wanted)) Use(row, wanted);
        }

        for (int i = 0; i < set.Gaits.Count && i < GaitClips.Length; i++)
        {
            string wanted = GaitClips[i];
            if (!clips.Contains(wanted)) { missing.Add(wanted); continue; }
            set.Gaits[i].Clip = wanted;
            mapped++;
        }

        foreach (DirectionalClipDefinition row in set.Strafe)
        {
            float radius = Mathf.Sqrt(row.X * row.X + row.Y * row.Y);
            string wanted = DirectionClip(row.X, row.Y, radius < 0.75f);
            if (wanted == null) continue;
            if (!clips.Contains(wanted)) { missing.Add(wanted); continue; }
            row.Clip = wanted;
            mapped++;
        }

        if (missing.Count > 0)
            Debug.LogWarning("[VergilSetup] Không thấy clip: " + string.Join(", ", missing.Distinct()) +
                             " — các state đó giữ clip template.");

        return mapped;
    }

    private static string DirectionClip(float x, float y, bool walk)
    {
        string prefix = walk ? "move_walk_" : "move_run_";
        bool front = y > 0.05f, back = y < -0.05f, right = x > 0.05f, left = x < -0.05f;

        if (front && left) return prefix + "frontL45";
        if (front && right) return prefix + "frontR45";
        if (back && left) return prefix + "backL45";
        if (back && right) return prefix + "backR45";
        if (front) return prefix + "front";
        if (back) return prefix + "back";
        if (left) return prefix + "left";
        if (right) return prefix + "right";
        return null;
    }

    private static bool EnsureWeapon(int animSetIndex)
    {
        GameObject yamato = AssetDatabase.LoadAssetAtPath<GameObject>(YamatoPrefab);
        if (yamato == null)
        {
            Debug.LogError("[VergilSetup] Không thấy model Yamato: " + YamatoPrefab);
            return false;
        }

        WeaponLibrary library = WeaponComboBuilder.FindLibrary();
        if (library == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WeaponComboBuilder.LibraryPath));
            library = ScriptableObject.CreateInstance<WeaponLibrary>();
            AssetDatabase.CreateAsset(library, WeaponComboBuilder.LibraryPath);
        }

        WeaponDefinition weapon = library.Weapons.FirstOrDefault(w => string.Equals(w.Key, WeaponKey, StringComparison.OrdinalIgnoreCase));
        if (weapon == null)
        {
            weapon = new WeaponDefinition();
            library.Weapons.Add(weapon);
        }

        Undo.RecordObject(library, "Cấu hình Yamato");

        weapon.Type = WeaponType.Yamato;
        weapon.Key = WeaponKey;
        weapon.DisplayName = "Yamato";
        weapon.AnimFolder = SwordFolder;
        weapon.Prefab = yamato;          // dùng để đo mũi kiếm
        weapon.Model = yamato;           // model cầm trong game
        weapon.IdleClip = "idle";
        weapon.ComboResetTime = 0.6f;
        weapon.AnimSetId = animSetIndex;

        BuildChains(weapon);

        EditorUtility.SetDirty(library);
        Debug.Log($"[VergilSetup] Yamato: AnimSetId {animSetIndex}, {weapon.Chains.Count} chuỗi, " +
                  $"{weapon.Chains.Sum(c => c.Steps.Count)} đòn. WeaponLibrary: {WeaponComboBuilder.LibraryPath}");
        return true;
    }

    private static void BuildChains(WeaponDefinition weapon)
    {
        weapon.Chains.Clear();

        AddChain(weapon, "Attack", AttackContext.Ground, "attack01", "attack02", "attack03");
        AddChain(weapon, "AirAttack", AttackContext.Air, "attack_jumpATK");

        AddChain(weapon, "Combo01", AttackContext.Ground, "combo01_1", "combo01_2", "combo01_3");
        AddChain(weapon, "Combo02", AttackContext.Ground, "combo02_1", "combo02_2", "combo02_3", "combo02_4", "combo02_5");
        AddChain(weapon, "Combo03", AttackContext.Ground, "combo03_1", "combo03_2", "combo03_3", "combo03_4");
        AddChain(weapon, "Combo04", AttackContext.Ground, "combo04_1", "combo04_2", "combo04_3", "combo04_4");
        AddChain(weapon, "Combo05", AttackContext.Ground, "combo05_1", "combo05_2", "combo05_3", "combo05_4");
        AddChain(weapon, "Combo06", AttackContext.Ground, "combo06_1", "combo06_2", "combo06_3", "combo06_4");
        AddChain(weapon, "Combo07", AttackContext.Ground, "combo07_1", "combo07_2", "combo07_3", "combo07_4");
        AddChain(weapon, "Combo08", AttackContext.Ground, "combo08_1", "combo08_2", "combo08_3");
    }

    private static void AddChain(WeaponDefinition weapon, string name, AttackContext context, params string[] clips)
    {
        var chain = new WeaponChainDefinition { Name = name, Context = context };
        foreach (string clip in clips)
        {
            chain.Steps.Add(new WeaponStepDefinition { Clip = clip + "_Inplace", Root = clip });
        }
        weapon.Chains.Add(chain);
    }

    #endregion

    #region Bước 2: build

    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Đang Play mode", "Thoát Play mode rồi build lại.", "OK");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<CharacterCombo>() == null)
        {
            EditorUtility.DisplayDialog("Thiếu nhân vật trong scene",
                "WeaponComboBuilder cần 1 nhân vật có CharacterCombo trong scene đang mở (để lấy avatar + tỉ lệ scale).\n" +
                "Mở scene có Player (hoặc prefab Dante) rồi chạy lại.", "OK");
            return;
        }

        AnimSetLibrary sets = LocomotionAnimatorBuilder.FindLibrary();
        if (sets == null)
        {
            Debug.LogError("[VergilSetup] Chưa có AnimSetLibrary. Chạy bước 1 trước.");
            return;
        }

        WeaponLibrary weapons = WeaponComboBuilder.FindLibrary();
        if (weapons == null || weapons.Weapons.Count == 0)
        {
            Debug.LogError("[VergilSetup] Chưa có WeaponLibrary/Yamato. Chạy bước 1 trước.");
            return;
        }

        LocoSetPrefixesGenerator.Generate(sets);
        LocomotionAnimatorBuilder.BuildFrom(sets);
        WeaponComboBuilder.BuildAll();

        Verify();
        Debug.Log("[VergilSetup] Build xong. Đọc Console: phải không còn 'Không thấy clip'. " +
                  "Tiếp: gán WD_Yamato vào CharacterCombo của prefab Vergil rồi play test.");
    }

    private static void Verify()
    {
        AnimSetLibrary sets = LocomotionAnimatorBuilder.FindLibrary();
        WeaponLibrary weapons = WeaponComboBuilder.FindLibrary();
        if (sets == null) { Debug.LogWarning("[VergilSetup] Không thấy AnimSetLibrary."); return; }

        int index = AnimSetCloneTool.IndexOfPrefix(sets, Prefix);
        Debug.Log($"[VergilSetup] AnimSetLibrary: {sets.Sets.Count} bộ, {Prefix} = index {index}.");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
        AnimatorStateMachine machine = AnimSetCloneTool.GetLocomotionStateMachine(controller);
        if (machine != null)
        {
            int stateCount = machine.states.Count(child => child.state != null && child.state.name.StartsWith(Prefix, StringComparison.Ordinal));
            Debug.Log($"[VergilSetup] Layer Locomotion có {stateCount} state bắt đầu bằng {Prefix}.");
            if (stateCount == 0)
                Debug.LogError("[VergilSetup] Không có state nào cho " + Prefix + " — build locomotion thất bại, xem lỗi phía trên.");
        }

        if (weapons != null)
        {
            WeaponDefinition yamato = weapons.Weapons.FirstOrDefault(w => string.Equals(w.Key, WeaponKey, StringComparison.OrdinalIgnoreCase));
            if (yamato != null)
                Debug.Log($"[VergilSetup] Yamato trong WeaponLibrary: AnimSetId {yamato.AnimSetId}, {yamato.Chains.Count} chuỗi, " +
                          $"{yamato.Chains.Sum(c => c.Steps.Count)} đòn.");
        }

        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Asset/Data/Weapons/WD_" + WeaponKey + ".asset");
        if (data != null)
            Debug.Log($"[VergilSetup] WD_{WeaponKey}: AnimSetId {data.AnimSetId}, {data.Chains.Count} chuỗi, " +
                      $"{data.Chains.Sum(c => c.Steps.Count)} đòn có dữ liệu.");
        else
            Debug.LogError($"[VergilSetup] Chưa có WD_{WeaponKey}.asset — Build Weapon Combos thất bại.");
    }

    #endregion
}
