using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
#endif

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// BuildAutomation – editor script providing one-click Build APK and Build AAB menu items.
    /// Also implements IPreprocessBuildWithReport to run AndroidBuildValidator before every build.
    ///
    /// Menu items:
    ///   Tools → HeavyVehiclesWorld → 🔨 Build Debug APK
    ///   Tools → HeavyVehiclesWorld → 📦 Build Release AAB
    ///   Tools → HeavyVehiclesWorld → 🚀 Build & Run (Device)
    /// </summary>
#if UNITY_EDITOR
    public class BuildAutomation : IPreprocessBuildWithReport
    {
        // IPreprocessBuildWithReport — runs BEFORE Unity starts the build
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            Debug.Log("[HVW BuildAutomation] Pre-build validation running...");
            var result = AndroidBuildValidator.RunValidation();

            if (result.failCount > 0)
            {
                // Abort the build if critical settings are wrong
                throw new BuildFailedException(
                    $"[HVW BuildAutomation] Build aborted — {result.failCount} Android setting(s) failed validation.\n" +
                    result.failDetails +
                    "\nFix the issues and rebuild.");
            }

            Debug.Log("[HVW BuildAutomation] All validation checks passed. Proceeding with build...");
        }

        // ── Menu items ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/HeavyVehiclesWorld/🔨 Build Debug APK")]
        public static void BuildDebugAPK()
        {
            string outputDir  = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld_Debug.apk");

            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes           = GetScenePaths(),
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            EditorUserBuildSettings.buildAppBundle = false; // APK not AAB
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, outputPath);
        }

        [MenuItem("Tools/HeavyVehiclesWorld/📦 Build Release AAB")]
        public static void BuildReleaseAAB()
        {
            string outputDir  = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld_Release.aab");

            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes           = GetScenePaths(),
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = BuildOptions.None,
            };

            EditorUserBuildSettings.buildAppBundle = true; // AAB for Play Store
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, outputPath);
        }

        [MenuItem("Tools/HeavyVehiclesWorld/🚀 Build & Run on Device (Debug APK)")]
        public static void BuildAndRun()
        {
            string outputDir  = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld_DevRun.apk");
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes           = GetScenePaths(),
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = BuildOptions.Development | BuildOptions.AutoRunPlayer |
                                   BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler,
            };

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, outputPath);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static string[] GetScenePaths()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            if (scenes.Count == 0)
                Debug.LogWarning("[HVW BuildAutomation] No scenes in Build Settings! Add scenes via File → Build Settings.");

            return scenes.ToArray();
        }

        private static void HandleBuildReport(BuildReport report, string outputPath)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                float sizeMB = summary.totalSize / 1048576f;
                Debug.Log($"[HVW BuildAutomation] ✅ Build succeeded!\n" +
                          $"Path: {outputPath}\n" +
                          $"Size: {sizeMB:F1} MB\n" +
                          $"Time: {summary.totalTime.TotalSeconds:F1}s");

                if (EditorUtility.DisplayDialog("Build Complete!",
                    $"✅ Build succeeded!\n{Path.GetFileName(outputPath)}\nSize: {sizeMB:F1} MB",
                    "Open Folder", "Close"))
                    EditorUtility.RevealInFinder(outputPath);
            }
            else
            {
                Debug.LogError($"[HVW BuildAutomation] ❌ Build FAILED: {summary.result}\n" +
                               $"Errors: {summary.totalErrors}");
                EditorUtility.DisplayDialog("Build Failed",
                    $"❌ Build failed with {summary.totalErrors} error(s).\nCheck the Console for details.",
                    "OK");
            }
        }
    }
#endif
}
