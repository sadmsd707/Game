using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Heavy truck controller with tipper bed and trailer hitch.
    /// Inherits VehicleController. Uses WheelPhysicsHelper for WheelCollider physics.
    /// Mass: 8000 kg. Air-brake feel via progressive build-up and dual sway-bar anti-roll.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class TruckController : VehicleController
    {
        // ── Wheel colliders ───────────────────────────────────────────────
        [Header("Wheel Colliders (Truck)")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;

        // ── Wheel meshes ──────────────────────────────────────────────────
        [Header("Wheel Meshes")]
        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        // ── Suspension & Stability ────────────────────────────────────────
        [Header("Suspension & Stability")]
        public float antiRollStiffness = 15000f;
        public float downforceCoefficient = 4.0f;
        public float steerSensitivity = 6.0f;
        private float _currentSmoothedSteer;

        // ── Tuning ────────────────────────────────────────────────────────
        [Header("Truck Tuning")]
        public float maxSteerAngle   = 28f;
        public float motorTorque     = 7000f;
        public float maxBrakeTorque  = 16000f;
        public float handBrakeTorque = 20000f;
        public float idleRPM         = 600f;

        // ── Gearbox ───────────────────────────────────────────────────────
        [Header("Gearbox")]
        public float[] gearRatios = { -4.5f, 0f, 5.0f, 3.5f, 2.5f, 1.8f, 1.3f, 1.0f };
        public float   finalDriveRatio = 4.11f;
        public int     currentGearIndex = 2;
        public float   upShiftRPM   = 2600f;
        public float   downShiftRPM = 1000f;

        // ── Engine torque curve ───────────────────────────────────────────
        [Header("Engine Torque Curve")]
        public AnimationCurve engineTorqueCurve = AnimationCurve.EaseInOut(0, 0.4f, 3000f, 1f);

        // ── Trailer hitch ─────────────────────────────────────────────────
        [Header("Trailer Hitch")]
        public Transform         trailerHitchPoint;
        public TrailerController attachedTrailer;
        public bool              isTrailerAttached;

        // ── Tipper bed ────────────────────────────────────────────────────
        [Header("Tipper Bed")]
        public Transform tipperBed;
        public bool      isDumping;
        public float     tipperAngle;    // degrees: 0 = flat, 60 = fully raised
        [SerializeField] private float tipperSpeed = 20f;

        // ── Payload ───────────────────────────────────────────────────────
        [Header("Cargo Payload")]
        public float currentPayload;
        public float maxPayload = 15000f; // 15 tons

        public void AddPayload(float amount)
        {
            currentPayload = Mathf.Clamp(currentPayload + amount, 0f, maxPayload);
            Debug.Log($"[TruckController] Added payload: {amount:F1}. Total: {currentPayload:F1}/{maxPayload:F1}");
        }

        // ── Lights ────────────────────────────────────────────────────────
        [Header("Headlights")]
        public Light headlightLeft;
        public Light headlightRight;
        private bool _headlightsOn;

        // ── Horn ──────────────────────────────────────────────────────────
        [Header("Horn")]
        public AudioSource hornSource;
        public AudioClip   hornClip;

        // ── Air-brake state ───────────────────────────────────────────────
        private float _currentBrakeTorque;
        private float _airBrakeBuildRate = 16000f;  // Fast pneumatic build-up (0.75s to full lock)

        // ── Internal ──────────────────────────────────────────────────────
        private WheelCollider[] _driveWheels;
        private WheelCollider[] _steerWheels;
        private float           _autoShiftCooldown;

        // ── Lifecycle ─────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            maxRPM = 3000f;
            rb.mass = 8000f;
            rb.centerOfMass = new Vector3(0f, -0.6f, 0.4f);

            _driveWheels = new[] { rearLeft, rearRight };
            _steerWheels = new[] { frontLeft, frontRight };

            ConfigureWheelFriction(frontLeft);
            ConfigureWheelFriction(frontRight);
            ConfigureWheelFriction(rearLeft);
            ConfigureWheelFriction(rearRight);
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;
            ApplyDriving();
            ApplySteering();
            ApplyAntiRoll(frontLeft, frontRight);
            ApplyAntiRoll(rearLeft, rearRight);
            ApplyAerodynamicDownforce();
            SyncWheelMeshes();
            AutoShift();
            UpdateTipperBed();
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
            if (_driveWheels == null || _driveWheels.Length == 0 || _driveWheels[0] == null)
            {
                if (rearLeft != null && rearRight != null)
                    _driveWheels = new[] { rearLeft, rearRight };
            }
            if (_steerWheels == null || _steerWheels.Length == 0 || _steerWheels[0] == null)
            {
                if (frontLeft != null && frontRight != null)
                    _steerWheels = new[] { frontLeft, frontRight };
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
                currentGearIndex = 2; // 1st forward gear
            }

            float throttle   = (currentGearIndex == 0) ? rawBrake : rawThrottle;
            float brakeInput = (currentGearIndex == 0) ? rawThrottle : rawBrake;

            float ratio    = (gearRatios != null && currentGearIndex < gearRatios.Length) ? gearRatios[currentGearIndex] : 1.0f;
            float wheelRPM = (rearLeft != null) ? WheelPhysicsHelper.GetWheelRPM(rearLeft) : 0f;
            engineRPM = Mathf.Clamp(
                WheelPhysicsHelper.ComputeEngineRPM(wheelRPM, ratio, finalDriveRatio),
                idleRPM, maxRPM);

            float torqueMult    = engineTorqueCurve.Evaluate(engineRPM);
            float appliedTorque = (currentGearIndex == 1) ? 0f
                                 : throttle * motorTorque * torqueMult;

            if (_driveWheels != null)
                WheelPhysicsHelper.ApplyMotorTorque(_driveWheels, appliedTorque);

            // Progressive air-brake build-up
            float targetBrake = brakeInput * maxBrakeTorque;
            _currentBrakeTorque = Mathf.MoveTowards(
                _currentBrakeTorque, targetBrake,
                _airBrakeBuildRate * Time.fixedDeltaTime);

            WheelPhysicsHelper.ApplyBrakeTorque(
                new[] { frontLeft, frontRight, rearLeft, rearRight },
                _currentBrakeTorque);

            // Handbrake (rear dual wheels locked)
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
            float speedFactor = Mathf.Clamp01(speedKmh / 90f);
            float effectiveSteerAngle = Mathf.Lerp(maxSteerAngle, 12f, speedFactor);

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
            fwd.extremumSlip = 0.45f;
            fwd.extremumValue = 1.0f;
            fwd.asymptoteSlip = 0.85f;
            fwd.asymptoteValue = 0.75f;
            fwd.stiffness = 1.6f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.40f;
            side.extremumValue = 1.0f;
            side.asymptoteSlip = 0.70f;
            side.asymptoteValue = 0.85f;
            side.stiffness = 1.8f;
            wc.sidewaysFriction = side;
        }

        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = CreateProceduralTruckDieselClip();
            }

            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.loop = true;
                engineAudioSource.Play();
            }

            float rpmRatio = Mathf.Clamp01(engineRPM / maxRPM);
            engineAudioSource.pitch = Mathf.Lerp(0.65f, 1.85f, rpmRatio);
            engineAudioSource.volume = Mathf.Lerp(0.40f, 0.90f, 0.4f + 0.6f * Mathf.Abs(InputManagerProxy.Throttle));
        }

        public static AudioClip CreateProceduralTruckDieselClip()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float fundamentalFreq = 32f; // ~32 Hz heavy 6-cylinder diesel idle
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float s2 = 0.6f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 2f * t);
                float s3 = 0.35f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 3f * t);
                float s4 = 0.2f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 6f * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.12f;
                samples[i] = Mathf.Clamp((s1 + s2 + s3 + s4 + noise) * 0.35f, -1f, 1f);
            }

            var clip = AudioClip.Create("Synthesized_Truck_Diesel", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public override string GetVehicleInfo() => "Truck";

        // ── Trailer API ───────────────────────────────────────────────────

        public void AttachTrailer(TrailerController t)
        {
            if (t == null || isTrailerAttached) return;
            attachedTrailer   = t;
            isTrailerAttached = true;
            t.AttachToTruck(trailerHitchPoint);
        }

        public void DetachTrailer()
        {
            if (!isTrailerAttached || attachedTrailer == null) return;
            attachedTrailer.Detach();
            attachedTrailer   = null;
            isTrailerAttached = false;
        }

        // ── Tipper API ────────────────────────────────────────────────────

        public void StartDumping() => isDumping = true;
        public void StopDumping()  => isDumping = false;
        public void ToggleDumping() => isDumping = !isDumping;

        public void UpdateTipperBed()
        {
            if (tipperBed == null) return;

            float target = isDumping ? 60f : 0f;
            tipperAngle = Mathf.MoveTowards(tipperAngle, target, tipperSpeed * Time.fixedDeltaTime);
            tipperBed.localRotation = Quaternion.Euler(tipperAngle, 0f, 0f);

            // Unload cargo payload when bed is tilted beyond 25 degrees
            if (tipperAngle > 25f && currentPayload > 0f)
            {
                currentPayload = Mathf.MoveTowards(currentPayload, 0f, 6000f * Time.fixedDeltaTime);
            }
        }

        // ── Auto-shift ────────────────────────────────────────────────────
        private void AutoShift()
        {
            _autoShiftCooldown -= Time.fixedDeltaTime;
            if (_autoShiftCooldown > 0f || currentGearIndex < 2) return;

            if (engineRPM > upShiftRPM && currentGearIndex < gearRatios.Length - 1)
            {
                currentGearIndex++;
                _autoShiftCooldown = 0.8f;
            }
            else if (engineRPM < downShiftRPM && currentGearIndex > 2)
            {
                currentGearIndex--;
                _autoShiftCooldown = 0.8f;
            }
        }

        // ── Mesh sync ─────────────────────────────────────────────────────
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
            if (impactForce > 4.0f)
            {
                float dmg = (impactForce - 4.0f) * 1.8f;
                damageLevel = Mathf.Clamp(damageLevel + dmg, 0f, 100f);
            }
        }

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.015f * Mathf.Abs(InputManagerProxy.Throttle);
        }
    }
}
