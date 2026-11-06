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
        // lighting standard = what was hand-tuned in DKM FeudalMap.unity on 2026-11-04
        const float LumenPerBlenderWatt = 0.245f;
        const float NitsPerEmissionStrength = 1f;
        const float FixedExposureEV100 = 0.4f;
        const float MoonLux = 14f;
        const float SkyMultiplier = 0.32f;
        const float ShadowedLightMinWatts = 200f;
        const uint CharacterLightLayerMask = 2u; // rendering layer bit 1

        // unity-side material tuning that differs from the blender json
        static readonly Dictionary<string, float> EmissionStrengthOverrides = new Dictionary<string, float>
        {
            { "M_paper", 2.5f },
            { "M_shoji", 0.33f },
        };
        static readonly Dictionary<string, float> NormalStrengthOverrides = new Dictionary<string, float>
        {
            { "M_path", 8f },
        };

        // roots this menu owns; everything else in the scene (character, player camera, gameplay) is kept on rebuild
        static readonly string[] GeneratedRoots = { "FeudalMap", "Moon", "Sky and Fog Volume", "Main Camera" };
        const string CharacterName = "Character";
        const string CharacterRigName = "CharLightRig";
        const string CharacterRigScript = "CharacterLightRig, Assembly-CSharp";

        static readonly string[] NoColliderPrefixes = { "Sakura", "Bamboo", "Portal", "Beam", "Seal", "Waterfall", "PL_" };
        static readonly float[] TwoLevelLods = { 0.2f, 0.01f };
        static readonly float[] ThreeLevelLods = { 0.3f, 0.1f, 0.01f };
        const string InstancePrefix = "INST_";

