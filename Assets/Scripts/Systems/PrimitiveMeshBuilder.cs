using System;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// [OBSOLETE] PrimitiveMeshBuilder has been deprecated and replaced by VehicleModelSlot.
    /// Do not generate fake/primitive placeholder meshes.
    /// Use real imported 3D models (.fbx / .obj / .gltf) placed under the 'Model' child transform.
    /// Refer to ASSET_INTEGRATION_GUIDE.md for model sources, pivot points, and rigging steps.
    /// </summary>
    [Obsolete("PrimitiveMeshBuilder is deprecated. Use VehicleModelSlot with real 3D models instead.")]
    public class PrimitiveMeshBuilder : MonoBehaviour
    {
        public enum VehicleTemplate
        {
            Car,
            Truck,
            Tractor,
            JCB,
            Excavator,
            Harvester
        }

        [SerializeField] private VehicleTemplate template = VehicleTemplate.Car;

        private void Awake()
        {
            // Direct user to VehicleModelSlot
            var slot = GetComponent<VehicleModelSlot>();
            if (slot == null)
            {
                slot = gameObject.AddComponent<VehicleModelSlot>();
                slot.vehicleType = (VehicleType)template;
            }
            Debug.LogWarning($"[PrimitiveMeshBuilder] ⚠️ PrimitiveMeshBuilder is obsolete. " +
                             $"Use VehicleModelSlot on '{gameObject.name}'. See ASSET_INTEGRATION_GUIDE.md.");
        }
    }
}
