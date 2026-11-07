using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tạo dữ liệu combo cho từng vũ khí từ gói anim Grruzam (Katana, Great Sword) và dựng layer
/// tấn công trong Animator của Dante.
///
/// Mỗi clip được phân tích bằng cách phát thử trên model gốc của gói (có xương vũ khí):
///   - Tốc độ mũi kiếm theo thời gian -> frame chém (đỉnh tốc độ), mốc nối đòn (hết vung),
///     mốc hủy hồi chiêu (kiếm đã dừng hẳn).
///   - Đường đi RootT của bản anim có di chuyển -> bước lướt / bật nhảy cho KCC.
///   - Xương ik_hand_gun (chỗ gắn kiếm) so với bàn tay -> cách cầm kiếm đổi theo động tác,
///     quy đổi sang bàn tay của Dante bằng cách phát cùng clip trên Dante.
/// Chạy lại menu sẽ phân tích lại nhưng giữ Speed / FadeIn / RootMotionScale đã chỉnh tay.
/// </summary>
public static class WeaponComboBuilder
{
    private const string PackRoot = "Assets/Grruzam Powerful Sword Animation(Great Sword, Katana)/";
    private const string GrrrruModel = PackRoot + "Modeling/Modeling_T-Pose_Grrrru_Man(recommend).FBX";
    private const string GunBone = "ik_hand_gun";
    private const string OutputFolder = "Assets/_Asset/Data/Weapons";
    private const string WeaponModelFolder = "Assets/_Asset/Prefab/Weapons";
    public const string ControllerPath = "Assets/_Asset/Animation/Player/Dante 1.controller";
    public const string LayerName = "WeaponAttack";
    public const string LibraryPath = "Assets/_Asset/Data/WeaponLibrary.asset";
    private const string EmptyStateName = "Empty";
    private const float AnalysisRate = 60f;
    private const float SlamHeight = 0.3f;          // mũi kiếm thấp hơn độ cao này (m) mới tính chạm đất
    private const float SlamMinDownSpeed = 6f;      // đang lao xuống ít nhất bấy nhiêu m/s
    private const float SlamMinVerticalRatio = 0.65f; // loại nhát quét ngang / chém chéo đi sát đất
    private const float SlamMinDrop = 0.9f;         // rơi ít nhất bấy nhiêu m trong SlamDropWindow giây trước đó
    private const float SlamDropWindow = 0.3f;
    private const float SlamStopWindow = 0.1f;      // sau khi chạm, xét bấy nhiêu giây kế tiếp
    private const float SlamBounce = 0.2f;          // kiếm được nảy lên tối đa bấy nhiêu m
    private const float SlamMaxSpeedAfter = 0.4f;   // tốc độ còn lại so với lúc chạm
    private const float KickMinSpeed = 8f;          // tốc độ bàn chân so với hông (m/s)
    private const float KickMaxBelowHips = 0.3f;    // bàn chân phải nhấc lên, cách hông không quá bấy nhiêu m theo chiều dọc
    private const float KickMinReach = 0.55f;       // chân duỗi ra xa hông theo chiều ngang (co gối lúc nhảy không tính)
    private const float KickMinSpacing = 0.2f;      // 2 cú đá cách nhau ít nhất bấy nhiêu giây

    private class StepDef
    {
        public string Clip;     // tên clip phát (bản Inplace / ZeroHeight)
        public string Root;     // tên clip lấy root motion (bản có di chuyển), null = chính nó
        public bool Vertical;   // dùng root motion dọc (hất tung)
    }

    private class ChainDef
    {
        public string Name;
        public AttackContext Context;
        public List<StepDef> Steps = new List<StepDef>();
    }

    private class WeaponDef
    {
        public WeaponType Type;
        public string Key;
        public string DisplayName;
        public string AnimFolder;
        public string Prefab;   // kiếm gốc của gói anim, chỉ dùng để phân tích mũi kiếm
        public string Model;    // model Dante cầm trong game, null = dùng Prefab
        public string IdleClip;
        public float ComboResetTime;
        public int AnimSetId;
        public List<ChainDef> Chains = new List<ChainDef>();
    }

    [MenuItem("Tools/Combat/Build Weapon Combos")]
    public static void BuildAll()
    {
        WeaponLibrary library = FindLibrary();
        if (library == null)
        {
            Debug.LogError("[WeaponComboBuilder] Chưa có WeaponLibrary. Mở Tools/Combat/Timing Window > tab 'Bộ anim & Vũ khí' > Tạo từ code.");
            return;
        }

        List<WeaponData> built = new List<WeaponData>();
        try
        {
            foreach (WeaponDef def in Definitions(library))
            {
                built.Add(BuildWeapon(def));
            }
            SyncAnimator(built);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponComboBuilder] Xong {built.Count} vũ khí: " + string.Join(", ", built.Select(w => $"{w.DisplayName} ({w.Chains.Count} chuỗi)")));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }
    }

