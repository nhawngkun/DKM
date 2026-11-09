using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using State = StudioGUI.State;

/// <summary>
/// Character Studio: 1 cửa sổ duy nhất để set up nhân vật.
/// Nhân vật = 1 bộ anim trong AnimSetLibrary (prefix + moveset + prefab) + các vũ khí trong WeaponLibrary có AnimSetId trỏ về bộ đó.
/// Các trang đi theo thứ tự làm việc: Tổng quan → Prefab → Moveset → Vũ khí & Combo → Mốc đánh → Cầm vũ khí → Âm thanh → Build.
/// Trang Mốc đánh / Âm thanh / Cầm vũ khí / Tự map clip nhúng lại CombatTimingWindow / WeaponGripTool / AnimSetCloneWindow.
/// </summary>
public partial class CharacterStudioWindow : EditorWindow
{
    public enum Page { Overview, Prefab, Moveset, Combo, Timing, Grip, Sound, Build, NewCharacter }

    private struct NavItem
    {
        public Page Page;
        public string Label;
        public string Hint;
    }

    private static readonly NavItem[] Nav =
    {
        new NavItem { Page = Page.Overview, Label = "Tổng quan", Hint = "Checklist + số liệu của nhân vật" },
        new NavItem { Page = Page.Prefab, Label = "Prefab", Hint = "Prefab, component, vũ khí gán sẵn" },
        new NavItem { Page = Page.Moveset, Label = "Moveset", Hint = "Bảng state di chuyển (Locomotion)" },
        new NavItem { Page = Page.Combo, Label = "Vũ khí & Combo", Hint = "WeaponData, chuỗi đòn, các bước" },
        new NavItem { Page = Page.Timing, Label = "Mốc đánh", Hint = "Timeline mốc chém / đá / slam / nối" },
        new NavItem { Page = Page.Grip, Label = "Cầm vũ khí", Hint = "Căn vũ khí trong lòng bàn tay" },
        new NavItem { Page = Page.Sound, Label = "Âm thanh", Hint = "Bộ SFX CombatEffects" },
        new NavItem { Page = Page.Build, Label = "Build", Hint = "Dựng Animator + WeaponData + kiểm tra" },
    };

    private const float HeaderHeight = 54f;
    private const float SidebarWidth = 212f;
    private const float FooterHeight = 22f;
    private const float NavHeight = 38f;

    [SerializeField] private Page _page;
    [SerializeField] private string _selectedPrefix;
    [SerializeField] private int _weaponTab;
    [SerializeField] private int _movesetTab;

    private AnimSetLibrary _sets;
    private WeaponLibrary _weaponLibrary;
    private int _setIndex = -1;
    private Vector2 _scroll;
    private string _status = string.Empty;
    private State _statusState;

    [NonSerialized] private CombatTimingWindow _timing;
    [NonSerialized] private WeaponGripTool _grip;
    [NonSerialized] private AnimSetCloneWindow _clone;
    [NonSerialized] private Health _health;

    private AnimSetDefinition Set => _sets != null && _setIndex >= 0 && _setIndex < _sets.Sets.Count ? _sets.Sets[_setIndex] : null;

    #region Mở cửa sổ

    [MenuItem("Tools/Character Studio", false, 0)]
    public static void OpenMenu() => Open(Page.Overview);

    public static void Open(Page page)
    {
        CharacterStudioWindow window = GetWindow<CharacterStudioWindow>();
        window.titleContent = new GUIContent("Character Studio", AssetPreview.GetMiniTypeThumbnail(typeof(Animator)));
        window.minSize = new Vector2(1080f, 660f);
        window._page = page;
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
        // panel ẩn của lần mở trước (trước khi Unity compile lại) không còn ai giữ -> dọn
        DestroyOrphans<CombatTimingWindow>();
        DestroyOrphans<WeaponGripTool>();
        DestroyOrphans<AnimSetCloneWindow>();
        ReloadLibraries();
        Undo.undoRedoPerformed += Invalidate;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Invalidate;
        DestroyPanel(ref _timing);
        DestroyPanel(ref _grip);
        DestroyPanel(ref _clone);
    }

    private void OnProjectChange()
    {
        AnimSetCloneTool.ClearCache();
        Invalidate();
        if (_timing != null) _timing.ReloadWeapons();
        Repaint();
    }

    private void OnFocus() => Invalidate();

