using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ------------------------------------------------------------------
// Lấy bộ anim ĐANG DÙNG của một nhân vật (mặc định Dante) làm template rồi thay clip của nhân vật mới:
//   1. Đọc thẳng layer "Locomotion" trong Animator Controller: mỗi state <Prefix><LocoState> lấy
//      đúng clip/blend tree đang gắn, kèm Speed, cờ Loop/FirstFrame lấy từ import settings của FBX.
//      -> không phụ thuộc AnimSetLibrary, không quét cả thư mục clip (chỉ anim đang dùng).
//   2. Nhân bản thành bộ mới: giữ state, Loop/Air, Speed, FirstFrame, threshold Gait, toạ độ 8 hướng.
//   3. Trỏ sang thư mục clip của nhân vật mới, map lại clip (gợi ý + tự chọn).
//   4. Kiểm tra: clip không có trong thư mục, cờ Loop/Air xung đột, state thiếu (runtime kẹt), trùng prefix.
// Ghi vào AnimSetLibrary xong thì "Sinh prefix" + "Build Locomotion" (builder dựng lại layer = overwrite controller).
//
// 2 chế độ ghi:
//   Clone     : bộ mới (prefix mới) -> nhân vật/vũ khí khác, AnimSetId mới.
//   Overwrite : giữ prefix + AnimSetId, chỉ đổi thư mục + clip (thay anim tại chỗ).
// ------------------------------------------------------------------
public static class AnimSetCloneTool
{
    public const string LibraryPath = "Assets/_Asset/Data/AnimSetLibrary.asset";

    // CharController/CharacterLocomotion chờ state này kết thúc (GetStateProgress), thiếu state là kẹt:
    // Turn (Start*), Stop (Stop*), Dodge (TickGround).
    public static readonly LocoState[] RequiredStates =
    {
        LocoState.Idle,
        LocoState.StartWalk, LocoState.StartJog, LocoState.StartRun,
        LocoState.StopWalk, LocoState.StopRun, LocoState.StopFast,
        LocoState.DodgeFront, LocoState.DodgeBack, LocoState.DodgeLeft, LocoState.DodgeRight,
    };

    // Thiếu thì không kẹt nhưng sai animation (rơi/tiếp đất/crouch).
    public static readonly LocoState[] SoftStates =
    {
        LocoState.JumpStart, LocoState.JumpLoop, LocoState.JumpEnd,
        LocoState.AirJumpStart, LocoState.AirJumpLoop, LocoState.AirJumpEnd,
        LocoState.CrouchEnter, LocoState.CrouchMove,
    };

    #region Đọc template từ Animator Controller

    private static readonly HashSet<LocoState> AirStates = new HashSet<LocoState>
    {
        LocoState.JumpStart, LocoState.JumpLoop, LocoState.JumpEnd,
        LocoState.AirJumpStart, LocoState.AirJumpLoop, LocoState.AirJumpEnd,
    };

    public static AnimatorController FindController()
    {
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
    }

