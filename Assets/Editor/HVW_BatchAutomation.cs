using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.Editor
{
    /// <summary>
    /// Unity batch-mode entry points called via -executeMethod.
    /// All methods suppress dialogs so they run headless (-batchmode -quit).
    /// </summary>
    public static class HVW_BatchAutomation
    {
        // ── COMBINED: Step 1 + Step 2 + save scene ──────────────────────────
        [MenuItem("HeavyVehiclesWorld/Batch - Apply All Steps (1+2)")]
        public static void ApplyAllSteps()
        {
            Debug.Log("[HVW_Batch] ===== Starting Step 1 + Step 2 combined apply =====");

            // Step 1 — Vehicle model import
            try
            {
                HVW_ModelImportHelper.ImportVehicleModels();
                Debug.Log("[HVW_Batch] Step 1 complete.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HVW_Batch] Step 1 error (non-fatal): {e.Message}");
            }

            // Step 2 — Terrain textures + HDRI sky
            try
            {
                HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky();
                Debug.Log("[HVW_Batch] Step 2 complete.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HVW_Batch] Step 2 error (non-fatal): {e.Message}");
            }

            // Save scene and project
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HVW_Batch] ===== All steps applied and saved =====");
        }

        // ── STEP 2 ONLY (terrain + sky) ─────────────────────────────────────
        public static void ApplyStep2()
        {
            Debug.Log("[HVW_Batch] ===== Starting Step 2 (terrain + sky) =====");
            HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HVW_Batch] ===== Step 2 complete =====");
        }

        // ── BUILD WINDOWS STANDALONE ─────────────────────────────────────────
        public static void BuildWindows()
        {
            string buildDir = Path.Combine(
                Application.dataPath, "..", "..", "..", "Builds", "Windows");
            buildDir = Path.GetFullPath(buildDir);
            Directory.CreateDirectory(buildDir);

            string exePath = Path.Combine(buildDir, "HeavyVehiclesWorld.exe");
            Debug.Log($"[HVW_Batch] Building Windows standalone to: {exePath}");

            // Collect scenes
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0) scenes.Add("Assets/Scenes/HeavyVehiclesWorld.unity");

            var opts = new BuildPlayerOptions
            {
                scenes           = scenes.ToArray(),
                locationPathName = exePath,
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[HVW_Batch] Windows build SUCCEEDED: {exePath}");
            else
                Debug.LogError($"[HVW_Batch] Windows build FAILED: {report.summary.result}");
        }

        // ── COMBINED: apply steps + build Windows ───────────────────────────
        public static void ApplyAllAndBuild()
        {
            ApplyAllSteps();
            BuildWindows();
        }
    }
}
