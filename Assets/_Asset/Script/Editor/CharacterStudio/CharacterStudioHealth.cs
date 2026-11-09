using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using State = StudioGUI.State;

public partial class CharacterStudioWindow
{
    private enum FixKind { None, AddComponent, AssignController, AssignWeapons }

    private sealed class Check
    {
        public State State;
        public string Label;
        public string Detail;
        public FixKind Fix;
        public Type Component;
        public string FixLabel;
        public bool Optional;
    }

    /// <summary>
    /// Ảnh chụp tình trạng của 1 nhân vật. Tính lười, bỏ đi khi đổi nhân vật / Undo / project đổi.
    /// </summary>
    private sealed class Health
    {
        public GameObject Prefab;
        public Animator Animator;
        public readonly List<Check> PrefabChecks = new List<Check>();
        public State PrefabState;

        public AnimSetCloneTool.ClipIndex Clips;
        public int Rows;
        public int MissingClips;
        public bool PrefixGenerated;
        public bool PrefixCountMatches;
        public int ControllerStates;
        public State MovesetState;

        public int Weapons;
        public int Chains;
        public int Steps;
        public int WeaponsBuilt;
        public int MissingStepClips;
        public State ComboState;

        public int BuiltSteps;
        public int StepsWithoutHits;
        public State TimingState;

        public readonly List<string> BuildIssues = new List<string>();
        public State BuildState;

        public State Overall => Worst(PrefabState, MovesetState, ComboState, BuildState);

        public State StateOf(Page page)
        {
            switch (page)
            {
                case Page.Prefab: return PrefabState;
                case Page.Moveset: return MovesetState;
                case Page.Combo: return ComboState;
                case Page.Timing: return TimingState;
                case Page.Grip: return WeaponsBuilt > 0 ? State.Ok : State.None;
                case Page.Build: return BuildState;
                case Page.Overview: return Overall;
                default: return State.None;
            }
        }

        public static State Worst(params State[] states)
        {
            if (states.Contains(State.Error)) return State.Error;
            if (states.Contains(State.Warn)) return State.Warn;
            return states.All(s => s == State.Ok || s == State.None) && states.Any(s => s == State.Ok) ? State.Ok : State.None;
        }

        public static Health Compute(AnimSetLibrary sets, int index, List<WeaponDefinition> weapons)
        {
            var health = new Health();
            if (sets == null || index < 0 || index >= sets.Sets.Count) return health;
            AnimSetDefinition set = sets.Sets[index];

            health.ComputeMoveset(sets, set, index);
            health.ComputeCombo(weapons, index);
            health.ComputePrefab(set, weapons);
            health.ComputeBuild();
            return health;
        }

        private void ComputeMoveset(AnimSetLibrary sets, AnimSetDefinition set, int index)
        {
            Clips = AnimSetCloneTool.ScanClips(set.MovementFolder, set.ExtraFolders);
            foreach (string clip in AllClips(set))
            {
                Rows++;
                if (string.IsNullOrEmpty(clip) || !Clips.Contains(clip)) MissingClips++;
            }

            string[] prefixes = LocoSetPrefixes.Values;
            PrefixGenerated = index < prefixes.Length && prefixes[index] == set.Prefix;
            PrefixCountMatches = prefixes.Length == sets.Sets.Count;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
            AnimatorStateMachine machine = AnimSetCloneTool.GetLocomotionStateMachine(controller);
            if (machine != null && !string.IsNullOrEmpty(set.Prefix))
                ControllerStates = machine.states.Count(s => s.state != null && s.state.name.StartsWith(set.Prefix, StringComparison.Ordinal));

            if (Rows == 0 || IsBroken(set.MovementFolder)) MovesetState = State.Error;
            else if (MissingClips > 0) MovesetState = State.Warn;
            else MovesetState = State.Ok;
        }

