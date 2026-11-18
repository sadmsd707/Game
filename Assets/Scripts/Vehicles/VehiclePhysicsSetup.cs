using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Add to any vehicle root. Applies WheelSuspensionData to all WheelColliders on Start.
    /// Also sets Rigidbody drag, angular drag, and centre of mass.
    /// </summary>
    public class VehiclePhysicsSetup : MonoBehaviour
    {
        [Header("Suspension")]
        public WheelSuspensionData suspensionData;

        [Header("Rigidbody Tuning")]
        public float   linearDrag   = 0.05f;
        public float   angularDrag  = 2.0f;
        public Vector3 centreOfMass = new Vector3(0f, -0.5f, 0.1f);

        private void Start()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping         = linearDrag;
                rb.angularDamping  = angularDrag;
                rb.centerOfMass = centreOfMass;
            }

            if (suspensionData == null)
            {
                Debug.LogWarning($"[VehiclePhysicsSetup] No WheelSuspensionData assigned on {gameObject.name}. Skipping wheel configuration.");
                return;
            }

            var wheels = GetComponentsInChildren<WheelCollider>();
            if (wheels.Length == 0)
            {
                Debug.LogWarning($"[VehiclePhysicsSetup] No WheelColliders found under {gameObject.name}.");
                return;
            }

            foreach (var wc in wheels)
                suspensionData.ApplyTo(wc);

            Debug.Log($"[VehiclePhysicsSetup] Applied suspension data '{suspensionData.name}' to {wheels.Length} wheel(s) on {gameObject.name}.");
        }
    }
}
