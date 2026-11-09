using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Bộ màu + widget vẽ tay cho Character Studio (card, chip, nút, dòng trạng thái).
// Tự đổi bảng màu theo skin sáng / tối của Editor.
public static class StudioGUI
{
    public enum State { None, Ok, Warn, Error }

    #region Bảng màu

    private static bool Dark => EditorGUIUtility.isProSkin;

    public static Color Background => Dark ? Hex(0x1B1D22) : Hex(0xDCDEE3);
    public static Color Sidebar => Dark ? Hex(0x16181C) : Hex(0xCFD2D8);
    public static Color Header => Dark ? Hex(0x121417) : Hex(0xC4C8CF);
    public static Color Card => Dark ? Hex(0x262930) : Hex(0xF2F3F5);
    public static Color CardAlt => Dark ? Hex(0x2E323A) : Hex(0xE6E8EC);
    public static Color Border => Dark ? Hex(0x363A44) : Hex(0xB9BDC6);
    public static Color Text => Dark ? Hex(0xE8EAEF) : Hex(0x1E2128);
    public static Color Muted => Dark ? Hex(0x8F96A3) : Hex(0x5E6470);
    public static Color Accent => Hex(0x4C9AFF);
    public static Color Ok => Hex(0x45C26A);
    public static Color Warn => Hex(0xF0B03C);
    public static Color Error => Hex(0xE5564E);
    public static Color Purple => Hex(0xA77BFF);

    public static Color For(State state)
    {
        switch (state)
        {
            case State.Ok: return Ok;
            case State.Warn: return Warn;
            case State.Error: return Error;
            default: return Muted;
        }
    }

    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    public static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    #endregion

    #region Style

    private static readonly Dictionary<Color, Texture2D> Textures = new Dictionary<Color, Texture2D>();
    private static bool _dark;
    private static GUIStyle _card, _title, _subtitle, _section, _label, _muted, _chip, _navItem, _navSelected, _button, _primary, _danger, _badge, _bigTitle;

    public static Texture2D Tex(Color color)
    {
        if (Textures.TryGetValue(color, out Texture2D texture) && texture != null) return texture;
        texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        Textures[color] = texture;
        return texture;
    }