        private void ComputeCombo(List<WeaponDefinition> weapons, int index)
        {
            Weapons = weapons.Count;
            foreach (WeaponDefinition weapon in weapons)
            {
                Chains += weapon.Chains.Count;
                AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(weapon.AnimFolder);
                foreach (WeaponChainDefinition chain in weapon.Chains)
                {
                    foreach (WeaponStepDefinition step in chain.Steps)
                    {
                        Steps++;
                        bool clipMissing = string.IsNullOrEmpty(step.Clip) || !clips.Contains(step.Clip);
                        bool rootMissing = !string.IsNullOrEmpty(step.Root) && !clips.Contains(step.Root);
                        if (clipMissing || rootMissing) MissingStepClips++;
                    }
                }

                WeaponData data = LoadWeaponData(weapon);
                if (data == null)
                {
                    BuildIssues.Add($"Chưa build WD_{weapon.Key}");
                    continue;
                }
                WeaponsBuilt++;
                if (data.AnimSetId != index) BuildIssues.Add($"WD_{weapon.Key} còn AnimSetId {data.AnimSetId} (đúng là {index}) — build lại vũ khí");
                int builtSteps = data.Chains.Sum(c => c.Steps.Count);
                int defSteps = weapon.Chains.Sum(c => c.Steps.Count);
                if (data.Chains.Count != weapon.Chains.Count || builtSteps != defSteps)
                    BuildIssues.Add($"WD_{weapon.Key} lệch WeaponLibrary ({data.Chains.Count}/{weapon.Chains.Count} chuỗi, {builtSteps}/{defSteps} đòn) — build lại vũ khí");

                foreach (AttackStep step in data.Chains.SelectMany(c => c.Steps))
                {
                    BuiltSteps++;
                    if (step.HitTimes.Count == 0) StepsWithoutHits++;
                }
            }

            if (Weapons == 0 || Chains == 0) ComboState = State.Error;
            else if (MissingStepClips > 0) ComboState = State.Warn;
            else ComboState = State.Ok;

            if (BuiltSteps == 0) TimingState = State.None;
            else TimingState = StepsWithoutHits > 0 ? State.Warn : State.Ok;
        }

        private void ComputePrefab(AnimSetDefinition set, List<WeaponDefinition> weapons)
        {
            Prefab = set.Prefab;
            if (Prefab == null)
            {
                PrefabChecks.Add(new Check { State = State.Error, Label = "Prefab nhân vật", Detail = "chưa gán — kéo prefab vào ô phía trên" });
                PrefabState = State.Error;
                return;
            }

            Animator = Prefab.GetComponentInChildren<Animator>(true);
            if (Animator == null)
            {
                PrefabChecks.Add(new Check { State = State.Error, Label = "Animator", Detail = "prefab không có Animator nào" });
                PrefabState = State.Error;
                return;
            }

            GameObject host = Animator.gameObject;
            bool humanoid = Animator.avatar != null && Animator.avatar.isHuman;
            PrefabChecks.Add(new Check
            {
                State = humanoid ? State.Ok : State.Error,
                Label = "Avatar Humanoid",
                Detail = humanoid ? Animator.avatar.name : "anim dùng chung giữa các nhân vật cần rig Humanoid",
            });

            RuntimeAnimatorController expected = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
            bool controllerOk = Animator.runtimeAnimatorController != null &&
                                (Animator.runtimeAnimatorController == expected ||
                                 (Animator.runtimeAnimatorController is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController == expected));
            PrefabChecks.Add(new Check
            {
                State = controllerOk ? State.Ok : State.Error,
                Label = "Animator Controller",
                Detail = controllerOk ? Animator.runtimeAnimatorController.name : "phải dùng controller chung " + System.IO.Path.GetFileName(LocomotionAnimatorBuilder.ControllerPath),
                Fix = controllerOk ? FixKind.None : FixKind.AssignController,
                FixLabel = "Gán",
            });

            bool magic = weapons.Any(w => w.Type == WeaponType.Magic);
            bool melee = weapons.Count == 0 || weapons.Any(w => w.Type != WeaponType.Magic);
            AddComponentCheck(host, typeof(CharacterCombo), "chạy combo, giữ danh sách WeaponData", false);
            AddComponentCheck(host, typeof(WeaponHolder), "gắn model vũ khí vào tay", false);
            AddComponentCheck(host, typeof(AnimatorRootMotion), "đẩy root motion sang KCC (chân không trượt)", false);
            AddComponentCheck(host, typeof(PoseBlend), "giấu chỗ nối giữa các clip một lần", false);
            AddComponentCheck(host, typeof(CombatEffects), "hiệu ứng + âm thanh đòn đánh", false);
            AddComponentCheck(host, typeof(AudioSource), "nguồn phát SFX", false);
            if (melee) AddComponentCheck(host, typeof(MeleeHitDetector), "quét lưỡi vũ khí trúng mục tiêu", false);
            if (magic) AddComponentCheck(host, typeof(SpellCaster), "vũ khí phép: bắn đạn phép thay vì quét lưỡi", false);
            AddComponentCheck(host, typeof(ComboCameraFeedback), "rung camera / hitstop khi trúng", true);
            AddComponentCheck(host, typeof(PlayerIK), "IK bàn chân / nhìn mục tiêu", true);

            // phần điều khiển (KCC) thường nằm ở prefab bọc ngoài (Player / Character)
            bool hasController = Prefab.GetComponentInChildren<CharController>(true) != null;
            PrefabChecks.Add(new Check
            {
                State = hasController ? State.Ok : State.None,
                Label = "CharController (KCC)",
                Detail = hasController ? "có trong prefab" : "không có — prefab chỉ là model, sẽ được đặt vào trong Player/Character khi chơi",
                Optional = true,
            });

            CharacterCombo combo = host.GetComponent<CharacterCombo>();
            if (combo != null)
            {
                List<WeaponData> datas = weapons.Select(LoadWeaponData).Where(d => d != null).ToList();
                int assigned = datas.Count(d => combo.Weapons.Contains(d));
                bool weaponsOk = datas.Count > 0 && assigned == datas.Count;
                PrefabChecks.Add(new Check
                {
                    State = datas.Count == 0 ? State.Warn : weaponsOk ? State.Ok : State.Warn,
                    Label = "Vũ khí trong CharacterCombo",
                    Detail = datas.Count == 0 ? "chưa build WeaponData nào để gán" : $"{assigned}/{datas.Count} WeaponData của nhân vật đã gán",
                    Fix = datas.Count > 0 && !weaponsOk ? FixKind.AssignWeapons : FixKind.None,
                    FixLabel = "Gán tất cả",
                });
            }

            PrefabState = Worst(PrefabChecks.Where(c => !c.Optional).Select(c => c.State).ToArray());
        }

