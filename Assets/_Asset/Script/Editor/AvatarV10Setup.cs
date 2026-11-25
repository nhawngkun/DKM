using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// dựng nhân vật AvatarV10 (xuất từ Blender): vật liệu HDRP/Lit, rig Humanoid, scene xem thử
public static class AvatarV10Setup
{
    const string Root = "Assets/_Asset/3D/AvatarV10";
    const string FbxPath = Root + "/AvatarV10.fbx";
    const string ManifestPath = Root + "/AvatarV10_materials.json";
    const string MatDir = Root + "/Materials";
    const string ScenePath = "Assets/Sence/AvatarV10.unity";
    const float EmissiveNits = 100f;

    [Serializable]
    class MatEntry
    {
        public string name;
        public float[] color;
        public float metallic;
        public float smoothness;
        public bool alphaClip;
        public bool doubleSided;
        public string baseMap;
        public string maskMap;
        public string normalMap;
        public float normalScale = 1f;
        public string emissionMap;
        public float emissionIntensity;
        public string variantOf;
    }

    [Serializable]
    class Manifest
    {
        public MatEntry[] items;
    }

    [MenuItem("Tools/Avatar/Setup AvatarV10 Scene")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        if (manifest?.items == null || manifest.items.Length == 0)
            throw new InvalidOperationException($"manifest rỗng: {ManifestPath}");

        ConfigureTextures(manifest);
        ConfigureModel(manifest);
        CreateScene();
        Debug.Log("AVATAR_SETUP_OK");
    }

    // chỉ dựng texture + vật liệu (cả biến thể màu), không đụng tới scene
    [MenuItem("Tools/Avatar/Build AvatarV10 Materials")]
    public static void BuildMaterials()
    {
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        if (manifest?.items == null || manifest.items.Length == 0)
            throw new InvalidOperationException($"manifest rỗng: {ManifestPath}");
        ConfigureTextures(manifest);
        ConfigureModel(manifest);
        AssetDatabase.SaveAssets();
        Debug.Log("AVATAR_MATERIALS_OK");
    }

    #region Textures

    static void ConfigureTextures(Manifest manifest)
    {
        foreach (var e in manifest.items)
        {
            SetTexture(e.baseMap, TextureImporterType.Default, true);
            SetTexture(e.maskMap, TextureImporterType.Default, false);
            SetTexture(e.normalMap, TextureImporterType.NormalMap, false);
            SetTexture(e.emissionMap, TextureImporterType.Default, true);
        }
    }

    static void SetTexture(string rel, TextureImporterType type, bool srgb)
    {
        if (string.IsNullOrEmpty(rel)) return;
        var path = $"{Root}/{rel}";
        if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
        {
            Debug.LogWarning($"thiếu texture: {path}");
            return;
        }
        ti.textureType = type;
        ti.sRGBTexture = srgb;
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
    }

    static Texture2D Tex(string rel) =>
        string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{rel}");

    #endregion

    #region Materials + Model

    static void ConfigureModel(Manifest manifest)
    {
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null) throw new InvalidOperationException("không tìm thấy shader HDRP/Lit");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");

        var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        if (importer == null) throw new InvalidOperationException($"không tìm thấy {FbxPath}");