    private static void EnsureStyles()
    {
        if (_card != null && _dark == Dark && _card.normal.background != null) return;
        _dark = Dark;

        _card = new GUIStyle
        {
            padding = new RectOffset(14, 14, 12, 12),
            margin = new RectOffset(0, 0, 0, 10),
            normal = { background = Tex(Card) },
        };
        _bigTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, normal = { textColor = Text } };
        _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = Text } };
        _subtitle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11, normal = { textColor = Muted } };
        _section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, normal = { textColor = Muted } };
        _label = new GUIStyle(EditorStyles.label) { normal = { textColor = Text } };
        _muted = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted }, wordWrap = true };
        _chip = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 1, 2),
            margin = new RectOffset(0, 4, 2, 2),
            fixedHeight = 18f,
        };
        _badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        _navItem = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(44, 8, 0, 0),
            normal = { textColor = Muted },
            hover = { textColor = Text },
        };
        _navSelected = new GUIStyle(_navItem) { fontStyle = FontStyle.Bold, normal = { textColor = Text } };
        _button = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 24f, fontSize = 11, padding = new RectOffset(10, 10, 3, 4) };
        _primary = new GUIStyle(_button)
        {
            fontStyle = FontStyle.Bold,
            normal = { background = Tex(Accent), textColor = Color.white },
            hover = { background = Tex(Color.Lerp(Accent, Color.white, 0.15f)), textColor = Color.white },
            active = { background = Tex(Color.Lerp(Accent, Color.black, 0.2f)), textColor = Color.white },
            border = new RectOffset(0, 0, 0, 0),
        };
        _danger = new GUIStyle(_button) { normal = { textColor = Error }, hover = { textColor = Error } };
    }

    public static GUIStyle TitleStyle { get { EnsureStyles(); return _title; } }
    public static GUIStyle BigTitleStyle { get { EnsureStyles(); return _bigTitle; } }
    public static GUIStyle MutedStyle { get { EnsureStyles(); return _muted; } }
    public static GUIStyle LabelStyle { get { EnsureStyles(); return _label; } }
    public static GUIStyle SectionStyle { get { EnsureStyles(); return _section; } }

    #endregion

    #region Khung

    public static void Fill(Rect rect, Color color)
    {
        if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, color);
    }

    public static void BeginCard(string title = null, string subtitle = null)
    {
        EnsureStyles();
        GUILayout.BeginVertical(_card);
        if (!string.IsNullOrEmpty(title)) GUILayout.Label(title, _title);
        if (!string.IsNullOrEmpty(subtitle))
        {
            GUILayout.Label(subtitle, _subtitle);
            GUILayout.Space(6f);
        }
    }

    public static void EndCard() => GUILayout.EndVertical();

    /// <summary>Card có chấm trạng thái cạnh tiêu đề.</summary>
    public static void BeginCard(State state, string title, string subtitle = null)
    {
        EnsureStyles();
        GUILayout.BeginVertical(_card);
        GUILayout.BeginHorizontal();
        Dot(state, 10f);
        GUILayout.Label(title, _title);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(subtitle))
        {
            GUILayout.Label(subtitle, _subtitle);
            GUILayout.Space(6f);
        }
    }

    public static void PageTitle(string title, string subtitle)
    {
        EnsureStyles();
        GUILayout.Label(title, _bigTitle);
        if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, _subtitle);
        GUILayout.Space(10f);
    }

    public static void Section(string text)
    {
        EnsureStyles();
        GUILayout.Space(6f);
        GUILayout.Label(text.ToUpperInvariant(), _section);
        Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        Fill(line, Border);
        GUILayout.Space(4f);
    }

    public static void Separator()
    {
        GUILayout.Space(4f);
        Fill(GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true)), Border);
        GUILayout.Space(4f);
    }

    #endregion

    #region Widget

    public static void Chip(string text, Color color, string tooltip = null)
    {
        EnsureStyles();
        GUIContent content = new GUIContent(text, tooltip);
        Vector2 size = _chip.CalcSize(content);
        Rect rect = GUILayoutUtility.GetRect(size.x, 18f, _chip, GUILayout.Width(size.x));
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rect, Alpha(color, 0.18f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
        }
        GUIStyle style = new GUIStyle(_chip) { normal = { textColor = color } };
        GUI.Label(rect, content, style);
    }

    public static void Dot(State state, float size = 8f)
    {
        Rect rect = GUILayoutUtility.GetRect(size + 6f, 18f, GUILayout.Width(size + 6f));
        DrawDot(new Rect(rect.x + 3f, rect.center.y - size * 0.5f, size, size), For(state));
    }

    public static void DrawDot(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        // chấm tròn nhỏ: vẽ 3 lớp rect để đỡ vuông
        EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y, rect.width - 2f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f), color);
    }

    public static void Badge(Rect rect, string text, Color color)
    {
        EnsureStyles();
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y, rect.width - 4f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f), color);
        }
        GUI.Label(rect, text, _badge);
    }

    public static bool Button(string text, string tooltip = null, float width = 0f)
    {
        EnsureStyles();
        return width > 0f
            ? GUILayout.Button(new GUIContent(text, tooltip), _button, GUILayout.Width(width))
            : GUILayout.Button(new GUIContent(text, tooltip), _button);
    }

    public static bool Primary(string text, string tooltip = null, float width = 0f)
    {
        EnsureStyles();
        return width > 0f
            ? GUILayout.Button(new GUIContent(text, tooltip), _primary, GUILayout.Width(width))
            : GUILayout.Button(new GUIContent(text, tooltip), _primary);
    }

    public static bool Danger(string text, string tooltip = null, float width = 0f)
    {
        EnsureStyles();
        return width > 0f
            ? GUILayout.Button(new GUIContent(text, tooltip), _danger, GUILayout.Width(width))
            : GUILayout.Button(new GUIContent(text, tooltip), _danger);
    }

    public static bool Icon(string glyph, string tooltip, float width = 22f)
    {
        return GUILayout.Button(new GUIContent(glyph, tooltip), EditorStyles.miniButton, GUILayout.Width(width), GUILayout.Height(18f));
    }

    /// <summary>Thanh tab dạng viên thuốc. Trả về index mới.</summary>
    public static int Pills(int selected, string[] labels, float height = 24f)
    {
        EnsureStyles();
        GUILayout.BeginHorizontal();
        for (int i = 0; i < labels.Length; i++)
        {
            bool on = i == selected;
            GUIContent content = new GUIContent(labels[i]);
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = on ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(12, 12, 0, 0),
                normal = { textColor = on ? Color.white : Muted },
                hover = { textColor = on ? Color.white : Text },
            };
            float width = style.CalcSize(content).x;
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, on ? Accent : CardAlt);
            }
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, content, style)) selected = i;
            GUILayout.Space(3f);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return selected;
    }

    /// <summary>1 dòng checklist: chấm trạng thái + chữ + chi tiết + nút hành động tuỳ chọn.</summary>
    public static bool CheckRow(State state, string label, string detail = null, string action = null, string actionTooltip = null)
    {
        EnsureStyles();
        bool clicked = false;
        GUILayout.BeginHorizontal(GUILayout.MinHeight(22f));
        Dot(state);
        GUILayout.Label(label, _label, GUILayout.MinWidth(120f));
        if (!string.IsNullOrEmpty(detail)) GUILayout.Label(detail, _muted, GUILayout.ExpandWidth(true));
        else GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(action)) clicked = Button(action, actionTooltip);
        GUILayout.EndHorizontal();
        return clicked;
    }

    public static void Stat(string value, string label, Color color)
    {
        EnsureStyles();
        GUILayout.BeginVertical(GUILayout.MinWidth(86f));
        GUIStyle big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, normal = { textColor = color } };
        GUILayout.Label(value, big, GUILayout.Height(26f));
        GUILayout.Label(label, _muted);
        GUILayout.EndVertical();
    }

    public static void Note(string text, State state = State.None)
    {
        EnsureStyles();
        GUILayout.BeginHorizontal();
        if (state != State.None) Dot(state);
        GUILayout.Label(text, _muted);
        GUILayout.EndHorizontal();
    }

    /// <summary>Chọn thư mục trong Assets, trả về đường dẫn dạng Assets/... hoặc null.</summary>
    public static string PickFolder(string current)
    {
        string start = string.IsNullOrEmpty(current) ? Application.dataPath : current;
        string folder = EditorUtility.OpenFolderPanel("Chọn thư mục", start, string.Empty);
        if (string.IsNullOrEmpty(folder)) return null;
        folder = folder.Replace('\\', '/');
        if (folder.StartsWith(Application.dataPath, StringComparison.Ordinal)) return "Assets" + folder.Substring(Application.dataPath.Length);
        int index = folder.IndexOf("/Assets/", StringComparison.Ordinal);
        return index >= 0 ? folder.Substring(index + 1) : null;
    }

    /// <summary>Ô đường dẫn thư mục + nút chọn.</summary>
    public static string FolderField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        string result = EditorGUILayout.TextField(label, value);
        if (GUILayout.Button("…", EditorStyles.miniButton, GUILayout.Width(26f)))
        {
            string picked = PickFolder(value);
            if (picked != null) result = picked;
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();
        return result;
    }

    #endregion
}

