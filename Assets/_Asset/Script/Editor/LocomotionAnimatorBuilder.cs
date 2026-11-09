using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Dựng layer Locomotion trong Dante 1.controller từ AnimSetLibrary:
// mỗi bộ (Katana = KT_, Big Sword = GS_, nhân vật mới = prefix tự đặt) là 1 AnimSetDefinition
// có thư mục anim + bảng clip theo LocoState. Mọi chuyển state do CharController gọi CrossFade
// nên layer không có transition nào.
//
// Bảng hard-code bên dưới chỉ dùng để Import lần đầu (Tools/Combat/Import Anim Sets From Code);
// sau đó nguồn dữ liệu là asset AnimSetLibrary.
public static class LocomotionAnimatorBuilder
{
    public const string ControllerPath = "Assets/_Asset/Animation/Player/Dante 1.controller";
    public const string PrefabPath = "Assets/_Asset/Prefab/Character.prefab";
    public const string LayerName = "Locomotion";
    public const string LibraryPath = "Assets/_Asset/Data/AnimSetLibrary.asset";

    private static readonly string[] OldLayers = { "Idle", "Move", "Jump", "Dash", LayerName };
    // pack Grruzam đã được move sang Assets/Dante/Animation (thư mục cũ bị xoá) -> sửa path để
    // "Import Anim Sets From Code" tạo lại AnimSetLibrary đúng; bộ mới cho nhân vật khác thì nhân bản qua Tools/Combat/Anim Set Clone
    private static readonly string[] MovementFolders = { "Assets/_Animation_Dante/Animation/Dante_Katana_Blade/1_Movements", "Assets/_Animation_Dante/Animation/Dante_Big_Sword/1_Movements" };

    // ------------------------------------------------------------------
    // Nguồn dữ liệu cũ (chỉ để Import lần đầu)
    // ------------------------------------------------------------------

