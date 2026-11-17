using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Scene-wide crop manager. Holds references to all CropFields and
    /// routes cut requests from the HarvesterController to the right field.
    /// </summary>
    public class CropManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        public static CropManager Instance { get; private set; }

        // ── Fields registry ───────────────────────────────────────────────
        [SerializeField] private List<CropField> cropFields = new List<CropField>();

        // ── Harvest stats ─────────────────────────────────────────────────
        public float TotalHarvestProgress
        {
            get
            {
                if (cropFields.Count == 0) return 0f;
                float sum = 0f;
                foreach (var f in cropFields) sum += f.HarvestProgress;
                return sum / cropFields.Count;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Registration ──────────────────────────────────────────────────
        public void RegisterField(CropField field)
        {
            if (!cropFields.Contains(field))
                cropFields.Add(field);
        }

        public void UnregisterField(CropField field)
        {
            cropFields.Remove(field);
        }

        // ── Cut API ───────────────────────────────────────────────────────
        /// <summary>
        /// Called by HarvesterController each frame while cutting.
        /// Routes to every field that overlaps the area.
        /// Returns total crops cut this call.
        /// </summary>
        public int CutCropsInArea(Bounds area)
        {
            int total = 0;
            foreach (var field in cropFields)
            {
                if (field == null) continue;
                total += field.CutCropsInArea(area);
            }
            return total;
        }

        // ── Regrow ────────────────────────────────────────────────────────
        public void RegrowCrops()
        {
            foreach (var field in cropFields)
                field?.RegrowField();
        }
    }
}
