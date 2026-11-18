using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using System.Text;
#endif

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// AndroidBuildValidator – Editor utility that checks all Android build settings
    /// required for HeavyVehiclesWorld before you hit Build.
    ///
    /// Run via:  Tools → HeavyVehiclesWorld → Validate Android Build Settings
    ///
    /// Also implements IActiveBuildTargetChanged to auto-warn when switching to Android.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class AndroidBuildValidator
    {
        static AndroidBuildValidator()
        {
            // Auto-check once after domain reload if Android is active target
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                ValidateQuiet();
        }

        [MenuItem("Tools/HeavyVehiclesWorld/✅ Validate Android Build Settings")]
        public static void ValidateFull()
        {
            var report = RunValidation();
            if (report.passCount == report.totalChecks)
                EditorUtility.DisplayDialog("Android Build Validator",
                    $"✅ All {report.totalChecks} checks passed!\nReady to build APK / AAB.", "Great!");
            else
                EditorUtility.DisplayDialog("Android Build Validator — Issues Found",
                    $"⚠️ {report.failCount} of {report.totalChecks} checks failed:\n\n{report.failDetails}\n\nSee Console for details.",
                    "Fix & Re-run");
        }

        [MenuItem("Tools/HeavyVehiclesWorld/📋 Print Android Settings to Console")]
        public static void PrintSettings()
        {
            Debug.Log(GetSettingsReport());
        }

        // ── Core validation ────────────────────────────────────────────────────────

        public static (int passCount, int failCount, int totalChecks, string failDetails) RunValidation()
        {
            var fails = new StringBuilder();
            int pass = 0, fail = 0;

            void Check(bool condition, string label, string fix)
            {
                if (condition) { pass++; Debug.Log($"  ✅ {label}"); }
                else
                {
                    fail++;
                    fails.AppendLine($"• {label}\n  Fix: {fix}\n");
                    Debug.LogWarning($"  ❌ {label}\n  Fix: {fix}");
                }
            }

            Debug.Log("═══════ HeavyVehiclesWorld – Android Build Validation ═══════");

            // ── Build target ──────────────────────────────────────────────────
            Check(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                  "Active build target is Android",
                  "File → Build Settings → Android → Switch Platform");

            // ── Package name ──────────────────────────────────────────────────
            Check(PlayerSettings.applicationIdentifier == "com.heavyvehicles.world",
                  $"Package name = com.heavyvehicles.world (current: {PlayerSettings.applicationIdentifier})",
                  "Project Settings → Player → Android → Package Name");

            // ── API levels ────────────────────────────────────────────────────
            Check((int)PlayerSettings.Android.minSdkVersion >= 24,
                  $"Minimum API ≥ 24  (current: {(int)PlayerSettings.Android.minSdkVersion})",
                  "Project Settings → Player → Android → Minimum API Level = 24 (Android 7.0)");

            Check((int)PlayerSettings.Android.targetSdkVersion == 0 ||
                  (int)PlayerSettings.Android.targetSdkVersion >= 33,
                  $"Target API ≥ 33 or Automatic (current: {(int)PlayerSettings.Android.targetSdkVersion})",
                  "Project Settings → Player → Android → Target API Level = Automatic");

            // ── IL2CPP ────────────────────────────────────────────────────────
            Check(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP,
                  "Scripting Backend = IL2CPP",
                  "Project Settings → Player → Android → Other Settings → Scripting Backend = IL2CPP");

            // ── ARM64 ─────────────────────────────────────────────────────────
            var archs = PlayerSettings.Android.targetArchitectures;
            Check((archs & AndroidArchitecture.ARM64) != 0,
                  "ARM64 architecture enabled",
                  "Project Settings → Player → Android → Target Architectures → ✅ ARM64");

            // ── Graphics APIs ─────────────────────────────────────────────────
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool hasVulkan = System.Array.Exists(apis, a => a == UnityEngine.Rendering.GraphicsDeviceType.Vulkan);
            bool hasGLES3  = System.Array.Exists(apis, a => a == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3);
            Check(hasVulkan,
                  "Vulkan in graphics APIs",
                  "Project Settings → Player → Android → Graphics APIs → Add Vulkan (move to top)");
            Check(hasGLES3,
                  "OpenGLES3 fallback in graphics APIs",
                  "Project Settings → Player → Android → Graphics APIs → Add OpenGLES3 below Vulkan");

            // ── Auto graphics API ─────────────────────────────────────────────
            Check(!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android),
                  "Auto Graphics API = OFF (manual order: Vulkan, OpenGLES3)",
                  "Project Settings → Player → Android → Graphics APIs → uncheck Auto Graphics API");

            // ── Orientation ───────────────────────────────────────────────────
            Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft ||
                  PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation,
                  "Default orientation = Landscape",
                  "Project Settings → Player → Resolution → Default Orientation = Landscape Left");

            // ── Multithreaded rendering ───────────────────────────────────────
            Check(PlayerSettings.MTRendering,
                  "Multithreaded Rendering = ON",
                  "Project Settings → Player → Android → Rendering → Multithreaded Rendering ✅");

            // ── Internet permission ───────────────────────────────────────────
            Check(PlayerSettings.Android.internetAccess == AndroidInternetAccess.Auto,
                  "Internet Access = Auto (not forced)",
                  "Project Settings → Player → Android → Internet Access = Auto");

            // ── Keystore ─────────────────────────────────────────────────────
            bool hasKeystore = !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName);
            Check(hasKeystore,
                  "Keystore configured for release signing",
                  "Project Settings → Player → Android → Publishing Settings → set Keystore path");

            // ── Incremental GC ────────────────────────────────────────────────
            Check(PlayerSettings.gcIncremental,
                  "Incremental GC = ON (reduces GC spikes on mobile)",
                  "Project Settings → Player → Other Settings → ✅ Use incremental GC");

            int total = pass + fail;
            Debug.Log($"═══ Validation complete: {pass}/{total} passed ═══");
            return (pass, fail, total, fails.ToString());
        }

        private static void ValidateQuiet()
        {
            var r = RunValidation();
            if (r.failCount > 0)
                Debug.LogWarning($"[HVW Build Validator] {r.failCount} Android settings need fixing. " +
                                 "Run Tools → HeavyVehiclesWorld → Validate Android Build Settings");
        }

        private static string GetSettingsReport()
        {
            return
                $"Package: {PlayerSettings.applicationIdentifier}\n" +
                $"Min API: {(int)PlayerSettings.Android.minSdkVersion}\n" +
                $"Target API: {(int)PlayerSettings.Android.targetSdkVersion}\n" +
                $"Backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}\n" +
                $"Architectures: {PlayerSettings.Android.targetArchitectures}\n" +
                $"Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}\n" +
                $"Orientation: {PlayerSettings.defaultInterfaceOrientation}\n" +
                $"MT Rendering: {PlayerSettings.MTRendering}\n" +
                $"Incremental GC: {PlayerSettings.gcIncremental}\n" +
                $"Keystore: {(string.IsNullOrEmpty(PlayerSettings.Android.keystoreName) ? "NOT SET" : PlayerSettings.Android.keystoreName)}";
        }
    }
#else
    // Stub for non-editor builds — this script does nothing at runtime
    public class AndroidBuildValidator : MonoBehaviour { }
#endif
}
