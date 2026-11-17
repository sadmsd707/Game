using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Runtime manager that spawns and attaches implements to a TractorController.
    /// Assign prefabs for each implement type in the Inspector.
    /// Press X (keyboard) or DPad Down (gamepad) to detach the current implement.
    /// </summary>
    public class ImplementManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Tractor")]
        [SerializeField] private TractorController trackedTractor;

        [Header("Implement Prefabs")]
        [SerializeField] private ImplementPrefabEntry[] implementPrefabEntries;

        // ── State ─────────────────────────────────────────────────────────

        /// <summary>Maps each AttachedImplement type to its prefab GameObject.</summary>
        private Dictionary<AttachedImplement, GameObject> implementPrefabs;

        /// <summary>The currently spawned and attached implement GameObject (null if none).</summary>
        private GameObject currentImplement;

        // ── Input state for DetachImplement button ─────────────────────────
        private bool _detachPressedLastFrame;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            // Build the dictionary from the serialisable array
            implementPrefabs = new Dictionary<AttachedImplement, GameObject>();
            if (implementPrefabEntries != null)
            {
                foreach (var entry in implementPrefabEntries)
                {
                    if (entry.prefab != null && !implementPrefabs.ContainsKey(entry.type))
                        implementPrefabs[entry.type] = entry.prefab;
                }
            }
        }

        private void Update()
        {
            HandleDetachInput();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Spawns the prefab for <paramref name="type"/> and attaches it to the tracked tractor.
        /// Any previously attached implement is first detached and destroyed.
        /// </summary>
        public void SpawnAndAttach(AttachedImplement type)
        {
            if (type == AttachedImplement.None)
            {
                DetachCurrent();
                return;
            }

            if (!implementPrefabs.TryGetValue(type, out GameObject prefab) || prefab == null)
            {
                Debug.LogWarning($"[ImplementManager] No prefab registered for {type}.");
                return;
            }

            // Detach anything currently attached
            if (currentImplement != null)
                DetachCurrent();

            // Spawn the implement
            GameObject instance = Instantiate(prefab);
            currentImplement    = instance;

            // Call the appropriate attach method depending on implement type
            switch (type)
            {
                case AttachedImplement.Plough:
                    var plough = instance.GetComponent<PloughImplement>();
                    if (plough != null)
                        plough.AttachToTractor(trackedTractor);
                    else
                        Debug.LogWarning("[ImplementManager] Plough prefab missing PloughImplement component.");
                    break;

                case AttachedImplement.Trailer:
                    var trailer = instance.GetComponent<TractorTrailerController>();
                    if (trailer != null)
                        trailer.AttachToTractor(trackedTractor);
                    else
                        Debug.LogWarning("[ImplementManager] Trailer prefab missing TractorTrailerController component.");
                    break;

                case AttachedImplement.FrontLoader:
                    var loader = instance.GetComponent<FrontLoaderImplement>();
                    if (loader != null)
                        loader.AttachToTractor(trackedTractor);
                    else
                        Debug.LogWarning("[ImplementManager] FrontLoader prefab missing FrontLoaderImplement component.");
                    break;

                default:
                    Debug.LogWarning($"[ImplementManager] SpawnAndAttach: unhandled type {type}.");
                    break;
            }
        }

        /// <summary>
        /// Detaches and destroys the currently attached implement.
        /// </summary>
        public void DetachCurrent()
        {
            if (currentImplement == null) return;

            // Attempt each known implement type's detach method
            var plough  = currentImplement.GetComponent<PloughImplement>();
            var trailer = currentImplement.GetComponent<TractorTrailerController>();
            var loader  = currentImplement.GetComponent<FrontLoaderImplement>();

            if (plough  != null) plough.DetachFromTractor();
            if (trailer != null) trailer.Detach();
            if (loader  != null) loader.DetachFromTractor();

            Destroy(currentImplement);
            currentImplement = null;
        }

        // ── Input handling ────────────────────────────────────────────────

        private void HandleDetachInput()
        {
            bool pressed = IsDetachPressed();
            if (pressed && !_detachPressedLastFrame)
                DetachCurrent();
            _detachPressedLastFrame = pressed;
        }

        /// <summary>
        /// Returns true when the "DetachImplement" action fires.
        /// Keyboard: X key.  Gamepad: DPad Down.
        /// Uses InputManager when available, falls back to legacy Input.
        /// </summary>
        private bool IsDetachPressed()
        {
            // Legacy keyboard fallback (always available)
            if (Input.GetKey(KeyCode.X)) return true;

            // Gamepad DPad Down via legacy Input axes (if mapped)
            float dpadY = Input.GetAxis("DPadY");
            if (dpadY < -0.5f) return true;

            // New Input System via InputManager singleton (optional integration)
            // InputManager.Instance can expose a DetachImplement bool property
            // If that property exists, check it here. We guard with null-conditional
            // so the game still compiles even if InputManager doesn't expose it yet.
            // Uncomment and extend InputManager.cs to wire this up:
            // if (InputManager.Instance != null && InputManager.Instance.DetachImplementPressed)
            //     return true;

            return false;
        }

        // ── Nested types ──────────────────────────────────────────────────

        /// <summary>Serialisable key-value pair for the implements dictionary.</summary>
        [System.Serializable]
        public class ImplementPrefabEntry
        {
            public AttachedImplement type;
            public GameObject        prefab;
        }
    }
}
