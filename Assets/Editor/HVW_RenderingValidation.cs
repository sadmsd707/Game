#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.EditorTools;

namespace HeavyVehiclesWorld.EditorTests
{
    public static class HVW_RenderingValidation
    {
        public struct ScreenshotReport
        {
            public string ViewName;
            public int Width;
            public int Height;
            public string FilePath;
            public int MagentaPixels;
            public int WhitePatchPixels;
            public float MagentaPercentage;
            public float WhitePatchPercentage;
            public bool Passed;
        }

        [MenuItem("Tools/HeavyVehiclesWorld/📸 Run Phase 2 Rendering & Screenshot Validation")]
        public static void RunRenderingValidation()
        {
            int exitCode = 0;
            Debug.Log("================================================================================");
            Debug.Log("[PHASE 2 RENDERING VALIDATION] Automated Screenshot Check & Shader Verification");
            Debug.Log("================================================================================");

            Directory.CreateDirectory("Builds/Screenshots");
            var reports = new List<ScreenshotReport>();

            try
            {
                // 1. Run Scene Generator first to ensure all static assets, URP pipeline, and scene are up to date
                HVW_SceneGenerator.ConfigureAndSaveMainWorldScene();

                string scenePath = "Assets/Scenes/MainWorld.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid()) throw new Exception($"Failed to open scene: {scenePath}");

                // Locate Camera
                var camGo = GameObject.Find("Main Camera");
                if (camGo == null) throw new Exception("Main Camera not found in MainWorld.unity!");
                var cam = camGo.GetComponent<Camera>();

                // Initialize World & UI
                var initGo = GameObject.Find("WorldInitializer");
                if (initGo != null)
                {
                    var worldInit = initGo.GetComponent<HVW_WorldInitializer>();
                    if (worldInit != null)
                    {
                        worldInit.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
                    }
                }

                // 2. Validate URP Asset Assignment
                if (GraphicsSettings.defaultRenderPipeline == null)
                    throw new Exception("❌ GraphicsSettings.defaultRenderPipeline is NULL!");
                Debug.Log($"✅ Active Graphics Pipeline: {GraphicsSettings.defaultRenderPipeline.name}");

                for (int q = 0; q < QualitySettings.names.Length; q++)
                {
                    QualitySettings.SetQualityLevel(q, false);
                    if (QualitySettings.renderPipeline == null)
                        throw new Exception($"❌ QualitySettings tier {q} ({QualitySettings.names[q]}) renderPipeline is NULL!");
                }
                QualitySettings.SetQualityLevel(2, true); // High
                Debug.Log("✅ Confirmed URP pipeline asset assigned in Graphics Settings and all Quality Tiers.");

                // 3. Define 4 Camera Perspectives for Screenshot Analysis
                var views = new (string name, Vector3 pos, Vector3 rot, int width, int height)[]
                {
                    ("Overview_Horizon",     new Vector3(0f, 15f, -30f),  new Vector3(12f, 0f, 0f),   1920, 1080),
                    ("Construction_Mounds",  new Vector3(35f, 10f, 15f),  new Vector3(18f, 15f, 0f),  1920, 1080),
                    ("Quarry_RockZone",      new Vector3(-35f, 10f, 15f), new Vector3(18f, -15f, 0f), 1920, 1080),
                    ("HUD_Toolbar_1920x1080",new Vector3(0f, 3f, -8f),    new Vector3(10f, 0f, 0f),   1920, 1080),
                    ("HUD_Toolbar_1280x576", new Vector3(0f, 3f, -8f),    new Vector3(10f, 0f, 0f),   1280, 576),
                    ("HUD_Toolbar_2400x1080",new Vector3(0f, 3f, -8f),    new Vector3(10f, 0f, 0f),   2400, 1080)
                };

                foreach (var v in views)
                {
                    cam.transform.position = v.pos;
                    cam.transform.rotation = Quaternion.Euler(v.rot);

                    var report = CaptureAndAnalyzeView(cam, v.name, v.width, v.height);
                    reports.Add(report);
                }

                // 4. Verify UI Button Labels Readability
                ValidateUIButtonsReadability();

                // 5. Print Final Summary Table
                Debug.Log("\n================================================================================");
                Debug.Log("📊 PHASE 2 RENDERING & SCREENSHOT VALIDATION REPORT");
                Debug.Log("================================================================================");
                Debug.Log(string.Format("| {0,-24} | {1,-10} | {2,-14} | {3,-15} | {4,-6} |", "View Name", "Resolution", "Magenta Pixels", "White Patches", "Status"));
                Debug.Log("|--------------------------|------------|----------------|-----------------|--------|");

                bool allPassed = true;
                foreach (var r in reports)
                {
                    Debug.Log(string.Format("| {0,-24} | {1,5}x{2,-4} | {3,6} ({4:F2}%) | {5,6} ({6:F2}%) | {7,-6} |",
                        r.ViewName, r.Width, r.Height, r.MagentaPixels, r.MagentaPercentage, r.WhitePatchPixels, r.WhitePatchPercentage, r.Passed ? "PASS" : "FAIL"));
                    if (!r.Passed) allPassed = false;
                }
                Debug.Log("================================================================================");

                if (allPassed)
                {
                    Debug.Log("🎉 ALL SCREENSHOTS PASSED: ZERO MAGENTA / PINK AND ZERO BLANK WHITE PATCHES DETECTED!");
                }
                else
                {
                    Debug.LogError("⚠️ RENDERING VALIDATION FAILED ON ONE OR MORE CAPTURED VIEWS!");
                    exitCode = 1;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RENDERING VALIDATION CRASH] ❌ Error: {ex.Message}\n{ex.StackTrace}");
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

        private static ScreenshotReport CaptureAndAnalyzeView(Camera cam, string viewName, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            GameObject.DestroyImmediate(rt);

            // Save PNG to disk
            string filename = $"Builds/Screenshots/{viewName}_{width}x{height}.png";
            byte[] pngBytes = tex.EncodeToPNG();
            File.WriteAllBytes(filename, pngBytes);
            Debug.Log($"[Screenshot Saved] -> {filename} ({width}x{height}, {pngBytes.Length / 1024} KB)");

            // Analyze pixels for magenta/pink error shader and unshaded white patches
            var pixels = tex.GetPixels();
            GameObject.DestroyImmediate(tex);

            int totalPixels = pixels.Length;
            int magentaCount = 0;
            int whitePatchCount = 0;

            for (int i = 0; i < totalPixels; i++)
            {
                Color c = pixels[i];
                // Pure Magenta / Hot Pink (r > 0.75, g < 0.25, b > 0.75)
                if (c.r > 0.75f && c.g < 0.25f && c.b > 0.75f)
                {
                    magentaCount++;
                }
                // Blank unshaded white patches (r > 0.99, g > 0.99, b > 0.99)
                else if (c.r > 0.99f && c.g > 0.99f && c.b > 0.99f)
                {
                    whitePatchCount++;
                }
            }

            float magPct = ((float)magentaCount / totalPixels) * 100f;
            float whitePct = ((float)whitePatchCount / totalPixels) * 100f;

            // Strict threshold: Magenta must be 0, White patch < 0.05%
            bool pass = magentaCount == 0 && whitePct < 0.05f;

            return new ScreenshotReport
            {
                ViewName = viewName,
                Width = width,
                Height = height,
                FilePath = filename,
                MagentaPixels = magentaCount,
                WhitePatchPixels = whitePatchCount,
                MagentaPercentage = magPct,
                WhitePatchPercentage = whitePct,
                Passed = pass
            };
        }

        private static void ValidateUIButtonsReadability()
        {
            var canvasGo = GameObject.Find("HVW_MobileCanvas");
            if (canvasGo == null) return;

            string[] expectedButtons = new[]
            {
                "Btn_Select_Car",
                "Btn_Select_Tractor",
                "Btn_Select_Truck",
                "Btn_Select_JCB",
                "Btn_Select_Excavator",
                "Btn_Select_Harvester",
                "Btn_Settings",
                "Btn_ToggleDBG"
            };

            Debug.Log("\n--- Validating UI Button Labels Readability ---");
            foreach (var btnName in expectedButtons)
            {
                var btnTr = canvasGo.transform.Find(btnName);
                if (btnTr == null)
                {
                    Debug.LogWarning($"[UI Check] Button '{btnName}' not found on canvas!");
                    continue;
                }

                var textComp = btnTr.GetComponentInChildren<Text>();
                if (textComp == null)
                {
                    Debug.LogError($"❌ [UI Check] Button '{btnName}' has NO Text component!");
                    continue;
                }

                if (string.IsNullOrEmpty(textComp.text))
                {
                    Debug.LogError($"❌ [UI Check] Button '{btnName}' has EMPTY text!");
                }
                else
                {
                    Debug.Log($"✅ [UI Check] Button '{btnName}' text: '{textComp.text}', BestFit: {textComp.resizeTextForBestFit}, MinSize: {textComp.resizeTextMinSize}, MaxSize: {textComp.resizeTextMaxSize}");
                }
            }
        }
    }
}
#endif
