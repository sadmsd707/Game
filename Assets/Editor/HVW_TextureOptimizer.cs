using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace HeavyVehiclesWorld.Editor
{
    public class HVW_TextureOptimizer : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Textures") && !assetPath.StartsWith("Assets/Terrain"))
                return;

            var importer = (TextureImporter)assetImporter;
            if (importer == null) return;

            ConfigureImporter(importer, assetPath);
        }

        [MenuItem("HeavyVehiclesWorld/Optimize All Textures (ASTC Android)")]
        public static void OptimizeAllExistingTextures()
        {
            Debug.Log("[HVW_TextureOptimizer] Starting texture optimization across Assets/Textures and Assets/Terrain...");

            string[] searchFolders = new string[] { "Assets/Textures", "Assets/Terrain" };
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchFolders);

            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    ConfigureImporter(importer, path);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HVW_TextureOptimizer] Successfully optimized {count} textures for ASTC Android!");
        }

        public static void ConfigureImporter(TextureImporter importer, string path)
        {
            string lower = path.ToLowerInvariant();
            bool isNormalMap = lower.Contains("normal") || lower.Contains("bump") || lower.Contains("nrm");
            bool isKeyTexture = lower.Contains("vehicle") || lower.Contains("car") || lower.Contains("truck") ||
                                lower.Contains("tractor") || lower.Contains("jcb") || lower.Contains("excavator") ||
                                lower.Contains("harvester") || lower.Contains("body");

            // Mipmaps enabled
            importer.mipmapEnabled = true;

            if (isNormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }

            // Android Platform Settings
            var androidSettings = importer.GetPlatformTextureSettings("Android");
            androidSettings.overridden = true;
            androidSettings.format = isKeyTexture ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(androidSettings);

            // Standalone / Windows Platform Settings for Editor/Player testing
            var standaloneSettings = importer.GetPlatformTextureSettings("Standalone");
            standaloneSettings.overridden = true;
            standaloneSettings.format = isNormalMap ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT5;
            importer.SetPlatformTextureSettings(standaloneSettings);
        }
    }
}