        private void AddComponentCheck(GameObject host, Type type, string purpose, bool optional)
        {
            bool has = host.GetComponent(type) != null;
            PrefabChecks.Add(new Check
            {
                State = has ? State.Ok : optional ? State.None : State.Warn,
                Label = type.Name,
                Detail = (has ? "" : optional ? "tuỳ chọn · " : "thiếu · ") + purpose,
                Fix = has ? FixKind.None : FixKind.AddComponent,
                Component = type,
                FixLabel = "Thêm",
                Optional = optional,
            });
        }

        private void ComputeBuild()
        {
            if (!PrefixGenerated) BuildIssues.Insert(0, "Prefix chưa sinh vào LocoSetPrefixes (runtime chưa biết bộ anim này)");
            else if (!PrefixCountMatches) BuildIssues.Insert(0, "Số prefix runtime khác số bộ trong AnimSetLibrary — sinh lại prefix");
            if (ControllerStates == 0) BuildIssues.Insert(0, "Layer Locomotion chưa có state của bộ này — Build Locomotion");

            if (!PrefixGenerated || ControllerStates == 0 || (Weapons > 0 && WeaponsBuilt < Weapons)) BuildState = State.Error;
            else if (BuildIssues.Count > 0) BuildState = State.Warn;
            else BuildState = Weapons > 0 ? State.Ok : State.Warn;
        }
    }

    #region Thư mục bị dời

    /// <summary>
    /// Pack anim hay bị move sang thư mục gốc khác (Assets/Dante -> Assets/_Animation_Dante).
    /// Giữ phần đuôi, thử từng thư mục cấp 1 trong Assets; ưu tiên thư mục có chứa tên cũ. Không thấy -> null.
    /// </summary>
    private static string FindMovedFolder(string folder)
    {
        folder = AnimSetCloneTool.NormalizeFolder(folder);
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return null;
        string[] parts = folder.Split('/');
        if (parts.Length < 3 || parts[0] != "Assets") return null;

        string oldRoot = parts[1];
        string tail = string.Join("/", parts, 2, parts.Length - 2);
        List<string> matches = AssetDatabase.GetSubFolders("Assets")
            .Where(root => AssetDatabase.IsValidFolder(root + "/" + tail))
            .Select(root => root + "/" + tail)
            .ToList();
        if (matches.Count == 0) return null;
        return matches.FirstOrDefault(path => path.Split('/')[1].IndexOf(oldRoot, StringComparison.OrdinalIgnoreCase) >= 0) ?? matches[0];
    }

