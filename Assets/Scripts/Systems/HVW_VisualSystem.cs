using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Master Visual & PBR System for HeavyVehiclesWorld.
    /// Handles PBR URP/Lit material configuration with texture slots (Albedo, Normal, Smoothness/Metallic),
    /// balanced daylight lighting (removes yellow blowout, soft shadows, HDRI skybox, light probes),
    /// and TerrainLayer configuration with slots for textures, grass detail, and tree prototypes.
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
        public Vector3 sunAngles = new Vector3(48f, -35f, 0f);

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

            // Skybox setup
            if (customSkyboxMaterial != null)
            {
                RenderSettings.skybox = customSkyboxMaterial;
            }
            else
            {
                var proceduralSkyShader = Shader.Find("Skybox/Procedural");
                if (proceduralSkyShader != null)
                {
                    var skyMat = new Material(proceduralSkyShader);
                    skyMat.SetFloat("_SunSize", 0.04f);
                    skyMat.SetFloat("_AtmosphereThickness", 1.0f);
                    skyMat.SetColor("_SkyTint", new Color(0.38f, 0.58f, 0.85f));
                    skyMat.SetColor("_GroundColor", new Color(0.26f, 0.32f, 0.24f));
                    skyMat.SetFloat("_Exposure", 1.0f);
                    RenderSettings.skybox = skyMat;
                }
            }

            // Balanced Trilight Ambient (Natural Daylight)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.42f, 0.55f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.48f);
            RenderSettings.ambientGroundColor  = new Color(0.22f, 0.28f, 0.20f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. PBR MATERIAL FACTORY & SLOT CONFIGURATION
        // ══════════════════════════════════════════════════════════════════════
        public void InitializePBRMaterials()
        {
            Shader pbrShader = Shader.Find("Universal Render Pipeline/Lit");
            if (pbrShader == null) pbrShader = Shader.Find("Standard");

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

        private Material CreatePBRMaterial(Shader shader, string name, Color baseColor, float metallic, float smoothness)
        {
            var mat = new Material(shader) { name = name };

            // URP & Standard compatible property assignments
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", baseColor);

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
                var tObj = GameObject.FindObjectOfType<Terrain>();
                if (tObj != null) terrainGo = tObj.gameObject;
            }

            if (terrainGo != null)
            {
                targetTerrain = terrainGo.GetComponent<Terrain>();
            }

            // Create TerrainLayers with texture slots
            if (grassLayer == null)
            {
                grassLayer = new TerrainLayer
                {
                    name = "Layer_Grass",
                    tileSize = new Vector2(10f, 10f),
                    smoothness = 0.2f,
                    metallic = 0.0f
                };
            }

            if (dirtLayer == null)
            {
                dirtLayer = new TerrainLayer
                {
                    name = "Layer_Dirt",
                    tileSize = new Vector2(8f, 8f),
                    smoothness = 0.15f,
                    metallic = 0.0f
                };
            }

            if (asphaltLayer == null)
            {
                asphaltLayer = new TerrainLayer
                {
                    name = "Layer_Asphalt",
                    tileSize = new Vector2(12f, 12f),
                    smoothness = 0.25f,
                    metallic = 0.05f
                };
            }

            if (rockLayer == null)
            {
                rockLayer = new TerrainLayer
                {
                    name = "Layer_Rock",
                    tileSize = new Vector2(14f, 14f),
                    smoothness = 0.18f,
                    metallic = 0.05f
                };
            }

            if (targetTerrain != null && targetTerrain.terrainData != null)
            {
                targetTerrain.terrainData.terrainLayers = new TerrainLayer[]
                {
                    grassLayer,
                    dirtLayer,
                    asphaltLayer,
                    rockLayer
                };
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. LIGHT PROBE GRID GENERATION
        // ══════════════════════════════════════════════════════════════════════
        public void SetupLightProbeGrid()
        {
            var lpGo = GameObject.Find("LightProbeGroup_Vehicles");
            if (lpGo != null) return; // already created

            lpGo = new GameObject("LightProbeGroup_Vehicles");
            var lpg = lpGo.AddComponent<LightProbeGroup>();

            // Generate probe grid covering -120m to +120m across main play area
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

            if (volume.profile == null)
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "HVW_PostProcessing_Profile";

                // 1. Tonemapping (ACES for cinematic daylight roll-off)
                if (!profile.TryGet<Tonemapping>(out var tonemapping))
                {
                    tonemapping = profile.Add<Tonemapping>(true);
                }
                tonemapping.mode.Override(TonemappingMode.ACES);

                // 2. Bloom (Subtle low bloom to prevent blowout while retaining vehicle specular glints)
                if (!profile.TryGet<Bloom>(out var bloom))
                {
                    bloom = profile.Add<Bloom>(true);
                }
                bloom.intensity.Override(0.20f);
                bloom.threshold.Override(1.05f);
                bloom.scatter.Override(0.65f);

                // 3. Color Grading / Color Adjustments
                if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
                {
                    colorAdj = profile.Add<ColorAdjustments>(true);
                }
                colorAdj.postExposure.Override(0.05f);
                colorAdj.contrast.Override(10.0f);
                colorAdj.saturation.Override(8.0f);

                volume.profile = profile;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. ASSET PRESENCE VALIDATION & CLEAR WARNINGS
        // ══════════════════════════════════════════════════════════════════════
        private void CheckMissingAssetsWarnings()
        {
            if (grassLayer.diffuseTexture == null)
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
