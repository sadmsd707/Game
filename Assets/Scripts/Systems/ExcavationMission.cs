using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Mission: "Excavate a pit 10 m × 10 m × 2 m deep".
    /// Samples a 5×5 grid of terrain heights within the target pit area every
    /// 2 seconds and reports completion percentage.
    /// </summary>
    public class ExcavationMission : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────
        [Header("Pit Definition")]
        [SerializeField] private Transform pitCentre;
        [SerializeField] private Vector3   pitSize = new Vector3(10f, 2f, 10f);  // X, Depth, Z

        [Header("References")]
        [SerializeField] private ExcavatorController assignedExcavator;
        [SerializeField] private TerrainDeformer     terrainDeformer;
        [SerializeField] private TMP_Text            progressText;    // "Pit: 42% complete"

        [Header("Events")]
        public UnityEvent OnMissionComplete;

        // ── State machine ─────────────────────────────────────────────
        public enum ExcavationMissionState { Inactive, Digging, Complete }

        [Header("Debug (read-only)")]
        [SerializeField] private ExcavationMissionState missionState = ExcavationMissionState.Inactive;
        [SerializeField] [Range(0f, 1f)] private float completionPercent;

        // ── Internal ──────────────────────────────────────────────────
        private const int GridRes      = 5;          // 5×5 sample grid
        private const float MeasureInterval = 2f;

        private float[,] _originalHeights;           // recorded at StartMission
        private float    _measureTimer;
        private Terrain  _terrain;

        // ── Public API ────────────────────────────────────────────────
        public void StartMission(ExcavatorController ex)
        {
            assignedExcavator = ex;

            // Find the Unity Terrain once
            _terrain = Terrain.activeTerrain;
            if (_terrain == null)
            {
                Debug.LogWarning("[ExcavationMission] No active Terrain found.");
                return;
            }

            if (terrainDeformer == null)
                terrainDeformer = FindObjectOfType<TerrainDeformer>();

            RecordOriginalHeights();
            completionPercent = 0f;
            missionState      = ExcavationMissionState.Digging;
            _measureTimer     = MeasureInterval;   // measure immediately on first Update

            if (progressText != null)
                progressText.text = "Pit: 0% complete";

            Debug.Log("[ExcavationMission] Mission started – Excavate 10 m × 10 m × 2 m pit.");
        }

        public void ResetMission()
        {
            missionState      = ExcavationMissionState.Inactive;
            completionPercent = 0f;
            _originalHeights  = null;

            if (progressText != null)
                progressText.text = "Pit: –";
        }

        // ── Unity lifecycle ───────────────────────────────────────────
        private void Update()
        {
            if (missionState != ExcavationMissionState.Digging) return;

            _measureTimer -= Time.deltaTime;
            if (_measureTimer <= 0f)
            {
                _measureTimer = MeasureInterval;
                MeasurePitDepth();
            }
        }

        // ── Measurement ───────────────────────────────────────────────
        private void RecordOriginalHeights()
        {
            if (_terrain == null || pitCentre == null) return;

            _originalHeights = new float[GridRes, GridRes];
            Vector3 centre   = pitCentre.position;

            for (int row = 0; row < GridRes; row++)
            {
                for (int col = 0; col < GridRes; col++)
                {
                    Vector3 samplePos = GetSampleWorldPos(centre, row, col);
                    _originalHeights[row, col] = _terrain.SampleHeight(samplePos);
                }
            }
        }

        private void MeasurePitDepth()
        {
            if (_terrain == null || _originalHeights == null || pitCentre == null) return;

            Vector3 centre       = pitCentre.position;
            float   targetDepth  = pitSize.y;          // 2 m
            float   totalScore   = 0f;
            int     sampleCount  = GridRes * GridRes;

            for (int row = 0; row < GridRes; row++)
            {
                for (int col = 0; col < GridRes; col++)
                {
                    Vector3 samplePos    = GetSampleWorldPos(centre, row, col);
                    float   currentH     = _terrain.SampleHeight(samplePos);
                    float   originalH    = _originalHeights[row, col];
                    float   dug          = Mathf.Max(0f, originalH - currentH);
                    float   sample01     = Mathf.Clamp01(dug / targetDepth);
                    totalScore          += sample01;
                }
            }

            completionPercent = totalScore / sampleCount;   // 0–1

            int pct = Mathf.RoundToInt(completionPercent * 100f);
            if (progressText != null)
                progressText.text = $"Pit: {pct}% complete";

            Debug.Log($"[ExcavationMission] Pit depth measured – {pct}% complete.");

            if (completionPercent >= 0.95f)
                CompleteMission();
        }

        private Vector3 GetSampleWorldPos(Vector3 centre, int row, int col)
        {
            float xHalf  = pitSize.x * 0.5f;
            float zHalf  = pitSize.z * 0.5f;

            float tRow   = GridRes > 1 ? (float)row / (GridRes - 1) : 0.5f;
            float tCol   = GridRes > 1 ? (float)col / (GridRes - 1) : 0.5f;

            float x = Mathf.Lerp(centre.x - xHalf, centre.x + xHalf, tCol);
            float z = Mathf.Lerp(centre.z - zHalf, centre.z + zHalf, tRow);

            return new Vector3(x, centre.y + 50f, z);   // high Y so SampleHeight works
        }

        private void CompleteMission()
        {
            missionState = ExcavationMissionState.Complete;
            completionPercent = 1f;

            if (progressText != null)
                progressText.text = "Pit: 100% complete! ✓";

            Debug.Log("[ExcavationMission] Mission COMPLETE – pit fully excavated!");
            OnMissionComplete?.Invoke();
        }

        // ── Editor gizmo ─────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (pitCentre == null) return;

            Gizmos.color = Color.yellow;
            Vector3 gizmoCenter = pitCentre.position - new Vector3(0f, pitSize.y * 0.5f, 0f);
            Gizmos.DrawWireCube(gizmoCenter, pitSize);
        }
    }
}
