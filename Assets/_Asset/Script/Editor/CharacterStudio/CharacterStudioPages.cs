using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using State = StudioGUI.State;

// Các trang tự vẽ của Character Studio: Tổng quan, Prefab, Moveset, Build.
public partial class CharacterStudioWindow
{
    #region Sửa giá trị có Undo

    private void Change<T>(UnityEngine.Object target, string undo, T oldValue, T newValue, Action<T> setter)
    {
        if (Equals(oldValue, newValue)) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
        Invalidate();
    }

    private void TextRow(UnityEngine.Object target, string label, string value, Action<string> setter, string tooltip = null)
    {
        Change(target, "Sửa " + label, value, EditorGUILayout.TextField(new GUIContent(label, tooltip), value), setter);
    }

    private void FolderRow(UnityEngine.Object target, string label, string value, Action<string> setter)
    {
        string result = StudioGUI.FolderField(label, value);
        if (result != value) AnimSetCloneTool.ClearCache();
        Change(target, "Sửa " + label, value, AnimSetCloneTool.NormalizeFolder(result), setter);
    }

    /// <summary>Nút hiện tên clip; bấm mở ô chọn có tìm kiếm. Chấm màu = clip có / thiếu trong thư mục.</summary>
    private void ClipButton(UnityEngine.Object target, string value, AnimSetCloneTool.ClipIndex clips, Action<string> setter, float minWidth = 160f)
    {
        bool empty = string.IsNullOrEmpty(value);
        bool found = !empty && clips != null && clips.Contains(value);
        StudioGUI.Dot(empty ? State.None : found ? State.Ok : State.Error, 7f);

        GUIStyle style = new GUIStyle(EditorStyles.popup)
        {
            normal = { textColor = empty ? StudioGUI.Muted : found ? StudioGUI.Text : StudioGUI.Error },
            fontStyle = empty ? FontStyle.Italic : FontStyle.Normal,
        };
        GUIContent content = new GUIContent(empty ? "— chọn clip —" : value, found || empty ? value : "Không thấy clip này trong thư mục anim");
        Rect rect = GUILayoutUtility.GetRect(content, style, GUILayout.MinWidth(minWidth), GUILayout.ExpandWidth(true));
        if (!GUI.Button(rect, content, style)) return;

        IList<string> names = clips != null ? (IList<string>)clips.Names : new List<string>();
        ClipDropdown.Show(rect, names, "Clip", picked =>
        {
            Change(target, "Chọn clip", value, picked, setter);
            Repaint();
        });
    }

    #endregion

    #region Tổng quan

