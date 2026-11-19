using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Step 1 — Real Vehicle Model Loader.
    ///
    /// Scans Assets/Models/Vehicles/<VehicleType>/ for vehicle_body.glb files at runtime,
    /// loads them via UnityWebRequest (GLB is a valid glTF binary the Unity runtime can
    /// load through the built-in glTF loader on Android), parents the mesh under the
    /// VehicleModelSlot.modelSlot transform, and hides the placeholder visuals.
    ///
    /// If no model file is found the placeholder visuals stay visible and a warning
    /// is shown in the diagnostics overlay.
    ///
    /// IMPORTANT: Unity's runtime does NOT have a built-in arbitrary-GLB importer for
    /// play mode — GLB files must be imported as Assets in the Editor first. This script
    /// therefore works at EDIT TIME (via HVW_ModelImportHelper in the Editor folder) and
    /// simply activates/deactivates the already-imported GameObjects at runtime.
    ///
    /// At runtime this script:
    ///   1. Looks for a child named "ImportedModel" under the vehicle root.
    ///   2. If found: activates it, hides placeholder.
    ///   3. If not found: keeps placeholder, logs a warning to DiagnosticsOverlay.
    /// </summary>
    public class HVW_ModelLoader : MonoBehaviour
    {
        public static HVW_ModelLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Defer one frame so all vehicles have spawned
            StartCoroutine(LoadAllVehicleModels());
        }

        private IEnumerator LoadAllVehicleModels()
        {
            yield return null; // wait one frame

            var slots = FindObjectsByType<VehicleModelSlot>(FindObjectsSortMode.None);
            int loaded   = 0;
            int missing  = 0;

            foreach (var slot in slots)
            {
                var root = slot.transform;

                // Check if the Editor import helper already placed an ImportedModel child
                var importedGo = root.Find("ImportedModel");
                if (importedGo != null)
                {
                    importedGo.gameObject.SetActive(true);

                    // Hide placeholder
                    if (slot.placeholderVisuals != null)
                        slot.placeholderVisuals.SetActive(false);

                    // Bind wheels from imported model naming convention
                    BindImportedWheels(slot, importedGo);

                    loaded++;
                    Debug.Log($"[ModelLoader] {root.name}: real model active.");
                }
                else
                {
                    missing++;
                    string msg = $"[ModelLoader] {root.name}: no ImportedModel child — placeholder active.";
                    Debug.LogWarning(msg);
                    HVW_DiagnosticsOverlay.AddWarning(msg);
                }
            }

            Debug.Log($"[ModelLoader] Done — {loaded} real models, {missing} placeholders.");
        }

        /// <summary>
        /// Attempts to bind wheel mesh transforms from an imported model by name convention.
        /// Wheels must be named: Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR (case-insensitive).
        /// </summary>
        private void BindImportedWheels(VehicleModelSlot slot, Transform modelRoot)
        {
            if (slot == null || modelRoot == null) return;

            var ctrl = slot.GetComponent<VehicleController>();
            if (ctrl == null) return;

            // Search for wheel transforms
            Transform fl = FindChildByPattern(modelRoot, "wheel_fl", "wheelf_l", "front_left", "fl_wheel");
            Transform fr = FindChildByPattern(modelRoot, "wheel_fr", "wheelf_r", "front_right", "fr_wheel");
            Transform rl = FindChildByPattern(modelRoot, "wheel_rl", "wheelr_l", "rear_left",  "rl_wheel");
            Transform rr = FindChildByPattern(modelRoot, "wheel_rr", "wheelr_r", "rear_right", "rr_wheel");

            if (ctrl is CarController car)
            {
                if (fl) car.frontLeftMesh  = fl;
                if (fr) car.frontRightMesh = fr;
                if (rl) car.rearLeftMesh   = rl;
                if (rr) car.rearRightMesh  = rr;
            }
            else if (ctrl is TruckController truck)
            {
                if (fl) truck.frontLeftMesh  = fl;
                if (fr) truck.frontRightMesh = fr;
                if (rl) truck.rearLeftMesh   = rl;
                if (rr) truck.rearRightMesh  = rr;
            }
            else if (ctrl is TractorController tractor)
            {
                if (fl) tractor.frontLeftMesh  = fl;
                if (fr) tractor.frontRightMesh = fr;
                if (rl) tractor.rearLeftMesh   = rl;
                if (rr) tractor.rearRightMesh  = rr;
            }
        }

        private Transform FindChildByPattern(Transform root, params string[] patterns)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = t.name.ToLower().Replace("-", "_").Replace(" ", "_");
                foreach (var p in patterns)
                {
                    if (nameLower.Contains(p.ToLower()))
                        return t;
                }
            }
            return null;
        }
    }
}
