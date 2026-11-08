using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Cửa sổ "Anim Set Clone": đọc bộ anim ĐANG DÙNG trong Animator Controller của Dante
// (layer Locomotion), nhân bản rồi thay clip của nhân vật mới. Logic nằm ở AnimSetCloneTool.
public class AnimSetCloneWindow : EditorWindow
{
    [MenuItem("Tools/Combat/Anim Set Clone")]
    public static void Open()
    {
        AnimSetCloneWindow window = GetWindow<AnimSetCloneWindow>(false, "Anim Set Clone", true);
        window.minSize = new Vector2(940f, 620f);
        window.Show();
    }

    // 1 dòng trong bảng map: giữ tham chiếu thẳng tới row của bộ nháp nên sửa là sửa luôn bộ nháp
    private sealed class Row
    {
        public string Label;
        public string Kind;
        public string Semantic;
        public float X;
        public float Y;
        public string Original;
        public LocoClipDefinition Shot;
        public GaitClipDefinition Gait;
        public DirectionalClipDefinition Direction;
        public string Suggestion;
        public float Score;

        public string Clip
        {
            get
            {
                if (Shot != null) return Shot.Clip;
                if (Gait != null) return Gait.Clip;
                return Direction != null ? Direction.Clip : string.Empty;
            }
            set
            {
                string clean = value ?? string.Empty;
                if (Shot != null) Shot.Clip = clean;
                else if (Gait != null) Gait.Clip = clean;
                else if (Direction != null) Direction.Clip = clean;
            }
        }
    }

    private AnimSetLibrary _library;
    private AnimatorController _controller;
    private int _source;                       // 0 = Animator Controller, 1 = AnimSetLibrary
    private readonly List<string> _prefixes = new List<string>();
    private int _prefixIndex;
    private int _mode;                         // 0 = nhân bản, 1 = ghi đè
    private int _templateIndex;
    private string _prefix = "NV_";
    private string _displayName = "Bộ mới";
    private string _folder = string.Empty;
    private bool _folderDirty;                 // đã sửa đường dẫn nhưng chưa quét
    private bool _draftUndoRecorded;

    // Các field dưới đây KHÔNG cho Unity serialize: sau khi compile lại, bộ nháp cũ sẽ thành
    // một bản copy rời (không còn trỏ vào library) và danh sách dòng thì rỗng -> sinh ra
    // lỗi ảo "0 dòng / thư mục không có clip / prefix trùng". Thà reset sạch để bấm Nạp lại.
    [NonSerialized] private AnimSetDefinition _draft;
    [NonSerialized] private readonly List<Row> _rows = new List<Row>();
    [NonSerialized] private readonly List<string> _templateFolders = new List<string>();
    [NonSerialized] private AnimSetCloneTool.ClipIndex _clips;
    [NonSerialized] private AnimSetCloneTool.ClipIndex _newClips;
    private bool _keepTemplateClips;
    private Vector2 _scroll;
    private float _threshold = 0.5f;
    private int _filter;                       // 0 tất cả, 1 dùng template, 2 map clip mới, 3 thiếu clip
    private string _clipFilter = string.Empty;
    private string _status = string.Empty;
    private string _previewKey = string.Empty;
    private string _previewText = string.Empty;
    private string _loadedTemplate = string.Empty;
    private int _writtenIndex = -1;
    private int _overwriteIndex = -1;

    private void OnEnable()
    {
        _library = LocomotionAnimatorBuilder.FindLibrary();
        _controller = AnimSetCloneTool.FindController();
        RefreshPrefixes();
        SyncTemplateFields();

        if (_library != null && _library.Sets.Count > 0)
            _status = "Chọn template + thư mục clip rồi bấm Nạp. (Unity compile lại sẽ reset bộ nháp; bộ đã ghi vẫn nằm trong library.)";
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            DrawHeader();
            DrawSource();
            DrawTarget();
            DrawLoad();

            if (_draft != null)
            {
                DrawMapping();
                DrawValidation();
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }

        // thanh hành động luôn nằm dưới cùng; Build vẫn bấm được sau khi Unity compile lại
        // (compile làm mất bộ nháp đang sửa, nhưng bộ đã ghi vẫn nằm trong library)
        DrawActions();
    }

