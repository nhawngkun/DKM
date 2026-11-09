using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using State = StudioGUI.State;

// Trang Vũ khí & Combo + trình tạo nhân vật mới.
public partial class CharacterStudioWindow
{
    private const string InplaceSuffix = "_Inplace";

    private readonly HashSet<int> _openChains = new HashSet<int>();
    private int _openChainsWeapon = -1;

    #region Vũ khí & Combo

    private static Color ContextColor(AttackContext context)
    {
        switch (context)
        {
            case AttackContext.Ground: return StudioGUI.Accent;
            case AttackContext.Dash: return StudioGUI.Hex(0x3CC8C8);
            case AttackContext.Air: return StudioGUI.Purple;
            case AttackContext.Launcher: return StudioGUI.Warn;
            default: return StudioGUI.Ok;
        }
    }

    private static string ContextHint(AttackContext context)
    {
        switch (context)
        {
            case AttackContext.Ground: return "đứng / đi trên đất";
            case AttackContext.Dash: return "đang chạy nhanh / vừa dash / lock-on + đẩy tới";
            case AttackContext.Air: return "trên không";
            case AttackContext.Launcher: return "lock-on + kéo lùi + đánh (hất tung)";
            default: return "nút kỹ năng";
        }
    }

    private void DrawComboPage()
    {
        AnimSetDefinition set = Set;
        StudioGUI.PageTitle("Vũ khí & Combo", "Mỗi vũ khí = thư mục anim đòn đánh + danh sách chuỗi. Sửa xong vào Build → Build vũ khí của nhân vật.");
        DrawBrokenFoldersCard();

        if (_weaponLibrary == null)
        {
            StudioGUI.BeginCard(State.Error, "Chưa có WeaponLibrary");
            if (StudioGUI.Primary("Tạo từ bảng Dante (Rebellion, Sparda)", null, 280f))
            {
                WeaponComboBuilder.CreateLibraryFromCode();
                ReloadLibraries();
            }
            StudioGUI.EndCard();
            return;
        }

        List<WeaponDefinition> weapons = CharacterWeapons();
        GUILayout.BeginHorizontal();
        if (weapons.Count > 0)
        {
            int tab = StudioGUI.Pills(Mathf.Clamp(_weaponTab, 0, weapons.Count - 1), weapons.Select(w => w.DisplayName).ToArray());
            if (tab != _weaponTab) { _weaponTab = tab; GUI.FocusControl(null); }
        }
        else GUILayout.Label("Nhân vật chưa có vũ khí.", StudioGUI.MutedStyle);
        GUILayout.FlexibleSpace();
        if (StudioGUI.Button("＋ Vũ khí", "Thêm vũ khí mới cho nhân vật", 90f)) AddWeapon(set);
        if (StudioGUI.Button("Gắn vũ khí có sẵn…", "Chuyển vũ khí của bộ khác sang nhân vật này (đổi AnimSetId)", 140f)) ShowAdoptMenu();
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);

        if (weapons.Count == 0) return;
        _weaponTab = Mathf.Clamp(_weaponTab, 0, weapons.Count - 1);
        WeaponDefinition weapon = weapons[_weaponTab];
        int weaponIndex = _weaponLibrary.Weapons.IndexOf(weapon);
        if (weaponIndex != _openChainsWeapon)
        {
            _openChainsWeapon = weaponIndex;
            _openChains.Clear();
        }