    public static AnimatorStateMachine GetLocomotionStateMachine(AnimatorController controller)
    {
        if (controller == null) return null;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == LocomotionAnimatorBuilder.LayerName) return layer.stateMachine;
        }
        return controller.layers.Length > 0 ? controller.layers[0].stateMachine : null;
    }

    // Duyệt cả state machine con (builder tạo phẳng, nhưng người dùng có thể gom nhóm sau)
    private static IEnumerable<AnimatorState> EnumerateStates(AnimatorStateMachine machine)
    {
        if (machine == null) yield break;

        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null) yield return child.state;
        }
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
        {
            foreach (AnimatorState state in EnumerateStates(sub.stateMachine)) yield return state;
        }
    }

    // Các prefix có state trong layer, vd "KT_", "GS_" (chỉ tính state có motion thật)
    public static List<string> ListPrefixes(AnimatorStateMachine machine)
    {
        var prefixes = new List<string>();
        if (machine == null) return prefixes;

        string[] stateNames = Enum.GetNames(typeof(LocoState));
        foreach (AnimatorState state in EnumerateStates(machine))
        {
            if (state.motion == null) continue;
            string stateName = state.name;

            string best = null;
            foreach (string candidate in stateNames)
            {
                if (stateName.Length <= candidate.Length) continue;
                if (!stateName.EndsWith(candidate, StringComparison.Ordinal)) continue;
                if (best == null || candidate.Length > best.Length) best = candidate;
            }
            if (best == null) continue;

            string prefix = stateName.Substring(0, stateName.Length - best.Length);
            if (prefix.Length == 0 || prefixes.Contains(prefix)) continue;
            prefixes.Add(prefix);
        }

        prefixes.Sort(StringComparer.OrdinalIgnoreCase);
        return prefixes;
    }

    // Dựng AnimSetDefinition đúng bằng những gì Animator đang phát cho prefix này
    public static AnimSetDefinition ReadFromController(AnimatorController controller, string prefix, string displayName)
    {
        var set = new AnimSetDefinition
        {
            Prefix = NormalizePrefix(prefix),
            DisplayName = string.IsNullOrEmpty(displayName) ? NormalizePrefix(prefix) : displayName,
            MovementFolder = string.Empty,
        };

        AnimatorStateMachine machine = GetLocomotionStateMachine(controller);
        if (machine == null) return set;

        string normalized = NormalizePrefix(prefix);
        var states = new Dictionary<LocoState, AnimatorState>();

        foreach (AnimatorState state in EnumerateStates(machine))
        {
            string stateName = state.name;
            if (!stateName.StartsWith(normalized, StringComparison.Ordinal)) continue;

            string suffix = stateName.Substring(normalized.Length);
            if (!Enum.TryParse(suffix, out LocoState parsed)) continue;
            states[parsed] = state;
        }

        string idleClip = null;
        if (states.TryGetValue(LocoState.Idle, out AnimatorState idleState) && idleState.motion is AnimationClip idleAnimation)
            idleClip = idleAnimation.name;

        var clipPaths = new List<string>();

        foreach (LocoState state in Enum.GetValues(typeof(LocoState)))
        {
            if (!states.TryGetValue(state, out AnimatorState animatorState) || animatorState.motion == null) continue;

            if (animatorState.motion is AnimationClip clip)
            {
                AddClipPath(clipPaths, clip);
                set.OneShots.Add(new LocoClipDefinition
                {
                    State = state,
                    Clip = clip.name,
                    Loop = ReadLoop(clip),
                    Air = AirStates.Contains(state),
                    Speed = animatorState.speed,
                    FirstFrame = ReadFirstFrame(clip),
                });
                continue;
            }

            if (!(animatorState.motion is BlendTree tree)) continue;

            if (state == LocoState.Loop)
            {
                foreach (ChildMotion child in tree.children)
                {
                    if (!(child.motion is AnimationClip gaitClip)) continue;
                    AddClipPath(clipPaths, gaitClip);
                    set.Gaits.Add(new GaitClipDefinition { Threshold = child.threshold, Clip = gaitClip.name });
                }
            }
            else if (state == LocoState.Strafe || state == LocoState.CrouchMove)
            {
                foreach (ChildMotion child in tree.children)
                {
                    if (!(child.motion is AnimationClip directionClip)) continue;
                    // builder tự thêm Idle ở giữa (0,0) cho cây Strafe -> bỏ khi đọc lại
                    if (state == LocoState.Strafe && child.position.sqrMagnitude < 0.0001f
                        && string.Equals(directionClip.name, idleClip, StringComparison.OrdinalIgnoreCase)) continue;

                    AddClipPath(clipPaths, directionClip);
                    var row = new DirectionalClipDefinition { X = child.position.x, Y = child.position.y, Clip = directionClip.name };
                    if (state == LocoState.Strafe) set.Strafe.Add(row);
                    else set.Crouch.Add(row);
                }
            }
        }

        // thư mục chứa toàn bộ clip đang dùng -> dùng làm thư mục dự phòng khi muốn giữ anim cũ
        set.MovementFolder = CommonFolder(clipPaths);
        return set;
    }

    private static ModelImporterClipAnimation FindImporterClip(AnimationClip clip)
    {
        if (clip == null) return null;
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path)) return null;

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;

        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation entry in clips)
        {
            if (string.Equals(entry.name, clip.name, StringComparison.Ordinal)) return entry;
        }
        return null;
    }

    private static bool ReadLoop(AnimationClip clip)
    {
        ModelImporterClipAnimation entry = FindImporterClip(clip);
        return entry != null ? entry.loopTime : clip.isLooping;
    }

    private static int ReadFirstFrame(AnimationClip clip)
    {
        ModelImporterClipAnimation entry = FindImporterClip(clip);
        if (entry == null) return -1;
        int firstFrame = Mathf.RoundToInt(entry.firstFrame);
        return firstFrame > 0 ? firstFrame : -1;
    }

    #endregion

    #region Quét clip trong thư mục

    public sealed class ClipIndex
    {
        public string Folder;
        public readonly List<string> Names = new List<string>();
        public readonly Dictionary<string, string> PathByClip = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> DuplicateNames = new List<string>();
        public readonly HashSet<string> NameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool IsValid => Names.Count > 0;

        public bool Contains(string clipName)
        {
            return !string.IsNullOrEmpty(clipName) && NameSet.Contains(clipName);
        }
    }

    private static readonly Dictionary<string, ClipIndex> Cache = new Dictionary<string, ClipIndex>();

    public static void ClearCache() => Cache.Clear();

    public static bool IsInplacePath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.IndexOf("inplace", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static ClipIndex ScanClips(string folder)
    {
        folder = NormalizeFolder(folder);
        if (Cache.TryGetValue(folder, out ClipIndex cached)) return cached;

        var index = new ClipIndex { Folder = folder };
        if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
        {
            var pathsByClip = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".fbx" && extension != ".anim") continue;

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null) continue;
                foreach (UnityEngine.Object asset in assets)
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!pathsByClip.TryGetValue(clip.name, out List<string> paths)) pathsByClip[clip.name] = paths = new List<string>();
                    if (!paths.Contains(path)) paths.Add(path);
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in pathsByClip)
            {
                index.Names.Add(entry.Key);
                index.NameSet.Add(entry.Key);
                if (entry.Value.Count > 1) index.DuplicateNames.Add(entry.Key);
                // Nhiều pack có 2 bản trùng tên clip (bản thường + bản trong thư mục Inplace).
                // Locomotion cần bản có root motion nên ưu tiên file không nằm trong Inplace.
                index.PathByClip[entry.Key] = entry.Value
                    .OrderBy(path => IsInplacePath(path) ? 1 : 0)
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .First();
            }
            index.Names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        Cache[folder] = index;
        return index;
    }

    // Gộp thư mục chính + thư mục dự phòng: clip trùng tên thì bản ở thư mục chính thắng
    public static ClipIndex ScanClips(string folder, IEnumerable<string> extraFolders)
    {
        ClipIndex primary = ScanClips(folder);
        if (extraFolders == null) return primary;

        var merged = new ClipIndex { Folder = primary.Folder };
        merged.Names.AddRange(primary.Names);
        merged.NameSet.UnionWith(primary.NameSet);
        merged.DuplicateNames.AddRange(primary.DuplicateNames);
        foreach (KeyValuePair<string, string> entry in primary.PathByClip) merged.PathByClip[entry.Key] = entry.Value;

        foreach (string extra in extraFolders)
        {
            string clean = NormalizeFolder(extra);
            if (string.IsNullOrEmpty(clean) || clean == primary.Folder) continue;

            ClipIndex other = ScanClips(clean);
            foreach (string name in other.Names)
            {
                if (!merged.NameSet.Add(name)) continue;
                merged.Names.Add(name);
                merged.PathByClip[name] = other.PathByClip[name];
            }
        }

        merged.Names.Sort(StringComparer.OrdinalIgnoreCase);
        return merged;
    }

    #endregion

    #region Nhân bản / ghi

    // Thư mục đang chứa clip của template (dùng làm thư mục dự phòng khi muốn giữ anim cũ)
    public static List<string> TemplateFolders(AnimSetDefinition template)
    {
        var folders = new List<string>();
        if (template == null) return folders;

        AddUniqueFolder(folders, template.MovementFolder);
        foreach (string extra in template.ExtraFolders) AddUniqueFolder(folders, extra);
        return folders;
    }

    private static void AddUniqueFolder(List<string> folders, string folder)
    {
        string clean = NormalizeFolder(folder);
        if (string.IsNullOrEmpty(clean)) return;
        if (folders.Contains(clean)) return;
        folders.Add(clean);
    }

    private static void AddClipPath(List<string> paths, AnimationClip clip)
    {
        if (clip == null) return;
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path) || paths.Contains(path)) return;
        paths.Add(path);
    }

    private static bool IsSameOrInside(string folder, string parent)
    {
        if (string.Equals(folder, parent, StringComparison.OrdinalIgnoreCase)) return true;
        return folder.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }

    // Thư mục sâu nhất chứa toàn bộ clip đang dùng (để đoán MovementFolder của template đọc từ controller)
    private static string CommonFolder(List<string> assetPaths)
    {
        string common = null;
        foreach (string path in assetPaths)
        {
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder)) continue;
            folder = folder.Replace('\\', '/');

            if (common == null)
            {
                common = folder;
                continue;
            }

            while (common.Length > 0 && !IsSameOrInside(folder, common))
            {
                int cut = common.LastIndexOf('/');
                if (cut <= 0) return string.Empty;
                common = common.Substring(0, cut);
            }
        }
        return common ?? string.Empty;
    }

    public static string NormalizeFolder(string folder)
    {
        string value = (folder ?? string.Empty).Trim();
        while (value.EndsWith("/")) value = value.Substring(0, value.Length - 1);
        return value;
    }

    public static string NormalizePrefix(string prefix)
    {
        string value = (prefix ?? string.Empty).Trim();
        if (value.Length == 0) return string.Empty;
        return value.EndsWith("_") ? value : value + "_";
    }

    public static int IndexOfPrefix(AnimSetLibrary library, string prefix)
    {
        if (library == null) return -1;
        string wanted = NormalizePrefix(prefix);
        for (int i = 0; i < library.Sets.Count; i++)
        {
            if (string.Equals(NormalizePrefix(library.Sets[i].Prefix), wanted, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    // Vị trí của chính object này trong library (-1 = chưa ghi)
    public static int IndexOfReference(AnimSetLibrary library, AnimSetDefinition set)
    {
        if (library == null || set == null) return -1;
        for (int i = 0; i < library.Sets.Count; i++)
        {
            if (ReferenceEquals(library.Sets[i], set)) return i;
        }
        return -1;
    }

    // ignoreSet: bộ đang sửa (đã nằm trong library) -> không tự báo trùng với chính nó
    public static bool PrefixExists(AnimSetLibrary library, string prefix, int ignoreIndex = -1, AnimSetDefinition ignoreSet = null)
    {
        if (library == null) return false;
        string wanted = NormalizePrefix(prefix);
        for (int i = 0; i < library.Sets.Count; i++)
        {
            if (i == ignoreIndex) continue;
            if (ignoreSet != null && ReferenceEquals(library.Sets[i], ignoreSet)) continue;
            if (string.Equals(NormalizePrefix(library.Sets[i].Prefix), wanted, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // Gợi ý prefix từ tên hiển thị, tự tránh trùng (Vergil -> VE_, nếu trùng -> VE2_)
    public static string SuggestPrefix(AnimSetLibrary library, string displayName)
    {
        string firstWord = (displayName ?? string.Empty).Trim();
        int space = firstWord.IndexOf(' ');
        if (space > 0) firstWord = firstWord.Substring(0, space);
        string letters = new string(firstWord.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        string basis = letters.Length >= 2 ? letters.Substring(0, 2) : "NV";

        string prefix = basis + "_";
        int suffix = 2;
        while (PrefixExists(library, prefix))
        {
            prefix = basis + suffix + "_";
            suffix++;
        }
        return prefix;
    }

    public static AnimSetLibrary CreateEmptyLibrary()
    {
        AnimSetLibrary existing = AssetDatabase.LoadAssetAtPath<AnimSetLibrary>(LibraryPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
        var library = ScriptableObject.CreateInstance<AnimSetLibrary>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();
        return library;
    }

    // Copy sâu: bộ mới không dính tham chiếu tới bộ template
    public static AnimSetDefinition CloneSet(AnimSetDefinition source, string prefix, string displayName, string folder)
    {
        string name = string.IsNullOrEmpty(displayName)
            ? (source != null ? source.DisplayName + " (copy)" : "Bộ mới")
            : displayName;

        var set = new AnimSetDefinition
        {
            Prefix = NormalizePrefix(prefix),
            DisplayName = name,
            MovementFolder = NormalizeFolder(folder),
        };

        if (source == null) return set;

        foreach (LocoClipDefinition row in source.OneShots)
        {
            set.OneShots.Add(new LocoClipDefinition
            {
                State = row.State,
                Clip = row.Clip,
                Loop = row.Loop,
                Air = row.Air,
                Speed = row.Speed,
                FirstFrame = row.FirstFrame,
            });
        }

        foreach (GaitClipDefinition row in source.Gaits)
            set.Gaits.Add(new GaitClipDefinition { Threshold = row.Threshold, Clip = row.Clip });

        foreach (DirectionalClipDefinition row in source.Strafe)
            set.Strafe.Add(new DirectionalClipDefinition { X = row.X, Y = row.Y, Clip = row.Clip });

        foreach (DirectionalClipDefinition row in source.Crouch)
            set.Crouch.Add(new DirectionalClipDefinition { X = row.X, Y = row.Y, Clip = row.Clip });

        foreach (string extraFolder in source.ExtraFolders) AddUniqueFolder(set.ExtraFolders, extraFolder);

        return set;
    }

    // overwriteIndex < 0 = thêm bộ mới, >= 0 = thay bộ đang có (giữ vị trí -> giữ AnimSetId).
    // Bấm Ghi nhiều lần thì cập nhật đúng bộ đó, không nhân bản thêm.
    public static int Write(AnimSetLibrary library, AnimSetDefinition draft, int overwriteIndex)
    {
        if (library == null || draft == null) return -1;

        int index = overwriteIndex >= 0 ? overwriteIndex : IndexOfReference(library, draft);

        Undo.RecordObject(library, index >= 0 ? "Cập nhật bộ anim" : "Thêm bộ anim");

        if (index >= 0 && index < library.Sets.Count)
        {
            // bộ nháp không mang prefab -> giữ prefab nhân vật đã gán cho bộ cũ
            if (draft.Prefab == null) draft.Prefab = library.Sets[index].Prefab;
            library.Sets[index] = draft;
        }
        else
        {
            library.Sets.Add(draft);
            index = library.Sets.Count - 1;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return index;
    }

    #endregion

    #region Kiểm tra

    public sealed class Report
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsClean => Errors.Count == 0 && Warnings.Count == 0;
    }

    public static Report Validate(AnimSetLibrary library, AnimSetDefinition set, int overwriteIndex, ClipIndex clips)
    {
        var report = new Report();
        if (set == null)
        {
            report.Errors.Add("Chưa có bộ anim nào để kiểm tra.");
            return report;
        }

        if (string.IsNullOrEmpty(set.Prefix)) report.Errors.Add("Prefix đang rỗng.");
        else if (PrefixExists(library, set.Prefix, overwriteIndex, set)) report.Errors.Add($"Prefix '{set.Prefix}' trùng với bộ khác -> state đè nhau, runtime không phân biệt được.");

        if (string.IsNullOrEmpty(set.MovementFolder))
            report.Errors.Add("Chưa trỏ thư mục anim.");
        else if (!AssetDatabase.IsValidFolder(NormalizeFolder(set.MovementFolder)))
            report.Errors.Add($"Thư mục anim không tồn tại: {set.MovementFolder}");

        foreach (string extra in set.ExtraFolders)
        {
            string clean = NormalizeFolder(extra);
            if (string.IsNullOrEmpty(clean)) continue;
            if (!AssetDatabase.IsValidFolder(clean)) report.Errors.Add($"Thư mục dự phòng không tồn tại: {extra}");
        }

        if (clips == null || !clips.IsValid)
            report.Errors.Add("Thư mục anim không có AnimationClip nào (FBX phải import Humanoid/Generic và có clip).");

        // clip dùng chung nhưng khác cờ import: builder ghi cờ theo file, dòng sau đè dòng trước
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        void CheckRow(string label, string clip, string signature)
        {
            if (string.IsNullOrEmpty(clip))
            {
                missing.Add(label);
                return;
            }

            if (clips != null && clips.IsValid && !clips.Contains(clip))
            {
                report.Errors.Add($"{label}: clip '{clip}' không có trong thư mục anim" +
                                  (set.ExtraFolders.Count == 0 ? " (bật 'Giữ anim cũ của template' để lấy clip của bộ template)" : string.Empty) + ".");
                return;
            }

            if (flags.TryGetValue(clip, out string existing))
            {
                if (existing != signature && reported.Add(clip))
                    report.Warnings.Add($"Clip '{clip}' dùng ở nhiều dòng với cờ khác nhau ({existing} vs {signature}) — import settings lấy cờ của dòng cuối.");
            }
            else
            {
                flags[clip] = signature;
            }
        }

        foreach (LocoClipDefinition row in set.OneShots)
            CheckRow(row.State.ToString(), row.Clip, (row.Loop ? "Loop" : "Once") + "/" + (row.Air ? "Air" : "Ground"));
        foreach (GaitClipDefinition row in set.Gaits)
            CheckRow("Gait " + row.Threshold.ToString("0.##"), row.Clip, "Loop/Ground");
        foreach (DirectionalClipDefinition row in set.Strafe)
            CheckRow($"Strafe ({row.X:0.##},{row.Y:0.##})", row.Clip, "Loop/Ground");
        foreach (DirectionalClipDefinition row in set.Crouch)
            CheckRow($"Crouch ({row.X:0.##},{row.Y:0.##})", row.Clip, "Loop/Ground");

        if (missing.Count > 0)
            report.Warnings.Add("Chưa có clip: " + string.Join(", ", missing) + " — state đó không được tạo trong Animator.");

        var gaitClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GaitClipDefinition row in set.Gaits)
        {
            if (!string.IsNullOrEmpty(row.Clip) && !gaitClips.Add(row.Clip))
                report.Warnings.Add($"Clip '{row.Clip}' đang dùng cho nhiều Gait — blend tree sẽ lặp cùng một clip, kiểm tra lại mapping.");
        }

        foreach (LocoState state in RequiredStates)
        {
            LocoClipDefinition row = set.OneShots.FirstOrDefault(entry => entry.State == state);
            if (row == null || string.IsNullOrEmpty(row.Clip))
                report.Errors.Add($"Thiếu state {state} — CharController sẽ kẹt ở MoveState.Turn/Stop/Dodge vì không có state để chờ kết thúc.");
        }

        foreach (LocoState state in SoftStates)
        {
            // CrouchMove là blend tree dựng từ set.Crouch, không phải OneShot
            if (state == LocoState.CrouchMove)
            {
                if (set.Crouch.Count == 0) report.Warnings.Add("Thiếu hướng CrouchMove — vẫn chạy nhưng anim crouch sai.");
                continue;
            }

            LocoClipDefinition row = set.OneShots.FirstOrDefault(entry => entry.State == state);
            if (row == null || string.IsNullOrEmpty(row.Clip))
                report.Warnings.Add($"Thiếu state {state} — vẫn chạy nhưng anim nhảy/crouch sai.");
        }

        if (set.Gaits.Count == 0)
            report.Errors.Add("Chưa có Gait nào -> blend tree Loop rỗng, nhân vật chạy tại chỗ (không có root motion).");
        if (set.Strafe.Count == 0)
            report.Warnings.Add("Chưa có hướng Strafe nào -> lock-on/strafe sẽ đứng yên tại chỗ.");

        return report;
    }

    #endregion

    #region Gợi ý clip theo tên

    // Từ vô nghĩa trong tên clip (tiền tố pack, hậu tố root motion...)
    private static readonly HashSet<string> Noise = new HashSet<string>(StringComparer.Ordinal)
    {
        "root", "inplace", "zeroheight", "zero", "height", "ver", "model", "anim", "animation",
        "take", "move", "moving", "motion", "a", "b", "c", "d", "e",
    };

    private static readonly string[] DirectionTokens = { "front", "back", "left", "right" };

    public static string SuggestClip(string oldClip, string semantic, float x, float y, ClipIndex clips, out float score)
    {
        score = 0f;
        if (clips == null || !clips.IsValid) return null;

        HashSet<string> wanted = CanonTokens(oldClip);
        wanted.UnionWith(CanonTokens(semantic));

        HashSet<string> rowDirection = DirectionsOf(wanted);
        HashSet<string> xyDirection = DirectionFromXY(x, y);
        if (xyDirection.Count > 0) rowDirection = xyDirection;

        string best = null;
        float bestScore = 0f;

        foreach (string name in clips.Names)
        {
            // Trùng tên tuyệt đối: pack mới giữ nguyên cách đặt tên -> khỏi đoán
            if (string.Equals(name, oldClip, StringComparison.OrdinalIgnoreCase))
            {
                score = 1f;
                return name;
            }

            HashSet<string> candidate = CanonTokens(name);
            HashSet<string> candidateDirection = DirectionsOf(candidate);

            if (candidateDirection.Count > 0)
            {
                if (rowDirection.Count == 0)
                {
                    // dòng không rõ hướng (Idle, Jump, Start...): loại clip sang ngang/lùi
                    if (candidateDirection.Contains("back") || candidateDirection.Contains("left") || candidateDirection.Contains("right")) continue;
                }
                else if (!candidateDirection.SetEquals(rowDirection))
                {
                    continue;
                }
            }

            int intersect = 0;
            foreach (string token in wanted)
            {
                if (candidate.Contains(token)) intersect++;
            }
            int union = wanted.Count + candidate.Count - intersect;
            float current = union > 0 ? (float)intersect / union : 0f;

            if (current > bestScore)
            {
                bestScore = current;
                best = name;
            }
        }

        score = bestScore;
        return bestScore > 0f ? best : null;
    }

    private static HashSet<string> CanonTokens(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string token in RawTokens(text))
        {
            if (Noise.Contains(token)) continue;
            Expand(token, result);
        }
        return result;
    }

    private static List<string> RawTokens(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(text)) return tokens;

        // "M_katana_Blade@Run_Fast" -> "Run_Fast"
        int at = text.IndexOf('@');
        if (at >= 0 && at + 1 < text.Length) text = text.Substring(at + 1);

        var builder = new StringBuilder();
        char previous = '\0';
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (builder.Length > 0 && char.IsUpper(character) && (char.IsLower(previous) || char.IsDigit(previous)))
                {
                    tokens.Add(builder.ToString());
                    builder.Clear();
                }
                builder.Append(character);
            }
            else if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
                builder.Clear();
            }
            previous = character;
        }
        if (builder.Length > 0) tokens.Add(builder.ToString());

        for (int i = 0; i < tokens.Count; i++) tokens[i] = tokens[i].ToLowerInvariant();
        return tokens;
    }

    private static void Expand(string token, HashSet<string> into)
    {
        switch (token)
        {
            case "jogging": case "jog": case "run": into.Add("run"); return;
            case "fast": case "sprint": case "sprinting": into.Add("sprint"); return;
            case "fwd": case "forward": case "front": into.Add("front"); return;
            case "bwd": case "backward": case "back": into.Add("back"); return;
            case "left": into.Add("left"); return;
            case "right": into.Add("right"); return;
            case "l45": into.Add("left"); return;
            case "r45": into.Add("right"); return;
            case "l90": into.Add("left"); return;
            case "r90": into.Add("right"); return;
            case "fl45": into.Add("front"); into.Add("left"); return;
            case "fr45": into.Add("front"); into.Add("right"); return;
            case "bl45": into.Add("back"); into.Add("left"); return;
            case "br45": into.Add("back"); into.Add("right"); return;
            case "avoid": case "roll": case "dodge": into.Add("dodge"); return;
            case "falling": case "fall": into.Add("fall"); return;
            case "landing": case "land": into.Add("land"); return;
            case "idle": case "stand": into.Add("idle"); return;
            case "start": into.Add("start"); return;
            case "stop": into.Add("stop"); return;
            case "to": into.Add("to"); return;
            case "turn": into.Add("turn"); return;
            case "walk": case "walking": into.Add("walk"); return;
            case "crouch": into.Add("crouch"); return;
        }

        // "run02"/"Idle_A" -> tách số dính đuôi rồi lặp lại
        int cut = token.Length;
        while (cut > 0 && char.IsDigit(token[cut - 1])) cut--;
        if (cut > 0 && cut < token.Length)
        {
            Expand(token.Substring(0, cut), into);
            return;
        }

        into.Add(token);
    }

    private static HashSet<string> DirectionsOf(HashSet<string> tokens)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string direction in DirectionTokens)
        {
            if (tokens.Contains(direction)) result.Add(direction);
        }
        return result;
    }

    private static HashSet<string> DirectionFromXY(float x, float y)
    {
        var direction = new HashSet<string>(StringComparer.Ordinal);
        const float epsilon = 0.05f;
        if (y > epsilon) direction.Add("front");
        else if (y < -epsilon) direction.Add("back");
        if (x > epsilon) direction.Add("right");
        else if (x < -epsilon) direction.Add("left");
        return direction;
    }

    #endregion
}