        // biến thể màu (variantOf) chỉ tạo vật liệu để đổi tay, không remap vào model
        foreach (var e in manifest.items)
        {
            var mat = BuildMaterial(shader, e);
            if (string.IsNullOrEmpty(e.variantOf))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), e.name), mat);
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importBlendShapes = true;
        importer.importAnimation = false;
        importer.SaveAndReimport();

        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FbxPath);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            Debug.LogError($"AVATAR_INVALID: avatar Humanoid của {FbxPath} không hợp lệ");
        else
            Debug.Log("AVATAR_HUMANOID_OK");
    }

    static Material BuildMaterial(Shader shader, MatEntry e)
    {
        var path = $"{MatDir}/{e.name.Replace('.', '_')}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;

        var c = e.color is { Length: >= 3 } ? new Color(e.color[0], e.color[1], e.color[2], 1f) : Color.white;
        mat.SetColor("_BaseColor", c);
        mat.SetTexture("_BaseColorMap", Tex(e.baseMap));
        mat.SetFloat("_Metallic", e.metallic);
        mat.SetFloat("_Smoothness", e.smoothness);

        var mask = Tex(e.maskMap);
        mat.SetTexture("_MaskMap", mask);
        if (mask != null)
        {
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", 1f);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
        }

        var normal = Tex(e.normalMap);
        mat.SetTexture("_NormalMap", normal);
        if (normal != null) mat.SetFloat("_NormalScale", e.normalScale);

        var emissive = Tex(e.emissionMap);
        mat.SetTexture("_EmissiveColorMap", emissive);
        mat.SetColor("_EmissiveColor", emissive != null ? Color.white * EmissiveNits : Color.black);

        mat.SetFloat("_AlphaCutoffEnable", e.alphaClip ? 1f : 0f);
        mat.SetFloat("_AlphaCutoff", 0.5f);
        mat.SetFloat("_DoubleSidedEnable", e.doubleSided ? 1f : 0f);

        // dựng lại keyword/pass HDRP theo property vừa set (alpha clip, double sided, emission)
        if (!HDMaterial.ValidateMaterial(mat))
            Debug.LogError($"HDMaterial.ValidateMaterial thất bại: {path}");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion

    #region Scene

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var avatar = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        avatar.name = "AvatarV10";
        avatar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var cam = Camera.main;
        if (cam != null)
        {
            var anim = avatar.GetComponent<Animator>();
            var lh = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var rh = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var forward = lh != null && rh != null
                ? Vector3.Cross(rh.position - lh.position, Vector3.up).normalized
                : Vector3.forward;
            var target = new Vector3(0f, 1.1f, 0f);
            cam.transform.position = target + forward * 3.2f + Vector3.up * 0.2f;
            cam.transform.LookAt(target);
            Debug.Log($"AVATAR_FORWARD {forward}");

            // đèn mặc định chiếu từ sau lưng → quay về chiếu xiên vào mặt trước
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                var side = Vector3.Cross(Vector3.up, forward);
                sun.transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.8f + side * 0.5f);
            }
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"AVATAR_SCENE_SAVED {ScenePath}");

        var shot = Environment.GetEnvironmentVariable("AVATAR_SHOT");
        if (cam != null && !string.IsNullOrEmpty(shot)) Capture(cam, shot);

        var variantShot = Environment.GetEnvironmentVariable("AVATAR_VARIANT_SHOT");
        if (cam != null && !string.IsNullOrEmpty(variantShot)) CaptureVariants(cam, avatar, variantShot);
    }

    // ảnh ghép: mỗi ô là một màu áo + giày cùng tên (nếu có); chỉ đổi trên instance, scene đã lưu trước đó
    static void CaptureVariants(Camera cam, GameObject avatar, string file)
    {
        string[] colors = { "Original", "Black", "Brown", "Burgundy", "Cream", "Navy", "Olive", "Tan", "White", "Grey" };
        var jacket = avatar.transform.Find("Body_Jacket")?.GetComponent<Renderer>();
        var boot = avatar.transform.Find("Body_Boot")?.GetComponent<Renderer>();
        if (jacket == null || boot == null)
        {
            Debug.LogError("không tìm thấy Body_Jacket/Body_Boot để chụp biến thể");
            return;
        }
        const int w = 320, h = 540, cols = 5;
        var sheet = new Texture2D(w * cols, h * 2, TextureFormat.RGB24, false);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tile = new Texture2D(w, h, TextureFormat.RGB24, false);
        var oldFov = cam.fieldOfView;
        cam.fieldOfView = 45f;
        cam.targetTexture = rt;
        for (var i = 0; i < colors.Length; i++)
        {
            var jm = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Jacket_{colors[i]}.mat");
            var bm = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Boot_{colors[i]}.mat");
            if (jm != null) jacket.sharedMaterial = jm;
            if (bm != null) boot.sharedMaterial = bm;
            for (var f = 0; f < 6; f++) cam.Render();
            RenderTexture.active = rt;
            tile.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tile.Apply();
            sheet.SetPixels((i % cols) * w, (1 - i / cols) * h, w, h, tile.GetPixels());
            Debug.Log($"AVATAR_VARIANT {colors[i]} jacket={(jm != null ? jm.name : "-")} boot={(bm != null ? bm.name : "-")}");
        }
        sheet.Apply();
        File.WriteAllBytes(file, sheet.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        cam.fieldOfView = oldFov;
        UnityEngine.Object.DestroyImmediate(sheet);
        UnityEngine.Object.DestroyImmediate(tile);
        rt.Release();
        Debug.Log($"AVATAR_VARIANT_SHOT {file}");
    }

    static void Capture(Camera cam, string file)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        // hdrp cần vài frame để exposure/history ổn định
        for (var i = 0; i < 8; i++) cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        Debug.Log($"AVATAR_SHOT {file}");
    }

    #endregion
}
