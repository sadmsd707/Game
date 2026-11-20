// HVW_AutoRunner.cs
// Fires automatically when Unity opens the project.
// Reads a flag file to decide what to run.
// Flag file: Assets/Editor/HVW_AutoRunFlag.txt
// Content: "FULLPIPELINE" or "BUILDONLY"

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace HeavyVehiclesWorld.Editor
{
    [InitializeOnLoad]
    public static class HVW_AutoRunner
    {
        private const string FLAG_FILE = "Assets/Editor/HVW_AutoRunFlag.txt";
        private static bool _scheduled = false;

        static HVW_AutoRunner()
        {
            // Check flag immediately on domain reload
            CheckAndSchedule();
        }

        private static void CheckAndSchedule()
        {
            string fullFlag = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", FLAG_FILE));

            if (!File.Exists(fullFlag)) return;
            if (_scheduled) return;
            _scheduled = true;

            string flag = File.ReadAllText(fullFlag).Trim();
            File.Delete(fullFlag); // delete immediately so it won't fire twice

            Debug.Log($"[HVW_AutoRunner] Flag '{flag}' detected — scheduling pipeline run...");

            // Delay execution to let Editor fully initialize (scene load, compilation, etc.)
            int delayCount = 0;
            EditorApplication.update += DelayedRun;

            void DelayedRun()
            {
                delayCount++;
                if (delayCount < 60) return; // wait ~1 second (60 editor update ticks)

                EditorApplication.update -= DelayedRun;

                // Make sure the main scene is open
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/MainWorld.unity");
                }

                Debug.Log("[HVW_AutoRunner] Running pipeline...");
                try
                {
                    RunPipeline(flag);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[HVW_AutoRunner] Pipeline error: {ex}");
                }
            }
        }

        private static void RunPipeline(string flag)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== HVW AutoRunner: {flag} ===");

            if (flag == "FULLPIPELINE" || flag == "STEP1")
            {
                try
                {
                    HVW_ModelImportHelper.ImportVehicleModels();
                    report.AppendLine("✅ Step 1 (models) complete");
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"⚠️ Step 1 error: {e.Message}");
                }
            }

            if (flag == "FULLPIPELINE" || flag == "STEP2")
            {
                try
                {
                    HVW_TerrainTextureImporter.ApplyTerrainTexturesAndSky();
                    report.AppendLine("✅ Step 2 (terrain+sky) complete");
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"⚠️ Step 2 error: {e.Message}");
                }
            }

            // Save scene
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.AppendLine("✅ Scene saved");

            if (flag == "FULLPIPELINE" || flag == "BUILDWINDOWS" || flag == "BUILDONLY")
            {
                try
                {
                    HVW_BatchAutomation.BuildWindows();
                    report.AppendLine("✅ Windows build complete");

                    // Run Windows build for screenshots
                    RunWindowsForScreenshots();
                    report.AppendLine("✅ Screenshot capture started");
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"⚠️ Windows build error: {e.Message}");
                }
            }

            if (flag == "FULLPIPELINE" || flag == "BUILDANDROID" || flag == "BUILDONLY")
            {
                try
                {
                    HVW_BatchAutomation.BuildAndroid();
                    report.AppendLine("✅ Android APK complete");
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"⚠️ Android build error: {e.Message}");
                }
            }

            Debug.Log(report.ToString());
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("HVW AutoRunner Complete", report.ToString(), "OK");
        }

        private static void RunWindowsForScreenshots()
        {
            string exePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "Builds", "Windows",
                             "HeavyVehiclesWorld.exe"));
            if (!File.Exists(exePath))
            {
                Debug.LogWarning($"[HVW_AutoRunner] Windows exe not found: {exePath}");
                return;
            }

            string shotDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "Builds", "Screenshots"));
            System.IO.Directory.CreateDirectory(shotDir);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName  = exePath,
                Arguments = "-hvwScreenshots",
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
            Debug.Log($"[HVW_AutoRunner] Windows standalone launched for screenshots → {shotDir}");
        }
    }
}
