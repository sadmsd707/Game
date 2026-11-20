using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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
        private const string MAIN_SCENE_PATH = "Assets/Scenes/MainWorld.unity";

        private static void EnsureMainSceneOpen()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MAIN_SCENE_PATH)
            {
                Debug.Log($"[HVW_Batch] Opening scene: {MAIN_SCENE_PATH}");
                EditorSceneManager.OpenScene(MAIN_SCENE_PATH, OpenSceneMode.Single);
            }
        }

        // ── COMBINED: Step 1 + Step 2 + save scene ──────────────────────────
        [MenuItem("HeavyVehiclesWorld/Batch - Apply All Steps (1+2)")]
        public static void ApplyAllSteps()
        {
            Debug.Log("[HVW_Batch] ===== Starting Step 1 + Step 2 combined apply =====");

            EnsureMainSceneOpen();

            // Step 1 — Vehicle model import
            try { HVW_ModelImportHelper.ImportVehicleModels(); Debug.Log("[HVW_Batch] Step 1 complete."); }
            catch (System.Exception e) { Debug.LogWarning($"[HVW_Batch] Step 1 error (non-fatal): {e.Message}"); }

            // Step 2 — Terrain textures + HDRI sky
            try { HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky(); Debug.Log("[HVW_Batch] Step 2 complete."); }
            catch (System.Exception e) { Debug.LogWarning($"[HVW_Batch] Step 2 error (non-fatal): {e.Message}"); }

            // Save scene and project
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HVW_Batch] ===== All steps applied and saved =====");
        }

        // ── STEP 2 ONLY (terrain + sky) ─────────────────────────────────────
        public static void ApplyStep2()
        {
            Debug.Log("[HVW_Batch] ===== Starting Step 2 (terrain + sky) =====");
            EnsureMainSceneOpen();
            HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HVW_Batch] ===== Step 2 complete =====");
        }

        // ── BUILD WINDOWS STANDALONE ─────────────────────────────────────────
        public static void BuildWindows()
        {
            string buildDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "Builds", "Windows"));
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "HeavyVehiclesWorld.exe");
            Debug.Log($"[HVW_Batch] Building Windows standalone to: {exePath}");
            var opts = new BuildPlayerOptions
            {
                scenes = GetScenes(), locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log(report.summary.result == BuildResult.Succeeded
                ? $"[HVW_Batch] Windows build SUCCEEDED: {exePath}"
                : $"[HVW_Batch] Windows build FAILED: {report.summary.result}");
        }

        // ── BUILD ANDROID APK ────────────────────────────────────────────────
        [MenuItem("HeavyVehiclesWorld/Build - Android APK (Step 4)")]
        public static void BuildAndroid()
        {
            string buildDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "Builds", "Android"));
            Directory.CreateDirectory(buildDir);
            string apkPath = Path.Combine(buildDir, "HeavyVehiclesWorld_v1.37-b7.apk");
            Debug.Log($"[HVW_Batch] Building Android APK to: {apkPath}");

            // Keystore
            string ksPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Keystore", "HVW_Production.keystore"));
            PlayerSettings.Android.useCustomKeystore    = true;
            PlayerSettings.Android.keystoreName        = ksPath;
            PlayerSettings.Android.keystorePass        = "Shivraj@2026";
            PlayerSettings.Android.keyaliasName        = "hvw_production";
            PlayerSettings.Android.keyaliasPass        = "Shivraj@2026";
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            var opts = new BuildPlayerOptions
            {
                scenes = GetScenes(), locationPathName = apkPath,
                target = BuildTarget.Android, options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log(report.summary.result == BuildResult.Succeeded
                ? $"[HVW_Batch] Android APK SUCCEEDED: {apkPath}  ({report.summary.totalSize / 1024 / 1024}MB)"
                : $"[HVW_Batch] Android APK FAILED: {report.summary.result}");
        }

        // ── FULL PIPELINE: apply + Windows + Android ─────────────────────────
        [MenuItem("HeavyVehiclesWorld/Batch - Full Pipeline (Steps 1+2 + Windows + Android)")]
        public static void FullPipeline()
        {
            ApplyAllSteps();
            BuildWindows();
            BuildAndroid();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string[] GetScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0) scenes.Add("Assets/Scenes/MainWorld.unity");
            return scenes.ToArray();
        }
    }
}
