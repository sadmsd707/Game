using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>Implement types a tractor can carry on its three-point hitch.</summary>
    public enum AttachedImplement
    {
        None,
        Plough,
        Trailer,
        FrontLoader
    }

    /// <summary>
    /// Farm tractor controller. Inherits VehicleController.
    /// Features: 4WD toggle, PTO, 3-point hitch (raise/lower), anti-roll stability,
    /// separate throttle pedal, and front/rear implement attachment.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class TractorController : VehicleController
    {
        // ── Wheel colliders ───────────────────────────────────────────────
        [Header("Wheel Colliders")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;

        [Header("Wheel Meshes")]
        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        // ── Suspension & Stability ────────────────────────────────────────
        [Header("Suspension & Stability")]
        public float antiRollStiffness = 9000f;
        public float downforceCoefficient = 2.0f;
        public float steerSensitivity = 7.0f;
        private float _currentSmoothedSteer;

        // ── Tuning ────────────────────────────────────────────────────────
        [Header("Tuning")]
        public float maxSteerAngle   = 40f;
        public float motorTorque     = 6500f;
        public float maxBrakeTorque  = 10000f;
        public float handBrakeTorque = 15000f;
        public float idleRPM         = 600f;

        // ── Gearbox ───────────────────────────────────────────────────────
        [Header("Gearbox")]
        public float[] gearRatios = { -5f, 0f, 6f, 4f, 2.5f, 1.5f };
        public float   finalDriveRatio = 5.0f;
        public int     currentGearIndex = 2;

        // ── 4WD ───────────────────────────────────────────────────────────
        [Header("4WD")]
        public bool is4WD = true;

        // ── PTO ───────────────────────────────────────────────────────────
        [Header("Power Take-Off (PTO)")]
        public bool  ptoActive = true;
        public float ptoRPM;             // read-only runtime value
        [SerializeField] private float ptoRatioToEngine = 0.4f;

        // ── Throttle / Clutch ─────────────────────────────────────────────
        [Header("Throttle & Clutch")]
        [Range(0f, 1f)] public float throttlePedal;    // separate from drive throttle
        [Range(0f, 1f)] public float clutchPedal;      // 0 = engaged, 1 = fully depressed
        public bool inGear = true;

        // ── Three-point hitch ─────────────────────────────────────────────
        [Header("Three-Point Hitch")]
        public Transform         rearHitch;
        public Transform         frontHitch;
        public AttachedImplement attachedImplement = AttachedImplement.None;

        [SerializeField] public Transform hitchArmTransform;   // visual arm that raises/lowers
        [SerializeField] private float       hitchLiftAngleMin = -20f;
        [SerializeField] private float       hitchLiftAngleMax =  20f;
        [Range(0f, 1f)] public  float        hitchPosition = 1f; // 0 = lowered (working), 1 = raised (transport)

        // ── Engine torque curve ───────────────────────────────────────────
        [Header("Engine Torque Curve")]
        public AnimationCurve engineTorqueCurve = AnimationCurve.EaseInOut(0, 0.4f, 2200f, 1f);

        // ── Lights / Horn ─────────────────────────────────────────────────
        [Header("Headlights")]
        public Light headlightLeft;
        public Light headlightRight;
        private bool _headlightsOn;

        [Header("Horn")]
        public AudioSource hornSource;
        public AudioClip   hornClip;

        // ── Lifecycle ─────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            maxRPM = 2200f;
            rb.mass = 4000f;
            rb.centerOfMass = new Vector3(0f, -0.2f, 0.1f);
            ptoActive = true;

            ConfigureWheelFriction(frontLeft);
            ConfigureWheelFriction(frontRight);
            ConfigureWheelFriction(rearLeft);
            ConfigureWheelFriction(rearRight);
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;
            ReadThrottleAndClutch();
            ApplyDriving();
            ApplySteering();
            ApplyAntiRoll(frontLeft, frontRight);
            ApplyAntiRoll(rearLeft, rearRight);
            ApplyAerodynamicDownforce();
            SyncWheelMeshes();
            UpdatePTO();
            UpdateHitchArm();
            ConsumeFuel();
        }

        protected override void Update()
        {
            base.Update();
            UpdateEngineSound();
            HandleExtras();
        }

        // ── Abstract overrides ────────────────────────────────────────────

        public override void ApplyDriving()
        {
            float rawThrottle = InputManagerProxy.Throttle;
            float rawBrake    = InputManagerProxy.Brake;

            // Seamless auto-reverse when stopped
            if (currentSpeed < 1.5f && rawBrake > 0.1f && rawThrottle < 0.05f)
            {
                currentGearIndex = 0; // Reverse
            }
            else if (rawThrottle > 0.1f && currentGearIndex == 0)
            {
                currentGearIndex = 2; // 1st forward gear
            }

            throttlePedal = (currentGearIndex == 0) ? rawBrake : rawThrottle;
            float brakeInput = (currentGearIndex == 0) ? rawThrottle : rawBrake;

            // Clutch reduces torque transfer: fully depressed = 0 torque
            float clutchFactor = 1f - Mathf.Clamp01(clutchPedal);
            bool  gearEngaged  = inGear && currentGearIndex != 1;

            float ratio    = (gearRatios != null && currentGearIndex < gearRatios.Length) ? gearRatios[currentGearIndex] : 1f;
            float wheelRPM = (rearLeft != null) ? WheelPhysicsHelper.GetWheelRPM(rearLeft) : 0f;
            engineRPM = Mathf.Clamp(
                WheelPhysicsHelper.ComputeEngineRPM(wheelRPM, ratio, finalDriveRatio),
                idleRPM, maxRPM);

            // Throttle pedal sets desired RPM; actual power is from that RPM + torque curve
            float desiredRPM = idleRPM + throttlePedal * (maxRPM - idleRPM);
            engineRPM = Mathf.Lerp(engineRPM, desiredRPM, Time.fixedDeltaTime * 10f);

            float torqueMult    = engineTorqueCurve.Evaluate(engineRPM);
            float appliedTorque = gearEngaged
                ? throttlePedal * motorTorque * torqueMult * clutchFactor
                : 0f;

            if (is4WD)
            {
                WheelPhysicsHelper.ApplyMotorTorque(
                    new[] { frontLeft, frontRight, rearLeft, rearRight },
                    appliedTorque * 0.5f);
            }
            else
            {
                WheelPhysicsHelper.ApplyMotorTorque(
                    new[] { rearLeft, rearRight }, appliedTorque);
                WheelPhysicsHelper.ApplyMotorTorque(
                    new[] { frontLeft, frontRight }, 0f);
            }

            // Brakes
            WheelPhysicsHelper.ApplyBrakeTorque(
                new[] { frontLeft, frontRight, rearLeft, rearRight },
                brakeInput * maxBrakeTorque);

            // Handbrake (rear wheels)
            if (InputManagerProxy.HandBrake > 0.5f)
            {
                if (rearLeft != null) rearLeft.brakeTorque = handBrakeTorque;
                if (rearRight != null) rearRight.brakeTorque = handBrakeTorque;
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            float targetSteer = InputManagerProxy.Steer;
            _currentSmoothedSteer = Mathf.Lerp(_currentSmoothedSteer, targetSteer, Time.fixedDeltaTime * steerSensitivity);

            float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
            float speedFactor = Mathf.Clamp01(speedKmh / 50f);
            float effectiveSteerAngle = Mathf.Lerp(maxSteerAngle, 18f, speedFactor);

            if (frontLeft != null) frontLeft.steerAngle = _currentSmoothedSteer * effectiveSteerAngle;
            if (frontRight != null) frontRight.steerAngle = _currentSmoothedSteer * effectiveSteerAngle;
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

        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = CreateProceduralTractorDieselClip();
            }

            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.loop = true;
                engineAudioSource.Play();
            }

            float rpmRatio = Mathf.Clamp01(engineRPM / maxRPM);
            engineAudioSource.pitch = Mathf.Lerp(0.70f, 1.90f, rpmRatio);
            engineAudioSource.volume = Mathf.Lerp(0.40f, 0.90f, 0.4f + 0.6f * Mathf.Abs(throttlePedal));
        }

        public static AudioClip CreateProceduralTractorDieselClip()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float fundamentalFreq = 26f; // ~26 Hz 3-cylinder agricultural diesel idle chug
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float s2 = 0.5f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 2f * t);
                float s3 = 0.4f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 3f * t);
                float pulse = (t % (1f / fundamentalFreq) < 0.006f) ? 0.35f : 0f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.1f;
                samples[i] = Mathf.Clamp((s1 + s2 + s3 + pulse + noise) * 0.35f, -1f, 1f);
            }

            var clip = AudioClip.Create("Synthesized_Tractor_Diesel", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public override string GetVehicleInfo() => "Tractor";

        // ── Hitch API ─────────────────────────────────────────────────────

        public void AttachImplement(AttachedImplement type)
        {
            attachedImplement = type;
        }

        public void DetachImplement()
        {
            attachedImplement = AttachedImplement.None;
        }

        public void RaiseLowerHitch(float amount)
        {
            hitchPosition = Mathf.Clamp01(amount);
        }

        public void ToggleHitch()
        {
            hitchPosition = (hitchPosition > 0.5f) ? 0f : 1f;
        }

        public void UpdateHitchArm()
        {
            if (hitchArmTransform == null) return;
            // 0 = lowered (-20 deg into ground/working), 1 = raised (+20 deg transport)
            float angle = Mathf.Lerp(hitchLiftAngleMin, hitchLiftAngleMax, hitchPosition);
            hitchArmTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // ── PTO ───────────────────────────────────────────────────────────
        private void UpdatePTO()
        {
            ptoRPM = ptoActive ? engineRPM * ptoRatioToEngine : 0f;
        }

        // ── Throttle / Clutch polling ─────────────────────────────────────
        private void ReadThrottleAndClutch()
        {
            throttlePedal = InputManagerProxy.Throttle;

#if ENABLE_INPUT_SYSTEM
            clutchPedal = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.isPressed ? 1f : 0f;
#else
            clutchPedal = Input.GetKey(KeyCode.LeftControl) ? 1f : 0f;
#endif
        }

        // ── Wheel mesh sync ───────────────────────────────────────────────
        private void SyncWheelMeshes()
        {
            WheelPhysicsHelper.UpdateWheelPose(frontLeft,  frontLeftMesh);
            WheelPhysicsHelper.UpdateWheelPose(frontRight, frontRightMesh);
            WheelPhysicsHelper.UpdateWheelPose(rearLeft,   rearLeftMesh);
            WheelPhysicsHelper.UpdateWheelPose(rearRight,  rearRightMesh);
        }

        // ── Extras ───────────────────────────────────────────────────────
        private void HandleExtras()
        {
            if (InputManagerProxy.HeadlightsToggled)
            {
                _headlightsOn = !_headlightsOn;
                if (headlightLeft)  headlightLeft.enabled  = _headlightsOn;
                if (headlightRight) headlightRight.enabled = _headlightsOn;
            }

            if (InputManagerProxy.HornHeld)
            {
                if (hornSource != null && hornClip != null && !hornSource.isPlaying)
                    hornSource.PlayOneShot(hornClip);
            }
            else if (hornSource != null && hornSource.isPlaying)
            {
                hornSource.Stop();
            }
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
                fuelLevel -= Time.fixedDeltaTime * 0.012f * throttlePedal;
        }
    }
}
