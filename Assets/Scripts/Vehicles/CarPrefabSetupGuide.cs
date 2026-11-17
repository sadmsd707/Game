using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Editor-only validation helper for Car prefabs.
    /// NOT a runtime script – attach in the Editor to surface setup errors early.
    /// Remove or disable before shipping to avoid execution overhead in builds.
    /// </summary>
    [ExecuteInEditMode]
    public class CarPrefabSetupGuide : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Inspector Fields – populate these in the prefab
        // ------------------------------------------------------------------ //

        [Header("Wheel Colliders (FL, FR, RL, RR)")]
        public WheelCollider wheelFL;
        public WheelCollider wheelFR;
        public WheelCollider wheelRL;
        public WheelCollider wheelRR;

        [Header("Wheel Meshes (FL, FR, RL, RR)")]
        public Transform meshFL;
        public Transform meshFR;
        public Transform meshRL;
        public Transform meshRR;

        [Header("Other Requirements")]
        public AudioSource engineAudioSource;

        // ------------------------------------------------------------------ //
        // Validation Result (visible in Inspector)
        // ------------------------------------------------------------------ //

        [HideInInspector]
        public bool setupOK = false;

        /// <summary>True when all checks pass; false otherwise.</summary>
        public bool ValidatedOK => setupOK;

        // ------------------------------------------------------------------ //
        // Unity Editor Callback
        // ------------------------------------------------------------------ //

        private void OnValidate()
        {
            // Only run in edit mode to avoid cost in Play mode
            if (Application.isPlaying) return;

            bool ok = true;

            // ---- WheelColliders ----
            if (wheelFL == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Front-Left WheelCollider is not assigned on " + gameObject.name);
                ok = false;
            }
            if (wheelFR == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Front-Right WheelCollider is not assigned on " + gameObject.name);
                ok = false;
            }
            if (wheelRL == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Rear-Left WheelCollider is not assigned on " + gameObject.name);
                ok = false;
            }
            if (wheelRR == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Rear-Right WheelCollider is not assigned on " + gameObject.name);
                ok = false;
            }

            // ---- Wheel Meshes ----
            if (meshFL == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Front-Left wheel mesh Transform is not assigned on " + gameObject.name);
                ok = false;
            }
            if (meshFR == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Front-Right wheel mesh Transform is not assigned on " + gameObject.name);
                ok = false;
            }
            if (meshRL == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Rear-Left wheel mesh Transform is not assigned on " + gameObject.name);
                ok = false;
            }
            if (meshRR == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Rear-Right wheel mesh Transform is not assigned on " + gameObject.name);
                ok = false;
            }

            // ---- Rigidbody ----
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] No Rigidbody found on " + gameObject.name + ". Add a Rigidbody component.");
                ok = false;
            }
            else if (rb.mass <= 0f)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Rigidbody mass must be greater than 0 on " + gameObject.name);
                ok = false;
            }

            // ---- AudioSource ----
            if (engineAudioSource == null)
            {
                Debug.LogWarning("[CarPrefabSetupGuide] Engine AudioSource is not assigned on " + gameObject.name);
                ok = false;
            }

            setupOK = ok;

            if (ok)
                Debug.Log("[CarPrefabSetupGuide] ✔ All checks passed for " + gameObject.name);
        }
    }
}
