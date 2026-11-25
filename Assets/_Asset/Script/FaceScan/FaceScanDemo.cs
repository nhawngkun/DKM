using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

// demo quét mặt từ ảnh (phương án A rút gọn): landmark ảnh -> khớp thanh trượt bằng bình phương tối thiểu.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FaceScanDemo : MonoBehaviour
{
    [Serializable]
    public class FaceSlider
    {
        [ReadOnly] public string name;
        [Range(-1f, 1f)] public float value;
    }

    [Serializable]
    class HeadData
    {
        public float[] vertices, anchors, mask;
        public int[] skin, eyes, lips, brows;
    }

    [Serializable]
    class ScanResult
    {
        public bool ok;
        public string error, image, debugImage;
        public float[] landmarks, skin, iris, brow, lips;
        public float yaw, pitch;
        public string[] warnings;
    }

    [SerializeField] TextAsset headJson;
    [SerializeField] string pythonExe = "python";
    [SerializeField, Range(0f, 0.5f)] float regularization = 0.05f;
    [SerializeField, Range(0f, 1f), Tooltip("độ sâu từ 1 ảnh kém tin cậy hơn x/y")] float depthWeight = 0.3f;
    [SerializeField] bool showLandmarks = true;
    [SerializeField] List<FaceSlider> sliders = new List<FaceSlider>();

    [Title("Kết quả quét")]
    [ShowInInspector, ReadOnly, PreviewField(220, ObjectFieldAlignment.Left), HideLabel] Texture2D photo;
    [SerializeField, ReadOnly] string scannedImage;
    [SerializeField, ReadOnly] float errorBeforeMm, errorAfterMm;
    [SerializeField, ReadOnly, MultiLineProperty] string warnings;
    [SerializeField, ReadOnly] Color skinColor = new Color(0.85f, 0.68f, 0.58f);
    [SerializeField, ReadOnly] Color eyeColor = new Color(0.15f, 0.1f, 0.08f);
    [SerializeField, ReadOnly] Color lipColor = new Color(0.7f, 0.45f, 0.42f);
    [SerializeField, ReadOnly] Color browColor = new Color(0.25f, 0.18f, 0.12f);

    HeadData head;
    Mesh mesh;
    Vector3[] baseVerts, workVerts, anchors, target;
    // delta trên 468 điểm neo (để khớp) và trên toàn lưới (để hiển thị)
    List<(string name, Vector3[] delta)> fields;
    List<Vector3[]> meshDeltas;
    Material[] materials;

    public IReadOnlyList<FaceSlider> Sliders => sliders;
    public Texture2D Photo => photo;
    public string Warnings => warnings;
    public float ErrorBeforeMm => errorBeforeMm;
    public float ErrorAfterMm => errorAfterMm;
    public bool IsScanning { get; private set; }
    public string LastError { get; private set; }

    // ExecuteAlways: dựng lại lưới sau mỗi lần reload script / mở scene (lưới không lưu vào scene)
    void OnEnable()
    {
        if (headJson != null) Initialize();
    }

    void OnValidate()
    {
        // OnValidate chạy trước OnEnable sau reload/vào play: cache có thể chưa dựng hoặc vật liệu DontSave đã bị huỷ
        if (fields != null && mesh != null && MaterialsAlive) ApplySliders();
    }

    bool MaterialsAlive => materials != null && materials[0] != null;

    #region Face

    public void Initialize()
    {
        if (headJson == null) throw new InvalidOperationException("FaceScanDemo: thiếu headJson (FaceHead.json)");
        head = JsonUtility.FromJson<HeadData>(headJson.text);
        baseVerts = ToVectors(head.vertices);
        anchors = ToVectors(head.anchors);
        workVerts = new Vector3[baseVerts.Length];

        fields = FaceSliders.Build(anchors, anchors);
        meshDeltas = new List<Vector3[]>();
        foreach (var f in FaceSliders.Build(anchors, baseVerts))
        {
            for (int i = 0; i < f.delta.Length; i++) f.delta[i] *= head.mask[i];
            meshDeltas.Add(f.delta);
        }
        SyncSliderList();

        if (mesh == null) mesh = new Mesh { name = "FaceScanHead", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.Clear();
        mesh.SetVertices(baseVerts);
        mesh.subMeshCount = 4;
        mesh.SetTriangles(head.skin, 0);
        mesh.SetTriangles(head.eyes, 1);
        mesh.SetTriangles(head.lips, 2);
        mesh.SetTriangles(head.brows, 3);
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (!MaterialsAlive)
        {
            var shader = Shader.Find("HDRP/Lit");
            materials = new Material[4];
            for (int i = 0; i < 4; i++) materials[i] = new Material(shader) { hideFlags = HideFlags.DontSave };
            materials[0].SetFloat("_Smoothness", 0.35f);
            GetComponent<MeshRenderer>().sharedMaterials = materials;
        }
        ApplySliders();
    }

    public void ApplySliders()
    {
        Array.Copy(baseVerts, workVerts, baseVerts.Length);
        for (int k = 0; k < fields.Count && k < sliders.Count; k++)
        {
            float s = sliders[k].value;
            if (s == 0f) continue;
            var d = meshDeltas[k];
            for (int i = 0; i < workVerts.Length; i++) workVerts[i] += d[i] * s;
        }
        mesh.SetVertices(workVerts);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        materials[0].SetColor("_BaseColor", skinColor);
        materials[1].SetColor("_BaseColor", eyeColor);
        materials[2].SetColor("_BaseColor", lipColor);
        materials[3].SetColor("_BaseColor", browColor);
    }

    public void SetSlider(int index, float value)
    {
        sliders[index].value = Mathf.Clamp(value, -1f, 1f);
        ApplySliders();
    }

    [Button("Về mặt trung tính")]
    public void ResetSliders()
    {
        foreach (var s in sliders) s.value = 0f;
        ApplySliders();
    }

    // python chạy trên thread pool để UI không đứng; chỉ một lượt quét mỗi lúc
    public async UniTask<bool> ScanFileAsync(string imagePath, CancellationToken ct)
    {
        if (IsScanning) return false;
        IsScanning = true;
        LastError = null;
        try
        {
            string tool = ToolDir, outJson = OutputPath(tool, imagePath);
            string err = await UniTask.RunOnThreadPool(() => RunPython(tool, imagePath, outJson), cancellationToken: ct);
            return err == null ? ApplyScan(outJson) : Fail(err);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [Button]
    public void ScanFile(string imagePath)
    {
        LastError = null;
        string tool = ToolDir, outJson = OutputPath(tool, imagePath);
        string err = RunPython(tool, imagePath, outJson);
        if (err == null) ApplyScan(outJson);
        else Fail(err);
    }

    public bool ApplyScan(string jsonPath)
    {
        var res = JsonUtility.FromJson<ScanResult>(File.ReadAllText(jsonPath));
        if (!res.ok) return Fail(res.error);
        LastError = null;
        if (mesh == null) Initialize();

        target = ToVectors(res.landmarks);
        skinColor = ToColor(res.skin);
        eyeColor = ToColor(res.iris);
        lipColor = ToColor(res.lips);
        browColor = ToColor(res.brow);
        scannedImage = res.image;
        warnings = res.warnings is { Length: > 0 } ? string.Join("\n", res.warnings) : "";

        Fit();
        ApplySliders();
        ShowPhoto(res.debugImage);

        var values = new List<string>();
        foreach (var s in sliders) values.Add($"{s.name}={s.value:+0.00;-0.00}");
        Debug.Log($"FaceScan: {Path.GetFileName(res.image)} | sai số {errorBeforeMm:0.0} -> {errorAfterMm:0.0} mm | {string.Join(", ", values)}"
                  + (warnings.Length > 0 ? $" | cảnh báo: {warnings.Replace('\n', ';')}" : ""));
        return true;
    }

    #endregion

    #region Fitting

    // bình phương tối thiểu có ràng buộc [-1, 1]: giải hệ chuẩn bằng coordinate descent
    void Fit()
    {
        int n = anchors.Length, kCount = fields.Count;
        var w = new Vector3(1f, 1f, depthWeight);
        var a = new float[kCount, kCount];
        var b = new float[kCount];
        float trace = 0f;
        for (int k = 0; k < kCount; k++)
        {
            var dk = fields[k].delta;
            for (int j = k; j < kCount; j++)
            {
                var dj = fields[j].delta;
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += Vector3.Dot(Vector3.Scale(dk[i], w), dj[i]);
                a[k, j] = a[j, k] = sum;
            }
            for (int i = 0; i < n; i++) b[k] += Vector3.Dot(Vector3.Scale(dk[i], w), target[i] - anchors[i]);
            trace += a[k, k];
        }
        float lambda = regularization * trace / kCount;

        var s = new float[kCount];
        for (int iter = 0; iter < 300; iter++)
        {
            for (int k = 0; k < kCount; k++)
            {
                float rhs = b[k];
                for (int j = 0; j < kCount; j++) if (j != k) rhs -= a[k, j] * s[j];
                s[k] = Mathf.Clamp(rhs / (a[k, k] + lambda), -1f, 1f);
            }
        }
        for (int k = 0; k < kCount; k++) sliders[k].value = s[k];

        errorBeforeMm = Rms(anchors, n);
        var fitted = (Vector3[])anchors.Clone();
        for (int k = 0; k < kCount; k++)
            for (int i = 0; i < n; i++) fitted[i] += fields[k].delta[i] * s[k];
        errorAfterMm = Rms(fitted, n);
    }

    float Rms(Vector3[] verts, int n)
    {
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += (target[i] - verts[i]).sqrMagnitude;
        return Mathf.Sqrt(sum / n) * 1000f;
    }

    #endregion

    #region Helpers

    // editor: <project>/Tools/FaceScan; bản build: <thư mục game>/Tools/FaceScan
    static string ToolDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/FaceScan"));

    static string OutputPath(string tool, string imagePath)
    {
        string outJson = Path.Combine(tool, "out", Path.GetFileNameWithoutExtension(imagePath) + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(outJson));
        return outJson;
    }

    // trả về null nếu thành công, ngược lại là thông báo lỗi
    string RunPython(string tool, string imagePath, string outJson)
    {
        if (File.Exists(outJson)) File.Delete(outJson);
        var psi = new System.Diagnostics.ProcessStartInfo(pythonExe, $"\"{Path.Combine(tool, "face_scan.py")}\" \"{imagePath}\" \"{outJson}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        try
        {
            using var p = System.Diagnostics.Process.Start(psi);
            var errTask = p.StandardError.ReadToEndAsync();
            p.StandardOutput.ReadToEnd();
            string err = errTask.Result;
            p.WaitForExit();
            return p.ExitCode == 0 && File.Exists(outJson) ? null : $"python lỗi (exit {p.ExitCode})\n{err}";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return $"Không tìm thấy '{pythonExe}'. Cài Python rồi chạy: pip install mediapipe";
        }
    }

    bool Fail(string error)
    {
        LastError = error;
        Debug.LogError($"FaceScan: quét thất bại - {error}");
        return false;
    }

    void SyncSliderList()
    {
        if (sliders.Count == fields.Count && sliders.TrueForAll(s => s.name == fields[sliders.IndexOf(s)].name)) return;
        var old = new Dictionary<string, float>();
        foreach (var s in sliders) old[s.name] = s.value;
        sliders.Clear();
        foreach (var f in fields)
            sliders.Add(new FaceSlider { name = f.name, value = old.TryGetValue(f.name, out float v) ? v : 0f });
    }

    void ShowPhoto(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        if (photo == null) photo = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
        photo.LoadImage(File.ReadAllBytes(path));
    }

    static Vector3[] ToVectors(float[] f)
    {
        var v = new Vector3[f.Length / 3];
        for (int i = 0; i < v.Length; i++) v[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]);
        return v;
    }

    static Color ToColor(float[] c) => c is { Length: 3 } ? new Color(c[0], c[1], c[2]) : Color.magenta;

    #endregion

    #region Debug

    [Button("Quét ảnh...")]
    void ScanPhoto()
    {
        string img = ImageFilePicker.Open("Chọn ảnh chân dung (chụp thẳng, mặt tự nhiên)");
        if (!string.IsNullOrEmpty(img)) ScanFile(img);
    }

    void OnDrawGizmosSelected()
    {
        if (!showLandmarks || target == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.red;
        for (int i = 0; i < target.Length; i++) Gizmos.DrawSphere(target[i], 0.0008f);
    }

    #endregion
}
