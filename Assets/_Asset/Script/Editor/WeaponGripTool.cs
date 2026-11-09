using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ căn vũ khí trong lòng bàn tay:
/// - Kéo chuột trong khung preview để xoay góc nhìn, lăn chuột để zoom.
/// - Dời / xoay / scale model (tay phải hoặc tay trái) và thấy ngay trên nhân vật.
/// - Kéo thanh anim để nhân vật đứng đúng pose cần căn (pose tra kiếm, pose chém...).
/// - "Bake vào WeaponData" ghi thẳng offset + tỉ lệ vào asset WeaponData.
/// </summary>
public class WeaponGripTool : EditorWindow
{
    private const string DefaultClipFolder = "Assets/_Animation_Vergil/Animation/Humanoid";
    private static readonly Color BackgroundColor = new Color(0.085f, 0.09f, 0.105f, 1f);

    // ---- đối tượng đang căn
    [SerializeField] private GameObject _character;
    [SerializeField] private WeaponData _weapon;
    [SerializeField] private string _clipFolder = DefaultClipFolder;

    // ---- giá trị đang chỉnh (của bên đang chọn)
    private Vector3 _position;
    private Vector3 _euler;
    private float _scale = 1f;
    private bool _offHand;

    // ---- anim
    private readonly List<AnimationClip> _clips = new List<AnimationClip>();
    private string[] _clipNames = new string[0];
    private int _clipIndex = -1;
    private AnimationClip _clip;
    private float _time;
    private bool _playing;
    private bool _loop = true;
    private bool _startedAnimationMode;

    // ---- preview
    private PreviewRenderUtility _renderer;
    private GameObject _previewCharacter;
    private GameObject _poseSource;
    private GameObject _previewSword;
    private GameObject _previewSaya;
    private GameObject _backdrop;
    private Transform _rightHand;
    private Transform _leftHand;
    private readonly List<Transform[]> _posePairs = new List<Transform[]>();
    private bool _broken;
    private bool _followHand = true;
    private float _yaw = 200f;
    private float _pitch = 5f;
    private float _zoom = 1f;
    private Vector3 _pan;
    private string _status = "";
    private string _shownStatus = "";
    private double _lastTick;
    private double _lastRepaint;
    private Rect _lastViewport;

    // giá trị riêng của từng bên: chỉnh kiếm không làm mất tiến trình của vỏ và ngược lại
    private Vector3 _swordPos;
    private Vector3 _swordEuler;
    private float _swordScale = 1f;
    private Vector3 _sayaPos;
    private Vector3 _sayaEuler;
    private float _sayaScale = 1f;

    // số khung hình còn phải vẽ thêm (HDRP cần vài frame để hội tụ exposure)
    private int _warmup;
    private float _lastSampleTime = -1f;
    private AnimationClip _lastSampleClip;

    // hoàn tác (Ctrl+Z) cho các thay đổi trong tool
    private struct GripSnapshot
    {
        public Vector3 SwordPos, SwordEuler; public float SwordScale;
        public Vector3 SayaPos, SayaEuler; public float SayaScale;
        public bool OffHand;
    }
    private readonly List<GripSnapshot> _undoStack = new List<GripSnapshot>();
    private GripSnapshot _pendingSnapshot;
    private double _lastUndoPush;
    private const int UndoCapacity = 40;

    // đèn preview (HDRP dùng đơn vị vật lý nên phải lớn hơn nhiều so với built-in)
    private float _keyIntensity = 30f;
    private float _fillIntensity = 11f;

    // gizmo
    private enum GizmoMode { None = 0, Move = 1, Rotate = 2, Scale = 3 }
    private GizmoMode _gizmo = GizmoMode.Move;
    private bool _localSpace = true;
    private int _dragAxis = -1;
    private Vector2 _dragStartMouse;
    private Vector3 _dragStartPosition;
    private Vector3 _dragStartEuler;
    private float _dragStartScale;

    /// <summary>Đặt đường dẫn để lần Repaint kế tiếp lưu đúng khung preview ra PNG (dùng để kiểm tra).</summary>
    public static string CapturePath;

    // Cửa sổ này giờ là 1 trang của Character Studio (Tools/Character Studio)
    public static void Open() => CharacterStudioWindow.Open(CharacterStudioWindow.Page.Grip);

    #region Nhúng vào Character Studio

    internal EditorWindow Host;

    private new void Repaint()
    {
        if (Host != null) Host.Repaint();
        else base.Repaint();
    }

    internal void DrawEmbedded(Rect rect)
    {
        GUILayout.BeginArea(rect);
        OnGUI();
        GUILayout.EndArea();
    }

    // Studio đổi nhân vật / vũ khí -> nạp lại preview + danh sách clip theo thư mục anim của vũ khí
    internal void SetTargets(GameObject character, WeaponData weapon, string clipFolder)
    {
        if (!string.IsNullOrEmpty(clipFolder) && clipFolder != _clipFolder)
        {
            _clipFolder = clipFolder;
            RefreshClips();
        }
        if (character == _character && weapon == _weapon) return;
        _character = character;
        _weapon = weapon;
        ResetPreviewObjects();
    }

