#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sinh clip idle "đứng thở mạnh mẽ" bằng code (muscle humanoid), lấy tư thế gốc từ Idle_Dante_Relaxed
/// rồi chỉnh sang dáng ưỡn ngực, vai mở, đầu thẳng + nhịp thở (hít nhanh, thở ra chậm) + dồn trọng tâm nhẹ.
/// Mọi chuyển động đều tuần hoàn theo độ dài clip nên loop liền mạch.
/// Chạy: Tools/DKM/Generate Idle Breath Strong (hoặc gọi IdleBreathGenerator.Generate() qua MCP).
/// </summary>
public static class IdleBreathGenerator
{
    const string BasePath = "Assets/_Asset/Animation/Player/Idle/Idle_Dante_Relaxed.anim";
    public const string OutPath = "Assets/_Asset/Animation/Player/Idle/Idle_Breath_Strong.anim";
    const float Length = 6f;          // 2 nhịp thở / loop
    const float BreathPeriod = 3f;
    const float InhaleRatio = 0.4f;   // hít vào chiếm 40% nhịp, thở ra 60%
    const int Fps = 30;
    const int KeyFps = 10;            // 10 key/giây là đủ mượt (tangent tính sẵn), file nhẹ
    const float MotionScale = 0.7f;   // độ lớn chuyển động thở/lắc (1 = mạnh, nhỏ hơn = nhẹ hơn)
    const float HipPitchForward = 3f; // đưa thân về thẳng đứng (gốc hơi ngả ra sau)

    // Giá trị trung bình tuyệt đối (ghi đè tư thế gốc)
    static readonly Dictionary<string, float> Target = new Dictionary<string, float>
    {
        { "Head Turn Left-Right", 0f }, { "Head Tilt Left-Right", 0f }, { "Head Nod Down-Up", -0.12f },
        { "Left Forearm Stretch", 0.62f }, { "Right Forearm Stretch", 0.58f },
    };

    // Độ lệch so với tư thế gốc: ưỡn ngực, vai mở ra sau, tay hơi tách khỏi thân
    static readonly Dictionary<string, float> Offset = new Dictionary<string, float>
    {
        { "Chest Front-Back", -0.02f }, { "UpperChest Front-Back", -0.06f },
        { "Left Shoulder Front-Back", -0.12f }, { "Right Shoulder Front-Back", -0.12f },
        { "Left Arm Down-Up", 0.03f }, { "Right Arm Down-Up", 0.03f },
        { "Left Arm Front-Back", -0.04f }, { "Right Arm Front-Back", -0.06f },
    };

