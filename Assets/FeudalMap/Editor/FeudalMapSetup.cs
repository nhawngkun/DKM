using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeudalMap.EditorTools
{
    public static class FeudalMapSetup
    {
        // blender light/emission units map 1:1 to hdrp lumen/nits; exposure matches blender's +0.5 ev view
        const float LumenPerBlenderWatt = 1f;
        const float NitsPerEmissionStrength = 1f;
        const float FixedExposureEV100 = -0.75f;
        const float MoonLux = 0.6f;
        const float SkyMultiplier = 0.22f;
        const float ShadowedLightMinWatts = 150f;

        static readonly string[] NoColliderPrefixes = { "Sakura", "Pine", "Portal", "Beam", "Seal", "Waterfall", "PL_" };
        static readonly float[] TwoLevelLods = { 0.2f, 0.01f };
        static readonly float[] ThreeLevelLods = { 0.3f, 0.1f, 0.01f };
        const string InstancePrefix = "INST_";

#pragma warning disable 0649
        [Serializable]
        class MatSpec
        {
            public string name, baseMap, normalMap, maskMap;
            public float normalStrength, metallic, smoothness, alpha, emissionStrength;
            public bool cutout;
            public float[] color, emission;
        }

        [Serializable]
        class MatSpecList
        {
            public MatSpec[] items;
        }
#pragma warning restore 0649

        #region Build
        [MenuItem("Tools/Feudal Map/Build HDRP Scene")]
        public static void Build()
        {
            var root = FindRoot();
            var materials = BuildMaterials(root);
            var models = AssetDatabase.FindAssets("t:Model", new[] { $"{root}/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();
            RemapModels(models, materials);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabs = BuildPrefabs(root, models.Where(IsPrefabModel));
            BuildScene(root, models.Where(p => !IsPrefabModel(p)), prefabs);
        }

        static bool IsPrefabModel(string path) => path.Contains("/Models/Prefabs/");

        static string FindRoot()
        {
            var guid = AssetDatabase.FindAssets("FeudalMap_Materials t:TextAsset").FirstOrDefault();
            if (guid == null) throw new InvalidOperationException("FeudalMap_Materials.json not found in Assets");
            return Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace('\\', '/');
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
        #endregion

        #region Materials
        static Dictionary<string, Material> BuildMaterials(string root)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) throw new InvalidOperationException("HDRP/Lit shader not found - is HDRP installed and active?");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/FeudalMap_Materials.json").text;
            var specs = JsonUtility.FromJson<MatSpecList>(json).items;
            EnsureFolder(root, "Materials");
            var result = new Dictionary<string, Material>();
            foreach (var spec in specs)
            {
                var path = $"{root}/Materials/{spec.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                ApplySpec(mat, spec, $"{root}/Textures/");
                result[spec.name] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static void ApplySpec(Material mat, MatSpec s, string texDir)
        {
            Texture2D Load(string file)
            {
                if (string.IsNullOrEmpty(file)) return null;
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + file);
                if (t == null) Debug.LogWarning($"[FeudalMap] {s.name}: texture '{file}' missing");
                return t;
            }

            var baseMap = Load(s.baseMap);
            var normal = Load(s.normalMap);
            var mask = Load(s.maskMap);
            var col = new Color(s.color[0], s.color[1], s.color[2]).gamma;
            col.a = s.alpha;
            mat.SetColor("_BaseColor", col);
            mat.SetTexture("_BaseColorMap", baseMap);
            mat.SetTexture("_NormalMap", normal);
            mat.SetFloat("_NormalScale", s.normalStrength);
            mat.SetTexture("_MaskMap", mask);
            mat.SetFloat("_Metallic", s.metallic);
            mat.SetFloat("_Smoothness", s.smoothness);
            mat.SetFloat("_MetallicRemapMax", s.metallic);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);

            var emit = new Color(s.emission[0], s.emission[1], s.emission[2]);
            mat.SetFloat("_UseEmissiveIntensity", 0f);
            mat.SetColor("_EmissiveColor", emit * (s.emissionStrength * NitsPerEmissionStrength));
            mat.SetColor("_EmissiveColorLDR", emit.gamma);
            mat.SetTexture("_EmissiveColorMap", s.emissionStrength > 0f ? baseMap : null);

            var transparent = s.alpha < 1f;
            mat.SetFloat("_AlphaCutoffEnable", s.cutout ? 1f : 0f);
            mat.SetFloat("_AlphaCutoff", 0.5f);
            mat.SetFloat("_SurfaceType", transparent ? 1f : 0f);
            mat.SetFloat("_BlendMode", 0f);
            mat.SetFloat("_EnableFogOnTransparent", 1f);
            mat.SetFloat("_DoubleSidedEnable", s.cutout || transparent ? 1f : 0f);
            mat.SetFloat("_DoubleSidedNormalMode", 1f);
            mat.enableInstancing = true;
            ValidateHdMaterial(mat);
            EditorUtility.SetDirty(mat);
        }
        #endregion

        #region Model
        static void RemapModels(List<string> paths, Dictionary<string, Material> materials)
        {
            if (paths.Count == 0) throw new InvalidOperationException("no models found in Models/");
            foreach (var path in paths)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var kv in materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                importer.SaveAndReimport();
            }
        }

        // every repeated building is one fbx -> one prefab; chunks only carry INST_ placement empties
        static Dictionary<string, GameObject> BuildPrefabs(string root, IEnumerable<string> modelPaths)
        {
            EnsureFolder(root, "Prefabs");
            var result = new Dictionary<string, GameObject>();
            foreach (var path in modelPaths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                SetupMeshes(go);
                result[name] = PrefabUtility.SaveAsPrefabAsset(go, $"{root}/Prefabs/{name}.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            return result;
        }

        static int SetupMeshes(GameObject root)
        {
            int colliders = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                SetupLodGroup(t);
                if (t.GetComponent<MeshFilter>() == null) continue;
                // no BatchingStatic: it would bake every instance into its own copy and undo the prefab sharing
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                if (NoColliderPrefixes.Any(p => t.name.StartsWith(p) || root.name.StartsWith(p))) continue;
                if (!IsColliderLod(t)) continue;
                t.gameObject.AddComponent<MeshCollider>();
                colliders++;
            }
            return colliders;
        }

        static void SetupLodGroup(Transform t)
        {
            var levels = new List<Renderer[]>();
            for (int k = 0; ; k++)
            {
                var child = t.Cast<Transform>().FirstOrDefault(c => c.name.EndsWith("_LOD" + k));
                if (child == null) break;
                levels.Add(child.GetComponentsInChildren<Renderer>());
            }
            if (levels.Count < 2) return;
            var thresholds = levels.Count == 2 ? TwoLevelLods : ThreeLevelLods;
            if (levels.Count > thresholds.Length)
                throw new InvalidOperationException($"'{t.name}' has {levels.Count} LODs, setup handles at most {ThreeLevelLods.Length}");
            var group = t.GetComponent<LODGroup>();
            if (group == null) group = t.gameObject.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(levels.Select((r, i) => new LOD(thresholds[i], r)).ToArray());
            group.RecalculateBounds();
        }

        // collide against LOD1 (no bevels, same silhouette); LOD0 only when nothing coarser exists
        static bool IsColliderLod(Transform t)
        {
            var n = t.name;
            if (n.Length < 6 || n.Substring(n.Length - 5, 4) != "_LOD" || !char.IsDigit(n[n.Length - 1])) return true;
            var lod = n[n.Length - 1] - '0';
            if (lod == 1) return true;
            return lod == 0 && t.parent != null && t.parent.Find(n.Substring(0, n.Length - 1) + "1") == null;
        }
        #endregion

        #region Scene
        static void BuildScene(string root, IEnumerable<string> chunkPaths, Dictionary<string, GameObject> prefabs)
        {
            var map = new GameObject("FeudalMap");
            int lights = 0, colliders = 0, placed = 0;
            foreach (var path in chunkPaths)
            {
                var chunk = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), map.transform);
                if (chunk.name == "FeudalMap")
                {
                    Debug.LogWarning($"[FeudalMap] skipped '{path}': old single-file export, delete it");
                    UnityEngine.Object.DestroyImmediate(chunk);
                    continue;
                }
                PrefabUtility.UnpackPrefabInstance(chunk, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                colliders += SetupMeshes(chunk);
                foreach (var t in chunk.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("PL_"))
                    {
                        if (AddPointLight(t.gameObject)) lights++;
                    }
                    else if (t.name.StartsWith(InstancePrefix) && PlacePrefab(t, prefabs)) placed++;
                }
            }

            var moon = new GameObject("Moon");
            moon.transform.rotation = Quaternion.Euler(38f, 215f, 0f);
            var moonLight = AddHdLight(moon, LightType.Directional);
            moonLight.color = new Color(0.62f, 0.68f, 1f).gamma;
            moonLight.shadows = LightShadows.Soft;
            SetLightIntensity(moonLight, MoonLux, "Lux");

            BuildVolume(root);
            BuildCamera(map);

            EnsureFolder(root, "Scenes");
            var scenePath = $"{root}/Scenes/FeudalMap.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            Debug.Log($"[FeudalMap] scene saved to {scenePath}: {prefabs.Count} prefabs, {placed} placed, " +
                      $"{lights} lights, {colliders} chunk mesh colliders");
        }

        static bool PlacePrefab(Transform marker, Dictionary<string, GameObject> prefabs)
        {
            var key = marker.name.Substring(InstancePrefix.Length);
            key = key.Substring(0, Math.Max(0, key.LastIndexOf('_')));
            if (!prefabs.TryGetValue(key, out var prefab))
            {
                Debug.LogError($"[FeudalMap] no prefab '{key}' for marker '{marker.name}', NOT placed");
                return false;
            }
            // the marker already carries the fbx axis conversion the prefab root has, so identity lines them up
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            return true;
        }

        static bool AddPointLight(GameObject go)
        {
            var parts = go.name.Split('_');
            if (parts.Length < 4 || !float.TryParse(parts[2], out var watts) ||
                !ColorUtility.TryParseHtmlString("#" + parts[3].Substring(0, Math.Min(6, parts[3].Length)), out var color))
            {
                Debug.LogWarning($"[FeudalMap] light marker '{go.name}' has an unreadable name, skipped");
                return false;
            }
            var light = AddHdLight(go, LightType.Point);
            light.color = color;
            light.range = Mathf.Clamp(Mathf.Sqrt(watts) * 2.5f, 8f, 40f);
            light.shadows = watts >= ShadowedLightMinWatts ? LightShadows.Soft : LightShadows.None;
            SetLightIntensity(light, watts * LumenPerBlenderWatt, "Lumen");
            return true;
        }

        static void BuildCamera(GameObject map)
        {
            var plaza = FindChild(map.transform, "Room_Plaza");
            var boss = FindChild(map.transform, "Room_Boss");
            var center = plaza != null ? Center(plaza) : Vector3.zero;
            var dir = boss != null ? Vector3.ProjectOnPlane(Center(boss) - center, Vector3.up).normalized : Vector3.forward;
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.transform.position = center - dir * 130f + Vector3.up * 96f;
            cam.transform.LookAt(center + dir * 14f - Vector3.up * 4f);
            var c = cam.AddComponent<Camera>();
            c.fieldOfView = 34f;
            c.farClipPlane = 2000f;
            var hdCam = FindHdType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            if (hdCam != null && cam.GetComponent(hdCam) == null) cam.AddComponent(hdCam);
        }

        static Vector3 Center(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return t.position;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.center;
        }

        static Transform FindChild(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        #endregion

        #region Volume
        static void BuildVolume(string root)
        {
            var path = $"{root}/FeudalMap_Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var sky = AssetDatabase.FindAssets("t:Cubemap", new[] { $"{root}/Sky" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Cubemap>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (sky == null) Debug.LogError("[FeudalMap] no Cubemap in Sky/ - the HDRI was not imported as a cube, sky will be empty");

            var env = AddOverride(profile, "VisualEnvironment");
            var skyType = FindHdType("UnityEngine.Rendering.HighDefinition.SkyType");
            if (skyType != null) SetParam(env, "skyType", (int)Enum.Parse(skyType, "HDRI"));

            var hdri = AddOverride(profile, "HDRISky");
            if (sky != null) SetParam(hdri, "hdriSky", sky);
            SetParam(hdri, "skyIntensityMode", "Multiplier");
            SetParam(hdri, "multiplier", SkyMultiplier);
            SetParam(hdri, "rotation", 200f);

            var fog = AddOverride(profile, "Fog");
            SetParam(fog, "enabled", true);
            SetParam(fog, "meanFreePath", 140f);
            SetParam(fog, "baseHeight", -12f);
            SetParam(fog, "maximumHeight", 30f);
            SetParam(fog, "albedo", new Color(0.5f, 0.44f, 0.68f));
            SetParam(fog, "enableVolumetricFog", true);

            var exposure = AddOverride(profile, "Exposure");
            SetParam(exposure, "mode", "Fixed");
            SetParam(exposure, "fixedExposure", FixedExposureEV100);

            var tonemap = AddOverride(profile, "Tonemapping");
            SetParam(tonemap, "mode", "ACES");

            var bloom = AddOverride(profile, "Bloom");
            SetParam(bloom, "intensity", 0.25f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Sky and Fog Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static VolumeComponent AddOverride(VolumeProfile profile, string typeName)
        {
            var type = FindHdType("UnityEngine.Rendering.HighDefinition." + typeName);
            if (type == null) throw new InvalidOperationException($"HDRP volume component '{typeName}' not found");
            var comp = profile.Add(type);
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
        }

        static void SetParam(VolumeComponent comp, string field, object value)
        {
            var f = comp.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (!(f?.GetValue(comp) is VolumeParameter p))
            {
                Debug.LogWarning($"[FeudalMap] {comp.GetType().Name}.{field} not found in this HDRP version, left at default");
                return;
            }
            var prop = p.GetType().GetProperty("value");
            var target = prop.PropertyType;
            object v = value;
            if (target.IsEnum)
                v = value is string s ? Enum.Parse(target, s) : Enum.ToObject(target, value);
            else if (!target.IsInstanceOfType(value))
                v = Convert.ChangeType(value, target);
            prop.SetValue(p, v);
            p.overrideState = true;
        }
        #endregion

        #region HDRP reflection
        // hdrp moved light units and material validation between versions; reflection keeps this compiling on 14-17
        static Type FindHdType(string fullName) =>
            Type.GetType(fullName + ", Unity.RenderPipelines.HighDefinition.Runtime") ??
            Type.GetType(fullName + ", Unity.RenderPipelines.HighDefinition.Editor");

        static Light AddHdLight(GameObject go, LightType type)
        {
            var light = go.GetComponent<Light>();
            if (light == null) light = go.AddComponent<Light>();
            light.type = type;
            var hd = FindHdType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
            if (hd != null && go.GetComponent(hd) == null) go.AddComponent(hd);
            return light;
        }

        static void SetLightIntensity(Light light, float value, string unit)
        {
            var unitProp = typeof(Light).GetProperty("lightUnit");
            if (unitProp != null)
            {
                unitProp.SetValue(light, Enum.Parse(unitProp.PropertyType, unit));
                light.intensity = value;
                return;
            }
            var hdType = FindHdType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
            var setter = hdType?.GetMethods().FirstOrDefault(m => m.Name == "SetIntensity" && m.GetParameters().Length == 2);
            if (setter == null)
            {
                Debug.LogError($"[FeudalMap] cannot set {unit} on '{light.name}' - no light unit API found, intensity NOT applied");
                return;
            }
            setter.Invoke(light.GetComponent(hdType), new[] { (object)value, Enum.Parse(setter.GetParameters()[1].ParameterType, unit) });
        }

        static void ValidateHdMaterial(Material mat)
        {
            var hdMaterial = Type.GetType("UnityEditor.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Editor");
            var validate = hdMaterial?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
            if (validate == null)
            {
                Debug.LogWarning($"[FeudalMap] HDMaterial.ValidateMaterial not found - select '{mat.name}' in the Inspector once to refresh its keywords");
                return;
            }
            validate.Invoke(null, new object[] { mat });
        }
        #endregion
    }
}
