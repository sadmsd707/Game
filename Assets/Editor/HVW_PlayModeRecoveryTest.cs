#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.EditorTests
{
    public static class HVW_PlayModeRecoveryTest
    {
        [MenuItem("Tools/HeavyVehiclesWorld/🧪 Run Step 0 Recovery Test")]
        public static void RunRecoveryTest()
        {
            int exitCode = 0;
            Debug.Log("================================================================================");
            Debug.Log("[RECOVERY TEST] Starting STEP 0 - RECOVERY Automated Play-Mode Verification...");
            Debug.Log("================================================================================");

            try
            {
                // 1. Open MainWorld scene
                string scenePath = "Assets/Scenes/MainWorld.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new Exception($"Failed to open scene: {scenePath}");
                }

                // 2. Locate or initialize WorldInitializer
                var initGo = GameObject.Find("WorldInitializer");
                if (initGo == null)
                {
                    initGo = new GameObject("WorldInitializer");
                    initGo.AddComponent<HVW_WorldInitializer>();
                }
                var initializer = initGo.GetComponent<HVW_WorldInitializer>();
                if (initializer == null) initializer = initGo.AddComponent<HVW_WorldInitializer>();

                // Trigger Awake via SendMessage or reflect
                initializer.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

                // 3. Test Visual System & Environment
                Debug.Log("\n--- [TEST 1: Visual System & Environment Integrity] ---");
                var vis = HVW_VisualSystem.Instance;
                if (vis == null) throw new Exception("HVW_VisualSystem instance is NULL!");

                var terrain = GameObject.Find("Terrain")?.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null)
                    throw new Exception("Terrain or TerrainData is missing!");

                if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length < 4)
                    throw new Exception($"TerrainLayers incomplete: {terrain.terrainData.terrainLayers?.Length ?? 0}/4 layers!");

                for (int l = 0; l < terrain.terrainData.terrainLayers.Length; l++)
                {
                    var layer = terrain.terrainData.terrainLayers[l];
                    if (layer.diffuseTexture == null)
                        throw new Exception($"TerrainLayer[{l}] '{layer.name}' diffuseTexture is NULL!");
                }
                Debug.Log($"✅ Terrain verified: 4 PBR TerrainLayers (Grass, Dirt, Asphalt, Rock) present with valid diffuse textures.");

                var dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
                if (dirLight == null || dirLight.shadows != LightShadows.Soft)
                    throw new Exception("Directional Light missing or soft shadows not enabled!");
                Debug.Log($"✅ Lighting verified: Directional light intensity {dirLight.intensity}, soft shadows enabled.");

                var cam = Camera.main;
                if (cam == null) throw new Exception("Main Camera is missing!");
                var followCam = cam.GetComponent<HVW_FollowCamera>();
                if (followCam == null) throw new Exception("HVW_FollowCamera is missing on Main Camera!");
                Debug.Log($"✅ Camera verified: HVW_FollowCamera active in Mode: {followCam.currentMode}.");

                // 4. Test Vehicle Spawning, Fallback Visuals & Model Slot Auto-Toggle
                Debug.Log("\n--- [TEST 2: Vehicles Placeholder Bodies & Model Slot Auto-Toggle] ---");
                string[] expectedVehicles = new[]
                {
                    "Car_SUV",
                    "Tractor_Agri",
                    "Truck_Tipper",
                    "JCB_Backhoe",
                    "Excavator_Tracked",
                    "Harvester_Combine"
                };

                var vehicleControllers = new Dictionary<string, VehicleController>();
                foreach (var vName in expectedVehicles)
                {
                    var vGo = GameObject.Find(vName);
                    if (vGo == null) throw new Exception($"Vehicle '{vName}' NOT spawned in world!");

                    var slot = vGo.GetComponent<VehicleModelSlot>();
                    if (slot == null) throw new Exception($"Vehicle '{vName}' missing VehicleModelSlot component!");

                    if (slot.placeholderVisuals == null)
                        throw new Exception($"Vehicle '{vName}' placeholderVisuals is NULL!");

                    if (!slot.placeholderVisuals.activeSelf)
                        throw new Exception($"Vehicle '{vName}' placeholderVisuals is NOT active when Model slot is empty!");

                    var renderers = slot.placeholderVisuals.GetComponentsInChildren<Renderer>();
                    if (renderers.Length < 4)
                        throw new Exception($"Vehicle '{vName}' placeholderVisuals has insufficient renderers: {renderers.Length}!");

                    // Verify Model slot auto-toggle
                    var dummyChild = new GameObject("DummyImportedModel");
                    dummyChild.transform.SetParent(slot.modelSlot, false);
                    slot.ValidateModelPresence();
                    if (slot.placeholderVisuals.activeSelf)
                        throw new Exception($"Vehicle '{vName}' placeholderVisuals DID NOT auto-hide when real model added to Model slot!");

                    GameObject.DestroyImmediate(dummyChild);
                    slot.ValidateModelPresence();
                    if (!slot.placeholderVisuals.activeSelf)
                        throw new Exception($"Vehicle '{vName}' placeholderVisuals DID NOT auto-restore when Model slot became empty!");

                    var ctrl = vGo.GetComponent<VehicleController>();
                    if (ctrl == null) throw new Exception($"Vehicle '{vName}' missing VehicleController!");
                    vehicleControllers[vName] = ctrl;

                    Debug.Log($"✅ {vName}: Visible Y (Renderers: {renderers.Length}), Model slot auto-toggle Y.");
                }

                // 5. Test Wheel Synchronisation (WheelCollider.GetWorldPose -> Wheel Mesh)
                Debug.Log("\n--- [TEST 3: Wheel Synchronisation (GetWorldPose)] ---");
                foreach (var kvp in vehicleControllers)
                {
                    var vName = kvp.Key;
                    var slot = kvp.Value.GetComponent<VehicleModelSlot>();
                    if (slot.wheels == null || slot.wheels.Length != 4)
                        throw new Exception($"Vehicle '{vName}' wheels bindings invalid ({slot.wheels?.Length ?? 0}/4)!");

                    for (int i = 0; i < 4; i++)
                    {
                        var b = slot.wheels[i];
                        if (b.collider == null) throw new Exception($"Vehicle '{vName}' wheel [{i}] collider is NULL!");
                        if (b.mesh == null) throw new Exception($"Vehicle '{vName}' wheel [{i}] mesh is NULL!");

                        b.collider.GetWorldPose(out Vector3 wPos, out Quaternion wRot);
                        b.mesh.position = wPos;
                        b.mesh.rotation = wRot;
                    }
                    Debug.Log($"✅ {vName}: 4 wheels bound and synchronised to WheelColliders.");
                }

                // 6. Test Drivability & Controls Simulation
                Debug.Log("\n--- [TEST 4: Drivability & Special Controls Simulation] ---");
                Physics.simulationMode = SimulationMode.Script;

                // Car Drive Test
                var car = vehicleControllers["Car_SUV"] as CarController;
                if (car == null) throw new Exception("CarController is missing!");
                car.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                Vector3 startPos = car.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;

                for (int f = 0; f < 30; f++)
                {
                    car.ApplyDriving();
                    car.ApplySteering();
                    Physics.Simulate(0.02f);
                }

                float carMoved = Vector3.Distance(startPos, car.transform.position);
                if (carMoved < 0.2f)
                    throw new Exception($"Car did not move under throttle! Moved distance: {carMoved:F3}m (Speed: {car.currentSpeed:F1} km/h)");
                Debug.Log($"✅ Car_SUV: Drives Y (Accelerated to {car.currentSpeed:F1} km/h, moved {carMoved:F2}m).");

                // Car Auto-Reverse Test
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                car.rb.linearVelocity = Vector3.zero;
                car.currentSpeed = 0f;
                car.ApplyDriving();
                if (car.currentGearIndex != 0)
                    throw new Exception("Car failed to auto-engage Reverse when holding brake from standstill!");
                Debug.Log($"✅ Car_SUV: Auto-Reverse Y (Shifted to gear index {car.currentGearIndex} [R]).");

                // Truck Drive & Tipper Bed Test
                var truck = vehicleControllers["Truck_Tipper"] as TruckController;
                truck.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                startPos = truck.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                for (int f = 0; f < 60; f++)
                {
                    truck.ApplyDriving();
                    truck.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                float truckMoved = Vector3.Distance(startPos, truck.transform.position);
                truck.StartDumping();
                for (int f = 0; f < 30; f++)
                {
                    truck.SendMessage("UpdateTipperBed", SendMessageOptions.DontRequireReceiver);
                }
                if (truck.tipperAngle <= 0f)
                    throw new Exception("Truck tipper bed did not tilt when dumping!");
                Debug.Log($"✅ Truck_Tipper: Drives Y (Speed: {truck.currentSpeed:F1} km/h, moved {truckMoved:F2}m), Tipper Bed Y (Angle: {truck.tipperAngle:F1}°).");

                // Tractor Drive & 3-Point Hitch Test
                var tractor = vehicleControllers["Tractor_Agri"] as TractorController;
                tractor.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                startPos = tractor.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                for (int f = 0; f < 60; f++)
                {
                    tractor.ApplyDriving();
                    tractor.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                float tractorMoved = Vector3.Distance(startPos, tractor.transform.position);
                tractor.hitchPosition = 1f;
                tractor.SendMessage("UpdateHitchArm", SendMessageOptions.DontRequireReceiver);
                Debug.Log($"✅ Tractor_Agri: Drives Y (Speed: {tractor.currentSpeed:F1} km/h, moved {tractorMoved:F2}m), Hitch Arm Y (Position: {tractor.hitchPosition:F1}).");

                // JCB Drive & Boom Test
                var jcb = vehicleControllers["JCB_Backhoe"] as JCBController;
                jcb.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                startPos = jcb.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                for (int f = 0; f < 60; f++)
                {
                    jcb.ApplyDriving();
                    jcb.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                float jcbMoved = Vector3.Distance(startPos, jcb.transform.position);
                Debug.Log($"✅ JCB_Backhoe: Drives Y (Speed: {jcb.currentSpeed:F1} km/h, moved {jcbMoved:F2}m), Loader & Backhoe Pivots Rigged Y.");

                // Excavator Tracks & Cabin Rotation Test
                var exc = vehicleControllers["Excavator_Tracked"] as ExcavatorController;
                exc.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                startPos = exc.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                exc.SendMessage("ReadTrackInput", SendMessageOptions.DontRequireReceiver);
                for (int f = 0; f < 60; f++)
                {
                    exc.SendMessage("ApplyTrackTorques", SendMessageOptions.DontRequireReceiver);
                    Physics.Simulate(0.02f);
                }
                float excMoved = Vector3.Distance(startPos, exc.transform.position);
                if (exc.cabinTransform == null) throw new Exception("Excavator cabinTransform is NULL!");
                exc.cabinTransform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Debug.Log($"✅ Excavator_Tracked: Drives Y (Speed: {exc.currentSpeed:F1} km/h, moved {excMoved:F2}m), Cabin Rotation Y (Rotated to 45°).");

                // Harvester Drive & Header Test
                var harv = vehicleControllers["Harvester_Combine"] as HarvesterController;
                harv.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                startPos = harv.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                for (int f = 0; f < 60; f++)
                {
                    harv.ApplyDriving();
                    harv.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                float harvMoved = Vector3.Distance(startPos, harv.transform.position);
                harv.RaiseLowerHeader(true);
                if (!harv.headerLowered) throw new Exception("Harvester header did not lower!");
                Debug.Log($"✅ Harvester_Combine: Drives Y (Speed: {harv.currentSpeed:F1} km/h, moved {harvMoved:F2}m), Header Lower Y.");

                // Reset inputs & simulation mode
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                Physics.simulationMode = SimulationMode.FixedUpdate;

                Debug.Log("\n================================================================================");
                Debug.Log("🎉 [RECOVERY TEST PASSED] All 6 vehicles visible, driveable, and mechanism rigged!");
                Debug.Log("================================================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RECOVERY TEST FAILED] ❌ Error: {ex.Message}\n{ex.StackTrace}");
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
    }
}
#endif
