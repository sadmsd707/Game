using System.Collections.Generic;
using System.IO;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;
using HeavyVehiclesWorld.World;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// FullSaveLoadManager – the single save/load entry point that ties together:
    ///   • Player position and rotation
    ///   • All vehicle fuel + damage + position + rotation
    ///   • Current day time (from DayNightCycle)
    ///   • Active / completed mission states
    ///   • Field harvest progress (from WorldStateManager)
    ///   • Graphics/FPS settings (delegated to GraphicsSettingsManager)
    ///
    /// Uses three files in Application.persistentDataPath/saves/:
    ///   slot1.json  – game data (player/vehicles/time)
    ///   world_state.json – mission + field progress (via WorldStateManager)
    ///   settings.json – already handled by GraphicsSettingsManager/PlayerPrefs
    /// </summary>
    public class FullSaveLoadManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────────
        public static FullSaveLoadManager Instance { get; private set; }

        // ── Inspector refs ─────────────────────────────────────────────────────────
        [Header("Scene References")]
        [SerializeField] private Transform       playerTransform;
        [SerializeField] private VehicleController[] allVehicles;

        [Header("World")]
        [SerializeField] private World.DayNightCycle dayNight;

        [Header("Auto-Save")]
        [SerializeField] private bool autoSaveEnabled   = true;
        [SerializeField] private float autoSaveInterval = 300f;   // 5 minutes

        // ── State ──────────────────────────────────────────────────────────────────
        private float _autoSaveTimer;
        public  bool  IsSaving  { get; private set; }
        public  bool  IsLoading { get; private set; }

        // ── Save data structure ────────────────────────────────────────────────────

        [System.Serializable]
        private class FullSaveData
        {
            // Player
            public float px, py, pz;
            public float prx, pry, prz, prw;

            // Day time
            public float dayTime;
            public float totalPlayTime;

            // Vehicles
            public List<VehicleSaveEntry> vehicles = new List<VehicleSaveEntry>();
        }

        [System.Serializable]
        private class VehicleSaveEntry
        {
            public string vehicleId;
            public float  fuel;
            public float  damage;
            public float  px, py, pz;
            public float  rx, ry, rz, rw;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Auto-find references if not set
            if (playerTransform == null)
            {
                var go = GameObject.FindWithTag("Player");
                if (go != null) playerTransform = go.transform;
            }

            if (dayNight == null) dayNight = FindObjectOfType<World.DayNightCycle>();
        }

        private void Update()
        {
            if (!autoSaveEnabled) return;
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                SaveAll("slot1");
                Debug.Log("[FullSaveLoadManager] Auto-saved.");
            }
        }

        private void OnApplicationQuit()
        {
            SaveAll("slot1");
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Save everything to the given slot name.</summary>
        public void SaveAll(string slot = "slot1")
        {
            if (IsSaving) return;
            IsSaving = true;

            var data = new FullSaveData();

            // Player position/rotation
            if (playerTransform != null)
            {
                data.px = playerTransform.position.x;
                data.py = playerTransform.position.y;
                data.pz = playerTransform.position.z;
                data.prx = playerTransform.rotation.x;
                data.pry = playerTransform.rotation.y;
                data.prz = playerTransform.rotation.z;
                data.prw = playerTransform.rotation.w;
            }

            // Day time
            data.dayTime = dayNight != null ? dayNight.TimeOfDay : 6f;
            data.totalPlayTime = Time.realtimeSinceStartup;

            // Vehicles
            if (allVehicles != null)
            {
                foreach (var v in allVehicles)
                {
                    if (v == null) continue;
                    data.vehicles.Add(new VehicleSaveEntry
                    {
                        vehicleId = v.name,
                        fuel      = v.fuelLevel,
                        damage    = v.damageLevel,
                        px = v.transform.position.x,
                        py = v.transform.position.y,
                        pz = v.transform.position.z,
                        rx = v.transform.rotation.x,
                        ry = v.transform.rotation.y,
                        rz = v.transform.rotation.z,
                        rw = v.transform.rotation.w,
                    });

                    // Also record in WorldStateManager
                    WorldStateManager.Instance?.RecordVehiclePosition(v.name, v.transform.position);
                }
            }

            // Write game data JSON
            string json  = JsonUtility.ToJson(data, true);
            string dir   = Path.Combine(Application.persistentDataPath, "saves");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, slot + ".json"), json);

            // Save world state (missions + fields)
            WorldStateManager.Instance?.SerializeToJson();
            if (WorldStateManager.Instance != null)
                SaveSystem.SaveWorldState(WorldStateManager.Instance.SerializeToJson());

            // Save mission states
            if (MissionManager.Instance != null)
            {
                foreach (var m in MissionManager.Instance.completedMissions)
                    WorldStateManager.Instance?.SetMissionComplete(m.missionId, true);
            }

            IsSaving = false;
            Debug.Log($"[FullSaveLoadManager] Saved to slot '{slot}'.");
        }

        /// <summary>Load everything from the given slot name.</summary>
        public bool LoadAll(string slot = "slot1")
        {
            string path = Path.Combine(Application.persistentDataPath, "saves", slot + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[FullSaveLoadManager] No save found at slot '{slot}'.");
                return false;
            }

            IsLoading = true;

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<FullSaveData>(json);

            if (data == null)
            {
                IsLoading = false;
                return false;
            }

            // Restore player
            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(data.px, data.py, data.pz);
                playerTransform.rotation = new Quaternion(data.prx, data.pry, data.prz, data.prw);
            }

            // Restore day time
            if (dayNight != null) dayNight.timeOfDay = data.dayTime;

            // Restore vehicles
            if (allVehicles != null)
            {
                foreach (var entry in data.vehicles)
                {
                    var v = System.Array.Find(allVehicles, x => x != null && x.name == entry.vehicleId);
                    if (v == null) continue;

                    v.fuelLevel   = entry.fuel;
                    v.damageLevel = entry.damage;
                    v.transform.position = new Vector3(entry.px, entry.py, entry.pz);
                    v.transform.rotation = new Quaternion(entry.rx, entry.ry, entry.rz, entry.rw);
                }
            }

            // Restore world state
            string worldJson = SaveSystem.LoadWorldState();
            if (worldJson != null)
                WorldStateManager.Instance?.DeserializeFromJson(worldJson);

            IsLoading = false;
            Debug.Log($"[FullSaveLoadManager] Loaded slot '{slot}' successfully.");
            return true;
        }

        /// <summary>Returns true if a save file exists for the given slot.</summary>
        public bool HasSave(string slot = "slot1")
            => File.Exists(Path.Combine(Application.persistentDataPath, "saves", slot + ".json"));

        /// <summary>Delete a save slot.</summary>
        public void DeleteSave(string slot = "slot1")
        {
            string path = Path.Combine(Application.persistentDataPath, "saves", slot + ".json");
            if (File.Exists(path)) File.Delete(path);
            Debug.Log($"[FullSaveLoadManager] Deleted slot '{slot}'.");
        }
    }
}