    // tên clip theo từng bộ: { Katana, Big Sword }; null = bộ đó không có clip
    private static readonly Dictionary<LocoState, string[]> OneShots = new Dictionary<LocoState, string[]>
    {
        { LocoState.Idle, new[] { "Idle_ver_A", "Idle" } },
        { LocoState.IdleTurnL90, new[] { "Idle_A_Turn_L90_Root", "Idle_Turn_L90_Root" } },
        { LocoState.IdleTurnR90, new[] { "Idle_A_Turn_R90_Root", "Idle_Turn_R90_Root" } },
        { LocoState.IdleTurn180, new[] { "Idle_A_Turn_Root", "Idle_Turn_Root" } },
        { LocoState.StartWalk, new[] { "Idle_To_Walk_ver_A_Root", "Idle_To_Walk_ver_A_Root" } },
        { LocoState.StartJog, new[] { "Idle_A_To_Jogging_A_Root", "Idle_To_Jogging_ver_A_Root" } },
        { LocoState.StartRun, new[] { "Idle_A_To_Run_A_Root", "Idle_To_Run_ver_A_Root" } },
        { LocoState.StartWalkL90, new[] { "Idle_A_To_Walk_A_Turn_L90_Root", "Idle_To_Walk_A_Turn_L90_Root" } },
        { LocoState.StartWalkR90, new[] { "Idle_A_To_Walk_A_Turn_R90_Root", "Idle_To_Walk_A_Turn_R90_Root" } },
        { LocoState.StartJogL90, new[] { "Idle_A_To_Jogging_A_Turn_L90_Root", "Idle_To_Jogging_A_Turn_L90_Root" } },
        { LocoState.StartJogR90, new[] { "Idle_A_To_Jogging_A_Turn_R90_Root", "Idle_To_Jogging_A_Turn_R90_Root" } },
        { LocoState.StartRunL90, new[] { "Idle_A_To_Run_A_Turn_L90_Root", "Idle_To_Run_A_Turn_L90_Root" } },
        { LocoState.StartRunR90, new[] { "Idle_A_To_Run_A_Turn_R90_Root", "Idle_To_Run_A_Turn_R90_Root" } },
        { LocoState.StartWalk180, new[] { "Turn_in_place_Idle_A_to_Walk_A_Root", "Turn_in_place_Idle_to_Walk_A_Root" } },
        { LocoState.StartJog180, new[] { "Turn_in_place_Idle_A_to_Jog_A_Root", "Turn_in_place_Idle_to_Jog_A_Root" } },
        { LocoState.StartRun180, new[] { "Turn_in_place_Idle_A_to_Run_A_Root", "Turn_in_place_Idle_to_Run_A_Root" } },
        { LocoState.StopWalk, new[] { "Walk_ver_A_To_Idle_ver_A_Root", "Walk_To_Idle_ver_A_Root" } },
        { LocoState.StopRun, new[] { "Run_Fast_To_Idle_ver_A", "Run_to_Idle_ver_A" } },
        { LocoState.StopFast, new[] { "Run_Fast_To_Idle_ver_B", "Run_Fast_to_Idle_ver_A_Root" } },
        { LocoState.TurnWalkL90, new[] { "Walk_A_To_Walk_A_Turn_L90_Root", "Walk_A_To_Walk_A_Turn_L90_Root" } },
        { LocoState.TurnWalkR90, new[] { "Walk_A_To_Walk_A_Turn_R90_Root", "Walk_A_To_Walk_A_Turn_R90_Root" } },
        { LocoState.TurnJogL90, new[] { "Jogging_A_To_Jogging_A_Turn_L90_Root", "Jogging_A_To_Jogging_A_Turn_L90_Root" } },
        { LocoState.TurnJogR90, new[] { "Jogging_A_To_Jogging_A_Turn_R90_Root", "Jogging_A_To_Jogging_A_Turn_R90_Root" } },
        { LocoState.TurnWalk180, new[] { "Turn_in_place_Walk_A_to_Walk_A_Root", "Turn_in_place_Walk_A_to_Walk_A_Root" } },
        { LocoState.TurnJog180, new[] { "Turn_in_place_Jog_A_to_Jog_A_Root", "Turn_in_place_Jog_A_to_Jog_A_Root" } },
        { LocoState.TurnRun180, new[] { "Turn_in_place_Run_A_to_Run_A_Root", "Turn_in_place_Run_A_to_Run_A_Root" } },
        { LocoState.JumpStart, new[] { "Jump_Start", "Jump_Start" } },
        { LocoState.JumpLoop, new[] { "Jump_Loop", "Jump_Loop" } },
        { LocoState.JumpEnd, new[] { "Jump_End", "Jump_End" } },
        { LocoState.AirJumpStart, new[] { "Double_Jump_Start", "Double_Jump_Start" } },
        { LocoState.AirJumpLoop, new[] { "Double_Jump_Loop", "Double_Jump_Loop" } },
        { LocoState.AirJumpEnd, new[] { "Double_Jump_End", "Double_Jump_End" } },
        { LocoState.DodgeFront, new[] { "Dodge_Front", "Dodge_Front" } },
        { LocoState.DodgeBack, new[] { "Dodge_Back", "Dodge_Back" } },
        { LocoState.DodgeLeft, new[] { "Dodge_Left", "Dodge_Left" } },
        { LocoState.DodgeRight, new[] { "Dodge_Right", "Dodge_Right" } },
        { LocoState.RunDodgeLeft, new[] { "Run_Fast_Dodge_Left", null } },
        { LocoState.RunDodgeRight, new[] { "Run_Fast_Dodge_Right", null } },
        { LocoState.CrouchEnter, new[] { "Idle_ver_A_To_Crouch_ver_A_Idle_Root", "Idle_To_Crouch_Root" } },
    };

    // bản không ZeroHeight: độ cao nằm ở root (bị bỏ qua, vật lý lo), pose giữ nguyên so với capsule
    private static readonly HashSet<LocoState> AirStates = new HashSet<LocoState>
    {
        LocoState.JumpStart, LocoState.JumpLoop, LocoState.JumpEnd,
        LocoState.AirJumpStart, LocoState.AirJumpLoop, LocoState.AirJumpEnd,
    };