    #endregion

    #region Dữ liệu

    private void ReloadLibraries()
    {
        _sets = LocomotionAnimatorBuilder.FindLibrary();
        _weaponLibrary = WeaponComboBuilder.FindLibrary();
        ResolveSelection();
        Invalidate();
    }

    private void ResolveSelection()
    {
        if (_sets == null || _sets.Sets.Count == 0)
        {
            _setIndex = -1;
            return;
        }
        int found = string.IsNullOrEmpty(_selectedPrefix) ? -1 : AnimSetCloneTool.IndexOfPrefix(_sets, _selectedPrefix);
        if (found >= 0) _setIndex = found;
        _setIndex = Mathf.Clamp(_setIndex, 0, _sets.Sets.Count - 1);
        _selectedPrefix = _sets.Sets[_setIndex].Prefix;
    }

    private void Select(int index)
    {
        _setIndex = index;
        _selectedPrefix = Set != null ? Set.Prefix : null;
        _weaponTab = 0;
        if (_page == Page.NewCharacter) _page = Page.Overview;
        Invalidate();
        GUI.FocusControl(null);
    }

    private void Invalidate() => _health = null;

    private List<WeaponDefinition> CharacterWeapons()
    {
        if (_weaponLibrary == null || _setIndex < 0) return new List<WeaponDefinition>();
        return _weaponLibrary.Weapons.Where(weapon => weapon.AnimSetId == _setIndex).ToList();
    }

    private static string WeaponDataPath(string key) => $"{WeaponComboBuilder.OutputFolder}/WD_{key}.asset";

