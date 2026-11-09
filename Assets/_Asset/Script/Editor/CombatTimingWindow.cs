using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cửa sổ canh mốc đòn đánh ngay trong Editor (không cần vào Play mode):
///  - Tab "Mốc đánh": timeline phát clip của đòn, kéo mốc chém / mốc đá / mốc đập đất,
///    kim đi qua mốc nào thì nghe đúng tiếng của mốc đó (lấy từ CombatEffects trong scene).
///  - Tab "Âm thanh": thay bộ SFX ở một chỗ duy nhất, có nút tự lấp clip theo tên.
///  - Tab "Build": chạy Build Locomotion / Build Weapon Combos / Bake Slam+Kick và kiểm tra dữ liệu.
///
/// Mốc lưu trong WeaponData là normalized (0..1 theo độ dài clip); cửa sổ hiển thị thêm giây.
/// Kéo mốc = Undo được; thả chuột là lưu asset ngay.
/// </summary>
public class CombatTimingWindow : EditorWindow
{
    private const string CharacterPrefabPath = "Assets/_Asset/Prefab/Character.prefab";
    private const float TrackHeight = 48f;
    private const float GrabRadius = 8f;

    private enum MarkerKind { None, Hit, Kick, Slam, Link, Cancel }

    private static readonly Color HitColor = new Color(0.35f, 0.95f, 0.45f);
    private static readonly Color KickColor = new Color(1f, 0.66f, 0.22f);
    private static readonly Color SlamColor = new Color(1f, 0.28f, 0.28f);
    private static readonly Color LinkColor = new Color(1f, 0.88f, 0.3f);
    private static readonly Color CancelColor = new Color(0.78f, 0.78f, 0.78f);
    private static readonly Color SeamColor = new Color(0.45f, 0.72f, 1f);

    // Bộ SFX trong CombatEffects + từ khoá để tự tìm clip theo tên asset
    private static readonly string[][] SfxSets =
    {
        new[] { "SwingClips", "Chém thường", "swing,slash,whoosh,chem" },
        new[] { "HeavySwingClips", "Chém nặng", "heavy" },
        new[] { "ThrustClips", "Đâm", "thrust,pierce,stab" },
        new[] { "HitClips", "Trúng đòn", "hit,impact" },
        new[] { "HeavyHitClips", "Trúng đòn nặng", "heavyhit,heavy_hit" },
        new[] { "SlamClips", "Đập đất", "slam,ground" },
        new[] { "KickSwingClips", "Vung chân", "kick" },
        new[] { "KickHitClips", "Đá trúng", "kickhit,kick_hit,hitkick" },
    };

    private readonly List<WeaponData> _weapons = new List<WeaponData>();
    private int _weaponIndex;
    private int _chainIndex;
    private int _stepIndex;

    private float _time;
    private bool _playing;
    private double _lastTick;
    private bool _loop = true;
    private bool _snapFrame = true;
    private int _snapFps = 30;

    private MarkerKind _dragKind = MarkerKind.None;
    private int _dragIndex = -1;
    private MarkerKind _hoverKind = MarkerKind.None;
    private int _hoverIndex = -1;
    private MarkerKind _selectedKind = MarkerKind.None;
    private int _selectedIndex = -1;
    private AttackStep _selectedStep;
    private Rect _lastTimelineStrip;
    private bool _scrubbing;
    private bool _sfxWhileScrub = true;

    private Animator _preview;
    private GameObject _spawnedPreview;
    private bool _startedAnimationMode;
    private string _previewSource;
    private bool _needsFrame;
    private bool _autoSample;

    // Preview trong cửa sổ (PreviewRenderUtility giống preview của Inspector)
    private int _previewMode;                 // 0 = trong cửa sổ, 1 = Scene view
    private PreviewRenderUtility _renderer;
    private GameObject _windowCharacter;
    private GameObject _poseSource;           // bản sao ẩn trong scene: lấy pose rồi copy sang nhân vật preview
    private readonly List<(Transform source, Transform target)> _posePairs = new List<(Transform, Transform)>();
    private GameObject _previewFloor;
    private GameObject _previewBackdrop;
    private Material _previewFloorMaterial;
    private Material _previewBackdropMaterial;
    private Transform _previewWeaponSocket;
    private GameObject _previewWeapon;
    private WeaponData _previewWeaponData;
    private GameObject _previewDummy;
    private Material _previewDummyMaterial;
    private float _previewLight = 1.4f;
    private bool _windowPreviewBroken;
    private float _previewYaw = 25f;
    private float _previewPitch = 8f;
    private bool _invertOrbitX;
    private bool _invertOrbitY;
    private float _previewZoom = 1f;

    private CombatEffects _fx;
    private SerializedObject _fxObject;

    private AnimSetLibrary _animLibrary;
    private WeaponLibrary _weaponLibrary;
    private readonly Dictionary<string, List<string>> _clipCache = new Dictionary<string, List<string>>();
    private readonly HashSet<int> _expandedSets = new HashSet<int>();
    private readonly HashSet<int> _expandedWeapons = new HashSet<int>();

    private float _windowBefore = 0.12f;
    private float _windowAfter = 0.08f;

    private int _tab;
    private Vector2 _scroll;
    private Vector2 _scrollLeft;
    private Vector2 _scrollRight;
    private string _status;
    private readonly List<string> _validation = new List<string>();

    private WeaponData Weapon => _weapons.Count > 0 ? _weapons[Mathf.Clamp(_weaponIndex, 0, _weapons.Count - 1)] : null;
    private ComboChain Chain => Weapon != null && Weapon.Chains.Count > 0 ? Weapon.Chains[Mathf.Clamp(_chainIndex, 0, Weapon.Chains.Count - 1)] : null;
    private AttackStep Step => Chain != null && Chain.Steps.Count > 0 ? Chain.Steps[Mathf.Clamp(_stepIndex, 0, Chain.Steps.Count - 1)] : null;
    private float Length => Step != null && Step.Clip != null ? Mathf.Max(Step.Clip.length, 0.001f) : 1f;
    private float Normalized => Mathf.Clamp01(_time / Length);

    // Cửa sổ này giờ là 1 trang của Character Studio (Tools/Character Studio)
    public static void Open() => CharacterStudioWindow.Open(CharacterStudioWindow.Page.Timing);

    #region Nhúng vào Character Studio

    internal EditorWindow Host;
    internal int ForcedTab = -1;
    private int _animSetFilter = -1;
    private GameObject _previewPrefab;
    private Rect _hostRect;

    private Rect View => Host != null ? _hostRect : position;

    private new void Repaint()
    {
        if (Host != null) Host.Repaint();
        else base.Repaint();
    }

    private new void Focus()
    {
        if (Host != null) Host.Focus();
        else base.Focus();
    }

    internal void DrawEmbedded(Rect rect)
    {
        _hostRect = new Rect(0f, 0f, rect.width, rect.height);
        GUILayout.BeginArea(rect);
        OnGUI();
        GUILayout.EndArea();
    }

    // chỉ hiện vũ khí của 1 bộ anim (-1 = tất cả) + preview bằng prefab của nhân vật đó
    internal void SetCharacter(int animSetId, GameObject prefab)
    {
        if (animSetId != _animSetFilter)
        {
            _animSetFilter = animSetId;
            _weaponIndex = 0;
            _chainIndex = 0;
            _stepIndex = 0;
            RefreshWeapons();
            _autoSample = true;
        }
        if (prefab != _previewPrefab)
        {
            _previewPrefab = prefab;
            CleanupRenderer();
        }
    }

    internal IReadOnlyList<string> Validation => _validation;
    internal void Validate() => ValidateAll();
    internal void ReloadWeapons() => RefreshWeapons();

    #endregion

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        RefreshWeapons();
        ReadDetectorWindows();
        _fx = FindFx();
        _animLibrary = LocomotionAnimatorBuilder.FindLibrary();
        _weaponLibrary = WeaponComboBuilder.FindLibrary();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        _playing = false;
        if (_startedAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        _startedAnimationMode = false;
        if (_spawnedPreview != null) UnityEngine.Object.DestroyImmediate(_spawnedPreview);
        _spawnedPreview = null;
        _preview = null;
        CleanupRenderer();
    }