    // 12 frame đầu (0.4s) của clip dừng Katana là nguyên 1 vòng chạy nhanh: giữ lại thì nhả phím vẫn chạy tại chỗ
    private static readonly Dictionary<string, int> ClipFirstFrames = new Dictionary<string, int>
    {
        { "Run_Fast_To_Idle_ver_A", 12 },
        { "Run_Fast_To_Idle_ver_B", 12 },
    };

    // clip khởi động + quay tại chỗ gần như không dời chỗ, phát nhanh hơn để bớt độ trễ từ lúc bấm tới lúc chạy
    private const float TurnClipSpeed = 1.25f;
    private static readonly HashSet<LocoState> FastTurnStates = new HashSet<LocoState>
    {
        LocoState.IdleTurnL90, LocoState.IdleTurnR90, LocoState.IdleTurn180,
        LocoState.StartWalkL90, LocoState.StartWalkR90, LocoState.StartJogL90, LocoState.StartJogR90, LocoState.StartRunL90, LocoState.StartRunR90,
        LocoState.StartWalk180, LocoState.StartJog180, LocoState.StartRun180,
    };

    private static readonly string[][] LoopGaits =
    {
        new[] { "Walk_ver_A_Front_Root", "Walk_ver_A_Front_Root" },
        new[] { "Jogging_8Way_verA_F_Root", "Jogging_8Way_verA_F_Root" },
        new[] { "Run_Root_ver_A", "Run_ver_A_Root" },
        new[] { "Run_Fast_Root_ver_A", "Run_Fast_ver_A_Root" },
    };

    // { x, y, tên Katana, tên Big Sword }: y+ = tới, x+ = phải
    private static readonly (float x, float y, string[] clips)[] StrafeJog =
    {
        (0f, 1f, new[] { "Jogging_8Way_verA_F_Root", "Jogging_8Way_verA_F_Root" }),
        (-0.707f, 0.707f, new[] { "Jogging_8Way_verA_FL45_Root", "Jogging_8Way_verA_FL45_Root" }),
        (-1f, 0f, new[] { "Jogging_8Way_verA_L90_Root", "Jogging_8Way_verA_L90_Root" }),
        (-0.707f, -0.707f, new[] { "Jogging_8Way_verA_BL45_Root", "Jogging_8Way_verA_BL45_Root" }),
        (0f, -1f, new[] { "Jogging_8Way_verA_B_Root", "Jogging_8Way_verA_B_Root" }),
        (0.707f, -0.707f, new[] { "Jogging_8Way_verA_BR45_Root", "Jogging_8Way_verA_BR45_Root" }),
        (1f, 0f, new[] { "Jogging_8Way_verA_R90_Root", "Jogging_8Way_verA_R90_Root" }),
        (0.707f, 0.707f, new[] { "Jogging_8Way_verA_FR45_Root", "Jogging_8Way_verA_FR45_Root" }),
    };

    private static readonly (float x, float y, string[] clips)[] StrafeWalk =
    {
        (0f, 0.5f, new[] { "Walk_ver_A_Front_Root", "Walk_ver_A_Front_Root" }),
        (-0.3535f, 0.3535f, new[] { "Walk_ver_A_Front_L45_Root", "Walk_ver_A_Front_L45_Root" }),
        (-0.5f, 0f, new[] { "Walk_ver_A_L90_Root", "Walk_ver_A_L90_Root" }),
        (-0.3535f, -0.3535f, new[] { "Walk_ver_A_Back_L45_Root", "Walk_ver_A_Back_L45_Root" }),
        (0f, -0.5f, new[] { "Walk_ver_A_Back_Root", "Walk_ver_A_Back_Root" }),
        (0.3535f, -0.3535f, new[] { "Walk_ver_A_Back_R45_Root", "Walk_ver_A_Back_R45_Root" }),
        (0.5f, 0f, new[] { "Walk_ver_A_R90_Root", "Walk_ver_A_R90_Root" }),
        (0.3535f, 0.3535f, new[] { "Walk_ver_A_Front_R45_Root", "Walk_ver_A_Front_R45_Root" }),
    };