    private static WeaponData LoadWeaponData(WeaponDefinition weapon)
    {
        return weapon == null || string.IsNullOrEmpty(weapon.Key) ? null : AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDataPath(weapon.Key));
    }

    private Health CurrentHealth
    {
        get
        {
            if (_health == null) _health = Health.Compute(_sets, _setIndex, CharacterWeapons());
            return _health;
        }
    }

    private void SetStatus(string message, State state = State.Ok)
    {
        _status = message;
        _statusState = state;
        Repaint();
    }

    #endregion

    #region Khung giao diện

    private void OnGUI()
    {
        if (_sets == null || _weaponLibrary == null)
        {
            // asset có thể vừa được tạo / import xong
            if (Event.current.type == EventType.Layout) ReloadLibraries();
        }
        if (_sets != null && (_setIndex < 0 || _setIndex >= _sets.Sets.Count)) ResolveSelection();

        Rect full = new Rect(0f, 0f, position.width, position.height);
        StudioGUI.Fill(full, StudioGUI.Background);

        Rect header = new Rect(0f, 0f, position.width, HeaderHeight);
        Rect sidebar = new Rect(0f, HeaderHeight, SidebarWidth, position.height - HeaderHeight - FooterHeight);
        Rect content = new Rect(SidebarWidth, HeaderHeight, position.width - SidebarWidth, position.height - HeaderHeight - FooterHeight);
        Rect footer = new Rect(0f, position.height - FooterHeight, position.width, FooterHeight);

        DrawHeader(header);
        DrawSidebar(sidebar);
        DrawFooter(footer);
        DrawContent(content);
    }

    private void DrawHeader(Rect rect)
    {
        StudioGUI.Fill(rect, StudioGUI.Header);
        StudioGUI.Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), StudioGUI.Border);

        GUIStyle logo = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = StudioGUI.Text } };
        GUIStyle sub = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = StudioGUI.Muted } };
        StudioGUI.Fill(new Rect(16f, 15f, 4f, 24f), StudioGUI.Accent);
        GUI.Label(new Rect(28f, 10f, 200f, 20f), "CHARACTER STUDIO", logo);
        GUI.Label(new Rect(28f, 29f, 200f, 16f), "set up nhân vật ở một chỗ", sub);

        // ô chọn nhân vật
        Rect picker = new Rect(SidebarWidth + 16f, 11f, 300f, 32f);
        StudioGUI.Fill(picker, StudioGUI.Card);
        StudioGUI.Fill(new Rect(picker.x, picker.y, 3f, picker.height), StudioGUI.Accent);
        AnimSetDefinition set = Set;
        string name = set != null ? set.DisplayName : "(chưa có nhân vật)";
        string prefix = set != null ? set.Prefix : string.Empty;
        GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = StudioGUI.Text } };
        GUIStyle prefixStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = StudioGUI.Accent } };
        GUI.Label(new Rect(picker.x + 12f, picker.y + 7f, picker.width - 90f, 18f), name, nameStyle);
        GUI.Label(new Rect(picker.xMax - 92f, picker.y + 8f, 60f, 16f), prefix, prefixStyle);
        GUI.Label(new Rect(picker.xMax - 24f, picker.y + 7f, 16f, 18f), "▾", nameStyle);
        EditorGUIUtility.AddCursorRect(picker, MouseCursor.Link);
        if (GUI.Button(picker, GUIContent.none, GUIStyle.none)) ShowCharacterMenu(picker);

        Rect add = new Rect(picker.xMax + 8f, 15f, 132f, 24f);
        if (GUI.Button(add, "＋ Nhân vật mới", new GUIStyle(EditorStyles.miniButton) { fontStyle = FontStyle.Bold, fixedHeight = 24f }))
        {
            BeginNewCharacter();
        }

        // tóm tắt nhanh bên phải
        if (set != null)
        {
            Health health = CurrentHealth;
            Rect right = new Rect(rect.xMax - 360f, 13f, 344f, 28f);
            GUILayout.BeginArea(right);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            StudioGUI.Chip(health.Weapons + " vũ khí", StudioGUI.Purple);
            StudioGUI.Chip(health.Chains + " chuỗi · " + health.Steps + " đòn", StudioGUI.Accent);
            State overall = health.Overall;
            StudioGUI.Chip(overall == State.Ok ? "sẵn sàng" : overall == State.Warn ? "cần xem lại" : "chưa xong", StudioGUI.For(overall));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }

    private void ShowCharacterMenu(Rect rect)
    {
        GenericMenu menu = new GenericMenu();
        if (_sets != null)
        {
            for (int i = 0; i < _sets.Sets.Count; i++)
            {
                int captured = i;
                AnimSetDefinition set = _sets.Sets[i];
                menu.AddItem(new GUIContent($"{set.DisplayName}   [{set.Prefix}]"), i == _setIndex, () => Select(captured));
            }
            menu.AddSeparator(string.Empty);
        }
        menu.AddItem(new GUIContent("＋ Nhân vật mới…"), false, () => BeginNewCharacter());
        menu.AddItem(new GUIContent("Nạp lại thư viện"), false, () =>
        {
            AnimSetCloneTool.ClearCache();
            ReloadLibraries();
        });
        menu.DropDown(rect);
    }

    private void DrawSidebar(Rect rect)
    {
        StudioGUI.Fill(rect, StudioGUI.Sidebar);
        StudioGUI.Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), StudioGUI.Border);

        GUIStyle group = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = StudioGUI.Muted } };
        GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, 160f, 16f), "CÁC BƯỚC", group);

        Health health = Set != null ? CurrentHealth : null;
        float y = rect.y + 32f;
        for (int i = 0; i < Nav.Length; i++)
        {
            NavItem item = Nav[i];
            Rect row = new Rect(rect.x + 8f, y, rect.width - 16f, NavHeight - 4f);
            bool selected = _page == item.Page;
            bool hover = row.Contains(Event.current.mousePosition);

            if (selected) StudioGUI.Fill(row, StudioGUI.Card);
            else if (hover) StudioGUI.Fill(row, StudioGUI.Alpha(StudioGUI.Card, 0.55f));
            if (selected) StudioGUI.Fill(new Rect(row.x, row.y, 3f, row.height), StudioGUI.Accent);

            State state = health != null ? health.StateOf(item.Page) : State.None;
            Color badge = selected ? StudioGUI.Accent : state == State.Ok ? StudioGUI.Alpha(StudioGUI.Ok, 0.85f) : StudioGUI.Border;
            StudioGUI.Badge(new Rect(row.x + 12f, row.center.y - 10f, 20f, 20f), state == State.Ok && !selected ? "✓" : (i + 1).ToString(), badge);

            GUIStyle label = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = selected || hover ? StudioGUI.Text : StudioGUI.Muted },
            };
            GUI.Label(new Rect(row.x + 42f, row.y + 2f, row.width - 60f, 18f), item.Label, label);
            GUIStyle hint = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, normal = { textColor = StudioGUI.Alpha(StudioGUI.Muted, 0.8f) } };
            GUI.Label(new Rect(row.x + 42f, row.y + 17f, row.width - 56f, 14f), item.Hint, hint);

            if (state == State.Warn || state == State.Error)
                StudioGUI.DrawDot(new Rect(row.xMax - 14f, row.center.y - 4f, 8f, 8f), StudioGUI.For(state));

            EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
            {
                _page = item.Page;
                _scroll = Vector2.zero;
                GUI.FocusControl(null);
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseMove && row.Contains(Event.current.mousePosition)) Repaint();
            y += NavHeight;
        }

        // lối tắt tới asset dữ liệu
        Rect bottom = new Rect(rect.x + 12f, rect.yMax - 86f, rect.width - 24f, 80f);
        GUILayout.BeginArea(bottom);
        GUILayout.Label("DỮ LIỆU", group);
        if (GUILayout.Button(new GUIContent("AnimSetLibrary", "Chọn asset bộ anim trong Project"), EditorStyles.miniButton)) Ping(_sets);
        if (GUILayout.Button(new GUIContent("WeaponLibrary", "Chọn asset danh sách vũ khí trong Project"), EditorStyles.miniButton)) Ping(_weaponLibrary);
        if (GUILayout.Button(new GUIContent("Hướng dẫn", "Mở COMBAT_TOOLS_GUIDE.md"), EditorStyles.miniButton))
        {
            EditorUtility.OpenWithDefaultApp(System.IO.Path.GetFullPath("COMBAT_TOOLS_GUIDE.md"));
        }
        GUILayout.EndArea();
    }

    private static void Ping(UnityEngine.Object target)
    {
        if (target == null) return;
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private void DrawFooter(Rect rect)
    {
        StudioGUI.Fill(rect, StudioGUI.Header);
        StudioGUI.Fill(new Rect(rect.x, rect.y, rect.width, 1f), StudioGUI.Border);
        if (string.IsNullOrEmpty(_status))
        {
            GUI.Label(new Rect(rect.x + 12f, rect.y + 3f, rect.width - 24f, 16f),
                "Chọn nhân vật ở ô trên cùng · làm lần lượt các bước bên trái · chấm vàng/đỏ = bước đó còn thiếu",
                StudioGUI.MutedStyle);
            return;
        }
        StudioGUI.DrawDot(new Rect(rect.x + 12f, rect.y + 7f, 8f, 8f), StudioGUI.For(_statusState));
        GUI.Label(new Rect(rect.x + 26f, rect.y + 3f, rect.width - 40f, 16f), _status, StudioGUI.LabelStyle);
    }

    private void DrawContent(Rect rect)
    {
        if (_page != Page.NewCharacter && Set == null)
        {
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            DrawEmptyState();
            GUILayout.EndArea();
            return;
        }

        // trang nhúng tool cũ: tự lo bố cục, chỉ chừa lề
        switch (_page)
        {
            case Page.Timing:
                DrawTimingPage(rect, 0);
                return;
            case Page.Sound:
                DrawTimingPage(rect, 1);
                return;
            case Page.Grip:
                DrawGripPage(rect);
                return;
            case Page.Moveset when _movesetTab == 1:
                DrawClonePage(rect);
                return;
        }

        GUILayout.BeginArea(new Rect(rect.x + 22f, rect.y + 16f, rect.width - 40f, rect.height - 20f));
        _scroll = GUILayout.BeginScrollView(_scroll);
        try
        {
            switch (_page)
            {
                case Page.Overview: DrawOverviewPage(); break;
                case Page.Prefab: DrawPrefabPage(); break;
                case Page.Moveset: DrawMovesetPage(); break;
                case Page.Combo: DrawComboPage(); break;
                case Page.Build: DrawBuildPage(); break;
                case Page.NewCharacter: DrawNewCharacterPage(); break;
            }
            GUILayout.Space(20f);
        }
        finally
        {
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

    private void DrawEmptyState()
    {
        StudioGUI.PageTitle("Chưa có nhân vật nào", "Studio đọc nhân vật từ AnimSetLibrary (mỗi bộ anim = 1 nhân vật).");
        StudioGUI.BeginCard("Bắt đầu");
        if (_sets == null)
        {
            StudioGUI.Note("Chưa có AnimSetLibrary trong project.", State.Error);
            GUILayout.BeginHorizontal();
            if (StudioGUI.Primary("Tạo AnimSetLibrary rỗng", null, 200f)) { AnimSetCloneTool.CreateEmptyLibrary(); ReloadLibraries(); }
            if (StudioGUI.Button("Nhập bộ Dante từ code", "Tạo lại AnimSetLibrary từ bảng hard-code (lần đầu)", 180f))
            {
                LocomotionAnimatorBuilder.CreateLibraryFromCode();
                ReloadLibraries();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else if (StudioGUI.Primary("＋ Tạo nhân vật đầu tiên", null, 220f))
        {
            BeginNewCharacter();
        }
        StudioGUI.EndCard();
    }

    #endregion

    #region Trang nhúng tool cũ

    private T EnsurePanel<T>(ref T panel) where T : EditorWindow
    {
        if (panel != null) return panel;
        panel = CreateInstance<T>();
        panel.hideFlags = HideFlags.HideAndDontSave;
        return panel;
    }

    private static void DestroyOrphans<T>() where T : EditorWindow
    {
        foreach (T panel in Resources.FindObjectsOfTypeAll<T>())
        {
            if ((panel.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(panel);
        }
    }

    private static void DestroyPanel<T>(ref T panel) where T : EditorWindow
    {
        if (panel != null) DestroyImmediate(panel);
        panel = null;
    }

    private void DrawTimingPage(Rect rect, int tab)
    {
        CombatTimingWindow timing = EnsurePanel(ref _timing);
        timing.Host = this;
        timing.ForcedTab = tab;
        timing.SetCharacter(_setIndex, Set.Prefab);

        Rect inner = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 8f);
        if (tab == 1)
        {
            inner = new Rect(rect.x + 22f, rect.y + 16f, rect.width - 40f, rect.height - 20f);
            GUILayout.BeginArea(new Rect(inner.x, inner.y, inner.width, 56f));
            StudioGUI.PageTitle("Âm thanh", "Bộ SFX của CombatEffects trong scene — cùng bộ clip MeleeHitDetector phát lúc chạy game.");
            GUILayout.EndArea();
            inner.yMin += 56f;
        }
        timing.DrawEmbedded(inner);
    }

    private void DrawGripPage(Rect rect)
    {
        WeaponGripTool grip = EnsurePanel(ref _grip);
        grip.Host = this;

        List<WeaponDefinition> weapons = CharacterWeapons();
        Rect bar = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 28f);
        GUILayout.BeginArea(bar);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Vũ khí:", StudioGUI.SectionStyle, GUILayout.Width(52f));
        if (weapons.Count == 0) GUILayout.Label("nhân vật chưa có vũ khí — thêm ở trang Vũ khí & Combo", StudioGUI.MutedStyle);
        else _weaponTab = StudioGUI.Pills(Mathf.Clamp(_weaponTab, 0, weapons.Count - 1), weapons.Select(w => w.DisplayName).ToArray(), 22f);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        WeaponDefinition weapon = weapons.Count > 0 ? weapons[Mathf.Clamp(_weaponTab, 0, weapons.Count - 1)] : null;
        WeaponData data = LoadWeaponData(weapon);
        grip.SetTargets(Set.Prefab, data, weapon != null ? weapon.AnimFolder : Set.MovementFolder);

        Rect inner = new Rect(rect.x + 4f, bar.yMax + 4f, rect.width - 8f, rect.yMax - bar.yMax - 6f);
        if (weapon != null && data == null)
        {
            GUILayout.BeginArea(inner);
            StudioGUI.BeginCard(State.Warn, "Chưa có WD_" + weapon.Key, "Build vũ khí trước để có WeaponData lưu cách cầm.");
            if (StudioGUI.Primary("Tới trang Build", null, 160f)) _page = Page.Build;
            StudioGUI.EndCard();
            GUILayout.EndArea();
            return;
        }
        grip.DrawEmbedded(inner);
    }

    private void DrawClonePage(Rect rect)
    {
        AnimSetCloneWindow clone = EnsurePanel(ref _clone);
        clone.Host = this;
        clone.PresetFor(_setIndex);

        Rect bar = new Rect(rect.x + 22f, rect.y + 16f, rect.width - 40f, 30f);
        GUILayout.BeginArea(bar);
        DrawMovesetTabs();
        GUILayout.EndArea();
        clone.DrawEmbedded(new Rect(rect.x + 12f, bar.yMax + 4f, rect.width - 24f, rect.yMax - bar.yMax - 6f));
    }

    #endregion
}
