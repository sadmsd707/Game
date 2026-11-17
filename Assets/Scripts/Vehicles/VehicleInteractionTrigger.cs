using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Attach to each vehicle. Shows an "Enter" prompt when the player enters
    /// the trigger sphere whose radius matches VehicleController.EnterRadius.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class VehicleInteractionTrigger : MonoBehaviour
    {
        // ── References ────────────────────────────────────────────────────
        [Header("Vehicle")]
        [SerializeField] private VehicleController vehicleController;

        // ── Events ────────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent OnPromptShow;
        public UnityEvent OnPromptHide;

        // ── Components ────────────────────────────────────────────────────
        private SphereCollider _trigger;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;

            // Auto-find parent VehicleController if not assigned
            if (vehicleController == null)
                vehicleController = GetComponentInParent<VehicleController>();

            // Sync trigger radius
            if (vehicleController != null)
                _trigger.radius = vehicleController.EnterRadius;
        }

        // ── Trigger callbacks ─────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            if (vehicleController != null && vehicleController.isOccupied) return;
            OnPromptShow?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            OnPromptHide?.Invoke();
        }
    }
}