    private static readonly (float x, float y, string[] clips)[] CrouchMove =
    {
        (0f, 0f, new[] { "Crouch_ver_A_Idle_Root", "Crouch_Idle_Root" }),
        (0f, 1f, new[] { "Crouch_ver_A_Fwd_Root", "Crouch_Fwd_Root" }),
        (-0.707f, 0.707f, new[] { "Crouch_ver_A_FL45_Root", "Crouch_FL45_Root" }),
        (-1f, 0f, new[] { "Crouch_ver_A_L90_Root", "Crouch_L90_Root" }),
        (-0.707f, -0.707f, new[] { "Crouch_ver_A_BL45_Root", "Crouch_BL45_Root" }),
        (0f, -1f, new[] { "Crouch_ver_A_Bwd_Root", "Crouch_Bwd_Root" }),
        (0.707f, -0.707f, new[] { "Crouch_ver_A_BR45_Root", "Crouch_BR45_Root" }),
        (1f, 0f, new[] { "Crouch_ver_A_R90_Root", "Crouch_R90_Root" }),
        (0.707f, 0.707f, new[] { "Crouch_ver_A_FR45_Root", "Crouch_FR45_Root" }),
    };

    // ------------------------------------------------------------------
    // Import bảng cũ -> asset (chạy 1 lần, sau đó sửa trực tiếp trong asset)
    // ------------------------------------------------------------------

    [MenuItem("Tools/Combat/Import Anim Sets From Code")]
    public static AnimSetLibrary CreateLibraryFromCode()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
        AnimSetLibrary existing = AssetDatabase.LoadAssetAtPath<AnimSetLibrary>(LibraryPath);
        if (existing != null) AssetDatabase.DeleteAsset(LibraryPath);

