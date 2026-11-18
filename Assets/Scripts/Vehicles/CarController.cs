using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Driveable car controller. Inherits VehicleController.
    /// Uses WheelPhysicsHelper for all WheelCollider interactions.
    /// Requires InputManager singleton (HeavyVehiclesWorld.Controls namespace).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class CarController : VehicleController
    {
        // ── Wheel colliders ───────────────────────────────────────────────
        [Header("Wheel Colliders")]
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

        // ── Vehicle tuning ────────────────────────────────────────────────
        [Header("Tuning")]
        public float maxSteerAngle   = 35f;
        public float motorTorque     = 2800f;
        public float brakeTorque     = 8500f;
        public float handBrakeTorque = 12000f;
        public float idleRPM         = 800f;

        [Header("Stability & Suspension (Anti-Roll & Downforce)")]
        [Tooltip("Prevents body rollover and flipping during high-speed sharp turns.")]
        public float antiRollStiffness = 6000f;
        [Tooltip("Aerodynamic downforce to keep vehicle firmly planted on the road at high speeds.")]
        public float downforceCoefficient = 2.5f;
        [Tooltip("Rate at which steering input interpolates to prevent sudden twitching.")]
        public float steerSensitivity = 8.0f;

        // ── Gear ratios: index 0=R, 1=N, 2=D1 ... 6=D5 ───────────────────
        [Header("Gearbox")]
        public float[] gearRatios = { -3.5f, 0f, 3.5f, 2.1f, 1.4f, 1.0f, 0.8f };
        public float   finalDriveRatio = 3.73f;
        public int     currentGearIndex = 2;   // Start in D1
        public float   upShiftRPM   = 5500f;
        public float   downShiftRPM = 1500f;

        // ── Engine curve ──────────────────────────────────────────────────
        [Header("Engine Torque Curve")]
        public AnimationCurve engineTorqueCurve = AnimationCurve.EaseInOut(0, 0.5f, 7000f, 1f);

        // ── Lights ────────────────────────────────────────────────────────
        [Header("Headlights")]
        public Light headlightLeft;
        public Light headlightRight;
        private bool _headlightsOn;

        // ── Horn ──────────────────────────────────────────────────────────
        [Header("Horn")]
        public AudioSource hornSource;
        public AudioClip   hornClip;

        // ── Internal ──────────────────────────────────────────────────────
        private WheelCollider[] _driveWheels;
        private WheelCollider[] _steerWheels;
        private WheelCollider[] _allWheels;
        private float           _brakeInput;
        private float           _handBrakeInput;
        private float           _autoShiftCooldown;
        private float           _smoothedSteer;

        // ── Lifecycle ─────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            maxRPM = 7000f;

            rb.mass = 1500f;
            // Low centre of mass to prevent rollovers and flipping
            rb.centerOfMass = new Vector3(0f, -0.35f, 0.1f);

            _driveWheels = new[] { rearLeft, rearRight };
            _steerWheels = new[] { frontLeft, frontRight };
            _allWheels   = new[] { frontLeft, frontRight, rearLeft, rearRight };

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
                _driveWheels = new[] { rearLeft, rearRight };
                _steerWheels = new[] { frontLeft, frontRight };
                _allWheels   = new[] { frontLeft, frontRight, rearLeft, rearRight };
            }

            float throttle = InputManagerProxy.Throttle;
            _brakeInput     = InputManagerProxy.Brake;
            _handBrakeInput = InputManagerProxy.HandBrake;

            if (rb != null)
                currentSpeed = rb.linearVelocity.magnitude * 3.6f; // km/h

            // Automatic Forward / Reverse Transition
            if (currentGearIndex >= 2) // In Forward gear
            {
                if (currentSpeed < 1.5f && _brakeInput > 0.3f && throttle < 0.1f)
                {
                    currentGearIndex = 0; // Switch to Reverse
                    currentGear = GearState.Reverse;
                }
            }
            else if (currentGearIndex == 0) // In Reverse gear
            {
                if (currentSpeed < 1.5f && throttle > 0.3f && _brakeInput < 0.1f)
                {
                    currentGearIndex = 2; // Switch to Drive 1
                    currentGear = GearState.Drive;
                }
            }

            float ratio = (gearRatios != null && currentGearIndex >= 0 && currentGearIndex < gearRatios.Length)
                ? gearRatios[currentGearIndex] : 3.5f;

            float wheelRPM = (rearLeft != null) ? WheelPhysicsHelper.GetWheelRPM(rearLeft) : 0f;
            engineRPM = Mathf.Clamp(
                WheelPhysicsHelper.ComputeEngineRPM(wheelRPM, ratio, finalDriveRatio),
                idleRPM, maxRPM);

            float torqueMult = (engineTorqueCurve != null) ? engineTorqueCurve.Evaluate(engineRPM) : 1f;
            float appliedTorque = 0f;
            float serviceBrake = 0f;

            if (currentGearIndex == 0) // Reverse
            {
                // When in reverse, brake pedal acts as accelerator backwards
                appliedTorque = -_brakeInput * motorTorque * torqueMult;
                serviceBrake  = throttle * brakeTorque;
            }
            else if (currentGearIndex >= 2) // Forward gears
            {
                appliedTorque = throttle * motorTorque * torqueMult;
                serviceBrake  = _brakeInput * brakeTorque;
            }

            WheelPhysicsHelper.ApplyMotorTorque(_driveWheels, appliedTorque);

            // Brakes
            float hbRear = _handBrakeInput > 0.5f ? handBrakeTorque : 0f;
            if (frontLeft != null)  frontLeft.brakeTorque  = serviceBrake;
            if (frontRight != null) frontRight.brakeTorque = serviceBrake;
            if (rearLeft != null)   rearLeft.brakeTorque   = serviceBrake + hbRear;
            if (rearRight != null)  rearRight.brakeTorque  = serviceBrake + hbRear;
        }

        public override void ApplySteering()
        {
            if (_steerWheels == null || _steerWheels.Length == 0 || _steerWheels[0] == null)
            {
                _steerWheels = new[] { frontLeft, frontRight };
            }

            float rawSteer = InputManagerProxy.Steer;
            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, rawSteer, steerSensitivity * Time.fixedDeltaTime);

            // Speed-sensitive steering limiting: prevents sharp twitching and flipping at high speed
            float speedFactor = Mathf.Clamp01(currentSpeed / 120f);
            float steerLimit = Mathf.Lerp(maxSteerAngle, 14f, speedFactor);
            float steer = _smoothedSteer * steerLimit;

            WheelPhysicsHelper.ApplySteering(_steerWheels, steer, steerLimit);
        }

        public void ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            if (left == null || right == null) return;

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
            // Downforce proportional to square of velocity: keeps car grounded firmly at speed
            rb.AddForce(-transform.up * downforceCoefficient * rb.linearVelocity.sqrMagnitude);
        }

        private void ConfigureWheelFriction(WheelCollider wc)
        {
            if (wc == null) return;

            // Stable forward friction (longitudinal)
            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.extremumSlip = 0.4f;
            fwd.extremumValue = 1.0f;
            fwd.asymptoteSlip = 0.8f;
            fwd.asymptoteValue = 0.7f;
            fwd.stiffness = 1.25f;
            wc.forwardFriction = fwd;

            // Stable sideways friction (lateral) - eliminates jitter and unintended drifting
            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.35f;
            side.extremumValue = 1.0f;
            side.asymptoteSlip = 0.65f;
            side.asymptoteValue = 0.8f;
            side.stiffness = 1.4f;
            wc.sidewaysFriction = side;
        }

        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = CreateProceduralEngineClip();
            }

            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.loop = true;
                engineAudioSource.Play();
            }

            float rpmRatio = Mathf.Clamp01(engineRPM / maxRPM);
            engineAudioSource.pitch = Mathf.Lerp(0.75f, 2.3f, rpmRatio);
            engineAudioSource.volume = Mathf.Lerp(0.35f, 0.85f, 0.4f + 0.6f * Mathf.Abs(InputManagerProxy.Throttle));
        }

        public static AudioClip CreateProceduralEngineClip()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float fundamentalFreq = 42f; // ~42 Hz rumble for 4-cylinder idle
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float s2 = 0.5f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 2f * t);
                float s3 = 0.25f * Mathf.Sin(2f * Mathf.PI * fundamentalFreq * 3f * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.08f;
                samples[i] = Mathf.Clamp((s1 + s2 + s3 + noise) * 0.4f, -1f, 1f);
            }

            var clip = AudioClip.Create("Synthesized_Car_Engine", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 3.0f)
            {
                float dmg = (impactForce - 3.0f) * 1.5f;
                damageLevel = Mathf.Clamp(damageLevel + dmg, 0f, 100f);
            }
        }

        public override string GetVehicleInfo() => "Car";

        // ── Auto-shift ────────────────────────────────────────────────────
        private void AutoShift()
        {
            _autoShiftCooldown -= Time.fixedDeltaTime;
            if (_autoShiftCooldown > 0f) return;
            if (currentGearIndex < 2) return; // R or N handled above

            if (engineRPM > upShiftRPM && currentGearIndex < gearRatios.Length - 1)
            {
                currentGearIndex++;
                _autoShiftCooldown = 0.5f;
            }
            else if (engineRPM < downShiftRPM && currentGearIndex > 2)
            {
                currentGearIndex--;
                _autoShiftCooldown = 0.5f;
            }
        }

        // ── Mesh sync ─────────────────────────────────────────────────────
        private void SyncWheelMeshes()
        {
            if (frontLeftMesh != null && frontLeft != null)
                WheelPhysicsHelper.UpdateWheelPose(frontLeft,  frontLeftMesh);
            if (frontRightMesh != null && frontRight != null)
                WheelPhysicsHelper.UpdateWheelPose(frontRight, frontRightMesh);
            if (rearLeftMesh != null && rearLeft != null)
                WheelPhysicsHelper.UpdateWheelPose(rearLeft,   rearLeftMesh);
            if (rearRightMesh != null && rearRight != null)
                WheelPhysicsHelper.UpdateWheelPose(rearRight,  rearRightMesh);
        }

        // ── Extras (lights, horn) ─────────────────────────────────────────
        private void HandleExtras()
        {
            if (InputManagerProxy.HeadlightsToggled)
                ToggleHeadlights();

            if (InputManagerProxy.HornHeld)
                PlayHorn();
            else
                StopHorn();
        }

        private void ToggleHeadlights()
        {
            _headlightsOn = !_headlightsOn;
            if (headlightLeft)  headlightLeft.enabled  = _headlightsOn;
            if (headlightRight) headlightRight.enabled = _headlightsOn;
        }

        private void PlayHorn()
        {
            if (hornSource != null && hornClip != null && !hornSource.isPlaying)
                hornSource.PlayOneShot(hornClip);
        }

        private void StopHorn()
        {
            if (hornSource != null && hornSource.isPlaying)
                hornSource.Stop();
        }

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.01f * Mathf.Abs(InputManagerProxy.Throttle);
        }
    }

    // ── InputManager proxy (safe stub if InputManager not yet present) ────
    public static class InputManagerProxy
    {
        public static float MobileThrottle = 0f;
        public static float MobileBrake    = 0f;
        public static float MobileSteer    = 0f;
        public static float MobileHandBrake = 0f;
        public static bool  MobileHeadlights = false;
        public static bool  MobileHorn = false;

        public static float Throttle
        {
            get
            {
                if (Mathf.Abs(MobileThrottle) > 0.01f) return MobileThrottle;
#if ENABLE_INPUT_SYSTEM
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null) return gp.rightTrigger.ReadValue();
                return UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.wKey.isPressed ? 1f : 0f;
#else
                return Mathf.Max(0f, Input.GetAxis("Vertical"));
#endif
            }
        }

        public static float Brake
        {
            get
            {
                if (Mathf.Abs(MobileBrake) > 0.01f) return MobileBrake;
#if ENABLE_INPUT_SYSTEM
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null) return gp.leftTrigger.ReadValue();
                return UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.sKey.isPressed ? 1f : 0f;
#else
                return Mathf.Max(0f, -Input.GetAxis("Vertical"));
#endif
            }
        }

        public static float Steer
        {
            get
            {
                if (Mathf.Abs(MobileSteer) > 0.01f) return MobileSteer;
#if ENABLE_INPUT_SYSTEM
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null) return gp.leftStick.x.ReadValue();
                float steer = 0f;
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    if (UnityEngine.InputSystem.Keyboard.current.aKey.isPressed) steer -= 1f;
                    if (UnityEngine.InputSystem.Keyboard.current.dKey.isPressed) steer += 1f;
                }
                return steer;
#else
                return Input.GetAxis("Horizontal");
#endif
            }
        }

        public static float HandBrake
        {
            get
            {
                if (MobileHandBrake > 0.1f) return MobileHandBrake;
#if ENABLE_INPUT_SYSTEM
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null) return gp.buttonEast.isPressed ? 1f : 0f;
                return UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed ? 1f : 0f;
#else
                return Input.GetKey(KeyCode.Space) ? 1f : 0f;
#endif
            }
        }

        public static bool HeadlightsToggled
        {
            get
            {
                if (MobileHeadlights)
                {
                    MobileHeadlights = false;
                    return true;
                }
#if ENABLE_INPUT_SYSTEM
                return UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.L);
#endif
            }
        }

        public static bool HornHeld
        {
            get
            {
                if (MobileHorn) return true;
#if ENABLE_INPUT_SYSTEM
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null) return gp.buttonNorth.isPressed;
                return UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.hKey.isPressed;
#else
                return Input.GetKey(KeyCode.H);
#endif
            }
        }
    }
}