    private void DrawOverviewPage()
    {
        AnimSetDefinition set = Set;
        Health health = CurrentHealth;
        List<WeaponDefinition> weapons = CharacterWeapons();

        StudioGUI.PageTitle("Tổng quan", "Tình trạng của nhân vật đang chọn. Làm lần lượt các bước, chấm xanh hết là chơi được.");
        DrawBrokenFoldersCard();

        // thẻ nhân vật
        StudioGUI.BeginCard();
        GUILayout.BeginHorizontal();
        Rect thumb = GUILayoutUtility.GetRect(112f, 112f, GUILayout.Width(112f), GUILayout.Height(112f));
        StudioGUI.Fill(thumb, StudioGUI.CardAlt);
        Texture preview = set.Prefab != null ? AssetPreview.GetAssetPreview(set.Prefab) : null;
        if (preview != null) GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit);
        else
        {
            GUI.Label(thumb, set.Prefab != null ? "đang tải…" : "chưa có\nprefab", new GUIStyle(StudioGUI.MutedStyle) { alignment = TextAnchor.MiddleCenter });
            if (set.Prefab != null && AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        GUILayout.Space(14f);
        GUILayout.BeginVertical();
        GUILayout.Label(set.DisplayName, StudioGUI.BigTitleStyle);
        GUILayout.BeginHorizontal();
        StudioGUI.Chip("prefix " + set.Prefix, StudioGUI.Accent, "Tiền tố state trong layer Locomotion");
        StudioGUI.Chip("AnimSetId " + _setIndex, StudioGUI.Muted, "Index bộ anim trong AnimSetLibrary = giá trị WeaponData.AnimSetId");
        foreach (WeaponDefinition weapon in weapons) StudioGUI.Chip(weapon.DisplayName, StudioGUI.Purple, "Vũ khí " + weapon.Key);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(string.IsNullOrEmpty(set.MovementFolder) ? "(chưa trỏ thư mục anim)" : set.MovementFolder, StudioGUI.MutedStyle);
        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        StudioGUI.Stat((health.Rows - health.MissingClips) + "/" + health.Rows, "clip moveset", health.MissingClips > 0 ? StudioGUI.Warn : StudioGUI.Ok);
        StudioGUI.Stat(health.Weapons.ToString(), "vũ khí", StudioGUI.Purple);
        StudioGUI.Stat(health.Chains.ToString(), "chuỗi combo", StudioGUI.Accent);
        StudioGUI.Stat(health.Steps.ToString(), "đòn", StudioGUI.Accent);
        StudioGUI.Stat(health.StepsWithoutHits.ToString(), "đòn thiếu mốc chém", health.StepsWithoutHits > 0 ? StudioGUI.Warn : StudioGUI.Muted);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        StudioGUI.EndCard();

        // checklist
        StudioGUI.BeginCard("Các bước set up");
        for (int i = 1; i < Nav.Length; i++)
        {
            NavItem item = Nav[i];
            if (StudioGUI.CheckRow(health.StateOf(item.Page), (i + 1) + ".  " + item.Label, StepSummary(item.Page, health), "Mở"))
            {
                _page = item.Page;
                _scroll = Vector2.zero;
            }
        }
        StudioGUI.EndCard();

        StudioGUI.BeginCard("Quản lý");
        GUILayout.BeginHorizontal();
        if (StudioGUI.Button("Nhân bản nhân vật này", "Tạo nhân vật mới lấy bộ này làm khuôn", 170f)) BeginNewCharacter(_setIndex);
        if (StudioGUI.Button("Chọn bộ anim trong asset", null, 170f)) Ping(_sets);
        GUILayout.FlexibleSpace();
        if (StudioGUI.Danger("Xoá nhân vật", "Xoá bộ anim khỏi AnimSetLibrary", 120f)) DeleteCharacter();
        GUILayout.EndHorizontal();
        StudioGUI.EndCard();
    }

    private string StepSummary(Page page, Health health)
    {
        switch (page)
        {
            case Page.Prefab:
                if (health.Prefab == null) return "chưa gán prefab";
                int missing = health.PrefabChecks.Count(c => !c.Optional && c.State != State.Ok);
                return missing == 0 ? health.Prefab.name + " — đủ component" : health.Prefab.name + $" — {missing} mục cần sửa";
            case Page.Moveset:
                return health.MissingClips == 0 ? $"{health.Rows} clip, đủ hết" : $"{health.MissingClips}/{health.Rows} dòng thiếu clip";
            case Page.Combo:
                return health.Weapons == 0 ? "chưa có vũ khí" : $"{health.Weapons} vũ khí, {health.Chains} chuỗi, {health.Steps} đòn" + (health.MissingStepClips > 0 ? $", {health.MissingStepClips} đòn sai clip" : "");
            case Page.Timing:
                return health.BuiltSteps == 0 ? "build vũ khí trước" : health.StepsWithoutHits == 0 ? "mọi đòn đã có mốc chém" : $"{health.StepsWithoutHits} đòn chưa có mốc chém";
            case Page.Grip:
                return health.WeaponsBuilt == 0 ? "build vũ khí trước" : "căn vũ khí trên tay, bake vào WeaponData";
            case Page.Sound:
                return "bộ SFX dùng chung (CombatEffects)";
            case Page.Build:
                return health.BuildIssues.Count == 0 ? "đã build, khớp dữ liệu" : health.BuildIssues[0];
            default:
                return string.Empty;
        }
    }

    private void DeleteCharacter()
    {
        AnimSetDefinition set = Set;
        if (set == null) return;
        List<WeaponDefinition> weapons = CharacterWeapons();
        int choice = EditorUtility.DisplayDialogComplex("Xoá " + set.DisplayName + "?",
            $"Xoá bộ anim {set.Prefix} (index {_setIndex}) khỏi AnimSetLibrary.\n" +
            $"Các bộ đứng sau sẽ lùi 1 index; AnimSetId của vũ khí được dời theo.\n\n" +
            (weapons.Count > 0 ? $"Xoá luôn {weapons.Count} vũ khí của nhân vật trong WeaponLibrary?" : "") +
            "\n\nSau đó cần Sinh prefix + Build lại.",
            weapons.Count > 0 ? "Xoá cả vũ khí" : "Xoá", "Huỷ", weapons.Count > 0 ? "Chỉ xoá bộ anim" : null);
        if (choice == 1) return;

        int index = _setIndex;
        Undo.RecordObject(_sets, "Xoá nhân vật");
        _sets.Sets.RemoveAt(index);
        EditorUtility.SetDirty(_sets);
        if (_weaponLibrary != null)
        {
            Undo.RecordObject(_weaponLibrary, "Xoá nhân vật");
            if (choice == 0) _weaponLibrary.Weapons.RemoveAll(w => w.AnimSetId == index);
            foreach (WeaponDefinition weapon in _weaponLibrary.Weapons)
            {
                if (weapon.AnimSetId > index) weapon.AnimSetId--;
            }
            EditorUtility.SetDirty(_weaponLibrary);
        }
        AssetDatabase.SaveAssets();
        Select(Mathf.Clamp(index - 1, 0, _sets.Sets.Count - 1));
        SetStatus("Đã xoá " + set.DisplayName + ". Vào trang Build: Sinh prefix → Build Locomotion → Build vũ khí.", State.Warn);
    }

    #endregion

    #region Prefab

    private void DrawPrefabPage()
    {
        AnimSetDefinition set = Set;
        Health health = CurrentHealth;
        StudioGUI.PageTitle("Prefab nhân vật", "Prefab chứa model + Animator. Studio kiểm tra component cần cho combat và sửa thẳng vào prefab.");

        StudioGUI.BeginCard();
        GUILayout.BeginHorizontal();
        GameObject prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "Prefab asset của nhân vật (không phải object trong scene)"), set.Prefab, typeof(GameObject), false);
        if (prefab != set.Prefab && (prefab == null || PrefabUtility.IsPartOfPrefabAsset(prefab)))
            Change(_sets, "Gán prefab nhân vật", set.Prefab, prefab, value => set.Prefab = value);
        GUI.enabled = set.Prefab != null;
        if (StudioGUI.Button("Mở prefab", null, 90f)) AssetDatabase.OpenAsset(set.Prefab);
        if (StudioGUI.Button("Chọn", null, 60f)) Ping(set.Prefab);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (set.Prefab == null)
        {
            GUILayout.Space(4f);
            StudioGUI.Note("Kéo prefab nhân vật vào ô trên (vd Vergil Variant). Prefab phải có Animator Humanoid.", State.Warn);
        }
        else if (health.Animator != null)
        {
            StudioGUI.Note("Animator: " + AnimationUtility.CalculateTransformPath(health.Animator.transform, set.Prefab.transform), State.None);
        }
        StudioGUI.EndCard();

