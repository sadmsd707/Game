// HVW_AutoRunner.cs  — v2
// Uses EditorPrefs instead of a flag file. Fires on domain reload.
// Set the pref BEFORE launching Unity:
//   EditorPrefs.SetString("HVW_AutoRun", "FULLPIPELINE")
// OR just use the simpler approach: check for the txt flag at a known absolute path.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace HeavyVehiclesWorld.Editor
{
    [InitializeOnLoad]
    public static class HVW_AutoRunner
    {
        // Absolute path — avoids Application.dataPath timing issues
        private static readonly string PROJECT_ROOT =
            Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(HVW_AutoRunner).Assembly.Location),
                "..", "..", ".."));  // Assembly is in Library/ScriptAssemblies — go up 3 levels

        private static readonly string FLAG_PATH =
            Path.Combine(PROJECT_ROOT, "Assets", "Editor", "HVW_AutoRunFlag.txt");

        static HVW_AutoRunner()
        {
            // Log so we can confirm the static constructor ran
            Debug.Log($"[HVW_AutoRunner] v2 Init. Checking flag at: {FLAG_PATH}");

            if (!File.Exists(FLAG_PATH))
            {
                Debug.Log("[HVW_AutoRunner] No flag file — skipping auto-run.");
                return;
            }

            string flag = File.ReadAllText(FLAG_PATH).Trim();
            Debug.Log($"[HVW_AutoRunner] Flag found: '{flag}' — scheduling pipeline...");

            // Delete immediately to prevent re-run on next domain reload
            File.Delete(FLAG_PATH);
            // Also delete .meta if present
            string meta = FLAG_PATH + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            // Schedule with delayCall (fires after Editor is fully ready)
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    ExecutePipeline(flag);
                };
            };
        }

        private static void ExecutePipeline(string flag)
        {
            Debug.Log($"[HVW_AutoRunner] Executing pipeline: {flag}");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[HVW_AutoRunner] Pipeline: {flag}");

            // ── Open scene if needed ──────────────────────────────────────────
            const string SCENE = "Assets/Scenes/MainWorld.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded || !activeScene.path.Contains("MainWorld"))
            {
                Debug.Log($"[HVW_AutoRunner] Opening scene: {SCENE}");
                EditorSceneManager.OpenScene(SCENE);
            }

            // ── Step 1: Import vehicle models ─────────────────────────────────
            if (flag is "FULLPIPELINE" or "STEP1" or "BUILDONLY")
            {
                try
                {
                    HVW_ModelImportHelper.ImportVehicleModels();
                    sb.AppendLine("✅ Step 1 (models) complete");
                }
                catch (System.Exception e) { sb.AppendLine($"⚠️ Step 1: {e.Message}"); }
            }

            // ── Step 2: Terrain textures + HDRI sky ──────────────────────────
            if (flag is "FULLPIPELINE" or "STEP2" or "BUILDONLY")
            {
                try
                {
                    HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky();
                    sb.AppendLine("✅ Step 2 (terrain+sky) complete");
                }
                catch (System.Exception e) { sb.AppendLine($"⚠️ Step 2: {e.Message}"); }
            }

            // ── Save scene ───────────────────────────────────────────────────
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("✅ Scene saved");

            // ── Windows build ─────────────────────────────────────────────────
            if (flag is "FULLPIPELINE" or "BUILDWINDOWS" or "BUILDONLY")
            {
                try
                {
                    HVW_BatchAutomation.BuildWindows();
                    sb.AppendLine("✅ Windows build complete");
                    RunWindowsForScreenshots();
                    sb.AppendLine("✅ Screenshot capture started");
                }
                catch (System.Exception e) { sb.AppendLine($"⚠️ Windows build: {e.Message}"); }
            }

            // ── Android APK ───────────────────────────────────────────────────
            if (flag is "FULLPIPELINE" or "BUILDANDROID" or "BUILDONLY")
            {
                try
                {
                    HVW_BatchAutomation.BuildAndroid();
                    sb.AppendLine("✅ Android APK complete");
                }
                catch (System.Exception e) { sb.AppendLine($"⚠️ Android build: {e.Message}"); }
            }

            string result = sb.ToString();
            Debug.Log("[HVW_AutoRunner] DONE:\n" + result);
            EditorUtility.DisplayDialog("HVW AutoRunner Complete", result, "OK");
        }

        private static void RunWindowsForScreenshots()
        {
            string exePath = Path.Combine(PROJECT_ROOT, "..", "Builds", "Windows",
                                          "HeavyVehiclesWorld.exe");
            exePath = Path.GetFullPath(exePath);
            if (!File.Exists(exePath)) { Debug.LogWarning($"[HVW_AutoRunner] exe not found: {exePath}"); return; }

            Directory.CreateDirectory(Path.Combine(PROJECT_ROOT, "..", "Builds", "Screenshots"));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath, Arguments = "-hvwScreenshots", UseShellExecute = true
            });
            Debug.Log($"[HVW_AutoRunner] Launched for screenshots: {exePath}");
        }
    }
}