    #endregion

    private void OnEnable()
    {
        if (_weapon == null) _weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Asset/Data/Weapons/WD_Yamato.asset");
        if (_character == null) _character = FindDefaultCharacter();
        RefreshClips();
        LoadValuesFromWeapon();
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        StopAnimationMode();
        CleanupPreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode && change != PlayModeStateChange.EnteredPlayMode) return;
        StopAnimationMode();
        CleanupPreview();
    }

    private static GameObject FindDefaultCharacter()
    {
        GameObject vergil = GameObject.Find("Vergil/Root/Dante/Vergil_Model");
        if (vergil != null && vergil.GetComponent<Animator>() != null) return vergil;

        Animator animator = Object.FindAnyObjectByType<Animator>(FindObjectsInactive.Include);
        return animator != null ? animator.gameObject : null;
    }

    // ------------------------------------------------------------------ GUI

    private void OnGUI()
    {
        // chụp trạng thái trước khi GUI sửa giá trị (để Ctrl+Z lùi đúng 1 bước)
        _pendingSnapshot = CaptureSnapshot();
        HandleUndoShortcut();

        DrawToolbar();
        DrawGizmoToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawViewport();
        DrawSidebar();
        EditorGUILayout.EndHorizontal();

        // _status có thể đổi trong lúc vẽ viewport (pass Repaint) -> chỉ lấy giá trị mới ở pass Layout, không thì lệch layout
        if (Event.current.type == EventType.Layout) _shownStatus = _status;
        if (!string.IsNullOrEmpty(_shownStatus))
            EditorGUILayout.HelpBox(_shownStatus, MessageType.Info);

        if (GUI.changed)
        {
            _warmup = Mathf.Max(_warmup, 6);
            PushSnapshot(_pendingSnapshot);
        }
    }

    private void HandleUndoShortcut()
    {
        Event current = Event.current;
        if (current.type != EventType.KeyDown) return;
        if (current.keyCode != KeyCode.Z || !(current.control || current.command)) return;

        UndoLastEdit();
        current.Use();
    }

    private void DrawGizmoToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _gizmo = (GizmoMode)GUILayout.Toolbar((int)_gizmo, new[] { "Tắt", "Dời", "Xoay", "Scale" }, EditorStyles.toolbarButton, GUILayout.Width(230f));
        _localSpace = GUILayout.Toggle(_localSpace, "Local", EditorStyles.toolbarButton, GUILayout.Width(56f));
        GUILayout.Label("Kéo trục gizmo để chỉnh trực tiếp · chuột trái/phải kéo nền để xoay · chuột giữa kéo để pan · lăn chuột để zoom",
                        EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        _character = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Nhân vật", "GameObject có Animator"), _character, typeof(GameObject), true, GUILayout.MinWidth(200f));
        _weapon = (WeaponData)EditorGUILayout.ObjectField(new GUIContent("WeaponData"), _weapon, typeof(WeaponData), false, GUILayout.MinWidth(170f));
        if (EditorGUI.EndChangeCheck())
        {
            ResetPreviewObjects();
            LoadValuesFromWeapon();
        }

        if (GUILayout.Button("Tìm nhân vật", EditorStyles.toolbarButton, GUILayout.Width(100f)))
        {
            _character = FindDefaultCharacter();
            ResetPreviewObjects();
        }

        GUILayout.FlexibleSpace();
        _followHand = GUILayout.Toggle(_followHand, "Bám tay", EditorStyles.toolbarButton, GUILayout.Width(70f));
        if (GUILayout.Button("Canh giữa", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            _yaw = 200f;
            _pitch = 5f;
            _zoom = 1f;
            _pan = Vector3.zero;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawViewport()
    {
        Rect viewport = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _lastViewport = viewport;

        if (Event.current.type == EventType.Repaint) RenderViewport(viewport);
        HandleInput(viewport);
        if (Event.current.type == EventType.Repaint) DrawGizmo(viewport);
    }

    private void HandleInput(Rect viewport)
    {
        Event current = Event.current;

        // bắt đầu kéo 1 trục gizmo
        if (current.type == EventType.MouseDown && current.button == 0 && _gizmo != GizmoMode.None && viewport.Contains(current.mousePosition))
        {
            int axis = PickAxis(viewport, current.mousePosition);
            if (axis >= 0)
            {
                PushSnapshot(CaptureSnapshot());
                _dragAxis = axis;
                _dragStartMouse = current.mousePosition;
                _dragStartPosition = _position;
                _dragStartEuler = _euler;
                _dragStartScale = _scale;
                current.Use();
                return;
            }
        }

        if (_dragAxis >= 0)
        {
            if (current.type == EventType.MouseDrag)
            {
                DragGizmo(current.mousePosition);
                _warmup = Mathf.Max(_warmup, 4);
                current.Use();
                Repaint();
                return;
            }
            if (current.type == EventType.MouseUp)
            {
                _dragAxis = -1;
                current.Use();
                return;
            }
        }

        HandleOrbitInput(viewport);
    }

    private void HandleOrbitInput(Rect viewport)
    {
        Event current = Event.current;
        if (!viewport.Contains(current.mousePosition)) return;

        // chuột giữa = pan khung nhìn (giống Scene view của Unity)
        if (current.type == EventType.MouseDrag && current.button == 2)
        {
            PanView(current.delta);
            _warmup = Mathf.Max(_warmup, 4);
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 1))
        {
            _yaw += current.delta.x * 0.5f;
            _pitch = Mathf.Clamp(_pitch + current.delta.y * 0.4f, -80f, 80f);
            _warmup = Mathf.Max(_warmup, 4);
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.ScrollWheel)
        {
            _zoom = Mathf.Clamp(_zoom * (1f + current.delta.y * 0.02f), 0.02f, 30f);
            _warmup = Mathf.Max(_warmup, 4);
            current.Use();
            Repaint();
        }
    }

    /// <summary>Pan khung nhìn: kéo chuột giữa thì "nắm" khung hình và kéo theo chuột như Scene view.</summary>
    private void PanView(Vector2 delta)
    {
        if (_renderer == null) return;

        Camera camera = _renderer.camera;
        Rect viewport = _lastViewport.width > 1f ? _lastViewport : new Rect(0f, 0f, 800f, 600f);
        float pixelsPerUnit = PixelsPerUnit(viewport, camera, FocusPoint());
        float unitsPerPixel = 1f / Mathf.Max(pixelsPerUnit, 0.0001f);

        _pan += (-camera.transform.right * delta.x + camera.transform.up * delta.y) * unitsPerPixel;
    }

    // ------------------------------------------------------------- gizmo

    private Transform GizmoTarget()
    {
        if (_offHand) return _previewSaya != null ? _previewSaya.transform : null;
        return _previewSword != null ? _previewSword.transform : null;
    }

    private static Vector2 WorldToGui(Rect viewport, Camera camera, Vector3 world)
    {
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0.01f) return new Vector2(-99999f, -99999f);
        return new Vector2(
            viewport.x + screen.x / Mathf.Max(camera.pixelWidth, 1) * viewport.width,
            viewport.y + (1f - screen.y / Mathf.Max(camera.pixelHeight, 1)) * viewport.height);
    }

    private static float PixelsPerUnit(Rect viewport, Camera camera, Vector3 world)
    {
        Vector2 a = WorldToGui(viewport, camera, world);
        Vector2 b = WorldToGui(viewport, camera, world + camera.transform.right * 0.1f);
        return Mathf.Max(Vector2.Distance(a, b) / 0.1f, 1f);
    }

    private void DrawGizmo(Rect viewport)
    {
        if (_gizmo == GizmoMode.None || _renderer == null) return;
        Transform target = GizmoTarget();
        if (target == null) return;

        Camera camera = _renderer.camera;
        Vector2 center = WorldToGui(viewport, camera, target.position);
        if (center.x < -9000f) return;

        float worldLength = 80f / PixelsPerUnit(viewport, camera, target.position);
        Quaternion basis = _localSpace ? target.rotation : Quaternion.identity;
        Vector3[] axes = { basis * Vector3.right, basis * Vector3.up, basis * Vector3.forward };
        Color[] colors = { new Color(0.95f, 0.32f, 0.32f), new Color(0.36f, 0.92f, 0.40f), new Color(0.36f, 0.62f, 1f) };

        Handles.BeginGUI();
        for (int index = 0; index < 3; index++)
        {
            Handles.color = _dragAxis == index ? Color.white : colors[index];
            if (_gizmo == GizmoMode.Rotate)
            {
                DrawRing(viewport, camera, target.position, axes[index], worldLength * 0.85f);
            }
            else
            {
                Vector2 tip = WorldToGui(viewport, camera, target.position + axes[index] * worldLength);
                Handles.DrawAAPolyLine(_dragAxis == index ? 5f : 3f,
                    new Vector3(center.x, center.y, 0f), new Vector3(tip.x, tip.y, 0f));
                EditorGUI.DrawRect(new Rect(tip.x - 4f, tip.y - 4f, 8f, 8f), Handles.color);
            }
        }
        Handles.EndGUI();
    }

    private void DrawRing(Rect viewport, Camera camera, Vector3 center, Vector3 axis, float radius)
    {
        Vector3 u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;
        const int steps = 48;
        Vector3 previous = Vector3.zero;
        for (int index = 0; index <= steps; index++)
        {
            float angle = index / (float)steps * Mathf.PI * 2f;
            Vector3 point = center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
            Vector2 gui = WorldToGui(viewport, camera, point);
            Vector3 current = new Vector3(gui.x, gui.y, 0f);
            if (index > 0) Handles.DrawAAPolyLine(2f, previous, current);
            previous = current;
        }
    }

    private int PickAxis(Rect viewport, Vector2 mouse)
    {
        Transform target = GizmoTarget();
        if (target == null || _renderer == null) return -1;

        Camera camera = _renderer.camera;
        Vector2 center = WorldToGui(viewport, camera, target.position);
        float worldLength = 80f / PixelsPerUnit(viewport, camera, target.position);
        Quaternion basis = _localSpace ? target.rotation : Quaternion.identity;
        Vector3[] axes = { basis * Vector3.right, basis * Vector3.up, basis * Vector3.forward };

        int bestAxis = -1;
        float bestDistance = 13f;
        for (int index = 0; index < 3; index++)
        {
            float distance;
            if (_gizmo == GizmoMode.Rotate)
                distance = DistanceToRing(viewport, camera, target.position, axes[index], worldLength * 0.85f, mouse);
            else
                distance = DistanceToSegment(center, WorldToGui(viewport, camera, target.position + axes[index] * worldLength), mouse);

            if (distance < bestDistance) { bestDistance = distance; bestAxis = index; }
        }
        return bestAxis;
    }

    private float DistanceToRing(Rect viewport, Camera camera, Vector3 center, Vector3 axis, float radius, Vector2 mouse)
    {
        Vector3 u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;
        const int steps = 48;
        float best = float.MaxValue;
        Vector2 previous = Vector2.zero;
        for (int index = 0; index <= steps; index++)
        {
            float angle = index / (float)steps * Mathf.PI * 2f;
            Vector3 point = center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
            Vector2 gui = WorldToGui(viewport, camera, point);
            if (index > 0) best = Mathf.Min(best, DistanceToSegment(previous, gui, mouse));
            previous = gui;
        }
        return best;
    }

    private static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 0.0001f) return Vector2.Distance(a, point);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(a + ab * t, point);
    }

    private void DragGizmo(Vector2 mouse)
    {
        Transform target = GizmoTarget();
        if (target == null || _renderer == null || _dragAxis < 0) return;

        Camera camera = _renderer.camera;
        Vector2 delta = mouse - _dragStartMouse;
        Quaternion basis = _localSpace ? target.rotation : Quaternion.identity;
        Vector3 axisWorld = _dragAxis == 0 ? basis * Vector3.right : _dragAxis == 1 ? basis * Vector3.up : basis * Vector3.forward;
        Quaternion parentRotation = target.parent != null ? target.parent.rotation : Quaternion.identity;

        if (_gizmo == GizmoMode.Move)
        {
            Vector2 origin = WorldToGui(_lastViewport, camera, target.position);
            Vector2 tip = WorldToGui(_lastViewport, camera, target.position + axisWorld);
            Vector2 screenAxis = tip - origin;
            float pixelsPerMeter = screenAxis.magnitude;
            if (pixelsPerMeter < 0.0001f) return;

            float meters = Vector2.Dot(delta, screenAxis / pixelsPerMeter) / pixelsPerMeter;
            Vector3 worldDelta = axisWorld * meters;
            _position = _dragStartPosition + Quaternion.Inverse(parentRotation) * worldDelta;
        }
        else if (_gizmo == GizmoMode.Rotate)
        {
            float angle = delta.x * 0.5f;
            float sign = Vector3.Dot(axisWorld, camera.transform.forward) > 0f ? -1f : 1f;

            Quaternion startWorld = parentRotation * Quaternion.Euler(_dragStartEuler);
            Quaternion rotated = Quaternion.AngleAxis(angle * sign, axisWorld) * startWorld;
            _euler = (Quaternion.Inverse(parentRotation) * rotated).eulerAngles;
        }
        else
        {
            _scale = Mathf.Clamp(_dragStartScale * (1f + (delta.x + delta.y) * 0.006f), 0.05f, 5f);
        }
    }

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(340f));
        EditorGUILayout.Space(2f);

        EditorGUILayout.LabelField("Bộ phận đang căn", EditorStyles.boldLabel);
        int side = GUILayout.SelectionGrid(_offHand ? 1 : 0, new[] { "Kiếm (tay phải)", "Vỏ (tay trái)" }, 2);
        if (side != (_offHand ? 1 : 0))
        {
            SwitchSide(side == 1);
            Repaint();
        }

        EditorGUILayout.Space(4f);
        DrawVectorRow("Vị trí (local, m)", ref _position, -0.6f, 0.6f);
        DrawVectorRow("Xoay (độ)", ref _euler, -180f, 180f);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Tỉ lệ model", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Scale", GUILayout.Width(36f));
        _scale = GUILayout.HorizontalSlider(_scale, 0.1f, 2.5f);
        _scale = EditorGUILayout.FloatField(_scale, GUILayout.Width(64f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ánh sáng preview", EditorStyles.boldLabel);
        _keyIntensity = EditorGUILayout.Slider("Đèn chính", _keyIntensity, 0f, 300f);
        _fillIntensity = EditorGUILayout.Slider("Đèn phụ", _fillIntensity, 0f, 300f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bake vào WeaponData", GUILayout.Height(28f))) Bake();
        if (GUILayout.Button("Đọc lại", GUILayout.Width(80f), GUILayout.Height(28f))) LoadValuesFromWeapon();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Hoàn tác (Ctrl+Z) — " + _undoStack.Count + " bước", GUILayout.Height(22f)))
            UndoLastEdit();

        EditorGUILayout.Space(8f);
        DrawAnimationSection();

        EditorGUILayout.EndVertical();
    }

    private void DrawVectorRow(string label, ref Vector3 value, float min, float max)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        value.x = DrawAxis("X", value.x, min, max);
        value.y = DrawAxis("Y", value.y, min, max);
        value.z = DrawAxis("Z", value.z, min, max);
        EditorGUI.indentLevel--;
    }

    private float DrawAxis(string axis, float value, float min, float max)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(axis, GUILayout.Width(14f));
        float slider = GUILayout.HorizontalSlider(value, min, max);
        float field = EditorGUILayout.FloatField(slider, GUILayout.Width(64f));
        EditorGUILayout.EndHorizontal();
        return field;
    }

    private void DrawAnimationSection()
    {
        EditorGUILayout.LabelField("Pose để căn (kéo thanh anim)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Thư mục", GUILayout.Width(48f));
        _clipFolder = EditorGUILayout.TextField(_clipFolder);
        if (GUILayout.Button("Quét", GUILayout.Width(44f))) RefreshClips();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.ObjectField("Clip", _clip, typeof(AnimationClip), false);

        if (_clips.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Clip", GUILayout.Width(48f));
            int index = EditorGUILayout.Popup(_clipIndex, _clipNames);
            if (index != _clipIndex)
            {
                _clipIndex = index;
                _clip = _clips[index];
                _time = 0f;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        float length = _clip != null ? Mathf.Max(_clip.length, 0.0001f) : 1f;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(_playing ? "■" : "▶", GUILayout.Width(28f))) _playing = !_playing;
        GUILayout.Label("Anim", GUILayout.Width(34f));
        float time = GUILayout.HorizontalSlider(_time, 0f, length);
        if (!Mathf.Approximately(time, _time))
        {
            _time = time;
            _playing = false;
        }
        GUILayout.Label(_clip != null ? _time.ToString("F2") + "/" + length.ToString("F2") : "--", GUILayout.Width(84f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _loop = EditorGUILayout.ToggleLeft("Lặp", _loop, GUILayout.Width(52f));
        if (GUILayout.Button("Về 0", GUILayout.Width(52f))) { _time = 0f; Repaint(); }
        if (_clip != null && GUILayout.Button("Cuối", GUILayout.Width(52f))) { _time = _clip.length; Repaint(); }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Nhả pose", GUILayout.Width(80f)))
        {
            _clip = null;
            _clipIndex = -1;
            StopAnimationMode();
            ResetPreviewObjects();
        }
        EditorGUILayout.EndHorizontal();

        if (_broken)
            EditorGUILayout.HelpBox("Preview đang lỗi — bấm 'Canh giữa' hoặc chọn lại nhân vật để dựng lại.", MessageType.Warning);
    }

    // ----------------------------------------------------- giá trị / bake

    private void LoadValuesFromWeapon()
    {
        if (_weapon == null) return;

        _swordPos = _weapon.IdleGripPosition;
        _swordEuler = _weapon.IdleGripRotation.eulerAngles;
        _swordScale = _weapon.WeaponScale <= 0f ? 1f : _weapon.WeaponScale;

        _sayaPos = _weapon.OffHandGripPosition;
        _sayaEuler = _weapon.OffHandGripRotation.eulerAngles;
        _sayaScale = _weapon.OffHandScale <= 0f ? 1f : _weapon.OffHandScale;

        LoadBuffer();
        _warmup = 8;
    }

    /// <summary>Chuyển bên đang căn: cất giá trị đang sửa của bên cũ trước, rồi nạp bên mới.</summary>
    private void SwitchSide(bool offHand)
    {
        if (_offHand == offHand) return;
        StoreBuffer();
        _offHand = offHand;
        LoadBuffer();
        _warmup = 8;
    }

    private void StoreBuffer()
    {
        if (_offHand)
        {
            _sayaPos = _position;
            _sayaEuler = _euler;
            _sayaScale = _scale;
        }
        else
        {
            _swordPos = _position;
            _swordEuler = _euler;
            _swordScale = _scale;
        }
    }

    private void LoadBuffer()
    {
        if (_offHand)
        {
            _position = _sayaPos;
            _euler = _sayaEuler;
            _scale = _sayaScale;
        }
        else
        {
            _position = _swordPos;
            _euler = _swordEuler;
            _scale = _swordScale;
        }
    }

    // ------------------------------------------------------------- undo

    private GripSnapshot CaptureSnapshot()
    {
        StoreBuffer();
        GripSnapshot snapshot = new GripSnapshot
        {
            SwordPos = _swordPos, SwordEuler = _swordEuler, SwordScale = _swordScale,
            SayaPos = _sayaPos, SayaEuler = _sayaEuler, SayaScale = _sayaScale,
            OffHand = _offHand,
        };
        return snapshot;
    }

    /// <summary>Lưu 1 mốc hoàn tác; gộp các thay đổi liên tiếp trong 0.35s thành 1 bước.</summary>
    private void PushSnapshot(GripSnapshot snapshot)
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastUndoPush < 0.35d) return;
        _lastUndoPush = now;

        _undoStack.Add(snapshot);
        if (_undoStack.Count > UndoCapacity) _undoStack.RemoveAt(0);
    }

    private void UndoLastEdit()
    {
        if (_undoStack.Count == 0)
        {
            _status = "Không còn bước nào để hoàn tác.";
            return;
        }

        GripSnapshot snapshot = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _swordPos = snapshot.SwordPos; _swordEuler = snapshot.SwordEuler; _swordScale = snapshot.SwordScale;
        _sayaPos = snapshot.SayaPos; _sayaEuler = snapshot.SayaEuler; _sayaScale = snapshot.SayaScale;
        _offHand = snapshot.OffHand;

        LoadBuffer();
        _warmup = 8;
        _status = "Đã hoàn tác (" + _undoStack.Count + " bước còn lại).";
        Repaint();
    }

    private void Bake()
    {
        if (_weapon == null) return;
        StoreBuffer();

        Undo.RecordObject(_weapon, "Bake weapon grip");
        _weapon.IdleGripPosition = _swordPos;
        _weapon.IdleGripRotation = Quaternion.Euler(_swordEuler);
        _weapon.WeaponScale = _swordScale;
        _weapon.OffHandGripPosition = _sayaPos;
        _weapon.OffHandGripRotation = Quaternion.Euler(_sayaEuler);
        _weapon.OffHandScale = _sayaScale;

        EditorUtility.SetDirty(_weapon);
        AssetDatabase.SaveAssets();
        _status = "Đã bake cả 2 bên vào " + _weapon.name +
                  " | kiếm pos " + _swordPos.ToString("F4") + " rot " + _swordEuler.ToString("F1") + " scale " + _swordScale.ToString("F3") +
                  " | vỏ pos " + _sayaPos.ToString("F4") + " rot " + _sayaEuler.ToString("F1") + " scale " + _sayaScale.ToString("F3");
        _warmup = 8;
        Repaint();
    }

    // ------------------------------------------------------------- anim

    private void RefreshClips()
    {
        _clips.Clear();
        if (!string.IsNullOrEmpty(_clipFolder) && AssetDatabase.IsValidFolder(_clipFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { _clipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview")) continue;
                    if (!clip.isHumanMotion) continue;
                    if (_clips.Contains(clip)) continue;
                    _clips.Add(clip);
                }
            }
        }

        _clips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        _clipNames = new string[_clips.Count];
        for (int index = 0; index < _clips.Count; index++) _clipNames[index] = _clips[index].name;

        if (_clipIndex >= _clips.Count) _clipIndex = -1;
        if (_clip == null && _clips.Count > 0)
        {
            int found = _clips.FindIndex(c => c.name == "idle");
            _clipIndex = found >= 0 ? found : 0;
            _clip = _clips[_clipIndex];
        }
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = _lastTick <= 0d ? 0f : (float)(now - _lastTick);
        _lastTick = now;

        // đang phát anim: vẽ mỗi tick để pose mượt
        if (_playing && _clip != null && delta > 0f)
        {
            _time += Mathf.Min(delta, 0.1f);
            if (_time >= _clip.length)
            {
                if (_loop) _time = 0f;
                else { _time = _clip.length; _playing = false; }
            }

            // giới hạn ~30 fps để không ngốn CPU khi đang xem anim
            if (now - _lastRepaint >= 1.0 / 30.0)
            {
                _lastRepaint = now;
                Repaint();
            }
            return;
        }

        // đứng yên: chỉ vẽ thêm vài khung hình sau mỗi thay đổi (HDRP cần vài frame để hội tụ exposure)
        if (_warmup <= 0) return;
        if (now - _lastRepaint < 1.0 / 60.0) return;
        _lastRepaint = now;
        _warmup--;
        Repaint();
    }

    // ----------------------------------------------------------- preview

    private void CleanupPreview()
    {
        _lastSampleClip = null;
        _lastSampleTime = -1f;

        // dọn hết object ẩn đã tạo (nhân vật preview nằm trong preview scene của renderer)
        if (_previewSword != null) DestroyImmediate(_previewSword);
        if (_previewSaya != null) DestroyImmediate(_previewSaya);
        if (_previewCharacter != null) DestroyImmediate(_previewCharacter);
        if (_poseSource != null) DestroyImmediate(_poseSource);
        if (_backdrop != null) DestroyImmediate(_backdrop);

        if (_renderer != null)
        {
            _renderer.Cleanup();
            _renderer = null;
        }

        _previewCharacter = null;
        _poseSource = null;
        _previewSword = null;
        _previewSaya = null;
        _backdrop = null;
        _rightHand = null;
        _leftHand = null;
        _posePairs.Clear();
    }

    private void ResetPreviewObjects()
    {
        StopAnimationMode();
        CleanupPreview();
        LoadValuesFromWeapon();
        Repaint();
    }

    private void StopAnimationMode()
    {
        if (_startedAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        _startedAnimationMode = false;
    }

    private GameObject ResolveModel()
    {
        if (_character == null) return null;
        if (_character.GetComponent<Animator>() != null) return _character;
        Animator animator = _character.GetComponentInChildren<Animator>(true);
        return animator != null ? animator.gameObject : null;
    }

    private void EnsurePreview()
    {
        if (_broken) return;

        if (_renderer == null)
        {
            _renderer = new PreviewRenderUtility();
            Camera camera = _renderer.camera;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 32f;
            TryAddHdrpCameraData(camera);
            TrySetHdrpClearColor(camera, BackgroundColor);

            if (_renderer.lights != null && _renderer.lights.Length > 0 && _renderer.lights[0] != null)
            {
                _renderer.lights[0].color = new Color(1f, 0.97f, 0.92f);
                _renderer.lights[0].transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            }
            if (_renderer.lights != null && _renderer.lights.Length > 1 && _renderer.lights[1] != null)
            {
                _renderer.lights[1].color = new Color(0.70f, 0.80f, 1f);
                _renderer.lights[1].transform.rotation = Quaternion.Euler(10f, 150f, 0f);
            }

            CreateBackdrop();
        }

        if (_previewCharacter == null) CreatePreviewCharacter();
        if (_previewCharacter == null) return;

        EnsureWeapons();
    }

    private void CreateBackdrop()
    {
        if (_backdrop != null) return;

        _backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _backdrop.name = "GripToolBackdrop";
        _backdrop.hideFlags = HideFlags.HideAndDontSave;
        _backdrop.transform.localScale = new Vector3(300f, 300f, 1f);
        Renderer renderer = _backdrop.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", new Color(0.055f, 0.06f, 0.075f));
                if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.055f, 0.06f, 0.075f));
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        _renderer.AddSingleGO(_backdrop);
    }

    private void CreatePreviewCharacter()
    {
        GameObject source = ResolveModel();
        if (source == null)
        {
            _status = "Chưa chọn nhân vật có Animator.";
            return;
        }

        try
        {
            _previewCharacter = InstantiatePreview(source);
            _previewCharacter.name = source.name + " (preview)";
            DisableAuxiliaryComponents(_previewCharacter, false);
            _renderer.AddSingleGO(_previewCharacter);
            _posePairs.Clear();
        }
        catch (System.Exception exception)
        {
            _broken = true;
            _status = "Không dựng được preview: " + exception.Message;
            if (_previewCharacter != null) DestroyImmediate(_previewCharacter);
            _previewCharacter = null;
        }
    }

    private static GameObject InstantiatePreview(GameObject source)
    {
        GameObject clone = Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static void DisableAuxiliaryComponents(GameObject root, bool disableRenderers)
    {
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        if (!disableRenderers) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
    }

    private void EnsureWeapons()
    {
        if (_weapon == null || _previewCharacter == null) return;

        Animator animator = _previewCharacter.GetComponent<Animator>();
        if (animator == null) animator = _previewCharacter.GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        if (_rightHand == null) _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (_leftHand == null) _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        // Tắt Animator của bản preview: nếu để chạy, controller (vd state attack01) sẽ ghi đè pose
        // vừa copy từ clip mỗi khi cửa sổ vẽ lại -> pose bị nhảy về trạng thái mặc định.
        if (animator.enabled) animator.enabled = false;

        if (_previewSword == null && _weapon.WeaponPrefab != null && _rightHand != null)
        {
            _previewSword = Instantiate(_weapon.WeaponPrefab, _rightHand, false);
            _previewSword.hideFlags = HideFlags.HideAndDontSave;
        }
        if (_previewSaya == null && _weapon.OffHandPrefab != null && _leftHand != null)
        {
            _previewSaya = Instantiate(_weapon.OffHandPrefab, _leftHand, false);
            _previewSaya.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private void ApplyLiveTransforms()
    {
        if (_weapon == null) return;

        // bên đang chọn dùng buffer đang sửa, bên còn lại dùng giá trị đã cất
        Vector3 swordPos = _offHand ? _swordPos : _position;
        Quaternion swordRot = Quaternion.Euler(_offHand ? _swordEuler : _euler);
        float swordScale = Mathf.Max(0.01f, _offHand ? _swordScale : _scale);

        Vector3 sayaPos = _offHand ? _position : _sayaPos;
        Quaternion sayaRot = Quaternion.Euler(_offHand ? _euler : _sayaEuler);
        float sayaScale = Mathf.Max(0.01f, _offHand ? _scale : _sayaScale);

        if (_previewSword != null)
        {
            _previewSword.transform.localPosition = swordPos;
            _previewSword.transform.localRotation = swordRot;
            _previewSword.transform.localScale = Vector3.one * swordScale;
        }
        if (_previewSaya != null)
        {
            _previewSaya.transform.localPosition = sayaPos;
            _previewSaya.transform.localRotation = sayaRot;
            _previewSaya.transform.localScale = Vector3.one * sayaScale;
        }
    }

    private void RenderViewport(Rect viewport)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            GUI.Label(viewport, "  Chỉ căn ở Edit mode — thoát Play mode để dùng preview.", EditorStyles.boldLabel);
            return;
        }

        EnsurePreview();
        if (_renderer == null || _previewCharacter == null || _broken) return;

        try
        {
            if (_clip != null && (_clip != _lastSampleClip || !Mathf.Approximately(_time, _lastSampleTime)))
            {
                try
                {
                    SamplePose(_clip, _time);
                    _lastSampleTime = _time;
                    _lastSampleClip = _clip;
                }
                catch (System.Exception sampleError)
                {
                    _status = "Không sample được pose: " + sampleError.Message;
                }
            }
            ApplyLiveTransforms();
            FrameCamera();

            _renderer.BeginPreview(viewport, GUIStyle.none);
            ApplyLighting();
            _renderer.camera.Render();
            Texture texture = _renderer.EndPreview();
            GUI.DrawTexture(viewport, texture, ScaleMode.StretchToFill, false);

            if (!string.IsNullOrEmpty(CapturePath))
            {
                CapturePreview(texture, CapturePath);
                CapturePath = null;
            }
        }
        catch (System.Exception exception)
        {
            _broken = true;
            _status = "Preview lỗi: " + exception.Message;
            CleanupPreview();
        }
    }

    /// <summary>Đèn preview: HDRP không dùng exposure của scene nên phải tự canh cường độ thấp.</summary>
    private void ApplyLighting()
    {
        if (_renderer == null || _renderer.lights == null) return;
        if (_renderer.lights.Length > 0 && _renderer.lights[0] != null) _renderer.lights[0].intensity = _keyIntensity;
        if (_renderer.lights.Length > 1 && _renderer.lights[1] != null) _renderer.lights[1].intensity = _fillIntensity;
    }

    private static void CapturePreview(Texture texture, string path)
    {
        var rt = texture as RenderTexture;
        if (rt == null) return;
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = previous;
        Object.DestroyImmediate(tex);
    }

    private void SamplePose(AnimationClip clip, float time)
    {
        if (_poseSource == null)
        {
            GameObject source = ResolveModel();
            if (source == null) return;
            _poseSource = InstantiatePreview(source);
            _poseSource.name = "GripToolPoseSource";
            _poseSource.transform.position = new Vector3(0f, -2000f, 0f);
            DisableAuxiliaryComponents(_poseSource, true);
            _posePairs.Clear();
        }

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _startedAnimationMode = true;
        }

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_poseSource, clip, Mathf.Clamp(time, 0f, clip.length));
        AnimationMode.EndSampling();

        CopyPose(_poseSource, _previewCharacter);
    }

    private void CopyPose(GameObject source, GameObject target)
    {
        if (_posePairs.Count == 0) BuildPosePairs(source, target);

        for (int index = 0; index < _posePairs.Count; index++)
        {
            Transform from = _posePairs[index][0];
            Transform to = _posePairs[index][1];
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
            string path = PathOf(source.transform, bone);
            if (string.IsNullOrEmpty(path)) continue; // không copy root: pose source nằm xa trong scene
            if (!map.TryGetValue(path, out Transform targetBone)) continue;
            _posePairs.Add(new[] { bone, targetBone });
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

    /// <summary>Điểm mà camera đang nhìn (đã cộng thêm pan của chuột giữa).</summary>
    private Vector3 FocusPoint()
    {
        Transform hand = _offHand ? _leftHand : _rightHand;
        if (_followHand && hand != null) return hand.position + _pan;
        return PreviewBounds().center + _pan;
    }

    private void FrameCamera()
    {
        Vector3 focus = FocusPoint();
        float distance;

        Transform hand = _offHand ? _leftHand : _rightHand;
        if (_followHand && hand != null)
        {
            distance = 0.62f * _zoom;
        }
        else
        {
            Bounds bounds = PreviewBounds();
            distance = Mathf.Max(bounds.extents.magnitude, 0.9f) * 1.9f * _zoom;
        }

        Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 position = focus - orbit * Vector3.forward * distance;

        Transform camera = _renderer.camera.transform;
        camera.position = position;
        camera.rotation = Quaternion.LookRotation(focus - position);
        _renderer.camera.nearClipPlane = Mathf.Max(distance * 0.01f, 0.01f);
        _renderer.camera.farClipPlane = distance * 8f + 60f;

        if (_backdrop != null)
        {
            _backdrop.transform.position = camera.position + camera.forward * (distance + 40f);
            _backdrop.transform.rotation = Quaternion.LookRotation(camera.forward, Vector3.up);
        }
    }

    private Bounds PreviewBounds()
    {
        Bounds bounds = new Bounds(_previewCharacter.transform.position + Vector3.up, new Vector3(0.4f, 0.4f, 0.4f));
        foreach (Renderer renderer in _previewCharacter.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null || !renderer.enabled) continue;
            if (_previewSword != null && renderer.transform.IsChildOf(_previewSword.transform)) continue;
            if (_previewSaya != null && renderer.transform.IsChildOf(_previewSaya.transform)) continue;
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    // ------------------------------------------------------- HDRP helpers

    private static System.Type FindHdrpCameraDataType()
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            if (type != null) return type;
        }
        return null;
    }

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
            // không có HDRP -> bỏ qua
        }
    }

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
            // HDRP đổi field -> giữ nguyên
        }
    }
}
