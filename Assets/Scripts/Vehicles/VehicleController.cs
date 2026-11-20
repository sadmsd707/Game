using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.Vehicles
{
    public enum VehicleType
    {
        Car,
        Truck,
        Tractor,
        JCB,
        Excavator,
        Harvester
    }

    public enum GearState
    {
        Reverse,
        Neutral,
        Drive
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public abstract class VehicleController : MonoBehaviour
    {
        // ── Vehicle identity ──────────────────────────────────────────────
        [Header("Vehicle Settings")]
        public VehicleType vehicleType;

        // ── State ─────────────────────────────────────────────────────────
        [Header("State")]
        public bool engineRunning;
        public GearState currentGear = GearState.Neutral;
        [Range(0f, 100f)] public float fuelLevel = 100f;
        [Range(0f, 100f)] public float damageLevel = 0f;
        public bool isOccupied;
        public float currentSpeed;
        public float engineRPM;
        public float maxRPM = 6000f;
        public float throttleInput;
        public float brakeInput;

        // ── HUD Convenience Properties ─────────────────────────────────────
        public float CurrentFuel => fuelLevel;
        public float MaxFuel => 100f;
        public GearState CurrentGear => currentGear;
        public string VehicleName => !string.IsNullOrEmpty(name) ? name : vehicleType.ToString();
        public float MaxRPM => maxRPM;
        public float CurrentRPM => engineRPM;
        public float MaxHealth => 100f;
        public float CurrentHealth => Mathf.Max(0f, 100f - damageLevel);
        public VehicleType Type => vehicleType;
        public bool isEngineOn => engineRunning;
        /// <summary>True when the vehicle is actively braking (brakeInput > 0.1).</summary>
        public bool IsBraking => brakeInput > 0.1f;
        public float SpeedKmh => currentSpeed * 3.6f;

        // ── Physics ───────────────────────────────────────────────────────
        [Header("Wheel Colliders")]
        public WheelCollider[] wheelColliders;

        [HideInInspector] [SerializeField] private Rigidbody _rb;
        public Rigidbody rb
        {
            get
            {
                if (_rb == null) _rb = GetComponent<Rigidbody>();
                return _rb;
            }
            set { _rb = value; }
        }

        // ── Audio ─────────────────────────────────────────────────────────
        [Header("Audio")]
        public AudioSource engineAudioSource;

        // ── Interaction ───────────────────────────────────────────────────
        [Header("Interaction")]
        public float EnterRadius = 4f;

        // ── Events ────────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent<VehicleController> OnVehicleEntered;
        public UnityEvent<VehicleController> OnVehicleExited;

        // ── Lifecycle ─────────────────────────────────────────────────────
        protected virtual void Awake()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (engineAudioSource == null) engineAudioSource = GetComponent<AudioSource>();
        }

        protected virtual void Start()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (engineAudioSource == null)
                engineAudioSource = GetComponent<AudioSource>();

            // Lower the centre of mass to improve stability
            if (_rb != null) _rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        }

        protected virtual void Update()
        {
            if (engineRunning)
                UpdateEngineSound();
        }

        // ── Abstract interface ────────────────────────────────────────────
        public abstract void ApplyDriving();
        public abstract void ApplySteering();
        public abstract void UpdateEngineSound();
        public abstract string GetVehicleInfo();

        // ── Virtual methods ───────────────────────────────────────────────
        public virtual void EnterVehicle(PlayerController player)
        {
            if (isOccupied) return;
            isOccupied = true;
            OnVehicleEntered?.Invoke(this);
        }

        public virtual void ExitVehicle()
        {
            isOccupied = false;
            OnVehicleExited?.Invoke(this);
        }

        public virtual void ToggleEngine()
        {
            if (fuelLevel <= 0f) return;
            engineRunning = !engineRunning;
            if (!engineRunning && engineAudioSource != null)
                engineAudioSource.Stop();
        }

        public virtual void ApplyBrake(float brakeForce)
        {
            foreach (var wc in wheelColliders)
                wc.brakeTorque = brakeForce;
        }

        public virtual void SetGear(GearState gear)
        {
            currentGear = gear;
        }

        // ── Helper ────────────────────────────────────────────────────────
        /// <summary>Syncs a wheel mesh transform to its WheelCollider pose.</summary>
        protected void UpdateWheelMeshes(WheelCollider wc, Transform wheelMesh)
        {
            if (wc == null || wheelMesh == null) return;
            wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheelMesh.position = pos;
            wheelMesh.rotation = rot;
        }

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawSphere(transform.position, EnterRadius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawWireSphere(transform.position, EnterRadius);
        }
    }
}