    #region Giao diện

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Anim Set Clone", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Chọn controller + bộ anim (prefix)  →  2. Chọn thư mục clip của nhân vật mới (bấm Áp dụng)  →  " +
            "3. Nạp → map clip → Ghi → Sinh prefix → Build Locomotion.",
            MessageType.Info);
        EditorGUILayout.LabelField("Build sẽ xoá và dựng lại layer Locomotion của controller.", EditorStyles.miniLabel);
    }

    private void DrawSource()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("1. Nguồn template", EditorStyles.boldLabel);

        int source = GUILayout.Toolbar(_source, new[] { "Animator Controller (chỉ anim đang dùng)", "AnimSetLibrary" });
        if (source != _source)
        {
            _source = source;
            ResetDraft();
            SyncTemplateFields();
        }

        if (_source == 0) DrawControllerSource();
        else DrawLibrarySource();
    }

    private void DrawControllerSource()
    {
        EditorGUILayout.HelpBox(
            "Chỉ đọc những state đang thật sự có trong layer '" + LocomotionAnimatorBuilder.LayerName + "' và có motion: " +
            "clip, Speed, cờ Loop/FirstFrame lấy từ import settings của chính FBX đó.",
            MessageType.None);

        GUILayout.BeginHorizontal();
        AnimatorController controller = (AnimatorController)EditorGUILayout.ObjectField("Controller", _controller, typeof(AnimatorController), false);
        if (GUILayout.Button("Lấy controller của Dante", GUILayout.Width(190f)))
        {
            _controller = AnimSetCloneTool.FindController();
            RefreshPrefixes();
            ResetDraft();
            SyncTemplateFields();
        }
        GUILayout.EndHorizontal();

        if (controller != _controller)
        {
            _controller = controller;
            RefreshPrefixes();
            ResetDraft();
            SyncTemplateFields();
        }

        if (_controller == null)
        {
            EditorGUILayout.HelpBox("Chưa chọn controller. Đường dẫn mặc định: " + LocomotionAnimatorBuilder.ControllerPath, MessageType.Warning);
            return;
        }

        if (_prefixes.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"Layer '{LocomotionAnimatorBuilder.LayerName}' không có state nào có motion. " +
                "Chạy 'Build Locomotion Animator' trước, hoặc chọn controller khác.", MessageType.Warning);
            return;
        }

        _prefixIndex = Mathf.Clamp(_prefixIndex, 0, _prefixes.Count - 1);
        int picked = EditorGUILayout.Popup("Bộ anim trong layer", _prefixIndex, _prefixes.ToArray());
        if (picked != _prefixIndex)
        {
            _prefixIndex = picked;
            ResetDraft();
            SyncTemplateFields();
        }

        // đọc thử để hiện số state (cache theo controller + prefix, không đọc lại mỗi frame)
        string previewKey = AssetDatabase.GetAssetPath(_controller) + "|" + _prefixes[_prefixIndex];
        if (previewKey != _previewKey)
        {
            AnimSetDefinition preview = AnimSetCloneTool.ReadFromController(_controller, _prefixes[_prefixIndex], _displayName);
            _previewKey = previewKey;
            _previewText = $"{preview.OneShots.Count} state, {preview.Gaits.Count} gait, " +
                           $"{preview.Strafe.Count} hướng strafe, {preview.Crouch.Count} hướng crouch";
        }
        EditorGUILayout.LabelField("Đọc được", _previewText);
    }

    private void DrawLibrarySource()
    {
        AnimSetLibrary library = (AnimSetLibrary)EditorGUILayout.ObjectField("AnimSetLibrary", _library, typeof(AnimSetLibrary), false);
        if (library != _library)
        {
            _library = library;
            ResetDraft();
            SyncTemplateFields();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tìm trong project", GUILayout.Width(140f)))
        {
            _library = LocomotionAnimatorBuilder.FindLibrary();
            ResetDraft();
            SyncTemplateFields();
        }
        if (GUILayout.Button("Tạo AnimSetLibrary rỗng", GUILayout.Width(190f)))
        {
            _library = AnimSetCloneTool.CreateEmptyLibrary();
            ResetDraft();
            SyncTemplateFields();
        }
        GUILayout.EndHorizontal();

        if (_library == null || _library.Sets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Library chưa có bộ nào. Dùng tab 'Animator Controller' để đọc bộ đang dùng của Dante, rồi ghi vào library (sẽ tạo bộ đầu tiên).",
                MessageType.Warning);
            return;
        }

        _templateIndex = Mathf.Clamp(_templateIndex, 0, _library.Sets.Count - 1);
        string[] options = _library.Sets
            .Select((set, index) => $"{index}:  {set.Prefix}  {set.DisplayName}  —  {set.OneShots.Count} state / {set.Gaits.Count} gait")
            .ToArray();

        int picked = EditorGUILayout.Popup("Bộ template", _templateIndex, options);
        if (picked != _templateIndex)
        {
            _templateIndex = picked;
            ResetDraft();
            SyncTemplateFields();
        }
    }

    private void DrawTarget()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("2. Kiểu ghi", EditorStyles.boldLabel);

        int mode = GUILayout.Toolbar(_mode, new[] { "Nhân bản thành bộ mới", "Ghi đè bộ đang có (giữ AnimSetId)" });
        if (mode != _mode)
        {
            _mode = mode;
            ResetDraft();
            SyncTemplateFields();
        }

        EditorGUILayout.HelpBox(_mode == 0
                ? "Tạo bộ mới (prefix mới), thêm cuối library -> AnimSetId mới. Dùng khi nhân vật mới có bộ anim riêng."
                : "Giữ nguyên prefix và vị trí trong library (AnimSetId không đổi), chỉ thay thư mục + clip. Dùng khi thay anim cho chính nhân vật đó.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("3. Bộ mới", EditorStyles.boldLabel);

        if (_mode == 0)
        {
            // sửa tên ở đây là sửa luôn bộ nháp (và bộ đã ghi trong library) -> Build không bị mất tên
            string typedPrefix = EditorGUILayout.TextField("Prefix", _prefix);
            if (typedPrefix != _prefix)
            {
                _prefix = typedPrefix;
                SyncDraftIdentity();
            }

            string typedName = EditorGUILayout.TextField("Tên bộ", _displayName);
            if (typedName != _displayName)
            {
                _displayName = typedName;
                SyncDraftIdentity();
            }
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Prefix", CurrentTemplatePrefix());
            EditorGUILayout.TextField("Tên bộ", CurrentTemplateName());
            EditorGUI.EndDisabledGroup();
        }

        if (_mode == 0)
        {
            string normalized = AnimSetCloneTool.NormalizePrefix(_prefix);
            if (string.IsNullOrEmpty(normalized))
                EditorGUILayout.HelpBox("Prefix rỗng — bắt buộc phải có, vd VG_.", MessageType.Error);
            else if (AnimSetCloneTool.PrefixExists(_library, normalized, _writtenIndex, _draft))
                EditorGUILayout.HelpBox($"Prefix '{normalized}' đã có bộ khác dùng -> state sẽ đè nhau.", MessageType.Error);
        }
        else if (AnimSetCloneTool.IndexOfPrefix(_library, CurrentTemplatePrefix()) < 0)
        {
            EditorGUILayout.HelpBox($"Không có bộ '{CurrentTemplatePrefix()}' trong library để ghi đè — chọn 'Nhân bản' thay vì 'Ghi đè'.", MessageType.Warning);
        }

        GUILayout.BeginHorizontal();
        string typedFolder = EditorGUILayout.TextField("Thư mục clip mới", _folder);
        if (typedFolder != _folder)
        {
            // chỉ ghi text, KHÔNG quét clip ở đây: quét hàng trăm FBX mỗi ký tự sẽ làm editor đứng
            _folder = typedFolder;
            _folderDirty = true;
        }

        EditorGUI.BeginDisabledGroup(!_folderDirty);
        if (GUILayout.Button(new GUIContent("Áp dụng", "Quét clip trong thư mục này"), EditorStyles.miniButton, GUILayout.Width(62f)))
        {
            _folderDirty = false;
            SyncDraftFolder();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(22f))) PickFolder();
        GUILayout.EndHorizontal();

        if (_folderDirty)
            EditorGUILayout.HelpBox("Đường dẫn mới chưa được áp dụng — bấm Áp dụng (hoặc ▾) để quét clip.", MessageType.Warning);

        if (!string.IsNullOrEmpty(_folder) && !AssetDatabase.IsValidFolder(AnimSetCloneTool.NormalizeFolder(_folder)))
            EditorGUILayout.HelpBox("Thư mục không tồn tại (phải bắt đầu bằng Assets/).", MessageType.Error);

        bool keep = EditorGUILayout.Toggle("Giữ anim cũ của template (dùng chung clip)", _keepTemplateClips);
        if (keep != _keepTemplateClips)
        {
            _keepTemplateClips = keep;
            if (_draft != null)
            {
                ApplyTemplateFolders();
                RescanClips();
            }
        }
        EditorGUILayout.LabelField("Bật: state thiếu clip trong thư mục mới sẽ dùng clip của template (thư mục template thành thư mục dự phòng).",
            EditorStyles.miniLabel);
    }

    private void DrawLoad()
    {
        if (GUILayout.Button("Nạp state đang dùng + quét clip thư mục mới", GUILayout.Height(26f)))
            LoadDraft();

        if (_draft != null)
        {
            CountRows(out int total, out int mapped, out int shared, out int missing);
            EditorGUILayout.HelpBox(
                $"Đã nạp {total} dòng:  {mapped} dùng clip của pack mới,  {shared} giữ clip template,  {missing} thiếu clip.",
                missing > 0 ? MessageType.Warning : MessageType.Info);
        }
    }

    private void DrawMapping()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(string.IsNullOrEmpty(_loadedTemplate)
            ? "4. Map clip nhân vật mới"
            : $"4. Map clip nhân vật mới — template {_loadedTemplate}", EditorStyles.boldLabel);

        CountRows(out int total, out int mapped, out int shared, out int missing);

        // lọc theo nhóm: thấy ngay còn bao nhiêu dòng chưa xử lý
        _filter = GUILayout.Toolbar(_filter, new[]
        {
            $"Tất cả ({total})",
            $"Dùng template ({shared})",
            $"Clip mới ({mapped})",
            $"Thiếu clip ({missing})",
        }, EditorStyles.toolbarButton, GUILayout.Height(22f));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Áp gợi ý ≥ {_threshold:0.00}", GUILayout.Width(160f))) ApplySuggestions(_threshold, false);
        if (GUILayout.Button("Áp tên trùng", GUILayout.Width(105f))) ApplySuggestions(1f, true);
        if (GUILayout.Button("↺ Về clip template", GUILayout.Width(140f))) ApplySuggestions(0f, false, true);
        if (GUILayout.Button("Quét lại thư mục", GUILayout.Width(125f)))
        {
            AnimSetCloneTool.ClearCache();
            RescanClips();
        }
        _threshold = GUILayout.HorizontalSlider(_threshold, 0f, 1f, GUILayout.Width(100f));
        GUILayout.Label("Lọc ▾", EditorStyles.miniLabel, GUILayout.Width(38f));
        _clipFilter = GUILayout.TextField(_clipFilter ?? string.Empty, GUILayout.Width(100f));
        GUILayout.EndHorizontal();

        if (_draft.ExtraFolders.Count > 0)
        {
            EditorGUILayout.LabelField("Thư mục dự phòng (clip dùng chung với template)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < _draft.ExtraFolders.Count; i++)
            {
                GUILayout.BeginHorizontal();
                _draft.ExtraFolders[i] = GUILayout.TextField(_draft.ExtraFolders[i]);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    _draft.ExtraFolders.RemoveAt(i);
                    RescanClips();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Thư mục dự phòng", _keepTemplateClips
                ? "— không cần (template dùng chung thư mục chính)"
                : "— bật 'Giữ anim cũ của template' rồi Nạp để thêm");
        }

        if (_clips != null && _clips.DuplicateNames.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"Có {_clips.DuplicateNames.Count} clip trùng tên trong thư mục (thường là bản Inplace + bản có root motion). " +
                "Tool ưu tiên bản không nằm trong thư mục Inplace khi gợi ý; builder thì vẫn có thể lấy bản Inplace — nên tách thư mục Inplace ra ngoài.",
                MessageType.Warning);
        }

        EditorGUILayout.LabelField("Cam = clip template (dùng chung)      Trắng = clip pack mới      Đỏ = thiếu clip", EditorStyles.miniLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("State", EditorStyles.miniBoldLabel, GUILayout.Width(130f));
        GUILayout.Label("Clip template", EditorStyles.miniBoldLabel, GUILayout.Width(220f));
        GUILayout.Label(string.Empty, GUILayout.Width(16f));
        GUILayout.Label("Clip mới", EditorStyles.miniBoldLabel, GUILayout.Width(280f));
        GUILayout.Label("Gợi ý", EditorStyles.miniBoldLabel, GUILayout.Width(196f));
        GUILayout.EndHorizontal();

        string group = null;
        int shown = 0;
        foreach (Row row in _rows)
        {
            if (!RowMatchesFilter(row)) continue;

            if (row.Kind != group)
            {
                group = row.Kind;
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(GroupTitle(group) + "  (" + CountGroup(group) + ")", EditorStyles.miniBoldLabel);
            }

            DrawRow(row);
            shown++;
        }

        if (shown == 0)
        {
            EditorGUILayout.HelpBox(_filter == 3
                ? "Không còn dòng nào thiếu clip — bộ đã sẵn sàng ghi."
                : "Bộ lọc đang ẩn hết dòng. Bấm 'Tất cả' ở trên để xem toàn bộ state.", MessageType.Info);
            if (GUILayout.Button("Hiện tất cả dòng", GUILayout.Width(160f))) _filter = 0;
        }
    }

    private bool RowMatchesFilter(Row row)
    {
        bool missing = string.IsNullOrEmpty(row.Clip) || (_clips != null && _clips.IsValid && !_clips.Contains(row.Clip));
        bool inNew = _newClips != null && _newClips.IsValid && _newClips.Contains(row.Clip);

        switch (_filter)
        {
            case 1: return !missing && !inNew;
            case 2: return !missing && inNew;
            case 3: return missing;
            default: return true;
        }
    }

    private static string GroupTitle(string kind)
    {
        switch (kind)
        {
            case "gait": return "Gait — blend tree Loop (Gait 1..4)";
            case "strafe": return "Strafe — 8 hướng đi + 8 hướng chạy";
            case "crouch": return "Crouch — 8 hướng";
            default: return "State one-shot / loop";
        }
    }

    private int CountGroup(string kind)
    {
        int count = 0;
        foreach (Row row in _rows)
        {
            if (row.Kind == kind) count++;
        }
        return count;
    }

    private void DrawRow(Row row)
    {
        bool missing = string.IsNullOrEmpty(row.Clip) || (_clips != null && _clips.IsValid && !_clips.Contains(row.Clip));
        bool inNew = _newClips != null && _newClips.IsValid && _newClips.Contains(row.Clip);

        GUILayout.BeginHorizontal();

        GUILayout.Label(row.Label, EditorStyles.miniBoldLabel, GUILayout.Width(130f));

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.55f);
        GUILayout.Label(string.IsNullOrEmpty(row.Original) ? "—" : row.Original, EditorStyles.miniLabel, GUILayout.Width(220f));
        GUI.color = previous;

        GUILayout.Label("→", GUILayout.Width(16f));

        if (missing) GUI.color = new Color(1f, 0.55f, 0.55f);
        else if (!inNew) GUI.color = new Color(1f, 0.85f, 0.5f);
        string typed = GUILayout.TextField(row.Clip ?? string.Empty, GUILayout.Width(240f));
        GUI.color = previous;
        if (typed != row.Clip) row.Clip = typed.Trim();
        if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(20f))) ShowClipMenu(row);

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(row.Original) || row.Clip == row.Original);
        if (GUILayout.Button(new GUIContent("↺", "Giữ clip template cho state này"), EditorStyles.miniButton, GUILayout.Width(20f)))
            row.Clip = row.Original;
        EditorGUI.EndDisabledGroup();

        if (string.IsNullOrEmpty(row.Suggestion))
        {
            GUILayout.Label("—", EditorStyles.miniLabel, GUILayout.Width(196f));
        }
        else
        {
            GUILayout.Label($"{row.Suggestion}  ({row.Score:0.00})", EditorStyles.miniLabel, GUILayout.Width(160f));
            if (GUILayout.Button("Áp", EditorStyles.miniButton, GUILayout.Width(36f))) row.Clip = row.Suggestion;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawValidation()
    {
        EditorGUILayout.Space(10f);
        AnimSetCloneTool.Report report = AnimSetCloneTool.Validate(_library, _draft, _overwriteIndex, _clips);

        EditorGUILayout.LabelField($"5. Kiểm tra — {report.Errors.Count} lỗi / {report.Warnings.Count} cảnh báo", EditorStyles.boldLabel);
        if (report.IsClean)
        {
            EditorGUILayout.HelpBox("Không thấy vấn đề.", MessageType.Info);
            return;
        }

        foreach (string error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        foreach (string warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    // Thanh dưới cùng: luôn thấy nút Ghi / Build mà không phải cuộn
    private void DrawActions()
    {
        EditorGUILayout.Space(3f);
        Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.35f));

        if (_library == null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tạo AnimSetLibrary rỗng (bắt buộc để ghi)", GUILayout.Height(26f), GUILayout.Width(320f)))
                _library = AnimSetCloneTool.CreateEmptyLibrary();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return;
        }

        GUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(_draft == null || (_mode == 1 && _overwriteIndex < 0));
        if (GUILayout.Button(_mode == 0 ? "Ghi vào AnimSetLibrary" : "Ghi đè bộ đang có", GUILayout.Height(26f), GUILayout.Width(190f)))
            WriteNow();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(_library.Sets.Count == 0);
        if (GUILayout.Button("Sinh prefix", GUILayout.Height(26f), GUILayout.Width(100f)))
        {
            LocoSetPrefixesGenerator.Generate(_library);
            _status = "Đã sinh LocoSetPrefixes.Generated.cs — Unity sẽ compile lại (bộ nháp trên cửa sổ sẽ reset).";
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(_library.Sets.Count == 0);
        if (GUILayout.Button("Build Locomotion", GUILayout.Height(26f), GUILayout.Width(140f)))
            BuildNow();
        EditorGUI.EndDisabledGroup();

        int draftIndex = AnimSetCloneTool.IndexOfReference(_library, _draft);
        EditorGUI.BeginDisabledGroup(draftIndex < 0);
        if (GUILayout.Button(new GUIContent("Xoá bộ khỏi library", "Bỏ bộ đang sửa ra khỏi AnimSetLibrary"),
            GUILayout.Height(26f), GUILayout.Width(150f)))
        {
            if (EditorUtility.DisplayDialog("Xoá bộ?", "Xoá bộ index " + draftIndex + " khỏi AnimSetLibrary?", "Xoá", "Huỷ"))
            {
                Undo.RecordObject(_library, "Xoá bộ anim");
                _library.Sets.RemoveAt(draftIndex);
                EditorUtility.SetDirty(_library);
                AssetDatabase.SaveAssets();
                ResetDraft();
                _status = "Đã xoá bộ. Bấm 'Sinh prefix' để cập nhật runtime.";
            }
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("AnimSetLibrary", GUILayout.Height(26f), GUILayout.Width(120f)))
        {
            Selection.activeObject = _library;
            EditorGUIUtility.PingObject(_library);
            _status = "Bộ anim lưu trong " + AnimSetCloneTool.LibraryPath + "  (prefix runtime: LocoSetPrefixes.Generated.cs)";
        }

        if (GUILayout.Button("Timing Window", GUILayout.Height(26f), GUILayout.Width(115f)))
            EditorApplication.ExecuteMenuItem("Tools/Combat/Timing Window");

        GUILayout.FlexibleSpace();
        if (_writtenIndex >= 0) GUILayout.Label($"AnimSetId = {_writtenIndex}", EditorStyles.boldLabel, GUILayout.Width(140f));
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
    }

    private void WriteNow()
    {
        if (_draft == null) return;
        if (_mode == 1 && _overwriteIndex < 0)
        {
            _status = "Không có bộ '" + CurrentTemplatePrefix() + "' trong library để ghi đè.";
            return;
        }

        _writtenIndex = AnimSetCloneTool.Write(_library, _draft, _mode == 1 ? _overwriteIndex : -1);
        if (_writtenIndex < 0) return;

        _draftUndoRecorded = false;   // từ giờ sửa bộ là sửa thẳng trong asset -> lần sửa đầu có Undo

        _status = _mode == 0
            ? $"Đã ghi vào {AnimSetCloneTool.LibraryPath} (index {_writtenIndex}). Tiếp: Sinh prefix -> Build Locomotion."
            : $"Đã ghi đè bộ index {_writtenIndex} (giữ AnimSetId). Tiếp: Build Locomotion.";
    }

    private void BuildNow()
    {
        if (_library == null || _library.Sets.Count == 0) return;

        if (!EditorUtility.DisplayDialog("Build Locomotion?",
            "Layer Locomotion trong controller sẽ bị xoá và dựng lại cho toàn bộ bộ anim trong library. Không được ở Play mode.",
            "Build", "Huỷ")) return;

        LocomotionAnimatorBuilder.BuildFrom(_library);
        _status = "Build xong — đọc Console để chắc không có 'Không thấy clip'.";
    }

    #endregion

    #region Thao tác

    private string CurrentTemplatePrefix()
    {
        if (_source == 0)
            return _prefixes.Count > 0 ? _prefixes[Mathf.Clamp(_prefixIndex, 0, _prefixes.Count - 1)] : string.Empty;

        if (_library != null && _library.Sets.Count > 0)
            return _library.Sets[Mathf.Clamp(_templateIndex, 0, _library.Sets.Count - 1)].Prefix;

        return string.Empty;
    }

    private string CurrentTemplateName()
    {
        if (_source == 0) return _displayName;
        if (_library != null && _library.Sets.Count > 0)
            return _library.Sets[Mathf.Clamp(_templateIndex, 0, _library.Sets.Count - 1)].DisplayName;
        return _displayName;
    }

    private void RefreshPrefixes()
    {
        _prefixes.Clear();
        _prefixes.AddRange(AnimSetCloneTool.ListPrefixes(AnimSetCloneTool.GetLocomotionStateMachine(_controller)));
        if (_prefixIndex >= _prefixes.Count) _prefixIndex = 0;
    }

    private void ResetDraft()
    {
        _draft = null;
        _rows.Clear();
        _clips = null;
        _newClips = null;
        _filter = 0;
        _loadedTemplate = string.Empty;
        _writtenIndex = -1;
        _overwriteIndex = -1;
        _draftUndoRecorded = false;
        _status = string.Empty;
    }

    private void SyncTemplateFields()
    {
        // Ghi đè: mặc định đúng thư mục của bộ đang có. Nhân bản: giữ đường dẫn người dùng đã nhập
        // (không tự điền đường dẫn của template, và không xoá khi cửa sổ bị reload sau compile).
        if (_mode == 1)
        {
            int existing = AnimSetCloneTool.IndexOfPrefix(_library, CurrentTemplatePrefix());
            if (existing >= 0) _folder = _library.Sets[existing].MovementFolder;
        }

        if (string.IsNullOrEmpty(_displayName)) _displayName = "Bộ mới";
        if (string.IsNullOrEmpty(_prefix)) _prefix = AnimSetCloneTool.SuggestPrefix(_library, _displayName);
        _folderDirty = false;
    }

    // Đổi Prefix / Tên bộ: ghi thẳng vào bộ nháp (nếu đã Ghi thì ghi luôn vào library)
    private void SyncDraftIdentity()
    {
        if (_draft == null) return;

        _draft.Prefix = AnimSetCloneTool.NormalizePrefix(_prefix);
        _draft.DisplayName = _displayName;
        TouchLibrary();
        _status = "Đã đổi prefix/tên bộ. Nếu đổi prefix thì bấm 'Sinh prefix' rồi 'Build Locomotion'.";
    }

    // Đổi thư mục clip: cập nhật bộ nháp + quét lại clip để kiểm tra/gợi ý đúng
    private void SyncDraftFolder()
    {
        if (_draft == null)
        {
            _status = "Chưa có bộ nháp — bấm 'Nạp state đang dùng + quét clip' trước.";
            return;
        }

        _draft.MovementFolder = AnimSetCloneTool.NormalizeFolder(_folder);
        ApplyTemplateFolders();
        AnimSetCloneTool.ClearCache();
        RescanClips();
        TouchLibrary();
        _status = "Đã áp dụng thư mục clip. Bấm 'Ghi vào AnimSetLibrary' rồi Build.";
    }

    private void TouchLibrary()
    {
        if (_library == null || _draft == null) return;
        if (AnimSetCloneTool.IndexOfReference(_library, _draft) < 0) return;
        if (!_draftUndoRecorded)
        {
            Undo.RecordObject(_library, "Sửa bộ anim");
            _draftUndoRecorded = true;
        }
        EditorUtility.SetDirty(_library);
    }

    private void PickFolder()
    {
        string start = string.IsNullOrEmpty(_folder) ? Application.dataPath : _folder;
        string picked = EditorUtility.OpenFolderPanel("Chọn thư mục clip", start, string.Empty);
        if (string.IsNullOrEmpty(picked)) return;

        if (picked.StartsWith(Application.dataPath))
            picked = "Assets" + picked.Substring(Application.dataPath.Length);
        _folder = AnimSetCloneTool.NormalizeFolder(picked);
        _folderDirty = false;
        SyncDraftFolder();
    }

    private void LoadDraft()
    {
        // nạp lại = bỏ bộ nháp cũ, tránh tình trạng bảng trống nhưng vẫn còn dữ liệu cũ
        _draft = null;
        _rows.Clear();
        _clips = null;
        _newClips = null;
        _writtenIndex = -1;
        _overwriteIndex = -1;
        _filter = 0;
        _folderDirty = false;
        _draftUndoRecorded = false;

        string templatePrefix = CurrentTemplatePrefix();
        AnimSetDefinition source;

        if (_source == 0)
        {
            if (_controller == null || string.IsNullOrEmpty(templatePrefix))
            {
                _status = "Chưa chọn controller hoặc bộ anim trong layer.";
                return;
            }
            source = AnimSetCloneTool.ReadFromController(_controller, templatePrefix, _displayName);
        }
        else
        {
            if (_library == null || _library.Sets.Count == 0)
            {
                _status = "AnimSetLibrary chưa có bộ nào để làm template.";
                return;
            }
            _templateIndex = Mathf.Clamp(_templateIndex, 0, _library.Sets.Count - 1);
            source = _library.Sets[_templateIndex];
            templatePrefix = source.Prefix;
        }

        _overwriteIndex = _mode == 1 ? AnimSetCloneTool.IndexOfPrefix(_library, templatePrefix) : -1;

        if (source.OneShots.Count == 0 && source.Gaits.Count == 0 && source.Strafe.Count == 0 && source.Crouch.Count == 0)
        {
            _status = "Template rỗng (không có state/clip nào). Chọn template khác, hoặc đọc từ tab Animator Controller.";
            return;
        }

        string prefix = _mode == 1 ? templatePrefix : AnimSetCloneTool.NormalizePrefix(_prefix);
        string displayName = _mode == 0
            ? _displayName
            : _overwriteIndex >= 0 ? _library.Sets[_overwriteIndex].DisplayName : source.DisplayName;

        _draft = AnimSetCloneTool.CloneSet(source, prefix, displayName, _folder);
        _rows.Clear();

        for (int i = 0; i < source.OneShots.Count && i < _draft.OneShots.Count; i++)
        {
            _rows.Add(new Row
            {
                Label = source.OneShots[i].State.ToString(),
                Kind = "state",
                Semantic = source.OneShots[i].State.ToString(),
                Original = source.OneShots[i].Clip,
                Shot = _draft.OneShots[i],
            });
        }

        for (int i = 0; i < source.Gaits.Count && i < _draft.Gaits.Count; i++)
        {
            _rows.Add(new Row
            {
                Label = "Gait " + source.Gaits[i].Threshold.ToString("0.##"),
                Kind = "gait",
                Semantic = string.Empty,
                Y = 1f,
                Original = source.Gaits[i].Clip,
                Gait = _draft.Gaits[i],
            });
        }

        for (int i = 0; i < source.Strafe.Count && i < _draft.Strafe.Count; i++)
        {
            _rows.Add(new Row
            {
                Label = $"Strafe ({source.Strafe[i].X:0.##}, {source.Strafe[i].Y:0.##})",
                Kind = "strafe",
                Semantic = string.Empty,
                X = source.Strafe[i].X,
                Y = source.Strafe[i].Y,
                Original = source.Strafe[i].Clip,
                Direction = _draft.Strafe[i],
            });
        }

        for (int i = 0; i < source.Crouch.Count && i < _draft.Crouch.Count; i++)
        {
            _rows.Add(new Row
            {
                Label = $"Crouch ({source.Crouch[i].X:0.##}, {source.Crouch[i].Y:0.##})",
                Kind = "crouch",
                Semantic = string.Empty,
                X = source.Crouch[i].X,
                Y = source.Crouch[i].Y,
                Original = source.Crouch[i].Clip,
                Direction = _draft.Crouch[i],
            });
        }

        _templateFolders.Clear();
        _templateFolders.AddRange(AnimSetCloneTool.TemplateFolders(source));
        ApplyTemplateFolders();
        RescanClips();

        _writtenIndex = -1;
        _filter = 0;
        _loadedTemplate = templatePrefix;
        _status = $"{_draft.OneShots.Count} state, {_draft.Gaits.Count} gait, {_draft.Strafe.Count} hướng strafe, {_draft.Crouch.Count} hướng crouch.";
        if (_mode == 1 && _overwriteIndex < 0)
            _status += " — Không thấy bộ " + templatePrefix + " trong library để ghi đè.";
    }

    // Thư mục dự phòng = thư mục clip của template (chỉ khi bật 'giữ anim cũ')
    private void ApplyTemplateFolders()
    {
        if (_draft == null) return;

        _draft.ExtraFolders.Clear();
        if (!_keepTemplateClips) return;

        string primary = AnimSetCloneTool.NormalizeFolder(_draft.MovementFolder);
        foreach (string folder in _templateFolders)
        {
            string clean = AnimSetCloneTool.NormalizeFolder(folder);
            if (string.IsNullOrEmpty(clean) || clean == primary) continue;
            if (!_draft.ExtraFolders.Contains(clean)) _draft.ExtraFolders.Add(clean);
        }
    }

    private void RescanClips()
    {
        _newClips = AnimSetCloneTool.ScanClips(_folder);
        _clips = AnimSetCloneTool.ScanClips(_folder, _draft != null ? _draft.ExtraFolders : null);
        ComputeSuggestions();
    }

    private void CountRows(out int total, out int mapped, out int shared, out int missing)
    {
        total = _rows.Count;
        mapped = 0;
        shared = 0;
        missing = 0;

        foreach (Row row in _rows)
        {
            bool notFound = string.IsNullOrEmpty(row.Clip) || (_clips != null && _clips.IsValid && !_clips.Contains(row.Clip));
            if (notFound)
            {
                missing++;
                continue;
            }

            if (_newClips != null && _newClips.IsValid && _newClips.Contains(row.Clip)) mapped++;
            else shared++;
        }
    }

    private void ComputeSuggestions()
    {
        // gợi ý chỉ lấy clip của thư mục mới, không gợi ý clip dùng chung của template
        foreach (Row row in _rows)
        {
            float score;
            row.Suggestion = AnimSetCloneTool.SuggestClip(row.Original, row.Semantic, row.X, row.Y, _newClips, out score);
            row.Score = score;
        }
    }

    private void ApplySuggestions(float minimumScore, bool sameNameOnly, bool revertToTemplate = false)
    {
        int applied = 0;
        foreach (Row row in _rows)
        {
            if (revertToTemplate)
            {
                if (string.IsNullOrEmpty(row.Original)) continue;
                row.Clip = row.Original;
                applied++;
                continue;
            }

            if (sameNameOnly)
            {
                if (string.IsNullOrEmpty(row.Original)) continue;
                if (_newClips == null || !_newClips.IsValid || !_newClips.Contains(row.Original)) continue;
                row.Clip = row.Original;
                applied++;
                continue;
            }

            if (string.IsNullOrEmpty(row.Suggestion)) continue;
            if (row.Score < minimumScore) continue;
            row.Clip = row.Suggestion;
            applied++;
        }
        _status = "Đã áp " + applied + " dòng.";
    }

    private void ShowClipMenu(Row row)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("— (để trống) —"), string.IsNullOrEmpty(row.Clip), () =>
        {
            row.Clip = string.Empty;
            Repaint();
        });

        if (_clips != null && _clips.IsValid)
        {
            menu.AddSeparator(string.Empty);
            foreach (string clip in _clips.Names)
            {
                if (!string.IsNullOrEmpty(_clipFilter) && clip.IndexOf(_clipFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool fromTemplate = _newClips == null || !_newClips.Contains(clip);
                string label = fromTemplate ? clip + "    · template" : clip;
                string captured = clip;
                menu.AddItem(new GUIContent(label), clip == row.Clip, () =>
                {
                    row.Clip = captured;
                    Repaint();
                });
            }
        }

        menu.ShowAsContext();
    }

    #endregion
}
