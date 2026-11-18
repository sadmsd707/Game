using UnityEngine;
using HeavyVehiclesWorld.Controls;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// JCB Backhoe Loader controller.
    /// Inherits VehicleController for engine, fuel, gear and event boilerplate.
    ///
    /// Drive layout  : 4 WheelColliders, optional 4WD toggle.
    /// Front loader  : boom → arm → bucket chain driven by HydraulicArm components.
    /// Rear backhoe  : swing (yaw) → boom → dipper → bucket chain.
    /// Stabilizers   : two legs that must be deployed before rear backhoe is used.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class JCBController : VehicleController
    {
        // ── Drive ──────────────────────────────────────────────────────────
        [Header("JCB – Drive")]
        [SerializeField] public WheelCollider wheelFL;
        [SerializeField] public WheelCollider wheelFR;
        [SerializeField] public WheelCollider wheelRL;
        [SerializeField] public WheelCollider wheelRR;

        [SerializeField] public Transform meshFL;
        [SerializeField] public Transform meshFR;
        [SerializeField] public Transform meshRL;
        [SerializeField] public Transform meshRR;

        [SerializeField] private float motorTorque    = 5000f;
        [SerializeField] private float maxSteerAngle  = 30f;
        [SerializeField] private float topSpeedKmh    = 35f;
        [SerializeField] private bool  fourWheelDrive = true;

        // ── Front loader arm ───────────────────────────────────────────────
        [Header("JCB – Front Loader")]
        [SerializeField] public Transform loaderBoomTransform;      // JCBBoom
        [SerializeField] public Transform loaderArmTransform;       // JCBArm
        [SerializeField] public Transform loaderBucketTransform;    // JCBBucket

        [SerializeField] private HydraulicArm loaderBoom;
        [SerializeField] private HydraulicArm loaderArm;
        [SerializeField] private HydraulicArm loaderBucket;

        [SerializeField] private float loaderRaiseSpeed  = 40f;  // deg/s used as joystick scale
        [SerializeField] private float loaderTiltSpeed   = 50f;

        // ── Rear backhoe arm ───────────────────────────────────────────────
        [Header("JCB – Rear Backhoe")]
        [SerializeField] public Transform backhoeSwingTransform;    // horizontal pivot
        [SerializeField] public Transform backhoeBoomTransform;
        [SerializeField] public Transform backhoeDipperTransform;   // stick
        [SerializeField] public Transform backhoeBucketTransform;

        [SerializeField] private HydraulicArm backhoeSwing;
        [SerializeField] private HydraulicArm backhoeBoom;
        [SerializeField] private HydraulicArm backhoeDipper;
        [SerializeField] private HydraulicArm backhoeBucket;

        [SerializeField] private float backhoeSwingSpeed  = 45f;
        [SerializeField] private float backhoeDigSpeed    = 40f;

        // ── Stabilizers ────────────────────────────────────────────────────
        [Header("JCB – Stabilizers")]
        [SerializeField] private Transform stabilizerLeft;
        [SerializeField] private Transform stabilizerRight;

        [SerializeField] private Vector3 stabDownRotation = new Vector3(80f, 0f, 0f);
        [SerializeField] private Vector3 stabUpRotation   = new Vector3(0f,  0f, 0f);
        [SerializeField] private float   stabSpeed        = 60f;  // deg/s

        public bool StabilizersDown { get; private set; }

        private Quaternion _stabLeftTarget;
        private Quaternion _stabRightTarget;

        // ── Engine audio ───────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private float engineIdlePitch = 0.6f;
        [SerializeField] private float engineMaxPitch  = 1.4f;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.JCB;
            _stabLeftTarget  = Quaternion.Euler(stabUpRotation);
            _stabRightTarget = Quaternion.Euler(stabUpRotation);
        }

        protected override void Update()
        {
            base.Update();
            if (!isOccupied || !engineRunning) return;

            HandleDrive();
            HandleFrontLoader();
            HandleBackhoe();
            HandleStabilizers();
            SyncWheelMeshes();
        }

        // ── Drive ──────────────────────────────────────────────────────────
        public override void ApplyDriving()
        {
            if (!engineRunning || StabilizersDown) return;

            // Drive only while stabilizers are UP
            var input = InputManager.Instance;
            float throttle = (input != null && Mathf.Abs(input.MoveY) > 0.01f)
                ? input.MoveY
                : (InputManagerProxy.Throttle - InputManagerProxy.Brake);

            float topSpeedMs = topSpeedKmh / 3.6f;

            float torque = 0f;
            if (Mathf.Abs(rb.linearVelocity.magnitude) < topSpeedMs)
                torque = throttle * motorTorque;

            if (Mathf.Abs(throttle) > 0.05f)
            {
                if (wheelRL != null) { wheelRL.brakeTorque = 0f; wheelRL.motorTorque = torque; }
                if (wheelRR != null) { wheelRR.brakeTorque = 0f; wheelRR.motorTorque = torque; }
                if (fourWheelDrive)
                {
                    if (wheelFL != null) { wheelFL.brakeTorque = 0f; wheelFL.motorTorque = torque * 0.7f; }
                    if (wheelFR != null) { wheelFR.brakeTorque = 0f; wheelFR.motorTorque = torque * 0.7f; }
                }
            }
            else
            {
                float brakeT = 2500f;
                if (wheelRL != null) { wheelRL.motorTorque = 0f; wheelRL.brakeTorque = brakeT; }
                if (wheelRR != null) { wheelRR.motorTorque = 0f; wheelRR.brakeTorque = brakeT; }
                if (wheelFL != null) { wheelFL.motorTorque = 0f; wheelFL.brakeTorque = brakeT; }
                if (wheelFR != null) { wheelFR.motorTorque = 0f; wheelFR.brakeTorque = brakeT; }
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            if (!engineRunning || StabilizersDown) return;

            var input = InputManager.Instance;
            float steerInput = (input != null && Mathf.Abs(input.MoveX) > 0.01f)
                ? input.MoveX
                : InputManagerProxy.Steer;

            float steer = steerInput * maxSteerAngle;
            wheelFL.steerAngle = steer;
            wheelFR.steerAngle = steer;
        }

        // ── Loader ─────────────────────────────────────────────────────────
        private void HandleFrontLoader()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            Vector2 lj = input.LeftJoystick;

            // Y axis: raise / lower boom
            if (loaderBoom != null)
                loaderBoom.AdjustTargetAngle(-lj.y * loaderRaiseSpeed * Time.deltaTime);

            // X axis: tilt bucket
            if (loaderBucket != null)
                loaderBucket.AdjustTargetAngle(lj.x * loaderTiltSpeed * Time.deltaTime);
        }

        // ── Backhoe ────────────────────────────────────────────────────────
        private void HandleBackhoe()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            Vector2 rj = input.RightJoystick;

            // Right joystick X: swing left/right
            if (backhoeSwing != null)
                backhoeSwing.AdjustTargetAngle(rj.x * backhoeSwingSpeed * Time.deltaTime);

            // Right joystick Y: dipper (stick) in/out
            if (backhoeDipper != null)
                backhoeDipper.AdjustTargetAngle(-rj.y * backhoeDigSpeed * Time.deltaTime);

            // Additional controls would come from extra UI buttons (bucket curl, boom) —
            // accessed via InputManager extended axes if wired.
        }

        // ── Stabilizers ────────────────────────────────────────────────────

        public void LowerStabilizers()
        {
            StabilizersDown  = true;
            _stabLeftTarget  = Quaternion.Euler(stabDownRotation);
            _stabRightTarget = Quaternion.Euler(stabDownRotation);
        }

        public void RaiseStabilizers()
        {
            StabilizersDown  = false;
            _stabLeftTarget  = Quaternion.Euler(stabUpRotation);
            _stabRightTarget = Quaternion.Euler(stabUpRotation);
        }

        private void HandleStabilizers()
        {
            if (stabilizerLeft != null)
                stabilizerLeft.localRotation  = Quaternion.RotateTowards(
                    stabilizerLeft.localRotation,  _stabLeftTarget,  stabSpeed * Time.deltaTime);

            if (stabilizerRight != null)
                stabilizerRight.localRotation = Quaternion.RotateTowards(
                    stabilizerRight.localRotation, _stabRightTarget, stabSpeed * Time.deltaTime);
        }

        // ── Engine sound ───────────────────────────────────────────────────
        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();

            float load = Mathf.Clamp01(Mathf.Abs(currentSpeed) / topSpeedKmh);
            engineAudioSource.pitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, load);
            engineRPM = Mathf.Lerp(800f, 4000f, load);
        }

        public override string GetVehicleInfo()
        {
            return $"JCB Backhoe | Speed: {currentSpeed:F1} km/h | " +
                   $"Fuel: {fuelLevel:F0}% | Stabs: {(StabilizersDown ? "DOWN" : "UP")}";
        }

        // ── Wheel mesh sync ────────────────────────────────────────────────
        private void SyncWheelMeshes()
        {
            UpdateWheelMeshes(wheelFL, meshFL);
            UpdateWheelMeshes(wheelFR, meshFR);
            UpdateWheelMeshes(wheelRL, meshRL);
            UpdateWheelMeshes(wheelRR, meshRR);
        }

        private void HandleDrive()
        {
            ApplyDriving();
            ApplySteering();
        }
    }
}
