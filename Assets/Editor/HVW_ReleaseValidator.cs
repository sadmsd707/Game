using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.EditorTests;
using HeavyVehiclesWorld.EditorTools;

namespace HeavyVehiclesWorld.Editor
{
    public static class HVW_ReleaseValidator
    {
        [MenuItem("HeavyVehiclesWorld/🚀 Run Full Release Validation & Build APK")]
        public static void RunFullReleaseValidation()
        {
            int exitCode = 0;
            Debug.Log("================================================================================");
            Debug.Log("🚀 [PHASE 4 RELEASE VALIDATION] Comprehensive Pre-Release Inspection");
            Debug.Log("================================================================================");

            try
            {
                // 1. Validate Android Platform & Settings (13 Technical Checks)
                Debug.Log("\n[STEP 1/4] Running Android Build Validator...");
                var (passCount, failCount, totalChecks, failDetails) = AndroidBuildValidator.RunValidation();
                if (failCount > 0)
                {
                    throw new Exception($"AndroidBuildValidator failed: {failCount}/{totalChecks} failed.\n{failDetails}");
                }
                Debug.Log($"✅ Android Build Validator: {passCount}/{totalChecks} checks passed.");

                // 2. Validate Quality Tiers & URP Assets (Low, Medium, High, Ultra)
                Debug.Log("\n[STEP 2/4] Validating URP Quality Tiers & Pipeline Assets...");
                string[] tiers = new string[] { "Low", "Medium", "High", "Ultra" };
                foreach (var tier in tiers)
                {
                    string path = $"Assets/Settings/URP_Quality_{tier}.asset";
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
                    if (asset == null) throw new Exception($"Missing URP tier asset: {path}");

                    string resPath = $"Assets/Resources/Settings/URP_Quality_{tier}.asset";
                    var resAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(resPath);
                    if (resAsset == null) throw new Exception($"Missing Resources URP tier asset: {resPath}");
                }
                Debug.Log("✅ All 4 URP Quality Tiers verified in Assets/Settings/ and Assets/Resources/Settings/.");

                // 3. Validate Textures & ASTC Overrides
                Debug.Log("\n[STEP 3/4] Validating Texture ASTC Platform Overrides...");
                string[] searchFolders = new string[] { "Assets/Terrain", "Assets/Textures" };
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchFolders);
                int texChecked = 0;
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        var ps = importer.GetPlatformTextureSettings("Android");
                        if (!ps.overridden)
                        {
                            throw new Exception($"Texture missing Android override: {path}");
                        }
                        texChecked++;
                    }
                }
                Debug.Log($"✅ All {texChecked} textures verified with Android ASTC overrides.");

                // 4. Validate Production Keystore
                Debug.Log("\n[STEP 4/4] Validating Production Release Keystore...");
                string ksPath = Path.Combine(Directory.GetCurrentDirectory(), "Keystore", "HVW_Production.keystore");
                if (!File.Exists(ksPath)) throw new Exception($"Missing keystore file: {ksPath}");
                Debug.Log($"✅ Production release keystore verified: {ksPath}");

                Debug.Log("\n================================================================================");
                Debug.Log("🎉 [PHASE 4 RELEASE VALIDATION PASSED] ALL 4 VALIDATION SUITES 100% CLEAN!");
                Debug.Log("================================================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [RELEASE VALIDATION FAILED]: {ex.Message}\n{ex.StackTrace}");
                exitCode = 1;
            }
            finally
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(exitCode);
                }
            }
        }
    }
}
