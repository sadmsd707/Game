using UnityEngine;
using UnityEngine.Events;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Self-contained mission: "Harvest the wheat field and deliver to silo."
    ///
    /// State machine:
    ///   Inactive → (StartMission) → Harvesting → (90 % of field cut) →
    ///   Delivering → (enough grain at silo) → Complete
    /// </summary>
    public class HarvestMission : MonoBehaviour
    {
        // ── State enum ────────────────────────────────────────────────────
        public enum HarvestMissionState
        {
            Inactive,
            Harvesting,
            Delivering,
            Complete
        }

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Mission Setup")]
        [SerializeField] private CropField          targetField;
        [SerializeField] private GrainDeliveryPoint deliveryPoint;
        [SerializeField] [Range(0f, 1f)]
        private float requiredHarvestPercent = 0.9f;   // 90 % of field

        [Header("Grain Requirement")]
        [SerializeField] private float grainRequired = 500f;   // units to deliver at silo
        public  float grainDelivered;                           // updated via event

        [Header("UI")]
        [SerializeField] private TMP_Text missionStatusText;

        [Header("Events")]
        public UnityEvent OnMissionComplete = new UnityEvent();

        // ── Runtime state ─────────────────────────────────────────────────
        [SerializeField] private HarvestMissionState _state = HarvestMissionState.Inactive;
        public HarvestMissionState State => _state;

        private HarvesterController _assignedHarvester;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (deliveryPoint != null)
                deliveryPoint.OnGrainReceived.AddListener(OnGrainReceived);
        }

        private void OnDisable()
        {
            if (deliveryPoint != null)
                deliveryPoint.OnGrainReceived.RemoveListener(OnGrainReceived);
        }

        private void Update()
        {
            switch (_state)
            {
                case HarvestMissionState.Harvesting:
                    UpdateHarvestingState();
                    break;

                case HarvestMissionState.Delivering:
                    UpdateDeliveringState();
                    break;
            }

            RefreshStatusText();
        }

        // ── State updates ─────────────────────────────────────────────────
        private void UpdateHarvestingState()
        {
            if (targetField == null) return;
            if (targetField.HarvestProgress >= requiredHarvestPercent)
            {
                _state = HarvestMissionState.Delivering;
                Debug.Log("[HarvestMission] Field sufficiently harvested – now deliver to silo.");
            }
        }

        private void UpdateDeliveringState()
        {
            if (grainDelivered >= grainRequired)
                CompleteMission();
        }

        // ── Event handlers ────────────────────────────────────────────────
        private void OnGrainReceived(float amount)
        {
            if (_state != HarvestMissionState.Delivering) return;
            grainDelivered += amount;
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Start or restart the mission with the given harvester.</summary>
        public void StartMission(HarvesterController harvester)
        {
            _assignedHarvester = harvester;
            grainDelivered     = 0f;

            if (deliveryPoint != null)
                deliveryPoint.ResetDeliveryPoint();

            _state = HarvestMissionState.Harvesting;
            Debug.Log("[HarvestMission] Mission started – harvest the field!");
        }

        /// <summary>Resets the mission to Inactive and optionally resets the field.</summary>
        public void ResetMission()
        {
            _state             = HarvestMissionState.Inactive;
            grainDelivered     = 0f;
            _assignedHarvester = null;

            if (deliveryPoint != null)
                deliveryPoint.ResetDeliveryPoint();

            if (targetField != null)
                targetField.RegrowField(2);

            RefreshStatusText();
        }

        // ── Internal ──────────────────────────────────────────────────────
        private void CompleteMission()
        {
            _state = HarvestMissionState.Complete;
            Debug.Log("[HarvestMission] Mission COMPLETE!");
            OnMissionComplete.Invoke();
        }

        private void RefreshStatusText()
        {
            if (missionStatusText == null) return;

            switch (_state)
            {
                case HarvestMissionState.Inactive:
                    missionStatusText.text = "Mission: Inactive";
                    break;

                case HarvestMissionState.Harvesting:
                    float fieldPct = targetField != null
                        ? targetField.HarvestProgress * 100f
                        : 0f;
                    float required = requiredHarvestPercent * 100f;
                    missionStatusText.text =
                        $"Harvest Field: {fieldPct:F0}% / {required:F0}%";
                    break;

                case HarvestMissionState.Delivering:
                    missionStatusText.text =
                        $"Deliver Grain: {grainDelivered:F0} / {grainRequired:F0} units";
                    break;

                case HarvestMissionState.Complete:
                    missionStatusText.text = "✓ Mission Complete!";
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (targetField != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, targetField.transform.position);
            }
            if (deliveryPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, deliveryPoint.transform.position);
            }
        }
    }
}