        if (set.Prefab == null || health.Animator == null) return;

        StudioGUI.BeginCard(health.PrefabState, "Kiểm tra component", "Component combat nằm cùng object với Animator. Bấm Thêm / Gán để sửa thẳng vào prefab.");
        Check apply = null;
        foreach (Check check in health.PrefabChecks)
        {
            string action = check.Fix != FixKind.None ? check.FixLabel : null;
            if (StudioGUI.CheckRow(check.State, check.Label, check.Detail, action)) apply = check;
        }
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        bool anyFix = health.PrefabChecks.Any(c => c.Fix != FixKind.None && !c.Optional);
        GUI.enabled = anyFix;
        if (StudioGUI.Primary("Sửa tất cả mục bắt buộc", null, 200f)) ApplyPrefabFixes(health.PrefabChecks.Where(c => c.Fix != FixKind.None && !c.Optional).ToList());
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        StudioGUI.EndCard();

        if (apply != null) ApplyPrefabFixes(new List<Check> { apply });

        CharacterCombo combo = health.Animator.GetComponent<CharacterCombo>();
        if (combo != null)
        {
            StudioGUI.BeginCard("Vũ khí đang gán trong CharacterCombo", "Thứ tự trong danh sách = thứ tự đổi vũ khí lúc chơi.");
            if (combo.Weapons.Count == 0) StudioGUI.Note("(trống)");
            foreach (WeaponData data in combo.Weapons)
            {
                GUILayout.BeginHorizontal();
                StudioGUI.Dot(data != null ? State.Ok : State.Error);
                GUILayout.Label(data != null ? $"{data.DisplayName}   ·   AnimSetId {data.AnimSetId}" : "(trống)", StudioGUI.LabelStyle);
                GUILayout.FlexibleSpace();
                if (data != null && StudioGUI.Icon("◎", "Chọn WeaponData")) Ping(data);
                GUILayout.EndHorizontal();
            }
            StudioGUI.EndCard();
        }
    }

    private void ApplyPrefabFixes(List<Check> checks)
    {
        AnimSetDefinition set = Set;
        if (set == null || set.Prefab == null || checks.Count == 0) return;
        if (Application.isPlaying)
        {
            SetStatus("Thoát Play mode rồi sửa prefab.", State.Error);
            return;
        }

        string path = AssetDatabase.GetAssetPath(set.Prefab);
        List<WeaponData> datas = CharacterWeapons().Select(LoadWeaponData).Where(d => d != null).ToList();
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            GameObject host = animator.gameObject;

            foreach (Check check in checks)
            {
                switch (check.Fix)
                {
                    case FixKind.AddComponent:
                        if (check.Component != null && host.GetComponent(check.Component) == null) host.AddComponent(check.Component);
                        break;
                    case FixKind.AssignController:
                        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionAnimatorBuilder.ControllerPath);
                        break;
                    case FixKind.AssignWeapons:
                        CharacterCombo combo = host.GetComponent<CharacterCombo>();
                        if (combo == null) break;
                        combo.Weapons.RemoveAll(w => w == null);
                        foreach (WeaponData data in datas)
                        {
                            if (!combo.Weapons.Contains(data)) combo.Weapons.Add(data);
                        }
                        break;
                }
            }

            LinkReferences(animator, datas);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            SetStatus($"Đã sửa {checks.Count} mục trong {System.IO.Path.GetFileName(path)}.");
        }
        catch (Exception exception)
        {
            SetStatus("Lỗi khi sửa prefab: " + exception.Message, State.Error);
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            Invalidate();
        }
    }

    // nối các tham chiếu hay bị bỏ trống khi thêm component bằng code
    private static void LinkReferences(Animator animator, List<WeaponData> datas)
    {
        animator.applyRootMotion = true;
        CharacterCombo combo = animator.GetComponent<CharacterCombo>();
        if (combo != null && combo.animator == null) combo.animator = animator;

        WeaponHolder holder = animator.GetComponent<WeaponHolder>();
        if (holder != null)
        {
            if (holder.animator == null) holder.animator = animator;
            if (holder.characterCombo == null) holder.characterCombo = combo;
            if (holder.StarterWeapon == null && datas.Count > 0) holder.StarterWeapon = datas[0];
        }

        CombatEffects effects = animator.GetComponent<CombatEffects>();
        AudioSource source = animator.GetComponent<AudioSource>();
        if (effects != null && effects.Source == null && source != null) effects.Source = source;
    }

    #endregion

    #region Moveset

    private static readonly (string title, string hint, Func<string, bool> match)[] StateGroups =
    {
        ("Đứng yên & xoay tại chỗ", "Idle + xoay 90/180 khi đứng", s => s.StartsWith("Idle")),
        ("Bắt đầu di chuyển", "Clip một lần từ đứng sang đi/chạy (kể cả xoay 90/180)", s => s.StartsWith("Start")),
        ("Dừng lại", "Từ đi/chạy về đứng", s => s.StartsWith("Stop")),
        ("Đổi hướng khi đang chạy", "Quay 90/180 lúc di chuyển", s => s.StartsWith("Turn")),
        ("Nhảy", "Nhảy từ đất + nhảy trên không (double jump)", s => s.Contains("Jump")),
        ("Né / lộn", "Dodge đứng yên + lộn khi chạy", s => s.Contains("Dodge")),
        ("Ngồi", "Vào thế ngồi + di chuyển ngồi", s => s.StartsWith("Crouch")),
        ("Khác", "State blend tree (Loop / Strafe) nếu cần clip riêng", s => s == "Loop" || s == "Strafe"),
    };

    private string _movesetSearch = string.Empty;
    private bool _movesetMissingOnly;

    private void DrawMovesetTabs()
    {
        int tab = StudioGUI.Pills(_movesetTab, new[] { "Bảng state", "Tự map clip (nhân bản / ghi đè)" });
        if (tab == _movesetTab) return;
        _movesetTab = tab;
        _scroll = Vector2.zero;
        Invalidate();
    }

    private void DrawMovesetPage()
    {
        AnimSetDefinition set = Set;
        Health health = CurrentHealth;
        StudioGUI.PageTitle("Moveset", "Clip cho từng state di chuyển. Sửa xong vào trang Build → Build Locomotion để dựng lại Animator.");
        DrawMovesetTabs();
        GUILayout.Space(8f);
        DrawBrokenFoldersCard();

        StudioGUI.BeginCard("Thông tin bộ anim");
        TextRow(_sets, "Tên nhân vật", set.DisplayName, value => set.DisplayName = value);
        string oldPrefix = set.Prefix;
        string prefix = EditorGUILayout.DelayedTextField(new GUIContent("Prefix", "Tiền tố state, vd VG_ -> VG_Idle. Đổi prefix phải Sinh prefix + Build lại."), set.Prefix);
        if (prefix != oldPrefix)
        {
            prefix = AnimSetCloneTool.NormalizePrefix(prefix);
            if (AnimSetCloneTool.PrefixExists(_sets, prefix, _setIndex)) SetStatus("Prefix " + prefix + " đã có bộ khác dùng.", State.Error);
            else
            {
                Change(_sets, "Sửa prefix", oldPrefix, prefix, value => set.Prefix = value);
                _selectedPrefix = prefix;
                SetStatus("Đã đổi prefix → nhớ Sinh prefix + Build Locomotion ở trang Build.", State.Warn);
            }
        }
        FolderRow(_sets, "Thư mục anim", set.MovementFolder, value => set.MovementFolder = value);

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Thư mục dự phòng", "State không thấy clip trong thư mục chính sẽ tìm ở đây (vd giữ clip của Dante)"), GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
        GUILayout.BeginVertical();
        int removeFolder = -1;
        for (int i = 0; i < set.ExtraFolders.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(set.ExtraFolders[i], StudioGUI.MutedStyle);
            if (StudioGUI.Icon("✕", "Bỏ thư mục này")) removeFolder = i;
            GUILayout.EndHorizontal();
        }
        if (StudioGUI.Button("＋ Thêm thư mục", null, 120f))
        {
            string picked = StudioGUI.PickFolder(set.MovementFolder);
            if (!string.IsNullOrEmpty(picked) && !set.ExtraFolders.Contains(picked))
            {
                Undo.RecordObject(_sets, "Thêm thư mục dự phòng");
                set.ExtraFolders.Add(AnimSetCloneTool.NormalizeFolder(picked));
                EditorUtility.SetDirty(_sets);
                AnimSetCloneTool.ClearCache();
                Invalidate();
            }
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        if (removeFolder >= 0)
        {
            Undo.RecordObject(_sets, "Bỏ thư mục dự phòng");
            set.ExtraFolders.RemoveAt(removeFolder);
            EditorUtility.SetDirty(_sets);
            AnimSetCloneTool.ClearCache();
            Invalidate();
        }
        StudioGUI.EndCard();

        // thanh lọc
        GUILayout.BeginHorizontal();
        StudioGUI.Chip($"{health.Rows - health.MissingClips}/{health.Rows} có clip", health.MissingClips > 0 ? StudioGUI.Warn : StudioGUI.Ok);
        StudioGUI.Chip($"{health.Clips?.Names.Count ?? 0} clip trong thư mục", StudioGUI.Muted);
        GUILayout.FlexibleSpace();
        _movesetMissingOnly = GUILayout.Toggle(_movesetMissingOnly, " chỉ dòng thiếu clip", GUILayout.Width(140f));
        _movesetSearch = EditorGUILayout.TextField(_movesetSearch, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        foreach (var group in StateGroups) DrawStateGroup(set, health.Clips, group.title, group.hint, group.match);
        DrawGaits(set, health.Clips);
        DrawDirections(set, set.Strafe, health.Clips, "Strafe 8 hướng", "Blend tree di chuyển khi khoá mục tiêu (Move_X / Move_Y). Gần tâm = đi, xa tâm = chạy.");
        DrawDirections(set, set.Crouch, health.Clips, "Ngồi 8 hướng", "Blend tree di chuyển khi ngồi.");
    }

    private bool PassesFilter(string state, string clip, AnimSetCloneTool.ClipIndex clips)
    {
        if (_movesetMissingOnly && !string.IsNullOrEmpty(clip) && clips != null && clips.Contains(clip)) return false;
        if (string.IsNullOrEmpty(_movesetSearch)) return true;
        return state.IndexOf(_movesetSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
               (!string.IsNullOrEmpty(clip) && clip.IndexOf(_movesetSearch, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void DrawStateGroup(AnimSetDefinition set, AnimSetCloneTool.ClipIndex clips, string title, string hint, Func<string, bool> match)
    {
        List<LocoState> states = Enum.GetValues(typeof(LocoState)).Cast<LocoState>().Where(s => match(s.ToString())).ToList();
        List<LocoState> visible = states.Where(s =>
        {
            LocoClipDefinition row = set.OneShots.FirstOrDefault(r => r.State == s);
            if (row == null) return !_movesetMissingOnly && string.IsNullOrEmpty(_movesetSearch) && s != LocoState.Loop && s != LocoState.Strafe;
            return PassesFilter(s.ToString(), row.Clip, clips);
        }).ToList();
        if (visible.Count == 0) return;

        int used = states.Count(s => set.OneShots.Any(r => r.State == s));
        int ok = set.OneShots.Count(r => states.Contains(r.State) && !string.IsNullOrEmpty(r.Clip) && clips != null && clips.Contains(r.Clip));
        State groupState = used == 0 ? State.None : ok == used ? State.Ok : State.Warn;

        StudioGUI.BeginCard(groupState, $"{title}   ·   {ok}/{used}", hint);
        GUILayout.BeginHorizontal();
        GUILayout.Space(14f);
        GUILayout.Label("State", StudioGUI.SectionStyle, GUILayout.Width(132f));
        GUILayout.Label("Clip", StudioGUI.SectionStyle, GUILayout.MinWidth(160f), GUILayout.ExpandWidth(true));
        GUILayout.Label(new GUIContent("Loop", "Clip lặp"), StudioGUI.SectionStyle, GUILayout.Width(38f));
        GUILayout.Label(new GUIContent("Air", "Clip trên không: root bám bàn chân"), StudioGUI.SectionStyle, GUILayout.Width(30f));
        GUILayout.Label(new GUIContent("Speed", "Tốc độ phát state"), StudioGUI.SectionStyle, GUILayout.Width(44f));
        GUILayout.Label(new GUIContent("Frame", "Frame bắt đầu cắt clip, -1 = giữ nguyên"), StudioGUI.SectionStyle, GUILayout.Width(44f));
        GUILayout.Space(26f);
        GUILayout.EndHorizontal();

        LocoClipDefinition remove = null;
        LocoState? add = null;
        foreach (LocoState state in visible)
        {
            LocoClipDefinition row = set.OneShots.FirstOrDefault(r => r.State == state);
            GUILayout.BeginHorizontal(GUILayout.Height(20f));
            if (row == null)
            {
                GUILayout.Space(14f);
                GUILayout.Label(state.ToString(), StudioGUI.MutedStyle, GUILayout.Width(132f));
                GUILayout.Label("chưa dùng", StudioGUI.MutedStyle);
                GUILayout.FlexibleSpace();
                if (StudioGUI.Icon("＋", "Thêm state này vào bộ")) add = state;
            }
            else
            {
                GUILayout.Label(state.ToString(), StudioGUI.LabelStyle, GUILayout.Width(132f));
                ClipButton(_sets, row.Clip, clips, value => row.Clip = value);
                Change(_sets, "Sửa Loop", row.Loop, GUILayout.Toggle(row.Loop, GUIContent.none, GUILayout.Width(38f)), value => row.Loop = value);
                Change(_sets, "Sửa Air", row.Air, GUILayout.Toggle(row.Air, GUIContent.none, GUILayout.Width(30f)), value => row.Air = value);
                Change(_sets, "Sửa Speed", row.Speed, EditorGUILayout.FloatField(row.Speed, GUILayout.Width(44f)), value => row.Speed = value);
                Change(_sets, "Sửa FirstFrame", row.FirstFrame, EditorGUILayout.IntField(row.FirstFrame, GUILayout.Width(44f)), value => row.FirstFrame = value);
                if (StudioGUI.Icon("✕", "Bỏ state này khỏi bộ")) remove = row;
            }
            GUILayout.EndHorizontal();
        }
        StudioGUI.EndCard();

        if (remove != null)
        {
            Undo.RecordObject(_sets, "Bỏ state");
            set.OneShots.Remove(remove);
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
        if (add.HasValue)
        {
            Undo.RecordObject(_sets, "Thêm state");
            set.OneShots.Add(new LocoClipDefinition { State = add.Value, Loop = add.Value == LocoState.Idle || add.Value.ToString().EndsWith("Loop") });
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
    }

    private void DrawGaits(AnimSetDefinition set, AnimSetCloneTool.ClipIndex clips)
    {
        if (!string.IsNullOrEmpty(_movesetSearch) || _movesetMissingOnly)
        {
            if (!set.Gaits.Any(g => PassesFilter("Gait", g.Clip, clips))) return;
        }
        int ok = set.Gaits.Count(g => !string.IsNullOrEmpty(g.Clip) && clips != null && clips.Contains(g.Clip));
        StudioGUI.BeginCard(set.Gaits.Count == 0 ? State.Warn : ok == set.Gaits.Count ? State.Ok : State.Warn,
            $"Tốc độ chạy (Gait)   ·   {ok}/{set.Gaits.Count}", "Blend tree 1D của state Loop: ngưỡng 1 = chậm nhất (đi), số lớn hơn = nhanh hơn (chạy, sprint).");
        int remove = -1;
        for (int i = 0; i < set.Gaits.Count; i++)
        {
            GaitClipDefinition gait = set.Gaits[i];
            GUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Label(GaitName(i, set.Gaits.Count), StudioGUI.LabelStyle, GUILayout.Width(80f));
            GUILayout.Label("ngưỡng", StudioGUI.MutedStyle, GUILayout.Width(42f));
            Change(_sets, "Sửa ngưỡng gait", gait.Threshold, EditorGUILayout.FloatField(gait.Threshold, GUILayout.Width(40f)), value => gait.Threshold = value);
            ClipButton(_sets, gait.Clip, clips, value => gait.Clip = value);
            if (StudioGUI.Icon("✕", "Bỏ mức gait này")) remove = i;
            GUILayout.EndHorizontal();
        }
        if (StudioGUI.Button("＋ Mức tốc độ", null, 120f))
        {
            Undo.RecordObject(_sets, "Thêm gait");
            set.Gaits.Add(new GaitClipDefinition { Threshold = set.Gaits.Count + 1f });
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
        StudioGUI.EndCard();
        if (remove >= 0)
        {
            Undo.RecordObject(_sets, "Bỏ gait");
            set.Gaits.RemoveAt(remove);
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
    }

    private static string GaitName(int index, int count)
    {
        string[] names = { "Đi", "Chạy chậm", "Chạy", "Sprint" };
        if (count == 4 && index < 4) return names[index];
        return "Mức " + (index + 1);
    }

    private void DrawDirections(AnimSetDefinition set, List<DirectionalClipDefinition> rows, AnimSetCloneTool.ClipIndex clips, string title, string hint)
    {
        if ((!string.IsNullOrEmpty(_movesetSearch) || _movesetMissingOnly) && !rows.Any(r => PassesFilter(title, r.Clip, clips))) return;
        int ok = rows.Count(r => !string.IsNullOrEmpty(r.Clip) && clips != null && clips.Contains(r.Clip));
        StudioGUI.BeginCard(rows.Count == 0 ? State.None : ok == rows.Count ? State.Ok : State.Warn, $"{title}   ·   {ok}/{rows.Count}", hint);
        int remove = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            DirectionalClipDefinition row = rows[i];
            if (!PassesFilter(title, row.Clip, clips)) continue;
            GUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Label(DirectionGlyph(row.X, row.Y), new GUIStyle(StudioGUI.TitleStyle) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(22f));
            GUILayout.Label(DirectionName(row.X, row.Y), StudioGUI.MutedStyle, GUILayout.Width(80f));
            GUILayout.Label("X", StudioGUI.MutedStyle, GUILayout.Width(10f));
            Change(_sets, "Sửa hướng", row.X, EditorGUILayout.FloatField(row.X, GUILayout.Width(36f)), value => row.X = value);
            GUILayout.Label("Y", StudioGUI.MutedStyle, GUILayout.Width(10f));
            Change(_sets, "Sửa hướng", row.Y, EditorGUILayout.FloatField(row.Y, GUILayout.Width(36f)), value => row.Y = value);
            ClipButton(_sets, row.Clip, clips, value => row.Clip = value);
            if (StudioGUI.Icon("✕", "Bỏ hướng này")) remove = i;
            GUILayout.EndHorizontal();
        }
        if (StudioGUI.Button("＋ Hướng", null, 100f))
        {
            Undo.RecordObject(_sets, "Thêm hướng");
            rows.Add(new DirectionalClipDefinition { Y = 1f });
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
        StudioGUI.EndCard();
        if (remove >= 0)
        {
            Undo.RecordObject(_sets, "Bỏ hướng");
            rows.RemoveAt(remove);
            EditorUtility.SetDirty(_sets);
            Invalidate();
        }
    }

    private static string DirectionGlyph(float x, float y)
    {
        if (Mathf.Abs(x) < 0.05f && Mathf.Abs(y) < 0.05f) return "•";
        string[] glyphs = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
        float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt((angle + 360f) % 360f / 45f) % 8;
        return glyphs[index];
    }

    private static string DirectionName(float x, float y)
    {
        if (Mathf.Abs(x) < 0.05f && Mathf.Abs(y) < 0.05f) return "đứng";
        return Mathf.Sqrt(x * x + y * y) < 0.75f ? "đi" : "chạy";
    }

    #endregion

    #region Build

    private bool _showAdvanced;

    private void DrawBuildPage()
    {
        AnimSetDefinition set = Set;
        Health health = CurrentHealth;
        List<WeaponDefinition> weapons = CharacterWeapons();
        bool sceneReady = UnityEngine.Object.FindAnyObjectByType<CharacterCombo>(FindObjectsInactive.Include) != null;

        StudioGUI.PageTitle("Build", "Biến dữ liệu (AnimSetLibrary + WeaponLibrary) thành Animator + WeaponData mà game dùng.");

        if (Application.isPlaying)
        {
            StudioGUI.BeginCard(State.Error, "Đang Play mode", "Thoát Play mode rồi build.");
            StudioGUI.EndCard();
            return;
        }

        StudioGUI.BeginCard(health.BuildState, "Tình trạng", health.BuildIssues.Count == 0 ? "Mọi thứ đã build và khớp dữ liệu." : null);
        foreach (string issue in health.BuildIssues) StudioGUI.Note(issue, State.Warn);
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (StudioGUI.Primary("▶  Build tất cả cho " + set.DisplayName, "Sinh prefix → Build Locomotion → Build vũ khí của nhân vật", 280f)) BuildCharacter(true, true, true);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (!sceneReady) StudioGUI.Note("Build vũ khí cần mở scene có nhân vật gắn CharacterCombo (vd Assets/Sence/SetUp.unity) — builder lấy avatar + scale từ đó.", State.Warn);
        StudioGUI.EndCard();

        StudioGUI.BeginCard("Từng bước");
        bool prefixOk = health.PrefixGenerated && health.PrefixCountMatches;
        if (PipelineRow(1, prefixOk ? State.Ok : State.Error, "Sinh prefix",
                "Ghi prefix các bộ anim vào LocoSetPrefixes.Generated.cs để runtime biết bộ này. Unity sẽ compile lại.", "Sinh"))
            BuildCharacter(true, false, false);
        if (PipelineRow(2, health.ControllerStates > 0 ? State.Ok : State.Error, "Build Locomotion",
                $"Dựng lại layer Locomotion cho MỌI bộ anim. Bộ {set.Prefix}: {health.ControllerStates} state trong controller.", "Build"))
            BuildCharacter(false, true, false);
        State weaponState = weapons.Count == 0 ? State.Warn : health.WeaponsBuilt < weapons.Count ? State.Error : health.BuildIssues.Any(i => i.StartsWith("WD_")) ? State.Warn : State.Ok;
        GUI.enabled = weapons.Count > 0 && sceneReady;
        if (PipelineRow(3, weaponState, "Build vũ khí của nhân vật",
                $"Phân tích clip ({string.Join(", ", weapons.Select(w => w.Key))}) → mốc chém / đá / slam, root motion, cách cầm → WD_<Key> + layer WeaponAttack. Vũ khí nhân vật khác giữ nguyên. Ghi đè mốc đã kéo tay.", "Build"))
            BuildCharacter(false, false, true);
        GUI.enabled = true;
        if (PipelineRow(4, State.None, "Bake Slam + Kick",
                "Chỉ tính lại SlamTime + KickTimes cho mọi vũ khí, không đụng Animator và mốc chém đã kéo tay.", "Bake"))
            RunBuild("Bake Slam + Kick", WeaponComboBuilder.BakeSlamAndKickTimes);
        StudioGUI.EndCard();

        StudioGUI.BeginCard("Kiểm tra dữ liệu đòn", "Quét WeaponData của nhân vật: thiếu clip, state chưa có trong Animator, mốc ngoài 0..1, mốc chém quá sát...");
        CombatTimingWindow timing = EnsurePanel(ref _timing);
        timing.Host = this;
        timing.SetCharacter(_setIndex, set.Prefab);
        if (StudioGUI.Button("Kiểm tra", null, 120f)) timing.Validate();
        foreach (string line in timing.Validation)
        {
            State state = line.StartsWith("✖") ? State.Error : line.StartsWith("⚠") ? State.Warn : State.Ok;
            StudioGUI.Note(line.TrimStart('✖', '⚠', '✔', ' '), state);
        }
        StudioGUI.EndCard();

        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Nâng cao", true);
        if (!_showAdvanced) return;
        StudioGUI.BeginCard("Nâng cao", "Lệnh ảnh hưởng mọi nhân vật — nên commit trước khi chạy.");
        GUILayout.BeginHorizontal();
        if (StudioGUI.Button("Build toàn bộ vũ khí", "Phân tích lại vũ khí của MỌI nhân vật", 170f) &&
            EditorUtility.DisplayDialog("Build toàn bộ vũ khí?", "Ghi đè mốc đã kéo tay của mọi vũ khí.", "Build", "Huỷ"))
            RunBuild("Build toàn bộ vũ khí", WeaponComboBuilder.BuildAll);
        if (StudioGUI.Button("Setup Vergil (preset)", "Chạy hồ sơ dựng sẵn trong CharacterSetupTool", 160f)) EditorApplication.ExecuteMenuItem("Tools/Combat/Nhân vật/Vergil - Setup + Build");
        if (StudioGUI.Button("Setup Trish (preset)", "Chạy hồ sơ dựng sẵn trong CharacterSetupTool", 160f)) EditorApplication.ExecuteMenuItem("Tools/Combat/Nhân vật/Trish - Setup + Build");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (StudioGUI.Danger("Nhập AnimSetLibrary từ code", "Tạo lại từ bảng hard-code của Dante — mất mọi chỉnh tay", 200f) && ConfirmOverwrite(LocomotionAnimatorBuilder.LibraryPath))
        {
            LocomotionAnimatorBuilder.CreateLibraryFromCode();
            ReloadLibraries();
        }
        if (StudioGUI.Danger("Nhập WeaponLibrary từ code", "Tạo lại từ bảng hard-code (Rebellion, Sparda) — mất mọi chỉnh tay", 200f) && ConfirmOverwrite(WeaponComboBuilder.LibraryPath))
        {
            WeaponComboBuilder.CreateLibraryFromCode();
            ReloadLibraries();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        StudioGUI.EndCard();
    }

    private static bool ConfirmOverwrite(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) == null) return true;
        return EditorUtility.DisplayDialog("Ghi đè asset?", assetPath + " đã có.\nTạo lại từ code sẽ mất mọi chỉnh tay trong asset này.", "Ghi đè", "Huỷ");
    }

    private static bool PipelineRow(int number, State state, string title, string detail, string action)
    {
        bool clicked = false;
        GUILayout.BeginHorizontal(GUILayout.MinHeight(40f));
        Rect badge = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(26f), GUILayout.Height(26f));
        StudioGUI.Badge(badge, state == State.Ok ? "✓" : number.ToString(), state == State.None ? StudioGUI.Border : StudioGUI.For(state));
        GUILayout.Space(8f);
        GUILayout.BeginVertical();
        GUILayout.Label(title, StudioGUI.LabelStyle);
        GUILayout.Label(detail, StudioGUI.MutedStyle);
        GUILayout.EndVertical();
        GUILayout.Space(8f);
        GUILayout.BeginVertical(GUILayout.Width(90f));
        GUILayout.Space(4f);
        clicked = StudioGUI.Button(action, null, 90f);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        StudioGUI.Separator();
        return clicked;
    }

    private void BuildCharacter(bool prefixes, bool locomotion, bool weapons)
    {
        if (Application.isPlaying) return;
        AnimSetDefinition set = Set;
        if (prefixes && locomotion && weapons &&
            !EditorUtility.DisplayDialog("Build " + set.DisplayName + "?",
                "• Sinh prefix (Unity compile lại)\n• Dựng lại layer Locomotion cho mọi bộ anim\n• Phân tích lại vũ khí của nhân vật này (ghi đè mốc đã kéo tay)\n\nNên commit trước.",
                "Build", "Huỷ")) return;

        RunBuild("Build " + set.DisplayName, () =>
        {
            AssetDatabase.SaveAssets();
            if (prefixes) LocoSetPrefixesGenerator.Generate(_sets);
            if (locomotion) LocomotionAnimatorBuilder.BuildFrom(_sets);
            if (weapons)
            {
                List<string> keys = CharacterWeapons().Select(w => w.Key).ToList();
                if (keys.Count > 0) WeaponComboBuilder.BuildWeapons(_weaponLibrary, keys);
            }
        });
    }

    private void RunBuild(string label, Action action)
    {
        try
        {
            action();
            AssetDatabase.SaveAssets();
            SetStatus(label + ": xong. Đọc Console để chắc không có 'Không thấy clip'.");
        }
        catch (Exception exception)
        {
            SetStatus(label + " lỗi: " + exception.Message, State.Error);
            Debug.LogException(exception);
        }
        finally
        {
            AnimSetCloneTool.ClearCache();
            if (_timing != null) _timing.ReloadWeapons();
            Invalidate();
        }
    }

    #endregion
}
