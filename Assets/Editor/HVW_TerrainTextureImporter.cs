using System.IO;
using UnityEngine;
using UnityEditor;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.Editor
{
    /// <summary>
    /// Step 2 — Terrain Texture & Sky Importer.
    ///
    /// Loads the PBR PNGs downloaded from Poly Haven (CC0) into Unity TerrainLayers
    /// and swaps the procedural skybox for a real HDRI panoramic sky.
    ///
    /// Run via menu: HeavyVehiclesWorld → Step 2 → Apply Terrain Textures & Sky
    ///
    /// Asset paths expected (relative to Assets/):
    ///   Textures/Terrain/grass_diff_1k.png   + grass_nor_1k.png   + grass_rough_1k.png
    ///   Textures/Terrain/dirt_diff_1k.png    + dirt_nor_1k.png    + dirt_rough_1k.png
    ///   Textures/Terrain/asphalt_diff_1k.png + asphalt_nor_1k.png + asphalt_rough_1k.png
    ///   Textures/Terrain/rock_diff_1k.png    + rock_nor_1k.png    + rock_rough_1k.png
    ///   Textures/Skybox/autumn_field_puresky_1k.hdr
    /// </summary>
    public static class HVW_TerrainTextureImporter
    {
        private const string TERRAIN_DIR = "Assets/Textures/Terrain";
        private const string SKYBOX_DIR  = "Assets/Textures/Skybox";

        [MenuItem("HeavyVehiclesWorld/Step 2 - Apply Terrain Textures & Sky")]
        public static void ApplyTerrainTexturesAndSky()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Step 2 Terrain Texture & Sky Report ===\n");

            // 1. Force re-import all PNGs with correct settings
            SetTextureImportSettings();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Find the terrain in the scene
            var terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                report.AppendLine("❌ No Terrain found in scene. Run HVW_SceneGenerator first.");
                Debug.LogError(report.ToString());
                EditorUtility.DisplayDialog("Step 2", report.ToString(), "OK");
                return;
            }

            // 3. Build terrain layers from the downloaded textures
            var layers = new TerrainLayer[4];

            var layerDefs = new[]
            {
                new { name="grass",   tile=new Vector2(8f,8f),   smooth=0.15f, metal=0.00f },
                new { name="dirt",    tile=new Vector2(6f,6f),   smooth=0.10f, metal=0.00f },
                new { name="asphalt", tile=new Vector2(10f,10f), smooth=0.30f, metal=0.05f },
                new { name="rock",    tile=new Vector2(12f,12f), smooth=0.12f, metal=0.02f },
            };

            for (int i = 0; i < layerDefs.Length; i++)
            {
                var def      = layerDefs[i];
                var layer    = new TerrainLayer();
                layer.name   = $"Layer_{char.ToUpper(def.name[0])}{def.name.Substring(1)}_PBR";
                layer.tileSize  = def.tile;
                layer.smoothness = def.smooth;
                layer.metallic   = def.metal;

                // Diffuse
                var diff = LoadTex($"{TERRAIN_DIR}/{def.name}_diff_1k.png", false);
                if (diff != null) { layer.diffuseTexture = diff; report.AppendLine($"✅ {def.name} diffuse loaded"); }
                else              { report.AppendLine($"⚠️  {def.name}_diff_1k.png missing — procedural fallback"); layer.diffuseTexture = ProceduralFallback(def.name); }

                // Normal
                var nor = LoadTex($"{TERRAIN_DIR}/{def.name}_nor_1k.png", true);
                if (nor != null) { layer.normalMapTexture = nor; report.AppendLine($"✅ {def.name} normal loaded"); }

                // Roughness (stored in maskMapTexture alpha on URP terrain)
                var rough = LoadTex($"{TERRAIN_DIR}/{def.name}_rough_1k.png", false);
                if (rough != null) { layer.maskMapTexture = rough; report.AppendLine($"✅ {def.name} roughness loaded"); }

                // Save as asset so it persists across domain reloads
                string layerPath = $"Assets/Terrain/Layer_{def.name}_PBR.terrainlayer";
                AssetDatabase.CreateAsset(layer, layerPath);
                layers[i] = layer;
            }

            // 4. Apply layers to terrain
            var td = terrain.terrainData;
            td.terrainLayers = layers;
            EditorUtility.SetDirty(td);
            report.AppendLine("\n✅ Terrain layers applied to TerrainData.");

            // 5. Sky — load HDR and create a Cubemap-based skybox material
            ApplyHDRISky(report);

            // 6. Fix horizon band by tuning fog and ambient
            FixHorizonAndAmbient(report);

            // 7. Update HVW_VisualSystem references
            UpdateVisualSystemRefs(terrain, layers, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Step 2 — Complete", report.ToString(), "OK");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void SetTextureImportSettings()
        {
            // Set correct import settings for each texture type
            string[] pngs = System.IO.Directory.GetFiles(
                Path.Combine(Application.dataPath, "..", TERRAIN_DIR), "*.png");

            foreach (var fullPath in pngs)
            {
                string assetPath = "Assets/" + fullPath.Replace(Application.dataPath + "/", "")
                    .Replace(Application.dataPath.Replace("/", "\\") + "\\", "")
                    .Replace("\\", "/");

                // Normalize path
                assetPath = assetPath.Replace(Application.dataPath.TrimEnd('/').TrimEnd('\\'), "Assets");
                if (!assetPath.StartsWith("Assets/")) assetPath = TERRAIN_DIR + "/" + Path.GetFileName(fullPath);

                var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (imp == null) continue;

                bool isNormal = Path.GetFileName(fullPath).Contains("_nor_");
                imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = !isNormal;
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 4;
                imp.wrapMode = TextureWrapMode.Repeat;

                // Mobile compression
                var androidSet = imp.GetPlatformTextureSettings("Android");
                androidSet.overridden = true;
                androidSet.format = isNormal
                    ? TextureImporterFormat.ASTC_6x6
                    : TextureImporterFormat.ASTC_4x4;
                androidSet.maxTextureSize = 1024;
                imp.SetPlatformTextureSettings(androidSet);
                imp.SaveAndReimport();
            }
        }

        private static Texture2D LoadTex(string assetPath, bool isNormal)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) return tex;
            // Try to import it
            if (File.Exists(Path.Combine(Application.dataPath, "..", assetPath)))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            return null;
        }

        private static Texture2D ProceduralFallback(string name)
        {
            var colors = new System.Collections.Generic.Dictionary<string, Color>
            {
                { "grass",   new Color(0.26f, 0.50f, 0.22f) },
                { "dirt",    new Color(0.48f, 0.35f, 0.20f) },
                { "asphalt", new Color(0.18f, 0.18f, 0.20f) },
                { "rock",    new Color(0.42f, 0.42f, 0.45f) }
            };
            var col = colors.ContainsKey(name) ? colors[name] : Color.grey;
            return HVW_VisualSystem.CreateProceduralTexture(col, 64);
        }

        private static void ApplyHDRISky(System.Text.StringBuilder report)
        {
            string hdrPath = $"{SKYBOX_DIR}/autumn_field_puresky_1k.hdr";
            var hdri = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
            if (hdri == null)
            {
                AssetDatabase.ImportAsset(hdrPath, ImportAssetOptions.ForceSynchronousImport);
                hdri = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
            }

            if (hdri == null)
            {
                report.AppendLine($"⚠️  HDRI not found at {hdrPath} — procedural sky stays. Download autumn_field_puresky_1k.hdr from polyhaven.com");
                return;
            }

            // Set HDRI import settings
            var imp = AssetImporter.GetAtPath(hdrPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.textureShape = TextureImporterShape.Texture2D;
                imp.sRGBTexture  = false;
                imp.mipmapEnabled = true;
                imp.filterMode   = FilterMode.Trilinear;
                imp.anisoLevel   = 1;
                imp.SaveAndReimport();
                hdri = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
            }

            // Create a Skybox/Panoramic material
            var skyShader = Shader.Find("Skybox/Panoramic");
            if (skyShader == null) skyShader = Shader.Find("Skybox/Cubemap");

            if (skyShader == null)
            {
                report.AppendLine("⚠️  Skybox/Panoramic shader not found — keeping procedural sky");
                return;
            }

            var skyMat = new Material(skyShader) { name = "HDRISkybox_AutumnField" };
            if (skyMat.HasProperty("_MainTex"))  skyMat.SetTexture("_MainTex", hdri);
            if (skyMat.HasProperty("_Tex"))      skyMat.SetTexture("_Tex", hdri);
            if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.2f);
            if (skyMat.HasProperty("_Rotation")) skyMat.SetFloat("_Rotation", 180f);

            string matPath = "Assets/Textures/Skybox/HDRISkybox_AutumnField.mat";
            AssetDatabase.CreateAsset(skyMat, matPath);

            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

            report.AppendLine($"✅ HDRI sky applied: {hdrPath}");
            report.AppendLine("✅ Ambient mode set to Skybox.");
        }

        private static void FixHorizonAndAmbient(System.Text.StringBuilder report)
        {
            // Fix yellow-green horizon band: tune exponential fog
            RenderSettings.fog      = true;
            RenderSettings.fogMode  = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.85f);  // light cool blue-grey
            RenderSettings.fogDensity = 0.004f;

            // Ambient light — softer, sky-sourced
            RenderSettings.ambientIntensity = 1.0f;

            // Sun angle: high and slightly warm, not orange/yellow
            var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color     = new Color(1.0f, 0.97f, 0.90f);
                sun.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
                sun.shadows   = LightShadows.Soft;
                report.AppendLine("✅ Sun light tuned (colour, angle, soft shadows).");
            }
            report.AppendLine("✅ Fog tuned (ExponentialSquared, cool blue-grey, density 0.004).");
        }

        private static void UpdateVisualSystemRefs(Terrain terrain, TerrainLayer[] layers,
                                                   System.Text.StringBuilder report)
        {
            var vs = Object.FindAnyObjectByType<HVW_VisualSystem>();
            if (vs == null) { report.AppendLine("⚠️  HVW_VisualSystem not found in scene."); return; }

            vs.targetTerrain = terrain;
            if (layers.Length > 0) vs.grassLayer   = layers[0];
            if (layers.Length > 1) vs.dirtLayer    = layers[1];
            if (layers.Length > 2) vs.asphaltLayer = layers[2];
            if (layers.Length > 3) vs.rockLayer    = layers[3];

            EditorUtility.SetDirty(vs);
            report.AppendLine("✅ HVW_VisualSystem terrain layer references updated.");
        }
    }
}
