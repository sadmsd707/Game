// HVW_BatchBuild.cs - In Assets/Editor/
// Called via: -executeMethod HVW_BatchBuild.BuildWindows
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HVW_BatchBuild
{
    [MenuItem("Tools/HeavyVehiclesWorld/🖥️ Build Windows Standalone")]
    public static void BuildWindows()
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
        string outputPath = Path.Combine(outputDir, "HeavyVehiclesWorld.exe");
        Directory.CreateDirectory(outputDir);

        // Gather scenes
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

        // If still no scene exists, automatically create a default World scene
        if (scenes.Count == 0)
        {
            string scenesFolder = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(scenesFolder))
                Directory.CreateDirectory(scenesFolder);

            string scenePath = "Assets/Scenes/MainWorld.unity";
            Debug.Log($"[HVW_BatchBuild] Creating starter scene at {scenePath}...");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(500f, 1f, 500f);

            // Scene Bootstrapper & core managers
            var bootGO = new GameObject("SceneBootstrapper");
            bootGO.AddComponent<HeavyVehiclesWorld.Systems.SceneBootstrapper>();

            var gameMgrGO = new GameObject("GameManager");
            gameMgrGO.AddComponent<HeavyVehiclesWorld.Systems.GameManager>();

            var worldStateGO = new GameObject("WorldStateManager");
            worldStateGO.AddComponent<HeavyVehiclesWorld.World.WorldStateManager>();

            var dayNightGO = new GameObject("DayNightCycle");
            dayNightGO.AddComponent<HeavyVehiclesWorld.World.DayNightCycle>();

            var weatherGO = new GameObject("WeatherSystem");
            weatherGO.AddComponent<HeavyVehiclesWorld.World.WeatherSystem>();

            var zoneMgrGO = new GameObject("ZoneManager");
            zoneMgrGO.AddComponent<HeavyVehiclesWorld.World.ZoneManager>();

            var vehicleSpawnerGO = new GameObject("VehicleSpawner");
            vehicleSpawnerGO.AddComponent<HeavyVehiclesWorld.Systems.VehicleSpawner>();

            EditorSceneManager.SaveScene(scene, scenePath);
            scenes.Add(scenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[HVW_BatchBuild] Starting Windows build: {outputPath}");
        Debug.Log($"[HVW_BatchBuild] Scenes included: {string.Join(", ", scenes)}");

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
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[HVW_BatchBuild] ❌ BUILD FAILED: {summary.result} with {summary.totalErrors} errors.");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