    // chỉ cập nhật SlamTime và KickTimes, không đụng Animator và các mốc đã chỉnh tay
    [MenuItem("Tools/Combat/Bake Slam And Kick Times")]
    public static void BakeSlamAndKickTimes()
    {
        WeaponLibrary library = FindLibrary();
        if (library == null)
        {
            Debug.LogError("[WeaponComboBuilder] Chưa có WeaponLibrary. Mở Tools/Combat/Timing Window > tab 'Bộ anim & Vũ khí' > Tạo từ code.");
            return;
        }

        try
        {
            foreach (WeaponDef def in Definitions(library))
            {
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{OutputFolder}/WD_{def.Key}.asset");
                if (data == null)
                {
                    Debug.LogWarning($"[WeaponComboBuilder] Chưa có WD_{def.Key}, chạy Build Weapon Combos trước");
                    continue;
                }

                Dictionary<string, AnimationClip> clips = IndexClips(def.AnimFolder);
                List<string> slams = new List<string>();
                List<string> kicks = new List<string>();
                using (Rig rig = new Rig(string.IsNullOrEmpty(def.Prefab) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab)))
                {
                    foreach (ChainDef chainDef in def.Chains)
                    {
                        ComboChain chain = data.Chains.Find(c => c.Name == chainDef.Name);
                        if (chain == null) continue;
                        for (int i = 0; i < chainDef.Steps.Count && i < chain.Steps.Count; i++)
                        {
                            AttackStep step = chain.Steps[i];
                            StepDef stepDef = chainDef.Steps[i];
                            AnimationClip rootClip = step.Clip;
                            if (stepDef.Root != null && clips.TryGetValue(stepDef.Root, out AnimationClip found)) rootClip = found;
                            AnimationClip gripClip = HasGunCurve(step.Clip) ? step.Clip : (HasGunCurve(rootClip) ? rootClip : null);
                            step.SlamTime = chain.Context == AttackContext.Air || chain.Context == AttackContext.Launcher
                                ? -1f
                                : FindSlamTime(SampleTips(rig, step.Clip, gripClip), step.Clip.length);
                            if (step.SlamTime >= 0f) slams.Add($"{step.Name}@{step.SlamTime:F2}");
                            step.KickTimes = FindKickTimes(rig, step.Clip);
                            if (step.KickTimes.Count > 0) kicks.Add($"{step.Name}@{string.Join("/", step.KickTimes.Select(k => k.ToString("F2")))}");
                        }
                    }
                }
                EditorUtility.SetDirty(data);
                Debug.Log($"[WeaponComboBuilder] {def.DisplayName} đòn đập đất: {(slams.Count > 0 ? string.Join(", ", slams) : "không có")}");
                Debug.Log($"[WeaponComboBuilder] {def.DisplayName} đòn đá: {(kicks.Count > 0 ? string.Join(", ", kicks) : "không có")}");
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }
    }

