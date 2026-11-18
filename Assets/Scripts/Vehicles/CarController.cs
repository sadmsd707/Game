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
        public float motorTorque     = 1500f;
        public float brakeTorque     = 5000f;
        public float handBrakeTorque = 10000f;
        public float maxRPM          = 7000f;
        public float idleRPM         = 800f;

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

        // ── Lifecycle ─────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();

            rb.mass = 1500f;
            rb.centerOfMass = new Vector3(0f, -0.5f, 0.3f);

            _driveWheels = new[] { rearLeft, rearRight };
            _steerWheels = new[] { frontLeft, frontRight };
            _allWheels   = new[] { frontLeft, frontRight, rearLeft, rearRight };
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;
            ApplyDriving();
            ApplySteering();
            SyncWheelMeshes();
            AutoShift();
            ConsumeFuel();
        }

        private void Update()
        {
            base.Update();
            HandleExtras();
        }

        // ── Abstract overrides ────────────────────────────────────────────

        public override void ApplyDriving()
        {
            float throttle = InputManagerProxy.Throttle;
            _brakeInput     = InputManagerProxy.Brake;
            _handBrakeInput = InputManagerProxy.HandBrake;

            float ratio      = gearRatios[currentGearIndex];
            float wheelRPM   = WheelPhysicsHelper.GetWheelRPM(rearLeft);
            engineRPM        = Mathf.Clamp(
                WheelPhysicsHelper.ComputeEngineRPM(wheelRPM, ratio, finalDriveRatio),
                idleRPM, maxRPM);

            float torqueMult = engineTorqueCurve.Evaluate(engineRPM);
            float appliedTorque = (currentGearIndex == 1) ? 0f
                                : throttle * motorTorque * torqueMult;

            WheelPhysicsHelper.ApplyMotorTorque(_driveWheels, appliedTorque);

            // Brakes
            float brake = _brakeInput * brakeTorque;
            float hbRear = _handBrakeInput > 0.5f ? handBrakeTorque : 0f;
            frontLeft.brakeTorque  = brake;
            frontRight.brakeTorque = brake;
            rearLeft.brakeTorque   = brake + hbRear;
            rearRight.brakeTorque  = brake + hbRear;

            currentSpeed = rb.linearVelocity.magnitude * 3.6f; // km/h
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

        public override string GetVehicleInfo() => "Car";

        // ── Auto-shift ────────────────────────────────────────────────────
        private void AutoShift()
        {
            _autoShiftCooldown -= Time.fixedDeltaTime;
            if (_autoShiftCooldown > 0f) return;
            if (currentGearIndex < 2) return; // R or N – no auto

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
            WheelPhysicsHelper.UpdateWheelPose(frontLeft,  frontLeftMesh);
            WheelPhysicsHelper.UpdateWheelPose(frontRight, frontRightMesh);
            WheelPhysicsHelper.UpdateWheelPose(rearLeft,   rearLeftMesh);
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
    internal static class InputManagerProxy
    {
        public static float Throttle
        {
            get
            {
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
