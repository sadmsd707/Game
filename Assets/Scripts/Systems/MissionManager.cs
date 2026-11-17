using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.Systems
{
    // ── Enums ─────────────────────────────────────────────────────────────────
    public enum MissionType
    {
        DeliverCargo,
        HarvestField,
        DigTrench,
        TowVehicle,
        MineQuarry
    }

    // ── MissionData ───────────────────────────────────────────────────────────
    [Serializable]
    public class MissionData
    {
        public string      missionId;
        public MissionType type;
        public string      description;
        public int         reward;
        public bool        isComplete;
        public Vector3     targetPosition;
        public VehicleType requiredVehicle;

        // Runtime state
        [NonSerialized] public bool isActive;
        [NonSerialized] public float progress;   // 0–1
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class MissionManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Condition Check")]
        [SerializeField] private float checkInterval = 1f;

        [Header("Completion Distance")]
        [SerializeField] private float deliveryRadius = 10f;

        [Header("Player")]
        [SerializeField] private Transform playerTransform;

        // ── Events ────────────────────────────────────────────────────────────
        public UnityEvent<MissionData> OnMissionStarted   = new UnityEvent<MissionData>();
        public UnityEvent<MissionData> OnMissionCompleted = new UnityEvent<MissionData>();
        public UnityEvent<MissionData> OnMissionFailed    = new UnityEvent<MissionData>();

        // ── State ─────────────────────────────────────────────────────────────
        public List<MissionData> availableMissions  = new List<MissionData>();
        public List<MissionData> completedMissions  = new List<MissionData>();

        private MissionData _currentMission;
        public  MissionData CurrentMission => _currentMission;

        // Singleton
        public static MissionManager Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (playerTransform == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTransform = go.transform;
            }

            PopulateStarterMissions();
        }

        private void Start()
        {
            StartCoroutine(ConditionCheckLoop());
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void StartMission(string missionId)
        {
            MissionData m = availableMissions.Find(x => x.missionId == missionId);
            if (m == null)
            {
                Debug.LogWarning($"[MissionManager] Mission '{missionId}' not found.");
                return;
            }
            if (m.isComplete)
            {
                Debug.Log($"[MissionManager] Mission '{missionId}' already completed.");
                return;
            }

            // Cancel any running mission
            if (_currentMission != null && _currentMission.isActive)
            {
                _currentMission.isActive = false;
                OnMissionFailed.Invoke(_currentMission);
            }

            _currentMission          = m;
            _currentMission.isActive = true;
            _currentMission.progress = 0f;

            OnMissionStarted.Invoke(_currentMission);
            Debug.Log($"[MissionManager] Started: {m.description}");
        }

        public void CompleteMission(string missionId)
        {
            MissionData m = availableMissions.Find(x => x.missionId == missionId);
            if (m == null) return;

            m.isComplete = true;
            m.isActive   = false;
            m.progress   = 1f;

            availableMissions.Remove(m);
            completedMissions.Add(m);

            if (_currentMission == m)
                _currentMission = null;

            // Award reward
            GameManager.Instance?.AddCurrency(m.reward);

            OnMissionCompleted.Invoke(m);
            Debug.Log($"[MissionManager] Completed: {m.description}  +{m.reward} coins");
        }

        /// <summary>Called every <see cref="checkInterval"/> seconds to test
        /// whether the active mission's objective is satisfied.</summary>
        public void CheckMissionCondition()
        {
            if (_currentMission == null || !_currentMission.isActive) return;

            switch (_currentMission.type)
            {
                case MissionType.DeliverCargo:
                case MissionType.TowVehicle:
                    // Complete when player (or vehicle) reaches target position
                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(playerTransform.position, _currentMission.targetPosition);
                        _currentMission.progress = Mathf.Clamp01(1f - dist / 500f);
                        if (dist <= deliveryRadius)
                            CompleteMission(_currentMission.missionId);
                    }
                    break;

                case MissionType.HarvestField:
                case MissionType.DigTrench:
                case MissionType.MineQuarry:
                    // Progress driven externally via SetMissionProgress()
                    if (_currentMission.progress >= 1f)
                        CompleteMission(_currentMission.missionId);
                    break;
            }
        }

        /// <summary>External systems (harvester, excavator, miner) call this
        /// to report incremental progress.</summary>
        public void SetMissionProgress(float progress)
        {
            if (_currentMission != null)
                _currentMission.progress = Mathf.Clamp01(progress);
        }

        // ── Condition check loop ──────────────────────────────────────────────

        private IEnumerator ConditionCheckLoop()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (true)
            {
                yield return wait;
                CheckMissionCondition();
            }
        }

        // ── Starter missions ──────────────────────────────────────────────────

        private void PopulateStarterMissions()
        {
            availableMissions.Clear();

            // 1. Truck – deliver cargo
            availableMissions.Add(new MissionData
            {
                missionId       = "MISSION_01_DELIVER",
                type            = MissionType.DeliverCargo,
                description     = "Deliver a load of gravel from the Quarry to the Construction Site.",
                reward          = 500,
                isComplete      = false,
                targetPosition  = new Vector3(800f, 0f, -800f),
                requiredVehicle = VehicleType.Truck,
            });

            // 2. Tractor – harvest the wheat field
            availableMissions.Add(new MissionData
            {
                missionId       = "MISSION_02_HARVEST",
                type            = MissionType.HarvestField,
                description     = "Use the harvester to collect wheat from Farmland sector 3.",
                reward          = 400,
                isComplete      = false,
                targetPosition  = new Vector3(-1000f, 0f, -1000f),
                requiredVehicle = VehicleType.Harvester,
            });

            // 3. JCB – dig a trench
            availableMissions.Add(new MissionData
            {
                missionId       = "MISSION_03_DIG",
                type            = MissionType.DigTrench,
                description     = "Dig the foundation trench at the new Village housing site.",
                reward          = 450,
                isComplete      = false,
                targetPosition  = new Vector3(-800f, 0f, 800f),
                requiredVehicle = VehicleType.JCB,
            });

            // 4. Car / Truck – tow a broken-down vehicle
            availableMissions.Add(new MissionData
            {
                missionId       = "MISSION_04_TOW",
                type            = MissionType.TowVehicle,
                description     = "Tow the broken Truck from the Highway to the nearest garage.",
                reward          = 350,
                isComplete      = false,
                targetPosition  = new Vector3(0f, 0f, 0f),
                requiredVehicle = VehicleType.Truck,
            });

            // 5. Excavator – mine the quarry
            availableMissions.Add(new MissionData
            {
                missionId       = "MISSION_05_MINE",
                type            = MissionType.MineQuarry,
                description     = "Extract 50 tonnes of rock from the Quarry east face.",
                reward          = 600,
                isComplete      = false,
                targetPosition  = new Vector3(1400f, 0f, 400f),
                requiredVehicle = VehicleType.Excavator,
            });
        }
    }
}