    [MenuItem("Tools/DKM/Generate Idle Breath Strong")]
    public static AnimationClip Generate()
    {
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(BasePath);
        var clip = new AnimationClip { frameRate = Fps, name = "Idle_Breath_Strong" };
        int n = Mathf.RoundToInt(Length * KeyFps);
        var binds = AnimationUtility.GetCurveBindings(src);

        foreach (var b in binds)
        {
            string p = b.propertyName;
            if (p.StartsWith("RootQ")) continue;
            float baseVal = Target.TryGetValue(p, out var abs) ? abs : Mean(AnimationUtility.GetEditorCurve(src, b)) + (Offset.TryGetValue(p, out var off) ? off : 0f);
            var v = new float[n + 1];
            for (int i = 0; i <= n; i++) v[i] = baseVal + MotionScale * Motion(p, i / (float)KeyFps);
            AnimationUtility.SetEditorCurve(clip, b, Build(v));
        }
        WriteRootQ(src, clip, binds, n);

        var s = AnimationUtility.GetAnimationClipSettings(src);
        s.loopTime = true; s.loopBlend = true; s.stopTime = Length;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutPath);
        if (old != null) { EditorUtility.CopySerialized(clip, old); clip = old; }
        else AssetDatabase.CreateAsset(clip, OutPath);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return clip;
    }

    /// <summary>Nhịp thở 0..1: hít vào nhanh (ease), giữ ngắn, thở ra chậm; nhịp sau sâu hơn chút.</summary>
    static float Breath(float t)
    {
        int idx = Mathf.FloorToInt(t / BreathPeriod);
        float ph = Mathf.Repeat(t, BreathPeriod) / BreathPeriod;
        float depth = (idx % 2 == 0) ? 1f : 1.15f;
        float x = ph < InhaleRatio
            ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * ph / InhaleRatio)
            : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (ph - InhaleRatio) / (1f - InhaleRatio));
        return x * depth;
    }

    /// <summary>Dao động tuần hoàn theo Length (k chu kỳ / loop).</summary>
    static float Wave(float t, int k, float phase) => Mathf.Sin(2f * Mathf.PI * k * t / Length + phase);

    // Dồn trọng tâm chậm sang 2 bên (1 chu kỳ / loop)
    static float Sway(float t) => Wave(t, 1, 0f);

    static float Motion(string p, float t)
    {
        float B = Breath(t), S = Sway(t);
        switch (p)
        {
            case "RootT.y": return 0.004f * B;
            case "RootT.x": return 0.006f * S;
            case "Spine Front-Back": return -0.02f * B;
            case "Spine Left-Right": return -0.025f * S;
            case "Chest Front-Back": return -0.035f * B;
            case "UpperChest Front-Back": return -0.07f * B;
            case "UpperChest Twist Left-Right": return 0.015f * Wave(t, 1, 1.3f);
            case "Neck Nod Down-Up": return -0.03f * B;
            case "Head Nod Down-Up": return 0.015f * B + 0.012f * Wave(t, 2, 0.7f);
            case "Head Turn Left-Right": return 0.035f * Wave(t, 1, 2.1f) + 0.01f * Wave(t, 3, 0.4f);
            case "Head Tilt Left-Right": return 0.02f * S;
            case "Left Shoulder Down-Up": case "Right Shoulder Down-Up": return 0.07f * B;
            case "Left Shoulder Front-Back": case "Right Shoulder Front-Back": return -0.025f * B;
            case "Left Arm Down-Up": return 0.03f * B + 0.006f * S;
            case "Right Arm Down-Up": return 0.03f * B - 0.006f * S;
            case "Left Arm Front-Back": return 0.012f * Wave(t, 1, 0.9f);
            case "Right Arm Front-Back": return 0.012f * Wave(t, 1, 2.6f);
            case "Left Forearm Stretch": case "Right Forearm Stretch": return 0.015f * B;
        }
        // Ngón tay hơi duỗi khi hít vào
        if (p.Contains("Stretched")) return 0.03f * B;
        return 0f;
    }

    /// <summary>Hông: lấy hướng trung bình của clip gốc + lắc nhẹ theo dồn trọng tâm và nhịp thở.</summary>
    static void WriteRootQ(AnimationClip src, AnimationClip clip, EditorCurveBinding[] binds, int n)
    {
        var comp = new[] { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        var bs = new EditorCurveBinding[4]; var mean = new float[4];
        for (int c = 0; c < 4; c++)
        {
            bs[c] = System.Array.Find(binds, x => x.propertyName == comp[c]);
            mean[c] = Mean(AnimationUtility.GetEditorCurve(src, bs[c]));
        }
        var baseQ = new Quaternion(mean[0], mean[1], mean[2], mean[3]).normalized;
        var vals = new float[4][];
        for (int c = 0; c < 4; c++) vals[c] = new float[n + 1];
        Quaternion prev = baseQ;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)KeyFps;
            var q = Quaternion.Euler(HipPitchForward - MotionScale * 0.5f * Breath(t), MotionScale * 0.6f * Wave(t, 1, 1.7f), -MotionScale * Sway(t)) * baseQ;
            if (Quaternion.Dot(q, prev) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            prev = q;
            vals[0][i] = q.x; vals[1][i] = q.y; vals[2][i] = q.z; vals[3][i] = q.w;
        }
        for (int c = 0; c < 4; c++) AnimationUtility.SetEditorCurve(clip, bs[c], Build(vals[c]));
    }

    /// <summary>Tạo curve 1 key / frame, tangent tính theo sai phân trung tâm có wrap (loop không gãy).</summary>
    static AnimationCurve Build(float[] v)
    {
        int n = v.Length - 1; float dt = 1f / KeyFps;
        // Curve không dao động -> 2 key cho file nhẹ
        if (Mathf.Approximately(Mathf.Max(v), Mathf.Min(v))) return AnimationCurve.Constant(0f, n * dt, v[0]);
        var keys = new Keyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = v[i == 0 ? n - 1 : i - 1], c = v[i == n ? 1 : i + 1];
            float tan = (c - a) / (2f * dt);
            keys[i] = new Keyframe(i * dt, v[i], tan, tan);
        }
        return new AnimationCurve(keys);
    }

    static float Mean(AnimationCurve c)
    {
        if (c == null || c.length == 0) return 0f;
        float len = c.keys[c.length - 1].time, s = 0f; const int k = 64;
        for (int i = 0; i < k; i++) s += c.Evaluate(len * i / k);
        return s / k;
    }
}
#endif