    // đo so với hông để bước chân/lướt thân không bị tính là đá; lấy đỉnh tốc độ của bàn chân đang nhấc cao
    private static List<float> FindKickTimes(Rig rig, AnimationClip clip)
    {
        int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * AnalysisRate) + 1);
        Vector3[][] feet = new Vector3[2][] { new Vector3[frames], new Vector3[frames] };
        for (int f = 0; f < frames; f++)
        {
            rig.Sample(clip, Mathf.Min(f / AnalysisRate, clip.length), null);
            rig.FootOffsets(out feet[0][f], out feet[1][f]);
        }

        List<int> peaks = new List<int>();
        int ignore = Mathf.Max(3, Mathf.RoundToInt(frames * 0.08f));
        int tail = frames - Mathf.Max(3, Mathf.RoundToInt(frames * 0.1f));
        foreach (Vector3[] foot in feet)
        {
            for (int f = ignore; f < tail; f++)
            {
                float speed = SmoothedSpeed(foot, f);
                if (speed < KickMinSpeed || foot[f].y < -KickMaxBelowHips) continue;
                if (new Vector2(foot[f].x, foot[f].z).magnitude < KickMinReach) continue;
                if (speed < SmoothedSpeed(foot, f - 1) || speed < SmoothedSpeed(foot, f + 1)) continue;
                if (peaks.Any(p => Mathf.Abs(p - f) / AnalysisRate < KickMinSpacing)) continue;
                peaks.Add(f);
            }
        }
        peaks.Sort();
        return peaks.Select(f => Mathf.Min(f / AnalysisRate, clip.length) / clip.length).ToList();
    }

    private static float SmoothedSpeed(Vector3[] points, int frame)
    {
        int from = Mathf.Max(frame - 2, 0);
        int to = Mathf.Min(frame + 2, points.Length - 1);
        return (points[to] - points[from]).magnitude * AnalysisRate / Mathf.Max(to - from, 1);
    }

    private static Vector3[] SampleTips(Rig rig, AnimationClip clip, AnimationClip gripClip)
    {
        int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * AnalysisRate) + 1);
        Vector3[] tips = new Vector3[frames];
        for (int f = 0; f < frames; f++)
        {
            rig.Sample(clip, Mathf.Min(f / AnalysisRate, clip.length), gripClip);
            tips[f] = rig.TipLocal();
        }
        return tips;
    }

    private static float FindSlamTime(Vector3[] tips, float length)
    {
        int ignore = Mathf.RoundToInt(tips.Length * 0.08f); // đoạn đầu clip còn đang blend từ tư thế trước
        for (int f = Mathf.Max(1, ignore); f < tips.Length; f++)
        {
            float down = (tips[f - 1].y - tips[f].y) * AnalysisRate;
            float speed = (tips[f - 1] - tips[f]).magnitude * AnalysisRate;
            if (tips[f].y > SlamHeight || down < SlamMinDownSpeed || down < speed * SlamMinVerticalRatio) continue;
            if (!CameFromAbove(tips, f) || !StopsOnGround(tips, f, speed)) continue;
            return Mathf.Min(f / AnalysisRate, length) / length;
        }
        return -1f;
    }

    // phải là cú bổ từ trên xuống, không phải kiếm đang đi sát đất
    private static bool CameFromAbove(Vector3[] tips, int frame)
    {
        int window = Mathf.RoundToInt(SlamDropWindow * AnalysisRate);
        for (int f = Mathf.Max(0, frame - window); f < frame; f++)
        {
            if (tips[f].y - tips[frame].y >= SlamMinDrop) return true;
        }
        return false;
    }

    // đập thật thì kiếm khựng lại và nằm sát đất, chém xuyên qua thì vẫn đi tiếp hoặc vung lên ngay
    private static bool StopsOnGround(Vector3[] tips, int frame, float impactSpeed)
    {
        int window = Mathf.RoundToInt(SlamStopWindow * AnalysisRate);
        if (frame + window >= tips.Length) return false;
        for (int f = frame + 1; f <= frame + window; f++)
        {
            if (tips[f].y > SlamHeight + SlamBounce) return false;
        }
        float speedAfter = (tips[frame + window] - tips[frame + window - 1]).magnitude * AnalysisRate;
        return speedAfter <= impactSpeed * SlamMaxSpeedAfter;
    }

    // ------------------------------------------------------------------
    // Định nghĩa chuỗi combo theo thư mục của gói anim
    // ------------------------------------------------------------------

    private static IEnumerable<WeaponDef> Definitions()
    {
        WeaponDef katana = new WeaponDef
        {
            Type = WeaponType.Rebellion,
            Key = "Rebellion",
            DisplayName = "Rebellion",
            AnimFolder = PackRoot + "Animation/M_Katana_Blade/",
            Prefab = PackRoot + "Prefebs/Modeling_Weapon_katana_Blade.prefab",
            Model = WeaponModelFolder + "/Rebellion.prefab",
            IdleClip = "Idle_ver_B",
            ComboResetTime = 0.5f,
        };
        katana.Chains.Add(Combo("3 Combo", "Attack_3Combo_", 3));
        katana.Chains.Add(Combo("4 Combo", "Attack_4Combo_", 4));
        katana.Chains.Add(Combo("5 Combo", "Attack_5Combo_", 5));
        katana.Chains.Add(Single("Dash Attack A", AttackContext.Dash, "Dash_Attack_ver_A"));
        katana.Chains.Add(Single("Dash Attack B", AttackContext.Dash, "Dash_Attack_ver_B"));
        katana.Chains.Add(Air("Air Combo", "Jump_Attack_Combo_", 6));
        katana.Chains.Add(Launcher());
        foreach (string s in new[] { "A", "B", "C", "D", "E", "F", "G", "I" })
        {
            katana.Chains.Add(Single("Skill " + s, AttackContext.Skill, "Skill_" + s));
        }
        katana.Chains.Add(Chain("Skill H", AttackContext.Skill, "Skill_H", "Skill_H_return"));
        yield return katana;

        WeaponDef greatSword = new WeaponDef
        {
            Type = WeaponType.Sparda,
            Key = "Sparda",
            DisplayName = "Sparda",
            AnimFolder = PackRoot + "Animation/M_Big_Sword/",
            Prefab = PackRoot + "Prefebs/Modeling_Weapon_Big_Sword.prefab",
            Model = WeaponModelFolder + "/Sparda.prefab",
            IdleClip = "Idle",
            ComboResetTime = 0.6f,
        };
        greatSword.Chains.Add(Combo("3 Combo", "Attack_3Combo_", 3));
        greatSword.Chains.Add(ComboNamed("4 Combo A", "Attack_4Combo_", "1A", "2", "3", "4"));
        greatSword.Chains.Add(ComboNamed("4 Combo B", "Attack_4Combo_", "1B", "2", "3", "4"));
        greatSword.Chains.Add(Combo("7 Combo", "Attack_7Combo_", 7));
        greatSword.Chains.Add(Single("Dash Attack A", AttackContext.Dash, "Dash_Attack_ver_A"));
        greatSword.Chains.Add(Single("Dash Attack B", AttackContext.Dash, "Dash_Attack_ver_B"));
        greatSword.Chains.Add(Air("Air Combo", "Jump_Attack_Combo_", 3));
        greatSword.Chains.Add(Launcher());
        foreach (string s in new[] { "A", "B", "C", "D", "E", "F", "H", "I", "K", "L" })
        {
            greatSword.Chains.Add(Single("Skill " + s, AttackContext.Skill, "Skill_" + s));
        }
        greatSword.Chains.Add(Chain("Skill G", AttackContext.Skill, "Skill_G_1", "Skill_G_2"));
        ChainDef skillJ = new ChainDef { Name = "Skill J", Context = AttackContext.Skill };
        skillJ.Steps.Add(new StepDef { Clip = "Skill_J_Inplace", Root = "Skill_J" });
        greatSword.Chains.Add(skillJ);
        yield return greatSword;
    }

    // ------------------------------------------------------------------
    // Thư viện vũ khí: nguồn dữ liệu thật là asset WeaponLibrary.
    // Bảng hard-code ở trên chỉ dùng để Import lần đầu; thêm vũ khí mới = sửa asset,
    // không sửa code.
    // ------------------------------------------------------------------

    public static WeaponLibrary FindLibrary()
    {
        WeaponLibrary direct = AssetDatabase.LoadAssetAtPath<WeaponLibrary>(LibraryPath);
        if (direct != null) return direct;
        string[] guids = AssetDatabase.FindAssets("t:WeaponLibrary");
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<WeaponLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    [MenuItem("Tools/Combat/Import Weapon Library From Code")]
    public static WeaponLibrary CreateLibraryFromCode()
    {
        Directory.CreateDirectory(OutputFolder);
        WeaponLibrary existing = AssetDatabase.LoadAssetAtPath<WeaponLibrary>(LibraryPath);
        if (existing != null) AssetDatabase.DeleteAsset(LibraryPath);

        WeaponLibrary library = ScriptableObject.CreateInstance<WeaponLibrary>();
        int animSetId = 0;
        foreach (WeaponDef def in Definitions())
        {
            WeaponDefinition definition = new WeaponDefinition
            {
                Type = def.Type,
                Key = def.Key,
                DisplayName = def.DisplayName,
                AnimFolder = def.AnimFolder,
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab),
                Model = def.Model != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Model) : null,
                IdleClip = def.IdleClip,
                ComboResetTime = def.ComboResetTime,
                AnimSetId = animSetId++,
            };
            foreach (ChainDef chain in def.Chains)
            {
                WeaponChainDefinition chainDefinition = new WeaponChainDefinition { Name = chain.Name, Context = chain.Context };
                foreach (StepDef step in chain.Steps)
                {
                    chainDefinition.Steps.Add(new WeaponStepDefinition { Clip = step.Clip, Root = step.Root, Vertical = step.Vertical });
                }
                definition.Chains.Add(chainDefinition);
            }
            library.Weapons.Add(definition);
        }

        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponComboBuilder] Đã tạo " + LibraryPath + " từ bảng trong code.");
        return library;
    }

    private static IEnumerable<WeaponDef> Definitions(WeaponLibrary library)
    {
        foreach (WeaponDefinition source in library.Weapons)
        {
            if (string.IsNullOrEmpty(source.Key) || string.IsNullOrEmpty(source.AnimFolder))
            {
                Debug.LogError($"[WeaponComboBuilder] Vũ khí '{source.DisplayName}' thiếu Key hoặc AnimFolder — bỏ qua.");
                continue;
            }

            WeaponDef def = new WeaponDef
            {
                Type = source.Type,
                Key = source.Key,
                DisplayName = source.DisplayName,
                AnimFolder = source.AnimFolder,
                Prefab = source.Prefab != null ? AssetDatabase.GetAssetPath(source.Prefab) : null,
                Model = source.Model != null ? AssetDatabase.GetAssetPath(source.Model) : null,
                IdleClip = source.IdleClip,
                ComboResetTime = source.ComboResetTime,
                AnimSetId = source.AnimSetId,
            };
            foreach (WeaponChainDefinition chain in source.Chains)
            {
                ChainDef chainDef = new ChainDef { Name = chain.Name, Context = chain.Context };
                foreach (WeaponStepDefinition step in chain.Steps)
                {
                    chainDef.Steps.Add(new StepDef { Clip = step.Clip, Root = string.IsNullOrEmpty(step.Root) ? null : step.Root, Vertical = step.Vertical });
                }
                def.Chains.Add(chainDef);
            }
            yield return def;
        }
    }

    private static ChainDef Combo(string name, string prefix, int count)
    {
        return ComboNamed(name, prefix, Enumerable.Range(1, count).Select(i => i.ToString()).ToArray());
    }
    private static ChainDef ComboNamed(string name, string prefix, params string[] parts)
    {
        ChainDef chain = new ChainDef { Name = name, Context = AttackContext.Ground };
        foreach (string part in parts)
        {
            chain.Steps.Add(new StepDef { Clip = prefix + part + "_Inplace", Root = prefix + part });
        }
        return chain;
    }

    private static ChainDef Air(string name, string prefix, int count)
    {
        ChainDef chain = new ChainDef { Name = name, Context = AttackContext.Air };
        for (int i = 1; i <= count; i++)
        {
            chain.Steps.Add(new StepDef { Clip = prefix + i + "_ZeroHeight", Root = prefix + i });
        }
        return chain;
    }

    private static ChainDef Launcher()
    {
        ChainDef chain = new ChainDef { Name = "Launcher", Context = AttackContext.Launcher };
        chain.Steps.Add(new StepDef { Clip = "UpperAttack_ZeroHeight", Root = "UpperAttack", Vertical = true });
        return chain;
    }

    private static ChainDef Single(string name, AttackContext context, string clip)
    {
        return Chain(name, context, clip);
    }

    private static ChainDef Chain(string name, AttackContext context, params string[] clips)
    {
        ChainDef chain = new ChainDef { Name = name, Context = context };
        foreach (string clip in clips)
        {
            chain.Steps.Add(new StepDef { Clip = clip });
        }
        return chain;
    }
    // ------------------------------------------------------------------
    // Build 1 vũ khí
    // ------------------------------------------------------------------

    private static WeaponData BuildWeapon(WeaponDef def)
    {
        Directory.CreateDirectory(OutputFolder);
        string assetPath = $"{OutputFolder}/WD_{def.Key}.asset";
        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(assetPath);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<WeaponData>();

        // Giữ lại phần chỉnh tay của lần build trước
        Dictionary<string, AttackStep> previous = new Dictionary<string, AttackStep>();
        foreach (ComboChain chain in data.Chains)
        {
            foreach (AttackStep step in chain.Steps)
            {
                if (!string.IsNullOrEmpty(step.StateName)) previous[step.StateName] = step;
            }
        }

        Dictionary<string, AnimationClip> clips = IndexClips(def.AnimFolder);
        if (BakeHeightIntoPose(def, clips)) clips = IndexClips(def.AnimFolder);
        GameObject weaponPrefab = string.IsNullOrEmpty(def.Prefab) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab);

        using (Rig rig = new Rig(weaponPrefab))
        {
            data.Type = def.Type;
            data.DisplayName = def.DisplayName;
            GameObject model = def.Model != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Model) : null;
            data.WeaponPrefab = model != null ? model : weaponPrefab;
            data.ComboResetTime = def.ComboResetTime;
            data.AnimSetId = def.AnimSetId;

            if (clips.TryGetValue(def.IdleClip, out AnimationClip idle))
            {
                rig.Sample(idle, 0f, idle);
                rig.GetGrip(out data.IdleGripPosition, out data.IdleGripRotation);
            }

            data.Chains = new List<ComboChain>();
            float maxSwing = 0.01f;
            Dictionary<AttackStep, float> swing = new Dictionary<AttackStep, float>();
            int total = def.Chains.Sum(c => c.Steps.Count), done = 0;

            foreach (ChainDef chainDef in def.Chains)
            {
                ComboChain chain = new ComboChain { Name = chainDef.Name, Context = chainDef.Context };
                for (int i = 0; i < chainDef.Steps.Count; i++)
                {
                    StepDef stepDef = chainDef.Steps[i];
                    EditorUtility.DisplayProgressBar("Build Weapon Combos", $"{def.DisplayName}: {stepDef.Clip}", (float)done++ / total);

                    if (!clips.TryGetValue(stepDef.Clip, out AnimationClip clip))
                    {
                        Debug.LogWarning($"[WeaponComboBuilder] Không tìm thấy clip '{stepDef.Clip}' trong {def.AnimFolder}");
                        continue;
                    }
                    AnimationClip rootClip = clip;
                    if (stepDef.Root != null && !clips.TryGetValue(stepDef.Root, out rootClip)) rootClip = clip;

                    string stateName = $"{def.Key}_{Sanitize(chainDef.Name)}_{i + 1}";
                    AttackStep step = new AttackStep
                    {
                        Name = $"{chainDef.Name} #{i + 1}",
                        Clip = clip,
                        StateName = stateName,
                        FadeIn = i == 0 ? 0.1f : 0.14f,
                        UseVerticalRootMotion = stepDef.Vertical,
                        IsFinisher = i == chainDef.Steps.Count - 1,
                    };
                    if (previous.TryGetValue(stateName, out AttackStep old))
                    {
                        step.Speed = old.Speed;
                        step.FadeIn = old.FadeIn;
                        step.RootMotionScale = old.RootMotionScale;
                    }

                    swing[step] = Analyze(rig, step, clip, rootClip);
                    if (chainDef.Context == AttackContext.Air || chainDef.Context == AttackContext.Launcher) step.SlamTime = -1f;
                    maxSwing = Mathf.Max(maxSwing, swing[step]);
                    chain.Steps.Add(step);
                }
                for (int i = 0; i + 1 < chain.Steps.Count; i++)
                {
                    chain.Steps[i].NextSeamTime = FindSeam(chain.Steps[i], chain.Steps[i + 1]);
                }
                if (chain.Steps.Count > 0) data.Chains.Add(chain);
            }

            foreach (var pair in swing)
            {
                pair.Key.Weight = Mathf.Clamp01(pair.Value / maxSwing);
            }
        }

        if (isNew) AssetDatabase.CreateAsset(data, assetPath);
        EditorUtility.SetDirty(data);
        return data;
    }

    private static Dictionary<string, AnimationClip> IndexClips(string folder)
    {
        Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !result.ContainsKey(clip.name))
                {
                    result[clip.name] = clip;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Dante không dùng root motion (applyRootMotion = false) nên phần lên/xuống chưa bake bị Unity bỏ:
    /// đòn nhảy mất cú nhảy, đòn bắt đầu trên không bị kẹt ở độ cao đầu clip và đáp đất giữa không trung.
    /// Đòn trên đất -> bake độ cao vào pose để thân nhảy lên / đáp xuống đúng anim. Air / Launcher để KCC lo độ cao.
    /// </summary>
    /// <returns>true nếu có clip vừa được import lại.</returns>
    private static bool BakeHeightIntoPose(WeaponDef def, Dictionary<string, AnimationClip> clips)
    {
        // Lấy đường dẫn trước: import lại 1 FBX làm các clip đã nạp bị hủy
        List<(string path, string name)> targets = new List<(string, string)>();
        foreach (ChainDef chain in def.Chains)
        {
            if (chain.Context == AttackContext.Air || chain.Context == AttackContext.Launcher) continue;
            foreach (StepDef stepDef in chain.Steps)
            {
                if (clips.TryGetValue(stepDef.Clip, out AnimationClip clip)) targets.Add((AssetDatabase.GetAssetPath(clip), clip.name));
            }
        }

        bool reimported = false;
        foreach ((string path, string name) in targets)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] anims = importer.clipAnimations;
            if (anims.Length == 0) anims = importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation anim in anims)
            {
                if (anim.name != name || anim.lockRootHeightY) continue;
                anim.lockRootHeightY = true;
                changed = true;
            }
            if (!changed) continue;

            importer.clipAnimations = anims;
            importer.SaveAndReimport();
            reimported = true;
        }
        return reimported;
    }

    private static string Sanitize(string name)
    {
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }

    // ------------------------------------------------------------------
    // Phân tích 1 clip
    // ------------------------------------------------------------------

    /// <returns>Tốc độ vung lớn nhất của mũi kiếm (m/s) để chuẩn hoá Weight.</returns>
    private static float Analyze(Rig rig, AttackStep step, AnimationClip clip, AnimationClip rootClip)
    {
        float length = clip.length;
        int frames = Mathf.Max(2, Mathf.CeilToInt(length * AnalysisRate) + 1);
        // Đổi chỉ số frame đã phân tích (lấy mẫu mỗi 1/AnalysisRate giây) sang normalized time đúng của clip
        float Norm(int f) => Mathf.Min(f / AnalysisRate, length) / length;
        int gripCount = Mathf.CeilToInt(length * WeaponData.RootSampleRate) + 1;
        step.GripPositions = new Vector3[gripCount];
        step.GripRotations = new Quaternion[gripCount];

        // Clip Inplace của vài đòn thiếu curve xương vũ khí -> lấy cách cầm từ bản root motion (cùng động tác)
        AnimationClip gripClip = HasGunCurve(clip) ? clip : (HasGunCurve(rootClip) ? rootClip : null);

        Vector3[] tips = SampleTips(rig, clip, gripClip);
        step.SlamTime = FindSlamTime(tips, length);
        step.KickTimes = FindKickTimes(rig, clip);

        for (int g = 0; g < gripCount; g++)
        {
            float t = Mathf.Min(g / WeaponData.RootSampleRate, length);
            rig.Sample(clip, t, gripClip);
            rig.GetGrip(out step.GripPositions[g], out step.GripRotations[g]);
        }

        // --- Tốc độ mũi kiếm -> mốc chém / nối đòn / hồi chiêu ---
        float[] speed = new float[frames];
        for (int f = 1; f < frames; f++)
        {
            speed[f] = (tips[f] - tips[f - 1]).magnitude * AnalysisRate;
        }
        speed[0] = speed.Length > 1 ? speed[1] : 0f;
        float[] smooth = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f; int n = 0;
            for (int k = -2; k <= 2; k++)
            {
                int i = f + k;
                if (i < 0 || i >= frames) continue;
                sum += speed[i]; n++;
            }
            smooth[f] = sum / n;
        }

        int ignore = Mathf.RoundToInt(frames * 0.04f); // vài frame đầu còn đang blend từ tư thế trước
        // ~10% cuối clip: kiếm thu về / key cuối nhảy -> đỉnh tốc độ giả, không phải nhát chém
        int tail = Mathf.Clamp(frames - 1 - Mathf.RoundToInt(frames * 0.1f), 1, frames - 1);
        int best = Mathf.Max(1, Mathf.Min(ignore, tail - 1));
        float max = 0f;
        for (int f = best; f < tail; f++)
        {
            if (smooth[f] > max) { max = smooth[f]; best = f; }
        }
        max = Mathf.Max(max, 0.01f);

        List<int> peaks = new List<int>();
        for (int f = Mathf.Max(1, ignore); f < tail; f++)
        {
            if (smooth[f] < max * 0.55f || smooth[f] < smooth[f - 1] || smooth[f] < smooth[f + 1]) continue;
            if (peaks.Count > 0 && (f - peaks[peaks.Count - 1]) / AnalysisRate < 0.15f)
            {
                if (smooth[f] > smooth[peaks[peaks.Count - 1]]) peaks[peaks.Count - 1] = f;
                continue;
            }
            peaks.Add(f);
        }
        if (peaks.Count == 0) peaks.Add(best);

        step.HitTimes = peaks.Select(Norm).ToList();

        int lastHit = peaks[peaks.Count - 1];
        int swingEnd = frames - 1;
        for (int f = lastHit; f < frames; f++)
        {
            if (smooth[f] < max * 0.3f) { swingEnd = f; break; }
        }
        int settle = frames - 1;
        for (int f = swingEnd; f < frames; f++)
        {
            if (smooth[f] < max * 0.12f) { settle = f; break; }
        }

        // Kẹp theo thứ tự max rồi min: mốc luôn nằm trong clip (Mathf.Clamp sẽ đẩy lên quá 1 nếu đòn chém sát cuối clip)
        float lastHitN = Norm(lastHit);
        step.LinkTime = Mathf.Min(Mathf.Max(Norm(swingEnd), lastHitN + 0.04f), 0.95f);
        step.MoveCancelTime = Mathf.Min(Mathf.Max(Norm(settle), step.LinkTime + 0.05f), 0.99f);

        // --- Root motion: đo dịch chuyển hông của bản anim có di chuyển ---
        BakeRoot(rig, step, clip, rootClip);
        step.PoseSamples = SamplePoses(rig, clip).SelectMany(p => p).ToArray();
        return max;
    }

    /// <summary>
    /// Clip combo của gói được cắt từ 1 combo liền: đòn kế bắt đầu đúng bằng 1 frame nằm trong đòn này
    /// (phần sau là đoạn thu thế). Tìm frame đó (từ ngay trước LinkTime tới cuối clip).
    /// </summary>
    /// <returns>Giây trong clip, -1 nếu không có frame nào đủ khớp.</returns>
    private static float FindSeam(AttackStep step, AttackStep next)
    {
        const float maxError = 0.08f;
        int bones = WeaponData.PoseBoneCount;
        int frames = step.PoseSamples.Length / bones;
        if (frames == 0 || next.PoseSamples.Length < bones) return -1f;

        int from = Mathf.Max(0, Mathf.RoundToInt(step.LinkTime * step.Length * WeaponData.RootSampleRate) - 3);
        int best = -1;
        float bestError = maxError;
        for (int f = from; f < frames; f++)
        {
            float sum = 0f;
            for (int i = 0; i < bones; i++) sum += (step.PoseSamples[f * bones + i] - next.PoseSamples[i]).sqrMagnitude;
            float error = Mathf.Sqrt(sum / bones);
            if (error < bestError) { bestError = error; best = f; }
        }
        return best < 0 ? -1f : best / WeaponData.RootSampleRate;
    }

    private static bool HasGunCurve(AnimationClip clip)
    {
        return clip != null && AnimationUtility.GetCurveBindings(clip).Any(b => b.path.EndsWith(GunBone) && b.propertyName.StartsWith("m_LocalRotation"));
    }

    /// <summary>
    /// Root motion = hông của bản anim có di chuyển trừ hông của bản Inplace/ZeroHeight cùng thời điểm,
    /// đo trong không gian model (+Z = phía trước nhân vật). Không đọc RootT/RootQ vì hướng gốc của
    /// RootQ lệch mỗi clip một góc (theo body orientation) -> lướt ngang/lùi.
    /// </summary>
    private static void BakeRoot(Rig rig, AttackStep step, AnimationClip clip, AnimationClip rootClip)
    {
        int count = Mathf.CeilToInt(clip.length * WeaponData.RootSampleRate) + 1;
        step.RootSamples = new Vector3[count];
        bool sameClip = rootClip == clip;
        // Không kéo giãn thời gian: bản root dài hơn/ngắn hơn thì dò đoạn trùng động tác rồi phát 1:1
        float offset = sameClip ? 0f : FindRootOffset(rig, clip, rootClip);
        Vector3 origin = Vector3.zero;
        float travel = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Min(i / WeaponData.RootSampleRate, clip.length);
            Vector3 body = rig.SampleHips(rootClip, Mathf.Clamp(t + offset, 0f, rootClip.length));
            // Lắc người / nhún giống nhau ở 2 bản -> trừ đi chỉ còn phần dịch chuyển gốc (kể cả độ nảy thật)
            if (!sameClip) body -= rig.SampleHips(clip, t);
            if (i == 0) origin = body;

            Vector3 d = body - origin;
            step.RootSamples[i] = new Vector3(d.x, sameClip ? 0f : d.y, d.z);
            travel = Mathf.Max(travel, new Vector2(d.x, d.z).magnitude);
        }
        step.RootTravel = travel;
        step.UsePlanarRootMotion = travel > 0.05f;
    }

    /// <summary>
    /// Độ lệch thời gian (giây) của bản root so với bản phát: so tư thế tay/chân/đầu theo hông
    /// (không phụ thuộc root motion) ở mọi độ lệch, lấy độ lệch khớp nhất.
    /// </summary>
    private static float FindRootOffset(Rig rig, AnimationClip clip, AnimationClip rootClip)
    {
        const float step = 1f / WeaponData.RootSampleRate;
        float range = rootClip.length - clip.length;
        if (Mathf.Abs(range) < step) return 0f;

        Vector3[][] a = SamplePoses(rig, clip);
        Vector3[][] b = SamplePoses(rig, rootClip);
        int minOff = Mathf.FloorToInt(Mathf.Min(0f, range) / step);
        int maxOff = Mathf.CeilToInt(Mathf.Max(0f, range) / step);
        int bestOff = 0;
        float bestCost = float.MaxValue;

        for (int off = minOff; off <= maxOff; off++)
        {
            float cost = 0f;
            int n = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int j = i + off;
                if (j < 0 || j >= b.Length) continue;
                for (int k = 0; k < a[i].Length; k++) cost += (a[i][k] - b[j][k]).sqrMagnitude;
                n++;
            }
            if (n < a.Length * 0.9f) continue;
            cost /= n;
            if (cost < bestCost) { bestCost = cost; bestOff = off; }
        }
        return bestOff * step;
    }

    private static Vector3[][] SamplePoses(Rig rig, AnimationClip clip)
    {
        int count = Mathf.CeilToInt(clip.length * WeaponData.RootSampleRate) + 1;
        Vector3[][] poses = new Vector3[count][];
        for (int i = 0; i < count; i++)
        {
            poses[i] = rig.SamplePose(clip, Mathf.Min(i / WeaponData.RootSampleRate, clip.length));
        }
        return poses;
    }

    // ------------------------------------------------------------------
    // Rig phân tích: model gốc Grrrru (có xương vũ khí) + Dante, phát cùng clip
    // ------------------------------------------------------------------

    private class Rig : System.IDisposable
    {
        private readonly GameObject _grrrru;
        private readonly GameObject _dante;
        private readonly Transform _gHand;
        private readonly Transform _gGun;
        private readonly Transform _dHand;
        private readonly Transform _tip;
        private readonly Transform _gHips;
        private readonly Transform[] _poseBones;
        private readonly float _gScale;
        private readonly float _scaleRatio;

        public Rig(GameObject weaponPrefab)
        {
            _grrrru = Spawn(GrrrruModel, out Animator gAnim);
            _gHand = gAnim.GetBoneTransform(HumanBodyBones.RightHand);
            _gGun = _gHand.Find(GunBone);
            _gHips = gAnim.GetBoneTransform(HumanBodyBones.Hips);
            _gScale = Mathf.Max(gAnim.humanScale, 0.0001f);
            // Thứ tự phải khớp WeaponData.PoseBoneCount
            _poseBones = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Head }
                .Select(gAnim.GetBoneTransform).ToArray();

            // Dante trong scene: lấy Animator gán sẵn, không có thì lấy Animator cha của CharacterCombo
            CharacterCombo sceneCombo = Object.FindFirstObjectByType<CharacterCombo>();
            Animator sceneDante = sceneCombo != null
                ? (sceneCombo.animator != null ? sceneCombo.animator : sceneCombo.GetComponentInParent<Animator>())
                : null;
            string dantePath = sceneDante != null ? AssetDatabase.GetAssetPath(sceneDante.avatar) : null;
            _dante = Spawn(dantePath, out Animator dAnim, sceneDante != null ? sceneDante.avatar : null);
            _dHand = dAnim.GetBoneTransform(HumanBodyBones.RightHand);
            _scaleRatio = dAnim.humanScale / Mathf.Max(gAnim.humanScale, 0.0001f);

            // Mũi kiếm = điểm xa chỗ cầm nhất trên trục dài của mesh vũ khí
            _tip = new GameObject("Tip").transform;
            _tip.hideFlags = HideFlags.HideAndDontSave;
            _tip.SetParent(_gGun, false);
            MeshFilter mf = weaponPrefab != null ? weaponPrefab.GetComponentInChildren<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 axis = b.extents.x >= b.extents.y && b.extents.x >= b.extents.z ? Vector3.right
                             : b.extents.y >= b.extents.z ? Vector3.up : Vector3.forward;
                Vector3 a = b.center + Vector3.Scale(axis, b.extents);
                Vector3 c = b.center - Vector3.Scale(axis, b.extents);
                Vector3 far = a.sqrMagnitude >= c.sqrMagnitude ? a : c;
                Transform meshT = mf.transform;
                // Toạ độ tip theo gốc prefab (prefab được gắn với transform gốc dưới ik_hand_gun)
                Matrix4x4 meshToRoot = weaponPrefab.transform.worldToLocalMatrix * meshT.localToWorldMatrix;
                _tip.localPosition = meshToRoot.MultiplyPoint3x4(far);
            }
            else
            {
                _tip.localPosition = new Vector3(0f, 0f, 1f);
            }
        }

        private static GameObject Spawn(string modelPath, out Animator animator, Avatar avatarOverride = null)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject go = (GameObject)Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            Avatar avatar = avatarOverride;
            if (avatar == null)
            {
                avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            }
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            return go;
        }

        public void Sample(AnimationClip clip, float time, AnimationClip gripClip)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_grrrru, clip, time);
            // Xương vũ khí lấy từ clip có curve ik_hand_gun (thường là chính clip)
            if (gripClip != null && gripClip != clip)
            {
                float t = Mathf.Min(time, gripClip.length);
                AnimationMode.SampleAnimationClip(_grrrru, gripClip, t);
            }
            AnimationMode.SampleAnimationClip(_dante, clip, time);
            AnimationMode.EndSampling();
        }

        /// <summary>Vị trí hông Grrrru theo không gian model khi phát clip (chia humanScale, runtime nhân lại theo Dante).</summary>
        public Vector3 SampleHips(AnimationClip clip, float time)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_grrrru, clip, time);
            AnimationMode.EndSampling();
            return _grrrru.transform.InverseTransformPoint(_gHips.position) / _gScale;
        }

        /// <summary>Tư thế tay/chân/đầu theo không gian hông (bỏ qua root motion) để so 2 clip cùng động tác.</summary>
        public Vector3[] SamplePose(AnimationClip clip, float time)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_grrrru, clip, time);
            AnimationMode.EndSampling();
            Vector3[] pose = new Vector3[_poseBones.Length];
            for (int i = 0; i < _poseBones.Length; i++)
            {
                pose[i] = _poseBones[i] != null ? _gHips.InverseTransformPoint(_poseBones[i].position) / _gScale : Vector3.zero;
            }
            return pose;
        }

        /// <summary>Hai bàn chân so với hông theo không gian model (chia humanScale), không xoay theo hông.</summary>
        public void FootOffsets(out Vector3 left, out Vector3 right)
        {
            Vector3 hips = _grrrru.transform.InverseTransformPoint(_gHips.position);
            left = (_grrrru.transform.InverseTransformPoint(_poseBones[2].position) - hips) / _gScale;
            right = (_grrrru.transform.InverseTransformPoint(_poseBones[3].position) - hips) / _gScale;
        }

        public Vector3 TipLocal()
        {
            return _grrrru.transform.InverseTransformPoint(_tip.position);
        }

        /// <summary>Pose của xương vũ khí quy về không gian bàn tay phải của Dante.</summary>
        public void GetGrip(out Vector3 position, out Quaternion rotation)
        {
            Vector3 offset = (_gGun.position - _gHand.position) * _scaleRatio;
            position = _dHand.InverseTransformPoint(_dHand.position + offset);
            rotation = Quaternion.Inverse(_dHand.rotation) * _gGun.rotation;
        }

        public void Dispose()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (_tip != null) Object.DestroyImmediate(_tip.gameObject);
            if (_grrrru != null) Object.DestroyImmediate(_grrrru);
            if (_dante != null) Object.DestroyImmediate(_dante);
        }
    }

    // ------------------------------------------------------------------
    // Animator: layer WeaponAttack, 1 state cho mỗi đòn (code tự CrossFade, không cần transition)
    // ------------------------------------------------------------------

    private static void SyncAnimator(List<WeaponData> weapons)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[WeaponComboBuilder] Không thấy Animator Controller: " + ControllerPath);
            return;
        }

        // Giữ Write Defaults như layer tấn công đã dựng lần trước để tránh lệch pose khi blend
        bool writeDefaults = true;
        AnimatorControllerLayer legacy = controller.layers.FirstOrDefault(l => l.name == LayerName);
        if (legacy != null && legacy.stateMachine.defaultState != null)
        {
            writeDefaults = legacy.stateMachine.defaultState.writeDefaultValues;
        }

        List<AnimatorControllerLayer> layers = controller.layers.ToList();
        int index = layers.FindIndex(l => l.name == LayerName);
        if (index >= 0)
        {
            Object.DestroyImmediate(layers[index].stateMachine, true);
            layers.RemoveAt(index);
        }
        else
        {
            index = layers.FindIndex(l => l.name == "IK");
            if (index < 0) index = layers.Count;
        }

        AnimatorStateMachine sm = new AnimatorStateMachine
        {
            name = LayerName,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        AnimatorState empty = sm.AddState(EmptyStateName, new Vector3(30f, 0f, 0f));
        empty.writeDefaultValues = writeDefaults;
        sm.defaultState = empty;

        float x = 300f;
        foreach (WeaponData weapon in weapons)
        {
            float y = 0f;
            foreach (ComboChain chain in weapon.Chains)
            {
                float cx = x;
                foreach (AttackStep step in chain.Steps)
                {
                    AnimatorState state = sm.AddState(step.StateName, new Vector3(cx, y, 0f));
                    state.motion = step.Clip;
                    state.speed = step.Speed;
                    state.writeDefaultValues = writeDefaults;
                    state.iKOnFeet = false;
                    cx += 250f;
                }
                y += 60f;
            }
            x += 2200f;
        }

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = LayerName,
            stateMachine = sm,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            iKPass = false,
        };
        layers.Insert(index, layer);
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
    }
}
