using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Dựng map thế giới mở 2 x 2 km: Terrain + prefab môi trường Naraka (gói NarakaWorld.unitypackage) + asset FeudalMap/Erbeilo3D.
// Asset Naraka là bản tách từ game thương mại: chỉ dùng nội bộ, không đưa vào bản phát hành.
// Menu Tools/World/Build Open World, hoặc batchmode -executeMethod OpenWorldBuilder.BuildBatch
public static class OpenWorldBuilder
{
    const string OutDir = "Assets/_Asset/World";
    const string AssetRoot = OutDir + "/Naraka";
    const string DetailDir = OutDir + "/Details";
    const string Check = "gamedesign/env/world";
    const float Size = 2048f, MaxH = 520f, Sea = 20f;
    const int HRes = 1025, ARes = 1024, DRes = 1024;

    #region Layout
    enum R { Snow, Pine, Maple, Canyon, Bamboo, Pampas, Lotus, Flower, Coast }
    const int RCount = 9;
    static readonly string[] RegionVi = { "Dãy Tuyết Sơn", "Rừng Thông Cổ", "Thung Lũng Phong Đỏ", "Hẻm Cát Vàng", "Rừng Trúc", "Đồng Lau Vàng", "Đầm Sen", "Đồi Hoa Trắng", "Bờ Biển Nam" };
    static readonly (R r, Vector2 c)[] Seeds =
    {
        (R.Snow, new Vector2(.28f, .80f)), (R.Snow, new Vector2(.48f, .83f)), (R.Snow, new Vector2(.69f, .80f)),
        (R.Pine, new Vector2(.15f, .62f)), (R.Pine, new Vector2(.26f, .68f)),
        (R.Maple, new Vector2(.43f, .64f)), (R.Maple, new Vector2(.57f, .61f)),
        (R.Canyon, new Vector2(.83f, .63f)), (R.Canyon, new Vector2(.75f, .71f)),
        (R.Bamboo, new Vector2(.85f, .42f)), (R.Bamboo, new Vector2(.80f, .29f)),
        (R.Pampas, new Vector2(.52f, .45f)), (R.Pampas, new Vector2(.64f, .40f)), (R.Pampas, new Vector2(.42f, .37f)),
        (R.Lotus, new Vector2(.17f, .45f)), (R.Lotus, new Vector2(.25f, .53f)),
        (R.Flower, new Vector2(.27f, .21f)), (R.Flower, new Vector2(.36f, .29f)),
        (R.Coast, new Vector2(.62f, .13f)), (R.Coast, new Vector2(.75f, .19f)), (R.Coast, new Vector2(.48f, .11f)),
    };

    static readonly Vector2[] River = { new Vector2(.47f, .78f), new Vector2(.47f, .68f), new Vector2(.47f, .56f), new Vector2(.50f, .44f), new Vector2(.54f, .30f), new Vector2(.56f, .08f) };
    static readonly Vector2 Lake = new Vector2(.20f, .47f);
    const float LakeR = 125f, IslandR = 26f;

    enum Kind { Village, Temple, Shrine, Outpost }
    class Site { public string name; public Kind kind; public Vector2 uv; public float r; public (string cat, int n)[] kit; public bool torii; }
    static readonly Site[] Sites =
    {
        new Site { name = "Làng Lau", kind = Kind.Village, uv = new Vector2(.585f, .50f), r = 85, kit = new[] { ("hallSmall", 2), ("house", 12), ("well", 1), ("bell", 1), ("stonelantern", 10), ("townprop", 8) } },
        new Site { name = "Làng Chài", kind = Kind.Village, uv = new Vector2(.645f, .175f), r = 55, kit = new[] { ("house", 8), ("well", 1), ("stonelantern", 6), ("townprop", 4) } },
        new Site { name = "Làng Trúc", kind = Kind.Village, uv = new Vector2(.79f, .39f), r = 50, kit = new[] { ("house", 6), ("hallSmall", 1), ("well", 1), ("stonelantern", 6) } },
        new Site { name = "Chùa Tuyết Sơn", kind = Kind.Temple, uv = new Vector2(.505f, .725f), r = 70, torii = true, kit = new[] { ("mainhall", 1), ("pagoda", 2), ("statue", 1), ("bell", 1), ("stonelantern", 14), ("brazier", 4) } },
        new Site { name = "Đền Phong", kind = Kind.Shrine, uv = new Vector2(.40f, .60f), r = 38, torii = true, kit = new[] { ("shrine", 1), ("pagoda", 1), ("stonelantern", 8) } },
        new Site { name = "Đền Sen", kind = Kind.Shrine, uv = Lake, r = 20, kit = new[] { ("shrine", 1), ("stonelantern", 4) } },
        new Site { name = "Đền Biển", kind = Kind.Shrine, uv = new Vector2(.31f, .22f), r = 30, torii = true, kit = new[] { ("shrine", 1), ("stonelantern", 6), ("toriiBlack", 1) } },
        new Site { name = "Trạm Hẻm Cát", kind = Kind.Outpost, uv = new Vector2(.765f, .60f), r = 38, kit = new[] { ("tower", 1), ("house", 3), ("gate", 1), ("brazier", 2) } },
        new Site { name = "Chòi Tiều Phu", kind = Kind.Outpost, uv = new Vector2(.23f, .65f), r = 26, kit = new[] { ("tower", 1), ("house", 1), ("stonelantern", 2) } },
    };
    static readonly (int a, int b)[] Roads = { (0, 1), (0, 2), (0, 3), (3, 4), (0, 5), (5, 6), (6, 1), (2, 7), (7, 3), (5, 8), (4, 8) };

    // asset sẵn có trong DKM, đã chạy HDRP
    static readonly (string cat, string[] paths)[] Native =
    {
        ("torii", new[] { "Assets/FeudalMap/Prefabs/Torii.prefab", "Assets/FeudalMap/Prefabs/Torii_Big.prefab" }),
        ("toriiBlack", new[] { "Assets/FeudalMap/Prefabs/Torii_Black.prefab" }),
        ("pagoda", new[] { "Assets/FeudalMap/Prefabs/Pagoda_2F.prefab", "Assets/FeudalMap/Prefabs/Pagoda_3F.prefab", "Assets/FeudalMap/Prefabs/Pagoda_5F.prefab" }),
        ("shrine", new[] { "Assets/FeudalMap/Prefabs/Shrine.prefab" }),
        ("hallSmall", new[] { "Assets/FeudalMap/Prefabs/Hall_Small.prefab" }),
        ("mainhall", new[] { "Assets/FeudalMap/Prefabs/MainHall_Boss.prefab" }),
        ("brazier", new[] { "Assets/FeudalMap/Prefabs/Brazier.prefab" }),
        ("stonelantern", new[] { "Assets/FeudalMap/Prefabs/StoneLantern.prefab", "Assets/FeudalMap/Prefabs/LanternPost.prefab", "Assets/Erbeilo3D/3D Model/Lamp/Prefab/Stone_Lantern.prefab", "Assets/Erbeilo3D/3D Model/Lamp/Prefab/Stone_Lantern_Mossy.prefab" }),
        ("sakura", new[] { "Assets/FeudalMap/Prefabs/Sakura_0.prefab", "Assets/FeudalMap/Prefabs/Sakura_1.prefab", "Assets/FeudalMap/Prefabs/Sakura_2.prefab" }),
        ("pineJP", new[] { "Assets/FeudalMap/Prefabs/Pine_0.prefab", "Assets/FeudalMap/Prefabs/Pine_1.prefab", "Assets/FeudalMap/Prefabs/Pine_2.prefab" }),
        ("bambooJP", new[] { "Assets/FeudalMap/Prefabs/Bamboo_0.prefab", "Assets/FeudalMap/Prefabs/Bamboo_1.prefab", "Assets/FeudalMap/Prefabs/Bamboo_2.prefab" }),
        ("fern", new[] { "Assets/Erbeilo3D/3D Model/Plant/Prefab/Fern_A0.prefab", "Assets/Erbeilo3D/3D Model/Plant/Prefab/Fern_A1.prefab", "Assets/Erbeilo3D/3D Model/Plant/Prefab/Fern_A2.prefab" }),
        ("berry", Enumerable.Range(0, 8).Select(i => $"Assets/Erbeilo3D/3D Model/Plant/Prefab/Billberry_A{i}.prefab").ToArray()),
        ("erock", new[] { "Assets/Erbeilo3D/3D Model/Rock/Prefab/Rock.prefab", "Assets/Erbeilo3D/3D Model/Rock/Prefab/rock1.prefab" }),
    };

