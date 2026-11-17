using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Singleton that manages ALL CropField instances in the scene for mission
    /// tracking and collective operations (reset, nearest-field query).
    /// </summary>
    public class FieldManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        private static FieldManager _instance;
        public static FieldManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<FieldManager>();
                return _instance;
            }
        }

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Registered Fields")]
        [SerializeField] private List<CropField> fields = new List<CropField>();

        // ── Properties ────────────────────────────────────────────────────
        /// <summary>Average harvest progress (0–1) across all registered fields.</summary>
        public float TotalHarvestProgress
        {
            get
            {
                if (fields == null || fields.Count == 0) return 0f;
                float sum = 0f;
                int   valid = 0;
                foreach (CropField f in fields)
                {
                    if (f == null) continue;
                    sum += f.HarvestProgress;
                    valid++;
                }
                return valid > 0 ? sum / valid : 0f;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Auto-register any fields already in the scene
            CropField[] sceneFIelds = FindObjectsOfType<CropField>();
            foreach (CropField f in sceneFIelds)
                RegisterField(f);
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Adds a CropField to the managed list (safe to call multiple times).</summary>
        public void RegisterField(CropField f)
        {
            if (f == null || fields.Contains(f)) return;
            fields.Add(f);
        }

        /// <summary>Calls RegrowField() on every registered field (use between missions).</summary>
        public void ResetAllFields()
        {
            foreach (CropField f in fields)
            {
                if (f == null) continue;
                f.RegrowField(2); // regrow to mature stage
            }
        }

        /// <summary>Returns the CropField whose transform is nearest to <paramref name="pos"/>.</summary>
        public CropField GetFieldAt(Vector3 pos)
        {
            CropField nearest  = null;
            float     bestDist = float.MaxValue;

            foreach (CropField f in fields)
            {
                if (f == null) continue;
                float d = Vector3.SqrMagnitude(f.transform.position - pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest  = f;
                }
            }
            return nearest;
        }

        /// <summary>Returns all registered fields (read-only copy).</summary>
        public IReadOnlyList<CropField> GetAllFields() => fields.AsReadOnly();

        private void OnDrawGizmosSelected()
        {
            if (fields == null) return;
            Gizmos.color = Color.cyan;
            foreach (CropField f in fields)
            {
                if (f == null) continue;
                Gizmos.DrawLine(transform.position, f.transform.position);
                Gizmos.DrawWireSphere(f.transform.position, 1f);
            }
        }
    }
}
