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

                // If not found in hierarchy, try loading real model from Resources/Vehicles/
                if (importedGo == null)
                {
                    var ctrl = slot.GetComponent<VehicleController>();
                    if (ctrl != null)
                    {
                        string vTypeStr = ctrl.vehicleType.ToString();
                        var prefab = Resources.Load<GameObject>($"Vehicles/{vTypeStr}/vehicle_body");
                        if (prefab != null)
                        {
                            var instance = Instantiate(prefab, root);
                            instance.name = "ImportedModel";
                            FitModelToCollider(instance.transform, root, ctrl.vehicleType);
                            foreach (var col in instance.GetComponentsInChildren<Collider>())
                            {
                                if (!(col is WheelCollider)) Destroy(col);
                            }
                            ApplyURPMaterials(instance, ctrl.vehicleType);
                            importedGo = instance.transform;
                        }
                    }
                }

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

        private static void FitModelToCollider(Transform modelT, Transform vehicleRoot, VehicleType vType)
        {
            var box = vehicleRoot.GetComponent<BoxCollider>();
            Vector3 targetSize = box != null ? box.size : new Vector3(2f, 1.5f, 4f);

            modelT.localPosition = Vector3.zero;
            modelT.localRotation = Quaternion.identity;
            modelT.localScale    = Vector3.one;

            var renderers = modelT.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            Vector3 modelSize = bounds.size;
            if (modelSize.x < 0.001f || modelSize.y < 0.001f || modelSize.z < 0.001f) return;

            float scaleX = targetSize.x / modelSize.x;
            float scaleY = targetSize.y / modelSize.y;
            float scaleZ = targetSize.z / modelSize.z;
            float uniformScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ));

            modelT.localScale = Vector3.one * uniformScale;

            Vector3 colliderCenter = box != null ? box.center : new Vector3(0, 0.75f, 0);
            modelT.localPosition = colliderCenter - (bounds.center - vehicleRoot.position) * uniformScale;
        }

        private static void ApplyURPMaterials(GameObject model, VehicleType vType)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Color bodyColor = vType switch
            {
                VehicleType.Car       => new Color(0.20f, 0.40f, 0.80f),
                VehicleType.Truck     => new Color(0.80f, 0.30f, 0.10f),
                VehicleType.Tractor   => new Color(0.10f, 0.55f, 0.15f),
                VehicleType.JCB       => new Color(1.00f, 0.75f, 0.00f),
                VehicleType.Excavator => new Color(1.00f, 0.65f, 0.00f),
                VehicleType.Harvester => new Color(0.85f, 0.20f, 0.10f),
                _                     => Color.grey
            };

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null &&
                        (mats[i].shader.name.Contains("Universal") || mats[i].shader.name.Contains("Lit")))
                        continue;

                    var newMat = new Material(shader)
                    {
                        color = bodyColor,
                        name = $"{model.name}_URP_{i}"
                    };
                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.5f);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic", 0.1f);
                    mats[i] = newMat;
                }
                r.sharedMaterials = mats;
            }
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
