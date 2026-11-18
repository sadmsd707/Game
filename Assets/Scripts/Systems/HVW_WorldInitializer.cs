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
        private HVW_FollowCamera _followCamera;
        private Canvas _mobileCanvas;
        private Text _speedometerText;
        private Text _gearText;
        private Text _vehicleNameText;

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

            // Initialize Master Visual & PBR System first
            if (HVW_VisualSystem.Instance == null)
            {
                var visGo = GameObject.Find("HVW_VisualSystem");
                if (visGo == null) visGo = new GameObject("HVW_VisualSystem");
                visGo.AddComponent<HVW_VisualSystem>();
            }

            BuildWorldEnvironment();
            SpawnAllVehicles();
            SetupFollowCamera();
            BuildMobileUI();

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
            }

            // Road Circuit (Asphalt Loop)
            BuildRoadTrack();

            // Props (Trees with Colliders, Construction Zone, Farm Fields, Crates)
            BuildWorldProps();
        }

        private void BuildRoadTrack()
        {
            var roadRoot = new GameObject("RoadNetwork");
            var vis = HVW_VisualSystem.Instance;
            var asphaltMat = (vis != null && vis.asphaltRoadPBR != null) ? vis.asphaltRoadPBR : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.18f, 0.18f, 0.20f) };
            var stripeMat  = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.95f, 0.85f, 0.15f) };
            var curbMat    = (vis != null && vis.concreteCurbsPBR != null) ? vis.concreteCurbsPBR : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.85f, 0.25f, 0.25f) };

            // Main Straight Road (North-South) - 400m long, 14m wide
            CreateRoadSegment(roadRoot, "Road_MainStraight", new Vector3(0, 0.02f, 0), new Vector3(14f, 0.05f, 400f), asphaltMat, stripeMat, curbMat);

            // East-West Cross Road - 300m long, 14m wide
            CreateRoadSegment(roadRoot, "Road_CrossRoad", new Vector3(0, 0.02f, 50f), new Vector3(300f, 0.05f, 14f), asphaltMat, stripeMat, curbMat);

            // Construction Site Ground (Dirt / Mud Brown)
            var constrGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            constrGround.name = "Zone_ConstructionDirt";
            constrGround.transform.SetParent(roadRoot.transform);
            constrGround.transform.position = new Vector3(35f, 0.03f, 35f);
            constrGround.transform.localScale = new Vector3(60f, 0.05f, 60f);
            constrGround.GetComponent<Renderer>().sharedMaterial = (vis != null && vis.constructionPropsPBR != null) ? vis.constructionPropsPBR : new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.38f, 0.22f) };

            // Farm Field (Golden Wheat Yellow/Amber)
            var farmGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farmGround.name = "Zone_WheatField";
            farmGround.transform.SetParent(roadRoot.transform);
            farmGround.transform.position = new Vector3(45f, 0.03f, -40f);
            farmGround.transform.localScale = new Vector3(80f, 0.05f, 80f);
            farmGround.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.82f, 0.68f, 0.25f) };

            // Quarry Dig Site (Grey Rock / Gravel)
            var quarryGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quarryGround.name = "Zone_QuarryGravel";
            quarryGround.transform.SetParent(roadRoot.transform);
            quarryGround.transform.position = new Vector3(-35f, 0.03f, 35f);
            quarryGround.transform.localScale = new Vector3(60f, 0.05f, 60f);
            quarryGround.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.40f, 0.40f, 0.42f) };
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
            if (c) Destroy(c);
        }

        private void BuildWorldProps()
        {
            var propsRoot = new GameObject("WorldProps");
            var vis = HVW_VisualSystem.Instance;
            var treeWoodMat   = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.22f, 0.12f) };
            var treeLeavesMat = new Material(Shader.Find("Standard")) { color = new Color(0.18f, 0.45f, 0.15f) };
            var coneMat       = (vis != null && vis.constructionPropsPBR != null) ? vis.constructionPropsPBR : new Material(Shader.Find("Standard")) { color = new Color(1f, 0.45f, 0.05f) };
            var crateMat      = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.45f, 0.25f) };

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

            // Dirt Mounds for Excavator & JCB (with colliders)
            for (int i = 0; i < 3; i++)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.transform.SetParent(propsRoot.transform);
                mound.transform.position = new Vector3(28f + i * 8f, 0.8f, 40f + i * 5f);
                mound.transform.localScale = new Vector3(7f, 3.5f, 7f);
                mound.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.48f, 0.32f, 0.18f) };
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
            if (leafCol) Destroy(leafCol);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. VEHICLE SPAWNING & RIGGING (ALL 6 VEHICLES)
        // ══════════════════════════════════════════════════════════════════════
        private void SpawnAllVehicles()
        {
            _allVehicles.Clear();

            // 1. CAR (SUV) - Start position (0, 0.5, 0)
            var car = CreateVehicleEntity("Car_SUV", VehicleType.Car, new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                new Color(0.12f, 0.45f, 0.85f), // Cobalt Blue
                new Vector3(4.2f, 1.4f, 2.0f), 1500f);
            var carCtrl = car.AddComponent<CarController>();
            carCtrl.vehicleType = VehicleType.Car;
            SetupVehiclePhysicsAndWheels(car, carCtrl, 0.4f, 1500f);
            _allVehicles.Add(carCtrl);

            // 2. TRACTOR - Farm entrance (15, 0.5, 8)
            var tractor = CreateVehicleEntity("Tractor_Agri", VehicleType.Tractor, new Vector3(15f, 0.5f, 8f), Quaternion.Euler(0, 45, 0),
                new Color(0.18f, 0.65f, 0.22f), // Farm Green
                new Vector3(3.8f, 2.2f, 2.2f), 4000f);
            var tractorCtrl = tractor.AddComponent<TractorController>();
            tractorCtrl.vehicleType = VehicleType.Tractor;
            SetupVehiclePhysicsAndWheels(tractor, tractorCtrl, 0.55f, 4000f);
            _allVehicles.Add(tractorCtrl);

            // 3. TRUCK (Heavy Tipper) - (-18, 0.6, 15)
            var truck = CreateVehicleEntity("Truck_Tipper", VehicleType.Truck, new Vector3(-18f, 0.6f, 15f), Quaternion.Euler(0, -30, 0),
                new Color(0.85f, 0.85f, 0.88f), // Industrial White
                new Vector3(7.5f, 2.8f, 2.6f), 8000f);
            var truckCtrl = truck.AddComponent<TruckController>();
            truckCtrl.vehicleType = VehicleType.Truck;
            SetupVehiclePhysicsAndWheels(truck, truckCtrl, 0.6f, 8000f);
            _allVehicles.Add(truckCtrl);

            // 4. JCB (Backhoe Loader) - (28, 0.6, 28)
            var jcb = CreateVehicleEntity("JCB_Backhoe", VehicleType.JCB, new Vector3(28f, 0.6f, 28f), Quaternion.Euler(0, 90, 0),
                new Color(0.98f, 0.78f, 0.05f), // JCB Yellow
                new Vector3(5.2f, 2.6f, 2.4f), 7500f);
            var jcbCtrl = jcb.AddComponent<JCBController>();
            jcbCtrl.vehicleType = VehicleType.JCB;
            SetupVehiclePhysicsAndWheels(jcb, jcbCtrl, 0.55f, 7500f);
            _allVehicles.Add(jcbCtrl);

            // 5. EXCAVATOR - (-28, 0.6, 32)
            var excavator = CreateVehicleEntity("Excavator_Tracked", VehicleType.Excavator, new Vector3(-28f, 0.6f, 32f), Quaternion.Euler(0, -90, 0),
                new Color(0.95f, 0.45f, 0.08f), // Construction Orange
                new Vector3(5.5f, 3.0f, 3.0f), 12000f);
            var excCtrl = excavator.AddComponent<ExcavatorController>();
            excCtrl.vehicleType = VehicleType.Excavator;
            SetupVehiclePhysicsAndWheels(excavator, excCtrl, 0.6f, 12000f);
            _allVehicles.Add(excCtrl);

            // 6. HARVESTER - (42, 0.8, -25)
            var harvester = CreateVehicleEntity("Harvester_Combine", VehicleType.Harvester, new Vector3(42f, 0.8f, -25f), Quaternion.Euler(0, 180, 0),
                new Color(0.12f, 0.55f, 0.18f), // Combine Green
                new Vector3(6.8f, 3.4f, 3.5f), 11000f);
            var harvCtrl = harvester.AddComponent<HarvesterController>();
            harvCtrl.vehicleType = VehicleType.Harvester;
            SetupVehiclePhysicsAndWheels(harvester, harvCtrl, 0.7f, 11000f);
            _allVehicles.Add(harvCtrl);
        }

        private GameObject CreateVehicleEntity(string name, VehicleType type, Vector3 pos, Quaternion rot, Color bodyColor, Vector3 bodySize, float mass)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            root.transform.rotation = rot;

            // Rigidbody
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.drag = 0.08f;
            rb.angularDrag = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // AudioSource
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = 0.5f;

            // Main Chassis Physics Collider
            var rootCol = root.AddComponent<BoxCollider>();
            rootCol.size = bodySize;
            rootCol.center = new Vector3(0f, bodySize.y * 0.5f, 0f);

            // Empty "Model" child slot for imported 3D asset (FBX/OBJ/GLTF)
            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(root.transform, false);

            var slot = root.AddComponent<VehicleModelSlot>();
            slot.modelSlot = modelGo.transform;
            slot.vehicleType = type;

            return root;
        }

        private void SetupVehiclePhysicsAndWheels(GameObject root, VehicleController ctrl, float wheelRadius, float mass)
        {
            float halfW = 1.0f;
            float halfL = 1.6f;

            Vector3[] wheelOffsets = new[]
            {
                new Vector3(-halfW, wheelRadius,  halfL), // FL
                new Vector3( halfW, wheelRadius,  halfL), // FR
                new Vector3(-halfW, wheelRadius, -halfL), // RL
                new Vector3( halfW, wheelRadius, -halfL)  // RR
            };

            var wcs = new WheelCollider[4];
            var bindings = new VehicleModelSlot.WheelBinding[4];

            for (int i = 0; i < 4; i++)
            {
                var wGo = new GameObject($"WheelCollider_{i}");
                wGo.transform.SetParent(root.transform, false);
                wGo.transform.localPosition = wheelOffsets[i];

                var wc = wGo.AddComponent<WheelCollider>();
                wc.radius = wheelRadius;
                wc.suspensionDistance = 0.25f;

                var spring = wc.suspensionSpring;
                spring.spring = mass * 8f;
                spring.damper = mass * 0.8f;
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
                slot.ValidateModelPresence();
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
                () => { _steerLeftHeld = true; },
                () => { _steerLeftHeld = false; });

            CreateTouchButton(canvasGo, "Btn_SteerRight", "STEER ▶",
                new Vector2(0f, 0f), new Vector2(320f, 130f), new Vector2(160f, 150f),
                new Color(0.15f, 0.18f, 0.22f, 0.85f),
                () => { _steerRightHeld = true; },
                () => { _steerRightHeld = false; });

            // ── Right: Pedals & Handbrake ─────────────────────────────────────
            // Gas Pedal (Large Green)
            CreateTouchButton(canvasGo, "Btn_Gas", "GAS / ACCEL\n▲",
                new Vector2(1f, 0f), new Vector2(-130f, 160f), new Vector2(140f, 210f),
                new Color(0.12f, 0.65f, 0.25f, 0.90f),
                () => { _accelHeld = true; },
                () => { _accelHeld = false; });

            // Brake / Reverse Pedal (Red)
            CreateTouchButton(canvasGo, "Btn_Brake", "BRAKE / REV\n▼",
                new Vector2(1f, 0f), new Vector2(-280f, 135f), new Vector2(130f, 160f),
                new Color(0.78f, 0.18f, 0.18f, 0.90f),
                () => { _brakeHeld = true; },
                () => { _brakeHeld = false; });

            // Handbrake (Yellow/Orange)
            CreateTouchButton(canvasGo, "Btn_Handbrake", "STOP / HB\n🛑",
                new Vector2(1f, 0f), new Vector2(-420f, 115f), new Vector2(120f, 120f),
                new Color(0.85f, 0.55f, 0.10f, 0.85f),
                () => { _handbrakeHeld = true; },
                () => { _handbrakeHeld = false; });

            // ── Top Toolbar: Vehicle Switcher ────────────────────────────────
            var vehicleDefs = new (string label, VehicleType type)[]
            {
                ("🚗 CAR", VehicleType.Car),
                ("🚜 TRACTOR", VehicleType.Tractor),
                ("🚛 TRUCK", VehicleType.Truck),
                ("🏗️ JCB", VehicleType.JCB),
                ("⛏️ EXCAVATOR", VehicleType.Excavator),
                ("🌾 HARVESTER", VehicleType.Harvester)
            };

            for (int i = 0; i < vehicleDefs.Length; i++)
            {
                int idx = i;
                float xPos = 60f + (i * 112f);
                CreateClickButton(canvasGo, $"Btn_Select_{vehicleDefs[i].type}", vehicleDefs[i].label,
                    new Vector2(0f, 1f), new Vector2(xPos, -35f), new Vector2(104f, 38f),
                    new Color(0.12f, 0.45f, 0.78f, 0.90f),
                    () => { SwitchToVehicleIndex(idx); });
            }

            // Camera Toggle Button (Top-Right)
            CreateClickButton(canvasGo, "Btn_CamView", "📷 VIEW",
                new Vector2(1f, 1f), new Vector2(-170f, -35f), new Vector2(100f, 38f),
                new Color(0.25f, 0.25f, 0.30f, 0.90f),
                () => { if (_followCamera) _followCamera.ToggleCameraMode(); });

            // Reset Vehicle Button (Top-Right)
            CreateClickButton(canvasGo, "Btn_ResetVeh", "🔄 RESET",
                new Vector2(1f, 1f), new Vector2(-60f, -35f), new Vector2(100f, 38f),
                new Color(0.35f, 0.35f, 0.40f, 0.90f),
                () => { ResetActiveVehicle(); });

            // ── Top Center: Speedometer & Dashboard HUD (Deconflicted Layout) ───
            var hudPanel = new GameObject("HUD_Dashboard");
            hudPanel.transform.SetParent(canvasGo.transform, false);
            var hudRect = hudPanel.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.5f, 1f);
            hudRect.anchorMax = new Vector2(0.5f, 1f);
            hudRect.anchoredPosition = new Vector2(0f, -42f);
            hudRect.sizeDelta = new Vector2(260f, 64f);
            var hudImg = hudPanel.AddComponent<Image>();
            hudImg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

            _speedometerText = CreateText(hudPanel, "Txt_Speed", "0 KM/H", 24, TextAnchor.MiddleCenter, new Vector2(0f, 10f), new Vector2(240f, 30f), Color.white);
            _gearText        = CreateText(hudPanel, "Txt_Gear", "GEAR: D1", 14, TextAnchor.MiddleCenter, new Vector2(-65f, -16f), new Vector2(110f, 24f), new Color(0.4f, 0.85f, 1f));
            _vehicleNameText = CreateText(hudPanel, "Txt_VehName", "Car (SUV)", 14, TextAnchor.MiddleCenter, new Vector2(65f, -16f), new Vector2(120f, 24f), new Color(1f, 0.85f, 0.3f));
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
        }

        private void CreateClickButton(GameObject parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, System.Action onClick)
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
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. INPUT & HUD UPDATE
        // ══════════════════════════════════════════════════════════════════════
        private void HandleMobileInputSmoothing()
        {
            // Steer smoothing
            float targetSteer = 0f;
            if (_steerLeftHeld) targetSteer -= 1f;
            if (_steerRightHeld) targetSteer += 1f;
            _currentSteer = Mathf.MoveTowards(_currentSteer, targetSteer, Time.deltaTime * 5f);

            // Throttle & Brake smoothing
            float targetThrottle = _accelHeld ? 1f : 0f;
            float targetBrake = _brakeHeld ? 1f : 0f;
            _currentThrottle = Mathf.MoveTowards(_currentThrottle, targetThrottle, Time.deltaTime * 6f);
            _currentBrake = Mathf.MoveTowards(_currentBrake, targetBrake, Time.deltaTime * 8f);

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
                string gearStr = _brakeHeld && speedKmh < 1f ? "GEAR: R" : "GEAR: D1";
                _gearText.text = gearStr;
            }

            if (_vehicleNameText != null)
                _vehicleNameText.text = _activeVehicle.VehicleName;
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

            if (_followCamera != null)
            {
                _followCamera.SetTarget(_activeVehicle.transform);
                _followCamera.currentMode = HVW_FollowCamera.ViewMode.ThirdPerson;
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
        }

        public void SwitchToVehicleIndex(int index)
        {
            if (index >= 0 && index < _allVehicles.Count)
            {
                SelectVehicle(_allVehicles[index]);
            }
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
    }
}