    private static bool IsBroken(string folder) => !string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(AnimSetCloneTool.NormalizeFolder(folder));

    /// <summary>Số thư mục không tồn tại trong cả 2 library (mọi nhân vật).</summary>
    private int CountBrokenFolders()
    {
        int count = 0;
        if (_sets != null)
        {
            foreach (AnimSetDefinition set in _sets.Sets)
            {
                if (IsBroken(set.MovementFolder)) count++;
                count += set.ExtraFolders.Count(IsBroken);
            }
        }
        if (_weaponLibrary != null) count += _weaponLibrary.Weapons.Count(w => IsBroken(w.AnimFolder));
        return count;
    }

    /// <summary>Sửa mọi đường dẫn tìm được chỗ mới. Trả về (đã sửa, còn hỏng).</summary>
    private (int fixedCount, int left) FixMovedFolders()
    {
        int fixedCount = 0, left = 0;
        string Remap(string folder)
        {
            if (!IsBroken(folder)) return folder;
            string moved = FindMovedFolder(folder);
            if (moved == null) { left++; return folder; }
            fixedCount++;
            return moved;
        }

        if (_sets != null)
        {
            Undo.RecordObject(_sets, "Sửa đường dẫn bộ anim");
            foreach (AnimSetDefinition set in _sets.Sets)
            {
                set.MovementFolder = Remap(set.MovementFolder);
                for (int i = 0; i < set.ExtraFolders.Count; i++) set.ExtraFolders[i] = Remap(set.ExtraFolders[i]);
                // sửa xong có thể trùng thư mục chính / trùng nhau
                List<string> extras = set.ExtraFolders.Distinct().Where(f => f != set.MovementFolder).ToList();
                set.ExtraFolders.Clear();
                set.ExtraFolders.AddRange(extras);
            }
            EditorUtility.SetDirty(_sets);
        }
        if (_weaponLibrary != null)
        {
            Undo.RecordObject(_weaponLibrary, "Sửa đường dẫn vũ khí");
            foreach (WeaponDefinition weapon in _weaponLibrary.Weapons) weapon.AnimFolder = Remap(weapon.AnimFolder);
            EditorUtility.SetDirty(_weaponLibrary);
        }
        AssetDatabase.SaveAssets();
        AnimSetCloneTool.ClearCache();
        Invalidate();
        return (fixedCount, left);
    }

    /// <summary>Card cảnh báo + nút sửa, hiện ở trang nào cũng được khi library có thư mục hỏng.</summary>
    private void DrawBrokenFoldersCard()
    {
        int broken = CountBrokenFolders();
        if (broken == 0) return;
        StudioGUI.BeginCard(State.Error, $"{broken} đường dẫn thư mục anim không tồn tại",
            "Thường do pack anim đã bị move (vd Assets/Dante → Assets/_Animation_Dante). Studio tìm thư mục cùng phần đuôi dưới Assets và sửa cho mọi nhân vật + vũ khí.");
        if (StudioGUI.Primary("Tự sửa đường dẫn", null, 160f))
        {
            (int fixedCount, int left) = FixMovedFolders();
            SetStatus(left == 0 ? $"Đã sửa {fixedCount} đường dẫn." : $"Đã sửa {fixedCount}, còn {left} đường dẫn không tìm được — chọn tay ở ô thư mục.",
                left == 0 ? State.Ok : State.Warn);
        }
        StudioGUI.EndCard();
    }

    #endregion

    private static IEnumerable<string> AllClips(AnimSetDefinition set)
    {
        foreach (LocoClipDefinition row in set.OneShots) yield return row.Clip;
        foreach (GaitClipDefinition row in set.Gaits) yield return row.Clip;
        foreach (DirectionalClipDefinition row in set.Strafe) yield return row.Clip;
        foreach (DirectionalClipDefinition row in set.Crouch) yield return row.Clip;
    }
}
