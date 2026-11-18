using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// JCB Backhoe Loader controller.
    /// Features: 4WD drivetrain, anti-roll suspension stability, articulated front loader
    /// (boom & bucket), rear backhoe (swing, boom, dipper, bucket), deployable stabilizer legs,
    /// procedural diesel-hydraulic engine audio, and terrain digging deformation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class JCBController : VehicleController
    {
        // ── Wheel Colliders & Meshes ───────────────────────────────────────
        [Header("Wheel Colliders & Meshes")]
        [SerializeField] public WheelCollider wheelFL;
        [SerializeField] public WheelCollider wheelFR;
        [SerializeField] public WheelCollider wheelRL;
        [SerializeField] public WheelCollider wheelRR;

        [SerializeField] public Transform meshFL;
        [SerializeField] public Transform meshFR;
        [SerializeField] public Transform meshRL;
        [SerializeField] public Transform meshRR;

        // ── Suspension & Stability ─────────────────────────────────────────
        [Header("Suspension & Stability")]
        public float antiRollStiffness = 12000f;
        public float downforceCoefficient = 3.0f;
        public float steerSensitivity = 6.0f;
        private float _currentSmoothedSteer;

        // ── Drive Tuning ───────────────────────────────────────────────────
        [Header("Drive Tuning")]
        [SerializeField] private float motorTorque     = 6500f;
        [SerializeField] private float maxSteerAngle   = 32f;
        [SerializeField] private float maxBrakeTorque  = 12000f;
        [SerializeField] private float handBrakeTorque = 18000f;
        [SerializeField] private float topSpeedKmh     = 38f;
        [SerializeField] private bool  fourWheelDrive  = true;
        public int currentGearIndex = 2; // 0 = R, 1 = N, 2 = D

        // ── Front Loader Arm ───────────────────────────────────────────────
        [Header("Front Loader")]
        [SerializeField] public Transform loaderBoomTransform;      // Main loader boom pivot
        [SerializeField] public Transform loaderBucketTransform;    // Loader bucket pivot
        public float loaderBoomAngle;                               // Degrees: -10 (scoop) to 45 (high dump)
        public float loaderBucketAngle;                             // Degrees: -30 (curl) to 45 (dump)
        [SerializeField] private float loaderSpeed = 35f;           // Deg/sec
        public bool isLoaderRaised;

        // ── Rear Backhoe Arm ───────────────────────────────────────────────
        [Header("Rear Backhoe")]
        [SerializeField] public Transform backhoeSwingTransform;    // Horizontal yaw pivot (-80 to +80)
        [SerializeField] public Transform backhoeBoomTransform;     // Boom pitch (-25 to +55)
        [SerializeField] public Transform backhoeDipperTransform;   // Stick/dipper (-40 to +40)
        [SerializeField] public Transform backhoeBucketTransform;   // Backhoe bucket (-60 to +60)
        [SerializeField] public Transform backhoeBucketTip;         // Tip for ground penetration
        public float backhoeSwingAngle;
        public float backhoeBoomAngle;
        public float backhoeDipperAngle;
        public float backhoeBucketAngle;
        [SerializeField] private float backhoeSpeed = 40f;

        // ── Stabilizers ────────────────────────────────────────────────────
        [Header("Stabilizers")]
        [SerializeField] public Transform stabilizerLeft;
        [SerializeField] public Transform stabilizerRight;
        [SerializeField] private Vector3 stabDownRotation = new Vector3(80f, 0f, 0f);
        [SerializeField] private Vector3 stabUpRotation   = new Vector3(0f, 0f, 0f);
        [SerializeField] private float stabSpeed = 80f; // Deg/sec
        public bool StabilizersDown { get; private set; }
        private Quaternion _stabLeftTarget;
        private Quaternion _stabRightTarget;

        // ── Soil Payload & Digging ─────────────────────────────────────────
        [Header("Soil Payload & Digging")]
        [Range(0f, 1f)] public float soilPayload;
        public TerrainDeformer terrainDeformer;
        private bool _isPerformingDigCycle;
        private float _digCycleTimer;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.JCB;
            maxRPM = 2800f;
            rb.mass = 7500f;
            rb.centerOfMass = new Vector3(0f, -0.4f, 0.1f);

            _stabLeftTarget  = Quaternion.Euler(stabUpRotation);
            _stabRightTarget = Quaternion.Euler(stabUpRotation);

            ConfigureWheelFriction(wheelFL);
            ConfigureWheelFriction(wheelFR);
            ConfigureWheelFriction(wheelRL);
            ConfigureWheelFriction(wheelRR);

            if (terrainDeformer == null)
                terrainDeformer = FindObjectOfType<TerrainDeformer>();
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;

            ApplyDriving();
            ApplySteering();
            ApplyAntiRoll(wheelFL, wheelFR);
            ApplyAntiRoll(wheelRL, wheelRR);
            ApplyAerodynamicDownforce();
            SyncWheelMeshes();
            UpdateStabilizers();
            UpdateArticulatedArms();
            ConsumeFuel();
        }

        protected override void Update()
        {
            base.Update();
            UpdateEngineSound();
            HandleManualInputs();
        }

        // ── Drive ──────────────────────────────────────────────────────────
        public override void ApplyDriving()
        {
            // If stabilizers are deployed, wheels are firmly locked
            if (StabilizersDown)
            {
                WheelPhysicsHelper.ApplyBrakeTorque(new[] { wheelFL, wheelFR, wheelRL, wheelRR }, maxBrakeTorque);
                WheelPhysicsHelper.ApplyMotorTorque(new[] { wheelFL, wheelFR, wheelRL, wheelRR }, 0f);
                currentSpeed = 0f;
                return;
            }

            float rawThrottle = InputManagerProxy.Throttle;
            float rawBrake    = InputManagerProxy.Brake;

            // Seamless auto-reverse when stopped
            if (currentSpeed < 1.5f && rawBrake > 0.1f && rawThrottle < 0.05f)
            {
                currentGearIndex = 0; // Reverse
            }
            else if (rawThrottle > 0.1f && currentGearIndex == 0)
            {
                currentGearIndex = 2; // Forward
            }

            float throttle   = (currentGearIndex == 0) ? rawBrake : rawThrottle;
            float brakeInput = (currentGearIndex == 0) ? rawThrottle : rawBrake;

            float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / (topSpeedKmh / 3.6f));
            float appliedTorque = (currentGearIndex == 1) ? 0f : throttle * motorTorque * (1f - speedRatio * 0.4f);

            if (fourWheelDrive)
            {
                WheelPhysicsHelper.ApplyMotorTorque(new[] { wheelRL, wheelRR }, appliedTorque * 0.6f);
                WheelPhysicsHelper.ApplyMotorTorque(new[] { wheelFL, wheelFR }, appliedTorque * 0.4f);
            }
            else
            {
                WheelPhysicsHelper.ApplyMotorTorque(new[] { wheelRL, wheelRR }, appliedTorque);
                WheelPhysicsHelper.ApplyMotorTorque(new[] { wheelFL, wheelFR }, 0f);
            }

            // Service brakes
            WheelPhysicsHelper.ApplyBrakeTorque(new[] { wheelFL, wheelFR, wheelRL, wheelRR }, brakeInput * maxBrakeTorque);

            // Handbrake (rear wheels)
            if (InputManagerProxy.HandBrake > 0.5f)
            {
                if (wheelRL != null) wheelRL.brakeTorque = handBrakeTorque;
                if (wheelRR != null) wheelRR.brakeTorque = handBrakeTorque;
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            if (StabilizersDown) return;

            float targetSteer = InputManagerProxy.Steer;
            _currentSmoothedSteer = Mathf.Lerp(_currentSmoothedSteer, targetSteer, Time.fixedDeltaTime * steerSensitivity);

            float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
            float speedFactor = Mathf.Clamp01(speedKmh / 35f);
            float effectiveSteerAngle = Mathf.Lerp(maxSteerAngle, 16f, speedFactor);

            if (wheelFL != null) wheelFL.steerAngle = _currentSmoothedSteer * effectiveSteerAngle;
            if (wheelFR != null) wheelFR.steerAngle = _currentSmoothedSteer * effectiveSteerAngle;
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

        private void ConfigureWheelFriction(WheelCollider wc)
        {
            if (wc == null) return;

            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.extremumSlip = 0.40f;
            fwd.extremumValue = 1.0f;
            fwd.asymptoteSlip = 0.80f;
            fwd.asymptoteValue = 0.70f;
            fwd.stiffness = 1.5f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.35f;
            side.extremumValue = 1.0f;
            side.asymptoteSlip = 0.65f;
            side.asymptoteValue = 0.80f;
            side.stiffness = 1.6f;
            wc.sidewaysFriction = side;
        }

        // ── Front Loader API ───────────────────────────────────────────────
        public void ToggleFrontLoader()
        {
            isLoaderRaised = !isLoaderRaised;
        }

        public void SetLoaderBoomAngle(float angle)
        {
            loaderBoomAngle = Mathf.Clamp(angle, -10f, 45f);
        }

        public void SetLoaderBucketAngle(float angle)
        {
            loaderBucketAngle = Mathf.Clamp(angle, -35f, 45f);
        }

        // ── Rear Backhoe & Digging API ─────────────────────────────────────
        public void SetBackhoeAngles(float swing, float boom, float dipper, float bucket)
        {
            backhoeSwingAngle  = Mathf.Clamp(swing, -80f, 80f);
            backhoeBoomAngle   = Mathf.Clamp(boom, -25f, 55f);
            backhoeDipperAngle = Mathf.Clamp(dipper, -40f, 40f);
            backhoeBucketAngle = Mathf.Clamp(bucket, -60f, 60f);
        }

        public void TriggerDigStroke()
        {
            _isPerformingDigCycle = true;
            _digCycleTimer = 0f;
            DeployStabilizers();
        }

        public void DigAtGround()
        {
            Vector3 digPos = transform.position - transform.forward * 3.5f + Vector3.up * 0.1f;
            if (backhoeBucketTip != null) digPos = backhoeBucketTip.position;

            if (terrainDeformer != null)
            {
                terrainDeformer.DigAt(digPos, 1.2f, 0.4f);
            }
            soilPayload = Mathf.Clamp01(soilPayload + 0.35f);
            Debug.Log($"[JCBController] DigAtGround performed at {digPos}. Soil payload: {soilPayload:P0}");
        }

        // ── Stabilizers API ────────────────────────────────────────────────
        public void DeployStabilizers()
        {
            StabilizersDown  = true;
            _stabLeftTarget  = Quaternion.Euler(stabDownRotation);
            _stabRightTarget = Quaternion.Euler(stabDownRotation);
        }

        public void LowerStabilizers() => DeployStabilizers();

        public void RetractStabilizers()
        {
            StabilizersDown  = false;
            _stabLeftTarget  = Quaternion.Euler(stabUpRotation);
            _stabRightTarget = Quaternion.Euler(stabUpRotation);
        }

        public void RaiseStabilizers() => RetractStabilizers();

        public void ToggleStabilizers()
        {
            if (StabilizersDown) RetractStabilizers();
            else DeployStabilizers();
        }

        public void UpdateStabilizers()
        {
            if (stabilizerLeft != null)
                stabilizerLeft.localRotation = Quaternion.RotateTowards(stabilizerLeft.localRotation, _stabLeftTarget, stabSpeed * Time.fixedDeltaTime);

            if (stabilizerRight != null)
                stabilizerRight.localRotation = Quaternion.RotateTowards(stabilizerRight.localRotation, _stabRightTarget, stabSpeed * Time.fixedDeltaTime);
        }

        // ── Articulated Arms Update ────────────────────────────────────────
        public void UpdateArticulatedArms()
        {
            // Front Loader target interpolation
            float targetBoom = isLoaderRaised ? 35f : -5f;
            float targetBucket = isLoaderRaised ? 25f : -15f;
            loaderBoomAngle = Mathf.MoveTowards(loaderBoomAngle, targetBoom, loaderSpeed * Time.fixedDeltaTime);
            loaderBucketAngle = Mathf.MoveTowards(loaderBucketAngle, targetBucket, loaderSpeed * Time.fixedDeltaTime);

            if (loaderBoomTransform != null)
                loaderBoomTransform.localRotation = Quaternion.Euler(loaderBoomAngle, 0f, 0f);
            if (loaderBucketTransform != null)
                loaderBucketTransform.localRotation = Quaternion.Euler(loaderBucketAngle, 0f, 0f);

            // Automated Dig Cycle animation
            if (_isPerformingDigCycle)
            {
                _digCycleTimer += Time.fixedDeltaTime;
                if (_digCycleTimer < 1.0f)
                {
                    // Phase 1: Reach down into ground
                    backhoeBoomAngle = Mathf.MoveTowards(backhoeBoomAngle, 35f, backhoeSpeed * Time.fixedDeltaTime);
                    backhoeDipperAngle = Mathf.MoveTowards(backhoeDipperAngle, 25f, backhoeSpeed * Time.fixedDeltaTime);
                    backhoeBucketAngle = Mathf.MoveTowards(backhoeBucketAngle, -40f, backhoeSpeed * Time.fixedDeltaTime);
                }
                else if (_digCycleTimer < 2.0f)
                {
                    // Phase 2: Curl bucket and scoop soil
                    backhoeBucketAngle = Mathf.MoveTowards(backhoeBucketAngle, 45f, backhoeSpeed * 1.5f * Time.fixedDeltaTime);
                    backhoeDipperAngle = Mathf.MoveTowards(backhoeDipperAngle, -15f, backhoeSpeed * Time.fixedDeltaTime);
                }
                else if (_digCycleTimer < 3.0f)
                {
                    // Phase 3: Lift boom and dig terrain
                    backhoeBoomAngle = Mathf.MoveTowards(backhoeBoomAngle, -15f, backhoeSpeed * Time.fixedDeltaTime);
                    if (_digCycleTimer > 2.2f && soilPayload < 0.2f)
                    {
                        DigAtGround();
                    }
                }
                else
                {
                    _isPerformingDigCycle = false;
                }
            }

            // Apply Backhoe rotations
            if (backhoeSwingTransform != null)
                backhoeSwingTransform.localRotation = Quaternion.Euler(0f, backhoeSwingAngle, 0f);
            if (backhoeBoomTransform != null)
                backhoeBoomTransform.localRotation = Quaternion.Euler(backhoeBoomAngle, 0f, 0f);
            if (backhoeDipperTransform != null)
                backhoeDipperTransform.localRotation = Quaternion.Euler(backhoeDipperAngle, 0f, 0f);
            if (backhoeBucketTransform != null)
                backhoeBucketTransform.localRotation = Quaternion.Euler(backhoeBucketAngle, 0f, 0f);
        }

        private void HandleManualInputs()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            // Joystick manual adjustments when stationary
            if (Mathf.Abs(input.LeftJoystick.y) > 0.1f)
            {
                loaderBoomAngle += input.LeftJoystick.y * loaderSpeed * Time.deltaTime;
                loaderBoomAngle = Mathf.Clamp(loaderBoomAngle, -10f, 45f);
            }
            if (Mathf.Abs(input.LeftJoystick.x) > 0.1f)
            {
                loaderBucketAngle += input.LeftJoystick.x * loaderSpeed * Time.deltaTime;
                loaderBucketAngle = Mathf.Clamp(loaderBucketAngle, -35f, 45f);
            }
        }

        // ── Engine Sound ───────────────────────────────────────────────────
        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = CreateProceduralJCBDieselClip();
            }

            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.loop = true;
                engineAudioSource.Play();
            }

            float load = Mathf.Clamp01(Mathf.Abs(currentSpeed) / topSpeedKmh);
            float rpmRatio = Mathf.Lerp(0.2f, 1.0f, load + (_isPerformingDigCycle ? 0.35f : 0f));
            engineAudioSource.pitch = Mathf.Lerp(0.70f, 1.80f, rpmRatio);
            engineAudioSource.volume = Mathf.Lerp(0.40f, 0.88f, 0.4f + 0.6f * Mathf.Abs(InputManagerProxy.Throttle));
        }

        public static AudioClip CreateProceduralJCBDieselClip()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float fundamentalFreq = 30f; // ~30 Hz 4-cylinder turbocharged diesel idle
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float s2 = 0.55f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 2f * t);
                float s3 = 0.30f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 3f * t);
                float pumpWhine = 0.08f * Mathf.Sin(2f * Mathf.PI * 440f * t); // Hydraulic pump hum
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.1f;
                samples[i] = Mathf.Clamp((s1 + s2 + s3 + pumpWhine + noise) * 0.36f, -1f, 1f);
            }

            var clip = AudioClip.Create("Synthesized_JCB_Diesel", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ── Wheel Mesh Sync ────────────────────────────────────────────────
        private void SyncWheelMeshes()
        {
            WheelPhysicsHelper.UpdateWheelPose(wheelFL, meshFL);
            WheelPhysicsHelper.UpdateWheelPose(wheelFR, meshFR);
            WheelPhysicsHelper.UpdateWheelPose(wheelRL, meshRL);
            WheelPhysicsHelper.UpdateWheelPose(wheelRR, meshRR);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 3.5f)
            {
                float dmg = (impactForce - 3.5f) * 1.5f;
                damageLevel = Mathf.Clamp(damageLevel + dmg, 0f, 100f);
            }
        }

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.012f * Mathf.Abs(InputManagerProxy.Throttle);
        }

        public override string GetVehicleInfo() => "JCB Backhoe";
    }
}
