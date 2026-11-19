using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Combine Harvester controller.
    /// Inherits VehicleController.
    ///
    /// Drive        : 4 WheelColliders, anti-roll sway bars, auto-reverse, 20k Nm handbrake.
    /// Header       : Raises / lowers cutting bar; rotating reel; triggers CropManager on active cut.
    /// Grain tank   : Accumulates grainLevel 0-100%.
    /// Unload auger : Swings out 90 degrees and unloads grain payload.
    /// Audio        : Procedural heavy 6-cylinder combine diesel synthesis.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class HarvesterController : VehicleController
    {
        // ── Drive & Physics ────────────────────────────────────────────────
        [Header("Harvester – Drive & Wheels")]
        [SerializeField] public WheelCollider wheelFL;
        [SerializeField] public WheelCollider wheelFR;
        [SerializeField] public WheelCollider wheelRL;
        [SerializeField] public WheelCollider wheelRR;

        [SerializeField] public Transform meshFL;
        [SerializeField] public Transform meshFR;
        [SerializeField] public Transform meshRL;
        [SerializeField] public Transform meshRR;

        [SerializeField] private float motorTorque       = 8000f;
        [SerializeField] private float maxSteerAngle     = 26f;
        [SerializeField] private float topSpeedKmh       = 22f;
        [SerializeField] private float brakeForce        = 14000f;
        [SerializeField] private float handbrakeTorque   = 20000f;
        [SerializeField] private float idleBrakeTorque   = 3500f;

        [Header("Harvester – Suspension & Anti-Roll")]
        [SerializeField] private float antiRollStiffness    = 14000f;
        [SerializeField] private float downforceCoefficient = 4.0f;

        // ── Header & Reel ──────────────────────────────────────────────────
        [Header("Harvester – Header & Reel")]
        [SerializeField] public Transform headerTransform;
        [SerializeField] public Transform reelTransform;
        [SerializeField] private Vector3   headerLoweredLocalPos = new Vector3(0f, 0.15f, 3.4f);
        [SerializeField] private Vector3   headerRaisedLocalPos  = new Vector3(0f, 0.75f, 3.4f);
        [SerializeField] private float     headerMoveSpeed       = 1.5f;  // m/s
        [SerializeField] private float     reelSpinSpeed         = 240f;  // deg/s

        public bool headerLowered { get; private set; }

        // ── Cutting System ─────────────────────────────────────────────────
        [Header("Harvester – Cutting System")]
        [SerializeField] private Collider headerTriggerZone;
        [SerializeField] private CropManager cropManager;
        [Tooltip("Width × height × depth of the cut bounds relative to header position.")]
        [SerializeField] private Vector3 cutBoundsSize = new Vector3(5.5f, 1.5f, 2.5f);

        [SerializeField] private ParticleSystem cropDustParticles;
        [SerializeField] private ParticleSystem chaffParticles;
        [SerializeField] private AudioSource cutterAudioSource;

        public bool isCutting { get; private set; }

        // ── Grain Tank ─────────────────────────────────────────────────────
        [Header("Harvester – Grain Tank")]
        [Range(0f, 100f)]
        public float grainLevel;
        public bool tankFull => grainLevel >= 100f;
        [SerializeField] private float grainFillRate = 12f;   // % per second while actively harvesting

        // ── Unload Auger ───────────────────────────────────────────────────
        [Header("Harvester – Unload Auger")]
        [SerializeField] public Transform augerTransform;
        [SerializeField] private Vector3   augerExtendedLocalRot  = new Vector3(0f, -90f, 0f);
        [SerializeField] private Vector3   augerRetractedLocalRot = new Vector3(0f, 0f, 0f);
        [SerializeField] private float     augerRotateSpeed       = 65f;   // deg/s
        [SerializeField] private float     augerUnloadRate        = 25f;   // % per second while unloading
        [SerializeField] private ParticleSystem grainStreamParticles;

        public bool augerExtended { get; private set; }
        public bool isUnloading   { get; private set; }

        // ── Audio ──────────────────────────────────────────────────────────
        [Header("Harvester – Engine Sound")]
        [SerializeField] private float engineIdlePitch = 0.65f;
        [SerializeField] private float engineMaxPitch  = 1.50f;

        // ── Internal State ─────────────────────────────────────────────────
        private Vector3 _headerTargetPos;
        private Quaternion _augerTargetRot;
        private bool _isAudioSynthesized = false;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.Harvester;

            _headerTargetPos = headerRaisedLocalPos;
            _augerTargetRot  = Quaternion.Euler(augerRetractedLocalRot);

            if (headerTransform != null)
                headerTransform.localPosition = _headerTargetPos;

            if (rb != null)
            {
                rb.mass = 11000f;
                rb.centerOfMass = new Vector3(0f, -0.6f, 0f);
            }

            EnsureProceduralHarvesterAudio();
        }

        private void FixedUpdate()
        {
            if (!isOccupied || !engineRunning)
            {
                ApplyBrakeTorque(idleBrakeTorque);
                return;
            }

            ApplyDriving();
            ApplySteering();
            ApplyAntiRollSwayBars();
            ApplyDownforce();
            UpdateWheelPoses();
            UpdateEngineSound();
            UpdateMechanisms(Time.fixedDeltaTime);
        }

        protected override void Update()
        {
            base.Update();
            if (!Application.isPlaying)
            {
                UpdateMechanisms(0.02f);
            }
        }

        // ── Drive & Physics ────────────────────────────────────────────────
        public override void ApplyDriving()
        {
            if (!engineRunning || rb == null) return;

            var input = InputManager.Instance;
            float rawThrottle = (input != null && Mathf.Abs(input.MoveY) > 0.01f)
                ? Mathf.Max(0f, input.MoveY)
                : InputManagerProxy.Throttle;

            float rawBrake = (input != null && Mathf.Abs(input.MoveY) > 0.01f)
                ? Mathf.Max(0f, -input.MoveY)
                : InputManagerProxy.Brake;

            bool handbrake = (input != null && input.Handbrake > 0.5f) || InputManagerProxy.HandBrake > 0.5f || InputManagerProxy.MobileHandBrake > 0.5f;
            currentSpeed = rb.linearVelocity.magnitude * 3.6f;

            if (handbrake)
            {
                ApplyBrakeTorque(handbrakeTorque);
                return;
            }

            // Automatic Forward / Reverse Transition
            if (currentGear == GearState.Drive)
            {
                if (currentSpeed < 0.8f && rawBrake > 0.3f && rawThrottle < 0.1f)
                {
                    currentGear = GearState.Reverse;
                }
            }
            else if (currentGear == GearState.Reverse)
            {
                if (currentSpeed < 0.8f && rawThrottle > 0.3f && rawBrake < 0.1f)
                {
                    currentGear = GearState.Drive;
                }
            }

            if (currentGear == GearState.Drive)
            {
                if (rawBrake > 0.05f)
                {
                    ApplyBrakeTorque(rawBrake * brakeForce);
                }
                else if (rawThrottle > 0.05f)
                {
                    ReleaseBrakes();
                    float torque = (currentSpeed < topSpeedKmh) ? rawThrottle * motorTorque : 0f;
                    ApplyMotorTorque(torque);
                }
                else
                {
                    ApplyBrakeTorque(idleBrakeTorque);
                }
            }
            else // GearState.Reverse
            {
                if (rawThrottle > 0.05f)
                {
                    ApplyBrakeTorque(rawThrottle * brakeForce);
                }
                else if (rawBrake > 0.05f)
                {
                    ReleaseBrakes();
                    float reverseTopSpeed = 10f;
                    float torque = (currentSpeed < reverseTopSpeed) ? -rawBrake * motorTorque * 0.75f : 0f;
                    ApplyMotorTorque(torque);
                }
                else
                {
                    ApplyBrakeTorque(idleBrakeTorque);
                }
            }
        }

        public override void ApplySteering()
        {
            if (!engineRunning) return;

            var input = InputManager.Instance;
            float steerInput = (input != null && Mathf.Abs(input.MoveX) > 0.01f)
                ? input.MoveX
                : InputManagerProxy.Steer;

            float speedFactor = Mathf.Clamp01(currentSpeed / topSpeedKmh);
            float steerLimit = Mathf.Lerp(maxSteerAngle, 14f, speedFactor);
            float targetSteer = steerInput * steerLimit;

            if (wheelFL != null) wheelFL.steerAngle = Mathf.MoveTowards(wheelFL.steerAngle, targetSteer, 80f * Time.fixedDeltaTime);
            if (wheelFR != null) wheelFR.steerAngle = Mathf.MoveTowards(wheelFR.steerAngle, targetSteer, 80f * Time.fixedDeltaTime);
        }

        private void ApplyMotorTorque(float torque)
        {
            float perWheel = torque * 0.25f;
            if (wheelFL != null) wheelFL.motorTorque = perWheel;
            if (wheelFR != null) wheelFR.motorTorque = perWheel;
            if (wheelRL != null) wheelRL.motorTorque = perWheel;
            if (wheelRR != null) wheelRR.motorTorque = perWheel;
        }

        private void ApplyBrakeTorque(float brake)
        {
            float perWheel = brake * 0.25f;
            if (wheelFL != null) { wheelFL.motorTorque = 0f; wheelFL.brakeTorque = perWheel; }
            if (wheelFR != null) { wheelFR.motorTorque = 0f; wheelFR.brakeTorque = perWheel; }
            if (wheelRL != null) { wheelRL.motorTorque = 0f; wheelRL.brakeTorque = perWheel; }
            if (wheelRR != null) { wheelRR.motorTorque = 0f; wheelRR.brakeTorque = perWheel; }
        }

        private void ReleaseBrakes()
        {
            if (wheelFL != null) wheelFL.brakeTorque = 0f;
            if (wheelFR != null) wheelFR.brakeTorque = 0f;
            if (wheelRL != null) wheelRL.brakeTorque = 0f;
            if (wheelRR != null) wheelRR.brakeTorque = 0f;
        }

        private void ApplyAntiRollSwayBars()
        {
            ApplyAntiRollPair(wheelFL, wheelFR);
            ApplyAntiRollPair(wheelRL, wheelRR);
        }

        private void ApplyAntiRollPair(WheelCollider wl, WheelCollider wr)
        {
            if (wl == null || wr == null || rb == null) return;
            float travelL = 1.0f;
            float travelR = 1.0f;

            bool groundedL = wl.GetGroundHit(out WheelHit hitL);
            if (groundedL)
                travelL = (-wl.transform.InverseTransformPoint(hitL.point).y - wl.radius) / wl.suspensionDistance;

            bool groundedR = wr.GetGroundHit(out WheelHit hitR);
            if (groundedR)
                travelR = (-wr.transform.InverseTransformPoint(hitR.point).y - wr.radius) / wr.suspensionDistance;

            float antiRollForce = (travelL - travelR) * antiRollStiffness;

            if (groundedL)
                rb.AddForceAtPosition(wl.transform.up * -antiRollForce, wl.transform.position);
            if (groundedR)
                rb.AddForceAtPosition(wr.transform.up * antiRollForce, wr.transform.position);
        }

        private void ApplyDownforce()
        {
            if (rb != null)
            {
                rb.AddForce(-transform.up * (downforceCoefficient * currentSpeed), ForceMode.Force);
            }
        }

        private void UpdateWheelPoses()
        {
            UpdateSingleWheelPose(wheelFL, meshFL);
            UpdateSingleWheelPose(wheelFR, meshFR);
            UpdateSingleWheelPose(wheelRL, meshRL);
            UpdateSingleWheelPose(wheelRR, meshRR);
        }

        private void UpdateSingleWheelPose(WheelCollider col, Transform mesh)
        {
            if (col == null || mesh == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        // ── Header & Reel Control ──────────────────────────────────────────
        public void RaiseLowerHeader() => RaiseLowerHeader(!headerLowered);

        public void RaiseLowerHeader(bool lower)
        {
            headerLowered = lower;
            _headerTargetPos = lower ? headerLoweredLocalPos : headerRaisedLocalPos;

            if (lower)
                OnStartCutting();
            else
                OnStopCutting();
        }

        public void UpdateMechanisms(float dt)
        {
            AnimateHeader(dt);
            AnimateAuger(dt);
            AnimateReel(dt);
            HandleCutting(dt);
            HandleUnloading(dt);
        }

        public void AnimateHeader() => AnimateHeader(Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime);
        public void AnimateHeader(float dt)
        {
            if (headerTransform == null) return;
            headerTransform.localPosition = Vector3.MoveTowards(
                headerTransform.localPosition, _headerTargetPos,
                headerMoveSpeed * dt);
        }

        public void AnimateReel() => AnimateReel(Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime);
        public void AnimateReel(float dt)
        {
            if (reelTransform == null) return;
            if (headerLowered || isCutting)
            {
                float speedMult = Mathf.Clamp(currentSpeed * 0.4f, 1f, 3.5f);
                reelTransform.Rotate(Vector3.right * (reelSpinSpeed * speedMult * dt), Space.Self);
            }
        }

        // ── Cutting System ─────────────────────────────────────────────────
        public void OnStartCutting()
        {
            if (tankFull) return;
            isCutting = true;

            if (cropDustParticles != null && !cropDustParticles.isPlaying) cropDustParticles.Play();
            if (chaffParticles != null && !chaffParticles.isPlaying) chaffParticles.Play();
            if (cutterAudioSource != null && !cutterAudioSource.isPlaying) cutterAudioSource.Play();
        }

        public void OnStopCutting()
        {
            isCutting = false;

            if (cropDustParticles != null) cropDustParticles.Stop();
            if (chaffParticles != null) chaffParticles.Stop();
            if (cutterAudioSource != null) cutterAudioSource.Stop();
        }

        public void HandleCutting() => HandleCutting(Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime);
        public void HandleCutting(float dt)
        {
            if (!isCutting) return;
            if (tankFull) { OnStopCutting(); return; }

            if (cropManager == null)
            {
                cropManager = CropManager.Instance;
                if (cropManager == null)
                    cropManager = Object.FindAnyObjectByType<CropManager>();
            }

            if (cropManager != null && headerTransform != null)
            {
                Vector3 centre = headerTransform.position;
                Bounds area = new Bounds(centre, cutBoundsSize);
                int cutCount = cropManager.CutCropsInArea(area);
                if (cutCount > 0)
                {
                    grainLevel += cutCount * 0.4f;
                }
            }

            // Passive grain collection while moving forward with header lowered
            if (currentSpeed > 0.4f)
            {
                grainLevel += grainFillRate * dt;
            }
            grainLevel = Mathf.Clamp(grainLevel, 0f, 100f);
        }

        // ── Auger Control ──────────────────────────────────────────────────
        public void ToggleAuger()
        {
            if (augerExtended)
            {
                if (isUnloading)
                {
                    StopUnloading();
                }
                else if (grainLevel > 0.5f)
                {
                    StartUnloading();
                }
                else
                {
                    RetractAuger();
                }
            }
            else
            {
                ExtendAuger();
            }
        }

        public void ExtendAuger()
        {
            augerExtended = true;
            _augerTargetRot = Quaternion.Euler(augerExtendedLocalRot);
        }

        public void RetractAuger()
        {
            StopUnloading();
            augerExtended = false;
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

        public void AnimateAuger() => AnimateAuger(Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime);
        public void AnimateAuger(float dt)
        {
            if (augerTransform == null) return;
            augerTransform.localRotation = Quaternion.RotateTowards(
                augerTransform.localRotation, _augerTargetRot, augerRotateSpeed * dt);
        }

        public void HandleUnloading() => HandleUnloading(Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime);
        public void HandleUnloading(float dt)
        {
            if (!isUnloading) return;
            if (grainLevel <= 0f)
            {
                grainLevel = 0f;
                StopUnloading();
                return;
            }

            grainLevel -= augerUnloadRate * dt;
            if (grainLevel <= 0f)
            {
                grainLevel = 0f;
                StopUnloading();
            }
        }

        // ── Procedural Engine Audio ────────────────────────────────────────
        private void EnsureProceduralHarvesterAudio()
        {
            if (engineAudioSource == null)
                engineAudioSource = GetComponent<AudioSource>();

            if (engineAudioSource != null && (engineAudioSource.clip == null || engineAudioSource.clip.name != "Synthesized_Harvester_Diesel"))
            {
                engineAudioSource.clip = CreateProceduralHarvesterDieselClip();
                engineAudioSource.loop = true;
                engineAudioSource.spatialBlend = 0.5f;
                engineAudioSource.volume = 0.65f;
                _isAudioSynthesized = true;
            }
        }

        private AudioClip CreateProceduralHarvesterDieselClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Heavy 6-cylinder combine diesel ~32Hz fundamental with mechanical buzz
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float pulse = Mathf.Sin(2f * Mathf.PI * 32f * t) * 0.45f
                            + Mathf.Sin(2f * Mathf.PI * 64f * t) * 0.25f
                            + Mathf.Sin(2f * Mathf.PI * 96f * t) * 0.15f
                            + Mathf.Sin(2f * Mathf.PI * 128f * t) * 0.08f;
                float rumble = (Mathf.PerlinNoise(t * 180f, 0f) - 0.5f) * 0.18f;
                float chug = Mathf.Repeat(t * 32f, 1f) > 0.75f ? 0.12f : 0f;
                samples[i] = Mathf.Clamp(pulse + rumble + chug, -1f, 1f) * 0.60f;
            }

            AudioClip clip = AudioClip.Create("Synthesized_Harvester_Diesel", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public override void UpdateEngineSound()
        {
            EnsureProceduralHarvesterAudio();
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying && engineRunning) engineAudioSource.Play();

            float load = Mathf.Clamp01(currentSpeed / topSpeedKmh);
            float targetPitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, load) + (isCutting ? 0.15f : 0f);
            float targetVolume = Mathf.Lerp(0.55f, 0.85f, load) + (isCutting ? 0.10f : 0f);

            engineAudioSource.pitch = Mathf.MoveTowards(engineAudioSource.pitch, targetPitch, 1.5f * Time.fixedDeltaTime);
            engineAudioSource.volume = Mathf.MoveTowards(engineAudioSource.volume, targetVolume, 1.5f * Time.fixedDeltaTime);
            engineRPM = Mathf.Lerp(600f, 2200f, load);
        }

        public override string GetVehicleInfo()
        {
            return $"Harvester | Speed: {currentSpeed:F1} km/h | " +
                   $"Grain: {grainLevel:F0}% | Cutting: {isCutting} | Auger: {(augerExtended ? (isUnloading ? "Unloading" : "Extended") : "Retracted")}";
        }
    }
}
