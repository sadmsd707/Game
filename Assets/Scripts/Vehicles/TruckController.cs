using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Heavy truck controller with tipper bed and trailer hitch.
    /// Inherits VehicleController. Uses WheelPhysicsHelper for WheelCollider physics.
    /// Mass: 8000 kg (vs 1500 kg for car). Air-brake feel via progressive build-up.
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

        // ── Tuning ────────────────────────────────────────────────────────
        [Header("Truck Tuning")]
        public float maxSteerAngle   = 25f;
        public float motorTorque     = 6000f;
        public float maxBrakeTorque  = 12000f;
        public float handBrakeTorque = 15000f;
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

        // ── Payload ───────────────────────────────────────────────────────
        [Header("Cargo Payload")]
        public float currentPayload;
        public float maxPayload = 15000f; // 15 tons

        public void AddPayload(float amount)
        {
            currentPayload = Mathf.Clamp(currentPayload + amount, 0f, maxPayload);
            Debug.Log($"[TruckController] Added payload: {amount:F1}. Total: {currentPayload:F1}/{maxPayload:F1}");
        }
        [SerializeField] private float tipperSpeed = 15f;

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
        private float _airBrakeBuildRate = 2000f;  // torque units / second

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
            rb.centerOfMass = new Vector3(0f, -0.8f, 0.5f);

            _driveWheels = new[] { rearLeft, rearRight };
            _steerWheels = new[] { frontLeft, frontRight };
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;
            ApplyDriving();
            ApplySteering();
            SyncWheelMeshes();
            AutoShift();
            UpdateTipperBed();
            ConsumeFuel();
        }

        protected override void Update()
        {
            base.Update();
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

            // Handbrake (rear only)
            if (InputManagerProxy.HandBrake > 0.5f)
            {
                if (rearLeft != null) rearLeft.brakeTorque = handBrakeTorque;
                if (rearRight != null) rearRight.brakeTorque = handBrakeTorque;
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            float steer = InputManagerProxy.Steer;
            WheelPhysicsHelper.ApplySteering(_steerWheels, steer, maxSteerAngle);
        }

        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();
            engineAudioSource.pitch = 0.5f + (engineRPM / maxRPM) * 1.5f;
        }

        public override string GetVehicleInfo() => "Truck";

        // ── Trailer API ───────────────────────────────────────────────────

        /// <summary>Attaches a trailer to this truck's hitch point.</summary>
        public void AttachTrailer(TrailerController t)
        {
            if (t == null || isTrailerAttached) return;
            attachedTrailer   = t;
            isTrailerAttached = true;
            t.AttachToTruck(trailerHitchPoint);
        }

        /// <summary>Detaches the currently coupled trailer.</summary>
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

        private void UpdateTipperBed()
        {
            if (tipperBed == null) return;

            float target = isDumping ? 60f : 0f;
            tipperAngle = Mathf.MoveTowards(tipperAngle, target, tipperSpeed * Time.fixedDeltaTime);
            tipperBed.localRotation = Quaternion.Euler(tipperAngle, 0f, 0f);
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

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.015f * Mathf.Abs(InputManagerProxy.Throttle);
        }
    }
}