    // vào Play mode thì phải nhả AnimationMode, không thì pose của nhân vật bị preview ghi đè
    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode) return;
        _playing = false;
        StopPreview();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space && !EditorGUIUtility.editingTextField)
        {
            TogglePlay();
            Event.current.Use();
        }

        if (Event.current.type == EventType.KeyDown && !EditorGUIUtility.editingTextField &&
            (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace))
        {
            if (DeleteCurrentMarker())
            {
                Event.current.Use();
                return;
            }
        }

        // 3 phím thêm mốc nhanh tại kim: H = chém, K = đá, S = slam
        if (Event.current.type == EventType.KeyDown && !EditorGUIUtility.editingTextField && _tab == 0 && !Event.current.control && !Event.current.command)
        {
            if (Event.current.keyCode == KeyCode.H)
            {
                AddMarkerAtPlayhead(MarkerKind.Hit);
                Event.current.Use();
                return;
            }
            if (Event.current.keyCode == KeyCode.K)
            {
                AddMarkerAtPlayhead(MarkerKind.Kick);
                Event.current.Use();
                return;
            }
            if (Event.current.keyCode == KeyCode.S)
            {
                AddMarkerAtPlayhead(MarkerKind.Slam);
                Event.current.Use();
                return;
            }
        }

        if (ForcedTab >= 0) _tab = ForcedTab;
        else _tab = GUILayout.Toolbar(_tab, new[] { "Mốc đánh", "Âm thanh", "Bộ anim & Vũ khí", "Build" });
        switch (_tab)
        {
            case 0:
                DrawTimingTab();
                break;
            case 1:
                DrawSfxTab();
                break;
            case 2:
                DrawLibrariesTab();
                break;
            default:
                DrawBuildTab();
                break;
        }

        if (_tab != 0 && !string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
    }

    // ------------------------------------------------------------------
    // Tab 1: timeline mốc đánh
    // ------------------------------------------------------------------

    private void DrawTimingTab()
    {
        if (_weapons.Count == 0)
        {
            GUILayout.Space(20f);
            EditorGUILayout.HelpBox(
                "Nhân vật này chưa có WeaponData nên chưa có đòn để chỉnh.\n" +
                "1) Trang 'Vũ khí & Combo': thêm vũ khí + chuỗi đòn\n" +
                "2) Trang 'Build': bấm 'Build vũ khí của nhân vật'",
                MessageType.Warning);
            return;
        }

        ClampSelection();

        if (_autoSample)
        {
            _autoSample = false;
            SampleNow();
        }

        AttackStep step = Step;

        // Bố cục: trái = danh sách, giữa = preview, phải = thuộc tính, dưới = timeline
        float top = ForcedTab >= 0 ? 2f : 26f;
        const float gap = 4f;
        const float leftWidth = 208f;
        const float rightWidth = 300f;
        const float bottomHeight = 186f;
        float statusHeight = 0f;
        float mainHeight = Mathf.Max(View.height - top - bottomHeight - statusHeight - 14f, 220f);

        Rect leftRect = new Rect(4f, top, leftWidth, mainHeight);
        Rect bottomRect = new Rect(4f, top + mainHeight + gap, Mathf.Max(View.width - 8f, 320f), bottomHeight);
        float centerWidth = Mathf.Max(View.width - leftWidth - rightWidth - gap * 4f - 8f, 220f);
        Rect centerRect = new Rect(leftRect.xMax + gap, top, centerWidth, mainHeight);
        Rect rightRect = new Rect(centerRect.xMax + gap, top, rightWidth, mainHeight);

        GUILayout.BeginArea(leftRect, EditorStyles.helpBox);
        DrawLeftPanel();
        GUILayout.EndArea();

        GUILayout.BeginArea(centerRect, EditorStyles.helpBox);
        DrawCenterPanel(step);
        GUILayout.EndArea();

        GUILayout.BeginArea(rightRect, EditorStyles.helpBox);
        DrawRightPanel(step);
        GUILayout.EndArea();

        GUILayout.BeginArea(bottomRect, EditorStyles.helpBox);
        DrawBottomBar(step);
        GUILayout.EndArea();
    }

    /// <summary>Cột trái: chọn vũ khí -> chuỗi -> đòn.</summary>
    private void DrawLeftPanel()
    {
        _scrollLeft = GUILayout.BeginScrollView(_scrollLeft);

        GUILayout.BeginHorizontal();
        GUILayout.Label("VŨ KHÍ", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("↻", "Quét lại danh sách WeaponData trong project"), EditorStyles.miniButton, GUILayout.Width(22f))) RefreshWeapons();
        GUILayout.EndHorizontal();
        for (int index = 0; index < _weapons.Count; index++)
        {
            if (!DrawListButton(_weapons[index].DisplayName, index == _weaponIndex)) continue;
            _weaponIndex = index;
            _chainIndex = 0;
            _stepIndex = 0;
            _playing = false;
            _time = 0f;
            _autoSample = true;
        }
        if (GUILayout.Button(new GUIContent("Chọn asset", "Chọn WeaponData trong Project"), EditorStyles.miniButton))
        {
            if (Weapon != null)
            {
                Selection.activeObject = Weapon;
                EditorGUIUtility.PingObject(Weapon);
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("CHUỖI", EditorStyles.boldLabel);
        WeaponData weapon = Weapon;
        if (weapon == null || weapon.Chains.Count == 0)
        {
            GUILayout.Label("vũ khí chưa có chuỗi", EditorStyles.miniLabel);
        }
        else
        {
            for (int index = 0; index < weapon.Chains.Count; index++)
            {
                ComboChain chain = weapon.Chains[index];
                if (!DrawListButton(chain.Name + "   (" + chain.Context + ")", index == _chainIndex)) continue;
                _chainIndex = index;
                _stepIndex = 0;
                _playing = false;
                _time = 0f;
                _autoSample = true;
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("ĐÒN", EditorStyles.boldLabel);
        ComboChain current = Chain;
        if (current == null || current.Steps.Count == 0)
        {
            GUILayout.Label("chuỗi này rỗng", EditorStyles.miniLabel);
        }
        else
        {
            for (int index = 0; index < current.Steps.Count; index++)
            {
                AttackStep candidate = current.Steps[index];
                if (!DrawListButton((index + 1) + ". " + candidate.Name, index == _stepIndex)) continue;
                _stepIndex = index;
                _playing = false;
                _time = 0f;
                _autoSample = true;
            }
        }

        GUILayout.EndScrollView();
    }

    private static bool DrawListButton(string label, bool selected)
    {
        Color previous = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);
        bool clicked = GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(21f));
        GUI.backgroundColor = previous;
        return clicked;
    }

    /// <summary>Cột phải: thông số đòn + các mốc.</summary>
    private void DrawRightPanel(AttackStep step)
    {
        if (step == null)
        {
            GUILayout.Label("Chọn một đòn ở cột trái", EditorStyles.miniLabel);
            return;
        }

        _scrollRight = GUILayout.BeginScrollView(_scrollRight);

        // thêm mốc nhanh tại kim: 3 nút = 3 phím tắt
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("＋ Chém (H)", "Thêm mốc chém tại kim"), EditorStyles.miniButton)) AddMarkerAtPlayhead(MarkerKind.Hit);
        if (GUILayout.Button(new GUIContent("＋ Đá (K)", "Thêm mốc đá tại kim"), EditorStyles.miniButton)) AddMarkerAtPlayhead(MarkerKind.Kick);
        if (GUILayout.Button(new GUIContent("＋ Slam (S)", "Đặt mốc đập đất tại kim"), EditorStyles.miniButton)) AddMarkerAtPlayhead(MarkerKind.Slam);
        GUILayout.EndHorizontal();

        DrawStepNumbers(step);
        GUILayout.Space(4f);
        DrawLinkCancelRows(step);
        GUILayout.Space(4f);
        DrawTimesEditor("Mốc chém", step.HitTimes, MarkerKind.Hit);
        DrawTimesEditor("Mốc đá", step.KickTimes, MarkerKind.Kick);
        DrawSlamRow(step);
        GUILayout.Space(4f);
        DrawStepWarnings(step);
        GUILayout.EndScrollView();
    }

    /// <summary>Giữa: preview nhân vật.</summary>
    private void DrawCenterPanel(AttackStep step)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("PREVIEW", EditorStyles.boldLabel, GUILayout.Width(64f));
        _previewMode = GUILayout.Toolbar(_previewMode, new[] { "Trong cửa sổ", "Scene view" }, EditorStyles.miniButton, GUILayout.Width(206f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Dừng", "Tắt AnimationMode, trả nhân vật về pose gốc"), EditorStyles.miniButton, GUILayout.Width(50f))) StopPreview();
        GUILayout.EndHorizontal();

        if (_previewMode == 0)
        {
            GUILayout.Label("kéo chuột = xoay · lăn chuột = zoom · Space = phát/dừng · kéo mốc dưới timeline",
                EditorStyles.miniLabel);
            GUILayout.BeginHorizontal();
            _previewLight = EditorGUILayout.Slider(new GUIContent("Độ sáng đèn", "Tăng nếu nhân vật tối, giảm nếu cháy trắng"), _previewLight, 0.1f, 4f);
            if (GUILayout.Button(new GUIContent("Mặc định", "Về 1.4"), EditorStyles.miniButton, GUILayout.Width(66f))) _previewLight = 1.4f;
            _invertOrbitX = GUILayout.Toggle(_invertOrbitX, new GUIContent("Đảo X", "Đảo chiều kéo ngang"), EditorStyles.miniButton, GUILayout.Width(56f));
            _invertOrbitY = GUILayout.Toggle(_invertOrbitY, new GUIContent("Đảo Y", "Đảo chiều kéo dọc"), EditorStyles.miniButton, GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            DrawWindowViewport(step);
        }
        else
        {
            DrawScenePreviewRow();
        }
    }

    /// <summary>Đáy: nút phát + timeline chạy hết chiều ngang.</summary>
    private void DrawBottomBar(AttackStep step)
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(132f));
        if (GUILayout.Button(_playing ? "⏸ Dừng" : "▶ Phát", GUILayout.Height(28f))) TogglePlay();
        if (GUILayout.Button("⏮ Về đầu", GUILayout.Height(22f)))
        {
            _time = 0f;
            SampleNow();
        }
        if (GUILayout.Button("⏭ Tới cuối", GUILayout.Height(22f)))
        {
            _time = Length;
            SampleNow();
        }
        _loop = GUILayout.Toggle(_loop, new GUIContent("Lặp", "Phát lặp lại clip"), EditorStyles.miniButton, GUILayout.Width(56f));
        GUILayout.BeginHorizontal();
        _snapFrame = GUILayout.Toggle(_snapFrame, new GUIContent("Snap", "Bám frame khi kéo mốc"), EditorStyles.miniButton, GUILayout.Width(50f));
        _snapFps = EditorGUILayout.IntField(_snapFps, GUILayout.Width(30f));
        GUILayout.EndHorizontal();
        _sfxWhileScrub = GUILayout.Toggle(_sfxWhileScrub, new GUIContent("Tiếng khi kéo", "Phát tiếng khi kim đi qua mốc lúc kéo"), EditorStyles.miniButton);
        if (GUILayout.Button(new GUIContent("Xoá mốc (Delete)", "Xoá mốc đang trỏ / đang chọn / gần kim nhất"), EditorStyles.miniButton)) DeleteCurrentMarker();
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        if (step != null) DrawTimeline(step);
        GUILayout.BeginHorizontal();
        LegendItem("chém", HitColor, "Mốc lưỡi kiếm chém trúng — mở cửa sổ quét va chạm");
        LegendItem("đá", KickColor, "Mốc chân đá mạnh nhất");
        LegendItem("slam", SlamColor, "Mốc kiếm đập xuống đất");
        LegendItem("nối", LinkColor, "LinkTime — kéo được. Bấm sẵn thì nối đòn kế từ đây");
        LegendItem("huỷ", CancelColor, "MoveCancelTime — kéo được. Từ đây đẩy stick là huỷ hồi chiêu");
        LegendItem("dải xanh", new Color(0.35f, 0.95f, 0.45f, 0.45f), "Vùng MeleeHitDetector quét lưỡi quanh mốc chém");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(string.IsNullOrEmpty(_status) ? "Space = phát/dừng   ·   Alt + click = xoá mốc" : _status, EditorStyles.miniLabel);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private static void LegendItem(string label, Color color, string tooltip)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 12f, GUILayout.Width(10f));
        if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, color);
        GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.miniLabel);
        GUILayout.Space(8f);
    }

    private void DrawStepNumbers(AttackStep step)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        float speed = EditorGUILayout.FloatField(new GUIContent("Speed", "Nhân tốc độ phát của state"), step.Speed);
        float fade = EditorGUILayout.FloatField(new GUIContent("FadeIn", "Thời gian crossfade vào đòn (giây)"), step.FadeIn);
        float weight = EditorGUILayout.Slider(new GUIContent("Weight", "0 = đòn nhẹ, 1 = đòn nặng nhất của vũ khí"), step.Weight, 0f, 1f);
        GUILayout.EndVertical();

        if (Mathf.Approximately(speed, step.Speed) && Mathf.Approximately(fade, step.FadeIn) && Mathf.Approximately(weight, step.Weight)) return;

        Undo.RecordObject(Weapon, "Sửa thông số đòn");
        step.Speed = Mathf.Max(0.01f, speed);
        step.FadeIn = Mathf.Max(0f, fade);
        step.Weight = Mathf.Clamp01(weight);
        EditorUtility.SetDirty(Weapon);
    }

    private void DrawTimeline(AttackStep step)
    {
        Rect area = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(138f), GUILayout.ExpandWidth(true));
        Rect strip = new Rect(area.x + 10f, area.y + 28f, Mathf.Max(area.width - 20f, 20f), TrackHeight);

        // hover trước khi vẽ để highlight đúng marker
        _hoverKind = MarkerKind.None;
        _hoverIndex = -1;
        if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseMove)
        {
            FindMarker(step, strip, Event.current.mousePosition, out _hoverKind, out _hoverIndex, out _);
        }

        _lastTimelineStrip = strip;

        // đổi đòn thì bỏ chọn mốc (chỉ số cũ không còn nghĩa)
        if (Step != _selectedStep)
        {
            _selectedStep = Step;
            _selectedKind = MarkerKind.None;
            _selectedIndex = -1;
        }

        GUI.Label(new Rect(strip.x, area.y + 6f, strip.width, 16f),
            $"{(step.Clip != null ? step.Clip.name : "(thiếu clip)")}   •   dài {Length:F2}s   •   Speed {step.Speed:F2}",
            EditorStyles.miniBoldLabel);

        if (Event.current.type == EventType.Repaint) DrawTimelineBackground(step, strip);
        HandleTimelineEvents(step, strip);

        if (Event.current.type == EventType.Repaint && _hoverKind != MarkerKind.None)
        {
            Vector2 mouse = Event.current.mousePosition;
            float hoverTime = MarkerNormalized(step, _hoverKind, _hoverIndex) * Length;
            GUIContent content = new GUIContent(MarkerDescription(_hoverKind, _hoverIndex, hoverTime));
            Vector2 size = EditorStyles.helpBox.CalcSize(content);
            float x = Mathf.Clamp(mouse.x + 14f, strip.x, Mathf.Max(strip.x, strip.xMax - size.x));
            float y = strip.y - size.y - 6f;
            if (y < area.y + 2f) y = strip.yMax + 4f; // hết chỗ phía trên thì bung xuống dưới
            GUI.Box(new Rect(x, y, size.x, size.y), content, EditorStyles.helpBox);
        }

        if (strip.Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.AddCursorRect(strip, _hoverKind != MarkerKind.None ? MouseCursor.SlideArrow : MouseCursor.Text);
        }

        GUI.Label(new Rect(strip.x, strip.yMax + 5f, strip.width, 16f),
            $"kim {_time:F3}s ({Normalized:F3})   ·   H/K/S = thêm mốc chém/đá/slam tại kim · Delete = xoá · kéo kim để tua · chuột phải = menu",
            EditorStyles.miniLabel);
    }

    /// <summary>Giá trị normalized của 1 marker bất kỳ (kể cả nối/huỷ).</summary>
    private static float MarkerNormalized(AttackStep step, MarkerKind kind, int index)
    {
        switch (kind)
        {
            case MarkerKind.Hit: return index >= 0 && index < step.HitTimes.Count ? step.HitTimes[index] : 0f;
            case MarkerKind.Kick: return index >= 0 && index < step.KickTimes.Count ? step.KickTimes[index] : 0f;
            case MarkerKind.Slam: return Mathf.Max(step.SlamTime, 0f);
            case MarkerKind.Link: return step.LinkTime;
            case MarkerKind.Cancel: return step.MoveCancelTime;
            default: return 0f;
        }
    }

    private string MarkerDescription(MarkerKind kind, int index, float time)
    {
        float length = Mathf.Max(Length, 0.001f);
        switch (kind)
        {
            case MarkerKind.Hit:
                return $"Mốc chém #{index + 1} — {time:F3}s ({(time / length) * 100f:F0}%)\n" +
                       $"Lúc mũi kiếm đạt tốc độ cao nhất. MeleeHitDetector quét lưỡi trong khoảng −{_windowBefore:F2}s … +{_windowAfter:F2}s quanh mốc này.\n" +
                       "Kéo ngang để đổi · Alt + click để xoá.";
            case MarkerKind.Kick:
                return $"Mốc đá #{index + 1} — {time:F3}s ({(time / length) * 100f:F0}%)\n" +
                       "Lúc bàn chân duỗi mạnh nhất: chân đá trúng mục tiêu quanh mốc này.";
            case MarkerKind.Link:
                return $"Nối đòn (LinkTime) — {time:F3}s ({(time / length) * 100f:F0}%)\n" +
                       "Từ đây, nếu đã bấm sẵn thì ra đòn kế ngay (không phải chờ hết clip).\n" +
                       "Kéo ngang để đổi; luôn phải nằm trước vạch 'huỷ'.";
            case MarkerKind.Cancel:
                return $"Huỷ hồi chiêu (MoveCancelTime) — {time:F3}s ({(time / length) * 100f:F0}%)\n" +
                       "Từ đây đẩy cần di chuyển thì cắt animation đòn để chạy.\n" +
                       $"Mốc thực tế = max(mốc này, MoveCancelMinTime 0.75) = {Mathf.Max(Step != null ? Step.MoveCancelTime : 0.8f, 0.75f) * length:F3}s.";
            default:
                return $"Mốc đập đất — {time:F3}s ({(time / length) * 100f:F0}%)\n" +
                       "Lúc mũi kiếm chạm đất: tiếng slam + rung + tia lửa.";
        }
    }

    private void DrawTimelineBackground(AttackStep step, Rect strip)
    {
        EditorGUI.DrawRect(strip, new Color(0.13f, 0.13f, 0.14f, 1f));

        float playheadX = strip.x + Normalized * strip.width;
        if (playheadX > strip.x) EditorGUI.DrawRect(new Rect(strip.x, strip.y, playheadX - strip.x, strip.height), new Color(0.3f, 0.5f, 0.7f, 0.22f));

        // vạch 0.1s
        for (float t = 0f; t <= Length + 0.001f; t += 0.1f)
        {
            float x = strip.x + Mathf.Clamp01(t / Length) * strip.width;
            EditorGUI.DrawRect(new Rect(x, strip.y, 1f, 6f), new Color(1f, 1f, 1f, 0.16f));
        }

        // cửa sổ quét thật của MeleeHitDetector quanh mỗi mốc chém
        foreach (float hit in step.HitTimes)
        {
            float from = strip.x + Mathf.Clamp01((hit * Length - _windowBefore) / Length) * strip.width;
            float to = strip.x + Mathf.Clamp01((hit * Length + _windowAfter) / Length) * strip.width;
            EditorGUI.DrawRect(new Rect(from, strip.y, Mathf.Max(to - from, 1f), strip.height), new Color(0.35f, 0.95f, 0.45f, 0.10f));
        }

        DrawGuideLine(strip, step.LinkTime, LinkColor, "nối", _hoverKind == MarkerKind.Link || _selectedKind == MarkerKind.Link);
        DrawGuideLine(strip, step.MoveCancelTime, CancelColor, "huỷ", _hoverKind == MarkerKind.Cancel || _selectedKind == MarkerKind.Cancel);
        if (step.NextSeamTime >= 0f) DrawGuideLine(strip, step.NextSeamTime / Length, SeamColor, "seam", false);

        for (int i = 0; i < step.HitTimes.Count; i++)
            DrawPin(strip, step.HitTimes[i], HitColor, 0f, 1f, "chém " + (i + 1), (_hoverKind == MarkerKind.Hit && _hoverIndex == i) || (_selectedKind == MarkerKind.Hit && _selectedIndex == i));
        for (int i = 0; i < step.KickTimes.Count; i++)
            DrawPin(strip, step.KickTimes[i], KickColor, 0.45f, 1f, "đá " + (i + 1), (_hoverKind == MarkerKind.Kick && _hoverIndex == i) || (_selectedKind == MarkerKind.Kick && _selectedIndex == i));
        if (step.SlamTime >= 0f)
            DrawPin(strip, step.SlamTime, SlamColor, 0f, 1f, "slam", _hoverKind == MarkerKind.Slam || _selectedKind == MarkerKind.Slam);

        EditorGUI.DrawRect(new Rect(playheadX - 1f, strip.y - 5f, 2f, strip.height + 10f), Color.white);
    }

    private static void DrawPin(Rect strip, float normalized, Color color, float top, float bottom, string label, bool highlight)
    {
        float x = strip.x + Mathf.Clamp01(normalized) * strip.width;
        float y = strip.y + top * strip.height;
        float height = (bottom - top) * strip.height;
        if (highlight) EditorGUI.DrawRect(new Rect(x - 4f, y - 2f, 8f, height + 4f), new Color(1f, 1f, 1f, 0.35f));
        EditorGUI.DrawRect(new Rect(x - 1.5f, y, 3f, height), color);
        EditorGUI.DrawRect(new Rect(x - 4.5f, y, 9f, 4f), color);
        GUI.Label(new Rect(x - 26f, y + 4f, 52f, 12f), label, EditorStyles.centeredGreyMiniLabel);
    }

    private static void DrawGuideLine(Rect strip, float normalized, Color color, string label, bool highlight)
    {
        float x = strip.x + Mathf.Clamp01(normalized) * strip.width;
        if (highlight) EditorGUI.DrawRect(new Rect(x - 5f, strip.y, 10f, strip.height), new Color(1f, 1f, 1f, 0.30f));
        EditorGUI.DrawRect(new Rect(x - 1f, strip.y, 2f, strip.height), color);
        // tay nắm ở đầu vạch cho dễ bắt chuột
        EditorGUI.DrawRect(new Rect(x - 5f, strip.y - 6f, 10f, 7f), color);
        // sát mép phải thì thôi vẽ nhãn, tránh chữ bị cắt cụt
        if (x < strip.xMax - 36f) GUI.Label(new Rect(x + 2f, strip.yMax - 13f, 40f, 12f), label, EditorStyles.miniLabel);
    }

    private void HandleTimelineEvents(AttackStep step, Rect strip)
    {
        Event current = Event.current;
        Rect grab = new Rect(strip.x - 6f, strip.y - 18f, strip.width + 12f, strip.height + 24f);

        // chuột phải lên timeline: menu xoá (luôn có mục xoá mốc gần kim)
        if (current.type == EventType.MouseDown && current.button == 1 && grab.Contains(current.mousePosition))
        {
            bool found = FindMarker(step, strip, current.mousePosition, out MarkerKind menuKind, out int menuIndex, out _);
            ShowTimelineMenu(step, found, menuKind, menuIndex);
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 0 && grab.Contains(current.mousePosition))
        {
            Focus(); // để phím Delete/Space tới được cửa sổ này

            if (!FindMarker(step, strip, current.mousePosition, out MarkerKind kind, out int index, out float markerTime))
            {
                // click/kéo chỗ trống = kéo kim (scrub), kể cả khi bấm đúng vạch kim
                _time = Snap(TimeAt(strip, current.mousePosition));
                _scrubbing = true;
                _playing = false;
                _selectedKind = MarkerKind.None;
                _selectedIndex = -1;
                SampleNow();
                if (_sfxWhileScrub) PlaySoundNear(step, _time);
            }
            else if (current.alt)
            {
                DeleteMarker(step, kind, index);
            }
            else
            {
                _dragKind = kind;
                _dragIndex = index;
                _selectedKind = kind;
                _selectedIndex = index;
                _time = markerTime;
                Undo.RecordObject(Weapon, "Kéo mốc đánh");
                PlayMarkerSound(step, kind);
                SampleNow();
                _status = "Đang kéo " + KindLabel(kind) + " " + (index + 1) + " — Delete để xoá mốc này";
            }
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.MouseDrag && _scrubbing)
        {
            float previous = _time;
            _time = Snap(TimeAt(strip, current.mousePosition));
            SampleNow();
            if (_sfxWhileScrub) PlayCrossedSounds(step, previous, _time);
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.MouseDrag && _dragKind != MarkerKind.None)
        {
            float seconds = Snap(TimeAt(strip, current.mousePosition));
            SetMarker(step, _dragKind, _dragIndex, Mathf.Clamp01(seconds / Length));
            _time = seconds;
            EditorUtility.SetDirty(Weapon);
            SampleNow();
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow)
        {
            _scrubbing = false;
            if (_dragKind == MarkerKind.None) return;

            // sắp xếp sau khi thả: lúc kéo không sort để chỉ số mốc khỏi nhảy
            if (_dragKind == MarkerKind.Hit) step.HitTimes.Sort();
            else if (_dragKind == MarkerKind.Kick) step.KickTimes.Sort();
            _dragKind = MarkerKind.None;
            _dragIndex = -1;
            if (Weapon != null) AssetDatabase.SaveAssetIfDirty(Weapon);
            current.Use();
            Repaint();
        }
    }

    private float TimeAt(Rect strip, Vector2 mouse)
    {
        float u = Mathf.Clamp01((mouse.x - strip.x) / Mathf.Max(strip.width, 1f));
        return u * Length;
    }

    private float Snap(float seconds)
    {
        if (!_snapFrame || _snapFps <= 0) return seconds;
        return Mathf.Round(seconds * _snapFps) / _snapFps;
    }

    private bool FindMarker(AttackStep step, Rect strip, Vector2 mouse, out MarkerKind kind, out int index, out float time)
    {
        kind = MarkerKind.None;
        index = -1;
        time = 0f;
        float best = GrabRadius;

        for (int i = 0; i < step.HitTimes.Count; i++)
        {
            float distance = Mathf.Abs(mouse.x - (strip.x + step.HitTimes[i] * strip.width));
            if (distance > best) continue;
            best = distance;
            kind = MarkerKind.Hit;
            index = i;
            time = step.HitTimes[i] * Length;
        }
        for (int i = 0; i < step.KickTimes.Count; i++)
        {
            float distance = Mathf.Abs(mouse.x - (strip.x + step.KickTimes[i] * strip.width)) + 2f;
            if (distance > best) continue;
            best = distance;
            kind = MarkerKind.Kick;
            index = i;
            time = step.KickTimes[i] * Length;
        }
        if (step.SlamTime >= 0f)
        {
            float distance = Mathf.Abs(mouse.x - (strip.x + step.SlamTime * strip.width)) + 3f;
            if (distance <= best)
            {
                best = distance;
                kind = MarkerKind.Slam;
                index = 0;
                time = step.SlamTime * Length;
            }
        }

        // vạch nối / huỷ cũng kéo được; cộng nhẹ khoảng cách để mốc chém/đá được ưu tiên khi trùng chỗ
        float linkDistance = Mathf.Abs(mouse.x - (strip.x + step.LinkTime * strip.width)) + 1f;
        if (linkDistance <= best)
        {
            best = linkDistance;
            kind = MarkerKind.Link;
            index = 0;
            time = step.LinkTime * Length;
        }
        float cancelDistance = Mathf.Abs(mouse.x - (strip.x + step.MoveCancelTime * strip.width)) + 1.5f;
        if (cancelDistance <= best)
        {
            kind = MarkerKind.Cancel;
            index = 0;
            time = step.MoveCancelTime * Length;
        }
        return kind != MarkerKind.None;
    }

    private static void SetMarker(AttackStep step, MarkerKind kind, int index, float normalized)
    {
        if (kind == MarkerKind.Hit && index >= 0 && index < step.HitTimes.Count) step.HitTimes[index] = normalized;
        else if (kind == MarkerKind.Kick && index >= 0 && index < step.KickTimes.Count) step.KickTimes[index] = normalized;
        else if (kind == MarkerKind.Slam) step.SlamTime = normalized;
        else if (kind == MarkerKind.Link) step.LinkTime = Mathf.Clamp(normalized, 0.01f, step.MoveCancelTime - 0.02f);
        else if (kind == MarkerKind.Cancel) step.MoveCancelTime = Mathf.Clamp(normalized, step.LinkTime + 0.02f, 0.99f);
    }

    private void DeleteMarker(AttackStep step, MarkerKind kind, int index)
    {
        if (Weapon == null) return;
        if (kind == MarkerKind.Link || kind == MarkerKind.Cancel)
        {
            _status = "Vạch nối/huỷ không xoá được — chỉ kéo hoặc nhập số.";
            return;
        }

        Undo.RecordObject(Weapon, "Xoá mốc đánh");
        if (kind == MarkerKind.Hit && index >= 0 && index < step.HitTimes.Count) step.HitTimes.RemoveAt(index);
        else if (kind == MarkerKind.Kick && index >= 0 && index < step.KickTimes.Count) step.KickTimes.RemoveAt(index);
        else if (kind == MarkerKind.Slam) step.SlamTime = -1f;
        EditorUtility.SetDirty(Weapon);
        AssetDatabase.SaveAssetIfDirty(Weapon);
        _status = "Đã xoá " + KindLabel(kind);
        Repaint();
    }

    /// <summary>Menu chuột phải trên timeline: xoá mốc đang trỏ, mốc gần kim, hoặc xoá cả loạt.</summary>
    private void ShowTimelineMenu(AttackStep step, bool found, MarkerKind kind, int index)
    {
        GenericMenu menu = new GenericMenu();

        if (found)
        {
            menu.AddItem(new GUIContent("Đặt " + KindLabel(kind) + " tại kim"), false, () =>
            {
                Undo.RecordObject(Weapon, "Đặt " + KindLabel(kind) + " tại kim");
                SetMarker(step, kind, index, Normalized);
                step.HitTimes.Sort();
                step.KickTimes.Sort();
                EditorUtility.SetDirty(Weapon);
                AssetDatabase.SaveAssetIfDirty(Weapon);
                Repaint();
            });

            if (kind == MarkerKind.Link || kind == MarkerKind.Cancel)
            {
                menu.AddItem(new GUIContent("Về giá trị mặc định (" + (kind == MarkerKind.Link ? "0.6" : "0.8") + ")"), false, () =>
                {
                    Undo.RecordObject(Weapon, "Đặt lại " + KindLabel(kind));
                    SetMarker(step, kind, index, kind == MarkerKind.Link ? 0.6f : 0.8f);
                    EditorUtility.SetDirty(Weapon);
                    AssetDatabase.SaveAssetIfDirty(Weapon);
                    Repaint();
                });
            }
            else
            {
                menu.AddItem(new GUIContent("Xoá " + KindLabel(kind) + (kind == MarkerKind.Slam ? string.Empty : " #" + (index + 1))), false, () => DeleteMarker(step, kind, index));
            }
            menu.AddSeparator(string.Empty);
        }

        menu.AddItem(new GUIContent("Xoá mốc gần kim nhất (Delete)"), false, () => DeleteMarkerNearPlayhead());
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Xoá tất cả mốc chém"), false, () => ClearAllMarkers(step, MarkerKind.Hit));
        menu.AddItem(new GUIContent("Xoá tất cả mốc đá"), false, () => ClearAllMarkers(step, MarkerKind.Kick));
        menu.AddItem(new GUIContent("Bỏ mốc đập đất"), false, () => ClearAllMarkers(step, MarkerKind.Slam));
        menu.ShowAsContext();
    }

    private void ClearAllMarkers(AttackStep step, MarkerKind kind)
    {
        if (Weapon == null || step == null) return;
        Undo.RecordObject(Weapon, "Xoá tất cả " + KindLabel(kind));
        if (kind == MarkerKind.Hit) step.HitTimes.Clear();
        else if (kind == MarkerKind.Kick) step.KickTimes.Clear();
        else if (kind == MarkerKind.Slam) step.SlamTime = -1f;
        EditorUtility.SetDirty(Weapon);
        AssetDatabase.SaveAssetIfDirty(Weapon);
        _status = "Đã xoá tất cả " + KindLabel(kind);
        Repaint();
    }

    /// <summary>Thêm mốc tại đúng vị trí kim (dùng cho nút ＋ và phím tắt H/K/S).</summary>
    private void AddMarkerAtPlayhead(MarkerKind kind)
    {
        AttackStep step = Step;
        if (Weapon == null || step == null)
        {
            _status = "Chưa chọn đòn nào để thêm mốc";
            return;
        }

        Undo.RecordObject(Weapon, "Thêm " + KindLabel(kind));
        if (kind == MarkerKind.Hit)
        {
            step.HitTimes.Add(Normalized);
            step.HitTimes.Sort();
        }
        else if (kind == MarkerKind.Kick)
        {
            step.KickTimes.Add(Normalized);
            step.KickTimes.Sort();
        }
        else if (kind == MarkerKind.Slam)
        {
            step.SlamTime = Mathf.Clamp01(Normalized);
        }

        EditorUtility.SetDirty(Weapon);
        AssetDatabase.SaveAssetIfDirty(Weapon);
        _status = "Đã thêm " + KindLabel(kind) + " tại " + (Normalized * Length).ToString("F3") + "s";
        PlayMarkerSound(step, kind);
        Repaint();
    }

    /// <summary>Phím Delete: ưu tiên mốc đang trỏ chuột, rồi mốc đang chọn, cuối cùng là mốc gần kim.</summary>
    private bool DeleteCurrentMarker()
    {
        AttackStep step = Step;
        if (step == null || _tab != 0) return false;

        if (_lastTimelineStrip.width > 0f && _lastTimelineStrip.Contains(Event.current.mousePosition) &&
            FindMarker(step, _lastTimelineStrip, Event.current.mousePosition, out MarkerKind hoverKind, out int hoverIndex, out _))
        {
            if (hoverKind == MarkerKind.Link || hoverKind == MarkerKind.Cancel)
            {
                _status = "Vạch nối/huỷ không xoá được — chỉ kéo hoặc nhập số.";
                return false;
            }
            DeleteMarker(step, hoverKind, hoverIndex);
            return true;
        }

        if (_selectedKind != MarkerKind.None)
        {
            if (_selectedKind == MarkerKind.Link || _selectedKind == MarkerKind.Cancel)
            {
                _status = "Vạch nối/huỷ không xoá được — chỉ kéo hoặc nhập số.";
                return false;
            }
            DeleteMarker(step, _selectedKind, _selectedIndex);
            _selectedKind = MarkerKind.None;
            _selectedIndex = -1;
            return true;
        }

        return DeleteMarkerNearPlayhead();
    }

    /// <summary>Phím Delete: xoá marker gần kim nhất (trong 4% độ dài clip).</summary>
    private bool DeleteMarkerNearPlayhead()
    {
        AttackStep step = Step;
        if (step == null || _tab != 0) return false;

        MarkerKind kind = MarkerKind.None;
        int index = -1;
        float best = 0.04f;

        for (int i = 0; i < step.HitTimes.Count; i++)
        {
            float distance = Mathf.Abs(step.HitTimes[i] - Normalized);
            if (distance >= best) continue;
            best = distance;
            kind = MarkerKind.Hit;
            index = i;
        }
        for (int i = 0; i < step.KickTimes.Count; i++)
        {
            float distance = Mathf.Abs(step.KickTimes[i] - Normalized);
            if (distance >= best) continue;
            best = distance;
            kind = MarkerKind.Kick;
            index = i;
        }
        if (step.SlamTime >= 0f)
        {
            float distance = Mathf.Abs(step.SlamTime - Normalized);
            if (distance < best)
            {
                kind = MarkerKind.Slam;
                index = 0;
            }
        }

        if (kind == MarkerKind.None)
        {
            _status = "Không có mốc nào gần kim (đưa kim sát mốc rồi bấm Delete)";
            return false;
        }
        DeleteMarker(step, kind, index);
        return true;
    }

    private static string KindLabel(MarkerKind kind)
    {
        switch (kind)
        {
            case MarkerKind.Hit: return "mốc chém";
            case MarkerKind.Kick: return "mốc đá";
            case MarkerKind.Slam: return "mốc đập đất";
            case MarkerKind.Link: return "vạch nối";
            case MarkerKind.Cancel: return "vạch huỷ";
            default: return "mốc";
        }
    }

    /// <summary>Hai mốc "nối"/"huỷ": kéo trên timeline, bấm −/+, hoặc nhập giây.</summary>
    private void DrawLinkCancelRows(AttackStep step)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(new GUIContent("Nối / huỷ", "Kéo tay nắm ở đầu vạch vàng (nối) hoặc xám (huỷ) trên timeline, hoặc dùng −/+ để nhích từng frame"), EditorStyles.boldLabel);

        float length = Mathf.Max(Length, 0.001f);

        GUILayout.BeginHorizontal();
        float linkSeconds = EditorGUILayout.FloatField(new GUIContent("Nối đòn", "LinkTime: từ giây này bấm sẵn thì ra đòn kế"), step.LinkTime * length);
        if (GUILayout.Button(new GUIContent("−", "Lùi 1 frame"), EditorStyles.miniButton, GUILayout.Width(24f))) NudgeLink(step, -1f);
        if (GUILayout.Button(new GUIContent("+", "Tiến 1 frame"), EditorStyles.miniButton, GUILayout.Width(24f))) NudgeLink(step, 1f);
        if (GUILayout.Button(new GUIContent("= kim", "Đặt bằng vị trí kim"), EditorStyles.miniButton, GUILayout.Width(46f)))
        {
            Undo.RecordObject(Weapon, "Đặt LinkTime tại kim");
            step.LinkTime = Mathf.Clamp(Normalized, 0.01f, step.MoveCancelTime - 0.02f);
            EditorUtility.SetDirty(Weapon);
        }
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(linkSeconds, step.LinkTime * length))
        {
            Undo.RecordObject(Weapon, "Sửa LinkTime");
            step.LinkTime = Mathf.Clamp(linkSeconds / length, 0.01f, step.MoveCancelTime - 0.02f);
            EditorUtility.SetDirty(Weapon);
        }

        GUILayout.BeginHorizontal();
        float cancelSeconds = EditorGUILayout.FloatField(new GUIContent("Huỷ hồi chiêu", "MoveCancelTime: từ giây này đẩy cần thì cắt đòn để chạy"), step.MoveCancelTime * length);
        if (GUILayout.Button(new GUIContent("−", "Lùi 1 frame"), EditorStyles.miniButton, GUILayout.Width(24f))) NudgeCancel(step, -1f);
        if (GUILayout.Button(new GUIContent("+", "Tiến 1 frame"), EditorStyles.miniButton, GUILayout.Width(24f))) NudgeCancel(step, 1f);
        if (GUILayout.Button(new GUIContent("= kim", "Đặt bằng vị trí kim"), EditorStyles.miniButton, GUILayout.Width(46f)))
        {
            Undo.RecordObject(Weapon, "Đặt MoveCancelTime tại kim");
            step.MoveCancelTime = Mathf.Clamp(Normalized, step.LinkTime + 0.02f, 0.99f);
            EditorUtility.SetDirty(Weapon);
        }
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(cancelSeconds, step.MoveCancelTime * length))
        {
            Undo.RecordObject(Weapon, "Sửa MoveCancelTime");
            step.MoveCancelTime = Mathf.Clamp(cancelSeconds / length, step.LinkTime + 0.02f, 0.99f);
            EditorUtility.SetDirty(Weapon);
        }

        float effective = Mathf.Max(step.MoveCancelTime, 0.75f) * length;
        GUILayout.Label($"Mốc huỷ thực tế: {effective:F3}s (= max(MoveCancelTime, MoveCancelMinTime 0.75))", EditorStyles.miniLabel);
        GUILayout.EndVertical();
    }

    private void NudgeLink(AttackStep step, float direction)
    {
        float perFrame = 1f / Mathf.Max(_snapFps, 1);
        float normalizedStep = perFrame / Mathf.Max(Length, 0.001f);
        Undo.RecordObject(Weapon, "Nhích LinkTime");
        step.LinkTime = Mathf.Clamp(step.LinkTime + direction * normalizedStep, 0.01f, step.MoveCancelTime - 0.02f);
        EditorUtility.SetDirty(Weapon);
    }

    private void NudgeCancel(AttackStep step, float direction)
    {
        float perFrame = 1f / Mathf.Max(_snapFps, 1);
        float normalizedStep = perFrame / Mathf.Max(Length, 0.001f);
        Undo.RecordObject(Weapon, "Nhích MoveCancelTime");
        step.MoveCancelTime = Mathf.Clamp(step.MoveCancelTime + direction * normalizedStep, step.LinkTime + 0.02f, 0.99f);
        EditorUtility.SetDirty(Weapon);
    }

    private void DrawTimesEditor(string title, List<float> times, MarkerKind kind)
    {
        AttackStep step = Step;
        if (step == null) return;

        GUILayout.BeginVertical(EditorStyles.helpBox);

        GUILayout.Label(new GUIContent(title + "   ·   " + times.Count + " mốc", KindTooltip(kind)), EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        string shortcut = kind == MarkerKind.Hit ? " (H)" : kind == MarkerKind.Kick ? " (K)" : string.Empty;
        if (GUILayout.Button(new GUIContent("＋ tại kim" + shortcut, "Thêm mốc đúng vị trí con trỏ thời gian"), EditorStyles.miniButton, GUILayout.Width(118f)))
        {
            Undo.RecordObject(Weapon, "Thêm " + KindLabel(kind));
            times.Add(Normalized);
            times.Sort();
            EditorUtility.SetDirty(Weapon);
            AssetDatabase.SaveAssetIfDirty(Weapon);
        }
        if (times.Count > 1 && GUILayout.Button(new GUIContent("Sắp xếp", "Sắp xếp tăng dần (bắt buộc để mốc bắn đúng)"), EditorStyles.miniButton, GUILayout.Width(64f)))
        {
            Undo.RecordObject(Weapon, "Sắp xếp mốc");
            times.Sort();
            EditorUtility.SetDirty(Weapon);
        }
        GUILayout.EndHorizontal();

        if (times.Count == 0) GUILayout.Label("chưa có mốc nào", EditorStyles.miniLabel);

        for (int index = 0; index < times.Count; index++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("#" + (index + 1), EditorStyles.miniLabel, GUILayout.Width(24f));

            float seconds = times[index] * Length;
            float newSeconds = EditorGUILayout.FloatField(seconds, GUILayout.Width(54f));
            if (!Mathf.Approximately(newSeconds, seconds))
            {
                Undo.RecordObject(Weapon, "Sửa " + KindLabel(kind));
                times[index] = Mathf.Clamp01(newSeconds / Length);
                EditorUtility.SetDirty(Weapon);
            }
            GUILayout.Label("giây", EditorStyles.miniLabel, GUILayout.Width(26f));
            GUILayout.Label(times[index].ToString("F3"), EditorStyles.miniLabel, GUILayout.Width(38f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("▶", "Nghe thử tiếng ở mốc này"), EditorStyles.miniButton, GUILayout.Width(24f))) PlayMarkerSound(step, kind);
            bool remove = GUILayout.Button(new GUIContent("Xoá", "Xoá mốc này"), EditorStyles.miniButton, GUILayout.Width(38f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                Undo.RecordObject(Weapon, "Xoá " + KindLabel(kind));
                times.RemoveAt(index);
                EditorUtility.SetDirty(Weapon);
                break;
            }
        }

        GUILayout.EndVertical();
    }

    private static string KindTooltip(MarkerKind kind)
    {
        if (kind == MarkerKind.Hit) return "HitTimes — lúc lưỡi kiếm đạt tốc độ cao nhất (frame chém trúng), theo giây trong clip";
        if (kind == MarkerKind.Kick) return "KickTimes — lúc bàn chân đá mạnh nhất, theo giây trong clip";
        return "Mốc tính theo giây trong clip";
    }

    private void DrawSlamRow(AttackStep step)
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Đập đất  (SlamTime)", "Lúc mũi kiếm chạm đất: tiếng slam + rung + tia lửa"), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("＋ tại kim (S)", "Đặt mốc đập đất tại vị trí kim"), EditorStyles.miniButton, GUILayout.Width(90f)))
        {
            AddMarkerAtPlayhead(MarkerKind.Slam);
        }

        if (step.SlamTime >= 0f)
        {
            float seconds = step.SlamTime * Length;
            float newSeconds = EditorGUILayout.FloatField(seconds, GUILayout.Width(62f));
            if (!Mathf.Approximately(newSeconds, seconds))
            {
                Undo.RecordObject(Weapon, "Sửa mốc đập đất");
                step.SlamTime = Mathf.Clamp01(newSeconds / Length);
                EditorUtility.SetDirty(Weapon);
            }
            GUILayout.Label("giây", EditorStyles.miniLabel, GUILayout.Width(28f));
            if (GUILayout.Button(new GUIContent("▶", "Nghe thử tiếng slam"), EditorStyles.miniButton, GUILayout.Width(24f))) PlayMarkerSound(step, MarkerKind.Slam);
            if (GUILayout.Button(new GUIContent("Bỏ", "Xoá mốc đập đất"), EditorStyles.miniButton, GUILayout.Width(38f)))
            {
                Undo.RecordObject(Weapon, "Bỏ mốc đập đất");
                step.SlamTime = -1f;
                EditorUtility.SetDirty(Weapon);
                AssetDatabase.SaveAssetIfDirty(Weapon);
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawStepWarnings(AttackStep step)
    {
        List<string> issues = StepIssues(step);
        foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        if (issues.Count == 0) EditorGUILayout.HelpBox("Mốc ổn: đúng thứ tự, nằm trong clip, không chồng cửa sổ quét.", MessageType.Info);
    }

    private List<string> StepIssues(AttackStep step)
    {
        List<string> issues = new List<string>();
        float length = Mathf.Max(Length, 0.001f);

        if (step.HitTimes.Count == 0) issues.Add("Không có mốc chém: MeleeHitDetector sẽ không quét lưỡi ở đòn này.");
        for (int i = 1; i < step.HitTimes.Count; i++)
        {
            if (step.HitTimes[i] >= step.HitTimes[i - 1]) continue;
            issues.Add("HitTimes chưa sắp xếp tăng dần: CharacterCombo chỉ tiến một chiều nên mốc bị lộn sẽ không bao giờ bắn.");
            break;
        }
        CheckRange(issues, "HitTimes", step.HitTimes);
        CheckRange(issues, "KickTimes", step.KickTimes);

        float gap = _windowBefore + _windowAfter;
        if (step.HitTimes.Count > 1 && gap > 0f)
        {
            for (int i = 1; i < step.HitTimes.Count; i++)
            {
                float distance = (step.HitTimes[i] - step.HitTimes[i - 1]) * length;
                if (distance >= gap * 0.95f) continue;
                issues.Add($"Mốc chém #{i} và #{i + 1} chỉ cách {distance:F3}s, ngắn hơn cửa sổ quét {gap:F2}s -> cửa sổ chồng nhau, mốc sau bị rút ngắn.");
            }
        }

        if (step.SlamTime >= 0f && step.HitTimes.Count > 0 && step.SlamTime < step.HitTimes[0])
            issues.Add("SlamTime nằm trước mốc chém đầu tiên: kiểm tra lại có đúng là đập đất không.");

        return issues;
    }

    private static void CheckRange(List<string> issues, string name, List<float> times)
    {
        foreach (float time in times)
        {
            if (time < 0f || time > 1f)
            {
                issues.Add($"{name} có mốc {time:F3} nằm ngoài 0..1 (normalized).");
                break;
            }
        }
    }

    // ------------------------------------------------------------------
    // Tab 2: âm thanh
    // ------------------------------------------------------------------

    private void DrawSfxTab()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bộ SFX dùng lúc chạy game (CombatEffects)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sửa ở đây là sửa thẳng vào component CombatEffects đang có trong scene — cùng bộ clip mà MeleeHitDetector phát lúc chạy.\n" +
                "Nút \"Tự lấp\" tìm clip trong project theo từ khoá tên file, để có tiếng nghe thử ngay; sau đó bạn thay từng clip bằng file thật.",
                MessageType.Info);

            CombatEffects newFx = (CombatEffects)EditorGUILayout.ObjectField("CombatEffects", _fx, typeof(CombatEffects), true);
            if (newFx != _fx)
            {
                _fx = newFx;
                _fxObject = null;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tìm trong scene", GUILayout.Width(130f)))
            {
                _fx = FindFx();
                _fxObject = null;
                _status = _fx != null ? "Đã tìm thấy CombatEffects: " + _fx.name : "Scene không có CombatEffects";
            }
            if (_fx != null && GUILayout.Button("Chọn trong Hierarchy", GUILayout.Width(150f)))
            {
                Selection.activeObject = _fx;
                EditorGUIUtility.PingObject(_fx);
            }
            if (GUILayout.Button("Lấp tất cả theo tên", GUILayout.Width(140f)) && _fx != null)
            {
                if (_fxObject == null || _fxObject.targetObject != _fx) _fxObject = new SerializedObject(_fx);
                _fxObject.Update();
                int filled = 0;
                foreach (string[] set in SfxSets)
                {
                    SerializedProperty property = _fxObject.FindProperty(set[0]);
                    if (property != null && AutoFill(property, set[2]) > 0) filled++;
                }
                _fxObject.ApplyModifiedProperties();
                _status = $"Đã lấp {filled}/{SfxSets.Length} bộ clip (nghe thử bằng nút ▶)";
            }
            GUILayout.EndHorizontal();

            if (_fx == null)
            {
                EditorGUILayout.HelpBox("Không tìm thấy CombatEffects. Kéo component vào ô trên, hoặc mở scene có nhân vật rồi bấm Tìm trong scene.", MessageType.Warning);
                return;
            }

            if (_fxObject == null || _fxObject.targetObject != _fx) _fxObject = new SerializedObject(_fx);
            _fxObject.Update();

            EditorGUILayout.Space();
            foreach (string[] set in SfxSets) DrawClipArray(set[0], set[1], set[2]);

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Volume vung / trúng / ngưỡng đòn nặng", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(_fxObject.FindProperty("SwingVolume"));
            EditorGUILayout.PropertyField(_fxObject.FindProperty("HitVolume"));
            EditorGUILayout.PropertyField(_fxObject.FindProperty("HeavyThreshold"));
            EditorGUILayout.PropertyField(_fxObject.FindProperty("PitchVariation"));

            _fxObject.ApplyModifiedProperties();
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawClipArray(string propertyName, string label, string keywords)
    {
        SerializedProperty property = _fxObject.FindProperty(propertyName);
        if (property == null) return;

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(210f));
        GUILayout.Label(property.arraySize + " clip", EditorStyles.miniLabel, GUILayout.Width(52f));
        if (GUILayout.Button("▶", GUILayout.Width(24f))) PlayRandom(ToClips(property));
        if (GUILayout.Button("＋", GUILayout.Width(24f))) property.arraySize++;
        if (GUILayout.Button("Tự lấp", GUILayout.Width(56f)))
        {
            int count = AutoFill(property, keywords);
            _status = count > 0 ? $"Đã lấp {count} clip cho {label}" : $"Không tìm thấy clip khớp '{keywords}'";
        }
        GUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            GUILayout.BeginHorizontal();
            element.objectReferenceValue = EditorGUILayout.ObjectField(element.objectReferenceValue, typeof(AudioClip), false);
            if (GUILayout.Button("▶", GUILayout.Width(24f))) PlayClip(element.objectReferenceValue as AudioClip);
            if (GUILayout.Button("✕", GUILayout.Width(24f)))
            {
                int sizeBefore = property.arraySize;
                property.DeleteArrayElementAtIndex(i);
                if (property.arraySize == sizeBefore) property.DeleteArrayElementAtIndex(i);
                break;
            }
            GUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
    }

    private static AudioClip[] ToClips(SerializedProperty property)
    {
        List<AudioClip> clips = new List<AudioClip>();
        for (int i = 0; i < property.arraySize; i++)
        {
            AudioClip clip = property.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }

    private int AutoFill(SerializedProperty property, string keywords)
    {
        string[] words = keywords.Split(',');
        List<AudioClip> found = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null) continue;
            string lower = clip.name.ToLowerInvariant();
            if (words.Any(word => lower.Contains(word.Trim().ToLowerInvariant()))) found.Add(clip);
        }
        if (found.Count == 0) return 0;

        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        property.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        return found.Count;
    }

    // ------------------------------------------------------------------
    // Tab 3: bộ anim + vũ khí (dữ liệu cho 2 builder)
    // ------------------------------------------------------------------

    private void DrawLibrariesTab()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bộ anim nhân vật (AnimSetLibrary)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Mỗi bộ = 1 tiền tố state + 1 thư mục anim + bảng clip theo LocoState.\n" +
                "Thêm nhân vật/bộ vũ khí mới = bấm 'Thêm bộ anim', đặt prefix (vd NV_), trỏ thư mục rồi bấm ＋ State để map clip.\n" +
                "Sau khi sửa: Build Locomotion Animator để dựng lại layer.",
                MessageType.Info);

            AnimSetLibrary animLibrary = (AnimSetLibrary)EditorGUILayout.ObjectField("Asset", _animLibrary, typeof(AnimSetLibrary), false);
            if (animLibrary != _animLibrary) { _animLibrary = animLibrary; _clipCache.Clear(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tìm trong project", GUILayout.Width(130f))) _animLibrary = LocomotionAnimatorBuilder.FindLibrary();
            if (GUILayout.Button("Tạo / ghi đè từ code", GUILayout.Width(170f)) && ConfirmOverwrite(LocomotionAnimatorBuilder.LibraryPath))
            {
                _animLibrary = LocomotionAnimatorBuilder.CreateLibraryFromCode();
                _clipCache.Clear();
            }
            if (GUILayout.Button("Build Locomotion", GUILayout.Width(140f))) Run(LocomotionAnimatorBuilder.Build);
            if (_animLibrary != null && GUILayout.Button("Sinh prefix", GUILayout.Width(110f)))
            {
                LocoSetPrefixesGenerator.Generate(_animLibrary);
                _status = "Đã sinh prefix vào LocoSetPrefixes.Generated.cs — Unity sẽ compile lại.";
            }
            GUILayout.EndHorizontal();

            if (_animLibrary == null)
            {
                EditorGUILayout.HelpBox("Chưa có AnimSetLibrary. Bấm 'Tạo / ghi đè từ code' để lấy đúng bảng clip hiện tại làm mốc, rồi sửa tiếp trong asset.", MessageType.Warning);
            }
            else
            {
                DrawAnimSets(_animLibrary);
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("Vũ khí (WeaponLibrary)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Mỗi vũ khí = Key (tiền tố state) + thư mục anim + danh sách chuỗi (ngữ cảnh + các bước clip).\n" +
                "Thêm vũ khí mới = 'Thêm vũ khí', đặt Key, trỏ thư mục, thêm chuỗi/bước. Sau đó Build Weapon Combos.",
                MessageType.Info);

            WeaponLibrary weaponLibrary = (WeaponLibrary)EditorGUILayout.ObjectField("Asset", _weaponLibrary, typeof(WeaponLibrary), false);
            if (weaponLibrary != _weaponLibrary) { _weaponLibrary = weaponLibrary; _clipCache.Clear(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tìm trong project", GUILayout.Width(130f))) _weaponLibrary = WeaponComboBuilder.FindLibrary();
            if (GUILayout.Button("Tạo / ghi đè từ code", GUILayout.Width(170f)) && ConfirmOverwrite(WeaponComboBuilder.LibraryPath))
            {
                _weaponLibrary = WeaponComboBuilder.CreateLibraryFromCode();
                _clipCache.Clear();
            }
            if (GUILayout.Button("Build Weapon Combos", GUILayout.Width(160f))) Run(WeaponComboBuilder.BuildAll);
            GUILayout.EndHorizontal();

            if (_weaponLibrary == null)
            {
                EditorGUILayout.HelpBox("Chưa có WeaponLibrary. Bấm 'Tạo / ghi đè từ code' để lấy đúng 2 vũ khí hiện tại làm mốc.", MessageType.Warning);
            }
            else
            {
                DrawWeapons(_weaponLibrary);
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private static bool ConfirmOverwrite(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) == null) return true;
        return EditorUtility.DisplayDialog(
            "Ghi đè asset?",
            assetPath + " đã có.\nTạo lại từ bảng trong code sẽ mất mọi chỉnh tay trong asset này.\n\nTiếp tục?",
            "Ghi đè", "Huỷ");
    }

    private void DrawAnimSets(AnimSetLibrary library)
    {
        if (library.Sets.Count == 0) EditorGUILayout.HelpBox("Asset chưa có bộ anim nào.", MessageType.Warning);

        for (int index = 0; index < library.Sets.Count; index++)
        {
            AnimSetDefinition set = library.Sets[index];
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            bool expanded = _expandedSets.Contains(index);
            bool newExpanded = EditorGUILayout.Foldout(expanded, $"{set.Prefix}   {set.DisplayName}   ({set.OneShots.Count} state)", true);
            if (newExpanded != expanded)
            {
                if (newExpanded) _expandedSets.Add(index);
                else _expandedSets.Remove(index);
            }
            bool remove = GUILayout.Button("Xoá bộ", GUILayout.Width(66f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                Undo.RecordObject(library, "Xoá bộ anim");
                library.Sets.RemoveAt(index);
                EditorUtility.SetDirty(library);
                _expandedSets.Clear();
                GUILayout.EndVertical();
                break;
            }

            if (_expandedSets.Contains(index))
            {
                EditText(library, "Sửa prefix", "Prefix", set.Prefix, value => set.Prefix = value);
                EditText(library, "Sửa tên bộ", "Tên bộ", set.DisplayName, value => set.DisplayName = value);
                GUILayout.BeginHorizontal();
                EditText(library, "Sửa thư mục anim", "Thư mục anim", set.MovementFolder, value => { set.MovementFolder = value; _clipCache.Clear(); });
                if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    PickFolder(set.MovementFolder, value => { set.MovementFolder = value; _clipCache.Clear(); EditorUtility.SetDirty(library); });
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                GUILayout.Label("State one-shot / loop", EditorStyles.miniBoldLabel);
                for (int row = 0; row < set.OneShots.Count; row++)
                {
                    LocoClipDefinition entry = set.OneShots[row];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(entry.State.ToString(), GUILayout.Width(126f));
                    DrawClipName(library, set.MovementFolder, entry.Clip, value => entry.Clip = value);
                    EditToggle(library, entry.Loop, "Loop", value => entry.Loop = value, 52f);
                    EditToggle(library, entry.Air, "Air", value => entry.Air = value, 44f);
                    EditFloatInline(library, entry.Speed, value => entry.Speed = value, 38f);
                    EditIntInline(library, entry.FirstFrame, value => entry.FirstFrame = value, 40f);
                    bool removeRow = GUILayout.Button("✕", GUILayout.Width(22f));
                    GUILayout.EndHorizontal();
                    if (removeRow)
                    {
                        Undo.RecordObject(library, "Xoá state");
                        set.OneShots.RemoveAt(row);
                        EditorUtility.SetDirty(library);
                        break;
                    }
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("＋ State", GUILayout.Width(90f))) AddStateMenu(library, set);
                GUILayout.Label("Speed  |  FirstFrame (-1 = giữ nguyên)", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();

                GUILayout.Space(2f);
                GUILayout.Label("Gait (blend tree Loop)", EditorStyles.miniBoldLabel);
                for (int row = 0; row < set.Gaits.Count; row++)
                {
                    GaitClipDefinition gait = set.Gaits[row];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Gait", EditorStyles.miniLabel, GUILayout.Width(34f));
                    EditFloatInline(library, gait.Threshold, value => gait.Threshold = value, 38f);
                    DrawClipName(library, set.MovementFolder, gait.Clip, value => gait.Clip = value);
                    bool removeRow = GUILayout.Button("✕", GUILayout.Width(22f));
                    GUILayout.EndHorizontal();
                    if (removeRow)
                    {
                        Undo.RecordObject(library, "Xoá gait");
                        set.Gaits.RemoveAt(row);
                        EditorUtility.SetDirty(library);
                        break;
                    }
                }
                if (GUILayout.Button("＋ Gait", GUILayout.Width(90f)))
                {
                    Undo.RecordObject(library, "Thêm gait");
                    set.Gaits.Add(new GaitClipDefinition { Threshold = set.Gaits.Count + 1f });
                    EditorUtility.SetDirty(library);
                }

                DrawDirectionalList(library, set.Strafe, "Strafe 8 hướng (Move_X / Move_Y)", set.MovementFolder);
                DrawDirectionalList(library, set.Crouch, "Crouch 8 hướng", set.MovementFolder);
            }

            GUILayout.EndVertical();
        }

        if (GUILayout.Button("＋ Thêm bộ anim", GUILayout.Height(24f)))
        {
            Undo.RecordObject(library, "Thêm bộ anim");
            library.Sets.Add(new AnimSetDefinition());
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawDirectionalList(AnimSetLibrary library, List<DirectionalClipDefinition> rows, string title, string folder)
    {
        GUILayout.Space(2f);
        GUILayout.Label(title, EditorStyles.miniBoldLabel);
        for (int row = 0; row < rows.Count; row++)
        {
            DirectionalClipDefinition child = rows[row];
            GUILayout.BeginHorizontal();
            GUILayout.Label("X", EditorStyles.miniLabel, GUILayout.Width(12f));
            EditFloatInline(library, child.X, value => child.X = value, 38f);
            GUILayout.Label("Y", EditorStyles.miniLabel, GUILayout.Width(12f));
            EditFloatInline(library, child.Y, value => child.Y = value, 38f);
            DrawClipName(library, folder, child.Clip, value => child.Clip = value);
            bool removeRow = GUILayout.Button("✕", GUILayout.Width(22f));
            GUILayout.EndHorizontal();
            if (removeRow)
            {
                Undo.RecordObject(library, "Xoá hướng");
                rows.RemoveAt(row);
                EditorUtility.SetDirty(library);
                break;
            }
        }
        if (GUILayout.Button("＋ Hướng", GUILayout.Width(90f)))
        {
            Undo.RecordObject(library, "Thêm hướng");
            rows.Add(new DirectionalClipDefinition());
            EditorUtility.SetDirty(library);
        }
    }

    private void AddStateMenu(AnimSetLibrary library, AnimSetDefinition set)
    {
        GenericMenu menu = new GenericMenu();
        foreach (LocoState state in System.Enum.GetValues(typeof(LocoState)))
        {
            LocoState captured = state;
            if (set.OneShots.Exists(row => row.State == captured)) menu.AddDisabledItem(new GUIContent(captured.ToString()));
            else
            {
                menu.AddItem(new GUIContent(captured.ToString()), false, () =>
                {
                    Undo.RecordObject(library, "Thêm state");
                    set.OneShots.Add(new LocoClipDefinition { State = captured });
                    EditorUtility.SetDirty(library);
                    Repaint();
                });
            }
        }
        menu.ShowAsContext();
    }

    private void DrawWeapons(WeaponLibrary library)
    {
        if (library.Weapons.Count == 0) EditorGUILayout.HelpBox("Asset chưa có vũ khí nào.", MessageType.Warning);

        for (int index = 0; index < library.Weapons.Count; index++)
        {
            WeaponDefinition weapon = library.Weapons[index];
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            bool expanded = _expandedWeapons.Contains(index);
            bool newExpanded = EditorGUILayout.Foldout(expanded, $"{weapon.DisplayName}   ({weapon.Key})   — {weapon.Chains.Count} chuỗi", true);
            if (newExpanded != expanded)
            {
                if (newExpanded) _expandedWeapons.Add(index);
                else _expandedWeapons.Remove(index);
            }
            bool remove = GUILayout.Button("Xoá", GUILayout.Width(48f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                Undo.RecordObject(library, "Xoá vũ khí");
                library.Weapons.RemoveAt(index);
                EditorUtility.SetDirty(library);
                _expandedWeapons.Clear();
                GUILayout.EndVertical();
                break;
            }

            if (_expandedWeapons.Contains(index))
            {
                EditText(library, "Sửa Key", "Key (tiền tố state)", weapon.Key, value => weapon.Key = value);
                EditText(library, "Sửa tên", "Tên hiển thị", weapon.DisplayName, value => weapon.DisplayName = value);
                EditEnum(library, "Sửa loại", "Loại", weapon.Type, value => weapon.Type = value);
                GUILayout.BeginHorizontal();
                EditText(library, "Sửa thư mục anim", "Thư mục anim", weapon.AnimFolder, value => { weapon.AnimFolder = value; _clipCache.Clear(); });
                if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    PickFolder(weapon.AnimFolder, value => { weapon.AnimFolder = value; _clipCache.Clear(); EditorUtility.SetDirty(library); });
                }
                GUILayout.EndHorizontal();
                EditObject(library, "Sửa prefab gốc", "Prefab gốc (đo mũi kiếm)", weapon.Prefab, typeof(GameObject), value => weapon.Prefab = value as GameObject);
                EditObject(library, "Sửa model", "Model trong game", weapon.Model, typeof(GameObject), value => weapon.Model = value as GameObject);
                EditText(library, "Sửa idle", "Clip idle", weapon.IdleClip, value => weapon.IdleClip = value);
                EditFloat(library, "Sửa reset", "ComboResetTime", weapon.ComboResetTime, value => weapon.ComboResetTime = value);
                EditInt(library, "Sửa AnimSetId", "AnimSetId (bộ anim dùng)", weapon.AnimSetId, value => weapon.AnimSetId = value);

                GUILayout.Space(4f);
                for (int chainIndex = 0; chainIndex < weapon.Chains.Count; chainIndex++)
                {
                    WeaponChainDefinition chain = weapon.Chains[chainIndex];
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Chuỗi", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
                    EditText(library, "Sửa tên chuỗi", string.Empty, chain.Name, value => chain.Name = value);
                    EditEnum(library, "Sửa ngữ cảnh", string.Empty, chain.Context, value => chain.Context = value);
                    bool removeChain = GUILayout.Button("✕", GUILayout.Width(22f));
                    GUILayout.EndHorizontal();

                    if (!removeChain)
                    {
                        for (int stepIndex = 0; stepIndex < chain.Steps.Count; stepIndex++)
                        {
                            WeaponStepDefinition step = chain.Steps[stepIndex];
                            GUILayout.BeginHorizontal();
                            GUILayout.Label((stepIndex + 1) + ".", GUILayout.Width(22f));
                            DrawClipName(library, weapon.AnimFolder, step.Clip, value => step.Clip = value);
                            DrawClipName(library, weapon.AnimFolder, step.Root, value => step.Root = value);
                            EditToggle(library, step.Vertical, "Y", value => step.Vertical = value, 34f);
                            bool removeStep = GUILayout.Button("✕", GUILayout.Width(22f));
                            GUILayout.EndHorizontal();
                            if (removeStep)
                            {
                                Undo.RecordObject(library, "Xoá bước");
                                chain.Steps.RemoveAt(stepIndex);
                                EditorUtility.SetDirty(library);
                                break;
                            }
                        }
                        if (GUILayout.Button("＋ Bước", GUILayout.Width(80f)))
                        {
                            Undo.RecordObject(library, "Thêm bước");
                            chain.Steps.Add(new WeaponStepDefinition());
                            EditorUtility.SetDirty(library);
                        }
                    }

                    GUILayout.EndVertical();
                    if (removeChain)
                    {
                        Undo.RecordObject(library, "Xoá chuỗi");
                        weapon.Chains.RemoveAt(chainIndex);
                        EditorUtility.SetDirty(library);
                        break;
                    }
                }

                if (GUILayout.Button("＋ Chuỗi", GUILayout.Width(90f)))
                {
                    Undo.RecordObject(library, "Thêm chuỗi");
                    weapon.Chains.Add(new WeaponChainDefinition { Name = "Chuỗi mới" });
                    EditorUtility.SetDirty(library);
                }
                GUILayout.Label("Cột clip thứ 2 = bản root motion (để trống nếu là chính clip). Y = dùng root motion dọc.", EditorStyles.miniLabel);
            }

            GUILayout.EndVertical();
        }

        if (GUILayout.Button("＋ Thêm vũ khí", GUILayout.Height(24f)))
        {
            Undo.RecordObject(library, "Thêm vũ khí");
            library.Weapons.Add(new WeaponDefinition());
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawClipName(UnityEngine.Object target, string folder, string clip, System.Action<string> setter)
    {
        string newClip = EditorGUILayout.TextField(clip, GUILayout.MinWidth(120f));
        if (newClip != clip)
        {
            Undo.RecordObject(target, "Sửa tên clip");
            setter(newClip);
            EditorUtility.SetDirty(target);
        }

        if (!GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(20f))) return;
        List<string> names = ClipNames(folder);
        GenericMenu menu = new GenericMenu();
        if (names.Count == 0) menu.AddDisabledItem(new GUIContent("(thư mục chưa có clip)"));
        foreach (string name in names)
        {
            string captured = name;
            menu.AddItem(new GUIContent(captured), captured == clip, () =>
            {
                Undo.RecordObject(target, "Chọn clip");
                setter(captured);
                EditorUtility.SetDirty(target);
                Repaint();
            });
        }
        menu.ShowAsContext();
    }

    private List<string> ClipNames(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return new List<string>();
        if (_clipCache.TryGetValue(folder, out List<string> cached)) return cached;

        List<string> names = new List<string>();
        string clean = folder.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(clean))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { clean }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip animation && !animation.name.StartsWith("__preview__") && !names.Contains(animation.name))
                        names.Add(animation.name);
                }
            }
        }
        names.Sort();
        _clipCache[folder] = names;
        return names;
    }

    private static void PickFolder(string current, System.Action<string> setter)
    {
        string start = string.IsNullOrEmpty(current) ? "Assets" : current;
        string folder = EditorUtility.OpenFolderPanel("Chọn thư mục", start, string.Empty);
        if (string.IsNullOrEmpty(folder)) return;
        folder = folder.Replace('\\', '/');
        int index = folder.IndexOf("/Assets/", System.StringComparison.Ordinal);
        if (index >= 0) setter(folder.Substring(index + 1));
    }

    // ------------------------------------------------------------------
    // Sửa giá trị có Undo
    // ------------------------------------------------------------------

    private static void EditText(UnityEngine.Object target, string undo, string label, string value, System.Action<string> setter)
    {
        string newValue = EditorGUILayout.TextField(label, value);
        if (newValue == value) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditFloat(UnityEngine.Object target, string undo, string label, float value, System.Action<float> setter)
    {
        float newValue = EditorGUILayout.FloatField(label, value);
        if (Mathf.Approximately(newValue, value)) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditInt(UnityEngine.Object target, string undo, string label, int value, System.Action<int> setter)
    {
        int newValue = EditorGUILayout.IntField(label, value);
        if (newValue == value) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditFloatInline(UnityEngine.Object target, float value, System.Action<float> setter, float width)
    {
        float newValue = EditorGUILayout.FloatField(value, GUILayout.Width(width));
        if (Mathf.Approximately(newValue, value)) return;
        Undo.RecordObject(target, "Sửa số");
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditIntInline(UnityEngine.Object target, int value, System.Action<int> setter, float width)
    {
        int newValue = EditorGUILayout.IntField(value, GUILayout.Width(width));
        if (newValue == value) return;
        Undo.RecordObject(target, "Sửa số");
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditToggle(UnityEngine.Object target, bool value, string label, System.Action<bool> setter, float width)
    {
        bool newValue = GUILayout.Toggle(value, label, GUILayout.Width(width));
        if (newValue == value) return;
        Undo.RecordObject(target, "Sửa cờ");
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditEnum<T>(UnityEngine.Object target, string undo, string label, T value, System.Action<T> setter) where T : System.Enum
    {
        T newValue = (T)EditorGUILayout.EnumPopup(label, value);
        if (Equals(newValue, value)) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    private static void EditObject(UnityEngine.Object target, string undo, string label, UnityEngine.Object value, System.Type type, System.Action<UnityEngine.Object> setter)
    {
        UnityEngine.Object newValue = EditorGUILayout.ObjectField(label, value, type, false);
        if (newValue == value) return;
        Undo.RecordObject(target, undo);
        setter(newValue);
        EditorUtility.SetDirty(target);
    }

    // ------------------------------------------------------------------
    // Tab 4: build + kiểm tra
    // ------------------------------------------------------------------

    private void DrawBuildTab()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation nhân vật (Locomotion)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Dựng lại layer Locomotion từ bộ anim Grruzam: chuẩn hoá import settings, tạo state cho từng LocoState + 3 blend tree (chạy, strafe 8 hướng, crouch), " +
                "rồi gắn AnimatorRootMotion + PoseBlend vào Character.prefab.",
                MessageType.None);
            if (GUILayout.Button("Build Locomotion Animator", GUILayout.Height(26f))) Run(LocomotionAnimatorBuilder.Build);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vũ khí (Combo)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Phân tích lại toàn bộ clip: mốc chém, mốc đá, mốc đập đất, root motion, cách cầm kiếm — rồi dựng layer WeaponAttack.\n" +
                "Lưu ý: menu này GHI ĐÈ HitTimes/KickTimes/SlamTime đã kéo tay. Chỉ dùng Bake nhẹ nếu muốn giữ mốc tay.",
                MessageType.None);
            if (GUILayout.Button("Build Weapon Combos (phân tích lại từ đầu)", GUILayout.Height(26f))) Run(WeaponComboBuilder.BuildAll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake nhẹ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Chỉ tính lại SlamTime + KickTimes, không đụng Animator và không ghi đè mốc chém đã kéo tay.", MessageType.None);
            if (GUILayout.Button("Bake Slam And Kick Times", GUILayout.Height(24f))) Run(WeaponComboBuilder.BakeSlamAndKickTimes);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kiểm tra dữ liệu", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kiểm tra toàn bộ", GUILayout.Height(24f), GUILayout.Width(160f))) ValidateAll();
            if (GUILayout.Button("Mở khoá preview", GUILayout.Width(120f))) StopPreview();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            foreach (string line in _validation)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void Run(System.Action action)
    {
        try
        {
            action();
            _status = "Xong: " + action.Method.Name;
        }
        catch (System.Exception exception)
        {
            _status = "Lỗi: " + exception.Message;
            Debug.LogException(exception);
        }
    }

    private void ValidateAll()
    {
        _validation.Clear();

        // state tấn công trong controller riêng của nhân vật sở hữu vũ khí
        Dictionary<AnimatorController, HashSet<string>> statesByController = new Dictionary<AnimatorController, HashSet<string>>();
        HashSet<string> AttackStates(WeaponData weapon)
        {
            HashSet<string> result = new HashSet<string>();
            AnimatorController controller = LocomotionAnimatorBuilder.ControllerOf(weapon.AnimSetId);
            if (controller == null)
            {
                _validation.Add($"✖ {weapon.DisplayName}: bộ anim {weapon.AnimSetId} chưa có Controller -> chạy Build Locomotion Animator");
                return result;
            }
            if (statesByController.TryGetValue(controller, out HashSet<string> cached)) return cached;
            statesByController[controller] = result;

            AnimatorControllerLayer attackLayer = controller.layers.FirstOrDefault(layer => layer.name == WeaponComboBuilder.LayerName);
            if (attackLayer == null) _validation.Add($"✖ {controller.name} thiếu layer '{WeaponComboBuilder.LayerName}' -> chạy Build Weapon Combos");
            else if (attackLayer.stateMachine != null)
            {
                foreach (ChildAnimatorState state in attackLayer.stateMachine.states) result.Add(state.state.name);
            }
            if (!controller.layers.Any(layer => layer.name == LocomotionAnimatorBuilder.LayerName))
                _validation.Add($"✖ {controller.name} thiếu layer '{LocomotionAnimatorBuilder.LayerName}' -> chạy Build Locomotion Animator");
            return result;
        }

        AnimSetLibrary animLibrary = LocomotionAnimatorBuilder.FindLibrary();
        if (animLibrary != null && animLibrary.Sets.Count != LocoSetPrefixes.Values.Length)
            _validation.Add($"⚠ AnimSetLibrary có {animLibrary.Sets.Count} bộ nhưng runtime đang có {LocoSetPrefixes.Values.Length} prefix -> bấm 'Sinh prefix' rồi build lại");
        if (animLibrary != null && animLibrary.Sets.Count > LocoSetPrefixes.Values.Length)
            _validation.Add("✖ Thêm bộ anim mới thì bắt buộc Sinh prefix, không thì CharacterAnim.SetWeapon báo AnimSetId không hợp lệ");

        float gap = _windowBefore + _windowAfter;
        foreach (WeaponData weapon in _weapons)
        {
            HashSet<string> states = AttackStates(weapon);
            if (weapon.Chains.Count == 0)
            {
                _validation.Add($"✖ {weapon.DisplayName}: chưa có chuỗi combo nào");
                continue;
            }
            foreach (ComboChain chain in weapon.Chains)
            {
                if (chain.Steps.Count == 0)
                {
                    _validation.Add($"✖ {weapon.DisplayName} / {chain.Name}: chuỗi rỗng");
                    continue;
                }
                foreach (AttackStep step in chain.Steps)
                {
                    string where = $"{weapon.DisplayName} / {chain.Name} / {step.Name}";
                    if (step.Clip == null) _validation.Add($"✖ {where}: thiếu Clip");
                    if (string.IsNullOrEmpty(step.StateName)) _validation.Add($"✖ {where}: thiếu StateName");
                    else if (states.Count > 0 && !states.Contains(step.StateName)) _validation.Add($"✖ {where}: state '{step.StateName}' chưa có trong Animator");

                    if (step.HitTimes.Count == 0) _validation.Add($"⚠ {where}: không có mốc chém");
                    else
                    {
                        for (int i = 1; i < step.HitTimes.Count; i++)
                        {
                            if (step.HitTimes[i] >= step.HitTimes[i - 1]) continue;
                            _validation.Add($"✖ {where}: HitTimes chưa sắp xếp");
                            break;
                        }
                        for (int i = 1; i < step.HitTimes.Count; i++)
                        {
                            if (step.Clip == null) break;
                            float distance = (step.HitTimes[i] - step.HitTimes[i - 1]) * step.Clip.length;
                            if (distance >= gap * 0.95f) continue;
                            _validation.Add($"⚠ {where}: 2 mốc chém cách {distance:F3}s < cửa sổ quét {gap:F2}s");
                            break;
                        }
                    }

                    foreach (float time in step.HitTimes)
                    {
                        if (time < 0f || time > 1f)
                        {
                            _validation.Add($"✖ {where}: HitTimes ngoài 0..1");
                            break;
                        }
                    }
                    foreach (float time in step.KickTimes)
                    {
                        if (time < 0f || time > 1f)
                        {
                            _validation.Add($"✖ {where}: KickTimes ngoài 0..1");
                            break;
                        }
                    }
                }
            }
        }

        if (_validation.Count == 0) _validation.Add("✔ Không thấy vấn đề gì.");
        _status = "Đã kiểm tra " + _weapons.Count + " vũ khí";
    }

    // ------------------------------------------------------------------
    // Preview + âm thanh
    // ------------------------------------------------------------------

    private void TogglePlay()
    {
        if (_playing)
        {
            _playing = false;
            return;
        }
        if (Step == null || Step.Clip == null)
        {
            _status = "Đòn này chưa có Clip";
            return;
        }
        EnsurePreview();
        _playing = true;
        _lastTick = EditorApplication.timeSinceStartup;
        SampleNow();
    }

    private void Tick()
    {
        if (!_playing) return;

        AttackStep step = Step;
        if (step == null || step.Clip == null)
        {
            _playing = false;
            Repaint();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float delta = Mathf.Clamp((float)(now - _lastTick), 0f, 0.1f);
        _lastTick = now;

        float length = Mathf.Max(step.Clip.length, 0.001f);
        float previous = _time;
        _time += delta * Mathf.Max(step.Speed, 0.01f);
        if (_time >= length)
        {
            if (_loop)
            {
                _time = 0f;
                previous = -1f; // vòng lại: coi như vừa đi qua mốc ở đầu clip
            }
            else
            {
                _time = length;
                _playing = false;
            }
        }

        PlayCrossedSounds(step, previous, _time);
        SampleNow();
        Repaint();
    }

    private void PlayCrossedSounds(AttackStep step, float from, float to)
    {
        if (step == null) return;
        if (_fx == null) _fx = FindFx();
        if (_fx == null) return;

        // kéo ngược (từ phải sang trái) cũng phải kêu
        if (from > to)
        {
            float swap = from;
            from = to;
            to = swap;
        }

        float length = Mathf.Max(step.Clip != null ? step.Clip.length : 1f, 0.001f);
        foreach (float hit in step.HitTimes)
        {
            float time = hit * length;
            if (time > from && time <= to) PlayRandom(step.Weight >= _fx.HeavyThreshold ? _fx.HeavySwingClips : _fx.SwingClips);
        }
        foreach (float kick in step.KickTimes)
        {
            float time = kick * length;
            if (time > from && time <= to) PlayRandom(_fx.KickSwingClips);
        }
        if (step.SlamTime >= 0f)
        {
            float time = step.SlamTime * length;
            if (time > from && time <= to) PlayRandom(_fx.SlamClips);
        }
    }

    /// <summary>Click lên timeline: nếu điểm vừa nhảy tới nằm trong cửa sổ của mốc nào thì phát tiếng mốc đó.</summary>
    private void PlaySoundNear(AttackStep step, float time)
    {
        if (step == null) return;
        if (_fx == null) _fx = FindFx();
        if (_fx == null) return;

        float length = Mathf.Max(Length, 0.001f);
        foreach (float hit in step.HitTimes)
        {
            if (Mathf.Abs(time - hit * length) > _windowBefore) continue;
            PlayRandom(step.Weight >= _fx.HeavyThreshold ? _fx.HeavySwingClips : _fx.SwingClips);
            return;
        }
        foreach (float kick in step.KickTimes)
        {
            if (Mathf.Abs(time - kick * length) > 0.1f) continue;
            PlayRandom(_fx.KickSwingClips);
            return;
        }
        if (step.SlamTime >= 0f && Mathf.Abs(time - step.SlamTime * length) <= 0.1f) PlayRandom(_fx.SlamClips);
    }

    private void PlayMarkerSound(AttackStep step, MarkerKind kind)
    {
        if (_fx == null) _fx = FindFx();
        if (_fx == null)
        {
            _status = "Chưa có CombatEffects để nghe thử (xem tab Âm thanh)";
            return;
        }

        if (kind == MarkerKind.Hit) PlayRandom(step.Weight >= _fx.HeavyThreshold ? _fx.HeavySwingClips : _fx.SwingClips);
        else if (kind == MarkerKind.Kick) PlayRandom(_fx.KickSwingClips);
        else if (kind == MarkerKind.Slam) PlayRandom(_fx.SlamClips);
    }

    private static void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        PlayClip(clips[Random.Range(0, clips.Length)]);
    }

    private static MethodInfo _playClipMethod;
    private static double _lastPreviewSound;

    /// <summary>
    /// Phát clip ngoài play mode bằng AudioUtil của Editor (reflection, vì API này là internal).
    /// Không tìm thấy thì im lặng — mọi thứ khác vẫn chạy.
    /// </summary>
    private static void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        // kéo nhanh có thể vượt nhiều mốc trong 1 frame -> bớt dồn tiếng
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastPreviewSound < 0.04) return;
        _lastPreviewSound = now;

        if (_playClipMethod == null)
        {
            System.Type audioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil")
                                 ?? typeof(AssetDatabase).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil != null)
            {
                foreach (MethodInfo method in audioUtil.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic))
                {
                    if (method.Name != "PlayPreviewClip" && method.Name != "PlayClip") continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 3) continue;
                    if (parameters[0].ParameterType != typeof(AudioClip)) continue;
                    if (parameters[1].ParameterType != typeof(int)) continue;
                    if (parameters[2].ParameterType != typeof(bool)) continue;
                    _playClipMethod = method;
                    break;
                }
            }
        }

        if (_playClipMethod == null) return;
        try
        {
            _playClipMethod.Invoke(null, new object[] { clip, 0, false });
        }
        catch
        {
            _playClipMethod = null;
        }
    }

    private void SampleNow()
    {
        AttackStep step = Step;
        if (step == null || step.Clip == null) return;

        if (_previewMode == 0)
        {
            GameObject character = EnsureWindowCharacter();
            GameObject poseSource = EnsurePoseSource();
            if (character == null || poseSource == null) return;
            SampleClip(poseSource, step.Clip, _time);
            CopyPose(poseSource, character);
            Repaint();
            return;
        }

        EnsurePreview();
        if (_preview == null) return;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _startedAnimationMode = true;
        }
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_preview.gameObject, step.Clip, Mathf.Clamp(_time, 0f, step.Clip.length));
        AnimationMode.EndSampling();

        // lần đầu phải lia Scene view tới nhân vật: preview vẽ ở đó, nằm ngoài khung hình thì coi như "không có gì"
        if (_needsFrame)
        {
            _needsFrame = false;
            FramePreview();
        }
        SceneView.RepaintAll();
    }

    private void EnsurePreview()
    {
        if (_preview != null) return;

        // player trong scene có thể đang inactive -> vẫn phải tìm ra
        CharacterCombo combo = UnityEngine.Object.FindAnyObjectByType<CharacterCombo>(FindObjectsInactive.Include);
        if (combo != null && combo.animator != null)
        {
            _preview = combo.animator;
            _previewSource = "nhân vật trong scene: " + combo.name;
            _needsFrame = true;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (prefab == null)
        {
            _status = "Không tìm thấy nhân vật: mở scene có CharacterCombo hoặc kiểm tra " + CharacterPrefabPath;
            return;
        }

        _spawnedPreview = (GameObject)UnityEngine.Object.Instantiate(prefab);
        _spawnedPreview.hideFlags = HideFlags.HideAndDontSave;
        // đặt ngay trước Scene view hiện tại để không bị nằm ngoài khung hình
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) _spawnedPreview.transform.position = view.pivot + view.rotation * Vector3.forward * 3f;

        _preview = _spawnedPreview.GetComponentInChildren<Animator>();
        _previewSource = "bản sao Character.prefab (scene không có CharacterCombo)";
        _needsFrame = true;
        _status = "Đã tạo nhân vật preview bằng bản sao " + CharacterPrefabPath;
    }

    private void FramePreview()
    {
        if (_preview == null) return;
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            _status = "Không có cửa sổ Scene đang mở — mở Scene view để thấy preview";
            return;
        }
        view.Frame(new Bounds(_preview.transform.position + Vector3.up * 1.1f, new Vector3(2.6f, 2.6f, 2.6f)), false);
        view.Repaint();
    }

    private void DrawWindowViewport(AttackStep step)
    {
        if (_windowPreviewBroken && GUILayout.Button(new GUIContent("Thử lại preview trong cửa sổ", "Xoá renderer cũ rồi tạo lại"), GUILayout.Width(210f)))
        {
            _windowPreviewBroken = false;
            CleanupRenderer();
        }

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        HandleViewportInput(rect);
        if (Event.current.type == EventType.Repaint) RenderWindowViewport(rect, step);
    }

    private void HandleViewportInput(Rect rect)
    {
        Event current = Event.current;
        if (!rect.Contains(current.mousePosition)) return;

        if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 1))
        {
            // mặc định giống Scene view của Unity; Y dùng cùng quy ước dấu với X
            float x = _invertOrbitX ? -1f : 1f;
            float y = _invertOrbitY ? -1f : 1f;
            _previewYaw += x * current.delta.x * 0.5f;
            _previewPitch = Mathf.Clamp(_previewPitch + y * current.delta.y * 0.4f, -25f, 70f);
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.ScrollWheel)
        {
            _previewZoom = Mathf.Clamp(_previewZoom * (1f + current.delta.y * 0.02f), 0.55f, 2.4f);
            current.Use();
            Repaint();
        }
    }

    private void RenderWindowViewport(Rect rect, AttackStep step)
    {
        if (_windowPreviewBroken) return;

        try
        {
            GameObject character = EnsureWindowCharacter();
            if (character == null) return;

            if (step != null && step.Clip != null)
            {
                GameObject poseSource = EnsurePoseSource();
                if (poseSource != null)
                {
                    // sample trên object trong scene (đường chắc chắn chạy), rồi copy pose sang nhân vật preview
                    SampleClip(poseSource, step.Clip, _time);
                    CopyPose(poseSource, character);
                }
            }

            UpdatePreviewWeapon(character, step);
            UpdatePreviewDummy(character, step);

            _renderer.BeginPreview(rect, GUIStyle.none);
            FrameWindowCamera(character);
            _renderer.camera.Render();
            Texture texture = _renderer.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
        catch (System.Exception exception)
        {
            _windowPreviewBroken = true;
            _status = "Preview trong cửa sổ lỗi: " + exception.Message + " — chuyển sang tab 'Scene view'";
            CleanupRenderer();
        }
    }

    private void EnsureRenderer()
    {
        if (_renderer != null) return;

        _renderer = new PreviewRenderUtility();
        Camera camera = _renderer.camera;
        camera.cameraType = CameraType.Preview;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        camera.fieldOfView = 32f;
        TryAddHdrpCameraData(camera);
        TrySetHdrpClearColor(camera, BackgroundColor);

        if (_renderer.lights != null && _renderer.lights.Length > 0 && _renderer.lights[0] != null)
        {
            _renderer.lights[0].color = new Color(1f, 0.97f, 0.92f);
            _renderer.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        }
        if (_renderer.lights != null && _renderer.lights.Length > 1 && _renderer.lights[1] != null)
        {
            _renderer.lights[1].color = new Color(0.72f, 0.80f, 1f);
            _renderer.lights[1].transform.rotation = Quaternion.Euler(12f, 150f, 0f);
        }

        EnsureEnvironment();
    }

    private static readonly Color BackgroundColor = new Color(0.075f, 0.08f, 0.095f, 1f);

    /// <summary>Sàn có lưới + backdrop tối, cả hai dùng material unlit nên không phụ thuộc ánh sáng/HDRP profile.</summary>
    private void EnsureEnvironment()
    {
        if (_previewFloor != null) return;

        _previewFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _previewFloor.name = "PreviewFloor";
        _previewFloor.hideFlags = HideFlags.HideAndDontSave;
        _previewFloor.transform.localScale = new Vector3(6f, 1f, 6f);
        Renderer floorRenderer = _previewFloor.GetComponent<Renderer>();
        if (floorRenderer != null)
        {
            _previewFloorMaterial = CreateUnlitMaterial(new Color(0.18f, 0.19f, 0.22f), CreateGridTexture(), 30f);
            floorRenderer.sharedMaterial = _previewFloorMaterial;
            floorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floorRenderer.receiveShadows = false;
        }
        _renderer.AddSingleGO(_previewFloor);

        _previewBackdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _previewBackdrop.name = "PreviewBackdrop";
        _previewBackdrop.hideFlags = HideFlags.HideAndDontSave;
        _previewBackdrop.transform.localScale = new Vector3(600f, 400f, 1f);
        Renderer backdropRenderer = _previewBackdrop.GetComponent<Renderer>();
        if (backdropRenderer != null)
        {
            _previewBackdropMaterial = CreateUnlitMaterial(new Color(0.055f, 0.06f, 0.072f), null, 1f);
            backdropRenderer.sharedMaterial = _previewBackdropMaterial;
            backdropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;
        }
        _renderer.AddSingleGO(_previewBackdrop);
    }

    private static Material CreateUnlitMaterial(Color color, Texture texture, float tiling)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (texture != null)
        {
            if (material.HasProperty("_UnlitColorMap"))
            {
                material.SetTexture("_UnlitColorMap", texture);
                material.SetTextureScale("_UnlitColorMap", new Vector2(tiling, tiling));
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
            }
        }
        return material;
    }

    private static Texture2D CreateGridTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
        };
        Color line = new Color(0.42f, 0.45f, 0.52f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, x < 2 || y < 2 ? line : Color.white);
            }
        }
        texture.Apply();
        return texture;
    }

    private GameObject EnsureWindowCharacter()
    {
        if (_windowCharacter != null) return _windowCharacter;

        EnsureRenderer();
        _windowCharacter = CreateCharacterInstance();
        if (_windowCharacter == null)
        {
            _windowPreviewBroken = true;
            _status = "Không thấy nhân vật để preview: mở scene có CharacterCombo hoặc kiểm tra " + CharacterPrefabPath;
            return null;
        }

        _windowCharacter.hideFlags = HideFlags.HideAndDontSave;
        _windowCharacter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
        DisableAuxiliaryComponents(_windowCharacter, false, false);
        try
        {
            _renderer.AddSingleGO(_windowCharacter);
        }
        catch (System.Exception exception)
        {
            _windowPreviewBroken = true;
            _status = "Không thêm được nhân vật vào preview: " + exception.Message;
            UnityEngine.Object.DestroyImmediate(_windowCharacter);
            _windowCharacter = null;
            return null;
        }
        _previewSource = "trong cửa sổ: " + _windowCharacter.name;
        return _windowCharacter;
    }

    /// <summary>
    /// Nhân vật clone từ scene có thể mang theo camera/light/audio listener.
    /// Với nhân vật preview: giữ light (ánh sáng riêng của nhân vật có ích cho preview),
    /// chỉ tắt camera/audio. Với bản lấy pose: tắt hết vì nó nằm trong scene thật.
    /// </summary>
    private static void DisableAuxiliaryComponents(GameObject root, bool disableRenderers, bool disableLights)
    {
        if (root == null) return;
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        if (disableLights)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
        }
        if (!disableRenderers) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
    }

    /// <summary>
    /// Ưu tiên nhân vật thật trong scene (giữ material override / prefab instance),
    /// không có mới lấy Character.prefab. Đây là lý do chính khiến preview ra trắng bệt.
    /// </summary>
    private GameObject CreateCharacterInstance()
    {
        if (_previewPrefab != null)
        {
            Animator prefabAnimator = _previewPrefab.GetComponentInChildren<Animator>(true);
            GameObject prefabSource = prefabAnimator != null ? prefabAnimator.gameObject : _previewPrefab;
            return (GameObject)UnityEngine.Object.Instantiate(prefabSource);
        }

        CharacterCombo combo = UnityEngine.Object.FindAnyObjectByType<CharacterCombo>(FindObjectsInactive.Include);
        Transform root = null;
        if (combo != null) root = combo.animator != null ? combo.animator.transform : combo.transform;

        GameObject source = root != null ? root.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        return source != null ? (GameObject)UnityEngine.Object.Instantiate(source) : null;
    }

    /// <summary>
    /// HDRP cần HDAdditionalCameraData trên camera; thêm bằng reflection để file này
    /// không phụ thuộc cứng vào package HDRP.
    /// </summary>
    private static System.Type FindHdrpCameraDataType()
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            if (type != null) return type;
        }
        return null;
    }

    /// <summary>
    /// HDRP cần HDAdditionalCameraData trên camera; thêm bằng reflection để file này
    /// không phụ thuộc cứng vào package HDRP.
    /// </summary>
    private static void TryAddHdrpCameraData(Camera camera)
    {
        if (camera == null) return;
        try
        {
            System.Type type = FindHdrpCameraDataType();
            if (type == null || camera.GetComponent(type) != null) return;
            camera.gameObject.AddComponent(type);
        }
        catch
        {
            // không có HDRP hoặc type đổi tên -> bỏ qua, preview vẫn thử render bình thường
        }
    }

    /// <summary>
    /// HDRP bỏ qua Camera.clearFlags: màu nền phải set qua clearColorMode/backgroundColorHDR
    /// trên HDAdditionalCameraData, nếu không sẽ ra skybox.
    /// </summary>
    private static void TrySetHdrpClearColor(Camera camera, Color color)
    {
        if (camera == null) return;
        try
        {
            System.Type type = FindHdrpCameraDataType();
            if (type == null) return;
            Component data = camera.GetComponent(type);
            if (data == null) return;

            System.Reflection.FieldInfo clearMode = type.GetField("clearColorMode");
            if (clearMode != null) clearMode.SetValue(data, System.Enum.Parse(clearMode.FieldType, "SolidColor"));

            System.Reflection.FieldInfo background = type.GetField("backgroundColorHDR");
            if (background != null) background.SetValue(data, color);
        }
        catch
        {
            // HDRP đổi field -> giữ skybox, backdrop phía sau vẫn che nền
        }
    }

    /// <summary>
    /// Bản sao ẩn trong scene (renderer tắt, đặt xa): AnimationMode sample lên nó,
    /// rồi copy pose sang nhân vật trong preview scene. Cách này không phụ thuộc việc
    /// AnimationMode có sample được object trong preview scene hay không.
    /// </summary>
    private GameObject EnsurePoseSource()
    {
        if (_poseSource != null) return _poseSource;

        _poseSource = CreateCharacterInstance();
        if (_poseSource == null) return null;

        _poseSource.hideFlags = HideFlags.HideAndDontSave;
        _poseSource.name = "PoseSource";
        _poseSource.transform.SetPositionAndRotation(new Vector3(0f, -1000f, 0f), Quaternion.identity);
        DisableAuxiliaryComponents(_poseSource, true, true);
        _posePairs.Clear();
        return _poseSource;
    }

    private void CopyPose(GameObject source, GameObject target)
    {
        if (_posePairs.Count == 0) BuildPosePairs(source, target);
        for (int index = 0; index < _posePairs.Count; index++)
        {
            Transform from = _posePairs[index].source;
            Transform to = _posePairs[index].target;
            if (from == null || to == null) continue;
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
        }
    }

    private void BuildPosePairs(GameObject source, GameObject target)
    {
        _posePairs.Clear();
        Dictionary<string, Transform> map = new Dictionary<string, Transform>();
        foreach (Transform bone in target.GetComponentsInChildren<Transform>(true))
        {
            string path = PathOf(target.transform, bone);
            if (!map.ContainsKey(path)) map[path] = bone;
        }
        foreach (Transform bone in source.GetComponentsInChildren<Transform>(true))
        {
            if (!map.TryGetValue(PathOf(source.transform, bone), out Transform targetBone)) continue;
            _posePairs.Add((bone, targetBone));
        }
    }

    private static string PathOf(Transform root, Transform node)
    {
        if (node == root) return string.Empty;
        string path = node.name;
        Transform parent = node.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void FrameWindowCamera(GameObject character)
    {
        Bounds bounds = PreviewBounds(character);

        float radius = Mathf.Max(bounds.extents.magnitude, 0.8f);
        float distance = radius / Mathf.Sin(_renderer.camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f * _previewZoom;
        Quaternion orbit = Quaternion.Euler(_previewPitch, _previewYaw, 0f);
        Vector3 position = bounds.center - orbit * Vector3.forward * distance;

        Transform camera = _renderer.camera.transform;
        camera.position = position;
        camera.rotation = Quaternion.LookRotation(bounds.center - position);
        _renderer.camera.nearClipPlane = Mathf.Max(distance * 0.01f, 0.01f);
        _renderer.camera.farClipPlane = Mathf.Max(distance * 6f + radius * 4f, distance + 120f);

        // backdrop luôn nằm sau nhân vật theo hướng camera -> khung luôn có nền tối dù HDRP có phớt lờ clear color
        if (_previewBackdrop != null)
        {
            _previewBackdrop.transform.position = camera.position + camera.forward * (distance + 60f);
            _previewBackdrop.transform.rotation = Quaternion.LookRotation(camera.forward, Vector3.up);
        }

        ApplyPreviewLight();
    }

    private void ApplyPreviewLight()
    {
        if (_renderer == null || _renderer.lights == null) return;
        if (_renderer.lights.Length > 0 && _renderer.lights[0] != null) _renderer.lights[0].intensity = 1.5f * _previewLight;
        if (_renderer.lights.Length > 1 && _renderer.lights[1] != null) _renderer.lights[1].intensity = 0.7f * _previewLight;
    }

    /// <summary>Khung hình gồm nhân vật + bù nhìn, nhưng bỏ vũ khí để camera không giật khi kiếm vung.</summary>
    private Bounds PreviewBounds(GameObject character)
    {
        Bounds bounds = new Bounds(character.transform.position + Vector3.up, new Vector3(0.4f, 0.4f, 0.4f));
        foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null || !renderer.enabled) continue;
            if (_previewWeapon != null && renderer.transform.IsChildOf(_previewWeapon.transform)) continue;
            bounds.Encapsulate(renderer.bounds);
        }
        if (_previewDummy != null)
        {
            foreach (Renderer renderer in _previewDummy.GetComponentsInChildren<Renderer>())
            {
                if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    /// <summary>
    /// Gắn vũ khí thật (WeaponData.WeaponPrefab) vào bàn tay phải và set cách cầm
    /// đúng như WeaponHolder làm lúc chạy: socket dưới RightHand, local = grip của anim.
    /// </summary>
    private void UpdatePreviewWeapon(GameObject character, AttackStep step)
    {
        WeaponData weapon = Weapon;
        bool socketLost = _previewWeaponSocket == null || _previewWeaponSocket.parent == null;

        if (weapon != _previewWeaponData || socketLost)
        {
            if (_previewWeapon != null) UnityEngine.Object.DestroyImmediate(_previewWeapon);
            _previewWeapon = null;
            _previewWeaponSocket = null;
            _previewWeaponData = weapon;

            if (weapon != null && weapon.WeaponPrefab != null && character != null)
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (hand != null)
                {
                    _previewWeaponSocket = new GameObject("PreviewWeaponSocket").transform;
                    _previewWeaponSocket.SetParent(hand, false);
                    _previewWeapon = (GameObject)UnityEngine.Object.Instantiate(weapon.WeaponPrefab, _previewWeaponSocket, false);
                    _previewWeapon.name = weapon.WeaponPrefab.name;
                    foreach (Collider collider in _previewWeapon.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
            }
        }

        if (_previewWeaponSocket == null || weapon == null) return;

        weapon.TrySampleAttackGrip(step, _time, out Vector3 position, out Quaternion rotation);
        _previewWeaponSocket.localPosition = position;
        _previewWeaponSocket.localRotation = rotation;
    }

    /// <summary>Bù nhìn trước mặt để thấy tầm với; sáng lên khi kim nằm trong cửa sổ chém/đá.</summary>
    private void UpdatePreviewDummy(GameObject character, AttackStep step)
    {
        if (_previewDummy == null && character != null)
        {
            _previewDummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _previewDummy.name = "TrainingDummy";
            _previewDummy.hideFlags = HideFlags.HideAndDontSave;
            _previewDummy.transform.localScale = new Vector3(0.6f, 0.85f, 0.6f);
            Collider collider = _previewDummy.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);

            _previewDummyMaterial = CreateUnlitMaterial(DummyColor, null, 1f);
            Renderer dummyRenderer = _previewDummy.GetComponent<Renderer>();
            if (dummyRenderer != null)
            {
                dummyRenderer.sharedMaterial = _previewDummyMaterial;
                dummyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dummyRenderer.receiveShadows = false;
            }
            _renderer.AddSingleGO(_previewDummy);
        }

        if (_previewDummy == null || character == null) return;

        _previewDummy.transform.position = character.transform.position + character.transform.forward * 1.35f + Vector3.up * 0.9f;
        _previewDummy.transform.rotation = Quaternion.LookRotation(-character.transform.forward, Vector3.up);

        bool inWindow = false;
        if (step != null)
        {
            float length = Mathf.Max(Length, 0.001f);
            foreach (float hit in step.HitTimes)
            {
                float time = hit * length;
                if (_time >= time - _windowBefore && _time <= time + _windowAfter) { inWindow = true; break; }
            }
            if (!inWindow)
            {
                foreach (float kick in step.KickTimes)
                {
                    float time = kick * length;
                    if (Mathf.Abs(_time - time) <= 0.12f) { inWindow = true; break; }
                }
            }
            if (!inWindow && step.SlamTime >= 0f && Mathf.Abs(_time - step.SlamTime * length) <= 0.1f) inWindow = true;
        }

        Color color = inWindow ? new Color(0.95f, 0.38f, 0.28f) : DummyColor;
        if (_previewDummyMaterial == null) return;
        if (_previewDummyMaterial.HasProperty("_UnlitColor")) _previewDummyMaterial.SetColor("_UnlitColor", color);
        if (_previewDummyMaterial.HasProperty("_Color")) _previewDummyMaterial.SetColor("_Color", color);
    }

    private static readonly Color DummyColor = new Color(0.52f, 0.40f, 0.29f);

    private void SampleClip(GameObject target, AnimationClip clip, float time)
    {
        if (target == null || clip == null) return;
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _startedAnimationMode = true;
        }
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(target, clip, Mathf.Clamp(time, 0f, clip.length));
        AnimationMode.EndSampling();
    }

    private void CleanupRenderer()
    {
        if (_renderer != null)
        {
            try { _renderer.Cleanup(); }
            catch { /* Editor đang đóng / domain reload */ }
            _renderer = null;
        }
        _windowCharacter = null;
        if (_poseSource != null) UnityEngine.Object.DestroyImmediate(_poseSource);
        _poseSource = null;
        _posePairs.Clear();
        if (_previewFloor != null) UnityEngine.Object.DestroyImmediate(_previewFloor);
        _previewFloor = null;
        if (_previewBackdrop != null) UnityEngine.Object.DestroyImmediate(_previewBackdrop);
        _previewBackdrop = null;
        if (_previewFloorMaterial != null) UnityEngine.Object.DestroyImmediate(_previewFloorMaterial);
        _previewFloorMaterial = null;
        if (_previewBackdropMaterial != null) UnityEngine.Object.DestroyImmediate(_previewBackdropMaterial);
        _previewBackdropMaterial = null;
        if (_previewWeapon != null) UnityEngine.Object.DestroyImmediate(_previewWeapon);
        _previewWeapon = null;
        _previewWeaponSocket = null;
        _previewWeaponData = null;
        if (_previewDummyMaterial != null) UnityEngine.Object.DestroyImmediate(_previewDummyMaterial);
        _previewDummyMaterial = null;
        _previewDummy = null;
    }

    /// <summary>Chế độ preview cũ: nhân vật sống trong Scene, phải lia camera Scene tới.</summary>
    private void DrawScenePreviewRow()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Nhìn vào nhân vật", GUILayout.Width(130f)))
        {
            EnsurePreview();
            FramePreview();
        }
        GUILayout.Label(string.IsNullOrEmpty(_previewSource) ? "chưa có nhân vật preview" : _previewSource, EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        if (_preview != null) return;

        EditorGUILayout.HelpBox(
            "Preview vẽ vào cửa sổ Scene. Bấm ▶ để phát clip, Space để phát/dừng.\n" +
            "Nếu Scene view đang nhìn chỗ khác: bấm 'Nhìn vào nhân vật'. Nếu scene không có nhân vật, tool tự đặt 1 bản sao Character.prefab ngay trước camera Scene.",
            MessageType.Info);
        bool hasCombo = UnityEngine.Object.FindAnyObjectByType<CharacterCombo>(FindObjectsInactive.Include) != null;
        GUILayout.Label(
            $"chẩn đoán — scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}   |   CharacterCombo: {(hasCombo ? "có" : "không")}" +
            $"   |   AnimationMode: {(AnimationMode.InAnimationMode() ? "on" : "off")}   |   clip: {(Step != null && Step.Clip != null ? Step.Clip.name : "(chưa chọn)")}",
            EditorStyles.miniLabel);
        if (GUILayout.Button("Mở scene Assets/Sence/SetUp.unity", GUILayout.Width(260f))) OpenSetupScene();
    }

    private void OpenSetupScene()
    {
        const string setupPath = "Assets/Sence/SetUp.unity";
        if (!System.IO.File.Exists(setupPath))
        {
            _status = "Không thấy " + setupPath;
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(setupPath, OpenSceneMode.Single);
        _preview = null;
        _previewSource = null;
        _spawnedPreview = null; // scene cũ đã đóng nên object này bị huỷ theo
        _needsFrame = true;
        _status = "Đã mở " + setupPath + " — bấm 'Nhìn vào nhân vật' hoặc ▶";
    }

    private void StopPreview()
    {
        _playing = false;
        if (_startedAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        _startedAnimationMode = false;
        _status = "Đã dừng preview (nhân vật trở về pose gốc)";
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------
    // Tiện ích dữ liệu
    // ------------------------------------------------------------------

    private void RefreshWeapons()
    {
        _weapons.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && (_animSetFilter < 0 || data.AnimSetId == _animSetFilter)) _weapons.Add(data);
        }
        _weapons.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        _weaponIndex = Mathf.Clamp(_weaponIndex, 0, Mathf.Max(_weapons.Count - 1, 0));
        ClampSelection();
    }

    private void ClampSelection()
    {
        int chainCount = Weapon != null ? Weapon.Chains.Count : 0;
        _chainIndex = Mathf.Clamp(_chainIndex, 0, Mathf.Max(chainCount - 1, 0));
        ComboChain chain = Weapon != null && chainCount > 0 ? Weapon.Chains[_chainIndex] : null;
        int stepCount = chain != null ? chain.Steps.Count : 0;
        _stepIndex = Mathf.Clamp(_stepIndex, 0, Mathf.Max(stepCount - 1, 0));
    }

    private void ReadDetectorWindows()
    {
        MeleeHitDetector detector = UnityEngine.Object.FindAnyObjectByType<MeleeHitDetector>();
        if (detector == null) return;
        _windowBefore = detector.WindowBefore;
        _windowAfter = detector.WindowAfter;
    }

    private static CombatEffects FindFx()
    {
        return UnityEngine.Object.FindAnyObjectByType<CombatEffects>();
    }
}