#pragma warning disable 0649
        [Serializable]
        class MatSpec
        {
            public string name, baseMap, normalMap, maskMap, detailMap;
            public float normalStrength, metallic, smoothness, alpha, emissionStrength, detailTiling, detailAlbedo;
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
            if (!OpenSceneForRebuild(root)) return;
            var prefabs = BuildPrefabs(root, models.Where(IsPrefabModel));
            BuildScene(root, models.Where(p => !IsPrefabModel(p)), prefabs);
        }

        static bool IsPrefabModel(string path) => path.Contains("/Models/Prefabs/");

        static string ScenePath(string root) => $"{root}/Scenes/FeudalMap.unity";

        static bool OpenSceneForRebuild(string root)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var path = ScenePath(root);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return true;
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (GeneratedRoots.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);
            return true;
        }

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
            mat.SetFloat("_NormalScale", NormalStrengthOverrides.TryGetValue(s.name, out var ns) ? ns : s.normalStrength);
            mat.SetTexture("_MaskMap", mask);
            mat.SetFloat("_Metallic", s.metallic);
            mat.SetFloat("_Smoothness", s.smoothness);
            mat.SetFloat("_MetallicRemapMax", s.metallic);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);

            // large-scale brightness drift (uv0 x detailTiling) so tiled stains do not read as a grid
            var detail = Load(s.detailMap);
            mat.SetTexture("_DetailMap", detail);
            mat.SetTextureScale("_DetailMap", Vector2.one * (s.detailTiling > 0f ? s.detailTiling : 1f));
            mat.SetFloat("_UVDetail", 0f);
            mat.SetVector("_UVDetailsMappingMask", new Vector4(1f, 0f, 0f, 0f));
            mat.SetFloat("_DetailAlbedoScale", s.detailAlbedo);
            mat.SetFloat("_DetailNormalScale", 0f);
            mat.SetFloat("_DetailSmoothnessScale", detail != null ? 1f : 0f);

            var emit = new Color(s.emission[0], s.emission[1], s.emission[2]);
            mat.SetFloat("_UseEmissiveIntensity", 0f);
            var strength = EmissionStrengthOverrides.TryGetValue(s.name, out var es) ? es : s.emissionStrength;
            mat.SetColor("_EmissiveColor", emit * (strength * NitsPerEmissionStrength));
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
            moon.transform.rotation = Quaternion.Euler(52f, 215f, 0f);
            var moonLight = AddHdLight(moon, LightType.Directional);
            moonLight.color = new Color(1f, 0.96f, 0.9f);
            moonLight.shadows = LightShadows.Soft;
            SetLightIntensity(moonLight, MoonLux, "Lux");
            SetHdLightProperty(moonLight, "angularDiameter", 3f);

            BuildVolume(root);
            if (!EditorSceneManager.GetActiveScene().GetRootGameObjects().Any(r => r.GetComponentInChildren<Camera>(true) != null)) BuildCamera(map);
            EnsureCharacterRig(map);

            EnsureFolder(root, "Scenes");
            var scenePath = ScenePath(root);
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
            light.range = Mathf.Clamp(Mathf.Sqrt(watts) * 1.5f, 4.8f, 24f);
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
            var hdCamType = FindHdType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            if (hdCamType == null) return;
            var hdCam = cam.GetComponent(hdCamType) ?? cam.AddComponent(hdCamType);
            SetField(hdCam, "antialiasing", "TemporalAntialiasing");
            SetField(hdCam, "TAAQuality", "High");
            SetField(hdCam, "taaSharpenMode", 2);
            SetField(hdCam, "taaSharpenStrength", 0.85f);
            EnableCameraFrameSetting(hdCam, "VolumetricClouds");
        }

        // key/fill/rim spots on rendering layer 2 only, so they light the character and never the map
        static void EnsureCharacterRig(GameObject map)
        {
            if (FindSceneObject(CharacterRigName) != null) return;
            var character = FindSceneObject(CharacterName);
            var rig = new GameObject(CharacterRigName);
            if (character != null)
            {
                rig.transform.SetParent(character.transform, false);
                foreach (var r in character.GetComponentsInChildren<Renderer>(true))
                    r.renderingLayerMask |= CharacterLightLayerMask;
            }
            else
            {
                var start = FindChild(map.transform, "PlayerStart");
                if (start != null) rig.transform.position = start.position;
                Debug.LogWarning($"[FeudalMap] no '{CharacterName}' in scene: {CharacterRigName} placed at PlayerStart, " +
                                 $"parent it to the character and add rendering layer mask {CharacterLightLayerMask} to its renderers");
            }
            var follow = Type.GetType(CharacterRigScript);
            if (follow != null) rig.AddComponent(follow);

            AddRigSpot(rig, "Key", new Vector3(-1.2f, 3.2f, -1.6f), new Vector3(43.53f, 36.87f, 0f),
                new Color(1f, 0.94f, 0.88f), 55f, 80f, 40f, 0.35f, false, 0f);
            AddRigSpot(rig, "Fill", new Vector3(1.6f, 1.4f, -2.2f), new Vector3(2.11f, 323.97f, 0f),
                new Color(0.92f, 0.94f, 1f), 18f, 80f, 30f, 0.8f, false, 1f);
            AddRigSpot(rig, "RimL", new Vector3(-1.7f, 1.8f, 2f), new Vector3(10.78f, 139.64f, 0f),
                new Color(1f, 0.72f, 0.55f), 40f, 60f, 36f, 0.25f, false, 0f);
            AddRigSpot(rig, "RimR", new Vector3(1.5f, 2.3f, 2.2f), new Vector3(20.58f, 214.29f, 0f),
                new Color(0.82f, 0.88f, 1f), 150f, 60f, 36f, 0.25f, false, 0f);
        }

        static void AddRigSpot(GameObject rig, string name, Vector3 pos, Vector3 euler, Color color, float lumen,
            float angle, float innerAngle, float radius, bool shadows, float volumetricDimmer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var light = AddHdLight(go, LightType.Spot);
            light.color = color;
            light.range = 8f;
            light.spotAngle = angle;
            light.innerSpotAngle = innerAngle;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.renderingLayerMask = (int)CharacterLightLayerMask;
            SetLightIntensity(light, lumen, "Lumen");
            SetHdLightProperty(light, "shapeRadius", radius);
            SetHdLightProperty(light, "volumetricDimmer", volumetricDimmer);
        }

        static GameObject FindSceneObject(string name) =>
            EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name)?.gameObject;

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
            // gradient sky is the active one; the hdri stays set up so switching skyType back is one click
            if (skyType != null) SetParam(env, "skyType", (int)Enum.Parse(skyType, "Gradient"));
            SetParam(env, "skyAmbientMode", "Dynamic");

            var gradient = AddOverride(profile, "GradientSky");
            SetParam(gradient, "skyIntensityMode", "Exposure");
            SetParam(gradient, "exposure", 3.2f);
            SetParam(gradient, "top", new Color(0.42f, 0.56f, 0.74f));
            SetParam(gradient, "middle", new Color(0.8f, 0.84f, 0.88f));
            SetParam(gradient, "bottom", new Color(0.42f, 0.42f, 0.42f));
            SetParam(gradient, "gradientDiffusion", 1.2f);

            var hdri = AddOverride(profile, "HDRISky");
            if (sky != null) SetParam(hdri, "hdriSky", sky);
            SetParam(hdri, "skyIntensityMode", "Multiplier");
            SetParam(hdri, "multiplier", SkyMultiplier);
            SetParam(hdri, "rotation", 200f);

            var fog = AddOverride(profile, "Fog");
            SetParam(fog, "enabled", false);
            SetParam(fog, "colorMode", "ConstantColor");
            SetParam(fog, "color", new Color(0.7f, 0.75f, 0.8f));
            SetParam(fog, "tint", new Color(0.78f, 0.84f, 0.92f));
            SetParam(fog, "maxFogDistance", 1500f);
            SetParam(fog, "meanFreePath", 130f);
            SetParam(fog, "baseHeight", 0f);
            SetParam(fog, "maximumHeight", 60f);
            SetParam(fog, "albedo", new Color(0.75f, 0.78f, 0.82f));
            SetParam(fog, "globalLightProbeDimmer", 0.8f);
            SetParam(fog, "enableVolumetricFog", false);
            SetParam(fog, "anisotropy", 0.35f);

            var clouds = AddOverride(profile, "VolumetricClouds");
            SetParam(clouds, "enable", false);
            SetParam(clouds, "shadows", true);
            SetParam(clouds, "shadowOpacity", 0.7f);

            var exposure = AddOverride(profile, "Exposure");
            SetParam(exposure, "mode", "Fixed");
            SetParam(exposure, "fixedExposure", FixedExposureEV100);

            var tonemap = AddOverride(profile, "Tonemapping");
            SetParam(tonemap, "mode", "ACES");

            var grading = AddOverride(profile, "ColorAdjustments");
            SetParam(grading, "postExposure", 0f);
            SetParam(grading, "contrast", 26f);
            SetParam(grading, "colorFilter", new Color(0.97f, 0.98f, 1f));
            SetParam(grading, "saturation", -18f);

            var white = AddOverride(profile, "WhiteBalance");
            SetParam(white, "temperature", -2f);
            SetParam(white, "tint", 2f);

            var smh = AddOverride(profile, "ShadowsMidtonesHighlights");
            SetParam(smh, "shadows", new Vector4(0.95f, 1f, 1.06f, -0.03f));
            SetParam(smh, "midtones", new Vector4(0.97f, 1f, 1.04f, -0.04f));
            SetParam(smh, "highlights", new Vector4(1f, 1f, 1f, 0f));

            var bloom = AddOverride(profile, "Bloom");
            SetParam(bloom, "threshold", 0.9f);
            SetParam(bloom, "intensity", 0.15f);
            SetParam(bloom, "scatter", 0.45f);

            var ao = AddOverride(profile, "ScreenSpaceAmbientOcclusion");
            SetParam(ao, "intensity", 1.1f);
            SetParam(ao, "radius", 1.5f);
            ao.active = false;

            var shadows = AddOverride(profile, "HDShadowSettings");
            SetParam(shadows, "maxShadowDistance", 50f);
            SetParam(shadows, "cascadeShadowSplitCount", 2);

            SetParam(AddOverride(profile, "MotionBlur"), "intensity", 0.2f);
            SetParam(AddOverride(profile, "FilmGrain"), "intensity", 0.06f);
            var vignette = AddOverride(profile, "Vignette");
            SetParam(vignette, "intensity", 0.22f);
            SetParam(vignette, "smoothness", 0.5f);

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

        // unity 6 moved some hd light properties (shapeRadius) onto Light itself; try Light first, then the hd component
        static void SetHdLightProperty(Light light, string name, float value)
        {
            var onLight = typeof(Light).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (onLight != null && onLight.CanWrite)
            {
                onLight.SetValue(light, value);
                return;
            }
            var hdType = FindHdType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
            var prop = hdType?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite)
            {
                Debug.LogWarning($"[FeudalMap] '{light.name}': light property '{name}' not found, left at default");
                return;
            }
            prop.SetValue(light.GetComponent(hdType), value);
        }

        static void SetField(Component target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null)
            {
                Debug.LogWarning($"[FeudalMap] {target.GetType().Name}.{field} not found in this HDRP version, left at default");
                return;
            }
            var t = f.FieldType;
            f.SetValue(target, t.IsEnum ? (value is string s ? Enum.Parse(t, s) : Enum.ToObject(t, value)) : Convert.ChangeType(value, t));
        }

        // turns a frame setting on through the camera's custom frame settings (boxed structs, so get-modify-set)
        static void EnableCameraFrameSetting(Component hdCam, string fieldName)
        {
            var type = hdCam.GetType();
            var fieldEnum = FindHdType("UnityEngine.Rendering.HighDefinition.FrameSettingsField");
            var settingsField = type.GetField("m_RenderingPathCustomFrameSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var maskField = type.GetField("renderingPathCustomFrameSettingsOverrideMask", BindingFlags.Public | BindingFlags.Instance);
            if (fieldEnum == null || settingsField == null || maskField == null)
            {
                Debug.LogWarning($"[FeudalMap] camera frame setting '{fieldName}' could not be enabled in this HDRP version");
                return;
            }
            var index = Enum.Parse(fieldEnum, fieldName);
            var settings = settingsField.GetValue(hdCam);
            settings.GetType().GetMethod("SetEnabled").Invoke(settings, new[] { index, (object)true });
            settingsField.SetValue(hdCam, settings);

            var overrideMask = maskField.GetValue(hdCam);
            var bitsField = overrideMask.GetType().GetField("mask");
            var bits = bitsField.GetValue(overrideMask);
            bits.GetType().GetProperty("Item").SetValue(bits, true, new object[] { Convert.ToUInt32(index) });
            bitsField.SetValue(overrideMask, bits);
            maskField.SetValue(hdCam, overrideMask);
            SetField(hdCam, "customRenderingSettings", true);
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
