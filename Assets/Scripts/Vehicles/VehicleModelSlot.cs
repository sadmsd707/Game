using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Replaces PrimitiveMeshBuilder with a dedicated 3D model slot system.
    /// Each vehicle prefab contains an empty "Model" child where an imported 3D asset is placed.
    /// Manages wheel visual pose synchronisation using WheelCollider.GetWorldPose and provides
    /// mechanical joint slots for heavy machinery (tipper, boom, stick, bucket, hitch, cutter).
    /// </summary>
    [ExecuteAlways]
    public class VehicleModelSlot : MonoBehaviour
    {
        [Header("3D Model Slot")]
        [Tooltip("The empty child GameObject named 'Model' where the downloaded 3D model is parented.")]
        public Transform modelSlot;

        [Header("Vehicle Identity")]
        public VehicleType vehicleType = VehicleType.Car;

        [System.Serializable]
        public class WheelBinding
        {
            public string name;
            public WheelCollider collider;
            public Transform mesh;
            public bool isSteer;
            public bool isDriven;
        }

        [Header("Wheel Slots (GetWorldPose Synchronisation)")]
        public WheelBinding[] wheels = new WheelBinding[0];

        [Header("Special Machinery Rigging Slots")]
        [Tooltip("Cabin root that rotates on Excavator")]
        public Transform rotatingCabin;
        [Tooltip("Primary boom arm (JCB backhoe or Excavator boom)")]
        public Transform boomArm;
        [Tooltip("Secondary dipper stick arm (JCB backhoe or Excavator stick)")]
        public Transform stickArm;
        [Tooltip("Bucket / attachment (JCB backhoe or Excavator bucket)")]
        public Transform bucket;
        [Tooltip("Front loader boom arm (JCB front loader)")]
        public Transform frontLoaderBoom;
        [Tooltip("Front loader bucket (JCB front loader)")]
        public Transform frontLoaderBucket;
        [Tooltip("Dump bed (Truck tipper)")]
        public Transform tipperBed;
        [Tooltip("Rear 3-point hitch (Tractor)")]
        public Transform threePointHitch;
        [Tooltip("Cutter header (Harvester)")]
        public Transform cutterHeader;
        [Tooltip("Grain discharge auger pipe (Harvester)")]
        public Transform unloadAuger;

        private bool _hasWarnedMissing = false;

        private void Awake()
        {
            ValidateModelPresence();
        }

        private void OnValidate()
        {
            ValidateModelPresence();
        }

        /// <summary>
        /// Validates whether an imported 3D model is present in the modelSlot.
        /// If missing, logs a clear instructional warning rather than generating fake primitives.
        /// </summary>
        public bool ValidateModelPresence()
        {
            if (modelSlot == null)
            {
                Transform found = transform.Find("Model");
                if (found != null) modelSlot = found;
                else
                {
                    GameObject mGo = new GameObject("Model");
                    mGo.transform.SetParent(transform, false);
                    modelSlot = mGo.transform;
                }
            }

            bool hasModel = modelSlot.childCount > 0;
            if (!hasModel && !_hasWarnedMissing && Application.isPlaying)
            {
                _hasWarnedMissing = true;
                Debug.LogWarning($"[VehicleModelSlot] ⚠️ No 3D model asset found in '{transform.name}/Model' for {vehicleType}!\n" +
                                 $"To import: Download the 3D model (.fbx/.obj) and drop it into 'Assets/Models/Vehicles/{vehicleType}/', then drag it into the 'Model' slot.\n" +
                                 $"See ASSET_INTEGRATION_GUIDE.md for exact models, pivot rules, and rigging instructions.");
            }
            else if (hasModel)
            {
                AutoBindPartsFromModel();
            }
            return hasModel;
        }

        /// <summary>
        /// Automatically maps child transforms of the imported 3D model to rigging and wheel slots
        /// based on common naming conventions.
        /// </summary>
        public void AutoBindPartsFromModel()
        {
            if (modelSlot == null || modelSlot.childCount == 0) return;

            Transform[] allChildren = modelSlot.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                string lower = child.name.ToLowerInvariant();

                // Rigging slots
                if (tipperBed == null && (lower.Contains("tipper") || lower.Contains("dumpbed") || lower.Contains("bed")))
                    tipperBed = child;
                if (boomArm == null && (lower.Contains("boom") || lower.Contains("arm1") || lower.Contains("mainarm")))
                    boomArm = child;
                if (stickArm == null && (lower.Contains("stick") || lower.Contains("dipper") || lower.Contains("arm2")))
                    stickArm = child;
                if (bucket == null && lower.Contains("bucket") && !lower.Contains("loader") && !lower.Contains("front"))
                    bucket = child;
                if (frontLoaderBoom == null && (lower.Contains("loaderboom") || lower.Contains("frontarm") || (lower.Contains("loader") && lower.Contains("boom"))))
                    frontLoaderBoom = child;
                if (frontLoaderBucket == null && (lower.Contains("loaderbucket") || (lower.Contains("front") && lower.Contains("bucket"))))
                    frontLoaderBucket = child;
                if (rotatingCabin == null && (lower.Contains("rotcabin") || lower.Contains("turret") || lower.Contains("upperstructure")))
                    rotatingCabin = child;
                if (threePointHitch == null && (lower.Contains("hitch") || lower.Contains("linkage")))
                    threePointHitch = child;
                if (cutterHeader == null && (lower.Contains("header") || lower.Contains("cutter") || lower.Contains("reel")))
                    cutterHeader = child;
                if (unloadAuger == null && (lower.Contains("auger") || lower.Contains("pipe") || lower.Contains("spout")))
                    unloadAuger = child;

                // Wheel mesh auto-binding
                if (wheels != null && wheels.Length > 0 && lower.Contains("wheel"))
                {
                    for (int i = 0; i < wheels.Length; i++)
                    {
                        if (wheels[i].mesh == null)
                        {
                            if ((lower.Contains("fl") || lower.Contains("frontleft") || lower.Contains("front_l")) && i == 0)
                                wheels[i].mesh = child;
                            else if ((lower.Contains("fr") || lower.Contains("frontright") || lower.Contains("front_r")) && i == 1)
                                wheels[i].mesh = child;
                            else if ((lower.Contains("rl") || lower.Contains("rearleft") || lower.Contains("back_l")) && i == 2)
                                wheels[i].mesh = child;
                            else if ((lower.Contains("rr") || lower.Contains("rearright") || lower.Contains("back_r")) && i == 3)
                                wheels[i].mesh = child;
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // Sync all wheel meshes to their respective WheelColliders using GetWorldPose
            if (wheels != null && wheels.Length > 0)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    var w = wheels[i];
                    if (w.collider != null && w.mesh != null)
                    {
                        w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                        w.mesh.position = pos;
                        w.mesh.rotation = rot;
                    }
                }
            }
        }
    }
}
