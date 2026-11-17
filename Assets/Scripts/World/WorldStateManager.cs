using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.Systems;    // for SaveSystem static class

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// WorldStateManager – singleton that tracks:
    ///   • Which missions are complete
    ///   • Last known positions of each registered vehicle
    ///   • Field harvest progress summary
    /// Integrates with SaveSystem for persistence.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        public static WorldStateManager Instance { get; private set; }

        // ── Mission state ──────────────────────────────────────────────────────────
        private readonly Dictionary<string, bool> _missionComplete = new Dictionary<string, bool>();

        public bool IsMissionComplete(string missionId)
        {
            return _missionComplete.TryGetValue(missionId, out bool v) && v;
        }

        public void SetMissionComplete(string missionId, bool complete = true)
        {
            _missionComplete[missionId] = complete;
            Debug.Log($"[WorldStateManager] Mission '{missionId}' complete={complete}");
        }

        public IReadOnlyDictionary<string, bool> AllMissions => _missionComplete;

        // ── Vehicle last positions ─────────────────────────────────────────────────
        private readonly Dictionary<string, Vector3> _vehicleLastPos = new Dictionary<string, Vector3>();

        public void RecordVehiclePosition(string vehicleId, Vector3 worldPos)
        {
            _vehicleLastPos[vehicleId] = worldPos;
        }

        public bool TryGetVehicleLastPos(string vehicleId, out Vector3 pos)
        {
            return _vehicleLastPos.TryGetValue(vehicleId, out pos);
        }

        public IReadOnlyDictionary<string, Vector3> AllVehiclePositions => _vehicleLastPos;

        // ── Field harvest summary ──────────────────────────────────────────────────
        private readonly Dictionary<string, float> _fieldProgress = new Dictionary<string, float>(); // 0-1

        public void SetFieldProgress(string fieldId, float progress01)
        {
            _fieldProgress[fieldId] = Mathf.Clamp01(progress01);
        }

        public float GetFieldProgress(string fieldId)
        {
            return _fieldProgress.TryGetValue(fieldId, out float v) ? v : 0f;
        }

        // ── Save / Load integration ────────────────────────────────────────────────

        [System.Serializable]
        private class WorldSaveData
        {
            public List<string> missionIds    = new List<string>();
            public List<bool>   missionDone   = new List<bool>();
            public List<string> vehicleIds    = new List<string>();
            public List<float>  vehiclePosX   = new List<float>();
            public List<float>  vehiclePosY   = new List<float>();
            public List<float>  vehiclePosZ   = new List<float>();
            public List<string> fieldIds      = new List<string>();
            public List<float>  fieldProgress = new List<float>();
        }

        public string SerializeToJson()
        {
            var data = new WorldSaveData();

            foreach (var kvp in _missionComplete)
            {
                data.missionIds.Add(kvp.Key);
                data.missionDone.Add(kvp.Value);
            }

            foreach (var kvp in _vehicleLastPos)
            {
                data.vehicleIds.Add(kvp.Key);
                data.vehiclePosX.Add(kvp.Value.x);
                data.vehiclePosY.Add(kvp.Value.y);
                data.vehiclePosZ.Add(kvp.Value.z);
            }

            foreach (var kvp in _fieldProgress)
            {
                data.fieldIds.Add(kvp.Key);
                data.fieldProgress.Add(kvp.Value);
            }

            return JsonUtility.ToJson(data, true);
        }

        public void DeserializeFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<WorldSaveData>(json);
            if (data == null) return;

            _missionComplete.Clear();
            for (int i = 0; i < data.missionIds.Count; i++)
                _missionComplete[data.missionIds[i]] = data.missionDone[i];

            _vehicleLastPos.Clear();
            for (int i = 0; i < data.vehicleIds.Count; i++)
                _vehicleLastPos[data.vehicleIds[i]] = new Vector3(
                    data.vehiclePosX[i], data.vehiclePosY[i], data.vehiclePosZ[i]);

            _fieldProgress.Clear();
            for (int i = 0; i < data.fieldIds.Count; i++)
                _fieldProgress[data.fieldIds[i]] = data.fieldProgress[i];
        }

        // ── Singleton lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            // Auto-save world state on quit via SaveSystem (static class)
            SaveSystem.SaveWorldState(SerializeToJson());
        }
    }
}
