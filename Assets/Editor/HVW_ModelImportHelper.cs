using System.IO;
using UnityEngine;
using UnityEditor;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Editor
{
    /// <summary>
    /// Step 1 — Vehicle Model Import Helper.
    ///
    /// Scans Assets/Models/Vehicles/<VehicleType>/ for vehicle_body.glb files,
    /// instantiates them into the scene under each vehicle's root (as a child named
    /// "ImportedModel"), scales/positions them to match the physics collider box,
    /// and writes .meta files so Unity tracks them.
    ///
    /// Run via menu: HeavyVehiclesWorld → Step 1 → Import Vehicle Models
    ///
    /// Rules enforced:
    ///   - Does NOT touch WheelColliders or Rigidbody.
    ///   - Strips any MeshCollider from imported geometry (physics stays in existing colliders).
    ///   - If no .glb exists for a vehicle, it is skipped (placeholder stays active).
    ///   - Reports exactly what was imported and what is missing.
    /// </summary>
    public static class HVW_ModelImportHelper
    {
        [MenuItem("HeavyVehiclesWorld/Step 1 - Import Vehicle Models")]
        public static void ImportVehicleModels()
        {
            int imported = 0;
            int skipped  = 0;
            var report   = new System.Text.StringBuilder();
            report.AppendLine("=== Step 1 Vehicle Model Import Report ===\n");

            // Vehicle type to folder name mapping
            var vehicleFolders = new System.Collections.Generic.Dictionary<VehicleType, string>
            {
                { VehicleType.Car,       "Car"       },
                { VehicleType.Truck,     "Truck"     },
                { VehicleType.Tractor,   "Tractor"   },
                { VehicleType.JCB,       "JCB"       },
                { VehicleType.Excavator, "Excavator" },
                { VehicleType.Harvester, "Harvester" },
            };

            foreach (var kvp in vehicleFolders)
            {
                var vType  = kvp.Key;
                var folder = kvp.Value;
                string assetPath = $"Assets/Models/Vehicles/{folder}/vehicle_body.glb";
                string fullPath  = Path.Combine(Application.dataPath, "..", assetPath);

                // Check if the GLB exists
                if (!File.Exists(fullPath))
                {
                    report.AppendLine($"❌ {folder}: vehicle_body.glb NOT FOUND — placeholder stays active");
                    skipped++;
                    continue;
                }

                // Find the vehicle controller in the scene
                var ctrl = FindVehicleController(vType);
                if (ctrl == null)
                {
                    report.AppendLine($"⚠️  {folder}: GLB found but no {vType} in scene yet — will bind on first play");
                    skipped++;
                    continue;
                }

                var vehicleRoot = ctrl.transform;

                // Check if already imported
                var existingImport = vehicleRoot.Find("ImportedModel");
                if (existingImport != null)
                {
                    report.AppendLine($"✅ {folder}: ImportedModel already present — skipping re-import");
                    imported++;
                    continue;
                }

                // Load the GLB asset
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    // Force Unity to import it
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                }

                if (prefab == null)
                {
                    report.AppendLine($"❌ {folder}: GLB found on disk but Unity could not load it as a prefab");
                    skipped++;
                    continue;
                }

                // Instantiate under vehicle root
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, vehicleRoot);
                instance.name = "ImportedModel";

                // Scale and position to fit the physics collider
                FitModelToCollider(instance.transform, vehicleRoot, vType);

                // Strip any mesh colliders from imported geo (keep existing physics)
                foreach (var col in instance.GetComponentsInChildren<Collider>())
                {
                    if (!(col is WheelCollider))
                        Object.DestroyImmediate(col);
                }

                // Apply URP/Lit materials to all renderers
                ApplyURPMaterials(instance, vType);

                // Hide placeholder
                var slot = vehicleRoot.GetComponent<VehicleModelSlot>();
                if (slot != null && slot.placeholderVisuals != null)
                    slot.placeholderVisuals.SetActive(false);

                EditorUtility.SetDirty(vehicleRoot.gameObject);

                report.AppendLine($"✅ {folder}: Imported vehicle_body.glb → scale={instance.transform.localScale}, pos={instance.transform.localPosition}");
                imported++;
            }

            report.AppendLine($"\nSummary: {imported} imported, {skipped} skipped/missing");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Step 1 — Vehicle Model Import", report.ToString(), "OK");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static VehicleController FindVehicleController(VehicleType type)
        {
            var allCtrls = Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
            foreach (var c in allCtrls)
                if (c.vehicleType == type) return c;
            return null;
        }

        /// <summary>
        /// Scale and position the imported model to fit inside the existing BoxCollider.
        /// Does NOT move or change the BoxCollider.
        /// </summary>
        private static void FitModelToCollider(Transform modelT, Transform vehicleRoot, VehicleType vType)
        {
            // Get the existing box collider size as the target bounding box
            var box = vehicleRoot.GetComponent<BoxCollider>();
            Vector3 targetSize = box != null ? box.size : new Vector3(2f, 1.5f, 4f);

            // Reset the model transform before measuring its bounds
            modelT.localPosition = Vector3.zero;
            modelT.localRotation = Quaternion.identity;
            modelT.localScale    = Vector3.one;

            // Measure model bounds in local space
            var renderers = modelT.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // Convert bounds size to local space of the vehicle root
            Vector3 modelSize = bounds.size;
            if (modelSize.x < 0.001f || modelSize.y < 0.001f || modelSize.z < 0.001f) return;

            // Uniform scale to fit the longest axis, then fine-tune
            float scaleX = targetSize.x / modelSize.x;
            float scaleY = targetSize.y / modelSize.y;
            float scaleZ = targetSize.z / modelSize.z;
            float uniformScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ));

            modelT.localScale = Vector3.one * uniformScale;

            // Centre the model on the collider centre offset
            Vector3 colliderCenter = box != null ? box.center : new Vector3(0, 0.75f, 0);
            modelT.localPosition = colliderCenter - (bounds.center - vehicleRoot.position) * uniformScale;
        }

        /// <summary>
        /// Apply URP/Lit materials so the model uses the render pipeline correctly.
        /// Uses the vehicle's colour palette.
        /// </summary>
        private static void ApplyURPMaterials(GameObject model, VehicleType vType)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Color bodyColor = vType switch
            {
                VehicleType.Car       => new Color(0.20f, 0.40f, 0.80f),   // Cobalt blue
                VehicleType.Truck     => new Color(0.80f, 0.30f, 0.10f),   // Orange-red
                VehicleType.Tractor   => new Color(0.10f, 0.55f, 0.15f),   // John Deere green
                VehicleType.JCB       => new Color(1.00f, 0.75f, 0.00f),   // JCB yellow
                VehicleType.Excavator => new Color(1.00f, 0.65f, 0.00f),   // Excavator orange-yellow
                VehicleType.Harvester => new Color(0.85f, 0.20f, 0.10f),   // Combine red
                _                     => Color.grey
            };

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var mat = new Material(shader) { name = $"M_{vType}_{i}" };

                    // Try to preserve existing textures from the GLB
                    if (mats[i].HasProperty("_MainTex") && mats[i].GetTexture("_MainTex") != null)
                        mat.SetTexture("_BaseMap", mats[i].GetTexture("_MainTex"));
                    else if (mats[i].HasProperty("_BaseMap") && mats[i].GetTexture("_BaseMap") != null)
                        mat.SetTexture("_BaseMap", mats[i].GetTexture("_BaseMap"));
                    else
                    {
                        // No texture — apply vehicle body colour
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", bodyColor);
                        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     bodyColor);
                    }

                    // Metallic/smoothness defaults for a painted vehicle
                    if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic",    0.0f);
                    if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness",  0.5f);
                    if (mat.HasProperty("_Glossiness"))  mat.SetFloat("_Glossiness",  0.5f);

                    mats[i] = mat;
                }
                renderer.sharedMaterials = mats;
            }
        }
    }
}
