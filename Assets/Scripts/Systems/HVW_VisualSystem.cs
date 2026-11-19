using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Master Visual & PBR System for HeavyVehiclesWorld.
    /// Handles PBR URP/Lit material configuration with texture slots (Albedo, Normal, Smoothness/Metallic),
    /// balanced daylight lighting (removes yellow blowout, soft shadows, HDRI skybox, light probes),
    /// TerrainLayer configuration with slots for textures, grass detail, and Terrain Tree system.
    /// </summary>
    [ExecuteAlways]
    public class HVW_VisualSystem : MonoBehaviour
    {
        public static HVW_VisualSystem Instance { get; private set; }

        [Header("Lighting Settings")]
        [Tooltip("Target Directional Light for sun")]
        public Light sunLight;
        public Color daylightColor = new Color(1.0f, 0.98f, 0.95f);
        public float daylightIntensity = 1.0f;
        public Vector3 sunAngles = new Vector3(48f, -32f, 0f);

        [Header("Skybox & Ambient")]
        public Material customSkyboxMaterial;
        public Texture2D hdriPanoramicTexture;

        [Header("PBR Material Slots")]
        public Material carBodyPBR;
        public Material truckBodyPBR;
        public Material tractorBodyPBR;
        public Material jcbBodyPBR;
        public Material excavatorBodyPBR;
        public Material harvesterBodyPBR;
        public Material rubberTiresPBR;
        public Material metalPartsPBR;
        public Material glassPBR;
        public Material asphaltRoadPBR;
        public Material concreteCurbsPBR;
        public Material constructionPropsPBR;

        [Header("Terrain Layer Slots")]
        public Terrain targetTerrain;
        public TerrainLayer grassLayer;
        public TerrainLayer dirtLayer;
        public TerrainLayer asphaltLayer;
        public TerrainLayer rockLayer;

        [Header("Terrain Tree & Detail Prototypes")]
        public GameObject treePrototypePrefab;
        public Texture2D grassDetailTexture;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ApplyCalibratedLighting();
            InitializePBRMaterials();
            ConfigureTerrainSystem();
            SetupLightProbeGrid();
            SetupPostProcessing();
            CheckMissingAssetsWarnings();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. CALIBRATED LIGHTING & SKYBOX (REMOVES YELLOW BLOWOUT)
        // ══════════════════════════════════════════════════════════════════════
        public void ApplyCalibratedLighting()
        {
            if (sunLight == null)
            {
                var lightGo = GameObject.Find("Directional Light");
                if (lightGo != null) sunLight = lightGo.GetComponent<Light>();
                else
                {
                    lightGo = new GameObject("Directional Light");
                    sunLight = lightGo.AddComponent<Light>();
                    sunLight.type = LightType.Directional;
                }
            }

            if (sunLight != null)
            {
                sunLight.type = LightType.Directional;
                sunLight.color = daylightColor;
                sunLight.intensity = daylightIntensity;
                sunLight.transform.rotation = Quaternion.Euler(sunAngles);

                // Calibrated Soft Shadows
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowResolution = LightShadowResolution.High;
                sunLight.shadowStrength = 0.78f;
                sunLight.shadowBias = 0.05f;
                sunLight.shadowNormalBias = 0.4f;
            }

            // Skybox setup: custom material -> HDRI panorama -> calibrated procedural sky
            if (customSkyboxMaterial != null && customSkyboxMaterial.shader != null && customSkyboxMaterial.shader.isSupported)
            {
                RenderSettings.skybox = customSkyboxMaterial;
            }
            else if (hdriPanoramicTexture != null)
            {
                var panoShader = Shader.Find("Skybox/Panoramic");
                if (panoShader != null)
                {
                    var panoMat = new Material(panoShader);
                    panoMat.SetTexture("_MainTex", hdriPanoramicTexture);
                    panoMat.SetFloat("_Exposure", 1.0f);
                    RenderSettings.skybox = panoMat;
                }
            }
            else
            {
                var proceduralSkyShader = Shader.Find("Skybox/Procedural");
                if (proceduralSkyShader != null)
                {
                    var skyMat = new Material(proceduralSkyShader);
                    skyMat.SetFloat("_SunSize", 0.04f);
                    skyMat.SetFloat("_AtmosphereThickness", 1.0f);
                    skyMat.SetColor("_SkyTint", new Color(0.42f, 0.65f, 0.90f));
                    skyMat.SetColor("_GroundColor", new Color(0.26f, 0.34f, 0.22f));
                    skyMat.SetFloat("_Exposure", 1.0f);
                    RenderSettings.skybox = skyMat;
                }
            }

            // Ensure Main Camera clears safely and enables URP post processing
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(0.42f, 0.65f, 0.90f);
                var urpCam = cam.GetComponent<UniversalAdditionalCameraData>();
                if (urpCam != null)
                {
                    urpCam.renderPostProcessing = true;
                    urpCam.renderShadows = true;
                }
            }

            // Balanced Trilight Ambient (Natural Daylight)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.45f, 0.58f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.44f, 0.50f);
            RenderSettings.ambientGroundColor  = new Color(0.24f, 0.28f, 0.20f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. PBR MATERIAL FACTORY & SLOT CONFIGURATION
        // ══════════════════════════════════════════════════════════════════════
        public void InitializePBRMaterials()
        {
            Shader pbrShader = GetSafeShader();

            // Car: Metallic Cobalt Blue
            carBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_Car_Body",
                new Color(0.12f, 0.42f, 0.85f), metallic: 0.75f, smoothness: 0.85f);

            // Truck: Heavy Industrial White
            truckBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_Truck_Body",
                new Color(0.88f, 0.88f, 0.90f), metallic: 0.35f, smoothness: 0.65f);

            // Tractor: Agriculture Green
            tractorBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_Tractor_Body",
                new Color(0.16f, 0.62f, 0.22f), metallic: 0.25f, smoothness: 0.60f);

            // JCB: Construction Yellow
            jcbBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_JCB_Body",
                new Color(0.98f, 0.78f, 0.05f), metallic: 0.20f, smoothness: 0.55f);

            // Excavator: Industrial Orange
            excavatorBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_Excavator_Body",
                new Color(0.95f, 0.45f, 0.08f), metallic: 0.25f, smoothness: 0.50f);

            // Harvester: Combine Forest Green
            harvesterBodyPBR = CreatePBRMaterial(pbrShader, "M_PBR_Harvester_Body",
                new Color(0.12f, 0.52f, 0.18f), metallic: 0.30f, smoothness: 0.60f);

            // Rubber Tires: Matte Dark Grey/Black
            rubberTiresPBR = CreatePBRMaterial(pbrShader, "M_PBR_Rubber_Tires",
                new Color(0.10f, 0.10f, 0.10f), metallic: 0.02f, smoothness: 0.25f);

            // Metal Parts / Chassis / Rims: Dark Steel
            metalPartsPBR = CreatePBRMaterial(pbrShader, "M_PBR_MetalParts",
                new Color(0.25f, 0.26f, 0.28f), metallic: 0.85f, smoothness: 0.65f);

            // Glass: Tinted Reflective Glass
            glassPBR = CreatePBRMaterial(pbrShader, "M_PBR_Glass",
                new Color(0.15f, 0.20f, 0.25f, 0.70f), metallic: 0.10f, smoothness: 0.95f);

            // Road: Dark Asphalt
            asphaltRoadPBR = CreatePBRMaterial(pbrShader, "M_PBR_Asphalt_Road",
                new Color(0.18f, 0.18f, 0.20f), metallic: 0.05f, smoothness: 0.20f);

            // Curbs: Concrete Red / White
            concreteCurbsPBR = CreatePBRMaterial(pbrShader, "M_PBR_Concrete_Curbs",
                new Color(0.80f, 0.25f, 0.25f), metallic: 0.02f, smoothness: 0.15f);

            // Props: Construction Hazard Orange
            constructionPropsPBR = CreatePBRMaterial(pbrShader, "M_PBR_Construction_Props",
                new Color(1.0f, 0.45f, 0.05f), metallic: 0.10f, smoothness: 0.40f);
        }

        public static Shader GetSafeShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                s = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Mobile/Diffuse");
            if (s == null) s = Shader.Find("Diffuse");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        public static Texture2D CreateProceduralTexture(Color col, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateProceduralNormalMap(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.35f;
                    float ny = (Mathf.PerlinNoise(x * 0.15f + 50f, y * 0.15f + 50f) - 0.5f) * 0.35f;
                    Vector3 normal = new Vector3(nx, ny, 1f).normalized;
                    pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateProceduralGrassBladeTexture()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Color grassGreen = new Color(0.28f, 0.65f, 0.22f, 1f);
            Color transparent = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    bool isBlade = (Mathf.Abs(u - 0.5f) < (1f - v) * 0.18f) ||
                                  (Mathf.Abs(u - 0.35f - (1f - v) * 0.15f) < (1f - v) * 0.12f) ||
                                  (Mathf.Abs(u - 0.65f + (1f - v) * 0.15f) < (1f - v) * 0.12f);
                    pixels[y * size + x] = (isBlade && v < 0.95f) ? grassGreen : transparent;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public Material CreatePBRMaterial(Shader shader, string name, Color baseColor, float metallic, float smoothness)
        {
            if (shader == null || !shader.isSupported) shader = GetSafeShader();
            var mat = new Material(shader) { name = name };

            Texture2D tex = CreateProceduralTexture(baseColor, 32);
            Texture2D nrm = CreateProceduralNormalMap(32);

            // Both URP & Standard / Mobile compatible assignments
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", baseColor);

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            return mat;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. TERRAIN SYSTEM & TERRAIN LAYERS CONFIGURATION
        // ══════════════════════════════════════════════════════════════════════
        public void ConfigureTerrainSystem()
        {
            var terrainGo = GameObject.Find("Terrain");
            if (terrainGo == null)
            {
                var tObj = Object.FindAnyObjectByType<Terrain>();
                if (tObj != null) terrainGo = tObj.gameObject;
            }

            if (terrainGo != null)
            {
                targetTerrain = terrainGo.GetComponent<Terrain>();
            }

            var normalMap = CreateProceduralNormalMap(64);

            // Create TerrainLayers with procedural textures and normal maps
            if (grassLayer == null)
            {
                grassLayer = new TerrainLayer
                {
                    name = "Layer_Grass",
                    tileSize = new Vector2(10f, 10f),
                    smoothness = 0.2f,
                    metallic = 0.0f,
                    diffuseTexture = CreateProceduralTexture(new Color(0.26f, 0.50f, 0.22f), 64),
                    normalMapTexture = normalMap
                };
            }
            else if (grassLayer.diffuseTexture == null)
            {
                grassLayer.diffuseTexture = CreateProceduralTexture(new Color(0.26f, 0.50f, 0.22f), 64);
                if (grassLayer.normalMapTexture == null) grassLayer.normalMapTexture = normalMap;
            }

            if (dirtLayer == null)
            {
                dirtLayer = new TerrainLayer
                {
                    name = "Layer_Dirt",
                    tileSize = new Vector2(8f, 8f),
                    smoothness = 0.15f,
                    metallic = 0.0f,
                    diffuseTexture = CreateProceduralTexture(new Color(0.48f, 0.35f, 0.20f), 64),
                    normalMapTexture = normalMap
                };
            }
            else if (dirtLayer.diffuseTexture == null)
            {
                dirtLayer.diffuseTexture = CreateProceduralTexture(new Color(0.48f, 0.35f, 0.20f), 64);
                if (dirtLayer.normalMapTexture == null) dirtLayer.normalMapTexture = normalMap;
            }

            if (asphaltLayer == null)
            {
                asphaltLayer = new TerrainLayer
                {
                    name = "Layer_Asphalt",
                    tileSize = new Vector2(12f, 12f),
                    smoothness = 0.25f,
                    metallic = 0.05f,
                    diffuseTexture = CreateProceduralTexture(new Color(0.18f, 0.18f, 0.20f), 64),
                    normalMapTexture = normalMap
                };
            }
            else if (asphaltLayer.diffuseTexture == null)
            {
                asphaltLayer.diffuseTexture = CreateProceduralTexture(new Color(0.18f, 0.18f, 0.20f), 64);
                if (asphaltLayer.normalMapTexture == null) asphaltLayer.normalMapTexture = normalMap;
            }

            if (rockLayer == null)
            {
                rockLayer = new TerrainLayer
                {
                    name = "Layer_Rock",
                    tileSize = new Vector2(14f, 14f),
                    smoothness = 0.18f,
                    metallic = 0.05f,
                    diffuseTexture = CreateProceduralTexture(new Color(0.42f, 0.42f, 0.45f), 64),
                    normalMapTexture = normalMap
                };
            }
            else if (rockLayer.diffuseTexture == null)
            {
                rockLayer.diffuseTexture = CreateProceduralTexture(new Color(0.42f, 0.42f, 0.45f), 64);
                if (rockLayer.normalMapTexture == null) rockLayer.normalMapTexture = normalMap;
            }

            if (targetTerrain != null && targetTerrain.terrainData != null)
            {
                var td = targetTerrain.terrainData;
                td.terrainLayers = new TerrainLayer[]
                {
                    grassLayer,
                    dirtLayer,
                    asphaltLayer,
                    rockLayer
                };

                // Paint Terrain Alphamaps across road, construction, quarry, and landscape
                PaintTerrainAlphamaps(td);

                // Configure Terrain Tree Prototypes and Instances
                if (treePrototypePrefab == null)
                {
                    treePrototypePrefab = CreateProceduralTreePrefab();
                }
                var treeProto = new TreePrototype { prefab = treePrototypePrefab };
                td.treePrototypes = new TreePrototype[] { treeProto };
                PopulateTerrainTrees(targetTerrain);

                // Configure Terrain Detail Grass Prototypes and Layers
                if (grassDetailTexture == null)
                {
                    grassDetailTexture = CreateProceduralGrassBladeTexture();
                }
                var detailProto = new DetailPrototype
                {
                    prototypeTexture = grassDetailTexture,
                    renderMode = DetailRenderMode.GrassBillboard,
                    minWidth = 0.8f,
                    maxWidth = 1.4f,
                    minHeight = 0.8f,
                    maxHeight = 1.5f,
                    healthyColor = new Color(0.35f, 0.70f, 0.25f),
                    dryColor = new Color(0.65f, 0.60f, 0.28f)
                };
                td.detailPrototypes = new DetailPrototype[] { detailProto };
                PopulateGrassDetails(td);

                targetTerrain.drawTreesAndFoliage = true;
                targetTerrain.treeDistance = 600f;
                targetTerrain.treeBillboardDistance = 150f;
                targetTerrain.detailObjectDistance = 90f;
                targetTerrain.detailObjectDensity = 0.8f;
            }
        }

        private void PaintTerrainAlphamaps(TerrainData td)
        {
            int res = td.alphamapResolution;
            if (res <= 0) res = 128;
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

                    // Road Track: Asphalt
                    if (Mathf.Abs(wx) < 7.5f && Mathf.Abs(wz) < 200f)
                    {
                        asphalt = 1.0f;
                        grass = 0.0f;
                    }
                    else if (Mathf.Abs(wz - 50f) < 7.5f && Mathf.Abs(wx) < 150f)
                    {
                        asphalt = 1.0f;
                        grass = 0.0f;
                    }
                    // Construction Dirt Zone: (35, 35) size 60x60
                    else if (Mathf.Abs(wx - 35f) < 30f && Mathf.Abs(wz - 35f) < 30f)
                    {
                        dirt = 0.95f;
                        grass = 0.05f;
                    }
                    // Quarry Gravel / Rock Zone: (-35, 35) size 60x60
                    else if (Mathf.Abs(wx + 35f) < 30f && Mathf.Abs(wz - 35f) < 30f)
                    {
                        rock = 0.90f;
                        dirt = 0.10f;
                        grass = 0.0f;
                    }
                    // Farm Wheat Field: (45, -40) size 80x80
                    else if (Mathf.Abs(wx - 45f) < 40f && Mathf.Abs(wz + 40f) < 40f)
                    {
                        dirt = 0.65f;
                        grass = 0.35f;
                    }

                    float total = grass + dirt + asphalt + rock;
                    if (total > 0f)
                    {
                        maps[y, x, 0] = grass / total;
                        maps[y, x, 1] = dirt / total;
                        maps[y, x, 2] = asphalt / total;
                        maps[y, x, 3] = rock / total;
                    }
                    else
                    {
                        maps[y, x, 0] = 1.0f;
                    }
                }
            }
            td.SetAlphamaps(0, 0, maps);
        }

        private GameObject CreateProceduralTreePrefab()
        {
            var treeRoot = new GameObject("Prefab_Tree_Oak");
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeRoot.transform, false);
            trunk.transform.localPosition = new Vector3(0, 2.5f, 0);
            trunk.transform.localScale = new Vector3(0.7f, 2.5f, 0.7f);
            var woodMat = new Material(GetSafeShader()) { color = new Color(0.35f, 0.22f, 0.12f) };
            trunk.GetComponent<Renderer>().sharedMaterial = woodMat;

            var capCol = trunk.GetComponent<CapsuleCollider>();
            if (capCol == null) capCol = trunk.AddComponent<CapsuleCollider>();
            capCol.radius = 0.45f;
            capCol.height = 5.0f;
            capCol.center = Vector3.zero;

            var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(treeRoot.transform, false);
            foliage.transform.localPosition = new Vector3(0, 6.0f, 0);
            foliage.transform.localScale = new Vector3(4.5f, 4.0f, 4.5f);
            var leavesMat = new Material(GetSafeShader()) { color = new Color(0.18f, 0.48f, 0.15f) };
            foliage.GetComponent<Renderer>().sharedMaterial = leavesMat;
            var leafCol = foliage.GetComponent<Collider>();
            if (leafCol != null)
            {
                if (Application.isPlaying) Destroy(leafCol);
                else DestroyImmediate(leafCol);
            }

            treeRoot.SetActive(false);
            return treeRoot;
        }

        private void PopulateTerrainTrees(Terrain terrain)
        {
            var td = terrain.terrainData;
            if (td.treePrototypes == null || td.treePrototypes.Length == 0) return;

            var treeInstances = new List<TreeInstance>();
            float terrainWidth = td.size.x;
            float terrainLength = td.size.z;

            // Roadside trees along both sides of highway
            for (int i = -8; i <= 8; i++)
            {
                if (i == 0 || i == 1) continue;
                float wz = i * 22f;
                AddTerrainTree(treeInstances, -14f, wz, terrainWidth, terrainLength, 0);
                AddTerrainTree(treeInstances,  14f, wz, terrainWidth, terrainLength, 0);
            }

            // Boundary trees along farm field
            for (int i = 0; i < 6; i++)
            {
                AddTerrainTree(treeInstances, 85f, -80f + i * 16f, terrainWidth, terrainLength, 0);
            }

            td.treeInstances = treeInstances.ToArray();
        }

        private void AddTerrainTree(List<TreeInstance> list, float wx, float wz, float width, float length, int prototypeIdx)
        {
            float normX = (wx + width * 0.5f) / width;
            float normZ = (wz + length * 0.5f) / length;
            if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) return;

            var tree = new TreeInstance
            {
                position = new Vector3(normX, 0f, normZ),
                widthScale = 1.0f,
                heightScale = 1.0f,
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = prototypeIdx
            };
            list.Add(tree);
        }

        private void PopulateGrassDetails(TerrainData td)
        {
            if (td.detailPrototypes == null || td.detailPrototypes.Length == 0) return;
            int detailRes = td.detailResolution;
            if (detailRes <= 0) return;

            int[,] grassMap = new int[detailRes, detailRes];
            float terrainWidth = td.size.x;
            float terrainLength = td.size.z;

            for (int y = 0; y < detailRes; y++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    float wx = ((float)x / detailRes) * terrainWidth - (terrainWidth * 0.5f);
                    float wz = ((float)y / detailRes) * terrainLength - (terrainLength * 0.5f);

                    bool onRoad = (Mathf.Abs(wx) < 8f && Mathf.Abs(wz) < 200f) || (Mathf.Abs(wz - 50f) < 8f && Mathf.Abs(wx) < 150f);
                    bool onSite = (Mathf.Abs(wx - 35f) < 32f && Mathf.Abs(wz - 35f) < 32f) || (Mathf.Abs(wx + 35f) < 32f && Mathf.Abs(wz - 35f) < 32f);

                    if (!onRoad && !onSite && Mathf.Abs(wx) < 160f && Mathf.Abs(wz) < 160f)
                    {
                        float noise = Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f);
                        grassMap[y, x] = (noise > 0.45f) ? Mathf.RoundToInt(noise * 4f) : 0;
                    }
                    else
                    {
                        grassMap[y, x] = 0;
                    }
                }
            }
            td.SetDetailLayer(0, 0, 0, grassMap);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. LIGHT PROBE GRID GENERATION
        // ══════════════════════════════════════════════════════════════════════
        public void SetupLightProbeGrid()
        {
            var lpGo = GameObject.Find("LightProbeGroup_Vehicles");
            if (lpGo != null) return;

            lpGo = new GameObject("LightProbeGroup_Vehicles");
            var lpg = lpGo.AddComponent<LightProbeGroup>();

            int stepsX = 7;
            int stepsZ = 7;
            int stepsY = 3;

            float startX = -120f;
            float endX   =  120f;
            float startZ = -120f;
            float endZ   =  120f;
            float[] yLevels = new float[] { 0.6f, 2.5f, 6.0f };

            var positions = new Vector3[stepsX * stepsZ * stepsY];
            int idx = 0;

            for (int y = 0; y < stepsY; y++)
            {
                for (int x = 0; x < stepsX; x++)
                {
                    for (int z = 0; z < stepsZ; z++)
                    {
                        float px = Mathf.Lerp(startX, endX, (float)x / (stepsX - 1));
                        float pz = Mathf.Lerp(startZ, endZ, (float)z / (stepsZ - 1));
                        positions[idx++] = new Vector3(px, yLevels[y], pz);
                    }
                }
            }

#if UNITY_EDITOR
            lpg.probePositions = positions;
#endif
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. POST-PROCESSING VOLUME (TONE MAPPING, LOW BLOOM, COLOR GRADING)
        // ══════════════════════════════════════════════════════════════════════
        public void SetupPostProcessing()
        {
            var volumeGo = GameObject.Find("GlobalPostProcessVolume");
            if (volumeGo == null)
            {
                volumeGo = new GameObject("GlobalPostProcessVolume");
            }

            var volume = volumeGo.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeGo.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 1f;

            var activeProfile = volume.profile != null ? volume.profile : volume.sharedProfile;
            if (activeProfile == null)
            {
                activeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                activeProfile.name = "HVW_PostProcessing_Profile";
                volume.profile = activeProfile;
            }

            // 1. Tonemapping (ACES for natural daylight roll-off)
            if (!activeProfile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = activeProfile.Add<Tonemapping>(true);
            }
            tonemapping.mode.Override(TonemappingMode.ACES);

            // 2. Bloom (Subtle low bloom to prevent blowout while retaining specular glints)
            if (!activeProfile.TryGet<Bloom>(out var bloom))
            {
                bloom = activeProfile.Add<Bloom>(true);
            }
            bloom.intensity.Override(0.18f);
            bloom.threshold.Override(1.10f);
            bloom.scatter.Override(0.65f);

            // 3. Color Grading / Color Adjustments
            if (!activeProfile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = activeProfile.Add<ColorAdjustments>(true);
            }
            colorAdj.postExposure.Override(0.05f);
            colorAdj.contrast.Override(10.0f);
            colorAdj.saturation.Override(8.0f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 6. ASSET PRESENCE VALIDATION & CLEAR WARNINGS
        // ══════════════════════════════════════════════════════════════════════
        private void CheckMissingAssetsWarnings()
        {
            if (grassLayer == null || grassLayer.diffuseTexture == null)
            {
                Debug.LogWarning("[HVW_VisualSystem] ⚠️ TerrainLayer 'Layer_Grass' has no diffuse texture assigned.\n" +
                                 "Drop grass albedo and normal textures into 'Assets/Textures/Terrain/'. See ASSET_INTEGRATION_GUIDE.md.");
            }

            if (treePrototypePrefab == null)
            {
                Debug.LogWarning("[HVW_VisualSystem] ⚠️ Tree prototype slot is empty.\n" +
                                 "Download tree FBX models and place them in 'Assets/Models/Environment/Trees/' to populate the Terrain Tree system. See ASSET_INTEGRATION_GUIDE.md.");
            }
        }
    }
}
