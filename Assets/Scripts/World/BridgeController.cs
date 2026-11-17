using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Controls the river bridge: load accumulation, collapse on overload, and mission reset repair.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BridgeController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Bridge State")]
        public bool  bridgeIntact   = true;
        public float maxLoadKg      = 20000f;
        public float currentLoadKg  = 0f;

        [Header("Visual Planks (optional)")]
        public Transform[] bridgePlanks;

        [Header("Effects")]
        public ParticleSystem dustEffect;

        [Header("Plank Drop Settings")]
        [Tooltip("How fast each plank falls during collapse.")]
        public float plankFallSpeed    = 5f;
        public float plankFallDuration = 3f;

        [Header("Events")]
        public UnityEvent OnBridgeCollapse;

        // ── Private ───────────────────────────────────────────────────────────

        private BoxCollider _bridgeCollider;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _bridgeCollider           = GetComponent<BoxCollider>();
            _bridgeCollider.isTrigger = true;   // acts as trigger for load sensing
        }

        private void Update()
        {
            if (bridgeIntact && currentLoadKg > maxLoadKg)
            {
                TriggerCollapse();
            }
        }

        // ── Trigger callbacks ─────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!bridgeIntact) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                // mass is in kg (Unity default is kg)
                currentLoadKg += rb.mass;
                Debug.Log($"[Bridge] Vehicle entered. Load: {currentLoadKg:F0} / {maxLoadKg:F0} kg");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                currentLoadKg = Mathf.Max(0f, currentLoadKg - rb.mass);
                Debug.Log($"[Bridge] Vehicle exited.  Load: {currentLoadKg:F0} / {maxLoadKg:F0} kg");
            }
        }

        // ── Collapse ──────────────────────────────────────────────────────────

        /// <summary>Collapses the bridge: disables surface, plays dust, drops planks.</summary>
        public void TriggerCollapse()
        {
            if (!bridgeIntact) return;

            bridgeIntact = false;
            Debug.LogWarning("[Bridge] BRIDGE COLLAPSE – load exceeded maximum!");

            // Disable the driveable collider
            _bridgeCollider.enabled = false;

            // Dust particle burst
            if (dustEffect != null)
                dustEffect.Play();

            // Notify listeners (MissionManager, audio, etc.)
            OnBridgeCollapse?.Invoke();

            // Drop planks over time
            if (bridgePlanks != null && bridgePlanks.Length > 0)
                StartCoroutine(DropPlanksCoroutine());
        }

        private IEnumerator DropPlanksCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < plankFallDuration)
            {
                elapsed += Time.deltaTime;
                foreach (var plank in bridgePlanks)
                {
                    if (plank == null) continue;
                    plank.position += Vector3.down * (plankFallSpeed * Time.deltaTime);
                }
                yield return null;
            }

            // Deactivate planks after they fall
            foreach (var plank in bridgePlanks)
                if (plank != null) plank.gameObject.SetActive(false);
        }

        // ── Repair ────────────────────────────────────────────────────────────

        /// <summary>Resets the bridge to intact state (call from mission reset).</summary>
        public void RepairBridge()
        {
            bridgeIntact              = true;
            currentLoadKg             = 0f;
            _bridgeCollider.enabled   = true;

            // Restore plank positions and visibility
            if (bridgePlanks != null)
            {
                foreach (var plank in bridgePlanks)
                {
                    if (plank == null) continue;
                    plank.gameObject.SetActive(true);
                    // Note: position reset requires the calling code to restore
                    // transforms (store originals if precise reset is needed).
                }
            }

            if (dustEffect != null)
                dustEffect.Stop();

            Debug.Log("[Bridge] Bridge repaired and reset.");
        }

        // ── Gizmos ───────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = bridgeIntact ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
            if (_bridgeCollider != null)
                Gizmos.DrawWireCube(transform.position + _bridgeCollider.center, _bridgeCollider.size);

            // Load bar in scene view
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"Load: {currentLoadKg:F0} / {maxLoadKg:F0} kg\n{(bridgeIntact ? "INTACT" : "COLLAPSED")}");
#endif
        }
    }
}
