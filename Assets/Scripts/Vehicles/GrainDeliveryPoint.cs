using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// A delivery zone where the harvester can unload its grain into the silo.
    /// Attach to an empty GameObject at the silo location; a SphereCollider
    /// (IsTrigger, radius 8) is added automatically at Awake if one is missing.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class GrainDeliveryPoint : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Trigger Zone")]
        [SerializeField] private float triggerRadius = 8f;

        [Header("UI")]
        [SerializeField] private TMP_Text promptText;
        private const string PromptMessage = "Extend Auger to Unload";

        [Header("Tracking")]
        public float totalGrainReceived;
        public bool  harvesterInRange;

        [Header("Events")]
        /// <summary>Fired each time grain is received; passes the amount added.</summary>
        public UnityEvent<float> OnGrainReceived = new UnityEvent<float>();

        // ── References ────────────────────────────────────────────────────
        private SphereCollider _trigger;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _trigger          = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius    = triggerRadius;

            SetPromptVisible(false);
        }

        // ── Trigger callbacks ─────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Harvester") &&
                other.GetComponentInParent<HarvesterController>() == null)
                return;

            harvesterInRange = true;
            SetPromptVisible(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Harvester") &&
                other.GetComponentInParent<HarvesterController>() == null)
                return;

            harvesterInRange = false;
            SetPromptVisible(false);
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>
        /// Called by HarvesterController.UnloadGrain() to deposit grain here.
        /// Fires OnGrainReceived with the amount added.
        /// </summary>
        public void ReceiveGrain(float amount)
        {
            if (amount <= 0f) return;
            totalGrainReceived += amount;
            OnGrainReceived.Invoke(amount);
            Debug.Log($"[GrainDeliveryPoint] Received {amount:F1} units. Total: {totalGrainReceived:F1}");
        }

        /// <summary>Resets the cumulative grain counter (call at mission start).</summary>
        public void ResetDeliveryPoint()
        {
            totalGrainReceived = 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void SetPromptVisible(bool visible)
        {
            if (promptText == null) return;
            promptText.text    = PromptMessage;
            promptText.gameObject.SetActive(visible);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, triggerRadius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