        AnimSetLibrary library = ScriptableObject.CreateInstance<AnimSetLibrary>();
        for (int set = 0; set < LocoStates.SetPrefixes.Length; set++)
        {
            AnimSetDefinition definition = new AnimSetDefinition
            {
                Prefix = LocoStates.SetPrefixes[set],
                DisplayName = set == 0 ? "Katana" : set == 1 ? "Great Sword" : "Bộ " + set,
                MovementFolder = set < MovementFolders.Length ? MovementFolders[set] : string.Empty,
            };

            foreach (KeyValuePair<LocoState, string[]> entry in OneShots)
            {
                string clip = entry.Value[set];
                if (string.IsNullOrEmpty(clip)) continue;
                definition.OneShots.Add(new LocoClipDefinition
                {
                    State = entry.Key,
                    Clip = clip,
                    Loop = entry.Key == LocoState.Idle || entry.Key == LocoState.JumpLoop || entry.Key == LocoState.AirJumpLoop,
                    Air = AirStates.Contains(entry.Key),
                    Speed = FastTurnStates.Contains(entry.Key) ? TurnClipSpeed : 1f,
                    FirstFrame = ClipFirstFrames.TryGetValue(clip, out int firstFrame) ? firstFrame : -1,
                });
            }

            for (int i = 0; i < LoopGaits.Length; i++)
                definition.Gaits.Add(new GaitClipDefinition { Threshold = i + 1f, Clip = LoopGaits[i][set] });

            foreach (var row in StrafeWalk)
                definition.Strafe.Add(new DirectionalClipDefinition { X = row.x, Y = row.y, Clip = row.clips[set] });
            foreach (var row in StrafeJog)
                definition.Strafe.Add(new DirectionalClipDefinition { X = row.x, Y = row.y, Clip = row.clips[set] });
            foreach (var row in CrouchMove)
                definition.Crouch.Add(new DirectionalClipDefinition { X = row.x, Y = row.y, Clip = row.clips[set] });

            library.Sets.Add(definition);
        }

        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[LocomotionAnimatorBuilder] Đã tạo " + LibraryPath + " từ bảng trong code.");
        return library;
    }

    public static AnimSetLibrary FindLibrary()
    {
        AnimSetLibrary direct = AssetDatabase.LoadAssetAtPath<AnimSetLibrary>(LibraryPath);
        if (direct != null) return direct;
        string[] guids = AssetDatabase.FindAssets("t:AnimSetLibrary");
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<AnimSetLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    [MenuItem("Tools/Combat/Build Locomotion Animator")]
    public static void Build()
    {
        AnimSetLibrary library = FindLibrary();
        if (library == null)
        {
            Debug.LogError("[LocomotionAnimatorBuilder] Chưa có AnimSetLibrary. Mở Tools/Combat/Timing Window > tab 'Bộ anim & Vũ khí' > Tạo từ code.");
            return;
        }
        BuildFrom(library);
    }

    public static void BuildFrom(AnimSetLibrary library)
    {
        if (library == null || library.Sets.Count == 0)
        {
            Debug.LogError("[LocomotionAnimatorBuilder] AnimSetLibrary rỗng.");
            return;
        }

        List<Dictionary<string, AnimationClip>> clipsBySet = new List<Dictionary<string, AnimationClip>>();
        foreach (AnimSetDefinition set in library.Sets)
        {
            Dictionary<string, AnimationClip> clips = LoadClips(set);
            ApplyImportSettings(set, clips);
            // reimport tạo lại object clip -> phải nạp lại
            clipsBySet.Add(LoadClips(set));
        }

        BuildController(library, clipsBySet);
        SetupPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("[LocomotionAnimatorBuilder] Xong " + library.Sets.Count + " bộ anim.");
    }

    // Clip của bộ: thư mục chính trước, rồi tới các thư mục dự phòng (clip dùng chung với bộ template).
    // Trùng tên thì bản trong thư mục chính thắng.
    private static Dictionary<string, AnimationClip> LoadClips(AnimSetDefinition set)
    {
        Dictionary<string, AnimationClip> clips = LoadClips(set.MovementFolder);
        foreach (string folder in set.ExtraFolders)
        {
            if (string.IsNullOrEmpty(folder)) continue;
            foreach (KeyValuePair<string, AnimationClip> entry in LoadClips(folder))
            {
                if (!clips.ContainsKey(entry.Key)) clips[entry.Key] = entry.Value;
            }
        }
        return clips;
    }

    private static Dictionary<string, AnimationClip> LoadClips(string folder)
    {
        var clips = new Dictionary<string, AnimationClip>();
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return clips;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[clip.name] = clip;
            }
        }
        return clips;
    }

    private static AnimationClip TryFind(Dictionary<string, AnimationClip> clips, string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;
        if (clips.TryGetValue(clipName, out AnimationClip clip)) return clip;
        Debug.LogError($"[LocomotionAnimatorBuilder] Không thấy clip '{clipName}' trong thư mục anim của bộ này.");
        return null;
    }

    #region Import settings

    // bake xoay + độ cao vào pose, chỉ để XZ làm root motion -> mọi clip có cùng 1 quy ước, không phụ thuộc cách đóng gói của pack
    private static void ApplyImportSettings(AnimSetDefinition set, Dictionary<string, AnimationClip> clips)
    {
        var paths = new Dictionary<string, (bool lockXZ, bool lockY, bool loop, int firstFrame)>();
        string primaryFolder = set.MovementFolder == null ? string.Empty : set.MovementFolder.TrimEnd('/');

        void Add(string clipName, bool lockXZ, bool lockY, bool loop, int firstFrame)
        {
            if (string.IsNullOrEmpty(clipName) || !clips.TryGetValue(clipName, out AnimationClip clip)) return;

            // clip lấy từ thư mục dự phòng (dùng chung với bộ khác): giữ nguyên import settings
            // của bộ sở hữu, không ghi đè để không làm hỏng anim của nhân vật kia
            string path = AssetDatabase.GetAssetPath(clip);
            if (primaryFolder.Length > 0 && !path.StartsWith(primaryFolder + "/", StringComparison.OrdinalIgnoreCase)) return;

            paths[path] = (lockXZ, lockY, loop, firstFrame);
        }

        foreach (LocoClipDefinition row in set.OneShots) Add(row.Clip, row.Air || row.State == LocoState.Idle, !row.Air, row.Loop, row.FirstFrame);
        foreach (GaitClipDefinition row in set.Gaits) Add(row.Clip, false, true, true, -1);
        foreach (DirectionalClipDefinition row in set.Strafe) Add(row.Clip, false, true, true, -1);
        foreach (DirectionalClipDefinition row in set.Crouch) Add(row.Clip, false, true, true, -1);
        // bay: vận tốc do code, clip Inplace giữ độ cao gốc trong pose (người bay lên nằm sẵn trong anim)
        foreach (DirectionalClipDefinition row in set.Fly) Add(row.Clip, true, true, true, -1);

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var entry in paths) ApplyImport(entry.Key, entry.Value.lockXZ, entry.Value.lockY, entry.Value.loop, entry.Value.firstFrame);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static void ApplyImport(string path, bool lockXZ, bool lockY, bool loop, int firstFrame)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            changed |= clip.lockRootRotation != true || clip.keepOriginalOrientation != true
                || clip.lockRootHeightY != lockY || clip.keepOriginalPositionY != lockY || clip.heightFromFeet == lockY
                || clip.lockRootPositionXZ != lockXZ || clip.loopTime != loop;
            if (firstFrame >= 0 && Mathf.RoundToInt(clip.firstFrame) != firstFrame)
            {
                clip.firstFrame = firstFrame;
                changed = true;
            }
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = lockY;
            // clip trên không: root bám theo bàn chân để pose không mang độ cao của cú nhảy
            clip.keepOriginalPositionY = lockY;
            clip.heightFromFeet = !lockY;
            clip.lockRootPositionXZ = lockXZ;
            // pack đặt loop không đồng nhất (vd Run_Fast của Big Sword) -> clip dừng ở frame cuối, root motion về 0
            clip.loopTime = loop;
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    #endregion

    #region Controller

    private static void BuildController(AnimSetLibrary library, List<Dictionary<string, AnimationClip>> clipsBySet)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException($"[LocomotionAnimatorBuilder] Không thấy {ControllerPath}");

        List<AnimatorControllerLayer> layers = controller.layers.ToList();
        foreach (AnimatorControllerLayer layer in layers.Where(l => OldLayers.Contains(l.name)).ToList())
        {
            DestroyStateMachine(layer.stateMachine);
            layers.Remove(layer);
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters) controller.RemoveParameter(parameter);
        controller.AddParameter("Gait", AnimatorControllerParameterType.Float);
        controller.AddParameter("Move_X", AnimatorControllerParameterType.Float);
        controller.AddParameter("Move_Y", AnimatorControllerParameterType.Float);
        AnimatorControllerParameter gait = controller.parameters.First(p => p.name == "Gait");
        gait.defaultFloat = 1f;
        controller.parameters = controller.parameters.Select(p => p.name == "Gait" ? gait : p).ToArray();

        var stateMachine = new AnimatorStateMachine
        {
            name = LayerName,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        for (int set = 0; set < library.Sets.Count; set++) BuildSet(controller, stateMachine, library.Sets[set], clipsBySet[set], set);
        stateMachine.defaultState = PickDefaultState(stateMachine, library.Sets[0].Prefix);

        layers.Insert(0, new AnimatorControllerLayer
        {
            name = LayerName,
            stateMachine = stateMachine,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            iKPass = true,
        });
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState PickDefaultState(AnimatorStateMachine stateMachine, string prefix)
    {
        if (stateMachine.states.Length == 0) return null;
        string wanted = prefix + LocoState.Idle;
        AnimatorState idle = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == wanted);
        return idle != null ? idle : stateMachine.states[0].state;
    }

    private static void BuildSet(AnimatorController controller, AnimatorStateMachine stateMachine, AnimSetDefinition set, Dictionary<string, AnimationClip> clips, int setIndex)
    {
        int row = 0;
        void AddState(LocoState state, Motion motion, float speed)
        {
            if (motion == null) return;
            AnimatorState animatorState = stateMachine.AddState(set.Prefix + state, new Vector3(300f + setIndex * 500f, row++ * 45f, 0f));
            animatorState.motion = motion;
            if (!Mathf.Approximately(speed, 1f)) animatorState.speed = speed;
        }

        foreach (LocoClipDefinition entry in set.OneShots) AddState(entry.State, TryFind(clips, entry.Clip), entry.Speed);

        BlendTree loop = NewTree(controller, set.Prefix + "Loop", BlendTreeType.Simple1D);
        loop.blendParameter = "Gait";
        loop.useAutomaticThresholds = false;
        foreach (GaitClipDefinition gait in set.Gaits)
        {
            AnimationClip clip = TryFind(clips, gait.Clip);
            if (clip != null) loop.AddChild(clip, gait.Threshold);
        }
        AddState(LocoState.Loop, loop, 1f);

        BlendTree strafe = NewTree(controller, set.Prefix + "Strafe", BlendTreeType.FreeformDirectional2D);
        strafe.blendParameter = "Move_X";
        strafe.blendParameterY = "Move_Y";
        LocoClipDefinition idle = set.Idle;
        AnimationClip idleClip = idle != null ? TryFind(clips, idle.Clip) : null;
        if (idleClip != null) strafe.AddChild(idleClip, Vector2.zero);
        foreach (DirectionalClipDefinition child in set.Strafe)
        {
            AnimationClip clip = TryFind(clips, child.Clip);
            if (clip != null) strafe.AddChild(clip, new Vector2(child.X, child.Y));
        }
        AddState(LocoState.Strafe, strafe, 1f);

        BlendTree crouch = NewTree(controller, set.Prefix + "CrouchMove", BlendTreeType.FreeformDirectional2D);
        crouch.blendParameter = "Move_X";
        crouch.blendParameterY = "Move_Y";
        foreach (DirectionalClipDefinition child in set.Crouch)
        {
            AnimationClip clip = TryFind(clips, child.Clip);
            if (clip != null) crouch.AddChild(clip, new Vector2(child.X, child.Y));
        }
        AddState(LocoState.CrouchMove, crouch, 1f);

        if (set.Fly.Count == 0) return;
        BlendTree fly = NewTree(controller, set.Prefix + "FlyMove", BlendTreeType.FreeformDirectional2D);
        fly.blendParameter = "Move_X";
        fly.blendParameterY = "Move_Y";
        LocoClipDefinition flyIdle = set.OneShots.Find(row => row.State == LocoState.FlyIdle);
        AnimationClip flyIdleClip = flyIdle != null ? TryFind(clips, flyIdle.Clip) : null;
        if (flyIdleClip != null) fly.AddChild(flyIdleClip, Vector2.zero);
        foreach (DirectionalClipDefinition child in set.Fly)
        {
            AnimationClip clip = TryFind(clips, child.Clip);
            if (clip != null) fly.AddChild(clip, new Vector2(child.X, child.Y));
        }
        AddState(LocoState.FlyMove, fly, 1f);
    }

    private static BlendTree NewTree(AnimatorController controller, string name, BlendTreeType type)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = type,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static void DestroyStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines) DestroyStateMachine(child.stateMachine);
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.motion is BlendTree tree) DestroyTree(tree);
            foreach (AnimatorStateTransition transition in child.state.transitions) UnityEngine.Object.DestroyImmediate(transition, true);
            UnityEngine.Object.DestroyImmediate(child.state, true);
        }
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions) UnityEngine.Object.DestroyImmediate(transition, true);
        foreach (AnimatorTransition transition in stateMachine.entryTransitions) UnityEngine.Object.DestroyImmediate(transition, true);
        UnityEngine.Object.DestroyImmediate(stateMachine, true);
    }

    private static void DestroyTree(BlendTree tree)
    {
        foreach (ChildMotion child in tree.children)
        {
            if (child.motion is BlendTree nested) DestroyTree(nested);
        }
        UnityEngine.Object.DestroyImmediate(tree, true);
    }

    #endregion

    #region Prefab

    private static void SetupPrefab()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException($"[LocomotionAnimatorBuilder] {PrefabPath} không có Animator");
            if (animator.GetComponent<AnimatorRootMotion>() == null) animator.gameObject.AddComponent<AnimatorRootMotion>();
            if (animator.GetComponent<PoseBlend>() == null) animator.gameObject.AddComponent<PoseBlend>();
            animator.applyRootMotion = true;
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    #endregion
}
