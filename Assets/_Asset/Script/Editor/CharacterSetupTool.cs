using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ------------------------------------------------------------------
// Dựng combo + moveset cho Vergil (pack Sword_sheath, Yamato) và Trish (pack tay không, bắn phép):
//   1. Sửa đường dẫn cũ trong 2 library (Assets/Dante -> _Animation_Dante, Sword_sheath_AnimSet -> _Animation_Vergil).
//   2. Đổi tên clip trong Humanoid/Inplace thành "<tên>_Inplace" + tắt loop cho clip đòn đánh.
//   3. Tạo/cập nhật bộ anim (VG_ / TR_) theo khuôn KT_ của Dante; state pack không có thì giữ clip Dante.
//   4. Tạo/cập nhật vũ khí trong WeaponLibrary (Yamato / TrishMagic) + chuỗi combo.
//   5. Build: Sinh prefix -> layer Locomotion -> phân tích lại CHỈ vũ khí của nhân vật này (WD_ của Dante giữ nguyên mốc).
//
// Cần mở scene có 1 nhân vật gắn CharacterCombo: WeaponComboBuilder lấy avatar + scale của nó khi phân tích clip.
// ------------------------------------------------------------------
public static class CharacterSetupTool
{
    private const string TemplatePrefix = "KT_";
    private const string MenuRoot = "Tools/Combat/Nhân vật/";

    private static readonly (string from, string to)[] MovedFolders =
    {
        ("Assets/Dante/", "Assets/_Animation_Dante/"),
        ("Assets/Sword_sheath_AnimSet/", "Assets/_Animation_Vergil/"),
    };

    private sealed class Profile
    {
        public string Name;
        public string Prefix;
        public string Folder;
        public string WeaponKey;
        public string WeaponName;
        public WeaponType WeaponType;
        public string WeaponPrefab;     // null = tay không
        public string IdleClip;
        public float ComboResetTime;
        public Dictionary<LocoState, string> StateClips;
        public string[] GaitClips;
        public string WalkPrefix;
        public string RunPrefix;
        public List<WeaponChainDefinition> Chains;
        // mode bay: state bay -> clip (khuôn Dante không có nên thêm dòng mới), FlyPrefix + hướng = clip bay 8 hướng
        public Dictionary<LocoState, string> FlyClips;
        public string FlyPrefix;
        // gắn vào mọi tên clip locomotion khi map (vd "_Inplace" -> dùng bản Inplace, nhân vật bật CharController.InPlaceLocomotion)
        public string ClipSuffix = "";

        public string InplaceFolder => Folder + "/Inplace";
    }

    #region Hồ sơ nhân vật

