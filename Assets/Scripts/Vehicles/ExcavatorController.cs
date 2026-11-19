using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Tracked heavy excavator (Poclain / 360 Excavator) controller.
    /// Features: Dual-track differential drive with counter-rotation in place,
    /// 360° continuous cabin slew rotation, multi-joint articulated arm chain
    /// (boom, stick, bucket), real-time terrain digging, soil collection,
    /// and procedural heavy turbo diesel audio.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class ExcavatorController : VehicleController
    {
        // ── Track WheelColliders (Bogies) ──────────────────────────────────
        [Header("Excavator – Tracks (WheelCollider Bogies)")]
        [SerializeField] public WheelCollider[] leftTrackWheels  = new WheelCollider[2];
        [SerializeField] public WheelCollider[] rightTrackWheels = new WheelCollider[2];

        [SerializeField] private float trackMotorTorque = 12000f;
        [SerializeField] private float trackBrakeTorque = 18000f;
        [SerializeField] private float handBrakeTorque  = 25000f;
        [SerializeField] private float topSpeedKmh      = 15f;

        // ── Suspension & Stability ─────────────────────────────────────────
        [Header("Suspension & Stability")]
        public float antiRollStiffness = 18000f;
        public float downforceCoefficient = 4.0f;

        // ── Track Speed State ──────────────────────────────────────────────
        private float _leftTrackInput;
        private float _rightTrackInput;

        // ── Cabin Rotation (Slew) ──────────────────────────────────────────
        [Header("Excavator – Cabin (Slew)")]
        [SerializeField] public Transform cabinTransform;
        [SerializeField] public float cabinRotateSpeed = 45f; // Deg/sec
        public float cabinAngle;

        // ── Articulated Arm Chain ──────────────────────────────────────────
        [Header("Excavator – Arm Transforms")]
        [SerializeField] public Transform boomTransform;      // Boom pitch: -15° to 65°
        [SerializeField] public Transform stickTransform;     // Stick pitch: -30° to 75°
        [SerializeField] public Transform bucketTransform;    // Bucket pitch: -55° to 80°
        [SerializeField] public Transform bucketTip;          // Penetration tip

        [SerializeField] public HydraulicArm boomArm;
        [SerializeField] public HydraulicArm stickArm;
        [SerializeField] public HydraulicArm bucketArm;

        [Header("Excavator – Arm Angles")]
        public float boomAngle = 10f;
        public float stickAngle = 15f;
        public float bucketAngle = 0f;
        [SerializeField] private float armSpeed = 35f;

        // ── Terrain Digging & Soil ─────────────────────────────────────────
        [Header("Excavator – Terrain & Soil")]
        [SerializeField] public TerrainDeformer terrainDeformer;
        [SerializeField] private float digRadius = 1.4f;
        [SerializeField] private float digDepth  = 0.5f;
        [Range(0f, 1f)] public float currentSoilVolume;
        [SerializeField] private float soilCollectRate = 0.35f;

        private bool _isPerformingDigCycle;
        private float _digCycleTimer;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.Excavator;
            maxRPM = 2400f;
            rb.mass = 22000f; // 22 tons
            rb.centerOfMass = new Vector3(0f, -0.6f, 0f);

            ConfigureTrackFriction(leftTrackWheels);
            ConfigureTrackFriction(rightTrackWheels);

            if (terrainDeformer == null)
                terrainDeformer = FindObjectOfType<TerrainDeformer>();
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;

            ReadTrackInput();
            ApplyTrackTorques();
            ApplyAntiRollBars();
            ApplyAerodynamicDownforce();
            UpdateArticulatedArms();
            ConsumeFuel();
        }

        protected override void Update()
        {
            base.Update();
            UpdateEngineSound();
            HandleManualCabinAndArm();
        }

        // ── Track Driving & Counter-Rotation ───────────────────────────────
        public override void ApplyDriving()
        {
            // Handled directly via ReadTrackInput & ApplyTrackTorques in FixedUpdate
        }

        public override void ApplySteering()
        {
            // Tank differential steering handled via ReadTrackInput
        }

        public void ReadTrackInput()
        {
            float throttle = InputManagerProxy.Throttle - InputManagerProxy.Brake;
            float steer    = InputManagerProxy.Steer;

            if (Mathf.Abs(throttle) < 0.05f && Mathf.Abs(steer) > 0.05f)
            {
                // In-place counter-rotation (tank spin)
                _leftTrackInput  = steer;
                _rightTrackInput = -steer;
            }
            else
            {
                // Forward/reverse differential drive
                _leftTrackInput  = Mathf.Clamp(throttle + steer * 0.6f, -1f, 1f);
                _rightTrackInput = Mathf.Clamp(throttle - steer * 0.6f, -1f, 1f);
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public void ApplyTrackTorques()
        {
            float topSpeedMs = topSpeedKmh / 3.6f;
            float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / topSpeedMs);

            ApplyToTrack(leftTrackWheels, _leftTrackInput, speedRatio);
            ApplyToTrack(rightTrackWheels, _rightTrackInput, speedRatio);

            // Handbrake locks both tracks completely
            if (InputManagerProxy.HandBrake > 0.5f)
            {
                LockTracks(handBrakeTorque);
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        private void ApplyToTrack(WheelCollider[] wheels, float input, float speedRatio)
        {
            if (wheels == null) return;

            float torque = Mathf.Abs(input) > 0.05f ? input * trackMotorTorque * (1f - speedRatio * 0.3f) : 0f;
            float brake  = Mathf.Abs(input) <= 0.05f ? trackBrakeTorque : 0f;

            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                wc.motorTorque = torque;
                wc.brakeTorque = brake;
            }
        }

        public void LockTracks(float brakeForce)
        {
            LockTrackGroup(leftTrackWheels, brakeForce);
            LockTrackGroup(rightTrackWheels, brakeForce);
        }

        private void LockTrackGroup(WheelCollider[] wheels, float brakeForce)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                wc.motorTorque = 0f;
                wc.brakeTorque = brakeForce;
            }
        }

        // ── Suspension & Anti-Roll ─────────────────────────────────────────
        public void ApplyAntiRollBars()
        {
            if (leftTrackWheels != null && rightTrackWheels != null)
            {
                int count = Mathf.Min(leftTrackWheels.Length, rightTrackWheels.Length);
                for (int i = 0; i < count; i++)
                {
                    ApplyAntiRoll(leftTrackWheels[i], rightTrackWheels[i]);
                }
            }
        }

        public void ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            if (left == null || right == null || rb == null) return;

            float travelL = 1.0f;
            float travelR = 1.0f;

            bool groundedL = left.GetGroundHit(out WheelHit hitL);
            if (groundedL)
                travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;

            bool groundedR = right.GetGroundHit(out WheelHit hitR);
            if (groundedR)
                travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;

            float antiRollForce = (travelL - travelR) * antiRollStiffness;

            if (groundedL)
                rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
            if (groundedR)
                rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }

        public void ApplyAerodynamicDownforce()
        {
            if (rb == null) return;
            rb.AddForce(-transform.up * downforceCoefficient * rb.linearVelocity.sqrMagnitude);
        }

        private void ConfigureTrackFriction(WheelCollider[] wheels)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc == null) continue;

                WheelFrictionCurve fwd = wc.forwardFriction;
                fwd.extremumSlip = 0.40f;
                fwd.extremumValue = 1.0f;
                fwd.asymptoteSlip = 0.80f;
                fwd.asymptoteValue = 0.70f;
                fwd.stiffness = 2.0f; // High track traction
                wc.forwardFriction = fwd;

                WheelFrictionCurve side = wc.sidewaysFriction;
                side.extremumSlip = 0.35f;
                side.extremumValue = 1.0f;
                side.asymptoteSlip = 0.65f;
                side.asymptoteValue = 0.80f;
                side.stiffness = 2.2f;
                wc.sidewaysFriction = side;
            }
        }

        // ── Cabin Slew (360° Continuous Rotation) ──────────────────────────
        public void RotateCabin(float deltaDeg)
        {
            cabinAngle = (cabinAngle + deltaDeg) % 360f;
            if (cabinTransform != null)
                cabinTransform.localRotation = Quaternion.Euler(0f, cabinAngle, 0f);
        }

        public void SlewCabinStep(float direction = 1f)
        {
            RotateCabin(direction * 45f);
        }

        // ── Articulated Arm & Digging Cycle ────────────────────────────────
        public void SetArmAngles(float b, float s, float bk)
        {
            boomAngle   = Mathf.Clamp(b, -15f, 65f);
            stickAngle  = Mathf.Clamp(s, -30f, 75f);
            bucketAngle = Mathf.Clamp(bk, -55f, 80f);
            if (boomArm != null) boomArm.SetTargetAngle(boomAngle);
            if (stickArm != null) stickArm.SetTargetAngle(stickAngle);
            if (bucketArm != null) bucketArm.SetTargetAngle(bucketAngle);
        }

        public void TriggerDigStroke()
        {
            _isPerformingDigCycle = true;
            _digCycleTimer = 0f;
        }

        public void UpdateArticulatedArms()
        {
            // Automated articulated digging sequence
            if (_isPerformingDigCycle)
            {
                _digCycleTimer += Time.fixedDeltaTime;
                if (_digCycleTimer < 1.0f)
                {
                    // Phase 1: Reach boom and stick downward toward ground
                    boomAngle   = Mathf.MoveTowards(boomAngle, 35f, armSpeed * Time.fixedDeltaTime);
                    stickAngle  = Mathf.MoveTowards(stickAngle, 45f, armSpeed * Time.fixedDeltaTime);
                    bucketAngle = Mathf.MoveTowards(bucketAngle, -35f, armSpeed * Time.fixedDeltaTime);
                }
                else if (_digCycleTimer < 2.0f)
                {
                    // Phase 2: Pull stick inward through soil and curl bucket
                    stickAngle  = Mathf.MoveTowards(stickAngle, -10f, armSpeed * 1.5f * Time.fixedDeltaTime);
                    bucketAngle = Mathf.MoveTowards(bucketAngle, 65f, armSpeed * 1.5f * Time.fixedDeltaTime);
                }
                else if (_digCycleTimer < 3.0f)
                {
                    // Phase 3: Lift boom upward with full bucket of soil
                    boomAngle = Mathf.MoveTowards(boomAngle, -10f, armSpeed * Time.fixedDeltaTime);
                    if (_digCycleTimer > 2.2f && currentSoilVolume < 0.2f)
                    {
                        DigAtGround();
                    }
                }
                else
                {
                    _isPerformingDigCycle = false;
                }
            }

            // Apply rotations to arm joints
            if (boomTransform != null)
                boomTransform.localRotation = Quaternion.Euler(boomAngle, 0f, 0f);
            if (stickTransform != null)
                stickTransform.localRotation = Quaternion.Euler(stickAngle, 0f, 0f);
            if (bucketTransform != null)
                bucketTransform.localRotation = Quaternion.Euler(bucketAngle, 0f, 0f);
        }

        public void DigAtGround()
        {
            Vector3 digPos = transform.position + transform.forward * 4.2f + Vector3.up * 0.1f;
            if (bucketTip != null) digPos = bucketTip.position;

            if (terrainDeformer != null)
            {
                terrainDeformer.DigAt(digPos, digRadius, digDepth);
            }
            currentSoilVolume = Mathf.Clamp01(currentSoilVolume + soilCollectRate);
            Debug.Log($"[ExcavatorController] DigAtGround performed at {digPos}. Soil volume: {currentSoilVolume:P0}");
        }

        public void DumpSoil()
        {
            if (currentSoilVolume <= 0f) return;

            Vector3 dumpPos = bucketTip != null ? bucketTip.position : transform.position + transform.forward * 4f;
            if (terrainDeformer != null)
            {
                terrainDeformer.RaiseAt(dumpPos, digRadius * 1.2f, currentSoilVolume * 0.35f);
            }
            currentSoilVolume = 0f;
            // Tilt bucket to dump angle
            bucketAngle = -45f;
            Debug.Log($"[ExcavatorController] DumpSoil performed at {dumpPos}. Soil emptied.");
        }

        private void HandleManualCabinAndArm()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            if (Mathf.Abs(input.RightJoystick.x) > 0.1f)
            {
                RotateCabin(input.RightJoystick.x * cabinRotateSpeed * Time.deltaTime);
            }
            if (Mathf.Abs(input.LeftJoystick.y) > 0.1f)
            {
                boomAngle += input.LeftJoystick.y * armSpeed * Time.deltaTime;
                boomAngle = Mathf.Clamp(boomAngle, -15f, 65f);
            }
        }

        // ── Engine Sound ───────────────────────────────────────────────────
        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = CreateProceduralExcavatorDieselClip();
            }

            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.loop = true;
                engineAudioSource.Play();
            }

            float load = Mathf.Clamp01(Mathf.Abs(currentSpeed) / topSpeedKmh);
            float rpmRatio = Mathf.Lerp(0.25f, 1.0f, load + (_isPerformingDigCycle ? 0.4f : 0f));
            engineAudioSource.pitch = Mathf.Lerp(0.65f, 1.85f, rpmRatio);
            engineAudioSource.volume = Mathf.Lerp(0.40f, 0.90f, 0.4f + 0.6f * Mathf.Abs(InputManagerProxy.Throttle));
        }

        public static AudioClip CreateProceduralExcavatorDieselClip()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float fundamentalFreq = 28f; // ~28 Hz heavy 6-cylinder diesel idle
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float s2 = 0.6f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 2f * t);
                float s3 = 0.35f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 3f * t);
                float hydraulicHum = 0.09f * Mathf.Sin(2f * Mathf.PI * 380f * t); // Hydraulic pump hum
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.12f;
                samples[i] = Mathf.Clamp((s1 + s2 + s3 + hydraulicHum + noise) * 0.35f, -1f, 1f);
            }

            var clip = AudioClip.Create("Synthesized_Excavator_Diesel", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 4.0f)
            {
                float dmg = (impactForce - 4.0f) * 1.5f;
                damageLevel = Mathf.Clamp(damageLevel + dmg, 0f, 100f);
            }
        }

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.015f * Mathf.Abs(InputManagerProxy.Throttle);
        }

        public override string GetVehicleInfo() => "Tracked Excavator";
    }
}
