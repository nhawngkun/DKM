using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
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

    private enum MarkerKind { None, Hit, Kick, Slam }

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

    private Animator _preview;
    private GameObject _spawnedPreview;
    private bool _startedAnimationMode;

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
    private string _status;
    private readonly List<string> _validation = new List<string>();

    private WeaponData Weapon => _weapons.Count > 0 ? _weapons[Mathf.Clamp(_weaponIndex, 0, _weapons.Count - 1)] : null;
    private ComboChain Chain => Weapon != null && Weapon.Chains.Count > 0 ? Weapon.Chains[Mathf.Clamp(_chainIndex, 0, Weapon.Chains.Count - 1)] : null;
    private AttackStep Step => Chain != null && Chain.Steps.Count > 0 ? Chain.Steps[Mathf.Clamp(_stepIndex, 0, Chain.Steps.Count - 1)] : null;
    private float Length => Step != null && Step.Clip != null ? Mathf.Max(Step.Clip.length, 0.001f) : 1f;
    private float Normalized => Mathf.Clamp01(_time / Length);

    [MenuItem("Tools/Combat/Timing Window")]
    public static void Open()
    {
        CombatTimingWindow window = GetWindow<CombatTimingWindow>("Mốc đòn đánh");
        window.minSize = new Vector2(660f, 560f);
        window.Show();
    }

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

        _tab = GUILayout.Toolbar(_tab, new[] { "Mốc đánh", "Âm thanh", "Bộ anim & Vũ khí", "Build" });
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

        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
    }

    // ------------------------------------------------------------------
    // Tab 1: timeline mốc đánh
    // ------------------------------------------------------------------

    private void DrawTimingTab()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            if (_weapons.Count == 0)
            {
                EditorGUILayout.HelpBox("Chưa có WeaponData nào. Chạy Tools/Combat/Build Weapon Combos trước.", MessageType.Warning);
                return;
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Vũ khí", GUILayout.Width(52f));
            string[] weaponNames = _weapons.Select(w => string.IsNullOrEmpty(w.DisplayName) ? w.name : w.DisplayName).ToArray();
            int weaponIndex = EditorGUILayout.Popup(_weaponIndex, weaponNames);
            if (weaponIndex != _weaponIndex)
            {
                _weaponIndex = weaponIndex;
                _chainIndex = 0;
                _stepIndex = 0;
                _playing = false;
                _time = 0f;
            }
            if (GUILayout.Button("Quét lại", GUILayout.Width(70f))) RefreshWeapons();
            if (GUILayout.Button("Chọn asset", GUILayout.Width(84f)) && Weapon != null)
            {
                Selection.activeObject = Weapon;
                EditorGUIUtility.PingObject(Weapon);
            }
            GUILayout.EndHorizontal();

            if (Weapon == null || Weapon.Chains.Count == 0)
            {
                EditorGUILayout.HelpBox("Vũ khí này chưa có chuỗi combo nào.", MessageType.Warning);
                return;
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Chuỗi", GUILayout.Width(52f));
            string[] chainNames = Weapon.Chains.Select(c => c.Name + "   [" + c.Context + "]").ToArray();
            int chainIndex = EditorGUILayout.Popup(_chainIndex, chainNames);
            if (chainIndex != _chainIndex)
            {
                _chainIndex = chainIndex;
                _stepIndex = 0;
                _playing = false;
                _time = 0f;
            }
            GUILayout.EndHorizontal();

            ClampSelection();
            ComboChain chain = Chain;
            if (chain == null || chain.Steps.Count == 0)
            {
                EditorGUILayout.HelpBox("Chuỗi này rỗng.", MessageType.Warning);
                return;
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Đòn", GUILayout.Width(52f));
            string[] stepNames = chain.Steps.Select((s, i) => $"{i + 1}. {s.Name}").ToArray();
            int stepIndex = EditorGUILayout.Popup(_stepIndex, stepNames);
            if (stepIndex != _stepIndex)
            {
                _stepIndex = stepIndex;
                _playing = false;
                _time = 0f;
            }
            GUILayout.EndHorizontal();

            AttackStep step = Step;
            if (step == null) return;

            EditorGUILayout.Space();
            DrawStepNumbers(step);
            DrawTimeline(step);
            DrawTransport();

            EditorGUILayout.Space();
            DrawTimesEditor("Mốc chém (HitTimes)", step.HitTimes, MarkerKind.Hit);
            DrawTimesEditor("Mốc đá (KickTimes)", step.KickTimes, MarkerKind.Kick);
            DrawSlamRow(step);

            EditorGUILayout.Space();
            DrawStepWarnings(step);
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawStepNumbers(AttackStep step)
    {
        GUILayout.BeginHorizontal();
        float speed = EditorGUILayout.FloatField("Speed", step.Speed, GUILayout.Width(150f));
        float fade = EditorGUILayout.FloatField("FadeIn", step.FadeIn, GUILayout.Width(170f));
        float weight = EditorGUILayout.Slider("Weight", step.Weight, 0f, 1f);
        GUILayout.EndHorizontal();

        if (Mathf.Approximately(speed, step.Speed) && Mathf.Approximately(fade, step.FadeIn) && Mathf.Approximately(weight, step.Weight)) return;

        Undo.RecordObject(Weapon, "Sửa thông số đòn");
        step.Speed = Mathf.Max(0.01f, speed);
        step.FadeIn = Mathf.Max(0f, fade);
        step.Weight = Mathf.Clamp01(weight);
        EditorUtility.SetDirty(Weapon);
    }

    private void DrawTimeline(AttackStep step)
    {
        Rect area = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(132f), GUILayout.ExpandWidth(true));
        Rect strip = new Rect(area.x + 10f, area.y + 30f, Mathf.Max(area.width - 20f, 20f), TrackHeight);

        GUI.Label(new Rect(strip.x, area.y + 10f, strip.width, 16f),
            $"{(step.Clip != null ? step.Clip.name : "(thiếu clip)")}   •   dài {Length:F2}s   •   Speed {step.Speed:F2}",
            EditorStyles.miniBoldLabel);

        if (Event.current.type == EventType.Repaint) DrawTimelineBackground(step, strip);
        HandleTimelineEvents(step, strip);

        GUI.Label(new Rect(strip.x, strip.yMax + 2f, strip.width - 100f, 16f),
            $"kim {_time:F3}s   ({Normalized:F3} normalized)", EditorStyles.miniLabel);
        GUI.Label(new Rect(strip.xMax - 100f, strip.yMax + 2f, 100f, 16f),
            $"quét −{_windowBefore:F2}/+{_windowAfter:F2}", EditorStyles.miniLabel);
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

        DrawGuideLine(strip, step.LinkTime, LinkColor, "nối");
        DrawGuideLine(strip, step.MoveCancelTime, CancelColor, "huỷ");
        if (step.NextSeamTime >= 0f) DrawGuideLine(strip, step.NextSeamTime / Length, SeamColor, "seam");

        for (int i = 0; i < step.HitTimes.Count; i++) DrawPin(strip, step.HitTimes[i], HitColor, 0f, 1f, "H" + (i + 1));
        for (int i = 0; i < step.KickTimes.Count; i++) DrawPin(strip, step.KickTimes[i], KickColor, 0.45f, 1f, "K" + (i + 1));
        if (step.SlamTime >= 0f) DrawPin(strip, step.SlamTime, SlamColor, 0f, 1f, "S");

        EditorGUI.DrawRect(new Rect(playheadX - 1f, strip.y - 5f, 2f, strip.height + 10f), Color.white);
    }

    private static void DrawPin(Rect strip, float normalized, Color color, float top, float bottom, string label)
    {
        float x = strip.x + Mathf.Clamp01(normalized) * strip.width;
        float y = strip.y + top * strip.height;
        float height = (bottom - top) * strip.height;
        EditorGUI.DrawRect(new Rect(x - 1.5f, y, 3f, height), color);
        EditorGUI.DrawRect(new Rect(x - 4.5f, y, 9f, 4f), color);
        GUI.Label(new Rect(x - 12f, y + 4f, 24f, 12f), label, EditorStyles.miniLabel);
    }

    private static void DrawGuideLine(Rect strip, float normalized, Color color, string label)
    {
        float x = strip.x + Mathf.Clamp01(normalized) * strip.width;
        EditorGUI.DrawRect(new Rect(x - 0.5f, strip.y, 1f, strip.height), color);
        GUI.Label(new Rect(x + 2f, strip.yMax - 13f, 44f, 12f), label, EditorStyles.miniLabel);
    }

    private void HandleTimelineEvents(AttackStep step, Rect strip)
    {
        Event current = Event.current;
        Rect grab = new Rect(strip.x - 6f, strip.y - 18f, strip.width + 12f, strip.height + 24f);

        if (current.type == EventType.MouseDown && current.button == 0 && grab.Contains(current.mousePosition))
        {
            if (!FindMarker(step, strip, current.mousePosition, out MarkerKind kind, out int index, out float markerTime))
            {
                _time = Snap(TimeAt(strip, current.mousePosition));
                SampleNow();
            }
            else if (current.alt)
            {
                DeleteMarker(step, kind, index);
            }
            else
            {
                _dragKind = kind;
                _dragIndex = index;
                _time = markerTime;
                Undo.RecordObject(Weapon, "Kéo mốc đánh");
                PlayMarkerSound(step, kind);
                SampleNow();
                _status = "Đang kéo " + KindLabel(kind) + " " + (index + 1) + " — giữ Alt + click để xoá mốc";
            }
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
        else if (_dragKind != MarkerKind.None && (current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow))
        {
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
                kind = MarkerKind.Slam;
                index = 0;
                time = step.SlamTime * Length;
            }
        }
        return kind != MarkerKind.None;
    }

    private static void SetMarker(AttackStep step, MarkerKind kind, int index, float normalized)
    {
        if (kind == MarkerKind.Hit && index >= 0 && index < step.HitTimes.Count) step.HitTimes[index] = normalized;
        else if (kind == MarkerKind.Kick && index >= 0 && index < step.KickTimes.Count) step.KickTimes[index] = normalized;
        else if (kind == MarkerKind.Slam) step.SlamTime = normalized;
    }

    private void DeleteMarker(AttackStep step, MarkerKind kind, int index)
    {
        if (Weapon == null) return;
        Undo.RecordObject(Weapon, "Xoá mốc đánh");
        if (kind == MarkerKind.Hit && index >= 0 && index < step.HitTimes.Count) step.HitTimes.RemoveAt(index);
        else if (kind == MarkerKind.Kick && index >= 0 && index < step.KickTimes.Count) step.KickTimes.RemoveAt(index);
        else if (kind == MarkerKind.Slam) step.SlamTime = -1f;
        EditorUtility.SetDirty(Weapon);
        AssetDatabase.SaveAssetIfDirty(Weapon);
        _status = "Đã xoá " + KindLabel(kind);
        Repaint();
    }

    private static string KindLabel(MarkerKind kind)
    {
        switch (kind)
        {
            case MarkerKind.Hit: return "mốc chém";
            case MarkerKind.Kick: return "mốc đá";
            case MarkerKind.Slam: return "mốc đập đất";
            default: return "mốc";
        }
    }

    private void DrawTransport()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⏮", GUILayout.Width(28f)))
        {
            _time = 0f;
            SampleNow();
        }
        if (GUILayout.Button(_playing ? "⏸" : "▶", GUILayout.Width(32f))) TogglePlay();
        if (GUILayout.Button("⏭", GUILayout.Width(28f)))
        {
            _time = Length;
            SampleNow();
        }
        _loop = GUILayout.Toggle(_loop, "Lặp", EditorStyles.toolbarButton, GUILayout.Width(44f));
        _snapFrame = GUILayout.Toggle(_snapFrame, "Snap", EditorStyles.toolbarButton, GUILayout.Width(50f));
        _snapFps = EditorGUILayout.IntField(_snapFps, GUILayout.Width(34f));
        GUILayout.Label("fps", EditorStyles.miniLabel, GUILayout.Width(24f));
        if (GUILayout.Button("Dừng preview", GUILayout.Width(104f))) StopPreview();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Space = phát/dừng", EditorStyles.miniLabel, GUILayout.Width(120f));
        GUILayout.EndHorizontal();
    }

    private void DrawTimesEditor(string label, List<float> times, MarkerKind kind)
    {
        AttackStep step = Step;
        if (step == null) return;

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(170f));
        if (GUILayout.Button("＋ tại kim", GUILayout.Width(74f)))
        {
            Undo.RecordObject(Weapon, "Thêm " + KindLabel(kind));
            times.Add(Normalized);
            times.Sort();
            EditorUtility.SetDirty(Weapon);
            AssetDatabase.SaveAssetIfDirty(Weapon);
        }
        if (times.Count > 1 && GUILayout.Button("Sắp xếp", GUILayout.Width(66f)))
        {
            Undo.RecordObject(Weapon, "Sắp xếp mốc");
            times.Sort();
            EditorUtility.SetDirty(Weapon);
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(times.Count + " mốc", EditorStyles.miniLabel, GUILayout.Width(54f));
        GUILayout.EndHorizontal();

        for (int i = 0; i < times.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            float seconds = times[i] * Length;
            float newSeconds = EditorGUILayout.FloatField(seconds, GUILayout.Width(64f));
            if (!Mathf.Approximately(newSeconds, seconds))
            {
                Undo.RecordObject(Weapon, "Sửa " + KindLabel(kind));
                times[i] = Mathf.Clamp01(newSeconds / Length);
                EditorUtility.SetDirty(Weapon);
            }
            GUILayout.Label($"= {times[i]:F3} norm", EditorStyles.miniLabel, GUILayout.Width(86f));
            if (GUILayout.Button("▶", GUILayout.Width(24f))) PlayMarkerSound(step, kind);
            if (GUILayout.Button("✕", GUILayout.Width(24f)))
            {
                Undo.RecordObject(Weapon, "Xoá " + KindLabel(kind));
                times.RemoveAt(i);
                EditorUtility.SetDirty(Weapon);
                break;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawSlamRow(AttackStep step)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Đập đất (SlamTime)", EditorStyles.boldLabel, GUILayout.Width(170f));
        bool hasSlam = step.SlamTime >= 0f;
        bool newHasSlam = GUILayout.Toggle(hasSlam, "có", GUILayout.Width(46f));
        if (newHasSlam != hasSlam)
        {
            Undo.RecordObject(Weapon, "Đổi mốc đập đất");
            step.SlamTime = newHasSlam ? Normalized : -1f;
            EditorUtility.SetDirty(Weapon);
            AssetDatabase.SaveAssetIfDirty(Weapon);
        }
        if (step.SlamTime >= 0f)
        {
            float seconds = step.SlamTime * Length;
            float newSeconds = EditorGUILayout.FloatField(seconds, GUILayout.Width(64f));
            if (!Mathf.Approximately(newSeconds, seconds))
            {
                Undo.RecordObject(Weapon, "Sửa mốc đập đất");
                step.SlamTime = Mathf.Clamp01(newSeconds / Length);
                EditorUtility.SetDirty(Weapon);
            }
            if (GUILayout.Button("▶", GUILayout.Width(24f))) PlayMarkerSound(step, MarkerKind.Slam);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
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

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponComboBuilder.ControllerPath);
        if (controller == null)
        {
            _validation.Add("✖ Không thấy Animator Controller: " + WeaponComboBuilder.ControllerPath);
            return;
        }

        AnimatorControllerLayer attackLayer = controller.layers.FirstOrDefault(layer => layer.name == WeaponComboBuilder.LayerName);
        if (attackLayer == null) _validation.Add("✖ Controller thiếu layer '" + WeaponComboBuilder.LayerName + "' -> chạy Build Weapon Combos");

        HashSet<string> states = new HashSet<string>();
        if (attackLayer != null && attackLayer.stateMachine != null)
        {
            foreach (ChildAnimatorState state in attackLayer.stateMachine.states) states.Add(state.state.name);
        }

        if (!controller.layers.Any(layer => layer.name == LocomotionAnimatorBuilder.LayerName))
            _validation.Add("✖ Controller thiếu layer '" + LocomotionAnimatorBuilder.LayerName + "' -> chạy Build Locomotion Animator");

        AnimSetLibrary animLibrary = LocomotionAnimatorBuilder.FindLibrary();
        if (animLibrary != null && animLibrary.Sets.Count != LocoSetPrefixes.Values.Length)
            _validation.Add($"⚠ AnimSetLibrary có {animLibrary.Sets.Count} bộ nhưng runtime đang có {LocoSetPrefixes.Values.Length} prefix -> bấm 'Sinh prefix' rồi build lại");
        if (animLibrary != null && animLibrary.Sets.Count > LocoSetPrefixes.Values.Length)
            _validation.Add("✖ Thêm bộ anim mới thì bắt buộc Sinh prefix, không thì CharacterAnim.SetWeapon báo AnimSetId không hợp lệ");

        float gap = _windowBefore + _windowAfter;
        foreach (WeaponData weapon in _weapons)
        {
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
        if (_fx == null) _fx = FindFx();
        if (_fx == null) return;

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

    /// <summary>
    /// Phát clip ngoài play mode bằng AudioUtil của Editor (reflection, vì API này là internal).
    /// Không tìm thấy thì im lặng — mọi thứ khác vẫn chạy.
    /// </summary>
    private static void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

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
        SceneView.RepaintAll();
    }

    private void EnsurePreview()
    {
        if (_preview != null) return;

        CharacterCombo combo = UnityEngine.Object.FindAnyObjectByType<CharacterCombo>();
        if (combo != null && combo.animator != null)
        {
            _preview = combo.animator;
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
        _spawnedPreview.transform.position = Vector3.zero;
        _preview = _spawnedPreview.GetComponentInChildren<Animator>();
        _status = "Preview bằng bản sao Character.prefab (scene không có CharacterCombo)";
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
            if (data != null) _weapons.Add(data);
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
