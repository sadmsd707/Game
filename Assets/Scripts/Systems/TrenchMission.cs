using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Mission: "Dig a trench along marked waypoints."
    ///
    /// Place a TerrainDeformer on the scene and assign it, together with
    /// Transform[] trenchWaypoints marking the waypoint positions along the
    /// intended trench line.  Call StartMission(jcb) to begin.
    ///
    /// The LineRenderer (auto-added) draws the trench path in green above ground.
    /// </summary>
    public class TrenchMission : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────
        public enum TrenchMissionState { Inactive, InProgress, Complete }

        [Header("State (read-only in Inspector)")]
        [SerializeField] private TrenchMissionState _state = TrenchMissionState.Inactive;
        public TrenchMissionState State => _state;

        // ── Waypoints ─────────────────────────────────────────────────────
        [Header("Waypoints")]
        [SerializeField] private Transform[] trenchWaypoints;

        [Tooltip("Required dig depth per waypoint (metres). Defaults to 1 m if array is shorter than waypoints.")]
        [SerializeField] private float[] waypointDepths;

        [HideInInspector] public bool[] waypointComplete;

        [SerializeField] private float completionThreshold = 0.5f;  // depth check tolerance
        [SerializeField] private float checkRadius         = 1.0f;  // radius for GetSoilVolumeAt

        // ── JCB ───────────────────────────────────────────────────────────
        [Header("JCB")]
        [SerializeField] private HeavyVehiclesWorld.Vehicles.JCBController assignedJCB;

        // ── Terrain ───────────────────────────────────────────────────────
        [Header("Terrain")]
        [SerializeField] private TerrainDeformer terrainDeformer;

        // ── UI ────────────────────────────────────────────────────────────
        [Header("UI")]
        [SerializeField] private TMP_Text missionText;      // "Trench: 3/8 segments done"

        // ── Events ────────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent OnMissionComplete;

        // ── LineRenderer ──────────────────────────────────────────────────
        private LineRenderer _lineRenderer;
        private const float  LINE_HEIGHT_OFFSET = 0.3f;   // metres above ground

        // ── Counts ────────────────────────────────────────────────────────
        private int _totalWaypoints;
        private int _completedCount;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            ConfigureLineRenderer();
        }

        private void Update()
        {
            if (_state != TrenchMissionState.InProgress) return;
            CheckWaypoints();
            UpdateMissionText();
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Begins the trench mission, assigning the given JCB.</summary>
        public void StartMission(HeavyVehiclesWorld.Vehicles.JCBController jcb)
        {
            if (_state == TrenchMissionState.InProgress) return;

            assignedJCB    = jcb;
            _totalWaypoints = trenchWaypoints != null ? trenchWaypoints.Length : 0;

            if (_totalWaypoints == 0)
            {
                Debug.LogWarning("[TrenchMission] No waypoints assigned – mission cannot start.");
                return;
            }

            waypointComplete = new bool[_totalWaypoints];
            _completedCount  = 0;
            _state           = TrenchMissionState.InProgress;

            DrawTrenchLine();
            UpdateMissionText();

            Debug.Log($"[TrenchMission] Started – {_totalWaypoints} segments to dig.");
        }

        /// <summary>Checks every incomplete waypoint and marks it done when dug deep enough.</summary>
        public void CheckWaypoints()
        {
            if (trenchWaypoints == null || terrainDeformer == null) return;

            for (int i = 0; i < _totalWaypoints; i++)
            {
                if (waypointComplete[i]) continue;
                if (trenchWaypoints[i] == null) continue;

                float requiredDepth = GetRequiredDepth(i);
                float soilAtPoint   = terrainDeformer.GetSoilVolumeAt(
                    trenchWaypoints[i].position, checkRadius);

                // soilVolumeAt represents cumulative depth dug at that spot
                if (soilAtPoint >= requiredDepth - completionThreshold)
                {
                    waypointComplete[i] = true;
                    _completedCount++;
                    Debug.Log($"[TrenchMission] Waypoint {i + 1}/{_totalWaypoints} complete.");
                }
            }

            if (_completedCount >= _totalWaypoints)
                CompleteMission();
        }

        /// <summary>Resets the mission to Inactive state.</summary>
        public void ResetMission()
        {
            _state          = TrenchMissionState.Inactive;
            waypointComplete = new bool[_totalWaypoints];
            _completedCount  = 0;
            assignedJCB      = null;

            if (_lineRenderer != null) _lineRenderer.positionCount = 0;

            UpdateMissionText();
            Debug.Log("[TrenchMission] Reset.");
        }

        // ── Private ───────────────────────────────────────────────────────
        private void CompleteMission()
        {
            _state = TrenchMissionState.Complete;

            if (missionText != null)
                missionText.text = $"Trench: COMPLETE! ({_totalWaypoints}/{_totalWaypoints})";

            OnMissionComplete?.Invoke();
            Debug.Log("[TrenchMission] All segments complete – mission done!");
        }

        private void UpdateMissionText()
        {
            if (missionText == null) return;

            switch (_state)
            {
                case TrenchMissionState.Inactive:
                    missionText.text = "Trench: Waiting to start";
                    break;
                case TrenchMissionState.InProgress:
                    missionText.text = $"Trench: {_completedCount}/{_totalWaypoints} segments done";
                    break;
                case TrenchMissionState.Complete:
                    missionText.text = $"Trench: COMPLETE! ({_totalWaypoints}/{_totalWaypoints})";
                    break;
            }
        }

        private float GetRequiredDepth(int index)
        {
            if (waypointDepths != null && index < waypointDepths.Length)
                return waypointDepths[index];
            return 1f; // default 1 metre
        }

        // ── Line Renderer ─────────────────────────────────────────────────
        private void DrawTrenchLine()
        {
            if (_lineRenderer == null || trenchWaypoints == null) return;

            _lineRenderer.positionCount = _totalWaypoints;

            for (int i = 0; i < _totalWaypoints; i++)
            {
                if (trenchWaypoints[i] == null) continue;

                Vector3 pos = trenchWaypoints[i].position;
                pos.y = GetTerrainY(pos) + LINE_HEIGHT_OFFSET;
                _lineRenderer.SetPosition(i, pos);
            }
        }

        private void ConfigureLineRenderer()
        {
            _lineRenderer.startWidth      = 0.15f;
            _lineRenderer.endWidth        = 0.15f;
            _lineRenderer.startColor      = Color.green;
            _lineRenderer.endColor        = Color.green;
            _lineRenderer.useWorldSpace   = true;
            _lineRenderer.positionCount   = 0;

            // Use a simple unlit material if none assigned
            if (_lineRenderer.sharedMaterial == null)
                _lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private float GetTerrainY(Vector3 worldPos)
        {
            if (Terrain.activeTerrain == null) return worldPos.y;
            return Terrain.activeTerrain.SampleHeight(worldPos)
                 + Terrain.activeTerrain.transform.position.y;
        }

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (trenchWaypoints == null) return;

            for (int i = 0; i < trenchWaypoints.Length; i++)
            {
                if (trenchWaypoints[i] == null) continue;

                bool done = waypointComplete != null
                         && i < waypointComplete.Length
                         && waypointComplete[i];

                Gizmos.color = done ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(trenchWaypoints[i].position, checkRadius);

                if (i < trenchWaypoints.Length - 1 && trenchWaypoints[i + 1] != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(trenchWaypoints[i].position, trenchWaypoints[i + 1].position);
                }
            }
        }
    }
}
