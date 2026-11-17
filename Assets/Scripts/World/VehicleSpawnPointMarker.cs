using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Scene-placed marker at each vehicle's spawn location.
    /// Draws a vehicle-type-coloured gizmo in the Scene view and tracks the spawned vehicle.
    /// </summary>
    public class VehicleSpawnPointMarker : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Spawn Config")]
        public VehicleType vehicleType;
        public string      vehicleLabel = "Vehicle";

        [Header("Gizmo Colour (auto-set by type, or override here)")]
        public Color gizmoColor = Color.white;

        [Header("Runtime")]
        [Tooltip("Populated by VehicleSpawner when a vehicle is instantiated at this point.")]
        public GameObject spawnedVehicle;

        // ── Properties ────────────────────────────────────────────────────────

        /// <summary>True while a vehicle occupies this spawn point.</summary>
        public bool IsOccupied => spawnedVehicle != null;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnValidate()
        {
            // Auto-assign colour when the type is changed in the Inspector
            gizmoColor = ColorForType(vehicleType);

            // Auto-set label if still default
            if (string.IsNullOrEmpty(vehicleLabel) || vehicleLabel == "Vehicle")
                vehicleLabel = vehicleType.ToString();
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Color col  = ColorForType(vehicleType);
            col.a      = IsOccupied ? 0.4f : 0.75f;
            Gizmos.color = col;

            // Vehicle-footprint wire box: 1m wide × 2m tall × 3m long
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.up, new Vector3(1f, 2f, 3f));
            Gizmos.matrix = Matrix4x4.identity;

            // Label (uses Gizmos icon fallback; replace with custom icon if available)
#if UNITY_EDITOR
            UnityEditor.Handles.color = col;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"[{vehicleType}]\n{vehicleLabel}");
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawSphere(transform.position, 2f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Returns the canonical gizmo colour for a vehicle type.</summary>
        public static Color ColorForType(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Car:       return new Color(0.2f, 0.4f, 1.0f);   // blue
                case VehicleType.Truck:     return new Color(1.0f, 0.5f, 0.0f);   // orange
                case VehicleType.Tractor:   return new Color(0.1f, 0.8f, 0.2f);   // green
                case VehicleType.JCB:       return new Color(1.0f, 0.9f, 0.0f);   // yellow
                case VehicleType.Excavator: return new Color(0.9f, 0.1f, 0.1f);   // red
                case VehicleType.Harvester: return new Color(0.0f, 0.9f, 0.9f);   // cyan
                default:                    return Color.white;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by VehicleSpawner to register the instantiated vehicle.</summary>
        public void RegisterSpawnedVehicle(GameObject vehicle)
        {
            spawnedVehicle = vehicle;
            Debug.Log($"[SpawnMarker] {vehicleLabel} spawned at {transform.position}.");
        }

        /// <summary>Clears the spawned vehicle reference (e.g., after vehicle destruction).</summary>
        public void ClearSpawnedVehicle()
        {
            spawnedVehicle = null;
        }
    }
}
