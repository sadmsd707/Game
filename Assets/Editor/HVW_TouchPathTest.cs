#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.EditorTests
{
    public static class HVW_TouchPathTest
    {
        public struct VehicleTestResult
        {
            public string Name;
            public VehicleType Type;
            public float FlatSpeedKmh;
            public float FlatDist;
            public float FlatBrakeFinalSpeed;
            public float SlopeClimbDist;
            public float SlopeSpeedKmh;
            public float TurnAngleDelta;
            public float TurnUprightDot;
            public bool Flipped;
            public bool OverallPass;
        }

        [MenuItem("Tools/HeavyVehiclesWorld/🧪 Run Phase 1 Touch Path Test")]
        public static void RunTouchPathTest()
        {
            int exitCode = 0;
            Debug.Log("================================================================================");
            Debug.Log("[PHASE 1 TOUCH-PATH TEST] Full Touch Path Simulation: Flat, Slope & Sharp Turn...");
            Debug.Log("================================================================================");

            Physics.simulationMode = SimulationMode.Script;
            var results = new List<VehicleTestResult>();

            try
            {
                string scenePath = "Assets/Scenes/MainWorld.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid()) throw new Exception($"Failed to open scene: {scenePath}");

                var initGo = GameObject.Find("WorldInitializer");
                if (initGo == null)
                {
                    initGo = new GameObject("WorldInitializer");
                    initGo.AddComponent<HVW_WorldInitializer>();
                }
                var worldInit = initGo.GetComponent<HVW_WorldInitializer>();
                if (worldInit == null) worldInit = initGo.AddComponent<HVW_WorldInitializer>();

                // Initialize world
                worldInit.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

                var canvasGo = GameObject.Find("HVW_MobileCanvas");
                if (canvasGo == null) throw new Exception("HVW_MobileCanvas not found!");

                var gasBtn = canvasGo.transform.Find("Btn_Gas")?.gameObject;
                var brakeBtn = canvasGo.transform.Find("Btn_Brake")?.gameObject;
                var steerLeftBtn = canvasGo.transform.Find("Btn_SteerLeft")?.gameObject;
                var steerRightBtn = canvasGo.transform.Find("Btn_SteerRight")?.gameObject;
                var handbrakeBtn = canvasGo.transform.Find("Btn_Handbrake")?.gameObject;

                if (gasBtn == null) throw new Exception("Btn_Gas not found on canvas!");
                if (brakeBtn == null) throw new Exception("Btn_Brake not found on canvas!");
                if (steerLeftBtn == null) throw new Exception("Btn_SteerLeft not found on canvas!");
                if (steerRightBtn == null) throw new Exception("Btn_SteerRight not found on canvas!");
                if (handbrakeBtn == null) throw new Exception("Btn_Handbrake not found on canvas!");

                var eventData = new PointerEventData(EventSystem.current);

                string[] vehicleNames = new[] { "Car_SUV", "Tractor_Agri", "Truck_Tipper", "JCB_Backhoe", "Excavator_Tracked", "Harvester_Combine" };

                // Build a dedicated 10-degree test slope ramp in the world
                var rampGo = new GameObject("TestSlopeRamp");
                rampGo.transform.position = new Vector3(200f, 2.5f, 200f);
                rampGo.transform.rotation = Quaternion.Euler(-10f, 0f, 0f); // 10 degree incline
                rampGo.transform.localScale = new Vector3(30f, 0.5f, 60f);
                var rampCol = rampGo.AddComponent<BoxCollider>();

                for (int v = 0; v < 6; v++)
                {
                    var res = new VehicleTestResult { Name = vehicleNames[v] };
                    Debug.Log($"\n>>> ======================================================= <<<");
                    Debug.Log($">>> TESTING VEHICLE [{v}]: {vehicleNames[v]} <<<");
                    Debug.Log($">>> ======================================================= <<<");

                    worldInit.SwitchToVehicleIndex(v);
                    var activeVeh = worldInit.ActiveVehicle;
                    if (activeVeh == null) throw new Exception($"Active vehicle [{v}] is NULL after SwitchToVehicleIndex!");
                    res.Type = activeVeh.vehicleType;

                    var rb = activeVeh.rb;
                    if (rb == null) throw new Exception($"Active vehicle [{v}] Rigidbody is NULL!");

                    // Initialize Vehicle Start
                    activeVeh.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

                    // Settle physics for 0.5s
                    for (int s = 0; s < 25; s++)
                    {
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }

                    Vector3 startPos = activeVeh.transform.position;
                    Quaternion startRot = activeVeh.transform.rotation;

                    // ── 1. FLAT GROUND TOUCH ACCELERATION TEST (3.0s GAS) ───────────
                    Debug.Log($"[Test 1.1 - Flat] Touching Gas Button via pointerDownHandler (Hold 3.0s)...");
                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerDownHandler);

                    for (int frame = 0; frame < 150; frame++) // 150 * 0.02s = 3.0s
                    {
                        worldInit.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }

                    res.FlatSpeedKmh = rb.linearVelocity.magnitude * 3.6f;
                    res.FlatDist = Vector3.Distance(startPos, activeVeh.transform.position);
                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerUpHandler);

                    Debug.Log($"[Result 1.1 - Flat Gas] {res.Name}: Speed = {res.FlatSpeedKmh:F1} km/h, Dist = {res.FlatDist:F2}m");

                    // ── 1.2 BRAKE TEST (1.5s BRAKE) ──────────────────────────────────
                    Debug.Log($"[Test 1.2 - Brake] Touching Brake Button (Hold 1.5s)...");
                    ExecuteEvents.Execute(brakeBtn, eventData, ExecuteEvents.pointerDownHandler);
                    for (int frame = 0; frame < 75; frame++)
                    {
                        worldInit.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }
                    res.FlatBrakeFinalSpeed = rb.linearVelocity.magnitude * 3.6f;
                    ExecuteEvents.Execute(brakeBtn, eventData, ExecuteEvents.pointerUpHandler);
                    Debug.Log($"[Result 1.2 - Brake] {res.Name}: Post-Brake Speed = {res.FlatBrakeFinalSpeed:F1} km/h");

                    // ── 2. SHARP TURN AT SPEED TEST (2.0s GAS + STEER LEFT) ──────────
                    Debug.Log($"[Test 2 - Sharp Turn] Gas + SteerLeft (Hold 2.5s)...");
                    Vector3 turnStartFwd = activeVeh.transform.forward;
                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(steerLeftBtn, eventData, ExecuteEvents.pointerDownHandler);

                    for (int frame = 0; frame < 125; frame++) // 2.5s
                    {
                        worldInit.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }

                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(steerLeftBtn, eventData, ExecuteEvents.pointerUpHandler);

                    res.TurnAngleDelta = Vector3.Angle(turnStartFwd, activeVeh.transform.forward);
                    res.TurnUprightDot = Vector3.Dot(activeVeh.transform.up, Vector3.up);
                    res.Flipped = res.TurnUprightDot < 0.65f;

                    Debug.Log($"[Result 2 - Sharp Turn] {res.Name}: Turn Angle = {res.TurnAngleDelta:F1} deg, Upright Dot = {res.TurnUprightDot:F2}, Flipped = {res.Flipped}");

                    // ── 3. SLOPE CLIMB TEST (10° INCLINE) ────────────────────────────
                    Debug.Log($"[Test 3 - Slope Climb] Repositioning to 10° Slope Ramp...");
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    // Place at bottom of slope facing uphill (+Z)
                    activeVeh.transform.position = new Vector3(200f, 1.5f, 185f);
                    activeVeh.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);

                    // Settle onto ramp
                    for (int s = 0; s < 25; s++)
                    {
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }

                    Vector3 slopeStartPos = activeVeh.transform.position;
                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerDownHandler);

                    for (int frame = 0; frame < 150; frame++) // 3.0s climb
                    {
                        worldInit.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                        activeVeh.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                        Physics.Simulate(0.02f);
                    }

                    ExecuteEvents.Execute(gasBtn, eventData, ExecuteEvents.pointerUpHandler);

                    res.SlopeSpeedKmh = rb.linearVelocity.magnitude * 3.6f;
                    res.SlopeClimbDist = Vector3.Distance(slopeStartPos, activeVeh.transform.position);

                    Debug.Log($"[Result 3 - Slope Climb] {res.Name}: Slope Speed = {res.SlopeSpeedKmh:F1} km/h, Climb Dist = {res.SlopeClimbDist:F2}m");

                    // Overall Pass verification
                    bool flatPass = res.FlatSpeedKmh >= 3.0f && res.FlatDist >= 1.0f;
                    bool turnPass = !res.Flipped && res.TurnUprightDot >= 0.70f;
                    bool slopePass = res.SlopeClimbDist >= 0.5f;

                    res.OverallPass = flatPass && turnPass && slopePass;
                    results.Add(res);

                    if (!res.OverallPass)
                    {
                        Debug.LogError($"❌ [FAIL] Vehicle '{res.Name}' failed verification! FlatPass={flatPass}, TurnPass={turnPass}, SlopePass={slopePass}");
                    }
                    else
                    {
                        Debug.Log($"✅ [PASS] Vehicle '{res.Name}' passed Flat, Turn & Slope touch tests!");
                    }

                    // Reset vehicle before next
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Cleanup test ramp
                GameObject.DestroyImmediate(rampGo);

                // Print Final Summary Table
                var sb = new StringBuilder();
                sb.AppendLine("\n================================================================================");
                sb.AppendLine("📊 PHASE 1 COMPREHENSIVE TOUCH PATH VERIFICATION REPORT");
                sb.AppendLine("================================================================================");
                sb.AppendLine("| Vehicle Name       | Flat Speed | Flat Dist | Post-Brake | Slope Dist | Turn Angle | Upright | Flipped | Status |");
                sb.AppendLine("|--------------------|------------|-----------|------------|------------|------------|---------|---------|--------|");
                bool allPass = true;
                foreach (var r in results)
                {
                    sb.AppendLine($"| {r.Name,-18} | {r.FlatSpeedKmh,6:F1} km/h | {r.FlatDist,7:F2}m | {r.FlatBrakeFinalSpeed,6:F1} km/h | {r.SlopeClimbDist,8:F2}m | {r.TurnAngleDelta,6:F1}°   | {r.TurnUprightDot,7:F2} | {r.Flipped,-7} | {(r.OverallPass ? "PASS" : "FAIL"),-6} |");
                    if (!r.OverallPass) allPass = false;
                }
                sb.AppendLine("================================================================================");
                if (allPass)
                {
                    sb.AppendLine("🎉 ALL 6 VEHICLES PASSED FLAT, SLOPE & SHARP TURN TOUCH TESTS!");
                }
                else
                {
                    sb.AppendLine("⚠️ ONE OR MORE VEHICLES FAILED TOUCH VERIFICATION!");
                    exitCode = 1;
                }
                sb.AppendLine("================================================================================");
                Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TOUCH-PATH TEST CRASH] ❌ Error: {ex.Message}\n{ex.StackTrace}");
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