    private static Profile Vergil => new Profile
    {
        Name = "Vergil",
        Prefix = "VG_",
        Folder = "Assets/_Animation_Vergil/Animation/Humanoid",
        WeaponKey = "Yamato",
        WeaponName = "Yamato",
        WeaponType = WeaponType.Yamato,
        WeaponPrefab = "Assets/_Asset/3D/Yamato.Clean/Split/Yamato_Sword.prefab",
        IdleClip = "idle",
        ComboResetTime = 0.6f,
        StateClips = new Dictionary<LocoState, string>
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
            { LocoState.RunDodgeLeft, "roll_left" },
            { LocoState.RunDodgeRight, "roll_right" },
        },
        GaitClips = new[] { "move_walk_front", "move_run_front", "move_run02", "sprint" },
        WalkPrefix = "move_walk_",
        RunPrefix = "move_run_",
        Chains = new List<WeaponChainDefinition>
        {
            Chain("Attack", AttackContext.Ground, "attack01", "attack02", "attack03"),
            Chain("Combo01", AttackContext.Ground, "combo01_1", "combo01_2", "combo01_3"),
            Chain("Combo02", AttackContext.Ground, "combo02_1", "combo02_2", "combo02_3", "combo02_4", "combo02_5"),
            Chain("Combo03", AttackContext.Ground, "combo03_1", "combo03_2", "combo03_3", "combo03_4"),
            Chain("Combo04", AttackContext.Ground, "combo04_1", "combo04_2", "combo04_3", "combo04_4"),
            Chain("Combo05", AttackContext.Ground, "combo05_1", "combo05_2", "combo05_3", "combo05_4"),
            Chain("Combo06", AttackContext.Ground, "combo06_1", "combo06_2", "combo06_3", "combo06_4"),
            Chain("Combo07", AttackContext.Ground, "combo07_1", "combo07_2", "combo07_3", "combo07_4"),
            Chain("Combo08", AttackContext.Ground, "combo08_1", "combo08_2", "combo08_3"),
            // Rapid Slash: đòn mở đầu lướt tới của combo dài nhất
            Chain("Rapid Slash", AttackContext.Dash, "combo02_1"),
            Chain("AirAttack", AttackContext.Air, "attack_jumpATK"),
            // Judgement Cut kiểu rút kiếm khỏi vỏ rồi tra vào: chuỗi chốt hạ của combo08 làm skill
            Chain("Judgement Cut", AttackContext.Skill, "combo08_3"),
            Chain("Sheath Flourish", AttackContext.Skill, "wp_unequip", "wp_equip"),
        },
    };

    private static Profile Trish => new Profile
    {
        Name = "Trish",
        Prefix = "TR_",
        Folder = "Assets/_Animation_Trish/Animations/Humanoid",
        WeaponKey = "TrishMagic",
        WeaponName = "Trish (Phép)",
        WeaponType = WeaponType.Magic,
        WeaponPrefab = null,
        IdleClip = "idle_Ground",
        ComboResetTime = 0.7f,
        StateClips = new Dictionary<LocoState, string>
        {
            { LocoState.Idle, "idle_Ground" },
            // nhảy = bay lên lơ lửng, rơi = tư thế bay, tiếp đất = hạ xuống
            { LocoState.JumpStart, "idle_Ground_to_Flying(Dynamic)" },
            { LocoState.JumpLoop, "idle_Flying" },
            { LocoState.JumpEnd, "idle_Flying_to_Ground(Dynamic)" },
            { LocoState.AirJumpStart, "idle_Ground_to_Flying" },
            { LocoState.AirJumpLoop, "idle_Flying" },
            { LocoState.AirJumpEnd, "idle_Flying_to_Ground(Dynamic)" },
        },
        // walk_front2 chậm hơn walk_front (0.54 vs 0.89 m/s) nên jog dùng luôn clip chạy
        GaitClips = new[] { "move_Ground_walk_front", "move_Ground_run_front", "move_Ground_run_front", "move_Ground_run_front" },
        WalkPrefix = "move_Ground_walk_",
        RunPrefix = "move_Ground_run_",
        FlyClips = new Dictionary<LocoState, string>
        {
            { LocoState.FlyStart, "idle_Ground_to_Flying" },
            { LocoState.FlyIdle, "idle_Flying" },
            { LocoState.FlyEnd, "idle_Flying_to_Ground" },
        },
        FlyPrefix = "move_Flying_",
        // mọi anim của Trish dùng bản Inplace: vận tốc do code (Trish.prefab bật InPlaceLocomotion),
        // độ cao bay nằm sẵn trong pose
        ClipSuffix = "_Inplace",
        Chains = new List<WeaponChainDefinition>
        {
            Chain("Spell A", AttackContext.Ground, "attack_GroundAtk01", "attack_GroundAtk02", "attack_GroundAtk03"),
            Chain("Spell B", AttackContext.Ground, "attack_GroundAtk04", "attack_GroundAtk05", "attack_GroundAtk06"),
            Chain("Spell Rush", AttackContext.Ground, "attack_GroundAtk01_short", "attack_GroundAtk03_short", "attack_GroundAtk05", "attack_GroundAtk06"),
            Chain("Dash Spell", AttackContext.Dash, "attack_GroundAtk01_short"),
            Chain("Air Spell", AttackContext.Air, "attack_FlyingAtk01", "attack_FlyingAtk02", "attack_FlyingAtk03", "attack_FlyingAtk04", "attack_FlyingAtk05", "attack_FlyingAtk06"),
            Chain("Air Spell Short", AttackContext.Air, "attack_FlyingAtk01_short", "attack_FlyingAtk03_short"),
            Chain("Launcher Spell", AttackContext.Launcher, "attack_GroundAtk03_short"),
            Chain("Buff", AttackContext.Skill, "buff_Ground"),
        },
    };

    private static WeaponChainDefinition Chain(string name, AttackContext context, params string[] clips)
    {
        var chain = new WeaponChainDefinition { Name = name, Context = context };
        foreach (string clip in clips)
        {
            chain.Steps.Add(new WeaponStepDefinition { Clip = clip + "_Inplace", Root = clip });
        }
        return chain;
    }

    #endregion

    #region Menu

    [MenuItem(MenuRoot + "Vergil - Setup + Build")]
    private static void VergilMenu() => Run(Vergil);

    [MenuItem(MenuRoot + "Trish - Setup + Build")]
    private static void TrishMenu() => Run(Trish);

    [MenuItem(MenuRoot + "Vergil + Trish - Setup + Build")]
    private static void BothMenu() => Run(Vergil, Trish);

    [MenuItem(MenuRoot + "Kiểm tra kết quả")]
    private static void VerifyMenu()
    {
        Verify(Vergil);
        Verify(Trish);
    }

    #endregion

    private static void Run(params Profile[] profiles)
    {
        string names = string.Join(" + ", profiles.Select(p => p.Name));
        if (!EditorUtility.DisplayDialog($"Setup {names}?",
            "Sẽ làm:\n" +
            "• Sửa đường dẫn pack cũ trong AnimSetLibrary / WeaponLibrary\n" +
            "• Đổi tên clip Inplace thành *_Inplace, tắt loop clip đòn đánh\n" +
            "• Tạo/cập nhật bộ anim + vũ khí\n" +
            "• Sinh prefix, dựng lại layer Locomotion + WeaponAttack (WD_ của Dante giữ nguyên)\n\n" +
            "Cần: không ở Play mode, scene đang mở có nhân vật CharacterCombo. Nên commit trước.", "Chạy", "Huỷ")) return;

        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Đang Play mode", "Thoát Play mode rồi chạy lại.", "OK");
            return;
        }
        if (UnityEngine.Object.FindFirstObjectByType<CharacterCombo>() == null)
        {
            EditorUtility.DisplayDialog("Thiếu nhân vật trong scene",
                "WeaponComboBuilder cần 1 nhân vật có CharacterCombo trong scene đang mở (lấy avatar + scale). Mở scene có Player rồi chạy lại.", "OK");
            return;
        }

        if (!FixMovedFolders()) return;

        foreach (Profile profile in profiles)
        {
            if (!Prepare(profile)) return;
        }
        AssetDatabase.SaveAssets();

        AnimSetLibrary sets = LocomotionAnimatorBuilder.FindLibrary();
        LocoSetPrefixesGenerator.Generate(sets);
        LocomotionAnimatorBuilder.BuildFrom(sets);
        WeaponComboBuilder.BuildWeapons(WeaponComboBuilder.FindLibrary(), profiles.Select(p => p.WeaponKey).ToList());

        foreach (Profile profile in profiles) Verify(profile);
        Debug.Log($"[CharacterSetup] Xong {names}. Gán WD_ tương ứng vào CharacterCombo.Weapons của prefab nhân vật" +
                  (profiles.Any(p => p.WeaponType == WeaponType.Magic) ? ", Trish cần thêm component SpellCaster." : "."));
    }

    #region Chuẩn bị

    private static bool FixMovedFolders()
    {
        AnimSetLibrary sets = LocomotionAnimatorBuilder.FindLibrary();
        if (sets == null)
        {
            Debug.LogError("[CharacterSetup] Chưa có AnimSetLibrary. Build bộ Dante trước (Tools/Combat/Timing Window).");
            return false;
        }

        int fixedCount = 0;
        Undo.RecordObject(sets, "Sửa đường dẫn bộ anim");
        foreach (AnimSetDefinition set in sets.Sets)
        {
            fixedCount += Remap(ref set.MovementFolder);
            for (int i = 0; i < set.ExtraFolders.Count; i++)
            {
                string folder = set.ExtraFolders[i];
                fixedCount += Remap(ref folder);
                set.ExtraFolders[i] = folder;
            }
        }
        EditorUtility.SetDirty(sets);

        WeaponLibrary weapons = WeaponComboBuilder.FindLibrary();
        if (weapons == null)
        {
            // Library mới phải có sẵn Rebellion + Sparda, không thì build WeaponAttack sẽ xoá state của Dante
            weapons = WeaponComboBuilder.CreateLibraryFromCode();
            Debug.Log("[CharacterSetup] Chưa có WeaponLibrary -> tạo từ bảng Dante (Rebellion, Sparda).");
        }
        Undo.RecordObject(weapons, "Sửa đường dẫn vũ khí");
        foreach (WeaponDefinition weapon in weapons.Weapons) fixedCount += Remap(ref weapon.AnimFolder);
        EditorUtility.SetDirty(weapons);

        if (fixedCount > 0) Debug.Log($"[CharacterSetup] Đã sửa {fixedCount} đường dẫn pack cũ.");
        return true;
    }

    private static int Remap(ref string folder)
    {
        if (string.IsNullOrEmpty(folder)) return 0;
        foreach ((string from, string to) in MovedFolders)
        {
            if (!folder.StartsWith(from, StringComparison.Ordinal)) continue;
            folder = to + folder.Substring(from.Length);
            return 1;
        }
        return 0;
    }

    private static bool Prepare(Profile profile)
    {
        if (!AssetDatabase.IsValidFolder(profile.Folder))
        {
            Debug.LogError($"[CharacterSetup] {profile.Name}: không thấy {profile.Folder}");
            return false;
        }

        int renamed = PrepareInplaceClips(profile);
        if (renamed > 0) Debug.Log($"[CharacterSetup] {profile.Name}: chuẩn hoá {renamed} FBX Inplace.");
        AnimSetCloneTool.ClearCache();

        if (!EnsureAnimSet(profile, out int animSetIndex)) return false;
        return EnsureWeapon(profile, animSetIndex);
    }

    // Clip Inplace trùng tên bản root motion -> builder không tách được Clip/Root.
    // Clip đòn đánh phải không loop: CharacterCombo tự đếm thời gian, clip loop sẽ quay về frame đầu lúc fade ra.
    private static int PrepareInplaceClips(Profile profile)
    {
        if (!AssetDatabase.IsValidFolder(profile.InplaceFolder)) return 0;

        var attackClips = new HashSet<string>(profile.Chains.SelectMany(c => c.Steps).Select(s => s.Clip), StringComparer.Ordinal);
        int changedFiles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { profile.InplaceFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.name.EndsWith("_Inplace", StringComparison.Ordinal))
                {
                    clip.name += "_Inplace";
                    changed = true;
                }
                if (attackClips.Contains(clip.name) && clip.loopTime)
                {
                    clip.loopTime = false;
                    changed = true;
                }
            }
            if (!changed) continue;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            changedFiles++;
        }
        return changedFiles;
    }

    private static bool EnsureAnimSet(Profile profile, out int index)
    {
        index = -1;
        AnimatorController controller = AnimSetCloneTool.FindController();
        if (controller == null)
        {
            Debug.LogError("[CharacterSetup] Không thấy Animator Controller: " + LocomotionAnimatorBuilder.ControllerPath);
            return false;
        }

        AnimSetDefinition template = AnimSetCloneTool.ReadFromController(controller, TemplatePrefix, "Khuôn Dante");
        if (template.OneShots.Count == 0 && template.Gaits.Count == 0 && template.Strafe.Count == 0)
        {
            Debug.LogError($"[CharacterSetup] Layer Locomotion không có state '{TemplatePrefix}*' để làm khuôn. Build lại bộ Dante trước.");
            return false;
        }

        AnimSetLibrary library = LocomotionAnimatorBuilder.FindLibrary();
        AnimSetDefinition set = AnimSetCloneTool.CloneSet(template, profile.Prefix, profile.Name, profile.Folder);
        // state pack không có (dodge của Trish, crouch, turn...) lấy clip Dante
        set.ExtraFolders.Clear();
        foreach (string folder in AnimSetCloneTool.TemplateFolders(template))
        {
            if (folder != profile.Folder && !set.ExtraFolders.Contains(folder)) set.ExtraFolders.Add(folder);
        }

        int mapped = Remap(profile, set);
        index = AnimSetCloneTool.Write(library, set, AnimSetCloneTool.IndexOfPrefix(library, profile.Prefix));
        Debug.Log($"[CharacterSetup] Bộ {profile.Prefix} (index {index}): map {mapped} clip của {profile.Name}, còn lại dùng clip Dante.");
        return index >= 0;
    }

    private static int Remap(Profile profile, AnimSetDefinition set)
    {
        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(profile.Folder);
        int mapped = 0;
        var missing = new List<string>();

        bool Use(string wanted, Action<string> assign)
        {
            if (string.IsNullOrEmpty(wanted)) return false;
            if (!string.IsNullOrEmpty(profile.ClipSuffix) && !wanted.EndsWith(profile.ClipSuffix, StringComparison.Ordinal)) wanted += profile.ClipSuffix;
            if (!clips.Contains(wanted)) { missing.Add(wanted); return false; }
            assign(wanted);
            mapped++;
            return true;
        }

        foreach (LocoClipDefinition row in set.OneShots)
        {
            if (profile.StateClips.TryGetValue(row.State, out string wanted)) Use(wanted, clip => row.Clip = clip);
        }
        for (int i = 0; i < set.Gaits.Count && i < profile.GaitClips.Length; i++)
        {
            GaitClipDefinition gait = set.Gaits[i];
            Use(profile.GaitClips[i], clip => gait.Clip = clip);
        }
        foreach (DirectionalClipDefinition row in set.Strafe)
        {
            bool walk = Mathf.Sqrt(row.X * row.X + row.Y * row.Y) < 0.75f;
            Use(DirectionClip(walk ? profile.WalkPrefix : profile.RunPrefix, row.X, row.Y), clip => row.Clip = clip);
        }

        set.OneShots.RemoveAll(row => row.State >= LocoState.FlyStart);
        set.Fly.Clear();
        if (profile.FlyClips != null)
        {
            foreach (var entry in profile.FlyClips)
            {
                // không đánh dấu Air: clip Inplace giữ nguyên độ cao gốc trong pose (không bám bàn chân)
                var row = new LocoClipDefinition { State = entry.Key, Loop = entry.Key == LocoState.FlyIdle };
                if (Use(entry.Value, clip => row.Clip = clip)) set.OneShots.Add(row);
            }
            foreach (Vector2 dir in FlyDirections)
            {
                var row = new DirectionalClipDefinition { X = dir.x, Y = dir.y };
                if (Use(DirectionClip(profile.FlyPrefix, dir.x, dir.y), clip => row.Clip = clip)) set.Fly.Add(row);
            }
        }

        if (missing.Count > 0)
            Debug.LogWarning($"[CharacterSetup] {profile.Name}: không thấy clip {string.Join(", ", missing.Distinct())} — state đó giữ clip Dante.");
        return mapped;
    }

    private static readonly Vector2[] FlyDirections =
    {
        new Vector2(0f, 1f), new Vector2(0.7071f, 0.7071f), new Vector2(1f, 0f), new Vector2(0.7071f, -0.7071f),
        new Vector2(0f, -1f), new Vector2(-0.7071f, -0.7071f), new Vector2(-1f, 0f), new Vector2(-0.7071f, 0.7071f),
    };

    private static string DirectionClip(string prefix, float x, float y)
    {
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

    private static bool EnsureWeapon(Profile profile, int animSetIndex)
    {
        GameObject model = null;
        if (profile.WeaponPrefab != null)
        {
            model = AssetDatabase.LoadAssetAtPath<GameObject>(profile.WeaponPrefab);
            if (model == null)
            {
                Debug.LogError($"[CharacterSetup] {profile.Name}: không thấy model vũ khí {profile.WeaponPrefab}");
                return false;
            }
        }

        WeaponLibrary library = WeaponComboBuilder.FindLibrary();
        WeaponDefinition weapon = library.Weapons.FirstOrDefault(w => string.Equals(w.Key, profile.WeaponKey, StringComparison.OrdinalIgnoreCase));
        Undo.RecordObject(library, "Cấu hình " + profile.WeaponKey);
        if (weapon == null)
        {
            weapon = new WeaponDefinition();
            library.Weapons.Add(weapon);
        }

        weapon.Type = profile.WeaponType;
        weapon.Key = profile.WeaponKey;
        weapon.DisplayName = profile.WeaponName;
        weapon.AnimFolder = profile.Folder;
        weapon.Prefab = model;
        weapon.Model = model;
        weapon.IdleClip = profile.IdleClip;
        weapon.ComboResetTime = profile.ComboResetTime;
        weapon.AnimSetId = animSetIndex;
        weapon.Chains = profile.Chains;

        EditorUtility.SetDirty(library);
        Debug.Log($"[CharacterSetup] {profile.WeaponKey}: AnimSetId {animSetIndex}, {weapon.Chains.Count} chuỗi, {weapon.Chains.Sum(c => c.Steps.Count)} đòn.");
        return true;
    }

    #endregion

    private static void Verify(Profile profile)
    {
        AnimSetLibrary sets = LocomotionAnimatorBuilder.FindLibrary();
        int index = sets != null ? AnimSetCloneTool.IndexOfPrefix(sets, profile.Prefix) : -1;
        bool inPrefixes = Array.IndexOf(LocoSetPrefixes.Values, profile.Prefix) >= 0;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
        AnimatorStateMachine machine = AnimSetCloneTool.GetLocomotionStateMachine(controller);
        int locoStates = machine == null ? 0 : machine.states.Count(s => s.state != null && s.state.name.StartsWith(profile.Prefix, StringComparison.Ordinal));

        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/_Asset/Data/Weapons/WD_{profile.WeaponKey}.asset");
        string weapon = data == null
            ? "chưa có WD_" + profile.WeaponKey
            : $"WD_{profile.WeaponKey}: AnimSetId {data.AnimSetId}, {data.Chains.Count} chuỗi, {data.Chains.Sum(c => c.Steps.Count)} đòn";

        bool ok = index >= 0 && locoStates > 0 && data != null && data.AnimSetId == index && data.Chains.Count > 0;
        string message = $"[CharacterSetup] {profile.Name}: bộ {profile.Prefix} index {index}, {locoStates} state Locomotion, {weapon}" +
                         (inPrefixes ? "" : $" (prefix {profile.Prefix} chưa có trong LocoSetPrefixes — chờ Unity compile xong rồi kiểm tra lại)");
        if (ok) Debug.Log(message);
        else Debug.LogError(message);
    }
}
