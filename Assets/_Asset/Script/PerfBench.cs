#if PERF_BENCH
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KinematicCharacterController;
using UnityEngine;

// chỉ biên dịch trong bản build của PerfBenchBuild: đo FPS không giới hạn ở từng vị trí rồi tự thoát
public class PerfBench : MonoBehaviour
{
    private const float SettleSeconds = 8f;
    private const float SampleSeconds = 12f;

    private static readonly Vector3[] Spots = { new Vector3(-15.71f, -0.99f, -32.59f), new Vector3(0f, 9.1f, 59.5f) };

    private readonly List<double> _frame = new List<double>();
    private readonly List<double> _gpu = new List<double>();
    private readonly List<double> _main = new List<double>();
    private readonly List<double> _render = new List<double>();
    private readonly FrameTiming[] _timing = new FrameTiming[1];
    private readonly StringBuilder _report = new StringBuilder();
    private KinematicCharacterMotor _motor;
    private int _spot = -1;
    private float _stateStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() => new GameObject(nameof(PerfBench)).AddComponent<PerfBench>();

    private void Start()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        _motor = FindAnyObjectByType<KinematicCharacterMotor>();
        _report.AppendLine($"screen={Screen.width}x{Screen.height} gfx={SystemInfo.graphicsDeviceType}");
        NextSpot();
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        float elapsed = Time.realtimeSinceStartup - _stateStart;
        if (elapsed < SettleSeconds) return;

        _frame.Add(Time.unscaledDeltaTime * 1000.0);
        if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
        {
            _gpu.Add(_timing[0].gpuFrameTime);
            _main.Add(_timing[0].cpuMainThreadFrameTime);
            _render.Add(_timing[0].cpuRenderThreadFrameTime);
        }

        if (elapsed < SettleSeconds + SampleSeconds) return;
        double avg = _frame.Average();
        _report.AppendLine($"spot{_spot} fps={1000.0 / avg:F1} p99Ms={Percentile(_frame, 0.99):F2} gpu={Percentile(_gpu, 0.5):F2} main={Percentile(_main, 0.5):F2} render={Percentile(_render, 0.5):F2} frames={_frame.Count}");
        NextSpot();
    }

    private void NextSpot()
    {
        _spot++;
        if (_spot >= Spots.Length)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "perf_result.txt"), _report.ToString());
            Application.Quit();
            enabled = false;
            return;
        }

        if (_motor != null) _motor.SetPosition(Spots[_spot]);
        _frame.Clear();
        _gpu.Clear();
        _main.Clear();
        _render.Clear();
        _stateStart = Time.realtimeSinceStartup;
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0) return -1.0;
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * p))];
    }
}
#endif