/// <summary>Ô chọn clip có ô tìm kiếm (AdvancedDropdown), gom nhóm theo tiền tố tên clip.</summary>
public class ClipDropdown : AdvancedDropdown
{
    private readonly IList<string> _names;
    private readonly Action<string> _onPick;
    private readonly string _title;

    private ClipDropdown(IList<string> names, string title, Action<string> onPick) : base(new AdvancedDropdownState())
    {
        _names = names;
        _onPick = onPick;
        _title = title;
        minimumSize = new Vector2(280f, 360f);
    }

    public static void Show(Rect rect, IList<string> names, string title, Action<string> onPick)
    {
        new ClipDropdown(names, title, onPick).Show(rect);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem(_title);
        root.AddChild(new AdvancedDropdownItem("— để trống —") { id = -1 });
        if (_names == null || _names.Count == 0)
        {
            root.AddChild(new AdvancedDropdownItem("(thư mục chưa có clip)") { enabled = false });
            return root;
        }

        // nhóm theo phần trước dấu '_' đầu tiên -> danh sách dài vẫn dễ lướt; tìm kiếm thì Unity tự làm phẳng
        var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _names.Count; i++)
        {
            string name = _names[i];
            int cut = name.IndexOf('_');
            string key = cut > 0 ? name.Substring(0, cut) : "Khác";
            if (!groups.TryGetValue(key, out AdvancedDropdownItem group))
            {
                group = new AdvancedDropdownItem(key);
                groups[key] = group;
                root.AddChild(group);
            }
            group.AddChild(new AdvancedDropdownItem(name) { id = i });
        }
        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item.id == -1) _onPick(string.Empty);
        else if (item.id >= 0 && item.id < _names.Count) _onPick(_names[item.id]);
    }
}
