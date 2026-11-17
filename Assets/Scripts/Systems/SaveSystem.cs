using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    // ── Serialisable helpers ──────────────────────────────────────────────

    [Serializable]
    public struct SerializableVector3
    {
        public float x, y, z;

        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x, y, z, w;

        public SerializableQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
    }

    [Serializable]
    public class VehicleState
    {
        public string               vehicleId;
        public float                fuelLevel;
        public float                damageLevel;
        public SerializableVector3  position;
        public SerializableQuaternion rotation;
    }

    [Serializable]
    public class SaveData
    {
        public SerializableVector3    playerPosition;
        public List<VehicleState>     vehicleStates  = new();
        public List<float>            fuelLevels     = new();
        public float                  damageLevel;
        public string                 currentGameMode;
        public float                  dayTime;
        public float                  totalPlayTime;
    }

    // ── Save system ───────────────────────────────────────────────────────

    /// <summary>
    /// JSON-based save / load system using Application.persistentDataPath.
    /// </summary>
    public static class SaveSystem
    {
        private static string SaveDir =>
            Path.Combine(Application.persistentDataPath, "saves");

        private static string SlotPath(string slot) =>
            Path.Combine(SaveDir, slot + ".json");

        // ── Public API ────────────────────────────────────────────────────
        public static void SaveGame(SaveData data, string slot = "slot1")
        {
            EnsureDirectoryExists();
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SlotPath(slot), json);
            Debug.Log($"[SaveSystem] Game saved to slot: {slot}");
        }

        public static SaveData LoadGame(string slot = "slot1")
        {
            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveSystem] No save found at slot: {slot}");
                return null;
            }
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public static void DeleteSave(string slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveSystem] Deleted save slot: {slot}");
            }
        }

        public static bool HasSave(string slot) =>
            File.Exists(SlotPath(slot));

        // ── World state helpers (used by WorldStateManager) ───────────────
        private static string WorldStatePath =>
            Path.Combine(SaveDir, "world_state.json");

        public static void SaveWorldState(string json)
        {
            EnsureDirectoryExists();
            File.WriteAllText(WorldStatePath, json);
            Debug.Log("[SaveSystem] World state saved.");
        }

        public static string LoadWorldState()
        {
            string path = WorldStatePath;
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }

        // ── Private helpers ───────────────────────────────────────────────
        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
        }
    }
}
