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
        [SerializeField] private WheelCollider wheelFL;
        [SerializeField] private WheelCollider wheelFR;
        [SerializeField] private WheelCollider wheelRL;
        [SerializeField] private WheelCollider wheelRR;

        [SerializeField] private Transform meshFL;
        [SerializeField] private Transform meshFR;
        [SerializeField] private Transform meshRL;
        [SerializeField] private Transform meshRR;

        [SerializeField] private float motorTorque    = 2000f;
        [SerializeField] private float maxSteerAngle  = 30f;
        [SerializeField] private float topSpeedKmh    = 35f;
        [SerializeField] private bool  fourWheelDrive = true;

        // ── Front loader arm ───────────────────────────────────────────────
        [Header("JCB – Front Loader")]
        [SerializeField] private Transform loaderBoomTransform;      // JCBBoom
        [SerializeField] private Transform loaderArmTransform;       // JCBArm
        [SerializeField] private Transform loaderBucketTransform;    // JCBBucket

        [SerializeField] private HydraulicArm loaderBoom;
        [SerializeField] private HydraulicArm loaderArm;
        [SerializeField] private HydraulicArm loaderBucket;

        [SerializeField] private float loaderRaiseSpeed  = 40f;  // deg/s used as joystick scale
        [SerializeField] private float loaderTiltSpeed   = 50f;

        // ── Rear backhoe arm ───────────────────────────────────────────────
        [Header("JCB – Rear Backhoe")]
        [SerializeField] private Transform backhoeSwingTransform;    // horizontal pivot
        [SerializeField] private Transform backhoeBoomTransform;
        [SerializeField] private Transform backhoeDipperTransform;   // stick
        [SerializeField] private Transform backhoeBucketTransform;

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
            if (input == null) return;

            float throttle = input.MoveY;
            float topSpeedMs = topSpeedKmh / 3.6f;

            float torque = 0f;
            if (Mathf.Abs(rb.linearVelocity.magnitude) < topSpeedMs)
                torque = throttle * motorTorque;

            // Always drive rear wheels; optionally front too
            wheelRL.motorTorque = torque;
            wheelRR.motorTorque = torque;
            if (fourWheelDrive)
            {
                wheelFL.motorTorque = torque;
                wheelFR.motorTorque = torque;
            }

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            if (!engineRunning || StabilizersDown) return;

            var input = InputManager.Instance;
            if (input == null) return;

            float steer = input.MoveX * maxSteerAngle;
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