    // thứ tự lớp khớp với TL_* trong gói; HDRP terrain chỉ nhận tối đa 8 lớp
    static readonly string[] LayerNames = { "grass", "dry", "soil", "sand", "rock", "snow", "road", "redrock" };
    const int LGrass = 0, LDry = 1, LSoil = 2, LSand = 3, LRock = 4, LSnow = 5, LRoad = 6, LRed = 7;
    #endregion

    class Tpl { public GameObject prefab; public Vector3 size; public Vector2 offXZ; public float minY; public string name; }
    static Dictionary<string, List<Tpl>> tpl;

    static float[,] H;
    static System.Random rng;
    static List<string> log;
    static Transform world;
    static readonly List<(Vector2 p, float r)> blocked = new List<(Vector2, float)>();
    static List<(Vector2 a, Vector2 b)> roadSegs;
    static List<List<Vector2>> roadLines;
    static List<Vector2> riverPts;
    static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    #region Build
    [MenuItem("Tools/World/Build Open World")]
    public static void Build()
    {
        log = new List<string>();
        rng = new System.Random(20261121);
        blocked.Clear(); groups.Clear(); counts.Clear();
        Directory.CreateDirectory(Check);
        var async = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            ConvertMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            world = new GameObject("World").transform;
            LoadTemplates();
            BuildHeights();
            BuildRoadLines();
            var plan = PlanSites();
            CarveRoads();
            var terrain = MakeTerrain();
            Paint(terrain.terrainData);
            PlaceSites(plan);
            PlaceBridges();
            Scatter();
            Details(terrain);
            Water();
            var cam = Lighting();
            var shots = Snapshots();
            AddPlayer(scene, shots);
            cam.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, $"{OutDir}/OpenWorld.unity");
            AssetDatabase.SaveAssets();
            WriteMapData();
            // batchmode không có khung thật nên ambient chưa cập nhật, ảnh sẽ đen mặt khuất: chỉ chụp khi chạy trong editor
            if (!Application.isBatchMode) CaptureLive(shots);
        }
        catch (System.Exception e) { log.Add("ERROR " + e); }
        finally { ShaderUtil.allowAsyncCompilation = async; }
        File.WriteAllLines(Check + "/world_result.txt", log);
        Debug.Log("[OpenWorld]\n" + string.Join("\n", log));
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(log.Any(l => l.StartsWith("ERROR")) ? 1 : 0);
    }
    #endregion

    #region Materials
    // material trong gói là Standard trung gian; đổi sang HDRP/Lit, lá thành translucent dùng diffusion profile Foliage
    static void ConvertMaterials()
    {
        var foliage = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>("Assets/Settings/HDRPDefaultResources/FoliageDiffusionProfile.asset");
        if (!foliage) log.Add("Không thấy FoliageDiffusionProfile: lá sẽ không translucent");
        int converted = 0, leaves = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { AssetRoot + "/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) continue;
            if (m.shader.name == "Standard")
            {
                var tex = m.mainTexture; var col = m.color;
                var cut = m.GetFloat("_Mode") > .5f; var cutoff = m.GetFloat("_Cutoff");
                m.shader = Shader.Find("HDRP/Lit");
                m.SetTexture("_BaseColorMap", tex);
                m.SetColor("_BaseColor", col);
                m.SetFloat("_AlphaCutoffEnable", cut ? 1f : 0f);
                m.SetFloat("_AlphaCutoff", Mathf.Min(cutoff, .4f));
                converted++;
            }
            if (m.shader.name != "HDRP/Lit") continue;
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            var isLeaf = m.GetFloat("_AlphaCutoffEnable") > .5f;
            if (isLeaf)
            {
                KeepCoverage(m.GetTexture("_BaseColorMap"), .4f);
                // màu gốc naraka hay bị nhân tối; lá cần sáng để ra sắc như ảnh tham chiếu
                var c = m.GetColor("_BaseColor");
                if (c.grayscale < .75f) m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", .22f);
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_DoubleSidedNormalMode", 1f);
                if (foliage)
                {
                    m.SetFloat("_MaterialID", 5f); // lit translucent
                    HDMaterial.SetDiffusionProfile(m, foliage);
                    m.SetFloat("_Thickness", .35f);
                }
                leaves++;
            }
            else m.SetFloat("_Smoothness", .12f);
            HDShaderUtils.ResetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
        }
        foreach (var path in AssetDatabase.FindAssets("t:TerrainLayer", new[] { AssetRoot + "/Layers" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            l.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
            l.smoothness = .05f; l.metallic = 0f;
            EditorUtility.SetDirty(l);
        }
        AssetDatabase.SaveAssets();
        log.Add($"Material: đổi {converted} sang HDRP/Lit, {leaves} material lá translucent");
    }

    // không giữ coverage thì mip xa trung bình alpha xuống dưới ngưỡng cắt, cây ở xa trơ cành
    static void KeepCoverage(Texture tex, float cutoff)
    {
        if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter imp) || imp.mipMapsPreserveCoverage) return;
        imp.mipMapsPreserveCoverage = true;
        imp.alphaTestReferenceValue = cutoff;
        imp.SaveAndReimport();
    }
    #endregion

    #region Noise and regions
    static float Fbm(float x, float y, int oct = 5)
    {
        float a = .5f, f = 1f, s = 0f, n = 0f;
        for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + i * 31.7f + 11.3f, y * f + i * 17.3f + 5.1f); n += a; a *= .5f; f *= 2f; }
        return Mathf.Clamp01((s / n - .25f) / .5f);
    }

    static float Ridge(float x, float y, int oct = 5)
    {
        float a = .5f, f = 1f, s = 0f, n = 0f;
        for (int i = 0; i < oct; i++) { s += a * (1f - Mathf.Abs(Mathf.PerlinNoise(x * f + i * 7.1f + 3.3f, y * f + i * 13.9f + 8.8f) * 2f - 1f)); n += a; a *= .5f; f *= 2.1f; }
        return Mathf.Pow(s / n, 2f);
    }

    // vùng kiểu voronoi: mỗi vùng là một khối rộng riêng, chỉ pha trộn trong dải biên hẹp (soft ~ độ rộng biên theo uv)
    static float[] Weights(float u, float v, float soft = .018f)
    {
        float wu = u + .06f * (Fbm(u * 4f + 40f, v * 4f) - .5f), wv = v + .06f * (Fbm(u * 4f, v * 4f + 40f) - .5f);
        var d = new float[RCount];
        for (int i = 0; i < RCount; i++) d[i] = float.MaxValue;
        foreach (var (r, c) in Seeds) d[(int)r] = Mathf.Min(d[(int)r], Vector2.Distance(new Vector2(wu, wv), c));
        var min = d.Min();
        var w = new float[RCount];
        float sum = 0f;
        for (int i = 0; i < RCount; i++) { w[i] = Mathf.Exp(-(d[i] - min) / soft); sum += w[i]; }
        for (int i = 0; i < RCount; i++) w[i] /= sum;
        return w;
    }

    static R Dominant(float[] w)
    {
        int best = 0; for (int i = 1; i < RCount; i++) if (w[i] > w[best]) best = i;
        return (R)best;
    }

    static float RegionHeight(R r, float u, float v)
    {
        switch (r)
        {
            case R.Snow: return 150f + 330f * Mathf.Pow(Ridge(u * 2.6f, v * 2.6f), .6f) + 30f * Fbm(u * 9f, v * 9f);
            case R.Pine: return 70f + 95f * Fbm(u * 3f + 1f, v * 3f + 7f);
            case R.Maple: return 48f + 55f * Fbm(u * 3.5f + 3f, v * 3.5f);
            case R.Canyon:
                {
                    var h = 85f + 70f * Fbm(u * 3f + 9f, v * 3f);
                    h = Mathf.Floor(h / 14f) * 14f + 3f * Fbm(u * 20f, v * 20f);
                    return h - 60f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.62f, .85f, Ridge(u * 5f + 2f, v * 5f)));
                }
            case R.Bamboo: return 40f + 55f * Fbm(u * 5f + 6f, v * 5f);
            case R.Pampas: return 25f + 9f * Fbm(u * 4f, v * 4f);
            case R.Lotus: return 20.4f + 3f * Fbm(u * 8f, v * 8f + 4f);
            case R.Flower: return 30f + 38f * Fbm(u * 3f + 12f, v * 3f);
            case R.Coast: return 22.5f + 6f * Fbm(u * 5f + 8f, v * 5f);
            default: throw new System.ArgumentOutOfRangeException(nameof(r), r, null);
        }
    }

    static float Land(float u, float v, float width = .045f)
    {
        float dx = u - .5f, dz = v - .5f, rr = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx);
        var R0 = .42f + .12f * (Fbm(Mathf.Cos(a) * 1.5f + 5f, Mathf.Sin(a) * 1.5f + 5f, 4) - .5f) + .03f * (Fbm(u * 12f, v * 12f) - .5f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((R0 - rr) / width));
    }
    #endregion

    #region Heights
    static Vector2 W(Vector2 uv) => uv * Size;

    static float SegDist(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        var ab = b - a;
        t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static void BuildRiver()
    {
        riverPts = new List<Vector2>();
        for (int i = 0; i < River.Length - 1; i++)
        {
            Vector2 a = W(River[i]), b = W(River[i + 1]), side = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (int k = 0; k < 8; k++)
            {
                var t = k / 8f; var g = (i + t) / (River.Length - 1);
                var amp = 50f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(g * 1.2f));
                riverPts.Add(Vector2.Lerp(a, b, t) + side * amp * Mathf.Sin(g * 23f + 1.3f) * (.6f + .4f * Fbm(g * 6f, 3f)));
            }
        }
        riverPts.Add(W(River[River.Length - 1]));
    }

    static float RiverDist(Vector2 p, out float along)
    {
        float best = float.MaxValue; along = 0f;
        for (int i = 0; i < riverPts.Count - 1; i++)
        {
            var d = SegDist(p, riverPts[i], riverPts[i + 1], out var t);
            if (d < best) { best = d; along = (i + t) / (riverPts.Count - 1); }
        }
        return best;
    }

    static float RiverWidth(float along) => 12f + 18f * along;

    static void BuildHeights()
    {
        BuildRiver();
        H = new float[HRes, HRes];
        for (int z = 0; z < HRes; z++)
            for (int x = 0; x < HRes; x++)
            {
                float u = x / (float)(HRes - 1), v = z / (float)(HRes - 1);
                // độ cao pha trộn rộng hơn màu/thực vật để biên vùng không thành vách
                var w = Weights(u, v, .05f);
                float h = 0f;
                for (int i = 0; i < RCount; i++) if (w[i] > .002f) h += w[i] * RegionHeight((R)i, u, v);
                var rough = 1f - w[(int)R.Lotus] - w[(int)R.Coast] - w[(int)R.Pampas];
                h += rough * (15f * Fbm(u * 2.2f + 70f, v * 2.2f) + 30f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, .9f, v)));
                h = Mathf.Lerp(26f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, Land(u, v, .17f))));
                var L = Land(u, v);
                h = Mathf.Lerp(3f + 8f * Fbm(u * 8f, v * 8f), h, L);
                var p = new Vector2(u, v) * Size;
                var rd = RiverDist(p, out var along);
                var rw = RiverWidth(along) * .5f;
                var bank = rw + Mathf.Min(Mathf.Max(0f, h - Sea) * 3f, 150f) + 12f;
                if (rd < bank && L > .2f) h = Mathf.Lerp(16.5f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rw, bank, rd)));
                var ld = Vector2.Distance(p, W(Lake));
                var ln = ld * (1f + .55f * (Fbm(u * 5f + 13f, v * 5f) - .5f) + .2f * (Fbm(u * 14f, v * 14f) - .5f));
                if (ln < LakeR * 1.6f && ld > IslandR) h = Mathf.Lerp(15f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LakeR * .7f, LakeR * 1.6f, ln)));
                if (ld <= IslandR + 8f) h = Mathf.Lerp(22.5f, Mathf.Min(h, 15f), Mathf.InverseLerp(IslandR, IslandR + 8f, ld));
                if (w[(int)R.Lotus] > .5f && ld > LakeR * 1.3f)
                {
                    var pn = Fbm(u * 16f + 20f, v * 16f);
                    if (pn > .62f) h = Mathf.Lerp(h, 18.4f, Mathf.InverseLerp(.62f, .72f, pn));
                }
                H[z, x] = h;
            }
        log.Add($"Heights: min {H.Cast<float>().Min():0.0} max {H.Cast<float>().Max():0.0} m");
    }

    static float HS(float wx, float wz)
    {
        float fx = Mathf.Clamp(wx / Size * (HRes - 1), 0, HRes - 1.001f), fz = Mathf.Clamp(wz / Size * (HRes - 1), 0, HRes - 1.001f);
        int x = (int)fx, z = (int)fz; float tx = fx - x, tz = fz - z;
        return Mathf.Lerp(Mathf.Lerp(H[z, x], H[z, x + 1], tx), Mathf.Lerp(H[z + 1, x], H[z + 1, x + 1], tx), tz);
    }

    static float Slope(float wx, float wz)
    {
        const float e = 2f;
        var dx = (HS(wx + e, wz) - HS(wx - e, wz)) / (2 * e);
        var dz = (HS(wx, wz + e) - HS(wx, wz - e)) / (2 * e);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
    }

    static void Flatten(Vector2 p, float r, float target, float edge)
    {
        int x0 = Mathf.Max(0, (int)((p.x - r - edge) / Size * (HRes - 1))), x1 = Mathf.Min(HRes - 1, (int)((p.x + r + edge) / Size * (HRes - 1)) + 1);
        int z0 = Mathf.Max(0, (int)((p.y - r - edge) / Size * (HRes - 1))), z1 = Mathf.Min(HRes - 1, (int)((p.y + r + edge) / Size * (HRes - 1)) + 1);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var d = Vector2.Distance(p, new Vector2(x, z) / (HRes - 1) * Size);
                if (d > r + edge) continue;
                H[z, x] = Mathf.Lerp(target, H[z, x], Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r + edge, d)));
            }
    }

    static (float mean, float range) FootStats(Vector2 p, float r)
    {
        float sum = HS(p.x, p.y), lo = sum, hi = sum;
        for (int i = 0; i < 8; i++)
        {
            var a = i * Mathf.PI / 4f;
            var h = HS(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r);
            sum += h; lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
        }
        return (sum / 9f, hi - lo);
    }
    #endregion

    #region Sites and roads
    class Piece { public Tpl t; public Vector2 p; public float yaw, scale; }

    static void BuildRoadLines()
    {
        roadLines = new List<List<Vector2>>();
        roadSegs = new List<(Vector2, Vector2)>();
        foreach (var (a, b) in Roads)
        {
            var pa = W(Sites[a].uv); var pb = W(Sites[b].uv);
            var pts = new List<Vector2> { pa };
            var n = Mathf.CeilToInt(Vector2.Distance(pa, pb) / 50f);
            var side = new Vector2(-(pb - pa).y, (pb - pa).x).normalized;
            for (int i = 1; i < n; i++)
            {
                var t = i / (float)n;
                pts.Add(Vector2.Lerp(pa, pb, t) + side * (Mathf.Sin(t * Mathf.PI) * 70f * (Fbm(a * 3.1f + t * 2f, b * 1.7f) - .5f) * 2f));
            }
            pts.Add(pb);
            roadLines.Add(pts);
            for (int i = 0; i < pts.Count - 1; i++) roadSegs.Add((pts[i], pts[i + 1]));
        }
    }

    static float RoadDist(Vector2 p)
    {
        var best = float.MaxValue;
        foreach (var (a, b) in roadSegs) best = Mathf.Min(best, SegDist(p, a, b, out _));
        return best;
    }

    // hướng lối vào: đoạn đường đầu tiên rời khỏi site, dùng để xoay điện chính và xếp hàng torii
    static Vector2 EntryDir(int site)
    {
        var c = W(Sites[site].uv);
        foreach (var line in roadLines)
        {
            if (Vector2.Distance(line[0], c) < 1f) return (line[1] - line[0]).normalized;
            if (Vector2.Distance(line[line.Count - 1], c) < 1f) return (line[line.Count - 2] - line[line.Count - 1]).normalized;
        }
        return Vector2.down;
    }

    static float YawTo(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

    static List<Piece> PlanSites()
    {
        var plan = new List<Piece>();
        for (int si = 0; si < Sites.Length; si++)
        {
            var site = Sites[si];
            var c = W(site.uv);
            var entry = EntryDir(si);
            var (plaza, plazaRange) = FootStats(c, site.r * .45f);
            var level = Mathf.Max(plaza, Sea + 2.5f);
            if (site.name == "Đền Sen") level = 22.5f;
            Flatten(c, site.r * .45f, level, site.r * .5f + plazaRange * 3f);
            blocked.Add((c, site.r + 6f));
            var used = new List<(Vector2 p, float r)> { (c, 5f) };
            foreach (var (cat, n) in site.kit)
            {
                if (!tpl.TryGetValue(cat, out var list) || list.Count == 0) { log.Add($"{site.name}: không có mẫu {cat}"); continue; }
                for (int i = 0; i < n; i++)
                {
                    var t = list[rng.Next(list.Count)];
                    if (cat == "statue") t = list.OrderByDescending(x => x.size.y).First();
                    if (cat == "pagoda" && site.kind == Kind.Temple && i == 0) t = list.OrderByDescending(x => x.size.y).First();
                    var sc = cat == "statue" && t.size.y > 30f ? 32f / t.size.y : 1f;
                    var fr = Mathf.Max(t.size.x, t.size.z) * .5f * sc;
                    Vector2 p = c; bool ok = false; float yaw = 0f;
                    var back = -entry; var right = new Vector2(entry.y, -entry.x);
                    for (int tries = 0; tries < 70 && !ok; tries++)
                    {
                        var j = new Vector2(R01 - .5f, R01 - .5f) * 6f;
                        switch (cat)
                        {
                            // trục đền: điện chính phía sau, tượng giữa sân, tháp hai bên, đèn đá dọc lối vào
                            case "mainhall": case "shrine": p = c + back * site.r * .35f; break;
                            case "statue": p = c + back * site.r * .02f + j; break;
                            case "pagoda": p = c + back * site.r * .2f + right * site.r * (i % 2 == 0 ? .55f : -.55f) + j; break;
                            case "toriiBlack": p = c + entry * site.r * .9f; break;
                            case "stonelantern":
                                p = site.kind == Kind.Village ? c + Rot(entry, R01 * 360f) * site.r * (.25f + .1f * R01)
                                    : c + entry * site.r * (.1f + .8f * (i / 2) / Mathf.Max(1f, n / 2f)) + right * (i % 2 == 0 ? 5f : -5f); break;
                            case "brazier": p = c + back * site.r * .1f + right * (i % 2 == 0 ? 9f : -9f) + entry * (i / 2) * 6f; break;
                            case "well": case "bell": p = c + Rot(entry, R01 * 360f) * site.r * (.12f + .15f * R01); break;
                            case "tower": case "gate": p = c + Rot(entry, R01 * 360f) * site.r * (.9f + .1f * R01); break;
                            default: p = c + Rot(entry, R01 * 360f) * site.r * (.45f + .5f * R01); break;
                        }
                        var maxRange = tries < 45 ? 4f : 9f;
                        var structural = cat == "mainhall" || cat == "shrine" || cat == "statue" || cat == "toriiBlack";
                        ok = structural || (HS(p.x, p.y) > Sea + 1.2f && RiverDist(p, out _) > fr + 12f && used.All(u => Vector2.Distance(u.p, p) > u.r + fr + 2f)
                             && (fr <= 2f || FootStats(p, fr).range < maxRange) && RoadDist(p) > fr * .6f + 2f);
                    }
                    if (!ok) continue;
                    used.Add((p, fr));
                    if (fr > 2f)
                    {
                        var (mean, range) = FootStats(p, fr);
                        Flatten(p, fr + 1.5f, Mathf.Max(site.name == "Đền Sen" ? 22.5f : mean, Sea + 1.5f), 6f + range * 3f);
                    }
                    var face = cat == "mainhall" || cat == "shrine" || cat == "statue" || cat == "pagoda" || cat == "toriiBlack" || cat == "stonelantern" && site.kind != Kind.Village
                        ? entry : (c - p).normalized;
                    yaw = YawTo(face.sqrMagnitude > .01f ? face : entry) + (cat == "house" ? R01 * 10f - 5f : 0f);
                    plan.Add(new Piece { t = t, p = p, yaw = yaw, scale = sc });
                }
            }
            if (site.torii) PlanToriiPath(si, plan);
        }
        return plan;
    }

    static Vector2 Rot(Vector2 v, float deg)
    {
        var r = deg * Mathf.Deg2Rad; float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // hàng torii dọc đường dẫn lên đền: cách nhau 9 m, từ mép sân ra ~110 m
    static void PlanToriiPath(int si, List<Piece> plan)
    {
        if (!tpl.TryGetValue("torii", out var list) || list.Count == 0) return;
        var c = W(Sites[si].uv);
        var line = roadLines.FirstOrDefault(l => Vector2.Distance(l[0], c) < 1f) ?? roadLines.Select(l => Enumerable.Reverse(l).ToList()).FirstOrDefault(l => Vector2.Distance(l[0], c) < 1f);
        if (line == null) return;
        var t = list.OrderBy(x => x.size.y).First();
        float walked = 0f, next = Sites[si].r * .75f;
        for (int i = 0; i < line.Count - 1 && next < Sites[si].r + 110f; i++)
        {
            var seg = line[i + 1] - line[i]; var len = seg.magnitude;
            while (walked + len >= next && next < Sites[si].r + 110f)
            {
                var p = line[i] + seg * ((next - walked) / len);
                if (HS(p.x, p.y) > Sea + 1f && RiverDist(p, out _) > 20f)
                    plan.Add(new Piece { t = t, p = p, yaw = YawTo(seg.normalized), scale = 1f });
                next += 9f;
            }
            walked += len;
        }
    }

    static void CarveRoads()
    {
        var copy = (float[,])H.Clone();
        for (int z = 2; z < HRes - 2; z++)
            for (int x = 2; x < HRes - 2; x++)
            {
                var p = new Vector2(x, z) / (HRes - 1) * Size;
                var d = RoadDist(p);
                if (d > 10f) continue;
                float s = 0; int c = 0;
                for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++) { s += copy[z + dz, x + dx]; c++; }
                var h = Mathf.Lerp(s / c, copy[z, x], Mathf.InverseLerp(3f, 10f, d));
                // đường đắp qua hồ/đầm (không đắp qua sông, ở đó có cầu)
                if (d < 3.5f && h < Sea + .8f && Vector2.Distance(p, W(Lake)) < LakeR * 1.8f && RiverDist(p, out var along) > RiverWidth(along) * .5f + 12f) h = Sea + .8f;
                H[z, x] = h;
            }
    }

    static void PlaceSites(List<Piece> plan)
    {
        foreach (var p in plan) Place("Sites", p.t, new Vector3(p.p.x, HS(p.p.x, p.p.y), p.p.y), p.yaw, p.scale, .02f);
        foreach (var site in Sites)
        {
            var c = W(site.uv);
            var marker = new GameObject($"{site.kind}: {site.name}").transform;
            marker.SetParent(Group("Sites"), false);
            marker.position = new Vector3(c.x, HS(c.x, c.y), c.y);
        }
    }

    static void PlaceBridges()
    {
        foreach (var (a, b) in roadSegs)
            for (int i = 0; i < riverPts.Count - 1; i++)
            {
                if (!Intersect(a, b, riverPts[i], riverPts[i + 1], out var p)) continue;
                var t = Pick("bridge"); if (t == null) return;
                RiverDist(p, out var along);
                var dir = (b - a).normalized;
                var yaw = YawTo(dir) + (t.size.x > t.size.z ? 90f : 0f);
                var sc = Mathf.Clamp((RiverWidth(along) + 16f) / Mathf.Max(t.size.x, t.size.z), .6f, 3.5f);
                Place("Sites", t, new Vector3(p.x, Sea - 3f, p.y), yaw, sc);
                blocked.Add((p, 12f));
            }
    }

    static bool Intersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 hit)
    {
        hit = default;
        var d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
        if (Mathf.Abs(d) < 1e-5f) return false;
        var t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        var s = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
        if (t < 0 || t > 1 || s < 0 || s > 1) return false;
        hit = p1 + (p2 - p1) * t; return true;
    }
    #endregion

    #region Terrain
    static Terrain MakeTerrain()
    {
        var td = new TerrainData { heightmapResolution = HRes };
        td.size = new Vector3(Size, MaxH, Size);
        var n = new float[HRes, HRes];
        for (int z = 0; z < HRes; z++) for (int x = 0; x < HRes; x++) n[z, x] = Mathf.Clamp01(H[z, x] / MaxH);
        td.SetHeights(0, 0, n);
        td.alphamapResolution = ARes;
        td.baseMapResolution = 1024;
        AssetDatabase.DeleteAsset($"{OutDir}/OpenWorld_Terrain.asset");
        AssetDatabase.CreateAsset(td, $"{OutDir}/OpenWorld_Terrain.asset");
        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(world, false);
        var t = go.GetComponent<Terrain>();
        t.basemapDistance = 1500f; t.heightmapPixelError = 4f; t.drawInstanced = true;
        return t;
    }

    static void Paint(TerrainData td)
    {
        td.terrainLayers = LayerNames.Select(n =>
        {
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{AssetRoot}/Layers/TL_{n}.terrainlayer");
            if (!l) throw new FileNotFoundException($"Thiếu terrain layer TL_{n} (chưa import NarakaWorld.unitypackage?)");
            return l;
        }).ToArray();
        var a = new float[ARes, ARes, LayerNames.Length];
        var m = new float[LayerNames.Length];
        for (int z = 0; z < ARes; z++)
            for (int x = 0; x < ARes; x++)
            {
                float u = (x + .5f) / ARes, v = (z + .5f) / ARes, wx = u * Size, wz = v * Size;
                var w = Weights(u, v);
                var h = HS(wx, wz);
                var s = Slope(wx, wz);
                var n1 = Fbm(u * 30f, v * 30f); var n2 = Fbm(u * 18f + 50f, v * 18f);
                System.Array.Clear(m, 0, m.Length);
                m[LGrass] += w[(int)R.Pine] * .5f + w[(int)R.Maple] * .6f + w[(int)R.Bamboo] * .6f + w[(int)R.Lotus] * .65f + w[(int)R.Flower] * (.9f + .1f * n1) + w[(int)R.Coast] * .6f + w[(int)R.Pampas] * (.35f + .3f * n1) + w[(int)R.Snow] * .15f;
                m[LDry] += w[(int)R.Pampas] * (.45f + .25f * n2) + w[(int)R.Maple] * .2f * n2 + w[(int)R.Canyon] * .2f + w[(int)R.Coast] * .1f;
                m[LSoil] += w[(int)R.Pine] * .45f + w[(int)R.Maple] * .15f + w[(int)R.Bamboo] * .35f + w[(int)R.Lotus] * .3f + w[(int)R.Flower] * .05f * n2;
                m[LSand] += w[(int)R.Coast] * .4f + w[(int)R.Canyon] * .45f;
                m[LRock] += w[(int)R.Snow] * .55f;
                m[LSnow] += w[(int)R.Snow] * .45f;
                m[LRed] += w[(int)R.Canyon] * .35f;
                var snow = Mathf.InverseLerp(230f, 280f, h + 25f * (n2 - .5f));
                var rock = Mathf.InverseLerp(26f, 40f, s);
                var beach = Mathf.InverseLerp(23.5f, 21f, h);
                var mud = Mathf.InverseLerp(21.8f, 20.2f, h) * (1 - w[(int)R.Coast]);
                var road = Mathf.InverseLerp(4.5f, 2.5f, RoadDist(new Vector2(wx, wz))) * (h > Sea + .5f ? 1 : 0);
                Add(m, LSnow, snow); Add(m, w[(int)R.Canyon] > .4f ? LRed : LRock, rock); Add(m, LRoad, road); Add(m, LSoil, mud); Add(m, LSand, beach);
                float sum = 0; for (int i = 0; i < m.Length; i++) sum += m[i];
                for (int i = 0; i < m.Length; i++) a[z, x, i] = m[i] / (sum + 1e-6f);
            }
        td.SetAlphamaps(0, 0, a);
    }

    static void Add(float[] m, int i, float amount)
    {
        if (amount <= 0) return;
        var sum = m.Sum();
        for (int k = 0; k < m.Length; k++) m[k] *= 1 - amount;
        m[i] += amount * Mathf.Max(sum, 1f);
    }
    #endregion

    #region Templates and placement
    static void LoadTemplates()
    {
        tpl = new Dictionary<string, List<Tpl>>();
        var prefabRoot = AssetRoot + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabRoot)) throw new DirectoryNotFoundException($"Thiếu {prefabRoot}: import NarakaWorld.unitypackage trước");
        foreach (var dir in AssetDatabase.GetSubFolders(prefabRoot))
            AddTemplates(Path.GetFileName(dir), AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath));
        foreach (var (cat, paths) in Native)
        {
            var missing = paths.Where(p => !File.Exists(p)).ToList();
            foreach (var p in missing) log.Add($"[{cat}] thiếu {p}");
            AddTemplates(cat, paths.Except(missing));
        }
        foreach (var kv in tpl.OrderBy(k => k.Key)) log.Add($"[{kv.Key}] {kv.Value.Count} mẫu: {string.Join(", ", kv.Value.Select(t => $"{t.name}({t.size.y:0.#}m)"))}");
    }

    static readonly HashSet<string> Vegetation = new HashSet<string> { "grass", "flower", "reed", "thatch", "shrub", "shrubBamboo", "lotus", "maple", "pine", "bamboo", "ficus" };

    static void AddTemplates(string cat, IEnumerable<string> paths)
    {
        if (!tpl.TryGetValue(cat, out var list)) tpl[cat] = list = new List<Tpl>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var plain = Vegetation.Contains(cat) && go.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Any(m => m && m.name.StartsWith("NW_plain"));
            // dây leo treo, măng trúc khối nón, cỏ material trắng: đặt dưới đất trông như lỗi
            var odd = Vegetation.Contains(cat) && System.Text.RegularExpressions.Regex.IsMatch(prefab.name, "vine|shrub_com_bamboo|__miss|grass_com_green");
            if (plain || odd) log.Add($"[{cat}] {prefab.name}: {(plain ? "material không texture" : "dạng không hợp đặt dưới đất")}, bỏ qua");
            else if (Bounds(go, out var b)) list.Add(new Tpl { prefab = prefab, size = b.size, offXZ = new Vector2(b.center.x, b.center.z), minY = b.min.y, name = prefab.name });
            else log.Add($"[{cat}] {prefab.name}: không có renderer, bỏ qua");
            Object.DestroyImmediate(go);
        }
    }

    static bool Bounds(GameObject go, out Bounds b)
    {
        b = default; var ok = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            var rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-6f || float.IsNaN(rb.size.x) || rb.size.magnitude > 3000f) continue;
            if (!ok) { b = rb; ok = true; } else b.Encapsulate(rb);
        }
        return ok;
    }

    static Transform Group(string name)
    {
        if (groups.TryGetValue(name, out var t) && t) return t;
        t = new GameObject(name).transform; t.SetParent(world, false);
        return groups[name] = t;
    }

    static float FootMin(float x, float z, float r)
    {
        var h = HS(x, z);
        for (int i = 0; i < 6; i++) { var a = i * Mathf.PI / 3f; h = Mathf.Min(h, HS(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r)); }
        return h;
    }

    static void Place(string group, Tpl t, Vector3 ground, float yaw, float scale, float sink = 0f)
    {
        if (group == "Rocks" || group == "Cliffs") ground.y = FootMin(ground.x, ground.z, Mathf.Min(t.size.x, t.size.z) * scale * .4f);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(t.prefab, Group(group));
        var rot = Quaternion.Euler(0f, yaw, 0f);
        go.transform.rotation = rot * t.prefab.transform.rotation;
        go.transform.localScale = t.prefab.transform.localScale * scale;
        var off = rot * new Vector3(t.offXZ.x, 0f, t.offXZ.y) * scale;
        go.transform.position = new Vector3(ground.x - off.x, ground.y - t.minY * scale - sink * t.size.y * scale, ground.z - off.z);
        counts[group] = counts.TryGetValue(group, out var c) ? c + 1 : 1;
    }

    static Tpl Pick(string cat) => tpl.TryGetValue(cat, out var l) && l.Count > 0 ? l[rng.Next(l.Count)] : null;
    static float R01 => (float)rng.NextDouble();
    #endregion

    #region Scatter
    static R PickRegion(float[] w)
    {
        var r = R01; for (int i = 0; i < RCount; i++) { r -= w[i]; if (r <= 0) return (R)i; }
        return Dominant(w);
    }

    // cây theo vùng: rừng dày ở thông / phong / trúc, đồng lau và đồi hoa để trống, chỉ cây lẻ làm điểm nhấn
    static readonly Dictionary<R, (string cat, float p)[]> TreeTable = new Dictionary<R, (string, float)[]>
    {
        { R.Snow, new[] { ("pine", .12f) } },
        { R.Pine, new[] { ("pine", .62f), ("ficus", .03f) } },
        { R.Maple, new[] { ("maple", .42f), ("sakura", .1f), ("pine", .03f) } },
        { R.Canyon, new[] { ("pine", .012f) } },
        { R.Bamboo, new[] { ("bamboo", .55f), ("bambooJP", .3f), ("maple", .01f) } },
        { R.Pampas, new[] { ("maple", .004f), ("sakura", .004f), ("ficus", .0015f) } },
        { R.Lotus, new[] { ("ficus", .035f), ("sakura", .03f), ("maple", .01f) } },
        { R.Flower, new[] { ("sakura", .025f), ("pine", .012f) } },
        { R.Coast, new[] { ("pine", .045f), ("sakura", .005f) } },
    };
    static readonly Dictionary<R, (string cat, float p)[]> ShrubTable = new Dictionary<R, (string, float)[]>
    {
        { R.Snow, new[] { ("shrub", .02f) } },
        { R.Pine, new[] { ("fern", .3f), ("berry", .12f), ("shrub", .1f) } },
        { R.Maple, new[] { ("shrub", .14f), ("fern", .06f), ("berry", .05f) } },
        { R.Canyon, new[] { ("thatch", .08f) } },
        { R.Bamboo, new[] { ("shrubBamboo", .25f), ("fern", .12f) } },
        { R.Pampas, new[] { ("shrub", .01f) } },
        { R.Lotus, new[] { ("reed", .3f), ("shrub", .04f) } },
        { R.Flower, new[] { ("shrub", .03f), ("berry", .02f) } },
        { R.Coast, new[] { ("shrub", .05f), ("thatch", .04f) } },
    };

    static bool Blocked(Vector2 p, float pad) => blocked.Any(b => Vector2.Distance(b.p, p) < b.r + pad);

    static void Scatter()
    {
        Layer("Trees", 9f, 8f, TreeTable, 32f, .85f, 1.3f, .03f, 2f);
        Layer("Shrubs", 5f, 4f, ShrubTable, 34f, .8f, 1.35f, .05f, -10f);
        for (float z = 0; z < Size; z += 5f)
            for (float x = 0; x < Size; x += 5f)
            {
                float px = x + R01 * 4f, pz = z + R01 * 4f, h = HS(px, pz);
                if (h > Sea - .3f || h < Sea - 3f) continue;
                if (Weights(px / Size, pz / Size)[(int)R.Lotus] < .4f || Land(px / Size, pz / Size) < .95f || R01 > .35f) continue;
                var t = Pick("lotus"); if (t == null) break;
                Place("Shrubs", t, new Vector3(px, Sea + .02f, pz), R01 * 360f, .9f + R01 * .5f);
            }
        for (float z = 0; z < Size; z += 20f)
            for (float x = 0; x < Size; x += 20f)
            {
                float px = x + R01 * 16f, pz = z + R01 * 16f, h = HS(px, pz);
                if (h < Sea + .8f) continue;
                var p = new Vector2(px, pz);
                if (Blocked(p, 4f) || RoadDist(p) < 7f) continue;
                var s = Slope(px, pz); var r = PickRegion(Weights(px / Size, pz / Size));
                var prob = .03f + (r == R.Canyon || r == R.Snow ? .12f : 0f) + (r == R.Pine ? .06f : 0f) + (s > 18f ? .18f : 0f) + (r == R.Coast && h < 25f ? .1f : 0f) - (r == R.Pampas || r == R.Flower ? .025f : 0f);
                if (R01 > prob) continue;
                var t = r == R.Pine || r == R.Bamboo ? Pick(R01 < .5f ? "erock" : "rock") : Pick("rock");
                if (t == null) continue;
                Place("Rocks", t, new Vector3(px, h, pz), R01 * 360f, (.8f + R01 * 1.4f) * (r == R.Canyon || r == R.Snow ? 1.6f : 1f), .3f);
            }
        for (float z = 0; z < Size; z += 40f)
            for (float x = 0; x < Size; x += 40f)
            {
                float px = x + R01 * 32f, pz = z + R01 * 32f, h = HS(px, pz);
                if (h < Sea + 2f) continue;
                var p = new Vector2(px, pz);
                if (Blocked(p, 20f) || RoadDist(p) < 18f || RiverDist(p, out _) < 25f) continue;
                var s = Slope(px, pz); var w = Weights(px / Size, pz / Size);
                var prob = (s > 30f ? .55f : s > 20f ? .2f : 0f) + w[(int)R.Canyon] * (s > 12f ? .4f : 0f) + (h > 220f ? .25f : 0f);
                if (R01 > prob) continue;
                var t = Pick("cliff"); if (t == null) break;
                var target = Mathf.Lerp(18f, 55f, R01) * (h > 220f ? 1.6f : 1f);
                Place("Cliffs", t, new Vector3(px, h, pz), R01 * 360f, Mathf.Clamp(target / Mathf.Max(t.size.x, t.size.z), .4f, 4f), .35f);
            }
        foreach (var kv in counts.OrderBy(k => k.Key)) log.Add($"{kv.Key}: {kv.Value}");
    }

    static void Layer(string group, float step, float roadPad, Dictionary<R, (string cat, float p)[]> table, float maxSlope, float smin, float smax, float sink, float sitePad)
    {
        for (float z = 0; z < Size; z += step)
            for (float x = 0; x < Size; x += step)
            {
                float px = x + R01 * step * .9f, pz = z + R01 * step * .9f, h = HS(px, pz);
                if (h < Sea + 1.2f) continue;
                var p = new Vector2(px, pz);
                if (Slope(px, pz) > maxSlope || RoadDist(p) < roadPad || Blocked(p, sitePad)) continue;
                var r = PickRegion(Weights(px / Size, pz / Size));
                if (r == R.Snow && h > 260f) continue;
                // nhiễu tần thấp: rừng thành mảng dày xen trảng trống
                var cluster = Mathf.Lerp(.25f, 1.7f, Fbm(px * .005f + (int)r * 3f, pz * .005f));
                var roll = R01;
                foreach (var (cat, prob) in table[r])
                {
                    if ((roll -= prob * cluster) > 0) continue;
                    var t = Pick(cat); if (t == null) break;
                    Place(group, t, new Vector3(px, h, pz), R01 * 360f, Mathf.Lerp(smin, smax, R01), sink);
                    break;
                }
            }
    }
    #endregion

    #region Terrain details
    // cỏ, lau, hoa dùng terrain detail (instanced) để phủ dày ở xa không tốn GameObject
    static readonly (string cat, string label)[] DetailCats = { ("grass", "grass"), ("flower", "flower"), ("reed", "reed"), ("thatch", "thatch") };

    static void Details(Terrain terrain)
    {
        AssetDatabase.DeleteAsset(DetailDir);
        Directory.CreateDirectory(DetailDir);
        AssetDatabase.Refresh();
        var protos = new List<DetailPrototype>();
        var layerOf = new List<string>();
        foreach (var (cat, label) in DetailCats)
        {
            if (!tpl.TryGetValue(cat, out var list)) continue;
            foreach (var t in list)
            {
                var src = (GameObject)PrefabUtility.InstantiatePrefab(t.prefab);
                var mr = src.GetComponentsInChildren<MeshRenderer>().OrderByDescending(r => r.name.ToLowerInvariant().Contains("lod0")).FirstOrDefault(r => r.GetComponent<MeshFilter>()?.sharedMesh);
                // lá không cắt alpha hoặc không có texture sẽ thành ô trắng/đen
                var bad = !mr || mr.sharedMaterials.Any(mt => !mt || mt.shader.name != "HDRP/Lit" || !mt.GetTexture("_BaseColorMap") || mt.GetFloat("_AlphaCutoffEnable") < .5f);
                if (bad) { log.Add($"Detail bỏ {t.name}: material không cắt alpha / thiếu texture"); Object.DestroyImmediate(src); continue; }
                var go = new GameObject("D_" + t.name);
                go.AddComponent<MeshFilter>().sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                var mats = mr.sharedMaterials;
                foreach (var mat in mats) if (mat) { mat.enableInstancing = true; EditorUtility.SetDirty(mat); }
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                go.transform.localScale = mr.transform.lossyScale;
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{DetailDir}/D_{t.name}.prefab");
                Object.DestroyImmediate(go); Object.DestroyImmediate(src);
                var proto = new DetailPrototype
                {
                    prototype = prefab, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = .85f, maxWidth = 1.35f, minHeight = .8f, maxHeight = cat == "reed" ? 1.8f : 1.4f, noiseSpread = .4f, alignToGround = .6f,
                };
                if (!proto.Validate(out var msg)) { log.Add($"Detail {t.name} không hợp lệ: {msg}"); continue; }
                protos.Add(proto); layerOf.Add(label);
            }
        }
        var td = terrain.terrainData;
        td.SetDetailResolution(DRes, 32);
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        td.detailPrototypes = protos.ToArray();
        var maps = protos.Select(_ => new int[DRes, DRes]).ToArray();
        var byLabel = layerOf.Select((l, i) => (l, i)).GroupBy(x => x.l).ToDictionary(g => g.Key, g => g.Select(x => x.i).ToArray());
        long total = 0;
        for (int z = 0; z < DRes; z++)
            for (int x = 0; x < DRes; x++)
            {
                float u = (x + .5f) / DRes, v = (z + .5f) / DRes, wx = u * Size, wz = v * Size, h = HS(wx, wz);
                if (h < Sea + .8f || Slope(wx, wz) > 28f) continue;
                var p = new Vector2(wx, wz);
                if (RoadDist(p) < 3.5f || Blocked(p, -12f)) continue;
                var w = Weights(u, v);
                var patch = Fbm(u * 40f + 3f, v * 40f);
                var patch2 = Fbm(u * 25f, v * 25f + 9f);
                float grass = 0, flower = 0, reed = 0, thatch = 0;
                grass += w[(int)R.Pampas] * 3f + w[(int)R.Flower] * 3f + w[(int)R.Maple] * 2f + w[(int)R.Lotus] * 2f + w[(int)R.Coast] * 2f + w[(int)R.Pine] * 1f + w[(int)R.Bamboo] * 1f;
                flower += w[(int)R.Flower] * 6f * patch + w[(int)R.Maple] * 1f * patch + w[(int)R.Pampas] * .6f * patch2;
                reed += w[(int)R.Pampas] * 5f * patch2 + w[(int)R.Lotus] * (h < Sea + 3f ? 4f : 1f);
                thatch += w[(int)R.Pampas] * 1.5f + w[(int)R.Canyon] * .6f + w[(int)R.Coast] * .5f;
                if (h > 240f) { grass *= .2f; flower = 0; }
                total += Put(maps, byLabel, "grass", grass) + Put(maps, byLabel, "flower", flower) + Put(maps, byLabel, "reed", reed) + Put(maps, byLabel, "thatch", thatch);

                int Put(int[][,] mm, Dictionary<string, int[]> idx, string label, float amount)
                {
                    if (!idx.TryGetValue(label, out var ids) || amount <= 0f) return 0;
                    var count = Mathf.FloorToInt(amount * .7f + R01);
                    if (count <= 0) return 0;
                    mm[ids[rng.Next(ids.Length)]][z, x] += count;
                    return count;
                }
            }
        for (int i = 0; i < maps.Length; i++) td.SetDetailLayer(0, 0, i, maps[i]);
        // 4 probe realtime + 1,2 triệu cụm cỏ từng làm gpu treo (device removed) khi vào play mode: giữ detail vừa phải
        terrain.detailObjectDistance = 150f;
        terrain.detailObjectDensity = 1f;
        log.Add($"Detail: {protos.Count} mẫu, {total} cụm cỏ/lau/hoa");
    }
    #endregion

    #region Player
    const string SetupScene = "Assets/Sence/SetUp.unity";

    // nhân bản cụm Character + rig camera (Player_H 1) từ SetUp như Ctrl+D để tham chiếu chéo được nối lại; SetUp không bị lưu
    static void AddPlayer(UnityEngine.SceneManagement.Scene worldScene, List<Shot> shots)
    {
        var setup = EditorSceneManager.OpenScene(SetupScene, OpenSceneMode.Additive);
        var roots = setup.GetRootGameObjects().Where(g => g.GetComponentInChildren<CharController>(true) || g.GetComponentInChildren<CameraController>(true)
            || g.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)).ToArray();
        if (!roots.Any(g => g.GetComponentInChildren<CharController>(true)) || !roots.Any(g => g.GetComponentInChildren<CameraController>(true)))
            throw new MissingComponentException($"{SetupScene} thiếu CharController hoặc CameraController (rig camera mất prefab?)");
        Selection.objects = roots;
        Unsupported.DuplicateGameObjectsUsingPasteboard();
        var copies = Selection.gameObjects;
        if (copies.Length != roots.Length) throw new System.InvalidOperationException($"Nhân bản player từ {SetupScene} thất bại ({copies.Length}/{roots.Length})");
        foreach (var go in copies) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, worldScene);
        Selection.objects = new Object[0];
        EditorSceneManager.CloseScene(setup, true);

        var c = W(Sites[0].uv);
        var spawn = new Vector3(c.x, HS(c.x, c.y) + .1f, c.y - 4f);
        var player = copies.Select(g => g.GetComponentInChildren<CharController>(true)).First(x => x);
        var delta = spawn - player.transform.position;
        foreach (var go in copies)
            if (!go.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)) go.transform.position += delta;
        log.Add($"Player: {string.Join(", ", copies.Select(g => g.name))} -> spawn {spawn}");

        foreach (var (name, offset) in new[] { ("player_front", new Vector3(-4f, 2.2f, 5f)), ("player_sunside", new Vector3(-6f, 2.4f, -3f)) })
            shots.Add(Look(name, spawn + offset, spawn + Vector3.up * 1.2f));
    }
    #endregion

    #region Water, light, snapshots
    static void Water()
    {
        var matPath = $"{OutDir}/M_Sea.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetColor("_BaseColor", new Color(.06f, .2f, .22f));
        mat.SetFloat("_Smoothness", .93f);
        mat.SetFloat("_Metallic", 0f);
        HDShaderUtils.ResetMaterialKeywords(mat);
        EditorUtility.SetDirty(mat);
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Sea";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(world, false);
        go.transform.position = new Vector3(Size * .5f, Sea, Size * .5f);
        go.transform.localScale = new Vector3(Size * 2.5f / 10f, 1f, Size * 2.5f / 10f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // giờ vàng: nắng thấp ấm chiếu xiên, trời chuyển xanh-cam, sương thể tích cho tia nắng xuyên rừng, đèn fill lạnh để mặt khuất và nhân vật không đen kịt
    static Camera Lighting()
    {
        var profilePath = $"{OutDir}/VP_OpenWorld.asset";
        AssetDatabase.DeleteAsset(profilePath);
        if (!AssetDatabase.CopyAsset("Assets/FeudalMap/FeudalMap_Volume.asset", profilePath)) throw new FileNotFoundException("Thiếu Assets/FeudalMap/FeudalMap_Volume.asset");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile.TryGet<GradientSky>(out var sky))
        {
            sky.top.Override(new Color(.48f, .62f, .8f));
            sky.middle.Override(new Color(1f, .9f, .76f));
            sky.bottom.Override(new Color(.55f, .5f, .44f));
            sky.gradientDiffusion.Override(1.4f);
            sky.multiplier.Override(1.45f);
        }
        else log.Add("Volume không có GradientSky");
        // volume gốc của FeudalMap lấy ambient từ lighting bake (StaticLightingSky); map này không bake nên phải lấy ambient động từ sky, nếu không mặt khuất nắng đen kịt
        Get<VisualEnvironment>(profile).skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        var fog = Get<Fog>(profile);
        fog.enabled.Override(true);
        fog.meanFreePath.Override(2000f);
        fog.baseHeight.Override(20f);
        fog.maximumHeight.Override(120f);
        fog.albedo.Override(new Color(1f, .96f, .9f));
        fog.enableVolumetricFog.Override(true);
        fog.anisotropy.Override(.65f);
        Get<HDShadowSettings>(profile).maxShadowDistance.Override(900f);
        Get<IndirectLightingController>(profile).indirectDiffuseLightingMultiplier.Override(2.1f);
        // phơi sáng sáng hơn bản FeudalMap, bỏ hạt film và giảm bloom cho hình trong
        var exposure = Get<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(-.25f);
        Get<FilmGrain>(profile).intensity.Override(0f);
        Get<Bloom>(profile).intensity.Override(.08f);
        var grade = Get<ColorAdjustments>(profile);
        grade.postExposure.Override(0f);
        grade.saturation.Override(12f);
        grade.contrast.Override(14f);
        Get<WhiteBalance>(profile).temperature.Override(4f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.transform.SetParent(world, false);
        vol.isGlobal = true; vol.sharedProfile = profile;

        var sun = MakeLight("Sun", Quaternion.Euler(30f, 62f, 0f), 30f, new Color(1f, .9f, .78f), true);
        sun.GetComponent<HDAdditionalLightData>().volumetricDimmer = 1.5f;
        MakeLight("Fill", Quaternion.Euler(40f, 235f, 0f), 10f, new Color(.75f, .8f, .9f), false);

        var cam = new GameObject("WorldCamera").AddComponent<Camera>();
        cam.gameObject.AddComponent<HDAdditionalCameraData>();
        cam.farClipPlane = 6000f; cam.nearClipPlane = .3f;
        return cam;
    }

    static Light MakeLight(string name, Quaternion rot, float lux, Color color, bool shadows)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.gameObject.AddComponent<HDAdditionalLightData>();
        l.transform.SetParent(world, false);
        l.type = LightType.Directional;
        l.lightUnit = LightUnit.Lux;
        l.intensity = lux;
        l.color = color;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        l.transform.rotation = rot;
        return l;
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet<T>(out var comp)) { comp.active = true; return comp; }
        comp = p.Add<T>(true);
        comp.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(comp, p);
        return comp;
    }

    class Shot { public string file; public Vector3 pos; public Quaternion rot; public bool ortho; public int w = 1600, h = 900; }

    static Shot Look(string name, Vector3 pos, Vector3 target) => new Shot { file = $"{Check}/world_{name}.jpg", pos = pos, rot = Quaternion.LookRotation(target - pos) };

    static List<Shot> Snapshots()
    {
        var list = new List<Shot>
        {
            new Shot { file = Check + "/world_top.jpg", pos = new Vector3(Size * .5f, 1500f, Size * .5f), rot = Quaternion.Euler(90f, 0f, 0f), ortho = true, w = 2048, h = 2048 },
            new Shot { file = Check + "/world_oblique.jpg", pos = new Vector3(Size * .5f, 900f, -450f), rot = Quaternion.Euler(38f, 0f, 0f), w = 1920, h = 1080 },
        };
        // camera nhìn về hướng bắc / đông bắc để nắng chiếu xiên từ trái
        var shots = new (string name, Vector2 uv, float dist, float up, float yaw)[]
        {
            ("village", Sites[0].uv, 200, 80, 330), ("fishing_village", Sites[1].uv, 200, 80, 20), ("snow_temple", Sites[3].uv, 230, 90, 10),
            ("maple_shrine", Sites[4].uv, 170, 60, 340), ("lotus_lake", Lake, 260, 70, 20), ("sea_shrine", Sites[6].uv, 160, 50, 30),
            ("pampas", new Vector2(.52f, .42f), 260, 60, 10), ("flower_hills", new Vector2(.30f, .25f), 220, 60, 20), ("pine_forest", new Vector2(.20f, .68f), 200, 70, 20),
            ("bamboo", new Vector2(.84f, .40f), 180, 60, 330), ("canyon", new Vector2(.80f, .67f), 300, 140, 20), ("snow", new Vector2(.50f, .84f), 520, 160, 10),
        };
        foreach (var s in shots)
        {
            var c = W(s.uv); var target = new Vector3(c.x, HS(c.x, c.y) + 8f, c.y);
            var p = target + Quaternion.Euler(0, s.yaw, 0) * Vector3.back * s.dist;
            p.y = Mathf.Max(HS(p.x, p.z) + 25f, target.y + s.up);
            list.Add(Look(s.name, p, target));
        }
        var eye = new (string name, Vector2 uv, float yaw)[]
        {
            ("eye_pampas", new Vector2(.50f, .40f), 20f), ("eye_flower", new Vector2(.31f, .26f), 30f), ("eye_maple", new Vector2(.55f, .64f), 10f),
            ("eye_pine", new Vector2(.18f, .66f), 40f), ("eye_bamboo", new Vector2(.84f, .44f), 0f), ("eye_temple", Sites[3].uv + new Vector2(0f, -.06f), 0f),
        };
        foreach (var (name, uv, yaw) in eye)
        {
            var c = W(uv);
            list.Add(new Shot { file = $"{Check}/world_{name}.jpg", pos = new Vector3(c.x, HS(c.x, c.y) + 1.8f, c.y), rot = Quaternion.Euler(2f, yaw, 0f) });
        }
        return list;
    }

    // trong editor: render qua nhiều khung thật để hdrp kịp cập nhật ambient probe của sky; render đồng bộ một khung cho mặt khuất nắng đen kịt
    static void CaptureLive(List<Shot> shots)
    {
        var go = new GameObject("_WorldShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        go.AddComponent<HDAdditionalCameraData>();
        cam.farClipPlane = 6000f; cam.nearClipPlane = .3f;
        int index = 0, frame = 0;
        RenderTexture rt = null;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!go || index >= shots.Count)
            {
                EditorApplication.update -= tick;
                if (rt) rt.Release();
                if (go) Object.DestroyImmediate(go);
                File.WriteAllText(Check + "/capture_done.txt", System.DateTime.Now.ToString("s"));
                return;
            }
            var s = shots[index];
            if (frame == 0)
            {
                if (rt) rt.Release();
                rt = new RenderTexture(s.w, s.h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.orthographic = s.ortho; cam.orthographicSize = Size * .5f; cam.fieldOfView = 50f;
                cam.transform.SetPositionAndRotation(s.pos, s.rot);
            }
            cam.Render();
            if (++frame < (index == 0 ? 120 : 40)) return;
            RenderTexture.active = rt;
            var tex = new Texture2D(s.w, s.h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0); tex.Apply();
            File.WriteAllBytes(s.file, tex.EncodeToJPG(85));
            RenderTexture.active = null; Object.DestroyImmediate(tex);
            index++; frame = 0;
        };
        File.Delete(Check + "/capture_done.txt");
        EditorApplication.update += tick;
    }

    static void WriteMapData()
    {
        var lines = new List<string> { "kind|name|u|v" };
        for (int i = 0; i < RCount; i++)
        {
            var seeds = Seeds.Where(s => (int)s.r == i).Select(s => s.c).ToList();
            var mid = seeds.Aggregate(Vector2.zero, (a, b) => a + b) / seeds.Count;
            lines.Add($"region|{RegionVi[i]}|{mid.x}|{mid.y}");
        }
        foreach (var s in Sites) lines.Add($"{s.kind.ToString().ToLowerInvariant()}|{s.name}|{s.uv.x}|{s.uv.y}");
        foreach (var p in riverPts) lines.Add($"river||{p.x / Size}|{p.y / Size}");
        foreach (var (a, b) in roadSegs) lines.Add($"road||{a.x / Size}|{a.y / Size}|{b.x / Size}|{b.y / Size}");
        File.WriteAllLines(Check + "/world_map.txt", lines);
    }
    #endregion
}
