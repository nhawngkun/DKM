using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// màn demo quét mặt (play mode): tải ảnh từ máy -> quét -> chỉnh thanh trượt, kéo chuột xoay đầu, cuộn để zoom
public class FaceScanDemoUI : MonoBehaviour
{
    [SerializeField] FaceScanDemo demo;
    [SerializeField] Camera viewCamera;
    [SerializeField] float panelWidth = 360f;
    [SerializeField] float rotateSpeed = 0.3f;
    [SerializeField] Vector2 fovRange = new Vector2(10f, 40f);

    float yaw, uiScale = 1f;
    Vector2 sliderScroll;
    GUIStyle title, box, warn, error;

    void Start()
    {
        if (demo == null) throw new MissingReferenceException("FaceScanDemoUI: chưa gán FaceScanDemo");
        if (viewCamera == null) viewCamera = Camera.main;
        yaw = demo.transform.eulerAngles.y;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pos = mouse.position.ReadValue();
        if (pos.x < panelWidth * uiScale) return;

        if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
        {
            yaw -= mouse.delta.ReadValue().x * rotateSpeed;
            demo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f && viewCamera != null)
            viewCamera.fieldOfView = Mathf.Clamp(viewCamera.fieldOfView - Mathf.Sign(scroll) * 2f, fovRange.x, fovRange.y);
    }

    void OnGUI()
    {
        uiScale = Mathf.Max(1f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        EnsureStyles();
        float h = Screen.height / uiScale;

        GUILayout.BeginArea(new Rect(0, 0, panelWidth, h), box);
        GUILayout.Label("Face Scan Demo", title);
        GUILayout.Space(6);

        GUI.enabled = !demo.IsScanning;
        if (GUILayout.Button(demo.IsScanning ? "Đang quét..." : "Tải ảnh lên để quét", GUILayout.Height(40))) PickAndScan();
        if (GUILayout.Button("Về mặt trung tính", GUILayout.Height(26))) demo.ResetSliders();
        GUI.enabled = true;

        GUILayout.Space(6);
        if (!string.IsNullOrEmpty(demo.LastError)) GUILayout.Label(demo.LastError, error);
        else if (demo.Photo != null) GUILayout.Label($"Sai số landmark: {demo.ErrorBeforeMm:0.0} → {demo.ErrorAfterMm:0.0} mm");
        else GUILayout.Label("Chọn ảnh chân dung chụp thẳng, mặt tự nhiên, đủ sáng.");
        if (!string.IsNullOrEmpty(demo.Warnings)) GUILayout.Label("⚠ " + demo.Warnings.Replace("\n", "\n⚠ "), warn);

        if (demo.Photo != null)
        {
            Rect r = GUILayoutUtility.GetRect(panelWidth - 20, 200);
            GUI.DrawTexture(r, demo.Photo, ScaleMode.ScaleToFit);
        }

        GUILayout.Space(6);
        sliderScroll = GUILayout.BeginScrollView(sliderScroll);
        var sliders = demo.Sliders;
        for (int i = 0; i < sliders.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(sliders[i].name, GUILayout.Width(120));
            float v = GUILayout.HorizontalSlider(sliders[i].value, -1f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label(sliders[i].value.ToString("+0.00;-0.00"), GUILayout.Width(42));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, sliders[i].value)) demo.SetSlider(i, v);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.Label(new Rect(panelWidth + 12, h - 30, 500, 24), "Kéo chuột để xoay · cuộn để zoom");
    }

    void PickAndScan()
    {
        string path = ImageFilePicker.Open("Chọn ảnh chân dung (chụp thẳng, mặt tự nhiên)");
        if (string.IsNullOrEmpty(path)) return;
        demo.ScanFileAsync(path, this.GetCancellationTokenOnDestroy()).Forget();
    }

    void EnsureStyles()
    {
        if (title != null) return;
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.88f));
        bg.Apply();
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 12, 12) };
        box.normal.background = bg;
        title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        warn = new GUIStyle(GUI.skin.label) { wordWrap = true };
        warn.normal.textColor = new Color(1f, 0.8f, 0.3f);
        error = new GUIStyle(warn);
        error.normal.textColor = new Color(1f, 0.45f, 0.4f);
    }
}
