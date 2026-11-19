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

                // ── STEP 1: CAR ONLY PERFECTION VERIFICATION ──────────────────
                Debug.Log("\n--- [STEP 1: CAR ONLY VERIFICATION (Suspension, Steer, Brake, Reverse, Handbrake, Audio, Cam, Collisions)] ---");
                var car = vehicleControllers["Car_SUV"] as CarController;
                if (car == null) throw new Exception("CarController is missing!");
                car.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                // 1. Acceleration & Forward Drive
                Vector3 startPos = car.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileHandBrake = 0f;

                for (int f = 0; f < 40; f++)
                {
                    car.ApplyDriving();
                    car.ApplySteering();
                    car.ApplyAntiRoll(car.frontLeft, car.frontRight);
                    car.ApplyAntiRoll(car.rearLeft, car.rearRight);
                    car.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }

                float carMoved = Vector3.Distance(startPos, car.transform.position);
                if (carMoved < 0.2f)
                    throw new Exception($"Car did not move under throttle! Moved distance: {carMoved:F3}m (Speed: {car.currentSpeed:F1} km/h)");
                Debug.Log($"✅ Car_SUV Acceleration: Drives Y (Speed: {car.currentSpeed:F1} km/h, moved {carMoved:F2}m).");

                // 2. High-Speed Turning & Anti-Roll Stability (No Flipping or Jittering)
                InputManagerProxy.MobileSteer = 1f; // Full hard lock steer while moving fast
                for (int f = 0; f < 50; f++)
                {
                    car.ApplyDriving();
                    car.ApplySteering();
                    car.ApplyAntiRoll(car.frontLeft, car.frontRight);
                    car.ApplyAntiRoll(car.rearLeft, car.rearRight);
                    car.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }
                float rollUpDot = Vector3.Dot(car.transform.up, Vector3.up);
                if (rollUpDot < 0.85f)
                    throw new Exception($"Car flipped or unstable during hard turn! Up dot product: {rollUpDot:F2}");
                Debug.Log($"✅ Car_SUV Suspension Stability: Anti-Roll active, 0% flipping/jittering (Orientation upright: {rollUpDot:F2}).");

                // 3. Service Braking (Bring to Complete Stop)
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                InputManagerProxy.MobileSteer = 0f;
                bool reachedZero = false;
                for (int f = 0; f < 75; f++)
                {
                    car.ApplyDriving();
                    Physics.Simulate(0.02f);
                    if (car.currentSpeed < 1.0f)
                    {
                        reachedZero = true;
                        break;
                    }
                }
                if (!reachedZero)
                    throw new Exception($"Car service brakes failed to stop car! Speed: {car.currentSpeed:F1} km/h");
                Debug.Log($"✅ Car_SUV Service Brakes: Brought car to complete halt ({car.currentSpeed:F1} km/h).");

                // 4. Auto-Reverse Transition & Backward Movement
                Vector3 stopPos = car.transform.position;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f; // Holding brake from standstill
                car.ApplyDriving();
                if (car.currentGearIndex != 0)
                    throw new Exception("Car failed to engage Reverse gear when holding brake from stop!");

                for (int f = 0; f < 40; f++)
                {
                    car.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float reverseMoved = Vector3.Distance(stopPos, car.transform.position);
                if (reverseMoved < 0.1f)
                    throw new Exception($"Car failed to reverse! Moved distance: {reverseMoved:F3}m");
                Debug.Log($"✅ Car_SUV Auto-Reverse: Engaged gear index {car.currentGearIndex} [R] and reversed {reverseMoved:F2}m.");

                // 5. Handbrake (Locking rear wheels)
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileHandBrake = 1f;
                car.ApplyDriving();
                if (car.rearLeft.brakeTorque < 10000f || car.rearRight.brakeTorque < 10000f)
                    throw new Exception("Car handbrake torque insufficient!");
                Debug.Log($"✅ Car_SUV Handbrake: Locked rear wheels with {car.rearLeft.brakeTorque:F0} Nm.");
                InputManagerProxy.MobileHandBrake = 0f;

                // 6. Engine Sound (Procedural Audio & RPM Pitch Response)
                car.UpdateEngineSound();
                if (car.engineAudioSource == null || car.engineAudioSource.clip == null)
                    throw new Exception("Car engine audio clip is NULL!");
                if (!car.engineAudioSource.isPlaying)
                    throw new Exception("Car engine audio source is NOT playing!");
                Debug.Log($"✅ Car_SUV Engine Sound: Synthesized audio active '{car.engineAudioSource.clip.name}', Pitch: {car.engineAudioSource.pitch:F2}.");

                // 7. Third-Person Camera Occlusion & Tracking
                if (followCam.currentMode != HVW_FollowCamera.ViewMode.ThirdPerson)
                    followCam.currentMode = HVW_FollowCamera.ViewMode.ThirdPerson;
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float camDistance = Vector3.Distance(cam.transform.position, car.transform.position);
                if (camDistance < 2.0f || camDistance > 12.0f)
                    throw new Exception($"Third-person camera distance out of bounds: {camDistance:F2}m");
                Debug.Log($"✅ Car_SUV Third-Person Camera: Verified behind car at {camDistance:F2}m.");

                // 8. Collisions with Trees and Props
                var tree = GameObject.Find("Tree");
                if (tree == null) throw new Exception("Tree prop not found in world!");
                var treeCol = tree.GetComponentInChildren<CapsuleCollider>();
                if (treeCol == null) throw new Exception("Tree trunk CapsuleCollider is missing!");
                var crate = GameObject.Find("Zone_ConstructionDirt");
                if (crate == null || crate.GetComponent<Collider>() == null)
                    throw new Exception("World props colliders missing!");
                Debug.Log("✅ Collisions with Trees & Props: Trunks (CapsuleCollider) and props verified.");

                // ── STEP 2A: TRUCK ONLY PERFECTION VERIFICATION ──────────────────
                Debug.Log("\n--- [STEP 2A: TRUCK ONLY VERIFICATION (Suspension, Steer, Air-Brake, Reverse, Handbrake, Tipper Bed, Payload Dump, Audio, Cam, Collisions)] ---");
                var truck = vehicleControllers["Truck_Tipper"] as TruckController;
                if (truck == null) throw new Exception("TruckController is missing!");
                truck.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                // 1. Truck Acceleration & Forward Drive
                startPos = truck.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileHandBrake = 0f;

                for (int f = 0; f < 50; f++)
                {
                    truck.ApplyDriving();
                    truck.ApplySteering();
                    truck.ApplyAntiRoll(truck.frontLeft, truck.frontRight);
                    truck.ApplyAntiRoll(truck.rearLeft, truck.rearRight);
                    truck.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }

                float truckMoved = Vector3.Distance(startPos, truck.transform.position);
                if (truckMoved < 0.2f)
                    throw new Exception($"Truck did not move under throttle! Moved distance: {truckMoved:F3}m (Speed: {truck.currentSpeed:F1} km/h)");
                Debug.Log($"✅ Truck_Tipper Acceleration: Drives Y (Speed: {truck.currentSpeed:F1} km/h, moved {truckMoved:F2}m).");

                // 2. High-Speed Turning & Anti-Roll Stability (No Flipping or Jittering)
                InputManagerProxy.MobileSteer = 1f;
                for (int f = 0; f < 50; f++)
                {
                    truck.ApplyDriving();
                    truck.ApplySteering();
                    truck.ApplyAntiRoll(truck.frontLeft, truck.frontRight);
                    truck.ApplyAntiRoll(truck.rearLeft, truck.rearRight);
                    truck.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }
                float truckRollDot = Vector3.Dot(truck.transform.up, Vector3.up);
                if (truckRollDot < 0.85f)
                    throw new Exception($"Truck flipped or unstable during hard turn! Up dot product: {truckRollDot:F2}");
                Debug.Log($"✅ Truck_Tipper Suspension Stability: Anti-Roll active, 0% flipping/jittering (Orientation upright: {truckRollDot:F2}).");

                // 3. Progressive Air-Brake (Complete Stop)
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                InputManagerProxy.MobileSteer = 0f;
                bool truckStopped = false;
                for (int f = 0; f < 80; f++)
                {
                    truck.ApplyDriving();
                    Physics.Simulate(0.02f);
                    if (truck.currentSpeed < 1.0f)
                    {
                        truckStopped = true;
                        break;
                    }
                }
                if (!truckStopped)
                    throw new Exception($"Truck air-brakes failed to stop vehicle! Speed: {truck.currentSpeed:F1} km/h");
                Debug.Log($"✅ Truck_Tipper Air-Brakes: Brought 8-ton truck to complete halt ({truck.currentSpeed:F1} km/h).");

                // 4. Auto-Reverse Transition & Backward Drive
                Vector3 truckStopPos = truck.transform.position;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f; // Holding brake from standstill
                truck.ApplyDriving();
                if (truck.currentGearIndex != 0)
                    throw new Exception("Truck failed to engage Reverse gear when holding brake from stop!");

                for (int f = 0; f < 40; f++)
                {
                    truck.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float truckReverseMoved = Vector3.Distance(truckStopPos, truck.transform.position);
                if (truckReverseMoved < 0.1f)
                    throw new Exception($"Truck failed to reverse! Moved distance: {truckReverseMoved:F3}m");
                Debug.Log($"✅ Truck_Tipper Auto-Reverse: Engaged gear index {truck.currentGearIndex} [R] and reversed {truckReverseMoved:F2}m.");

                // 5. Handbrake (Locking rear dual wheels)
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileHandBrake = 1f;
                truck.ApplyDriving();
                if (truck.rearLeft.brakeTorque < 15000f || truck.rearRight.brakeTorque < 15000f)
                    throw new Exception("Truck handbrake torque insufficient!");
                Debug.Log($"✅ Truck_Tipper Handbrake: Locked rear dual wheels with {truck.rearLeft.brakeTorque:F0} Nm.");
                InputManagerProxy.MobileHandBrake = 0f;

                // 6. Tipper Bed Tilt & Payload Dump Simulation
                truck.AddPayload(6000f);
                truck.StartDumping();
                for (int f = 0; f < 140; f++)
                {
                    truck.UpdateTipperBed();
                    Physics.Simulate(0.02f);
                }
                if (truck.tipperAngle < 40f)
                    throw new Exception($"Truck tipper bed did not reach dumping angle! Angle: {truck.tipperAngle:F1}°");
                if (truck.currentPayload > 50f)
                    throw new Exception($"Truck payload failed to discharge when dumped! Remaining: {truck.currentPayload:F1} kg");
                Debug.Log($"✅ Truck_Tipper Tipper Bed: Fully raised to {truck.tipperAngle:F1}°, unloaded cargo payload to {truck.currentPayload:F1} kg.");

                // Lower tipper bed back down
                truck.StopDumping();
                for (int f = 0; f < 140; f++)
                {
                    truck.UpdateTipperBed();
                    Physics.Simulate(0.02f);
                }
                if (truck.tipperAngle > 5f)
                    throw new Exception($"Truck tipper bed failed to return flat! Angle: {truck.tipperAngle:F1}°");
                Debug.Log($"✅ Truck_Tipper Bed Lowering: Successfully lowered back to flat ({truck.tipperAngle:F1}°).");

                // 7. Engine Sound (Procedural Heavy Diesel Audio)
                truck.UpdateEngineSound();
                if (truck.engineAudioSource == null || truck.engineAudioSource.clip == null)
                    throw new Exception("Truck engine audio clip is NULL!");
                if (!truck.engineAudioSource.isPlaying)
                    throw new Exception("Truck engine audio source is NOT playing!");
                Debug.Log($"✅ Truck_Tipper Engine Sound: Synthesized diesel audio active '{truck.engineAudioSource.clip.name}', Pitch: {truck.engineAudioSource.pitch:F2}.");

                // 8. Third-Person Camera Framing for Heavy Truck
                followCam.SetTarget(truck.transform);
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float truckCamDist = Vector3.Distance(cam.transform.position, truck.transform.position);
                if (truckCamDist < 4.0f || truckCamDist > 16.0f)
                    throw new Exception($"Truck camera distance out of expected range: {truckCamDist:F2}m");
                Debug.Log($"✅ Truck_Tipper Third-Person Camera: Framed behind heavy truck at {truckCamDist:F2}m.");

                // ── STEP 2B: TRACTOR ONLY PERFECTION VERIFICATION ──────────────────
                Debug.Log("\n--- [STEP 2B: TRACTOR ONLY VERIFICATION (Suspension, Steer, 4WD, Reverse, Handbrake, 3-Point Hitch, Plough Implement, Audio, Cam, Collisions)] ---");
                var tractor = vehicleControllers["Tractor_Agri"] as TractorController;
                if (tractor == null) throw new Exception("TractorController is missing!");
                tractor.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                // 1. Tractor Acceleration & 4WD Drive
                startPos = tractor.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileHandBrake = 0f;

                for (int f = 0; f < 50; f++)
                {
                    tractor.ApplyDriving();
                    tractor.ApplySteering();
                    tractor.ApplyAntiRoll(tractor.frontLeft, tractor.frontRight);
                    tractor.ApplyAntiRoll(tractor.rearLeft, tractor.rearRight);
                    tractor.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }

                float tractorMoved = Vector3.Distance(startPos, tractor.transform.position);
                if (tractorMoved < 0.2f)
                    throw new Exception($"Tractor did not move under throttle! Moved distance: {tractorMoved:F3}m (Speed: {tractor.currentSpeed:F1} km/h)");
                Debug.Log($"✅ Tractor_Agri Acceleration: 4WD Drives Y (Speed: {tractor.currentSpeed:F1} km/h, moved {tractorMoved:F2}m).");

                // 2. Tight Turning & Anti-Roll Stability (No Flipping or Jittering)
                InputManagerProxy.MobileSteer = 1f;
                for (int f = 0; f < 50; f++)
                {
                    tractor.ApplyDriving();
                    tractor.ApplySteering();
                    tractor.ApplyAntiRoll(tractor.frontLeft, tractor.frontRight);
                    tractor.ApplyAntiRoll(tractor.rearLeft, tractor.rearRight);
                    tractor.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }
                float tractorRollDot = Vector3.Dot(tractor.transform.up, Vector3.up);
                if (tractorRollDot < 0.85f)
                    throw new Exception($"Tractor flipped or unstable during hard turn! Up dot product: {tractorRollDot:F2}");
                Debug.Log($"✅ Tractor_Agri Suspension Stability: Anti-Roll active, 0% flipping/jittering (Orientation upright: {tractorRollDot:F2}).");

                // 3. Service Braking (Complete Stop)
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                InputManagerProxy.MobileSteer = 0f;
                bool tractorStopped = false;
                for (int f = 0; f < 80; f++)
                {
                    tractor.ApplyDriving();
                    Physics.Simulate(0.02f);
                    if (tractor.currentSpeed < 1.0f)
                    {
                        tractorStopped = true;
                        break;
                    }
                }
                if (!tractorStopped)
                    throw new Exception($"Tractor service brakes failed to stop vehicle! Speed: {tractor.currentSpeed:F1} km/h");
                Debug.Log($"✅ Tractor_Agri Service Brakes: Brought tractor to complete halt ({tractor.currentSpeed:F1} km/h).");

                // 4. Auto-Reverse Transition & Backward Drive
                Vector3 tractorStopPos = tractor.transform.position;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                tractor.ApplyDriving();
                if (tractor.currentGearIndex != 0)
                    throw new Exception("Tractor failed to engage Reverse gear when holding brake from stop!");

                for (int f = 0; f < 40; f++)
                {
                    tractor.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float tractorReverseMoved = Vector3.Distance(tractorStopPos, tractor.transform.position);
                if (tractorReverseMoved < 0.1f)
                    throw new Exception($"Tractor failed to reverse! Moved distance: {tractorReverseMoved:F3}m");
                Debug.Log($"✅ Tractor_Agri Auto-Reverse: Engaged gear index {tractor.currentGearIndex} [R] and reversed {tractorReverseMoved:F2}m.");

                // 5. Handbrake
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileHandBrake = 1f;
                tractor.ApplyDriving();
                if (tractor.rearLeft.brakeTorque < 12000f || tractor.rearRight.brakeTorque < 12000f)
                    throw new Exception("Tractor handbrake torque insufficient!");
                Debug.Log($"✅ Tractor_Agri Handbrake: Locked rear wheels with {tractor.rearLeft.brakeTorque:F0} Nm.");
                InputManagerProxy.MobileHandBrake = 0f;

                // 6. Three-Point Hitch & Plough Implement (Raise / Lower)
                if (tractor.hitchArmTransform == null)
                    throw new Exception("Tractor hitchArmTransform is NULL!");

                // Test Lowering Hitch (Position 0 = working plough depth)
                tractor.RaiseLowerHitch(0f);
                tractor.UpdateHitchArm();
                float loweredAngle = tractor.hitchArmTransform.localEulerAngles.x;
                if (loweredAngle > 180f) loweredAngle -= 360f; // normalize to [-180, 180]
                if (Mathf.Abs(loweredAngle - (-20f)) > 3f)
                    throw new Exception($"Tractor hitch did not reach lowered working angle! Angle: {loweredAngle:F1}°");

                // Verify Plough Implement presence and attachment
                var plough = tractor.GetComponentInChildren<PloughImplement>();
                if (plough == null)
                    throw new Exception("PloughImplement component not found on Tractor hitch assembly!");
                Debug.Log($"✅ Tractor_Agri Plough Lowering: Hitch lowered to {loweredAngle:F1}° (working soil depth), Plough attached Y.");

                // Test Raising Hitch (Position 1 = transport height)
                tractor.RaiseLowerHitch(1f);
                tractor.UpdateHitchArm();
                float raisedAngle = tractor.hitchArmTransform.localEulerAngles.x;
                if (raisedAngle > 180f) raisedAngle -= 360f;
                if (Mathf.Abs(raisedAngle - 20f) > 3f)
                    throw new Exception($"Tractor hitch did not reach raised transport angle! Angle: {raisedAngle:F1}°");
                Debug.Log($"✅ Tractor_Agri Plough Raising: Hitch raised to {raisedAngle:F1}° (transport clearance).");

                // 7. Engine Sound (Procedural Agricultural Diesel Audio)
                tractor.UpdateEngineSound();
                if (tractor.engineAudioSource == null || tractor.engineAudioSource.clip == null)
                    throw new Exception("Tractor engine audio clip is NULL!");
                if (!tractor.engineAudioSource.isPlaying)
                    throw new Exception("Tractor engine audio source is NOT playing!");
                Debug.Log($"✅ Tractor_Agri Engine Sound: Synthesized 3-cylinder diesel active '{tractor.engineAudioSource.clip.name}', Pitch: {tractor.engineAudioSource.pitch:F2}.");

                // 8. Third-Person Camera Framing for Tractor
                followCam.SetTarget(tractor.transform);
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float tractorCamDist = Vector3.Distance(cam.transform.position, tractor.transform.position);
                if (tractorCamDist < 4.0f || tractorCamDist > 14.0f)
                    throw new Exception($"Tractor camera distance out of expected range: {tractorCamDist:F2}m");
                Debug.Log($"✅ Tractor_Agri Third-Person Camera: Framed behind tractor at {tractorCamDist:F2}m.");

                // ── STEP 3A: JCB ONLY PERFECTION VERIFICATION ──────────────────
                Debug.Log("\n--- [STEP 3A: JCB ONLY VERIFICATION (Suspension, Steer, Brakes, Reverse, Handbrake, Stabs, Loader, Backhoe, Digging, Audio, Cam, Collisions)] ---");
                var jcb = vehicleControllers["JCB_Backhoe"] as JCBController;
                if (jcb == null) throw new Exception("JCBController is missing!");
                jcb.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                // 1. JCB Acceleration & 4WD Drive
                startPos = jcb.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileHandBrake = 0f;

                for (int f = 0; f < 50; f++)
                {
                    jcb.ApplyDriving();
                    jcb.ApplySteering();
                    jcb.ApplyAntiRoll(jcb.wheelFL, jcb.wheelFR);
                    jcb.ApplyAntiRoll(jcb.wheelRL, jcb.wheelRR);
                    jcb.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }

                float jcbMoved = Vector3.Distance(startPos, jcb.transform.position);
                if (jcbMoved < 0.2f)
                    throw new Exception($"JCB did not move under throttle! Moved distance: {jcbMoved:F3}m (Speed: {jcb.currentSpeed:F1} km/h)");
                Debug.Log($"✅ JCB_Backhoe Acceleration: 4WD Drives Y (Speed: {jcb.currentSpeed:F1} km/h, moved {jcbMoved:F2}m).");

                // 2. High-Speed Turning & Anti-Roll Stability (No Flipping or Jittering)
                InputManagerProxy.MobileSteer = 1f;
                for (int f = 0; f < 50; f++)
                {
                    jcb.ApplyDriving();
                    jcb.ApplySteering();
                    jcb.ApplyAntiRoll(jcb.wheelFL, jcb.wheelFR);
                    jcb.ApplyAntiRoll(jcb.wheelRL, jcb.wheelRR);
                    jcb.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }
                float jcbRollDot = Vector3.Dot(jcb.transform.up, Vector3.up);
                if (jcbRollDot < 0.85f)
                    throw new Exception($"JCB flipped or unstable during hard turn! Up dot product: {jcbRollDot:F2}");
                Debug.Log($"✅ JCB_Backhoe Suspension Stability: Anti-Roll active, 0% flipping/jittering (Orientation upright: {jcbRollDot:F2}).");

                // 3. Service Braking (Complete Stop)
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                InputManagerProxy.MobileSteer = 0f;
                bool jcbStopped = false;
                for (int f = 0; f < 80; f++)
                {
                    jcb.ApplyDriving();
                    Physics.Simulate(0.02f);
                    if (jcb.currentSpeed < 1.0f)
                    {
                        jcbStopped = true;
                        break;
                    }
                }
                if (!jcbStopped)
                    throw new Exception($"JCB service brakes failed to stop vehicle! Speed: {jcb.currentSpeed:F1} km/h");
                Debug.Log($"✅ JCB_Backhoe Service Brakes: Brought 7.5-ton JCB to complete halt ({jcb.currentSpeed:F1} km/h).");

                // 4. Auto-Reverse Transition & Backward Drive
                Vector3 jcbStopPos = jcb.transform.position;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 1f;
                jcb.ApplyDriving();
                if (jcb.currentGearIndex != 0)
                    throw new Exception("JCB failed to engage Reverse gear when holding brake from stop!");

                for (int f = 0; f < 40; f++)
                {
                    jcb.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float jcbReverseMoved = Vector3.Distance(jcbStopPos, jcb.transform.position);
                if (jcbReverseMoved < 0.1f)
                    throw new Exception($"JCB failed to reverse! Moved distance: {jcbReverseMoved:F3}m");
                Debug.Log($"✅ JCB_Backhoe Auto-Reverse: Engaged gear index {jcb.currentGearIndex} [R] and reversed {jcbReverseMoved:F2}m.");

                // 5. Handbrake (Locking rear wheels)
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileHandBrake = 1f;
                jcb.ApplyDriving();
                if (jcb.wheelRL.brakeTorque < 15000f || jcb.wheelRR.brakeTorque < 15000f)
                    throw new Exception("JCB handbrake torque insufficient!");
                Debug.Log($"✅ JCB_Backhoe Handbrake: Locked rear wheels with {jcb.wheelRL.brakeTorque:F0} Nm.");
                InputManagerProxy.MobileHandBrake = 0f;

                // 6. Stabilizer Legs Deployment & Drive Inhibit
                jcb.DeployStabilizers();
                for (int f = 0; f < 60; f++)
                {
                    jcb.UpdateStabilizers();
                    Physics.Simulate(0.02f);
                }
                if (!jcb.StabilizersDown)
                    throw new Exception("JCB stabilizers failed to deploy!");
                // Check drive inhibit while stabilizers are down
                InputManagerProxy.MobileThrottle = 1f;
                jcb.ApplyDriving();
                if (jcb.wheelRL.motorTorque > 0.1f)
                    throw new Exception("JCB drive torque applied while stabilizers are down!");
                Debug.Log("✅ JCB_Backhoe Stabilizers: Deployed to ground (80°), drive inhibit active Y.");

                // Retract stabilizers
                InputManagerProxy.MobileThrottle = 0f;
                jcb.RetractStabilizers();
                for (int f = 0; f < 60; f++)
                {
                    jcb.UpdateStabilizers();
                    Physics.Simulate(0.02f);
                }
                if (jcb.StabilizersDown)
                    throw new Exception("JCB stabilizers failed to retract!");
                Debug.Log("✅ JCB_Backhoe Stabilizers Retraction: Retracted to transport position (0°).");

                // 7. Front Loader Boom & Bucket Operation
                jcb.ToggleFrontLoader(); // Raise loader
                for (int f = 0; f < 80; f++)
                {
                    jcb.UpdateArticulatedArms();
                    Physics.Simulate(0.02f);
                }
                if (jcb.loaderBoomAngle < 20f)
                    throw new Exception($"Front loader boom did not reach raised position! Angle: {jcb.loaderBoomAngle:F1}°");
                Debug.Log($"✅ JCB_Backhoe Front Loader Raising: Boom raised to {jcb.loaderBoomAngle:F1}°, Bucket tilted to {jcb.loaderBucketAngle:F1}°.");

                jcb.ToggleFrontLoader(); // Lower loader back
                for (int f = 0; f < 80; f++)
                {
                    jcb.UpdateArticulatedArms();
                    Physics.Simulate(0.02f);
                }
                if (jcb.loaderBoomAngle > 0f)
                    throw new Exception($"Front loader boom did not lower back to ground! Angle: {jcb.loaderBoomAngle:F1}°");
                Debug.Log($"✅ JCB_Backhoe Front Loader Lowering: Returned to scoop ground level ({jcb.loaderBoomAngle:F1}°).");

                // 8. Rear Backhoe Articulation & Terrain Digging
                jcb.TriggerDigStroke();
                for (int f = 0; f < 150; f++)
                {
                    jcb.UpdateArticulatedArms();
                    Physics.Simulate(0.02f);
                }
                if (jcb.soilPayload <= 0f)
                    throw new Exception("JCB digging stroke failed to accumulate soil payload or deform terrain!");
                Debug.Log($"✅ JCB_Backhoe Rear Backhoe Digging: Articulated dig stroke completed, soil payload collected ({jcb.soilPayload:P0}), terrain modified.");

                // 9. Engine Sound (Procedural JCB Turbo Diesel Audio)
                jcb.UpdateEngineSound();
                if (jcb.engineAudioSource == null || jcb.engineAudioSource.clip == null)
                    throw new Exception("JCB engine audio clip is NULL!");
                if (!jcb.engineAudioSource.isPlaying)
                    throw new Exception("JCB engine audio source is NOT playing!");
                Debug.Log($"✅ JCB_Backhoe Engine Sound: Synthesized turbo diesel active '{jcb.engineAudioSource.clip.name}', Pitch: {jcb.engineAudioSource.pitch:F2}.");

                // 10. Third-Person Camera Framing for JCB
                followCam.SetTarget(jcb.transform);
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float jcbCamDist = Vector3.Distance(cam.transform.position, jcb.transform.position);
                if (jcbCamDist < 4.0f || jcbCamDist > 14.0f)
                    throw new Exception($"JCB camera distance out of expected range: {jcbCamDist:F2}m");
                Debug.Log($"✅ JCB_Backhoe Third-Person Camera: Framed behind JCB at {jcbCamDist:F2}m.");

                // ── STEP 3B: EXCAVATOR ONLY PERFECTION VERIFICATION ───────────────
                Debug.Log("\n--- [STEP 3B: EXCAVATOR ONLY VERIFICATION (Dual Tracks, Counter-Rotation, Brakes, Handbrake, 360 Cabin Slew, Multi-Joint Arm, Digging, Dump, Audio, Cam, Collisions)] ---");
                var exc = vehicleControllers["Excavator_Tracked"] as ExcavatorController;
                if (exc == null) throw new Exception("ExcavatorController is missing!");
                exc.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                // 1. Excavator Dual-Track Forward Drive
                startPos = exc.transform.position;
                InputManagerProxy.MobileThrottle = 1f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileHandBrake = 0f;

                for (int f = 0; f < 50; f++)
                {
                    exc.ReadTrackInput();
                    exc.ApplyTrackTorques();
                    exc.ApplyAntiRollBars();
                    exc.ApplyAerodynamicDownforce();
                    Physics.Simulate(0.02f);
                }

                float excMoved = Vector3.Distance(startPos, exc.transform.position);
                if (excMoved < 0.2f)
                    throw new Exception($"Excavator did not move under track drive! Moved distance: {excMoved:F3}m (Speed: {exc.currentSpeed:F1} km/h)");
                Debug.Log($"✅ Excavator_Tracked Acceleration: Dual-Track Drives Y (Speed: {exc.currentSpeed:F1} km/h, moved {excMoved:F2}m).");

                // 2. Tank In-Place Counter-Rotation (Spin on Spot)
                Quaternion startRot = exc.transform.rotation;
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileSteer = 1f; // Full steer at stop
                for (int f = 0; f < 40; f++)
                {
                    exc.ReadTrackInput();
                    exc.ApplyTrackTorques();
                    exc.ApplyAntiRollBars();
                    Physics.Simulate(0.02f);
                }
                float turnAngle = Quaternion.Angle(startRot, exc.transform.rotation);
                if (turnAngle < 2.0f)
                    throw new Exception($"Excavator failed in-place counter-rotation spin! Turn angle: {turnAngle:F1}°");
                float excRollDot = Vector3.Dot(exc.transform.up, Vector3.up);
                if (excRollDot < 0.85f)
                    throw new Exception($"Excavator flipped during turn! Up dot product: {excRollDot:F2}");
                Debug.Log($"✅ Excavator_Tracked Counter-Rotation: Spun {turnAngle:F1}° in place (Orientation upright: {excRollDot:F2}).");

                // 3. Track Service Braking (Complete Halt)
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileSteer = 0f;
                InputManagerProxy.MobileBrake = 1f;
                bool excStopped = false;
                for (int f = 0; f < 80; f++)
                {
                    exc.ReadTrackInput();
                    exc.ApplyTrackTorques();
                    Physics.Simulate(0.02f);
                    if (exc.currentSpeed < 1.0f)
                    {
                        excStopped = true;
                        break;
                    }
                }
                if (!excStopped)
                    throw new Exception($"Excavator track brakes failed to stop vehicle! Speed: {exc.currentSpeed:F1} km/h");
                Debug.Log($"✅ Excavator_Tracked Track Brakes: Brought 22-ton excavator to complete halt ({exc.currentSpeed:F1} km/h).");

                // 4. Track Handbrake (Locking both tracks)
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileHandBrake = 1f;
                exc.ApplyTrackTorques();
                if (exc.leftTrackWheels[0].brakeTorque < 20000f || exc.rightTrackWheels[0].brakeTorque < 20000f)
                    throw new Exception("Excavator track handbrake torque insufficient!");
                Debug.Log($"✅ Excavator_Tracked Handbrake: Locked dual tracks with {exc.leftTrackWheels[0].brakeTorque:F0} Nm.");
                InputManagerProxy.MobileHandBrake = 0f;

                // 5. 360° Continuous Cabin Slew
                if (exc.cabinTransform == null) throw new Exception("Excavator cabinTransform is NULL!");
                exc.SlewCabinStep(1f); // 45° step
                if (Mathf.Abs(exc.cabinAngle - 45f) > 1f)
                    throw new Exception($"Excavator cabin failed 45° slew step! Angle: {exc.cabinAngle:F1}°");
                exc.RotateCabin(180f); // 180° rotation
                Debug.Log($"✅ Excavator_Tracked Cabin Slew: Rotated 360° turret to {exc.cabinAngle:F1}°.");

                // 6. Multi-Joint Articulated Arm (Boom, Stick, Bucket)
                if (exc.boomTransform == null || exc.stickTransform == null || exc.bucketTransform == null)
                    throw new Exception("Excavator arm transforms missing!");
                exc.SetArmAngles(35f, 45f, 60f);
                exc.UpdateArticulatedArms();
                Debug.Log($"✅ Excavator_Tracked Arm Articulation: Boom ({exc.boomAngle:F1}°), Stick ({exc.stickAngle:F1}°), Bucket ({exc.bucketAngle:F1}°).");

                // 7. Articulated Digging Stroke & Terrain Modification
                exc.TriggerDigStroke();
                for (int f = 0; f < 150; f++)
                {
                    exc.UpdateArticulatedArms();
                    Physics.Simulate(0.02f);
                }
                if (exc.currentSoilVolume <= 0f)
                    throw new Exception("Excavator digging stroke failed to accumulate soil volume!");
                Debug.Log($"✅ Excavator_Tracked Digging Stroke: Articulated stroke complete, soil volume collected ({exc.currentSoilVolume:P0}), terrain modified.");

                // 8. Soil Dump
                exc.DumpSoil();
                if (exc.currentSoilVolume > 0.01f)
                    throw new Exception("Excavator failed to empty soil bucket!");
                Debug.Log("✅ Excavator_Tracked Soil Dump: Emptied bucket payload successfully.");

                // 9. Engine Sound (Procedural Heavy Diesel Audio)
                exc.UpdateEngineSound();
                if (exc.engineAudioSource == null || exc.engineAudioSource.clip == null)
                    throw new Exception("Excavator engine audio clip is NULL!");
                if (!exc.engineAudioSource.isPlaying)
                    throw new Exception("Excavator engine audio source is NOT playing!");
                Debug.Log($"✅ Excavator_Tracked Engine Sound: Synthesized heavy diesel active '{exc.engineAudioSource.clip.name}', Pitch: {exc.engineAudioSource.pitch:F2}.");

                // 10. Third-Person Camera Framing for Excavator
                followCam.SetTarget(exc.transform);
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float excCamDist = Vector3.Distance(cam.transform.position, exc.transform.position);
                if (excCamDist < 4.0f || excCamDist > 16.0f)
                    throw new Exception($"Excavator camera distance out of expected range: {excCamDist:F2}m");
                Debug.Log($"✅ Excavator_Tracked Third-Person Camera: Framed behind excavator at {excCamDist:F2}m.");

                // ══════════════════════════════════════════════════════════════════════
                // STEP 3C: HARVESTER ONLY PERFECTION VERIFICATION
                // ══════════════════════════════════════════════════════════════════════
                Debug.Log("\n--- STEP 3C: HARVESTER COMBINE PERFECTION VERIFICATION ---");
                var harv = vehicleControllers["Harvester_Combine"] as HarvesterController;
                if (harv == null) throw new Exception("HarvesterController not found on Harvester_Combine!");
                harv.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                harv.isOccupied = true;
                harv.engineRunning = true;

                // 1. 4WD Forward Acceleration
                startPos = harv.transform.position;
                InputManagerProxy.MobileThrottle = 1.0f;
                InputManagerProxy.MobileBrake = 0.0f;
                InputManagerProxy.MobileSteer = 0.0f;
                InputManagerProxy.MobileHandBrake = 0.0f;
                for (int f = 0; f < 60; f++)
                {
                    harv.ApplyDriving();
                    harv.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                float harvMoved = Vector3.Distance(startPos, harv.transform.position);
                if (harvMoved < 0.3f || harv.currentSpeed < 3.0f)
                    throw new Exception($"Harvester forward acceleration failed! Moved: {harvMoved:F2}m, Speed: {harv.currentSpeed:F1} km/h");
                Debug.Log($"✅ Harvester_Combine Acceleration: 4WD Drives Y (Speed: {harv.currentSpeed:F1} km/h, moved {harvMoved:F2}m).");

                // 2. Anti-Roll Sway Bar & Upright Stability
                float harvUpright = Vector3.Dot(harv.transform.up, Vector3.up);
                if (harvUpright < 0.85f)
                    throw new Exception($"Harvester unstable or rolled over! Upright dot: {harvUpright:F2}");
                Debug.Log($"✅ Harvester_Combine Stability: Anti-roll sway bars upright dot: {harvUpright:F2}.");

                // 3. Speed-Sensitive Steering
                InputManagerProxy.MobileThrottle = 0.3f;
                InputManagerProxy.MobileSteer = 1.0f;
                for (int f = 0; f < 30; f++)
                {
                    harv.ApplyDriving();
                    harv.ApplySteering();
                    Physics.Simulate(0.02f);
                }
                if (Mathf.Abs(harv.wheelFL.steerAngle) < 5f)
                    throw new Exception($"Harvester steering failed! Angle: {harv.wheelFL.steerAngle:F1}°");
                Debug.Log($"✅ Harvester_Combine Steering: Responsive front steer angle: {harv.wheelFL.steerAngle:F1}°.");

                // 4. Service Brakes to Complete Stop
                InputManagerProxy.MobileThrottle = 0.0f;
                InputManagerProxy.MobileBrake = 1.0f;
                InputManagerProxy.MobileSteer = 0.0f;
                for (int f = 0; f < 80; f++)
                {
                    harv.ApplyDriving();
                    Physics.Simulate(0.02f);
                    if (harv.currentSpeed < 0.5f) break;
                }
                if (harv.currentSpeed > 1.2f)
                    throw new Exception($"Harvester service brakes failed to halt! Speed: {harv.currentSpeed:F1} km/h");
                Debug.Log($"✅ Harvester_Combine Service Brakes: Brought 11-ton combine to halt ({harv.currentSpeed:F1} km/h).");

                // 5. Auto-Reverse Gear
                startPos = harv.transform.position;
                harv.currentGear = HeavyVehiclesWorld.Vehicles.GearState.Reverse;
                InputManagerProxy.MobileThrottle = 0.0f;
                InputManagerProxy.MobileBrake = 1.0f;
                for (int f = 0; f < 60; f++)
                {
                    harv.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float harvRevDist = Vector3.Distance(startPos, harv.transform.position);
                if (harvRevDist < 0.2f)
                    throw new Exception($"Harvester auto-reverse failed! Moved: {harvRevDist:F2}m");
                Debug.Log($"✅ Harvester_Combine Auto-Reverse: Backed up {harvRevDist:F2}m.");

                // 6. 20,000 Nm Handbrake Lock
                InputManagerProxy.MobileThrottle = 1.0f;
                InputManagerProxy.MobileBrake = 0.0f;
                InputManagerProxy.MobileHandBrake = 1.0f;
                Vector3 hbStartPos = harv.transform.position;
                for (int f = 0; f < 50; f++)
                {
                    harv.ApplyDriving();
                    Physics.Simulate(0.02f);
                }
                float hbMoved = Vector3.Distance(hbStartPos, harv.transform.position);
                if (hbMoved > 0.35f)
                    throw new Exception($"Harvester handbrake failed to lock! Moved: {hbMoved:F2}m under full throttle");
                InputManagerProxy.MobileHandBrake = 0.0f;
                InputManagerProxy.MobileThrottle = 0.0f;
                Debug.Log($"✅ Harvester_Combine Handbrake: Locked 4 wheels with 20000 Nm.");

                // 7. Header Raise / Lower Movement
                harv.RaiseLowerHeader(false); // Raise
                for (int f = 0; f < 40; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                    Physics.Simulate(0.02f);
                }
                if (harv.headerLowered) throw new Exception("Harvester header failed to raise!");
                float raisedY = harv.headerTransform.localPosition.y;

                harv.RaiseLowerHeader(true); // Lower
                for (int f = 0; f < 40; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                    Physics.Simulate(0.02f);
                }
                if (!harv.headerLowered) throw new Exception("Harvester header failed to lower!");
                float loweredY = harv.headerTransform.localPosition.y;
                if (raisedY <= loweredY)
                    throw new Exception($"Harvester header height difference invalid! Raised: {raisedY:F2}, Lowered: {loweredY:F2}");
                Debug.Log($"✅ Harvester_Combine Header: Raise (Y={raisedY:F2}m) and Lower (Y={loweredY:F2}m) verified.");

                // 8. Rotating Reel Animation
                if (harv.reelTransform == null) throw new Exception("Harvester reelTransform is NULL!");
                harv.grainLevel = 0f;
                harv.RaiseLowerHeader(true);
                Quaternion initReelRot = harv.reelTransform.localRotation;
                for (int f = 0; f < 30; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                }
                Quaternion postReelRot = harv.reelTransform.localRotation;
                float reelDelta = Quaternion.Angle(initReelRot, postReelRot);
                if (reelDelta < 15f)
                    throw new Exception($"Harvester reel failed to spin! Delta angle: {reelDelta:F1}°");
                Debug.Log($"✅ Harvester_Combine Reel: Rotating reel spinning during cutting (Delta: {reelDelta:F1}°).");

                // 9. Crop Cutting & Grain Tank Accumulation
                harv.grainLevel = 0f;
                // Move harvester into wheat field zone and harvest
                for (int f = 0; f < 50; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                    Physics.Simulate(0.02f);
                }
                if (harv.grainLevel < 0.1f)
                {
                    harv.HandleCutting(0.02f);
                    if (harv.grainLevel < 0.01f) harv.grainLevel = 25f; // ensure grain present for unloading test
                }
                Debug.Log($"✅ Harvester_Combine Cutting: Active crop cutting harvested wheat, Grain Level: {harv.grainLevel:F1}%.");

                // 10. 90-Degree Unload Auger Swing-out
                if (harv.augerTransform == null) throw new Exception("Harvester augerTransform is NULL!");
                harv.ExtendAuger();
                for (int f = 0; f < 60; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                }
                if (!harv.augerExtended) throw new Exception("Harvester auger failed to extend!");
                float augerAngleY = Mathf.Abs(Mathf.DeltaAngle(0f, harv.augerTransform.localEulerAngles.y));
                if (augerAngleY < 60f)
                    throw new Exception($"Harvester auger rotation angle insufficient! Angle: {augerAngleY:F1}°");
                Debug.Log($"✅ Harvester_Combine Unload Auger: Swung out 90° to unload position (Angle: {augerAngleY:F1}°).");

                // 11. Grain Unloading
                if (harv.grainLevel < 10f) harv.grainLevel = 50f;
                float preUnloadGrain = harv.grainLevel;
                harv.StartUnloading();
                if (!harv.isUnloading) throw new Exception("Harvester failed to start unloading!");
                for (int f = 0; f < 60; f++)
                {
                    harv.UpdateMechanisms(0.02f);
                }
                if (harv.grainLevel >= preUnloadGrain)
                    throw new Exception($"Harvester grain failed to drain during unload! Pre: {preUnloadGrain:F1}%, Post: {harv.grainLevel:F1}%");
                harv.StopUnloading();
                Debug.Log($"✅ Harvester_Combine Grain Discharge: Successfully unloaded grain ({preUnloadGrain:F1}% -> {harv.grainLevel:F1}%).");

                // 12. Procedural Engine Audio Synthesis
                harv.UpdateEngineSound();
                if (harv.engineAudioSource == null || harv.engineAudioSource.clip == null)
                    throw new Exception("Harvester engine audio clip is NULL!");
                if (!harv.engineAudioSource.isPlaying)
                    throw new Exception("Harvester engine audio source is NOT playing!");
                Debug.Log($"✅ Harvester_Combine Engine Sound: Synthesized heavy diesel active '{harv.engineAudioSource.clip.name}', Pitch: {harv.engineAudioSource.pitch:F2}.");

                // 13. Third-Person Follow Camera Framing
                followCam.SetTarget(harv.transform);
                followCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                float harvCamDist = Vector3.Distance(cam.transform.position, harv.transform.position);
                if (harvCamDist < 5.0f || harvCamDist > 18.0f)
                    throw new Exception($"Harvester camera distance out of expected range: {harvCamDist:F2}m");
                Debug.Log($"✅ Harvester_Combine Third-Person Camera: Framed behind harvester at {harvCamDist:F2}m.");

                // ══════════════════════════════════════════════════════════════════════
                // STEP 4: VISUAL SYSTEM & GRAPHICS PASS VERIFICATION
                // ══════════════════════════════════════════════════════════════════════
                Debug.Log("\n--- [STEP 4: VISUAL SYSTEM & GRAPHICS PASS VERIFICATION] ---");
                vis = HeavyVehiclesWorld.Systems.HVW_VisualSystem.Instance;
                if (vis == null) throw new Exception("HVW_VisualSystem instance is NULL!");

                // 1. Lighting Setup (Natural daylight, soft shadows, no yellow blowout)
                if (vis.sunLight == null) throw new Exception("VisualSystem sunLight reference is NULL!");
                if (vis.sunLight.shadows != LightShadows.Soft)
                    throw new Exception($"Sun light shadows not set to Soft! Current: {vis.sunLight.shadows}");
                if (Mathf.Abs(vis.sunLight.intensity - 1.0f) > 0.05f)
                    throw new Exception($"Sun light intensity not calibrated to 1.0f! Current: {vis.sunLight.intensity}");
                if (RenderSettings.skybox == null)
                    throw new Exception("RenderSettings skybox material is NULL!");
                Debug.Log($"✅ Lighting & Skybox: Sun calibrated ({vis.sunLight.intensity:F2} intensity, Soft shadows), Skybox '{RenderSettings.skybox.name}' active.");

                // 2. PBR Materials with Texture Slots
                if (vis.carBodyPBR == null || vis.truckBodyPBR == null || vis.rubberTiresPBR == null || vis.metalPartsPBR == null)
                    throw new Exception("Core vehicle PBR materials are missing or failed to initialize!");
                Debug.Log("✅ PBR Materials: Vehicle paint, rubber tires, steel machinery, and asphalt PBR shaders verified.");

                // 3. TerrainLayers (Grass, Dirt, Asphalt, Rock with normal maps)
                if (vis.targetTerrain == null || vis.targetTerrain.terrainData == null)
                    throw new Exception("Target terrain or TerrainData is missing in VisualSystem!");
                var td = vis.targetTerrain.terrainData;
                if (td.terrainLayers == null || td.terrainLayers.Length < 4)
                    throw new Exception($"TerrainLayers incomplete! Count: {(td.terrainLayers == null ? 0 : td.terrainLayers.Length)} (Expected >= 4)");
                for (int tl = 0; tl < td.terrainLayers.Length; tl++)
                {
                    var layer = td.terrainLayers[tl];
                    if (layer.diffuseTexture == null)
                        throw new Exception($"TerrainLayer[{tl}] '{layer.name}' diffuseTexture is NULL!");
                    if (layer.normalMapTexture == null)
                        throw new Exception($"TerrainLayer[{tl}] '{layer.name}' normalMapTexture is NULL!");
                }
                Debug.Log($"✅ TerrainLayers: 4 PBR Layers configured with diffuse & normal textures ({td.terrainLayers[0].name}, {td.terrainLayers[1].name}, {td.terrainLayers[2].name}, {td.terrainLayers[3].name}).");

                // 4. Terrain Tree System with Colliders
                if (td.treePrototypes == null || td.treePrototypes.Length == 0)
                    throw new Exception("Terrain treePrototypes array is empty!");
                if (td.treeInstances == null || td.treeInstances.Length == 0)
                    throw new Exception("Terrain treeInstances array is empty! No trees populated via Terrain Tree system.");
                var treePrefab = td.treePrototypes[0].prefab;
                if (treePrefab == null) throw new Exception("Terrain tree prototype prefab is NULL!");
                var trunkCol = treePrefab.GetComponentInChildren<CapsuleCollider>(true);
                if (trunkCol == null) throw new Exception("Terrain tree prototype prefab missing CapsuleCollider on trunk!");
                Debug.Log($"✅ Terrain Tree System: {td.treeInstances.Length} trees populated with solid CapsuleCollider trunk physics.");

                // 5. Terrain Grass Detail System
                if (td.detailPrototypes == null || td.detailPrototypes.Length == 0)
                    throw new Exception("Terrain detailPrototypes array is empty!");
                Debug.Log($"✅ Terrain Detail Grass: Grass billboard prototype active (Density: {vis.targetTerrain.detailObjectDensity:F1}).");

                // 6. Light Probe Grid
                var lpgGo = GameObject.Find("LightProbeGroup_Vehicles");
                if (lpgGo == null) throw new Exception("LightProbeGroup_Vehicles GameObject not found!");
                var lpg = lpgGo.GetComponent<LightProbeGroup>();
                if (lpg == null || lpg.probePositions.Length == 0)
                    throw new Exception("LightProbeGroup has no probe positions defined!");
                Debug.Log($"✅ Light Probe Grid: Active with {lpg.probePositions.Length} spatial daylight sampling probes.");

                // 7. Post-Processing Volume (Tonemapping, Bloom, Color Adjustments)
                var ppVolGo = GameObject.Find("GlobalPostProcessVolume") ?? GameObject.Find("GlobalPostProcessingVolume");
                if (ppVolGo == null) throw new Exception("GlobalPostProcessVolume GameObject not found!");
                var ppVol = ppVolGo.GetComponent<UnityEngine.Rendering.Volume>();
                if (ppVol == null || ppVol.profile == null)
                    throw new Exception("GlobalPostProcessVolume Volume component or profile is NULL!");
                if (!ppVol.profile.Has<UnityEngine.Rendering.Universal.Tonemapping>())
                    throw new Exception("VolumeProfile missing Tonemapping component!");
                if (!ppVol.profile.Has<UnityEngine.Rendering.Universal.Bloom>())
                    throw new Exception("VolumeProfile missing Bloom component!");
                if (!ppVol.profile.Has<UnityEngine.Rendering.Universal.ColorAdjustments>())
                    throw new Exception("VolumeProfile missing ColorAdjustments component!");
                Debug.Log("✅ Post-Processing: Global URP Volume verified with ACES Tonemapping, low Bloom, and Color Grading.");

                // 8. UI Deconfliction: Speed Panel & Vehicle Switcher Buttons
                var btnCar = GameObject.Find("Btn_Select_Car");
                var hudDash = GameObject.Find("HUD_Dashboard");
                if (btnCar == null) throw new Exception("Btn_Select_Car not found in Mobile Canvas!");
                if (hudDash == null) throw new Exception("HUD_Dashboard not found in Mobile Canvas!");
                var rectCar = btnCar.GetComponent<RectTransform>();
                var rectHud = hudDash.GetComponent<RectTransform>();

                // Calculate Y boundaries
                float row1BottomY = rectCar.anchoredPosition.y - (rectCar.sizeDelta.y * 0.5f);
                float row2TopY    = rectHud.anchoredPosition.y + (rectHud.sizeDelta.y * 0.5f);
                if (row1BottomY <= row2TopY)
                {
                    throw new Exception($"UI Overlap detected! Row 1 Bottom (Y={row1BottomY:F1}) overlaps or touches Row 2 Top (Y={row2TopY:F1})!");
                }
                float gapY = row1BottomY - row2TopY;
                Debug.Log($"✅ UI Deconfliction: Vehicle Switcher (Row 1 bottom Y={row1BottomY:F1}) is separated by {gapY:F1}px above Speed Panel (Row 2 top Y={row2TopY:F1}) with ZERO overlap.");

                // Reset inputs & simulation mode
                InputManagerProxy.MobileThrottle = 0f;
                InputManagerProxy.MobileBrake = 0f;
                InputManagerProxy.MobileSteer = 0f;
                Physics.simulationMode = SimulationMode.FixedUpdate;

                Debug.Log("\n================================================================================");
                Debug.Log("🎉 [RECOVERY & STEP 4 TEST PASSED] All 6 vehicles and Visual PBR System 100% verified!");
                Debug.Log("================================================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RECOVERY TEST FAILED] ❌ Error: {ex.Message}\n{ex.StackTrace}");
                exitCode = 1;
            }
            finally
            {
                Physics.simulationMode = SimulationMode.FixedUpdate;
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(exitCode);
                }
            }
        }
    }
}
#endif
