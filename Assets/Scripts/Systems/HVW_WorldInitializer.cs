using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HeavyVehiclesWorld.Vehicles;
using HeavyVehiclesWorld.Controls;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Master runtime world initializer for HeavyVehiclesWorld.
    /// Ensures that even if the scene file was empty, the complete 3D world,
    /// all 6 vehicles, follow camera, and mobile touch UI are generated
    /// immediately upon launch on Android.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class HVW_WorldInitializer : MonoBehaviour
    {
        public static HVW_WorldInitializer Instance { get; private set; }

        private List<VehicleController> _allVehicles = new List<VehicleController>();
        private VehicleController _activeVehicle;
        public VehicleController ActiveVehicle => _activeVehicle;
        private HVW_DiagnosticsOverlay _diagOverlay;
        private HVW_FollowCamera _followCamera;
        private HVW_PhotoCamera _photoCamera;
        private Canvas _mobileCanvas;
        private Text _speedometerText;
        private Text _gearText;
        private Text _vehicleNameText;
        private GameObject _actionButtonGo;
        private Text _actionButtonLabel;
        private GameObject _actionButtonGo2;
        private Text _actionButtonLabel2;
        private GameObject _settingsModalPanel;
        private Text _gfxQualityStatusText;
        private Text _fpsCapStatusText;

        private bool _steerLeftHeld;
        private bool _steerRightHeld;
        private bool _accelHeld;
        private bool _brakeHeld;
        private bool _handbrakeHeld;

        private float _currentSteer = 0f;
        private float _currentThrottle = 0f;
        private float _currentBrake = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure Diagnostics Overlay is active
            _diagOverlay = GetComponent<HVW_DiagnosticsOverlay>();
            if (_diagOverlay == null)
                _diagOverlay = gameObject.AddComponent<HVW_DiagnosticsOverlay>();

            // Initialize Master Visual & PBR System first
            if (HVW_VisualSystem.Instance == null)
            {
                var visGo = GameObject.Find("HVW_VisualSystem");
                if (visGo == null) visGo = new GameObject("HVW_VisualSystem");
                visGo.AddComponent<HVW_VisualSystem>();
            }

            // Step 3 — Post-processing (Global Volume: ACES, Bloom, Color Grading, Vignette)
            if (FindAnyObjectByType<HVW_PostProcessing>() == null)
            {
                var ppGo = new GameObject("HVW_PostProcessing");
                ppGo.AddComponent<HVW_PostProcessing>();
            }

            BuildWorldEnvironment();
            SpawnAllVehicles();
            SetupFollowCamera();
            BuildMobileUI();

            // Step 1 — activate any real imported models, hide placeholders where model exists
            if (GetComponent<HVW_ModelLoader>() == null)
                gameObject.AddComponent<HVW_ModelLoader>();

            // Screenshot automation (activates only when -hvwScreenshots flag is passed)
            if (GetComponent<HVW_StartupScreenshot>() == null)
                gameObject.AddComponent<HVW_StartupScreenshot>();

            // Set Car as default starting vehicle
            if (_allVehicles.Count > 0)
            {
                SelectVehicle(_allVehicles[0]);
            }
        }

        private void Update()
        {
            HandleMobileInputSmoothing();
            UpdateHUD();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. WORLD ENVIRONMENT GENERATION
        // ══════════════════════════════════════════════════════════════════════
        private void BuildWorldEnvironment()
        {
            // EventSystem
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Calibrated Lighting & Skybox managed by HVW_VisualSystem
            if (HVW_VisualSystem.Instance != null)
            {
                HVW_VisualSystem.Instance.ApplyCalibratedLighting();
            }
            else
            {
                var lightGo = GameObject.Find("Directional Light");
                if (lightGo == null)
                {
                    lightGo = new GameObject("Directional Light");
                    var l = lightGo.AddComponent<Light>();
                    l.type = LightType.Directional;
                }
                var dirLight = lightGo.GetComponent<Light>();
                dirLight.color = new Color(1.0f, 0.98f, 0.95f);
                dirLight.intensity = 1.0f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.78f;
                lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            // Remove legacy Ground plane if present (obsolete built-in Standard plane)
            var oldGround = GameObject.Find("Ground");
            if (oldGround != null)
            {
                if (Application.isPlaying) Destroy(oldGround);
                else DestroyImmediate(oldGround);
            }

            // Ground Terrain System (4 PBR TerrainLayers: Grass, Dirt, Asphalt, Rock)
            var terrainGo = GameObject.Find("Terrain");
            if (terrainGo == null)
            {
                var terrainData = new TerrainData();
                terrainData.size = new Vector3(800f, 60f, 800f);
                terrainData.heightmapResolution = 129;
                terrainData.baseMapResolution = 512;
                terrainData.SetDetailResolution(256, 16);

                if (HVW_VisualSystem.Instance != null)
                {
                    terrainData.terrainLayers = new TerrainLayer[]
                    {
                        HVW_VisualSystem.Instance.grassLayer,
                        HVW_VisualSystem.Instance.dirtLayer,
                        HVW_VisualSystem.Instance.asphaltLayer,
                        HVW_VisualSystem.Instance.rockLayer
                    };
                }

                terrainGo = Terrain.CreateTerrainGameObject(terrainData);
                terrainGo.name = "Terrain";
                terrainGo.transform.position = new Vector3(-400f, 0f, -400f);

                var terrainComp = terrainGo.GetComponent<Terrain>();
                terrainComp.drawTreesAndFoliage = true;
                terrainComp.treeDistance = 500f;
                terrainComp.detailObjectDistance = 80f;
                if (terrainGo.GetComponent<HeavyVehiclesWorld.World.TerrainDeformer>() == null)
                    terrainGo.AddComponent<HeavyVehiclesWorld.World.TerrainDeformer>();
            }

            // Road Circuit (Asphalt Loop)
            BuildRoadTrack();

            // Props (Trees with Colliders, Construction Zone, Farm Fields, Crates)
            BuildWorldProps();
        }

        private static Material CreateSafeMaterial(string name, Color color, float metallic = 0.1f, float smoothness = 0.3f)
        {
            var mat = new Material(HVW_VisualSystem.GetSafeShader()) { name = name };
            var tex = HVW_VisualSystem.CreateProceduralTexture(color, 32);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        public static Material LoadOrSafeMaterial(string assetPath, string name, Color fallbackColor, float metallic = 0.05f, float smoothness = 0.2f)
        {
#if UNITY_EDITOR
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat != null) return mat;
#endif
            return CreateSafeMaterial(name, fallbackColor, metallic, smoothness);
        }

        private void BuildRoadTrack()
        {
            var roadRoot = new GameObject("RoadNetwork");
            var vis = HVW_VisualSystem.Instance;
            var asphaltMat = (vis != null && vis.asphaltRoadPBR != null) ? vis.asphaltRoadPBR : LoadOrSafeMaterial("Assets/Materials/M_PBR_Asphalt_Road.mat", "M_Road_Asphalt", new Color(0.18f, 0.18f, 0.20f), 0.05f, 0.2f);
            var stripeMat  = LoadOrSafeMaterial("Assets/Materials/M_PBR_Road_Stripe.mat", "M_Road_Stripe", new Color(0.95f, 0.85f, 0.15f), 0.1f, 0.3f);
            var curbMat    = (vis != null && vis.concreteCurbsPBR != null) ? vis.concreteCurbsPBR : LoadOrSafeMaterial("Assets/Materials/M_PBR_Concrete_Curbs.mat", "M_Road_Curb", new Color(0.85f, 0.25f, 0.25f), 0.05f, 0.2f);

            // Main Straight Road (North-South) - 400m long, 14m wide
            CreateRoadSegment(roadRoot, "Road_MainStraight", new Vector3(0, 0.02f, 0), new Vector3(14f, 0.05f, 400f), asphaltMat, stripeMat, curbMat);

            // East-West Cross Road - 300m long, 14m wide
            CreateRoadSegment(roadRoot, "Road_CrossRoad", new Vector3(0, 0.02f, 50f), new Vector3(300f, 0.05f, 14f), asphaltMat, stripeMat, curbMat);

            // Construction Site Ground (Natural Earth Brown / Dirt - NOT RED)
            var constrGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            constrGround.name = "Zone_ConstructionDirt";
            constrGround.transform.SetParent(roadRoot.transform);
            constrGround.transform.position = new Vector3(35f, 0.03f, 35f);
            constrGround.transform.localScale = new Vector3(60f, 0.05f, 60f);
            var constrMat = LoadOrSafeMaterial("Assets/Materials/M_PBR_ConstructionDirt.mat", "M_ConstructionDirt", new Color(0.48f, 0.36f, 0.22f), 0.0f, 0.15f);
            constrGround.GetComponent<Renderer>().sharedMaterial = constrMat;

            // Farm Field (Golden Wheat Yellow/Amber)
            var farmGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farmGround.name = "Zone_WheatField";
            farmGround.transform.SetParent(roadRoot.transform);
            farmGround.transform.position = new Vector3(45f, 0.03f, -40f);
            farmGround.transform.localScale = new Vector3(80f, 0.05f, 80f);
            farmGround.GetComponent<Renderer>().sharedMaterial = LoadOrSafeMaterial("Assets/Materials/M_PBR_WheatField.mat", "M_WheatField", new Color(0.82f, 0.68f, 0.25f), 0.0f, 0.2f);

            // Crop Manager & Wheat Crop Field with cuttable stalks
            var cropMgrGo = new GameObject("CropManager");
            cropMgrGo.transform.SetParent(roadRoot.transform);
            var cropMgr = cropMgrGo.AddComponent<CropManager>();

            var fieldGo = new GameObject("WheatField_Crops");
            fieldGo.transform.SetParent(roadRoot.transform);
            fieldGo.transform.position = new Vector3(45f, 0.05f, -40f);
            var cropField = fieldGo.AddComponent<CropField>();
            cropField.rows = 20;
            cropField.columns = 30;
            cropField.rowSpacing = 2.0f;
            cropField.columnSpacing = 2.0f;
            cropMgr.RegisterField(cropField);

            // Quarry Dig Site (Grey Rock / Gravel)
            var quarryGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quarryGround.name = "Zone_QuarryGravel";
            quarryGround.transform.SetParent(roadRoot.transform);
            quarryGround.transform.position = new Vector3(-35f, 0.03f, 35f);
            quarryGround.transform.localScale = new Vector3(60f, 0.05f, 60f);
            quarryGround.GetComponent<Renderer>().sharedMaterial = LoadOrSafeMaterial("Assets/Materials/M_PBR_QuarryGravel.mat", "M_QuarryGravel", new Color(0.40f, 0.40f, 0.42f), 0.05f, 0.2f);
        }

        private void CreateRoadSegment(GameObject parent, string name, Vector3 pos, Vector3 scale, Material roadMat, Material stripeMat, Material curbMat)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.SetParent(parent.transform);
            road.transform.position = pos;
            road.transform.localScale = scale;
            road.GetComponent<Renderer>().sharedMaterial = roadMat;

            // Center yellow stripe
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = name + "_CenterStripe";
            stripe.transform.SetParent(road.transform);
            stripe.transform.localPosition = new Vector3(0, 0.55f, 0);
            stripe.transform.localScale = scale.x > scale.z
                ? new Vector3(1f, 0.2f, 0.04f)
                : new Vector3(0.04f, 0.2f, 1f);
            stripe.GetComponent<Renderer>().sharedMaterial = stripeMat;
            var c = stripe.GetComponent<Collider>();
            if (c) SafeDestroy(c);
        }

        private void BuildWorldProps()
        {
            var propsRoot = new GameObject("WorldProps");
            var vis = HVW_VisualSystem.Instance;
            var treeWoodMat   = CreateSafeMaterial("M_TreeWood", new Color(0.35f, 0.22f, 0.12f));
            var treeLeavesMat = CreateSafeMaterial("M_TreeLeaves", new Color(0.18f, 0.45f, 0.15f));
            var coneMat       = (vis != null && vis.constructionPropsPBR != null) ? vis.constructionPropsPBR : CreateSafeMaterial("M_Cone", new Color(1f, 0.45f, 0.05f));
            var crateMat      = CreateSafeMaterial("M_Crate", new Color(0.6f, 0.45f, 0.25f));

            // Trees along road with CapsuleCollider for physical collision
            for (int i = -8; i <= 8; i++)
            {
                if (i == 0 || i == 1) continue;
                CreateTree(propsRoot.transform, new Vector3(-12f, 0f, i * 22f), treeWoodMat, treeLeavesMat);
                CreateTree(propsRoot.transform, new Vector3( 12f, 0f, i * 22f), treeWoodMat, treeLeavesMat);
            }

            // Construction cones around Construction Zone (with colliders)
            for (int i = 0; i < 6; i++)
            {
                var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.transform.SetParent(propsRoot.transform);
                cone.transform.position = new Vector3(12f + i * 4f, 0.4f, 18f);
                cone.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
                cone.GetComponent<Renderer>().sharedMaterial = coneMat;
                var col = cone.GetComponent<CapsuleCollider>();
                if (col == null) col = cone.AddComponent<CapsuleCollider>();
            }

            // Dirt Mounds for Excavator & JCB (smooth, natural low-profile mounds)
            var moundMat = LoadOrSafeMaterial("Assets/Materials/M_PBR_Mound.mat", "M_Mound", new Color(0.44f, 0.32f, 0.18f), 0.0f, 0.15f);
            for (int i = 0; i < 3; i++)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.transform.SetParent(propsRoot.transform);
                mound.transform.position = new Vector3(28f + i * 8f, 0.25f, 40f + i * 5f);
                mound.transform.localScale = new Vector3(4.5f, 1.0f, 4.5f);
                mound.GetComponent<Renderer>().sharedMaterial = moundMat;
                var col = mound.GetComponent<SphereCollider>();
                if (col == null) col = mound.AddComponent<SphereCollider>();
            }

            // Freight crates for Truck (with BoxCollider)
            for (int i = 0; i < 4; i++)
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.transform.SetParent(propsRoot.transform);
                crate.transform.position = new Vector3(-18f, 1f + (i % 2) * 2f, -10f + (i / 2) * 2.5f);
                crate.transform.localScale = new Vector3(2f, 2f, 2f);
                crate.GetComponent<Renderer>().sharedMaterial = crateMat;
                var col = crate.GetComponent<BoxCollider>();
                if (col == null) col = crate.AddComponent<BoxCollider>();
            }
        }

        private void CreateTree(Transform parent, Vector3 pos, Material woodMat, Material leavesMat)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent);
            tree.transform.position = pos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0, 2f, 0);
            trunk.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            trunk.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Ensure tree trunk has solid CapsuleCollider for vehicle collision
            var capCol = trunk.GetComponent<CapsuleCollider>();
            if (capCol == null) capCol = trunk.AddComponent<CapsuleCollider>();
            capCol.radius = 0.45f;
            capCol.height = 4.0f;
            capCol.center = Vector3.zero;

            var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(tree.transform, false);
            leaves.transform.localPosition = new Vector3(0, 4.8f, 0);
            leaves.transform.localScale = new Vector3(3.5f, 3.5f, 3.5f);
            leaves.GetComponent<Renderer>().sharedMaterial = leavesMat;
            var leafCol = leaves.GetComponent<Collider>();
            if (leafCol) SafeDestroy(leafCol);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. VEHICLE SPAWNING & RIGGING (ALL 6 VEHICLES)
        // ══════════════════════════════════════════════════════════════════════
        private void SpawnAllVehicles()
        {
            _allVehicles.Clear();

            // 1. CAR (SUV) - Start position (0, 0.55, 0)
            var carColor = new Color(0.12f, 0.45f, 0.85f); // Cobalt Blue
            var carSize  = new Vector3(2.0f, 1.4f, 4.4f);
            var car = CreateVehicleEntity("Car_SUV", VehicleType.Car, new Vector3(0f, 0.55f, 0f), Quaternion.identity, carColor, carSize, 1500f);
            var carCtrl = car.AddComponent<CarController>();
            carCtrl.vehicleType = VehicleType.Car;
            SetupVehiclePhysicsAndWheels(car, carCtrl, 0.38f, 1500f, carColor, carSize);
            _allVehicles.Add(carCtrl);

            // 2. TRACTOR - Farm path (20, 0.65, -10)
            var tracColor = new Color(0.18f, 0.65f, 0.22f); // Farm Green
            var tracSize  = new Vector3(2.2f, 2.4f, 3.8f);
            var tractor = CreateVehicleEntity("Tractor_Agri", VehicleType.Tractor, new Vector3(20f, 0.65f, -10f), Quaternion.Euler(0f, 180f, 0f), tracColor, tracSize, 4000f);
            var tractorCtrl = tractor.AddComponent<TractorController>();
            tractorCtrl.vehicleType = VehicleType.Tractor;
            SetupVehiclePhysicsAndWheels(tractor, tractorCtrl, 0.5f, 4000f, tracColor, tracSize);
            _allVehicles.Add(tractorCtrl);

            // 3. TRUCK (Heavy Tipper) - (-18, 0.75, 15)
            var truckColor = new Color(0.85f, 0.85f, 0.88f); // Industrial White
            var truckSize  = new Vector3(2.6f, 2.8f, 7.5f);
            var truck = CreateVehicleEntity("Truck_Tipper", VehicleType.Truck, new Vector3(-18f, 0.75f, 15f), Quaternion.Euler(0, -30, 0), truckColor, truckSize, 8000f);
            var truckCtrl = truck.AddComponent<TruckController>();
            truckCtrl.vehicleType = VehicleType.Truck;
            SetupVehiclePhysicsAndWheels(truck, truckCtrl, 0.6f, 8000f, truckColor, truckSize);
            _allVehicles.Add(truckCtrl);

            // 4. JCB (Backhoe Loader) - (28, 0.70, 28)
            var jcbColor = new Color(0.98f, 0.78f, 0.05f); // JCB Yellow
            var jcbSize  = new Vector3(2.4f, 2.6f, 5.2f);
            var jcb = CreateVehicleEntity("JCB_Backhoe", VehicleType.JCB, new Vector3(28f, 0.70f, 28f), Quaternion.Euler(0, 90, 0), jcbColor, jcbSize, 7500f);
            var jcbCtrl = jcb.AddComponent<JCBController>();
            jcbCtrl.vehicleType = VehicleType.JCB;
            SetupVehiclePhysicsAndWheels(jcb, jcbCtrl, 0.55f, 7500f, jcbColor, jcbSize);
            _allVehicles.Add(jcbCtrl);

            // 5. EXCAVATOR - (-28, 0.70, 32)
            var excColor = new Color(0.95f, 0.45f, 0.08f); // Construction Orange
            var excSize  = new Vector3(3.0f, 3.0f, 5.5f);
            var excavator = CreateVehicleEntity("Excavator_Tracked", VehicleType.Excavator, new Vector3(-28f, 0.70f, 32f), Quaternion.Euler(0, -90, 0), excColor, excSize, 12000f);
            var excCtrl = excavator.AddComponent<ExcavatorController>();
            excCtrl.vehicleType = VehicleType.Excavator;
            SetupVehiclePhysicsAndWheels(excavator, excCtrl, 0.55f, 12000f, excColor, excSize);
            _allVehicles.Add(excCtrl);

            // 6. HARVESTER - (42, 0.80, -25)
            var harvColor = new Color(0.12f, 0.55f, 0.18f); // Combine Green
            var harvSize  = new Vector3(3.2f, 3.4f, 7.0f);
            var harvester = CreateVehicleEntity("Harvester_Combine", VehicleType.Harvester, new Vector3(42f, 0.80f, -25f), Quaternion.Euler(0, 180, 0), harvColor, harvSize, 11000f);
            var harvCtrl = harvester.AddComponent<HarvesterController>();
            harvCtrl.vehicleType = VehicleType.Harvester;
            SetupVehiclePhysicsAndWheels(harvester, harvCtrl, 0.65f, 11000f, harvColor, harvSize);
            _allVehicles.Add(harvCtrl);
        }

        private GameObject CreateVehicleEntity(string name, VehicleType type, Vector3 pos, Quaternion rot, Color bodyColor, Vector3 bodySize, float mass)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(pos.x, Mathf.Max(pos.y, 0.75f), pos.z);
            root.transform.rotation = rot;

            // Rigidbody
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.08f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // AudioSource
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = 0.5f;

            // Main Chassis Physics Collider (elevated off the ground so it never drags against road/terrain)
            var rootCol = root.AddComponent<BoxCollider>();
            rootCol.size = new Vector3(bodySize.x * 0.85f, bodySize.y * 0.55f, bodySize.z * 0.88f);
            rootCol.center = new Vector3(0f, bodySize.y * 0.35f, 0f);

            // Empty "Model" child slot for imported 3D asset (FBX/OBJ/GLTF)
            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(root.transform, false);

            var slot = root.AddComponent<VehicleModelSlot>();
            slot.modelSlot = modelGo.transform;
            slot.vehicleType = type;

            // Step 3 — Headlights, brake lights, wheel dust
            root.AddComponent<HVW_VehicleLights>();

            return root;
        }

        private void SetupVehiclePhysicsAndWheels(GameObject root, VehicleController ctrl, float wheelRadius, float mass, Color bodyColor, Vector3 bodySize)
        {
            float halfW = bodySize.x * 0.45f;
            float halfL = bodySize.z * 0.38f;

            Vector3[] wheelOffsets = new[]
            {
                new Vector3(-halfW, 0f,  halfL), // FL
                new Vector3( halfW, 0f,  halfL), // FR
                new Vector3(-halfW, 0f, -halfL), // RL
                new Vector3( halfW, 0f, -halfL)  // RR
            };

            var wcs = new WheelCollider[4];
            var bindings = new VehicleModelSlot.WheelBinding[4];

            for (int i = 0; i < 4; i++)
            {
                var wGo = new GameObject($"WheelCollider_{i}");
                wGo.transform.SetParent(root.transform, false);
                wGo.transform.localPosition = wheelOffsets[i];

                var wc = wGo.AddComponent<WheelCollider>();
                float actualRadius = (ctrl.vehicleType == VehicleType.Tractor) ? ((i >= 2) ? 0.65f : 0.45f) : wheelRadius;
                wc.radius = actualRadius;
                wc.suspensionDistance = 0.25f;

                var spring = wc.suspensionSpring;
                spring.spring = mass * 12f;
                spring.damper = mass * 1.2f;
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;

                wcs[i] = wc;

                bindings[i] = new VehicleModelSlot.WheelBinding
                {
                    name = $"Wheel_{i}",
                    collider = wc,
                    mesh = null,
                    isSteer = (i < 2),
                    isDriven = (i >= 2)
                };
            }

            var slot = root.GetComponent<VehicleModelSlot>();
            if (slot != null)
            {
                slot.wheels = bindings;
            }

            ctrl.wheelColliders = wcs;
            ctrl.engineRunning = true;
            ctrl.isOccupied = true;
            ctrl.currentGear = GearState.Drive;

            if (ctrl is CarController cc)
            {
                cc.frontLeft = wcs[0];
                cc.frontRight = wcs[1];
                cc.rearLeft = wcs[2];
                cc.rearRight = wcs[3];
            }
            else if (ctrl is TruckController tk)
            {
                tk.frontLeft = wcs[0];
                tk.frontRight = wcs[1];
                tk.rearLeft = wcs[2];
                tk.rearRight = wcs[3];
            }
            else if (ctrl is TractorController tc)
            {
                tc.frontLeft = wcs[0];
                tc.frontRight = wcs[1];
                tc.rearLeft = wcs[2];
                tc.rearRight = wcs[3];
            }
            else if (ctrl is JCBController jcb)
            {
                jcb.wheelFL = wcs[0];
                jcb.wheelFR = wcs[1];
                jcb.wheelRL = wcs[2];
                jcb.wheelRR = wcs[3];
            }
            else if (ctrl is ExcavatorController exc)
            {
                exc.leftTrackWheels = new[] { wcs[0], wcs[2] };
                exc.rightTrackWheels = new[] { wcs[1], wcs[3] };
            }
            else if (ctrl is HarvesterController hc)
            {
                hc.wheelFL = wcs[0];
                hc.wheelFR = wcs[1];
                hc.wheelRL = wcs[2];
                hc.wheelRR = wcs[3];
            }

            // Build visible fallback placeholder geometry for chassis, cabin, and wheels
            BuildPlaceholderBody(root, ctrl.vehicleType, bodyColor, bodySize, slot, ctrl, wcs, wheelRadius);
        }

        private void BuildPlaceholderBody(GameObject root, VehicleType type, Color bodyColor, Vector3 bodySize, VehicleModelSlot slot, VehicleController ctrl, WheelCollider[] wcs, float wheelRadius)
        {
            var placeholderGo = new GameObject("PlaceholderVisuals");
            placeholderGo.transform.SetParent(root.transform, false);

            var vis = HVW_VisualSystem.Instance;
            Material bodyMat = new Material(HVW_VisualSystem.GetSafeShader()) { name = $"M_Placeholder_{type}" };
            var bodyTex = HVW_VisualSystem.CreateProceduralTexture(bodyColor, 32);
            if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", bodyTex);
            if (bodyMat.HasProperty("_BaseMap")) bodyMat.SetTexture("_BaseMap", bodyTex);
            if (bodyMat.HasProperty("_Color")) bodyMat.SetColor("_Color", bodyColor);
            if (bodyMat.HasProperty("_BaseColor")) bodyMat.SetColor("_BaseColor", bodyColor);

            Material darkMat = new Material(HVW_VisualSystem.GetSafeShader()) { name = "M_Placeholder_Dark" };
            var darkTex = HVW_VisualSystem.CreateProceduralTexture(new Color(0.14f, 0.16f, 0.20f), 32);
            if (darkMat.HasProperty("_MainTex")) darkMat.SetTexture("_MainTex", darkTex);
            if (darkMat.HasProperty("_BaseMap")) darkMat.SetTexture("_BaseMap", darkTex);
            if (darkMat.HasProperty("_Color")) darkMat.SetColor("_Color", new Color(0.14f, 0.16f, 0.20f));
            if (darkMat.HasProperty("_BaseColor")) darkMat.SetColor("_BaseColor", new Color(0.14f, 0.16f, 0.20f));

            Material steelMat = new Material(HVW_VisualSystem.GetSafeShader()) { name = "M_Placeholder_Steel" };
            var steelTex = HVW_VisualSystem.CreateProceduralTexture(new Color(0.25f, 0.26f, 0.28f), 32);
            if (steelMat.HasProperty("_MainTex")) steelMat.SetTexture("_MainTex", steelTex);
            if (steelMat.HasProperty("_BaseMap")) steelMat.SetTexture("_BaseMap", steelTex);
            if (steelMat.HasProperty("_Color")) steelMat.SetColor("_Color", new Color(0.25f, 0.26f, 0.28f));
            if (steelMat.HasProperty("_BaseColor")) steelMat.SetColor("_BaseColor", new Color(0.25f, 0.26f, 0.28f));

            // 1. Vehicle-specific body assemblies
            switch (type)
            {
                case VehicleType.Car:
                    CreateBox("Car_Chassis", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.35f, 0f), new Vector3(bodySize.x, bodySize.y * 0.5f, bodySize.z), bodyMat);
                    CreateBox("Car_Cabin", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.8f, -bodySize.z * 0.08f), new Vector3(bodySize.x * 0.85f, bodySize.y * 0.45f, bodySize.z * 0.55f), darkMat);
                    break;

                case VehicleType.Tractor:
                    CreateBox("Tractor_Hood", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.38f, bodySize.z * 0.15f), new Vector3(bodySize.x * 0.65f, bodySize.y * 0.55f, bodySize.z * 0.55f), bodyMat);
                    CreateBox("Tractor_Cabin", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.78f, -bodySize.z * 0.22f), new Vector3(bodySize.x * 0.82f, bodySize.y * 0.6f, bodySize.z * 0.4f), darkMat);

                    var hitchPivot = new GameObject("Hitch_Pivot");
                    hitchPivot.transform.SetParent(placeholderGo.transform, false);
                    hitchPivot.transform.localPosition = new Vector3(0f, 0.45f, -bodySize.z * 0.48f);
                    CreateBox("Hitch_Bar", hitchPivot.transform, new Vector3(0f, 0f, -0.35f), new Vector3(0.8f, 0.15f, 0.7f), steelMat);

                    // Working Plough Implement attached to Hitch
                    var ploughGo = new GameObject("Plough_Assembly");
                    ploughGo.transform.SetParent(hitchPivot.transform, false);
                    ploughGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                    CreateBox("Plough_Beam", ploughGo.transform, new Vector3(0f, 0.05f, -0.35f), new Vector3(1.2f, 0.12f, 0.7f), steelMat);
                    for (int p = -1; p <= 1; p++)
                    {
                        CreateBox($"Plough_Blade_{p + 2}", ploughGo.transform, new Vector3(p * 0.42f, -0.28f, -0.35f - p * 0.15f), new Vector3(0.12f, 0.45f, 0.32f), steelMat);
                    }

                    slot.threePointHitch = hitchPivot.transform;
                    if (ctrl is TractorController tc)
                    {
                        tc.rearHitch = hitchPivot.transform;
                        tc.hitchArmTransform = hitchPivot.transform;
                        var ploughComp = ploughGo.AddComponent<PloughImplement>();
                        ploughComp.AttachToTractor(tc);
                    }
                    break;

                case VehicleType.Truck:
                    CreateBox("Truck_Frame", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.25f, 0f), new Vector3(bodySize.x * 0.7f, bodySize.y * 0.25f, bodySize.z), steelMat);
                    CreateBox("Truck_Cab", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.7f, bodySize.z * 0.32f), new Vector3(bodySize.x * 0.95f, bodySize.y * 0.65f, bodySize.z * 0.32f), bodyMat);

                    var tipperPivot = new GameObject("Tipper_Pivot");
                    tipperPivot.transform.SetParent(placeholderGo.transform, false);
                    tipperPivot.transform.localPosition = new Vector3(0f, bodySize.y * 0.42f, -bodySize.z * 0.44f);
                    CreateBox("Tipper_Bed", tipperPivot.transform, new Vector3(0f, bodySize.y * 0.28f, bodySize.z * 0.32f), new Vector3(bodySize.x, bodySize.y * 0.5f, bodySize.z * 0.62f), bodyMat);
                    slot.tipperBed = tipperPivot.transform;
                    if (ctrl is TruckController tk)
                    {
                        tk.tipperBed = tipperPivot.transform;
                    }
                    break;

                case VehicleType.JCB:
                    CreateBox("JCB_Cab", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.62f, 0f), new Vector3(bodySize.x * 0.85f, bodySize.y * 0.62f, bodySize.z * 0.65f), bodyMat);

                    // Front Loader Boom & Bucket
                    var loaderBoomPivot = new GameObject("Loader_Boom_Pivot");
                    loaderBoomPivot.transform.SetParent(placeholderGo.transform, false);
                    loaderBoomPivot.transform.localPosition = new Vector3(0f, bodySize.y * 0.45f, bodySize.z * 0.35f);
                    CreateBox("Loader_Arm", loaderBoomPivot.transform, new Vector3(0f, 0f, 0.65f), new Vector3(bodySize.x * 0.8f, 0.2f, 1.3f), bodyMat);

                    var loaderBucketPivot = new GameObject("Loader_Bucket_Pivot");
                    loaderBucketPivot.transform.SetParent(loaderBoomPivot.transform, false);
                    loaderBucketPivot.transform.localPosition = new Vector3(0f, -0.05f, 1.3f);
                    CreateBox("Loader_Bucket", loaderBucketPivot.transform, new Vector3(0f, 0f, 0.15f), new Vector3(bodySize.x * 1.05f, 0.55f, 0.65f), steelMat);

                    slot.frontLoaderBoom = loaderBoomPivot.transform;
                    slot.frontLoaderBucket = loaderBucketPivot.transform;

                    // Rear Backhoe Swing, Boom, Dipper & Bucket
                    var backhoeSwingPivot = new GameObject("Backhoe_Swing_Pivot");
                    backhoeSwingPivot.transform.SetParent(placeholderGo.transform, false);
                    backhoeSwingPivot.transform.localPosition = new Vector3(0f, bodySize.y * 0.45f, -bodySize.z * 0.45f);

                    var backhoeBoomPivot = new GameObject("Backhoe_Boom_Pivot");
                    backhoeBoomPivot.transform.SetParent(backhoeSwingPivot.transform, false);
                    backhoeBoomPivot.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    CreateBox("Backhoe_Boom_Mesh", backhoeBoomPivot.transform, new Vector3(0f, 0.4f, -0.6f), new Vector3(0.35f, 0.35f, 1.3f), bodyMat);

                    var backhoeDipperPivot = new GameObject("Backhoe_Dipper_Pivot");
                    backhoeDipperPivot.transform.SetParent(backhoeBoomPivot.transform, false);
                    backhoeDipperPivot.transform.localPosition = new Vector3(0f, 0.8f, -1.2f);
                    CreateBox("Backhoe_Dipper_Mesh", backhoeDipperPivot.transform, new Vector3(0f, -0.3f, -0.4f), new Vector3(0.28f, 0.28f, 1.0f), bodyMat);

                    var backhoeBucketPivot = new GameObject("Backhoe_Bucket_Pivot");
                    backhoeBucketPivot.transform.SetParent(backhoeDipperPivot.transform, false);
                    backhoeBucketPivot.transform.localPosition = new Vector3(0f, -0.7f, -0.8f);
                    CreateBox("Backhoe_Bucket_Mesh", backhoeBucketPivot.transform, new Vector3(0f, -0.1f, 0.1f), new Vector3(0.55f, 0.45f, 0.55f), steelMat);

                    var backhoeTip = new GameObject("Bucket_Tip");
                    backhoeTip.transform.SetParent(backhoeBucketPivot.transform, false);
                    backhoeTip.transform.localPosition = new Vector3(0f, -0.3f, 0.3f);

                    slot.boomArm = backhoeBoomPivot.transform;
                    slot.stickArm = backhoeDipperPivot.transform;
                    slot.bucket = backhoeBucketPivot.transform;

                    // Stabilizer Legs
                    var stabL = new GameObject("Stabilizer_L");
                    stabL.transform.SetParent(placeholderGo.transform, false);
                    stabL.transform.localPosition = new Vector3(-bodySize.x * 0.48f, bodySize.y * 0.35f, -bodySize.z * 0.32f);
                    CreateBox("Stab_L_Leg", stabL.transform, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.8f, 0.18f), steelMat);

                    var stabR = new GameObject("Stabilizer_R");
                    stabR.transform.SetParent(placeholderGo.transform, false);
                    stabR.transform.localPosition = new Vector3(bodySize.x * 0.48f, bodySize.y * 0.35f, -bodySize.z * 0.32f);
                    CreateBox("Stab_R_Leg", stabR.transform, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.8f, 0.18f), steelMat);

                    if (ctrl is JCBController jcb)
                    {
                        jcb.loaderBoomTransform = loaderBoomPivot.transform;
                        jcb.loaderBucketTransform = loaderBucketPivot.transform;
                        jcb.backhoeSwingTransform = backhoeSwingPivot.transform;
                        jcb.backhoeBoomTransform = backhoeBoomPivot.transform;
                        jcb.backhoeDipperTransform = backhoeDipperPivot.transform;
                        jcb.backhoeBucketTransform = backhoeBucketPivot.transform;
                        jcb.backhoeBucketTip = backhoeTip.transform;
                        jcb.stabilizerLeft = stabL.transform;
                        jcb.stabilizerRight = stabR.transform;
                    }
                    break;

                case VehicleType.Excavator:
                    CreateBox("Excavator_Tracks", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.22f, 0f), new Vector3(bodySize.x, bodySize.y * 0.32f, bodySize.z), steelMat);

                    // 360-degree Rotating Turret / Cabin
                    var cabinPivot = new GameObject("RotatingCabin_Pivot");
                    cabinPivot.transform.SetParent(placeholderGo.transform, false);
                    cabinPivot.transform.localPosition = new Vector3(0f, bodySize.y * 0.38f, 0f);
                    CreateBox("Excavator_CabinBody", cabinPivot.transform, new Vector3(0f, bodySize.y * 0.35f, -bodySize.z * 0.08f), new Vector3(bodySize.x * 0.85f, bodySize.y * 0.6f, bodySize.z * 0.75f), bodyMat);

                    // Multi-joint Articulated Excavator Arm Chain
                    var excBoomPivot = new GameObject("Excavator_Boom_Pivot");
                    excBoomPivot.transform.SetParent(cabinPivot.transform, false);
                    excBoomPivot.transform.localPosition = new Vector3(0.45f, bodySize.y * 0.42f, 0.75f);
                    CreateBox("Boom_Bar", excBoomPivot.transform, new Vector3(0f, 0.7f, 1.1f), new Vector3(0.35f, 0.35f, 2.4f), bodyMat);

                    var excStickPivot = new GameObject("Excavator_Stick_Pivot");
                    excStickPivot.transform.SetParent(excBoomPivot.transform, false);
                    excStickPivot.transform.localPosition = new Vector3(0f, 1.4f, 2.2f);
                    CreateBox("Stick_Bar", excStickPivot.transform, new Vector3(0f, -0.6f, 0.7f), new Vector3(0.30f, 0.30f, 1.6f), bodyMat);

                    var excBucketPivot = new GameObject("Excavator_Bucket_Pivot");
                    excBucketPivot.transform.SetParent(excStickPivot.transform, false);
                    excBucketPivot.transform.localPosition = new Vector3(0f, -1.2f, 1.4f);
                    CreateBox("Bucket_Tool", excBucketPivot.transform, new Vector3(0f, -0.1f, 0.2f), new Vector3(0.75f, 0.65f, 0.75f), steelMat);

                    var excBucketTip = new GameObject("Excavator_Bucket_Tip");
                    excBucketTip.transform.SetParent(excBucketPivot.transform, false);
                    excBucketTip.transform.localPosition = new Vector3(0f, -0.4f, 0.5f);

                    slot.rotatingCabin = cabinPivot.transform;
                    slot.boomArm = excBoomPivot.transform;
                    slot.stickArm = excStickPivot.transform;
                    slot.bucket = excBucketPivot.transform;

                    if (ctrl is ExcavatorController ec)
                    {
                        ec.cabinTransform = cabinPivot.transform;
                        ec.boomTransform = excBoomPivot.transform;
                        ec.stickTransform = excStickPivot.transform;
                        ec.bucketTransform = excBucketPivot.transform;
                        ec.bucketTip = excBucketTip.transform;
                    }
                    break;

                case VehicleType.Harvester:
                    CreateBox("Harvester_Body", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.62f, 0f), new Vector3(bodySize.x * 0.85f, bodySize.y * 0.68f, bodySize.z * 0.85f), bodyMat);
                    CreateBox("Harvester_Cab", placeholderGo.transform, new Vector3(0f, bodySize.y * 0.95f, bodySize.z * 0.25f), new Vector3(bodySize.x * 0.7f, bodySize.y * 0.45f, bodySize.z * 0.35f), darkMat);

                    var headerPivot = new GameObject("CutterHeader_Pivot");
                    headerPivot.transform.SetParent(placeholderGo.transform, false);
                    headerPivot.transform.localPosition = new Vector3(0f, 0.4f, bodySize.z * 0.48f);
                    CreateBox("Cutter_Bar", headerPivot.transform, new Vector3(0f, 0f, 0.6f), new Vector3(bodySize.x * 1.35f, 0.5f, 0.85f), new Color(0.85f, 0.25f, 0.25f));

                    // Revolving Reel Assembly on cutting header
                    var reelPivot = new GameObject("Cutter_Reel_Pivot");
                    reelPivot.transform.SetParent(headerPivot.transform, false);
                    reelPivot.transform.localPosition = new Vector3(0f, 0.35f, 0.6f);

                    var reelAxle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    reelAxle.name = "Reel_Axle";
                    reelAxle.transform.SetParent(reelPivot.transform, false);
                    reelAxle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    reelAxle.transform.localScale = new Vector3(0.12f, bodySize.x * 0.65f, 0.12f);
                    DestroyImmediate(reelAxle.GetComponent<Collider>());
                    reelAxle.GetComponent<Renderer>().sharedMaterial = steelMat;

                    for (int b = 0; b < 4; b++)
                    {
                        float angle = b * 90f;
                        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bar.name = $"Reel_Paddle_{b}";
                        bar.transform.SetParent(reelPivot.transform, false);
                        float rad = angle * Mathf.Deg2Rad;
                        bar.transform.localPosition = new Vector3(0f, Mathf.Sin(rad) * 0.28f, Mathf.Cos(rad) * 0.28f);
                        bar.transform.localScale = new Vector3(bodySize.x * 1.30f, 0.04f, 0.10f);
                        DestroyImmediate(bar.GetComponent<Collider>());
                        bar.GetComponent<Renderer>().sharedMaterial = steelMat;
                    }

                    slot.cutterHeader = headerPivot.transform;
                    if (ctrl is HarvesterController hc)
                    {
                        hc.headerTransform = headerPivot.transform;
                        hc.reelTransform = reelPivot.transform;
                    }

                    // 90-Degree Swing-out Unloading Auger Pipe
                    var augerPivot = new GameObject("Auger_Pivot");
                    augerPivot.transform.SetParent(placeholderGo.transform, false);
                    augerPivot.transform.localPosition = new Vector3(-bodySize.x * 0.45f, bodySize.y * 0.88f, -0.4f);
                    CreateBox("Auger_Pipe", augerPivot.transform, new Vector3(0f, 0f, -1.5f), new Vector3(0.25f, 0.25f, 3.0f), steelMat);
                    CreateBox("Auger_Spout", augerPivot.transform, new Vector3(0f, -0.2f, -2.9f), new Vector3(0.22f, 0.35f, 0.22f), darkMat);
                    slot.unloadAuger = augerPivot.transform;
                    if (ctrl is HarvesterController hc2)
                    {
                        hc2.augerTransform = augerPivot.transform;
                    }
                    break;
            }

            // 2. Placeholder Cylinder Wheels
            var wheelsContainer = new GameObject("PlaceholderWheels");
            wheelsContainer.transform.SetParent(placeholderGo.transform, false);

            Material tireMat = (vis != null && vis.rubberTiresPBR != null)
                ? vis.rubberTiresPBR
                : new Material(HVW_VisualSystem.GetSafeShader()) { color = new Color(0.12f, 0.12f, 0.12f) };

            for (int i = 0; i < wcs.Length; i++)
            {
                var wc = wcs[i];
                if (wc == null) continue;

                float r = (type == VehicleType.Tractor) ? ((i >= 2) ? 0.65f : 0.45f) : wheelRadius;
                float width = (type == VehicleType.Truck || type == VehicleType.Harvester) ? 0.45f : 0.32f;

                // Dedicated wheel root transform synced to WheelCollider.GetWorldPose
                var wHolder = new GameObject($"PlaceholderWheel_{i}");
                wHolder.transform.SetParent(wheelsContainer.transform, false);
                wHolder.transform.localPosition = wc.transform.localPosition;

                // Cylinder mesh oriented along local wheel axle (Z=90 deg)
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "WheelTireMesh";
                cyl.transform.SetParent(wHolder.transform, false);
                cyl.transform.localPosition = Vector3.zero;
                cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cyl.transform.localScale = new Vector3(r * 2f, width * 0.5f, r * 2f);
                cyl.GetComponent<Renderer>().sharedMaterial = tireMat;

                var c = cyl.GetComponent<Collider>();
                if (c) SafeDestroy(c);

                if (slot != null && i < slot.wheels.Length)
                {
                    slot.wheels[i].mesh = wHolder.transform;
                }

                if (ctrl is CarController cc)
                {
                    if (i == 0) cc.frontLeftMesh = wHolder.transform;
                    else if (i == 1) cc.frontRightMesh = wHolder.transform;
                    else if (i == 2) cc.rearLeftMesh = wHolder.transform;
                    else if (i == 3) cc.rearRightMesh = wHolder.transform;
                }
                else if (ctrl is TruckController tk)
                {
                    if (i == 0) tk.frontLeftMesh = wHolder.transform;
                    else if (i == 1) tk.frontRightMesh = wHolder.transform;
                    else if (i == 2) tk.rearLeftMesh = wHolder.transform;
                    else if (i == 3) tk.rearRightMesh = wHolder.transform;
                }
                else if (ctrl is TractorController tc)
                {
                    if (i == 0) tc.frontLeftMesh = wHolder.transform;
                    else if (i == 1) tc.frontRightMesh = wHolder.transform;
                    else if (i == 2) tc.rearLeftMesh = wHolder.transform;
                    else if (i == 3) tc.rearRightMesh = wHolder.transform;
                }
                else if (ctrl is JCBController jcb)
                {
                    if (i == 0) jcb.meshFL = wHolder.transform;
                    else if (i == 1) jcb.meshFR = wHolder.transform;
                    else if (i == 2) jcb.meshRL = wHolder.transform;
                    else if (i == 3) jcb.meshRR = wHolder.transform;
                }
                else if (ctrl is HarvesterController hc)
                {
                    if (i == 0) hc.meshFL = wHolder.transform;
                    else if (i == 1) hc.meshFR = wHolder.transform;
                    else if (i == 2) hc.meshRL = wHolder.transform;
                    else if (i == 3) hc.meshRR = wHolder.transform;
                }
            }

            slot.placeholderVisuals = placeholderGo;
            slot.ValidateModelPresence();

            // Strip any rogue non-wheel colliders that might have leaked into child visual components
            var rootCol = root.GetComponent<BoxCollider>();
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                if (col != rootCol && !(col is WheelCollider))
                {
                    SafeDestroy(col);
                }
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            DestroyImmediate(obj);
        }

        private GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            Material mat = new Material(HVW_VisualSystem.GetSafeShader()) { name = $"M_{name}" };
            var tex = HVW_VisualSystem.CreateProceduralTexture(color, 32);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return CreateBox(name, parent, localPos, localScale, mat);
        }

        private GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var c = go.GetComponent<Collider>();
            if (c) SafeDestroy(c);
            return go;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. SMOOTH FOLLOW CAMERA SETUP
        // ══════════════════════════════════════════════════════════════════════
        private void SetupFollowCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            _followCamera = camGo.GetComponent<HVW_FollowCamera>();
            if (_followCamera == null)
            {
                _followCamera = camGo.AddComponent<HVW_FollowCamera>();
            }

            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;

            // Setup Photo Camera on a separate GameObject.
            // Camera must be added BEFORE HVW_PhotoCamera (RequireComponent order).
            var photoCamGo  = new GameObject("PhotoCamera");
            var photoCam    = photoCamGo.AddComponent<Camera>();   // ← Camera first
            photoCam.enabled      = false;
            photoCam.clearFlags   = CameraClearFlags.Skybox;
            photoCam.fieldOfView  = 55f;
            photoCam.nearClipPlane = 0.3f;
            photoCam.farClipPlane  = 1000f;
            _photoCamera = photoCamGo.AddComponent<HVW_PhotoCamera>(); // ← then PhotoCamera
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. MOBILE TOUCH CONTROLS UI CANVAS
        // ══════════════════════════════════════════════════════════════════════
        private void BuildMobileUI()
        {
            var canvasGo = new GameObject("HVW_MobileCanvas");
            _mobileCanvas = canvasGo.AddComponent<Canvas>();
            _mobileCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // ── Left: Steering Controls ───────────────────────────────────────
            CreateTouchButton(canvasGo, "Btn_SteerLeft", "◀ STEER",
                new Vector2(0f, 0f), new Vector2(140f, 130f), new Vector2(160f, 150f),
                new Color(0.15f, 0.18f, 0.22f, 0.85f),
                () => { _steerLeftHeld = true; HVW_DiagnosticsOverlay.IsSteerLeftPointerDown = true; },
                () => { _steerLeftHeld = false; HVW_DiagnosticsOverlay.IsSteerLeftPointerDown = false; });

            CreateTouchButton(canvasGo, "Btn_SteerRight", "STEER ▶",
                new Vector2(0f, 0f), new Vector2(320f, 130f), new Vector2(160f, 150f),
                new Color(0.15f, 0.18f, 0.22f, 0.85f),
                () => { _steerRightHeld = true; HVW_DiagnosticsOverlay.IsSteerRightPointerDown = true; },
                () => { _steerRightHeld = false; HVW_DiagnosticsOverlay.IsSteerRightPointerDown = false; });

            // ── Right: Pedals & Handbrake ─────────────────────────────────────
            // Gas Pedal (Large Green)
            CreateTouchButton(canvasGo, "Btn_Gas", "GAS / ACCEL\n▲",
                new Vector2(1f, 0f), new Vector2(-130f, 150f), new Vector2(140f, 190f),
                new Color(0.12f, 0.65f, 0.25f, 0.90f),
                () => { _accelHeld = true; HVW_DiagnosticsOverlay.IsGasPointerDown = true; },
                () => { _accelHeld = false; HVW_DiagnosticsOverlay.IsGasPointerDown = false; });

            // Brake / Reverse Pedal (Red)
            CreateTouchButton(canvasGo, "Btn_Brake", "BRAKE / REV\n▼",
                new Vector2(1f, 0f), new Vector2(-280f, 135f), new Vector2(130f, 160f),
                new Color(0.78f, 0.18f, 0.18f, 0.90f),
                () => { _brakeHeld = true; HVW_DiagnosticsOverlay.IsBrakePointerDown = true; },
                () => { _brakeHeld = false; HVW_DiagnosticsOverlay.IsBrakePointerDown = false; });

            // Handbrake (Yellow/Orange)
            CreateTouchButton(canvasGo, "Btn_Handbrake", "STOP / HB\n🛑",
                new Vector2(1f, 0f), new Vector2(-420f, 115f), new Vector2(120f, 120f),
                new Color(0.85f, 0.55f, 0.10f, 0.85f),
                () => { _handbrakeHeld = true; },
                () => { _handbrakeHeld = false; });

            // Vehicle Action Button 1 (Dump Bed / Loader / Plough / Header)
            _actionButtonGo = CreateClickButton(canvasGo, "Btn_VehicleAction", "ACTION",
                new Vector2(1f, 0f), new Vector2(-130f, 290f), new Vector2(140f, 55f),
                new Color(0.92f, 0.45f, 0.12f, 0.92f),
                () => { TriggerVehicleAction(1); });
            _actionButtonLabel = _actionButtonGo.GetComponentInChildren<Text>();

            // Vehicle Action Button 2 (Digging / Slew / Auger)
            _actionButtonGo2 = CreateClickButton(canvasGo, "Btn_VehicleAction2", "ACTION 2",
                new Vector2(1f, 0f), new Vector2(-130f, 360f), new Vector2(140f, 55f),
                new Color(0.15f, 0.65f, 0.95f, 0.92f),
                () => { TriggerVehicleAction(2); });
            _actionButtonLabel2 = _actionButtonGo2.GetComponentInChildren<Text>();

            // ── Top Toolbar: Vehicle Switcher (Row 1: Y = -22f) ────────────────
            var vehicleDefs = new (string label, VehicleType type)[]
            {
                ("CAR", VehicleType.Car),
                ("TRACTOR", VehicleType.Tractor),
                ("TRUCK", VehicleType.Truck),
                ("JCB", VehicleType.JCB),
                ("EXCAV", VehicleType.Excavator),
                ("HARVEST", VehicleType.Harvester)
            };

            for (int i = 0; i < vehicleDefs.Length; i++)
            {
                int idx = i;
                float xPos = 46f + (i * 88f);
                CreateClickButton(canvasGo, $"Btn_Select_{vehicleDefs[i].type}", vehicleDefs[i].label,
                    new Vector2(0f, 1f), new Vector2(xPos, -22f), new Vector2(82f, 30f),
                    new Color(0.12f, 0.45f, 0.78f, 0.90f),
                    () => { SwitchToVehicleIndex(idx); });
            }

            // Diagnostics Button (Top-Right, Row 1, left of Settings)
            var dbgBtn = CreateClickButton(canvasGo, "Btn_ToggleDBG", "DBG",
                new Vector2(1f, 1f), new Vector2(-286f, -22f), new Vector2(56f, 30f),
                new Color(0.35f, 0.15f, 0.55f, 0.90f),
                () => { if (_diagOverlay != null) _diagOverlay.Toggle(); });
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // For production release, hide the DBG button by default so HUD is clean
            dbgBtn.SetActive(false);
#endif

            // Settings Button (Top-Right, Row 1)
            CreateClickButton(canvasGo, "Btn_Settings", "SETTINGS",
                new Vector2(1f, 1f), new Vector2(-212f, -22f), new Vector2(76f, 30f),
                new Color(0.25f, 0.35f, 0.45f, 0.90f),
                () => { ToggleSettingsPanel(); });

            // Camera Toggle Button (Top-Right, Row 1)
            CreateClickButton(canvasGo, "Btn_CamView", "VIEW",
                new Vector2(1f, 1f), new Vector2(-130f, -22f), new Vector2(76f, 30f),
                new Color(0.25f, 0.25f, 0.30f, 0.90f),
                () => { if (_followCamera) _followCamera.ToggleCameraMode(); });

            // Reset Vehicle Button (Top-Right, Row 1)
            CreateClickButton(canvasGo, "Btn_ResetVeh", "RESET",
                new Vector2(1f, 1f), new Vector2(-48f, -22f), new Vector2(76f, 30f),
                new Color(0.35f, 0.35f, 0.40f, 0.90f),
                () => { ResetActiveVehicle(); });

            // Photo Camera Button (Top-Right, Row 1) — after RESET
            CreateClickButton(canvasGo, "Btn_PhotoCam", "PHOTO",
                new Vector2(1f, 1f), new Vector2(-130f, -60f), new Vector2(76f, 30f),
                new Color(0.20f, 0.65f, 0.35f, 0.90f),
                () => { TogglePhotoCamera(); });

            // ── Speedometer & Dashboard HUD (Row 2: Y = -76f, strictly below Row 1) ───
            var hudPanel = new GameObject("HUD_Dashboard");
            hudPanel.transform.SetParent(canvasGo.transform, false);
            var hudRect = hudPanel.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 1f);
            hudRect.anchorMax = new Vector2(0.5f, 1f);
            hudRect.anchoredPosition = new Vector2(0f, -76f);
            hudRect.sizeDelta = new Vector2(300f, 68f);
            var hudImg = hudPanel.AddComponent<Image>();
            hudImg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

            _speedometerText = CreateText(hudPanel, "Txt_Speed", "0 KM/H", 20, TextAnchor.MiddleCenter, new Vector2(0f, 16f), new Vector2(280f, 24f), Color.white);
            _gearText        = CreateText(hudPanel, "Txt_Gear", "GEAR: D1", 12, TextAnchor.MiddleCenter, new Vector2(-75f, -6f), new Vector2(110f, 18f), new Color(0.4f, 0.85f, 1f));
            _vehicleNameText = CreateText(hudPanel, "Txt_VehName", "Car (SUV)", 12, TextAnchor.MiddleCenter, new Vector2(75f, -6f), new Vector2(145f, 20f), new Color(1f, 0.85f, 0.3f));
            var stampText = CreateText(hudPanel, "Txt_BuildStamp", HVW_BuildInfo.FullStamp, 10, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(290f, 16f), new Color(0.75f, 0.75f, 0.75f));
            var stampBtn = stampText.gameObject.AddComponent<Button>();
            stampBtn.transition = Selectable.Transition.None;
            stampBtn.onClick.AddListener(() => { if (_diagOverlay != null) _diagOverlay.Toggle(); });

            // Initialize Diagnostics Overlay UI
            if (_diagOverlay != null)
            {
                _diagOverlay.InitializeUI(canvasGo);
            }

            // Build Settings Modal Dialog
            BuildSettingsModal(canvasGo);
        }

        private void BuildSettingsModal(GameObject canvasGo)
        {
            _settingsModalPanel = new GameObject("Panel_SettingsModal");
            _settingsModalPanel.transform.SetParent(canvasGo.transform, false);
            var rect = _settingsModalPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 380f);

            var bgImg = _settingsModalPanel.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.12f, 0.16f, 0.96f);

            // Title
            CreateText(_settingsModalPanel, "Txt_SettingsTitle", "⚙️ GRAPHICS & PERFORMANCE SETTINGS", 20, TextAnchor.MiddleCenter, new Vector2(0f, 150f), new Vector2(520f, 36f), Color.white);

            // Graphics Quality Section
            CreateText(_settingsModalPanel, "Lbl_GfxQuality", "GRAPHICS QUALITY:", 16, TextAnchor.MiddleLeft, new Vector2(-120f, 95f), new Vector2(260f, 28f), new Color(0.4f, 0.85f, 1f));
            int curQIdx = QualitySettings.GetQualityLevel();
            string qName = (curQIdx >= 0 && curQIdx < QualitySettings.names.Length) ? QualitySettings.names[curQIdx] : $"Tier {curQIdx}";
            _gfxQualityStatusText = CreateText(_settingsModalPanel, "Txt_QualityStatus", $"CURRENT: {qName}", 14, TextAnchor.MiddleRight, new Vector2(140f, 95f), new Vector2(220f, 28f), new Color(1f, 0.85f, 0.3f));

            CreateClickButton(_settingsModalPanel, "Btn_Quality_Low", "LOW", new Vector2(0.5f, 0.5f), new Vector2(-180f, 50f), new Vector2(110f, 38f), new Color(0.20f, 0.45f, 0.25f, 0.90f), () => SetGraphicsQuality(0));
            CreateClickButton(_settingsModalPanel, "Btn_Quality_Medium", "MEDIUM", new Vector2(0.5f, 0.5f), new Vector2(-60f, 50f), new Vector2(110f, 38f), new Color(0.20f, 0.40f, 0.65f, 0.90f), () => SetGraphicsQuality(1));
            CreateClickButton(_settingsModalPanel, "Btn_Quality_High", "HIGH", new Vector2(0.5f, 0.5f), new Vector2(60f, 50f), new Vector2(110f, 38f), new Color(0.25f, 0.35f, 0.70f, 0.90f), () => SetGraphicsQuality(2));
            CreateClickButton(_settingsModalPanel, "Btn_Quality_Ultra", "ULTRA", new Vector2(0.5f, 0.5f), new Vector2(180f, 50f), new Vector2(110f, 38f), new Color(0.55f, 0.25f, 0.55f, 0.90f), () => SetGraphicsQuality(3));

            // FPS Cap Section
            CreateText(_settingsModalPanel, "Lbl_FPSCap", "FRAME RATE CAP:", 16, TextAnchor.MiddleLeft, new Vector2(-120f, -10f), new Vector2(260f, 28f), new Color(0.4f, 0.85f, 1f));
            string curFpsText = Application.targetFrameRate > 0 ? $"{Application.targetFrameRate} FPS" : "UNCAPPED";
            _fpsCapStatusText = CreateText(_settingsModalPanel, "Txt_FPSStatus", $"CURRENT: {curFpsText}", 14, TextAnchor.MiddleRight, new Vector2(140f, -10f), new Vector2(220f, 28f), new Color(1f, 0.85f, 0.3f));

            CreateClickButton(_settingsModalPanel, "Btn_FPS_30", "30 FPS (Battery)", new Vector2(0.5f, 0.5f), new Vector2(-160f, -55f), new Vector2(140f, 38f), new Color(0.20f, 0.45f, 0.25f, 0.90f), () => SetFPSCap(30));
            CreateClickButton(_settingsModalPanel, "Btn_FPS_60", "60 FPS (Smooth)", new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(140f, 38f), new Color(0.20f, 0.40f, 0.65f, 0.90f), () => SetFPSCap(60));
            CreateClickButton(_settingsModalPanel, "Btn_FPS_Uncapped", "UNCAPPED", new Vector2(0.5f, 0.5f), new Vector2(160f, -55f), new Vector2(140f, 38f), new Color(0.55f, 0.35f, 0.15f, 0.90f), () => SetFPSCap(-1));

            // Close Button
            CreateClickButton(_settingsModalPanel, "Btn_CloseSettings", "✕ CLOSE", new Vector2(0.5f, 0.5f), new Vector2(0f, -135f), new Vector2(180f, 42f), new Color(0.75f, 0.20f, 0.20f, 0.95f), () => ToggleSettingsPanel());

            _settingsModalPanel.SetActive(false);
        }

        public void ToggleSettingsPanel()
        {
            if (_settingsModalPanel != null)
            {
                bool active = !_settingsModalPanel.activeSelf;
                _settingsModalPanel.SetActive(active);
                if (active) UpdateSettingsLabels();
            }
        }

        public void SetGraphicsQuality(int level)
        {
            var mgr = GraphicsSettingsManager.Instance;
            if (mgr != null)
            {
                mgr.ApplyQuality((HeavyVehiclesWorld.Systems.QualityLevel)level);
            }
            else
            {
                int targetQuality = level switch
                {
                    0 => 1, // Low
                    1 => 2, // Medium
                    2 => 3, // High
                    3 => 5, // Ultra
                    _ => 3
                };
                QualitySettings.SetQualityLevel(targetQuality, true);
                PlayerPrefs.SetInt("Settings_GraphicsQuality", level);
            }

            PlayerPrefs.SetInt("GFX_Quality_UserOverride", 1);
            PlayerPrefs.Save();

            UpdateSettingsLabels();
            Debug.Log($"[Settings] Graphics Quality changed to tier {level} (QualityLevel: {QualitySettings.names[QualitySettings.GetQualityLevel()]})");
        }

        public void SetFPSCap(int fps)
        {
            Application.targetFrameRate = fps;
            var mgr = GraphicsSettingsManager.Instance;
            if (mgr != null)
            {
                FPSCap cap = fps switch
                {
                    30 => FPSCap.Cap30,
                    60 => FPSCap.Cap60,
                    _  => FPSCap.Uncapped
                };
                mgr.SetFPSCap(cap);
            }
            else
            {
                PlayerPrefs.SetInt("Settings_FPSCap", fps);
                PlayerPrefs.Save();
            }

            UpdateSettingsLabels();
            Debug.Log($"[Settings] Target frame rate changed to: {(fps > 0 ? fps + " FPS" : "Uncapped")}");
        }

        private void UpdateSettingsLabels()
        {
            if (_gfxQualityStatusText != null)
            {
                int qIdx = QualitySettings.GetQualityLevel();
                string qName = (qIdx >= 0 && qIdx < QualitySettings.names.Length) ? QualitySettings.names[qIdx] : $"Tier {qIdx}";
                _gfxQualityStatusText.text = $"CURRENT: {qName}";
            }
            if (_fpsCapStatusText != null)
            {
                string fpsText = Application.targetFrameRate > 0 ? $"{Application.targetFrameRate} FPS" : "UNCAPPED";
                _fpsCapStatusText.text = $"CURRENT: {fpsText}";
            }
        }

        private Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 8;
            t.resizeTextMaxSize = fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.alignment = alignment;
            t.color = col;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return t;
        }

        private void CreateTouchButton(GameObject parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, System.Action onDown, System.Action onUp)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = btnGo.AddComponent<Image>();
            img.color = col;

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.fontSize = 20;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = 20;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var trigger = btnGo.AddComponent<EventTrigger>();

            var entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entryDown.callback.AddListener((data) => { onDown(); img.color = col * 1.3f; });
            trigger.triggers.Add(entryDown);

            var entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entryUp.callback.AddListener((data) => { onUp(); img.color = col; });
            trigger.triggers.Add(entryUp);

            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => { onUp(); img.color = col; });
            trigger.triggers.Add(entryExit);
        }

        private GameObject CreateClickButton(GameObject parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, System.Action onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = btnGo.AddComponent<Image>();
            img.color = col;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.fontSize = 15;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 9;
            t.resizeTextMaxSize = 15;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return btnGo;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. INPUT & HUD UPDATE
        // ══════════════════════════════════════════════════════════════════════
        private void HandleMobileInputSmoothing()
        {
            float dt = Time.deltaTime > 0.0001f ? Time.deltaTime : Time.fixedDeltaTime;
            if (dt <= 0.0001f) dt = 0.02f;

            // Steer smoothing
            float targetSteer = 0f;
            if (_steerLeftHeld) targetSteer -= 1f;
            if (_steerRightHeld) targetSteer += 1f;
            _currentSteer = Mathf.MoveTowards(_currentSteer, targetSteer, dt * 5f);

            // Throttle & Brake smoothing
            float targetThrottle = _accelHeld ? 1f : 0f;
            float targetBrake = _brakeHeld ? 1f : 0f;
            _currentThrottle = Mathf.MoveTowards(_currentThrottle, targetThrottle, dt * 6f);
            _currentBrake = Mathf.MoveTowards(_currentBrake, targetBrake, dt * 8f);

            float hb = _handbrakeHeld ? 1f : 0f;

            // Push into InputManagerProxy
            InputManagerProxy.MobileThrottle = _currentThrottle;
            InputManagerProxy.MobileBrake = _currentBrake;
            InputManagerProxy.MobileSteer = _currentSteer;
            InputManagerProxy.MobileHandBrake = hb;

            // Push into InputManager if present
            if (InputManager.Instance != null)
            {
                InputManager.Instance.MobileThrottle = _currentThrottle;
                InputManager.Instance.MobileBrake = _currentBrake;
                InputManager.Instance.MobileSteer = _currentSteer;
                InputManager.Instance.MobileHandbrake = hb;
                InputManager.Instance.MobileLeftJoystick = new Vector2(_currentSteer, _currentThrottle - _currentBrake);
            }
        }

        private void UpdateHUD()
        {
            if (_activeVehicle == null) return;

            float speedKmh = 0f;
            if (_activeVehicle.rb != null)
                speedKmh = _activeVehicle.rb.linearVelocity.magnitude * 3.6f;

            if (_speedometerText != null)
                _speedometerText.text = $"{Mathf.RoundToInt(speedKmh)} KM/H";

            if (_gearText != null)
            {
                string gearStr = "GEAR: D1";
                if (_brakeHeld && speedKmh < 0.5f)
                    gearStr = "GEAR: R";
                else if (_activeVehicle.currentGear == GearState.Reverse)
                    gearStr = "GEAR: R";
                else if (_activeVehicle.currentGear == GearState.Neutral)
                    gearStr = "GEAR: N";
                else
                    gearStr = "GEAR: D1";
                _gearText.text = gearStr;
            }

            if (_vehicleNameText != null)
            {
                string baseName = _activeVehicle.vehicleType switch
                {
                    VehicleType.Car => "Car (SUV)",
                    VehicleType.Tractor => "Tractor (Agri)",
                    VehicleType.Truck => "Truck (Tipper)",
                    VehicleType.JCB => "JCB (Backhoe)",
                    VehicleType.Excavator => "Excavator",
                    VehicleType.Harvester => "Harvester",
                    _ => _activeVehicle.VehicleName
                };

                if (_activeVehicle is HarvesterController hc)
                    _vehicleNameText.text = $"{baseName} | {hc.grainLevel:F0}%";
                else if (_activeVehicle is ExcavatorController ec)
                    _vehicleNameText.text = $"{baseName} | {ec.currentSoilVolume:P0}";
                else if (_activeVehicle is JCBController jcb)
                    _vehicleNameText.text = $"{baseName} | {jcb.soilPayload:P0}";
                else if (_activeVehicle is TruckController tk)
                    _vehicleNameText.text = $"{baseName} | {tk.currentPayload:F0}kg";
                else
                    _vehicleNameText.text = baseName;
            }
        }

        public void SelectVehicle(VehicleController vc)
        {
            if (vc == null) return;

            // Safely park previously active vehicle
            if (_activeVehicle != null && _activeVehicle != vc)
            {
                _activeVehicle.isOccupied = false;
                _activeVehicle.throttleInput = 0f;
                _activeVehicle.brakeInput = 0f;
            }

            _activeVehicle = vc;
            _activeVehicle.engineRunning = true;
            _activeVehicle.isOccupied = true;
            _activeVehicle.currentGear = GearState.Drive;
            _activeVehicle.throttleInput = 0f;
            _activeVehicle.brakeInput = 0f;

            if (_activeVehicle is JCBController jcbCtrl)
            {
                jcbCtrl.RetractStabilizers();
            }

            if (_activeVehicle.rb != null && _activeVehicle.rb.IsSleeping())
            {
                _activeVehicle.rb.WakeUp();
            }

            if (_followCamera != null)
            {
                _followCamera.SetTarget(_activeVehicle.transform);
                _followCamera.currentMode = HVW_FollowCamera.ViewMode.ThirdPerson;
            }

            // If photo camera is active, re-target it to new vehicle
            if (_photoCamera != null && _photoCamera.IsActive)
            {
                _photoCamera.Activate(_activeVehicle.transform);
            }

            // Reset touch input state so new vehicle starts clean
            _currentSteer = 0f;
            _currentThrottle = 0f;
            _currentBrake = 0f;
            _steerLeftHeld = false;
            _steerRightHeld = false;
            _accelHeld = false;
            _brakeHeld = false;
            _handbrakeHeld = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SwitchVehicle(_activeVehicle);
            }

            UpdateActionUI();
            UpdateHUD();
        }

        private void TriggerVehicleAction(int buttonIndex = 1)
        {
            if (_activeVehicle == null) return;
            if (_activeVehicle is TruckController tk)
            {
                tk.ToggleDumping();
                UpdateActionUI();
            }
            else if (_activeVehicle is TractorController tc)
            {
                tc.ToggleHitch();
                UpdateActionUI();
            }
            else if (_activeVehicle is JCBController jcb)
            {
                if (buttonIndex == 1)
                {
                    jcb.ToggleFrontLoader();
                }
                else
                {
                    jcb.TriggerDigStroke();
                }
                UpdateActionUI();
            }
            else if (_activeVehicle is ExcavatorController ec)
            {
                if (buttonIndex == 1)
                {
                    if (ec.currentSoilVolume > 0.05f)
                        ec.DumpSoil();
                    else
                        ec.TriggerDigStroke();
                }
                else
                {
                    ec.SlewCabinStep(1f);
                }
                UpdateActionUI();
            }
            else if (_activeVehicle is HarvesterController hc)
            {
                if (buttonIndex == 1)
                {
                    hc.RaiseLowerHeader(!hc.headerLowered);
                }
                else
                {
                    hc.ToggleAuger();
                }
                UpdateActionUI();
            }
        }

        private void UpdateActionUI()
        {
            if (_actionButtonGo == null || _activeVehicle == null) return;

            if (_activeVehicle is TruckController tk)
            {
                _actionButtonGo.SetActive(true);
                if (_actionButtonLabel != null)
                    _actionButtonLabel.text = tk.isDumping ? "🚛 LOWER\nBED" : "🚛 DUMP\nBED";
                if (_actionButtonGo2 != null) _actionButtonGo2.SetActive(false);
            }
            else if (_activeVehicle is TractorController tc)
            {
                _actionButtonGo.SetActive(true);
                if (_actionButtonLabel != null)
                    _actionButtonLabel.text = (tc.hitchPosition < 0.5f) ? "🚜 RAISE\nPLOUGH" : "🚜 LOWER\nPLOUGH";
                if (_actionButtonGo2 != null) _actionButtonGo2.SetActive(false);
            }
            else if (_activeVehicle is JCBController jcb)
            {
                _actionButtonGo.SetActive(true);
                if (_actionButtonLabel != null)
                    _actionButtonLabel.text = jcb.isLoaderRaised ? "🏗️ LOWER\nLOADER" : "🏗️ RAISE\nLOADER";

                if (_actionButtonGo2 != null)
                {
                    _actionButtonGo2.SetActive(true);
                    if (_actionButtonLabel2 != null)
                        _actionButtonLabel2.text = "⛏️ DIG\nSTROKE";
                }
            }
            else if (_activeVehicle is ExcavatorController ec)
            {
                _actionButtonGo.SetActive(true);
                if (_actionButtonLabel != null)
                    _actionButtonLabel.text = (ec.currentSoilVolume > 0.05f) ? "🪣 DUMP\nSOIL" : "⛏️ DIG\nSTROKE";

                if (_actionButtonGo2 != null)
                {
                    _actionButtonGo2.SetActive(true);
                    if (_actionButtonLabel2 != null)
                        _actionButtonLabel2.text = "🔄 SLEW\nCABIN";
                }
            }
            else if (_activeVehicle is HarvesterController hc)
            {
                _actionButtonGo.SetActive(true);
                if (_actionButtonLabel != null)
                    _actionButtonLabel.text = hc.headerLowered ? "🌾 RAISE\nHEADER" : "🌾 LOWER\nHEADER";

                if (_actionButtonGo2 != null)
                {
                    _actionButtonGo2.SetActive(true);
                    if (_actionButtonLabel2 != null)
                    {
                        if (!hc.augerExtended)
                            _actionButtonLabel2.text = "🚜 EXTEND\nAUGER";
                        else if (hc.isUnloading)
                            _actionButtonLabel2.text = "⏹️ STOP\nUNLOAD";
                        else
                            _actionButtonLabel2.text = "🌾 UNLOAD\nGRAIN";
                    }
                }
            }
            else
            {
                _actionButtonGo.SetActive(false);
                if (_actionButtonGo2 != null) _actionButtonGo2.SetActive(false);
            }
        }

        public void SwitchToVehicleIndex(int index)
        {
            if (index >= 0 && index < _allVehicles.Count)
            {
                SelectVehicle(_allVehicles[index]);
            }
        }

        /// <summary>Switch to the first vehicle of the given type. Used by screenshot automation.</summary>
        public void SwitchToVehicle(Vehicles.VehicleType type)
        {
            var vc = _allVehicles.Find(v => v.vehicleType == type);
            if (vc != null) SelectVehicle(vc);
        }

        public void ResetActiveVehicle()
        {
            if (_activeVehicle == null || _activeVehicle.rb == null) return;
            var rb = _activeVehicle.rb;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 curPos = _activeVehicle.transform.position;
            _activeVehicle.transform.position = new Vector3(curPos.x, curPos.y + 1.2f, curPos.z);
            _activeVehicle.transform.rotation = Quaternion.Euler(0f, _activeVehicle.transform.eulerAngles.y, 0f);
        }

        /// <summary>
        /// Toggle the debug photo orbit camera on/off.
        /// When active, the follow camera is disabled so only one camera renders.
        /// </summary>
        private void TogglePhotoCamera()
        {
            if (_photoCamera == null) return;

            if (_photoCamera.IsActive)
            {
                _photoCamera.Deactivate();
                // Re-enable the follow camera
                if (_followCamera != null) _followCamera.enabled = true;
            }
            else
            {
                if (_activeVehicle == null) return;
                // Disable follow camera while photo mode is active
                if (_followCamera != null) _followCamera.enabled = false;
                _photoCamera.Activate(_activeVehicle.transform);
            }
        }
    }
}
