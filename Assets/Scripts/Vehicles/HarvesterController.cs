using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Combine Harvester controller.
    /// Inherits VehicleController.
    ///
    /// Drive        : 4 WheelColliders, slow max speed (15 km/h).
    /// Header       : Raises / lowers cutting bar; triggers CropManager on active cut.
    /// Grain tank   : Accumulates grainLevel 0-100.
    /// Unload auger : Swings out and streams grain to trailers / bunkers.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class HarvesterController : VehicleController
    {
        // ── Drive ──────────────────────────────────────────────────────────
        [Header("Harvester – Drive")]
        [SerializeField] public WheelCollider wheelFL;
        [SerializeField] public WheelCollider wheelFR;
        [SerializeField] public WheelCollider wheelRL;
        [SerializeField] public WheelCollider wheelRR;

        [SerializeField] public Transform meshFL;
        [SerializeField] public Transform meshFR;
        [SerializeField] public Transform meshRL;
        [SerializeField] public Transform meshRR;

        [SerializeField] private float motorTorque   = 6000f;
        [SerializeField] private float maxSteerAngle = 25f;
        [SerializeField] private float topSpeedKmh   = 20f;

        // ── Header ─────────────────────────────────────────────────────────
        [Header("Harvester – Header (Cutting Bar)")]
        [SerializeField] public Transform headerTransform;
        [SerializeField] private Vector3   headerLoweredLocalPos   = new Vector3(0f, -0.8f, 2.2f);
        [SerializeField] private Vector3   headerRaisedLocalPos    = new Vector3(0f,  0.6f, 2.2f);
        [SerializeField] private float     headerMoveSpeed         = 1.0f;  // m/s

        public bool headerLowered { get; private set; }

        // ── Cutting ────────────────────────────────────────────────────────
        [Header("Harvester – Cutting System")]
        [SerializeField] private Collider headerTriggerZone;    // set IsTrigger=true in Editor
        [SerializeField] private CropManager cropManager;

        [Tooltip("Width × height × depth of the cut bounds relative to header position.")]
        [SerializeField] private Vector3 cutBoundsSize = new Vector3(5f, 1f, 1f);

        [SerializeField] private ParticleSystem cropDustParticles;
        [SerializeField] private ParticleSystem chaffParticles;         // blows out the back

        public bool isCutting { get; private set; }

        // ── Grain tank ─────────────────────────────────────────────────────
        [Header("Harvester – Grain Tank")]
        [Range(0f, 100f)]
        public float grainLevel;
        public bool tankFull => grainLevel >= 100f;

        [SerializeField] private float grainFillRate = 2f;   // units per second while cutting

        // ── Unload auger ───────────────────────────────────────────────────
        [Header("Harvester – Unload Auger")]
        [SerializeField] public Transform augerTransform;
        [SerializeField] private Vector3   augerExtendedLocalRot = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3   augerRetractedLocalRot = new Vector3(0f, 0f, 0f);
        [SerializeField] private float     augerRotateSpeed      = 60f;   // deg/s

        public bool augerExtended  { get; private set; }
        public bool isUnloading    { get; private set; }

        [SerializeField] private float augerUnloadRate = 5f;   // grain units/s
        [SerializeField] private ParticleSystem grainStreamParticles;

        // ── Engine audio ───────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private float engineIdlePitch = 0.5f;
        [SerializeField] private float engineMaxPitch  = 1.1f;
        [SerializeField] private AudioSource cutterAudioSource;

        // ── Internal state ─────────────────────────────────────────────────
        private Vector3 _headerTargetPos;
        private Quaternion _augerTargetRot;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.Harvester;

            _headerTargetPos = headerRaisedLocalPos;
            _augerTargetRot  = Quaternion.Euler(augerRetractedLocalRot);

            if (headerTransform != null)
                headerTransform.localPosition = _headerTargetPos;
        }

        protected override void Update()
        {
            base.Update();

            AnimateHeader();
            AnimateAuger();

            if (!isOccupied || !engineRunning) return;

            ApplyDriving();
            ApplySteering();
            HandleCutting();
            HandleUnloading();
            SyncWheelMeshes();
        }

        // ── Drive ──────────────────────────────────────────────────────────
        public override void ApplyDriving()
        {
            if (!engineRunning) return;

            var input = InputManager.Instance;
            float throttle = (input != null && Mathf.Abs(input.MoveY) > 0.01f)
                ? input.MoveY
                : (InputManagerProxy.Throttle - InputManagerProxy.Brake);

            float topSpeedMs = topSpeedKmh / 3.6f;
            float torque = rb.linearVelocity.magnitude < topSpeedMs ? throttle * motorTorque : 0f;

            if (Mathf.Abs(throttle) > 0.05f)
            {
                if (wheelFL != null) { wheelFL.brakeTorque = 0f; wheelFL.motorTorque = torque * 0.5f; }
                if (wheelFR != null) { wheelFR.brakeTorque = 0f; wheelFR.motorTorque = torque * 0.5f; }
                if (wheelRL != null) { wheelRL.brakeTorque = 0f; wheelRL.motorTorque = torque * 0.5f; }
                if (wheelRR != null) { wheelRR.brakeTorque = 0f; wheelRR.motorTorque = torque * 0.5f; }
            }
            else
            {
                float brakeT = 3000f; // Idle hold brake
                if (wheelFL != null) { wheelFL.motorTorque = 0f; wheelFL.brakeTorque = brakeT; }
                if (wheelFR != null) { wheelFR.motorTorque = 0f; wheelFR.brakeTorque = brakeT; }
                if (wheelRL != null) { wheelRL.motorTorque = 0f; wheelRL.brakeTorque = brakeT; }
                if (wheelRR != null) { wheelRR.motorTorque = 0f; wheelRR.brakeTorque = brakeT; }
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            if (!engineRunning) return;

            var input = InputManager.Instance;
            float steerInput = (input != null && Mathf.Abs(input.MoveX) > 0.01f)
                ? input.MoveX
                : InputManagerProxy.Steer;

            float steer = steerInput * maxSteerAngle;
            wheelFL.steerAngle = steer;
            wheelFR.steerAngle = steer;
        }

        // ── Header control ─────────────────────────────────────────────────

        /// <summary>Toggle or explicitly raise / lower the cutting header.</summary>
        public void RaiseLowerHeader() => RaiseLowerHeader(!headerLowered);

        /// <summary>Raise or lower the cutting header.</summary>
        /// <param name="lower">true = lower to field height; false = raise for transport.</param>
        public void RaiseLowerHeader(bool lower)
        {
            headerLowered    = lower;
            _headerTargetPos = lower ? headerLoweredLocalPos : headerRaisedLocalPos;

            if (!lower)
                OnStopCutting();
        }

        private void AnimateHeader()
        {
            if (headerTransform == null) return;
            headerTransform.localPosition = Vector3.MoveTowards(
                headerTransform.localPosition, _headerTargetPos,
                headerMoveSpeed * Time.deltaTime);
        }

        // ── Cutting ────────────────────────────────────────────────────────

        public void OnStartCutting()
        {
            if (!headerLowered || tankFull) return;
            isCutting = true;

            if (cropDustParticles != null && !cropDustParticles.isPlaying) cropDustParticles.Play();
            if (chaffParticles    != null && !chaffParticles.isPlaying)    chaffParticles.Play();
            if (cutterAudioSource != null && !cutterAudioSource.isPlaying) cutterAudioSource.Play();
        }

        public void OnStopCutting()
        {
            isCutting = false;

            if (cropDustParticles != null) cropDustParticles.Stop();
            if (chaffParticles    != null) chaffParticles.Stop();
            if (cutterAudioSource != null) cutterAudioSource.Stop();
        }

        private void HandleCutting()
        {
            if (!isCutting) return;
            if (tankFull)   { OnStopCutting(); return; }

            // Build the cutting bounds from the header position
            if (cropManager != null && headerTransform != null)
            {
                Vector3 centre = headerTransform.position;
                Bounds  area   = new Bounds(centre, cutBoundsSize);
                cropManager.CutCropsInArea(area);
            }

            // Fill grain tank
            grainLevel += grainFillRate * Time.deltaTime;
            grainLevel  = Mathf.Clamp(grainLevel, 0f, 100f);
        }

        // ── Auger ──────────────────────────────────────────────────────────

        public void ExtendAuger()
        {
            augerExtended    = true;
            _augerTargetRot  = Quaternion.Euler(augerExtendedLocalRot);
        }

        public void RetractAuger()
        {
            if (isUnloading) StopUnloading();
            augerExtended   = false;
            _augerTargetRot = Quaternion.Euler(augerRetractedLocalRot);
        }

        public void UnloadGrain()
        {
            if (isUnloading) StopUnloading();
            else StartUnloading();
        }

        public void StartUnloading()
        {
            if (!augerExtended || grainLevel <= 0f) return;
            isUnloading = true;
            if (grainStreamParticles != null && !grainStreamParticles.isPlaying)
                grainStreamParticles.Play();
        }

        public void StopUnloading()
        {
            isUnloading = false;
            if (grainStreamParticles != null) grainStreamParticles.Stop();
        }

        private void AnimateAuger()
        {
            if (augerTransform == null) return;
            augerTransform.localRotation = Quaternion.RotateTowards(
                augerTransform.localRotation, _augerTargetRot, augerRotateSpeed * Time.deltaTime);
        }

        private void HandleUnloading()
        {
            if (!isUnloading) return;
            if (grainLevel <= 0f) { StopUnloading(); return; }

            grainLevel -= augerUnloadRate * Time.deltaTime;
            grainLevel  = Mathf.Max(0f, grainLevel);
        }

        // ── Trigger zone ───────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Crop"))
                OnStartCutting();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Crop"))
                OnStopCutting();
        }

        // ── Engine sound ───────────────────────────────────────────────────
        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();

            float load = Mathf.Clamp01(currentSpeed / topSpeedKmh);
            engineAudioSource.pitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, load);
            engineRPM = Mathf.Lerp(500f, 2500f, load);
        }

        public override string GetVehicleInfo()
        {
            return $"Harvester | Speed: {currentSpeed:F1} km/h | " +
                   $"Grain: {grainLevel:F0}% | Cutting: {isCutting} | Fuel: {fuelLevel:F0}%";
        }

        // ── Wheel mesh sync ────────────────────────────────────────────────
        private void SyncWheelMeshes()
        {
            UpdateWheelMeshes(wheelFL, meshFL);
            UpdateWheelMeshes(wheelFR, meshFR);
            UpdateWheelMeshes(wheelRL, meshRL);
            UpdateWheelMeshes(wheelRR, meshRR);
        }
    }
}