        DrawWeaponInfo(weapon);
        DrawChains(weapon);
    }

    private void DrawWeaponInfo(WeaponDefinition weapon)
    {
        WeaponData data = LoadWeaponData(weapon);
        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(weapon.AnimFolder);

        StudioGUI.BeginCard();
        GUILayout.BeginHorizontal();
        GUILayout.Label(weapon.DisplayName, StudioGUI.TitleStyle);
        StudioGUI.Chip(weapon.Type.ToString(), StudioGUI.Purple);
        if (data != null) StudioGUI.Chip("đã build · WD_" + weapon.Key, StudioGUI.Ok);
        else StudioGUI.Chip("chưa build", StudioGUI.Warn);
        GUILayout.FlexibleSpace();
        if (data != null && StudioGUI.Button("Chọn WD", "Chọn WeaponData trong Project", 80f)) Ping(data);
        if (StudioGUI.Danger("Xoá vũ khí", null, 90f) &&
            EditorUtility.DisplayDialog("Xoá " + weapon.DisplayName + "?", "Xoá vũ khí khỏi WeaponLibrary (asset WD_ đã build vẫn giữ).", "Xoá", "Huỷ"))
        {
            Undo.RecordObject(_weaponLibrary, "Xoá vũ khí");
            _weaponLibrary.Weapons.Remove(weapon);
            EditorUtility.SetDirty(_weaponLibrary);
            _weaponTab = 0;
            Invalidate();
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 150f;
        TextRow(_weaponLibrary, "Tên hiển thị", weapon.DisplayName, value => weapon.DisplayName = value);
        string key = EditorGUILayout.DelayedTextField(new GUIContent("Key", "Tiền tố state đòn đánh + tên asset WD_<Key>"), weapon.Key);
        if (key != weapon.Key)
        {
            key = Regex.Replace(key ?? string.Empty, @"[^A-Za-z0-9_]", string.Empty);
            if (_weaponLibrary.Weapons.Any(w => w != weapon && string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase)))
                SetStatus("Key " + key + " đã có vũ khí khác dùng.", State.Error);
            else if (!string.IsNullOrEmpty(key))
                Change(_weaponLibrary, "Sửa Key", weapon.Key, key, value => weapon.Key = value);
        }
        Change(_weaponLibrary, "Sửa loại", weapon.Type, (WeaponType)EditorGUILayout.EnumPopup(new GUIContent("Loại", "Magic = tay không, bắn phép (cần SpellCaster)"), weapon.Type), value => weapon.Type = value);
        FolderRow(_weaponLibrary, "Thư mục anim đòn", weapon.AnimFolder, value => weapon.AnimFolder = value);
        Change(_weaponLibrary, "Sửa model", weapon.Model,
            (GameObject)EditorGUILayout.ObjectField(new GUIContent("Model cầm trong game", "Trống = dùng Prefab gốc"), weapon.Model, typeof(GameObject), false), value => weapon.Model = value);
        Change(_weaponLibrary, "Sửa prefab gốc", weapon.Prefab,
            (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab gốc (đo mũi kiếm)", "Prefab vũ khí của pack anim — chỉ dùng để phân tích quỹ đạo lưỡi"), weapon.Prefab, typeof(GameObject), false), value => weapon.Prefab = value);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Clip idle", "Clip đứng yên khi cầm vũ khí"), GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
        ClipButton(_weaponLibrary, weapon.IdleClip, clips, value => weapon.IdleClip = value);
        GUILayout.EndHorizontal();
        Change(_weaponLibrary, "Sửa reset", weapon.ComboResetTime,
            EditorGUILayout.FloatField(new GUIContent("Reset combo (giây)", "Không bấm tiếp quá thời gian này -> quay về đòn 1"), weapon.ComboResetTime), value => weapon.ComboResetTime = value);
        EditorGUIUtility.labelWidth = labelWidth;

        if (!clips.IsValid || clips.Names.Count == 0) StudioGUI.Note("Thư mục anim đòn chưa có clip nào.", State.Error);
        else StudioGUI.Note($"{clips.Names.Count} clip trong thư mục.", State.None);
        StudioGUI.EndCard();
    }

    private void DrawChains(WeaponDefinition weapon)
    {
        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(weapon.AnimFolder);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"CHUỖI ĐÒN  ·  {weapon.Chains.Count}", StudioGUI.SectionStyle);
        GUILayout.FlexibleSpace();
        if (StudioGUI.Button("Mở hết", null, 70f)) { for (int i = 0; i < weapon.Chains.Count; i++) _openChains.Add(i); }
        if (StudioGUI.Button("Thu hết", null, 70f)) _openChains.Clear();
        if (StudioGUI.Button("✨ Gợi ý từ tên clip", "Tìm clip dạng attack01, combo02_3... và tạo chuỗi còn thiếu", 150f)) AddSuggestedChains(weapon);
        if (StudioGUI.Primary("＋ Chuỗi", null, 80f))
        {
            Undo.RecordObject(_weaponLibrary, "Thêm chuỗi");
            weapon.Chains.Add(new WeaponChainDefinition { Name = "Chuỗi " + (weapon.Chains.Count + 1) });
            _openChains.Add(weapon.Chains.Count - 1);
            EditorUtility.SetDirty(_weaponLibrary);
            Invalidate();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        int move = -1, moveDirection = 0, remove = -1;
        for (int i = 0; i < weapon.Chains.Count; i++)
        {
            WeaponChainDefinition chain = weapon.Chains[i];
            int missing = chain.Steps.Count(s => string.IsNullOrEmpty(s.Clip) || !clips.Contains(s.Clip) || (!string.IsNullOrEmpty(s.Root) && !clips.Contains(s.Root)));
            bool open = _openChains.Contains(i);

            StudioGUI.BeginCard();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(open ? "▾" : "▸", EditorStyles.label, GUILayout.Width(14f)))
            {
                if (open) _openChains.Remove(i);
                else _openChains.Add(i);
            }
            StudioGUI.Dot(chain.Steps.Count == 0 ? State.Error : missing > 0 ? State.Warn : State.Ok);
            Change(_weaponLibrary, "Sửa tên chuỗi", chain.Name, EditorGUILayout.TextField(chain.Name, GUILayout.MinWidth(120f), GUILayout.MaxWidth(240f)), value => chain.Name = value);
            Change(_weaponLibrary, "Sửa ngữ cảnh", chain.Context, (AttackContext)EditorGUILayout.EnumPopup(chain.Context, GUILayout.Width(90f)), value => chain.Context = value);
            StudioGUI.Chip(ContextHint(chain.Context), ContextColor(chain.Context));
            GUILayout.FlexibleSpace();
            GUILayout.Label(chain.Steps.Count + " đòn" + (missing > 0 ? $" · {missing} sai clip" : ""), missing > 0 ? new GUIStyle(StudioGUI.MutedStyle) { normal = { textColor = StudioGUI.Warn } } : StudioGUI.MutedStyle, GUILayout.ExpandWidth(false));
            if (StudioGUI.Icon("▲", "Đưa lên")) { move = i; moveDirection = -1; }
            if (StudioGUI.Icon("▼", "Đưa xuống")) { move = i; moveDirection = 1; }
            if (StudioGUI.Icon("✕", "Xoá chuỗi")) remove = i;
            GUILayout.EndHorizontal();

            if (open) DrawSteps(chain, clips);
            StudioGUI.EndCard();
        }

        if (remove >= 0 && EditorUtility.DisplayDialog("Xoá chuỗi?", "Xoá chuỗi " + weapon.Chains[remove].Name + "?", "Xoá", "Huỷ"))
        {
            Undo.RecordObject(_weaponLibrary, "Xoá chuỗi");
            weapon.Chains.RemoveAt(remove);
            _openChains.Clear();
            EditorUtility.SetDirty(_weaponLibrary);
            Invalidate();
        }
        if (move >= 0) MoveItem(weapon.Chains, move, moveDirection, "Đổi thứ tự chuỗi");
    }

    private void DrawSteps(WeaponChainDefinition chain, AnimSetCloneTool.ClipIndex clips)
    {
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(34f);
        GUILayout.Label(new GUIContent("Clip phát", "Bản Inplace / ZeroHeight phát trên Animator"), StudioGUI.SectionStyle, GUILayout.MinWidth(160f), GUILayout.ExpandWidth(true));
        GUILayout.Label(new GUIContent("Clip root motion", "Bản có di chuyển để lấy quãng lướt. Trống = chính clip phát"), StudioGUI.SectionStyle, GUILayout.MinWidth(160f), GUILayout.ExpandWidth(true));
        GUILayout.Label(new GUIContent("Hất", "Dùng root motion dọc (hất tung / nhảy lên)"), StudioGUI.SectionStyle, GUILayout.Width(30f));
        GUILayout.Space(74f);
        GUILayout.EndHorizontal();

        int move = -1, moveDirection = 0, remove = -1;
        for (int i = 0; i < chain.Steps.Count; i++)
        {
            WeaponStepDefinition step = chain.Steps[i];
            GUILayout.BeginHorizontal(GUILayout.Height(20f));
            Rect badge = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20f));
            StudioGUI.Badge(badge, (i + 1).ToString(), i == chain.Steps.Count - 1 ? StudioGUI.Error : StudioGUI.Border);
            GUILayout.Space(4f);
            ClipButton(_weaponLibrary, step.Clip, clips, picked => AssignStepClip(step, picked, clips));
            ClipButton(_weaponLibrary, step.Root, clips, value => step.Root = value);
            Change(_weaponLibrary, "Sửa hất", step.Vertical, GUILayout.Toggle(step.Vertical, GUIContent.none, GUILayout.Width(30f)), value => step.Vertical = value);
            if (StudioGUI.Icon("▲", "Đưa lên")) { move = i; moveDirection = -1; }
            if (StudioGUI.Icon("▼", "Đưa xuống")) { move = i; moveDirection = 1; }
            if (StudioGUI.Icon("✕", "Xoá đòn")) remove = i;
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(24f);
        if (StudioGUI.Button("＋ Đòn", null, 70f))
        {
            Undo.RecordObject(_weaponLibrary, "Thêm đòn");
            chain.Steps.Add(new WeaponStepDefinition());
            EditorUtility.SetDirty(_weaponLibrary);
            Invalidate();
        }
        GUILayout.Label("Chọn clip phát mà có bản '_Inplace' thì cột root motion tự điền. Đòn cuối (đỏ) = đòn chốt.", StudioGUI.MutedStyle);
        GUILayout.EndHorizontal();

        if (remove >= 0)
        {
            Undo.RecordObject(_weaponLibrary, "Xoá đòn");
            chain.Steps.RemoveAt(remove);
            EditorUtility.SetDirty(_weaponLibrary);
            Invalidate();
        }
        if (move >= 0) MoveItem(chain.Steps, move, moveDirection, "Đổi thứ tự đòn");
    }

    // chọn clip phát: tự ghép cặp bản Inplace (phát) + bản gốc (root motion)
    private void AssignStepClip(WeaponStepDefinition step, string picked, AnimSetCloneTool.ClipIndex clips)
    {
        Undo.RecordObject(_weaponLibrary, "Chọn clip đòn");
        if (string.IsNullOrEmpty(picked))
        {
            step.Clip = string.Empty;
        }
        else if (picked.EndsWith(InplaceSuffix, StringComparison.Ordinal))
        {
            step.Clip = picked;
            string root = picked.Substring(0, picked.Length - InplaceSuffix.Length);
            if (clips.Contains(root)) step.Root = root;
        }
        else if (clips.Contains(picked + InplaceSuffix))
        {
            step.Clip = picked + InplaceSuffix;
            step.Root = picked;
        }
        else
        {
            step.Clip = picked;
        }
        EditorUtility.SetDirty(_weaponLibrary);
        Invalidate();
    }

    private void MoveItem<T>(List<T> list, int index, int direction, string undo)
    {
        int target = index + direction;
        if (target < 0 || target >= list.Count) return;
        Undo.RecordObject(_weaponLibrary, undo);
        (list[index], list[target]) = (list[target], list[index]);
        bool openA = _openChains.Contains(index), openB = _openChains.Contains(target);
        if (typeof(T) == typeof(WeaponChainDefinition))
        {
            _openChains.Remove(index); _openChains.Remove(target);
            if (openA) _openChains.Add(target);
            if (openB) _openChains.Add(index);
        }
        EditorUtility.SetDirty(_weaponLibrary);
        Invalidate();
    }

    private void AddWeapon(AnimSetDefinition set)
    {
        string baseKey = Regex.Replace(set.DisplayName ?? "Weapon", @"[^A-Za-z0-9]", string.Empty);
        if (string.IsNullOrEmpty(baseKey)) baseKey = "Weapon";
        string key = baseKey;
        for (int n = 2; _weaponLibrary.Weapons.Any(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase)); n++) key = baseKey + n;

        Undo.RecordObject(_weaponLibrary, "Thêm vũ khí");
        _weaponLibrary.Weapons.Add(new WeaponDefinition
        {
            Key = key,
            DisplayName = set.DisplayName + " - vũ khí",
            AnimFolder = set.MovementFolder,
            AnimSetId = _setIndex,
            IdleClip = set.Idle != null ? set.Idle.Clip : "Idle",
        });
        EditorUtility.SetDirty(_weaponLibrary);
        _weaponTab = CharacterWeapons().Count - 1;
        Invalidate();
        SetStatus("Đã thêm vũ khí " + key + ". Trỏ thư mục anim đòn rồi bấm ✨ Gợi ý từ tên clip.");
    }

    private void ShowAdoptMenu()
    {
        GenericMenu menu = new GenericMenu();
        foreach (WeaponDefinition weapon in _weaponLibrary.Weapons)
        {
            if (weapon.AnimSetId == _setIndex) continue;
            string owner = _sets != null && weapon.AnimSetId >= 0 && weapon.AnimSetId < _sets.Sets.Count ? _sets.Sets[weapon.AnimSetId].DisplayName : "bộ " + weapon.AnimSetId;
            WeaponDefinition captured = weapon;
            menu.AddItem(new GUIContent($"{weapon.DisplayName}  ({owner})"), false, () =>
            {
                Undo.RecordObject(_weaponLibrary, "Chuyển vũ khí");
                captured.AnimSetId = _setIndex;
                EditorUtility.SetDirty(_weaponLibrary);
                Invalidate();
                SetStatus($"{captured.DisplayName} giờ thuộc {Set.DisplayName}. Build lại vũ khí để WD_ nhận AnimSetId mới.", State.Warn);
            });
        }
        if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("(không có vũ khí nào khác)"));
        menu.ShowAsContext();
    }

    private void AddSuggestedChains(WeaponDefinition weapon)
    {
        List<WeaponChainDefinition> suggested = SuggestChains(weapon.AnimFolder);
        Undo.RecordObject(_weaponLibrary, "Gợi ý chuỗi");
        int added = 0;
        foreach (WeaponChainDefinition chain in suggested)
        {
            bool exists = weapon.Chains.Any(c => string.Equals(c.Name, chain.Name, StringComparison.OrdinalIgnoreCase) ||
                                                 (c.Steps.Count > 0 && chain.Steps.Count > 0 && c.Steps[0].Clip == chain.Steps[0].Clip));
            if (exists) continue;
            weapon.Chains.Add(chain);
            added++;
        }
        EditorUtility.SetDirty(_weaponLibrary);
        Invalidate();
        SetStatus(added > 0 ? $"Đã thêm {added} chuỗi gợi ý — kiểm tra ngữ cảnh (Ground/Air/Dash...) từng chuỗi." : "Không tìm thấy chuỗi mới nào theo tên clip.", added > 0 ? State.Ok : State.Warn);
    }

    /// <summary>
    /// Đoán chuỗi combo từ tên clip: attack01, attack02 -> 1 chuỗi; combo02_1..combo02_5 -> 1 chuỗi.
    /// Ngữ cảnh đoán theo từ khoá (air/jump/fly, dash, skill/buff).
    /// </summary>
    private static List<WeaponChainDefinition> SuggestChains(string folder)
    {
        var result = new List<WeaponChainDefinition>();
        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(folder);
        if (clips == null || !clips.IsValid) return result;

        var groups = new SortedDictionary<string, List<(int number, string clip)>>(StringComparer.OrdinalIgnoreCase);
        Regex pattern = new Regex(@"^(?<base>.*?)(?<num>\d+)$");
        foreach (string name in clips.Names)
        {
            if (name.EndsWith(InplaceSuffix, StringComparison.Ordinal)) continue;
            string lower = name.ToLowerInvariant();
            if (!(lower.Contains("attack") || lower.Contains("combo") || lower.Contains("atk") || lower.Contains("slash") || lower.Contains("skill"))) continue;
            Match match = pattern.Match(name);
            if (!match.Success) continue;
            string key = match.Groups["base"].Value.TrimEnd('_', '-', ' ');
            if (string.IsNullOrEmpty(key)) continue;
            if (!groups.TryGetValue(key, out var steps)) groups[key] = steps = new List<(int, string)>();
            steps.Add((int.Parse(match.Groups["num"].Value), name));
        }

        foreach (var group in groups)
        {
            string lower = group.Key.ToLowerInvariant();
            AttackContext context = lower.Contains("air") || lower.Contains("jump") || lower.Contains("fly") ? AttackContext.Air
                : lower.Contains("dash") ? AttackContext.Dash
                : lower.Contains("skill") || lower.Contains("buff") ? AttackContext.Skill
                : AttackContext.Ground;

            var chain = new WeaponChainDefinition { Name = Pretty(group.Key), Context = context };
            foreach (var step in group.Value.OrderBy(s => s.number))
            {
                bool inplace = clips.Contains(step.clip + InplaceSuffix);
                chain.Steps.Add(new WeaponStepDefinition
                {
                    Clip = inplace ? step.clip + InplaceSuffix : step.clip,
                    Root = inplace ? step.clip : string.Empty,
                });
            }
            result.Add(chain);
        }
        return result;
    }

    private static string Pretty(string key)
    {
        string text = key.Replace('_', ' ').Trim();
        return text.Length == 0 ? key : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    #endregion

    #region Nhân vật mới

    [SerializeField] private string _newName = "Nhân vật mới";
    [SerializeField] private string _newPrefix = "NV_";
    [SerializeField] private string _newFolder = string.Empty;
    [SerializeField] private int _newTemplate;
    [SerializeField] private bool _newKeepTemplate = true;
    [SerializeField] private GameObject _newPrefab;
    [SerializeField] private bool _newWeapon = true;
    [SerializeField] private string _newWeaponKey = string.Empty;
    [SerializeField] private WeaponType _newWeaponType = WeaponType.Yamato;
    [SerializeField] private GameObject _newWeaponModel;
    [SerializeField] private string _newWeaponFolder = string.Empty;
    [SerializeField] private bool _newSuggestChains = true;

    private string _previewKey;
    private string _previewSummary;

    private void BeginNewCharacter() => BeginNewCharacter(_setIndex);

    private void BeginNewCharacter(int template)
    {
        _newName = "Nhân vật mới";
        _newPrefix = _sets != null ? AnimSetCloneTool.SuggestPrefix(_sets, _newName) : "NV_";
        _newFolder = string.Empty;
        _newWeaponFolder = string.Empty;
        _newTemplate = Mathf.Max(template, 0);
        _newPrefab = null;
        _newWeaponKey = string.Empty;
        _newWeaponModel = null;
        _previewKey = null;
        _page = Page.NewCharacter;
        _scroll = Vector2.zero;
        GUI.FocusControl(null);
    }

    private void DrawNewCharacterPage()
    {
        StudioGUI.PageTitle("Nhân vật mới", "Nhân bản moveset từ một nhân vật có sẵn, tự map clip theo tên trong thư mục anim mới, tạo luôn vũ khí + chuỗi combo gợi ý.");

        if (_sets == null)
        {
            StudioGUI.BeginCard(State.Error, "Chưa có AnimSetLibrary");
            if (StudioGUI.Primary("Tạo AnimSetLibrary rỗng", null, 200f)) { AnimSetCloneTool.CreateEmptyLibrary(); ReloadLibraries(); }
            StudioGUI.EndCard();
            return;
        }

        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 170f;

        StudioGUI.BeginCard("1 · Nhân vật");
        string name = EditorGUILayout.TextField("Tên nhân vật", _newName);
        if (name != _newName)
        {
            _newName = name;
            _newPrefix = AnimSetCloneTool.SuggestPrefix(_sets, _newName);
        }
        _newPrefix = AnimSetCloneTool.NormalizePrefix(EditorGUILayout.TextField(new GUIContent("Prefix state", "vd VG_ -> VG_Idle"), _newPrefix));
        if (AnimSetCloneTool.PrefixExists(_sets, _newPrefix)) StudioGUI.Note("Prefix này đã có bộ khác dùng.", State.Error);
        _newPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab (tuỳ chọn)", "Có thể gán sau ở trang Prefab"), _newPrefab, typeof(GameObject), false);
        StudioGUI.EndCard();

        StudioGUI.BeginCard("2 · Moveset");
        string[] templates = _sets.Sets.Select(s => $"{s.DisplayName}  [{s.Prefix}]").ToArray();
        if (templates.Length > 0) _newTemplate = EditorGUILayout.Popup(new GUIContent("Khuôn", "Bộ anim có sẵn để nhân bản danh sách state"), Mathf.Clamp(_newTemplate, 0, templates.Length - 1), templates);
        else StudioGUI.Note("Library chưa có bộ nào làm khuôn — sẽ tạo bộ rỗng.", State.Warn);
        _newFolder = AnimSetCloneTool.NormalizeFolder(StudioGUI.FolderField("Thư mục anim di chuyển", _newFolder));
        _newKeepTemplate = EditorGUILayout.Toggle(new GUIContent("Giữ clip khuôn khi thiếu", "State không tìm được clip mới thì dùng clip của khuôn (thêm thư mục khuôn làm dự phòng)"), _newKeepTemplate);
        StudioGUI.EndCard();

        StudioGUI.BeginCard("3 · Vũ khí");
        _newWeapon = EditorGUILayout.Toggle("Tạo vũ khí", _newWeapon);
        if (_newWeapon)
        {
            if (string.IsNullOrEmpty(_newWeaponKey)) _newWeaponKey = Regex.Replace(_newName, @"[^A-Za-z0-9]", string.Empty);
            _newWeaponKey = Regex.Replace(EditorGUILayout.TextField(new GUIContent("Key", "WD_<Key>"), _newWeaponKey), @"[^A-Za-z0-9_]", string.Empty);
            if (_weaponLibrary != null && _weaponLibrary.Weapons.Any(w => string.Equals(w.Key, _newWeaponKey, StringComparison.OrdinalIgnoreCase)))
                StudioGUI.Note("Key này đã có vũ khí khác dùng.", State.Error);
            _newWeaponType = (WeaponType)EditorGUILayout.EnumPopup("Loại", _newWeaponType);
            if (_newWeaponType != WeaponType.Magic)
                _newWeaponModel = (GameObject)EditorGUILayout.ObjectField("Model vũ khí", _newWeaponModel, typeof(GameObject), false);
            _newWeaponFolder = AnimSetCloneTool.NormalizeFolder(StudioGUI.FolderField(new GUIContent("Thư mục anim đòn").text, string.IsNullOrEmpty(_newWeaponFolder) ? _newFolder : _newWeaponFolder));
            _newSuggestChains = EditorGUILayout.Toggle("Gợi ý chuỗi từ tên clip", _newSuggestChains);
        }
        StudioGUI.EndCard();
        EditorGUIUtility.labelWidth = labelWidth;

        // xem trước kết quả map
        UpdateNewPreview();
        StudioGUI.BeginCard("Xem trước");
        StudioGUI.Note(_previewSummary, State.None);
        StudioGUI.EndCard();

        bool valid = !string.IsNullOrEmpty(_newName) && !string.IsNullOrEmpty(_newPrefix) && !AnimSetCloneTool.PrefixExists(_sets, _newPrefix) &&
                     AssetDatabase.IsValidFolder(_newFolder) &&
                     (!_newWeapon || (!string.IsNullOrEmpty(_newWeaponKey) && (_weaponLibrary == null || !_weaponLibrary.Weapons.Any(w => string.Equals(w.Key, _newWeaponKey, StringComparison.OrdinalIgnoreCase)))));
        GUILayout.BeginHorizontal();
        GUI.enabled = valid;
        if (StudioGUI.Primary("Tạo nhân vật", null, 160f)) CreateCharacter();
        GUI.enabled = true;
        if (StudioGUI.Button("Huỷ", null, 80f)) _page = Page.Overview;
        if (!valid) GUILayout.Label("Cần tên, prefix chưa dùng và thư mục anim hợp lệ.", StudioGUI.MutedStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void UpdateNewPreview()
    {
        string weaponFolder = string.IsNullOrEmpty(_newWeaponFolder) ? _newFolder : _newWeaponFolder;
        string key = $"{_newFolder}|{_newTemplate}|{weaponFolder}|{_newWeapon}|{_newSuggestChains}";
        if (key == _previewKey) return;
        _previewKey = key;

        if (!AssetDatabase.IsValidFolder(_newFolder))
        {
            _previewSummary = "Chọn thư mục anim di chuyển để xem trước.";
            return;
        }
        AnimSetDefinition draft = BuildDraft(out int mapped, out int total);
        string text = $"Thư mục có {AnimSetCloneTool.ScanClips(_newFolder).Names.Count} clip · tự map được {mapped}/{total} dòng moveset";
        if (_newKeepTemplate) text += $" · {total - mapped} dòng giữ clip khuôn";
        if (_newWeapon && _newSuggestChains)
        {
            List<WeaponChainDefinition> chains = SuggestChains(weaponFolder);
            text += $" · gợi ý {chains.Count} chuỗi ({chains.Sum(c => c.Steps.Count)} đòn)";
        }
        _previewSummary = draft != null ? text : "Không tạo được bộ nháp.";
    }

    private AnimSetDefinition BuildDraft(out int mapped, out int total)
    {
        mapped = 0;
        total = 0;
        AnimSetDefinition template = _sets.Sets.Count > 0 ? _sets.Sets[Mathf.Clamp(_newTemplate, 0, _sets.Sets.Count - 1)] : null;
        AnimSetDefinition set = AnimSetCloneTool.CloneSet(template, _newPrefix, _newName, _newFolder);
        set.ExtraFolders.Clear();
        if (template != null && _newKeepTemplate)
        {
            foreach (string folder in AnimSetCloneTool.TemplateFolders(template))
            {
                string clean = AnimSetCloneTool.NormalizeFolder(folder);
                if (clean != set.MovementFolder && !set.ExtraFolders.Contains(clean)) set.ExtraFolders.Add(clean);
            }
        }

        AnimSetCloneTool.ClipIndex clips = AnimSetCloneTool.ScanClips(_newFolder);
        int count = 0, hit = 0;
        string Map(string old, string semantic, float x, float y)
        {
            count++;
            string suggestion = AnimSetCloneTool.SuggestClip(old, semantic, x, y, clips, out float score);
            if (!string.IsNullOrEmpty(suggestion) && score >= 0.5f) { hit++; return suggestion; }
            return _newKeepTemplate ? old : string.Empty;
        }

        foreach (LocoClipDefinition row in set.OneShots) row.Clip = Map(row.Clip, row.State.ToString(), 0f, 0f);
        foreach (GaitClipDefinition row in set.Gaits) row.Clip = Map(row.Clip, string.Empty, 0f, 1f);
        foreach (DirectionalClipDefinition row in set.Strafe) row.Clip = Map(row.Clip, string.Empty, row.X, row.Y);
        foreach (DirectionalClipDefinition row in set.Crouch) row.Clip = Map(row.Clip, string.Empty, row.X, row.Y);
        mapped = hit;
        total = count;
        return set;
    }

    private void CreateCharacter()
    {
        AnimSetCloneTool.ClearCache();
        AnimSetDefinition set = BuildDraft(out int mapped, out int total);
        set.Prefab = _newPrefab;
        int index = AnimSetCloneTool.Write(_sets, set, -1);
        if (index < 0)
        {
            SetStatus("Không ghi được bộ anim.", State.Error);
            return;
        }

        string weaponNote = string.Empty;
        if (_newWeapon)
        {
            if (_weaponLibrary == null)
            {
                _weaponLibrary = WeaponComboBuilder.CreateLibraryFromCode();
            }
            string weaponFolder = string.IsNullOrEmpty(_newWeaponFolder) ? _newFolder : _newWeaponFolder;
            var weapon = new WeaponDefinition
            {
                Type = _newWeaponType,
                Key = _newWeaponKey,
                DisplayName = _newName,
                AnimFolder = weaponFolder,
                Prefab = _newWeaponType == WeaponType.Magic ? null : _newWeaponModel,
                Model = _newWeaponType == WeaponType.Magic ? null : _newWeaponModel,
                IdleClip = set.Idle != null && !string.IsNullOrEmpty(set.Idle.Clip) ? set.Idle.Clip : "Idle",
                AnimSetId = index,
            };
            if (_newSuggestChains) weapon.Chains = SuggestChains(weaponFolder);
            Undo.RecordObject(_weaponLibrary, "Thêm vũ khí");
            _weaponLibrary.Weapons.Add(weapon);
            EditorUtility.SetDirty(_weaponLibrary);
            weaponNote = $", vũ khí {weapon.Key} ({weapon.Chains.Count} chuỗi)";
        }

        AssetDatabase.SaveAssets();
        Select(index);
        _page = Page.Moveset;
        _movesetTab = 0;
        SetStatus($"Đã tạo {set.DisplayName}: map {mapped}/{total} clip{weaponNote}. Kiểm tra từng trang rồi vào Build.");
    }

    #endregion
}
