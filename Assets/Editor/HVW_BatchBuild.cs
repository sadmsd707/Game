// HVW_BatchBuild.cs - In Assets/Editor/
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HVW_BatchBuild
{
    private static List<string> GetScenesList()
    {
        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled && File.Exists(s.path))
                scenes.Add(s.path);
        }

        if (scenes.Count == 0)
        {
            string[] found = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            foreach (var f in found)
            {
                string rel = "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
                scenes.Add(rel);
            }
        }
        return scenes;
    }

    [MenuItem("Tools/HeavyVehiclesWorld/📦 Build Production APK (Signed Release)")]
    public static void BuildProductionAPK()
    {
        Debug.Log("[HVW] Checking Android Build Target Support...");
        bool isSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log($"[HVW] Android Build Target Supported: {isSupported}");

        if (!isSupported)
        {
            Debug.LogError("[HVW] ❌ Android Build Support module is NOT installed in Unity! Please install Android Build Support in Unity Hub.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        // 1. Configure Android Paths (Prefer Unity's officially bundled SDK, NDK, and OpenJDK)
        string unityData = @"C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Data/PlaybackEngines/AndroidPlayer";
        string bundledSdk = Path.Combine(unityData, "SDK").Replace('\\', '/');
        string bundledNdk = Path.Combine(unityData, "NDK").Replace('\\', '/');
        string bundledJdk = Path.Combine(unityData, "OpenJDK").Replace('\\', '/');

        if (Directory.Exists(bundledSdk))
        {
            EditorPrefs.SetBool("SdkUseEmbedded", true);
            EditorPrefs.SetString("AndroidSdkRoot", bundledSdk);
        }
        else
        {
            string extSdk = "C:/Users/Shivraj Deshmukh/AppData/Local/Android/Sdk";
            if (Directory.Exists(extSdk)) EditorPrefs.SetString("AndroidSdkRoot", extSdk);
        }

        if (Directory.Exists(bundledNdk))
        {
            EditorPrefs.SetBool("NdkUseEmbedded", true);
            EditorPrefs.SetString("AndroidNdkRootR27c", bundledNdk);
            EditorPrefs.SetString("AndroidNdkRoot", bundledNdk);
        }

        if (Directory.Exists(bundledJdk))
        {
            EditorPrefs.SetBool("JdkUseEmbedded", true);
            EditorPrefs.SetString("JdkPath", bundledJdk);
        }
        else
        {
            string extJdk = "C:/Program Files/Microsoft/jdk-17.0.20.101-hotspot";
            if (Directory.Exists(extJdk)) EditorPrefs.SetString("JdkPath", extJdk);
        }

        // 2. Configure Android Player Settings for Production
        PlayerSettings.companyName = "Shivraj Deshmukh";
        PlayerSettings.productName = "HeavyVehiclesWorld";
        PlayerSettings.applicationIdentifier = "com.heavyvehicles.world";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Android 7.0
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

        // IL2CPP & ARM64 Architecture
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // Graphics APIs: Vulkan, OpenGLES3
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
            UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
        });

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // Keystore
        string keystorePath = Path.Combine(Directory.GetCurrentDirectory(), "Keystore", "HVW_Production.keystore");
        if (File.Exists(keystorePath))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = "Shivraj@2026";
            PlayerSettings.Android.keyaliasName = "hvw_production";
            PlayerSettings.Android.keyaliasPass = "Shivraj@2026";
            Debug.Log("[HVW] Production Keystore attached and configured.");
        }

        // Build Settings
        EditorUserBuildSettings.buildAppBundle = false; // APK
        EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;

        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
        string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld_Production.apk");
        Directory.CreateDirectory(outputDir);

        // Ensure MainWorld scene is fully populated with all vehicles, camera, and mobile touch UI
        HeavyVehiclesWorld.EditorTools.HVW_SceneGenerator.GenerateAndSaveMainWorld();

        var scenes = GetScenesList();
        Debug.Log($"[HVW] Building Production APK: {outputPath}");
        Debug.Log($"[HVW] Scenes: {string.Join(", ", scenes)}");

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None // Signed production release
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            float sizeMB = summary.totalSize / 1048576f;
            Debug.Log($"[HVW] ✅ PRODUCTION APK CREATED! Size: {sizeMB:F1} MB -> {outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[HVW] ❌ BUILD FAILED: {summary.result} with {summary.totalErrors} errors.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/HeavyVehiclesWorld/🖥️ Build Windows Standalone")]
    public static void BuildWindows()
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
        string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld.exe");
        Directory.CreateDirectory(outputDir);

        var scenes = GetScenesList();

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            float sizeMB = summary.totalSize / 1048576f;
            Debug.Log($"[HVW_BatchBuild] ✅ BUILD SUCCEEDED! Size: {sizeMB:F1} MB -> {outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[HVW_BatchBuild] ❌ BUILD FAILED: {summary.result}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
