using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PerfBenchBuild
{
    public const string ExePath = "Temp/PerfBench/DKM.exe";
    public const string StatePath = "Temp/perf_build_state.txt";

    [MenuItem("Tools/DKM/Build Perf Bench")]
    public static void Build()
    {
        File.WriteAllText(StatePath, "started");
        bool frameTiming = PlayerSettings.enableFrameTimingStats;
        PlayerSettings.enableFrameTimingStats = true;
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/FeudalMap/Scenes/FeudalMap.unity" },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = new[] { "PERF_BENCH" },
            });
            string result = $"done {report.summary.result} errors={report.summary.totalErrors} seconds={report.summary.totalTime.TotalSeconds:F0}";
            File.WriteAllText(StatePath, result);
            Debug.Log($"[PerfBench] {result}");
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = frameTiming;
            // build tự lưu ProjectSettings khi cờ đang bật, phải lưu lại giá trị gốc xuống đĩa
            AssetDatabase.SaveAssets();
        }
    }
}
