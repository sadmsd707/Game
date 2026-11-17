using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Runtime vehicle spawner.  Assign prefabs in the Inspector and define
    /// spawn points.  Call SpawnAll() on Start or trigger it from a mission.
    /// </summary>
    public class VehicleSpawner : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Serialisable spawn-point definition                                 //
        // ------------------------------------------------------------------ //
        [System.Serializable]
        public class VehicleSpawnPoint
        {
            public VehicleType vehicleType;
            public Vector3     position;
            public Vector3     eulerRotation;
        }

        // ------------------------------------------------------------------ //
        //  Inspector fields                                                    //
        // ------------------------------------------------------------------ //
        [Header("Prefab Catalogue")]
        [SerializeField]
        private VehicleTypePrefabEntry[] vehiclePrefabEntries;   // assigned in Inspector

        [Header("Spawn Points")]
        [SerializeField]
        private List<VehicleSpawnPoint> spawnPoints = new List<VehicleSpawnPoint>();

        [Header("Options")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool usePooling   = true;

        // Runtime dictionary built from the serialisable array
        private Dictionary<VehicleType, GameObject> vehiclePrefabs;

        // Optional pooler reference (may be null)
        private ObjectPooler objectPooler;

        // ------------------------------------------------------------------ //
        //  Inner helper: serialisable key-value pair for the prefab dictionary //
        // ------------------------------------------------------------------ //
        [System.Serializable]
        public class VehicleTypePrefabEntry
        {
            public VehicleType vehicleType;
            public GameObject  prefab;
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            // Build the runtime dictionary from the inspector array
            vehiclePrefabs = new Dictionary<VehicleType, GameObject>();
            if (vehiclePrefabEntries != null)
            {
                foreach (var entry in vehiclePrefabEntries)
                {
                    if (entry.prefab != null && !vehiclePrefabs.ContainsKey(entry.vehicleType))
                        vehiclePrefabs[entry.vehicleType] = entry.prefab;
                }
            }

            // Find pooler (optional)
            objectPooler = FindObjectOfType<ObjectPooler>();
        }

        private void Start()
        {
            if (spawnOnStart)
                SpawnAll();
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Spawns every vehicle defined in the spawn-point list.</summary>
        public void SpawnAll()
        {
            foreach (var sp in spawnPoints)
            {
                SpawnVehicle(sp.vehicleType,
                             sp.position,
                             Quaternion.Euler(sp.eulerRotation));
            }
        }

        /// <summary>
        /// Spawns a single vehicle of the given type at the given position /
        /// rotation.  Uses the ObjectPooler when available and usePooling is
        /// true; otherwise falls back to Instantiate.
        /// </summary>
        /// <returns>The VehicleController on the spawned object, or null.</returns>
        public VehicleController SpawnVehicle(VehicleType type, Vector3 pos, Quaternion rot)
        {
            if (!vehiclePrefabs.TryGetValue(type, out GameObject prefab) || prefab == null)
            {
                Debug.LogWarning($"[VehicleSpawner] No prefab registered for VehicleType '{type}'.");
                return null;
            }

            GameObject instance;

            if (usePooling && objectPooler != null)
            {
                instance = objectPooler.GetPooledObject(prefab.name);
                if (instance == null)
                {
                    // Pool empty or pool entry missing – fall back to Instantiate
                    instance = Instantiate(prefab, pos, rot);
                }
                else
                {
                    instance.transform.SetPositionAndRotation(pos, rot);
                    instance.SetActive(true);
                }
            }
            else
            {
                instance = Instantiate(prefab, pos, rot);
            }

            // Name for scene clarity
            instance.name = $"{type}_{pos}";

            // Register with the GameManager
            var controller = instance.GetComponent<VehicleController>();
            if (controller != null && GameManager.Instance != null)
            {
                GameManager.Instance.RegisterVehicle(controller);
            }
            else if (controller == null)
            {
                Debug.LogWarning($"[VehicleSpawner] Spawned '{instance.name}' has no VehicleController component.");
            }

            return controller;
        }

        /// <summary>
        /// Despawns (returns to pool, or destroys) a vehicle by its controller.
        /// </summary>
        public void DespawnVehicle(VehicleController controller)
        {
            if (controller == null) return;

            GameManager.Instance?.UnregisterVehicle(controller);

            if (usePooling && objectPooler != null)
            {
                controller.gameObject.SetActive(false);
            }
            else
            {
                Destroy(controller.gameObject);
            }
        }

        // ------------------------------------------------------------------ //
        //  Editor helper                                                       //
        // ------------------------------------------------------------------ //
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var sp in spawnPoints)
            {
                Gizmos.DrawWireSphere(sp.position, 1.0f);
                UnityEditor.Handles.Label(sp.position + Vector3.up * 1.5f, sp.vehicleType.ToString());
            }
        }
#endif
    }
}
