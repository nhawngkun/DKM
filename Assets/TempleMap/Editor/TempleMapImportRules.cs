using System.IO;
using UnityEditor;

namespace TempleMap.EditorTools
{
    public class TempleMapImportRules : AssetPostprocessor
    {
        static bool InPackage(string path) => path.Contains("/TempleMap/");

        void OnPreprocessTexture()
        {
            if (!InPackage(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            var file = Path.GetFileNameWithoutExtension(assetPath);
            ti.maxTextureSize = 2048;
            if (assetPath.Contains("/Sky/"))
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.maxTextureSize = 4096;
                return;
            }
            if (file.Contains("_nor_"))
                ti.textureType = TextureImporterType.NormalMap;
            else if (file.Contains("_mask_"))
                ti.sRGBTexture = false;
            else if (file.EndsWith("Card"))
                ti.alphaIsTransparency = true;
        }

        void OnPreprocessModel()
        {
            if (!InPackage(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import;
            // blender's own bake_space_transform double-rotates meshes nested under empties; unity's bake does not
            mi.bakeAxisConversion = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
