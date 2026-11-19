#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.EditorTools
{
    public static class HVW_SceneGenerator
    {
        [MenuItem("Tools/HeavyVehiclesWorld/🏗️ 1. Setup URP Pipeline & Always Included Shaders")]
        public static void SetupURPPipelineAndShaders()
        {
            Debug.Log("================================================================================");
            Debug.Log("[HVW_SceneGenerator] 1. Configuring URP Pipeline Assets & Quality Settings...");
            Debug.Log("================================================================================");

            Directory.CreateDirectory("Assets/Settings");

            // 1. Create or Load UniversalRendererData
            string rendererPath = "Assets/Settings/URP_Mobile_Renderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                Debug.Log($"[HVW] Created URP Renderer Data: {rendererPath}");
            }

            // 2. Create or Load UniversalRenderPipelineAsset
            string urpAssetPath = "Assets/Settings/URP_Mobile_Asset.asset";
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpAssetPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urpAsset, urpAssetPath);
                Debug.Log($"[HVW] Created URP Pipeline Asset: {urpAssetPath}");
            }

            // Configure URP Pipeline Asset for High-Quality Mobile
            urpAsset.supportsHDR = true;
            urpAsset.msaaSampleCount = 4;
            urpAsset.renderScale = 1.0f;
            urpAsset.shadowDistance = 100f;
            urpAsset.shadowCascadeCount = 2;
            EditorUtility.SetDirty(urpAsset);

            // 3. Assign to GraphicsSettings.defaultRenderPipeline
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            Debug.Log($"[HVW] Assigned GraphicsSettings.defaultRenderPipeline -> {urpAsset.name}");

            // 4. Assign to QualitySettings across all tiers
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                QualitySettings.renderPipeline = urpAsset;
            }
            QualitySettings.SetQualityLevel(2, true); // Default High
            Debug.Log("[HVW] Assigned QualitySettings.renderPipeline across all quality tiers.");

            // 5. Always Included Shaders in GraphicsSettings
            ConfigureAlwaysIncludedShaders();

            // 6. Create ShaderVariantCollection
            CreateShaderVariantCollection();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ URP Pipeline Asset, Quality Settings & Shaders successfully configured!");
        }

        private static void ConfigureAlwaysIncludedShaders()
        {
            string[] shaderNames = new string[]
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Terrain/Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Skybox/Procedural",
                "Skybox/Panoramic"
            };

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj == null)
            {
                Debug.LogWarning("[HVW] Could not load GraphicsSettings.asset directly.");
                return;
            }

            var so = new SerializedObject(graphicsSettingsObj);
            var alwaysIncludedProp = so.FindProperty("m_AlwaysIncludedShaders");
            if (alwaysIncludedProp != null)
            {
                var existingShaders = new HashSet<string>();
                for (int i = 0; i < alwaysIncludedProp.arraySize; i++)
                {
                    var elem = alwaysIncludedProp.GetArrayElementAtIndex(i);
                    var s = elem.objectReferenceValue as Shader;
                    if (s != null) existingShaders.Add(s.name);
                }

                foreach (var sName in shaderNames)
                {
                    var s = Shader.Find(sName);
                    if (s != null && !existingShaders.Contains(sName))
                    {
                        int newIdx = alwaysIncludedProp.arraySize;
                        alwaysIncludedProp.InsertArrayElementAtIndex(newIdx);
                        alwaysIncludedProp.GetArrayElementAtIndex(newIdx).objectReferenceValue = s;
                        Debug.Log($"[HVW] Added to Always Included Shaders: {sName}");
                    }
                }
                so.ApplyModifiedProperties();
            }
        }

        private static void CreateShaderVariantCollection()
        {
            string svcPath = "Assets/Settings/HVW_ShaderVariants.shadervariants";
            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(svcPath);
            if (svc == null)
            {
                svc = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(svc, svcPath);
            }

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                AddVariant(svc, litShader, PassType.ScriptableRenderPipeline, new[] { "_NORMALMAP", "_MAIN_LIGHT_SHADOWS" });
                AddVariant(svc, litShader, PassType.ScriptableRenderPipeline, new[] { "_NORMALMAP", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT" });
                AddVariant(svc, litShader, PassType.ScriptableRenderPipeline, new string[0]);
            }

            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null)
            {
                AddVariant(svc, terrainShader, PassType.ScriptableRenderPipeline, new[] { "_TERRAIN_NORMAL_MAP", "_MAIN_LIGHT_SHADOWS" });
                AddVariant(svc, terrainShader, PassType.ScriptableRenderPipeline, new[] { "_MAIN_LIGHT_SHADOWS" });
                AddVariant(svc, terrainShader, PassType.ScriptableRenderPipeline, new string[0]);
            }

            EditorUtility.SetDirty(svc);
            Debug.Log($"[HVW] Populated ShaderVariantCollection at: {svcPath} (Variant Count: {svc.variantCount})");
        }

        private static void AddVariant(ShaderVariantCollection svc, Shader shader, PassType passType, string[] keywords)
        {
            try
            {
                var variant = new ShaderVariantCollection.ShaderVariant
                {
                    shader = shader,
                    passType = passType,
                    keywords = keywords
                };
                svc.Add(variant);
            }
            catch { }
        }

        [MenuItem("Tools/HeavyVehiclesWorld/🎨 2. Generate Static Materials & Terrain Assets")]
        public static void GenerateStaticAssets()
        {
            Debug.Log("================================================================================");
            Debug.Log("[HVW_SceneGenerator] 2. Generating Static Materials & Terrain Assets on Disk...");
            Debug.Log("================================================================================");

            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Terrain");

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Procedural PBR Textures
            var grassAlbedo = CreateAndSaveTexture("Assets/Terrain/Tex_Grass_Albedo.png", new Color(0.26f, 0.50f, 0.22f));
            var dirtAlbedo  = CreateAndSaveTexture("Assets/Terrain/Tex_Dirt_Albedo.png",  new Color(0.48f, 0.35f, 0.20f));
            var asphAlbedo  = CreateAndSaveTexture("Assets/Terrain/Tex_Asphalt_Albedo.png", new Color(0.18f, 0.18f, 0.20f));
            var rockAlbedo  = CreateAndSaveTexture("Assets/Terrain/Tex_Rock_Albedo.png",  new Color(0.42f, 0.42f, 0.45f));
            var normalMap   = CreateAndSaveNormalMap("Assets/Terrain/Tex_NormalMap.png");

            // 2. Pre-Baked Static Materials
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Car_Body.mat", litShader, new Color(0.12f, 0.42f, 0.85f), 0.75f, 0.85f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Truck_Body.mat", litShader, new Color(0.88f, 0.88f, 0.90f), 0.35f, 0.65f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Tractor_Body.mat", litShader, new Color(0.16f, 0.62f, 0.22f), 0.25f, 0.60f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_JCB_Body.mat", litShader, new Color(0.98f, 0.78f, 0.05f), 0.20f, 0.55f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Excavator_Body.mat", litShader, new Color(0.95f, 0.45f, 0.08f), 0.25f, 0.50f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Harvester_Body.mat", litShader, new Color(0.12f, 0.52f, 0.18f), 0.30f, 0.60f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Rubber_Tires.mat", litShader, new Color(0.10f, 0.10f, 0.10f), 0.02f, 0.25f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_MetalParts.mat", litShader, new Color(0.25f, 0.26f, 0.28f), 0.85f, 0.65f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Glass.mat", litShader, new Color(0.15f, 0.20f, 0.25f, 0.70f), 0.10f, 0.95f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Asphalt_Road.mat", litShader, new Color(0.18f, 0.18f, 0.20f), 0.05f, 0.20f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Road_Stripe.mat", litShader, new Color(0.95f, 0.85f, 0.15f), 0.10f, 0.30f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Concrete_Curbs.mat", litShader, new Color(0.80f, 0.25f, 0.25f), 0.02f, 0.15f);
            // Construction Dirt: Natural Earth Brown (NOT RED!)
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_ConstructionDirt.mat", litShader, new Color(0.48f, 0.36f, 0.22f), 0.00f, 0.15f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_QuarryGravel.mat", litShader, new Color(0.42f, 0.42f, 0.45f), 0.05f, 0.20f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_WheatField.mat", litShader, new Color(0.82f, 0.68f, 0.25f), 0.00f, 0.20f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_TreeWood.mat", litShader, new Color(0.35f, 0.22f, 0.12f), 0.00f, 0.25f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_TreeLeaves.mat", litShader, new Color(0.18f, 0.48f, 0.15f), 0.00f, 0.30f);
            CreatePBRMaterialAsset("Assets/Materials/M_PBR_Mound.mat", litShader, new Color(0.44f, 0.32f, 0.18f), 0.00f, 0.15f);

            // Calibrated Procedural Skybox Material (natural horizon haze, removes yellow-green band)
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                string skyMatPath = "Assets/Materials/M_PBR_Skybox.mat";
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyMatPath);
                if (skyMat == null)
                {
                    skyMat = new Material(skyShader) { name = "M_PBR_Skybox" };
                    AssetDatabase.CreateAsset(skyMat, skyMatPath);
                }
                skyMat.shader = skyShader;
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_SunSizeConvergence", 5f);
                skyMat.SetFloat("_AtmosphereThickness", 1.0f);
                skyMat.SetColor("_SkyTint", new Color(0.42f, 0.65f, 0.90f));
                // Clean distant horizon atmospheric haze color (removes yellow-green band)
                skyMat.SetColor("_GroundColor", new Color(0.68f, 0.72f, 0.78f));
                skyMat.SetFloat("_Exposure", 1.0f);
                EditorUtility.SetDirty(skyMat);
            }

            // 3. Static TerrainLayer Assets
            var grassLayer = CreateOrLoadTerrainLayer("Assets/Terrain/Layer_Grass.terrainlayer", grassAlbedo, normalMap, new Vector2(10f, 10f), 0.2f, 0.0f);
            var dirtLayer  = CreateOrLoadTerrainLayer("Assets/Terrain/Layer_Dirt.terrainlayer",  dirtAlbedo,  normalMap, new Vector2(8f, 8f),   0.15f, 0.0f);
            var asphLayer  = CreateOrLoadTerrainLayer("Assets/Terrain/Layer_Asphalt.terrainlayer", asphAlbedo, normalMap, new Vector2(12f, 12f), 0.25f, 0.05f);
            var rockLayer  = CreateOrLoadTerrainLayer("Assets/Terrain/Layer_Rock.terrainlayer",  rockAlbedo,  normalMap, new Vector2(14f, 14f), 0.18f, 0.05f);

            // 4. Static Terrain Material (URP Terrain/Lit)
            var terrainLitShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainLitShader == null) terrainLitShader = Shader.Find("Nature/Terrain/Standard");
            string terrainMatPath = "Assets/Terrain/M_Terrain_Lit.mat";
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(terrainMatPath);
            if (terrainMat == null)
            {
                terrainMat = new Material(terrainLitShader) { name = "M_Terrain_Lit" };
                AssetDatabase.CreateAsset(terrainMat, terrainMatPath);
            }
            terrainMat.shader = terrainLitShader;
            EditorUtility.SetDirty(terrainMat);

            // 5. Pre-Baked TerrainData Asset with Painted Splatmap
            string terrainDataPath = "Assets/Terrain/MainWorld_TerrainData.asset";
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData();
                AssetDatabase.CreateAsset(terrainData, terrainDataPath);
            }
            terrainData.heightmapResolution = 257;
            terrainData.size = new Vector3(800f, 50f, 800f);
            terrainData.alphamapResolution = 256;
            terrainData.terrainLayers = new TerrainLayer[] { grassLayer, dirtLayer, asphLayer, rockLayer };

            BakeTerrainSplatmap(terrainData);
            EditorUtility.SetDirty(terrainData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ All Materials, Textures, TerrainLayers, and TerrainData saved to Assets!");
        }

        private static void BakeTerrainSplatmap(TerrainData td)
        {
            int res = td.alphamapResolution;
            float[,,] maps = new float[res, res, 4];
            float terrainWidth = td.size.x;
            float terrainLength = td.size.z;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = ((float)x / res) * terrainWidth - (terrainWidth * 0.5f);
                    float wz = ((float)y / res) * terrainLength - (terrainLength * 0.5f);

                    float grass = 1.0f;
                    float dirt = 0.0f;
                    float asphalt = 0.0f;
                    float rock = 0.0f;

                    // Main Road & Cross Road
                    if (Mathf.Abs(wx) < 7.5f && Mathf.Abs(wz) < 200f)
                    {
                        asphalt = 1.0f; grass = 0.0f;
                    }
                    else if (Mathf.Abs(wz - 50f) < 7.5f && Mathf.Abs(wx) < 150f)
                    {
                        asphalt = 1.0f; grass = 0.0f;
                    }
                    // Construction Dirt Zone: (35, 35) size 60x60
                    else if (Mathf.Abs(wx - 35f) < 30f && Mathf.Abs(wz - 35f) < 30f)
                    {
                        dirt = 0.95f; grass = 0.05f;
                    }
                    // Quarry Rock Zone: (-35, 35) size 60x60
                    else if (Mathf.Abs(wx + 35f) < 30f && Mathf.Abs(wz - 35f) < 30f)
                    {
                        rock = 0.90f; dirt = 0.10f; grass = 0.0f;
                    }
                    // Farm Wheat Field: (45, -40) size 80x80
                    else if (Mathf.Abs(wx - 45f) < 40f && Mathf.Abs(wz + 40f) < 40f)
                    {
                        dirt = 0.65f; grass = 0.35f;
                    }

                    float total = grass + dirt + asphalt + rock;
                    maps[y, x, 0] = grass / total;
                    maps[y, x, 1] = dirt / total;
                    maps[y, x, 2] = asphalt / total;
                    maps[y, x, 3] = rock / total;
                }
            }
            td.SetAlphamaps(0, 0, maps);
        }

        private static Texture2D CreateAndSaveTexture(string path, Color col)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
                var px = new Color[64 * 64];
                for (int i = 0; i < px.Length; i++) px[i] = col;
                tex.SetPixels(px);
                tex.Apply();
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return tex;
        }

        private static Texture2D CreateAndSaveNormalMap(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
                var px = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        float nx = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.35f;
                        float ny = (Mathf.PerlinNoise(x * 0.15f + 50f, y * 0.15f + 50f) - 0.5f) * 0.35f;
                        Vector3 n = new Vector3(nx, ny, 1f).normalized;
                        px[y * 64 + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                // Set NormalMap texture type
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return tex;
        }

        private static Material CreatePBRMaterialAsset(string path, Shader shader, Color color, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static TerrainLayer CreateOrLoadTerrainLayer(string path, Texture2D diffuse, Texture2D normal, Vector2 tileSize, float smoothness, float metallic)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.tileSize = tileSize;
            layer.smoothness = smoothness;
            layer.metallic = metallic;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        [MenuItem("Tools/HeavyVehiclesWorld/🏗️ Setup MainWorld Scene")]
        public static void GenerateAndSaveMainWorld()
        {
            ConfigureAndSaveMainWorldScene();
        }

        [MenuItem("Tools/HeavyVehiclesWorld/🌐 3. Configure and Save MainWorld Scene")]
        public static void ConfigureAndSaveMainWorldScene()
        {
            Debug.Log("================================================================================");
            Debug.Log("[HVW_SceneGenerator] 3. Building and Saving MainWorld.unity with Static Assets...");
            Debug.Log("================================================================================");

            SetupURPPipelineAndShaders();
            GenerateStaticAssets();

            string scenePath = "Assets/Scenes/MainWorld.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 0. Remove obsolete legacy 'Ground' plane (which has built-in Standard shader rendering magenta)
            var oldGround = GameObject.Find("Ground");
            if (oldGround != null)
            {
                GameObject.DestroyImmediate(oldGround);
                Debug.Log("[HVW] Removed obsolete legacy 'Ground' plane.");
            }

            // 1. Terrain Setup
            var terrainGo = GameObject.Find("Terrain");
            if (terrainGo == null)
            {
                terrainGo = Terrain.CreateTerrainGameObject(null);
                terrainGo.name = "Terrain";
            }
            var terrain = terrainGo.GetComponent<Terrain>();
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/MainWorld_TerrainData.asset");
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Terrain/M_Terrain_Lit.mat");
            if (terrainData != null) terrain.terrainData = terrainData;
            if (terrainMat != null)
            {
                terrain.materialTemplate = terrainMat;
            }
            terrainGo.transform.position = new Vector3(-400f, 0f, -400f);

            var terrainCol = terrainGo.GetComponent<TerrainCollider>();
            if (terrainCol == null) terrainCol = terrainGo.AddComponent<TerrainCollider>();
            terrainCol.terrainData = terrainData;

            if (terrainGo.GetComponent<TerrainDeformer>() == null)
                terrainGo.AddComponent<TerrainDeformer>();

            // 2. Calibrated Sun Light
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo == null)
            {
                lightGo = new GameObject("Directional Light");
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
            }
            var dirLight = lightGo.GetComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1.0f, 0.98f, 0.95f);
            dirLight.intensity = 1.0f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowResolution = LightShadowResolution.High;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // 3. Skybox & Ambient (Natural Horizon, No Yellow-Green Band)
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Skybox.mat");
            if (skyMat != null) RenderSettings.skybox = skyMat;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.56f);
            RenderSettings.ambientGroundColor  = new Color(0.30f, 0.32f, 0.35f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.86f);
            RenderSettings.fogDensity = 0.0015f;

            // 4. World Initializer & Visual System Binding
            var initGo = GameObject.Find("WorldInitializer");
            if (initGo == null) initGo = new GameObject("WorldInitializer");

            var worldInit = initGo.GetComponent<HVW_WorldInitializer>();
            if (worldInit == null) worldInit = initGo.AddComponent<HVW_WorldInitializer>();

            var visSys = initGo.GetComponent<HVW_VisualSystem>();
            if (visSys == null) visSys = initGo.AddComponent<HVW_VisualSystem>();

            // Link pre-baked assets to VisualSystem slots
            visSys.sunLight = dirLight;
            visSys.customSkyboxMaterial = skyMat;
            visSys.targetTerrain = terrain;
            visSys.carBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Car_Body.mat");
            visSys.truckBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Truck_Body.mat");
            visSys.tractorBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Tractor_Body.mat");
            visSys.jcbBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_JCB_Body.mat");
            visSys.excavatorBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Excavator_Body.mat");
            visSys.harvesterBodyPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Harvester_Body.mat");
            visSys.rubberTiresPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Rubber_Tires.mat");
            visSys.metalPartsPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_MetalParts.mat");
            visSys.glassPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Glass.mat");
            visSys.asphaltRoadPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Asphalt_Road.mat");
            visSys.concreteCurbsPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Concrete_Curbs.mat");
            visSys.constructionPropsPBR = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_PBR_Construction_Props.mat");
            visSys.grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layer_Grass.terrainlayer");
            visSys.dirtLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layer_Dirt.terrainlayer");
            visSys.asphaltLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layer_Asphalt.terrainlayer");
            visSys.rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layer_Rock.terrainlayer");

            // 5. Main Camera & Follow Camera
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
            }
            if (camGo.GetComponent<HVW_FollowCamera>() == null)
                camGo.AddComponent<HVW_FollowCamera>();

            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.42f, 0.65f, 0.90f);

            var urpCam = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (urpCam == null) urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.renderShadows = true;

            // 6. Global Post-Processing Volume
            visSys.SetupPostProcessing();

            // 7. Light Probe Group
            visSys.SetupLightProbeGrid();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("🎉 [HVW_SceneGenerator] MainWorld.unity successfully configured, baked, and saved!");
        }
    }
}
#endif
