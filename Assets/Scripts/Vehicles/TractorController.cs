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
    /// Features: 4WD toggle, PTO, 3-point hitch (raise/lower), clutch simulation,
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

        // ── Tuning ────────────────────────────────────────────────────────
        [Header("Tuning")]
        public float maxSteerAngle   = 40f;
        public float motorTorque     = 6000f;
        public float maxBrakeTorque  = 8000f;
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
        public bool  ptoActive;
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
        [Range(0f, 1f)] public  float        hitchPosition;        // 0 = down, 1 = up

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
            rb.centerOfMass = new Vector3(0f, -0.1f, 0f);
        }

        private void FixedUpdate()
        {
            if (!engineRunning) return;
            ReadThrottleAndClutch();
            ApplyDriving();
            ApplySteering();
            SyncWheelMeshes();
            UpdatePTO();
            ConsumeFuel();
        }

        protected override void Update()
        {
            base.Update();
            UpdateHitchArm();
            HandleExtras();
        }

        // ── Abstract overrides ────────────────────────────────────────────

        public override void ApplyDriving()
        {
            float rawThrottle = InputManagerProxy.Throttle;
            float rawBrake    = InputManagerProxy.Brake;

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

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        public override void ApplySteering()
        {
            float steer = InputManagerProxy.Steer;
            WheelPhysicsHelper.ApplySteering(
                new[] { frontLeft, frontRight }, steer, maxSteerAngle);
        }

        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();
            engineAudioSource.pitch = 0.5f + (engineRPM / maxRPM) * 1.5f;
        }

        public override string GetVehicleInfo() => "Tractor";

        // ── Hitch API ─────────────────────────────────────────────────────

        /// <summary>Attaches an implement type to the tractor.</summary>
        public void AttachImplement(AttachedImplement type)
        {
            attachedImplement = type;
        }

        /// <summary>Removes the currently attached implement.</summary>
        public void DetachImplement()
        {
            attachedImplement = AttachedImplement.None;
        }

        /// <summary>Sets hitch position. 0 = fully lowered, 1 = fully raised.</summary>
        public void RaiseLowerHitch(float amount)
        {
            hitchPosition = Mathf.Clamp01(amount);
        }

        // ── PTO ───────────────────────────────────────────────────────────
        private void UpdatePTO()
        {
            ptoRPM = ptoActive ? engineRPM * ptoRatioToEngine : 0f;
        }

        // ── Throttle / Clutch polling ─────────────────────────────────────
        private void ReadThrottleAndClutch()
        {
            // Throttle pedal (separate from drive)
            throttlePedal = InputManagerProxy.Throttle;

            // Clutch
#if ENABLE_INPUT_SYSTEM
            clutchPedal = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.isPressed ? 1f : 0f;
#else
            clutchPedal = Input.GetKey(KeyCode.LeftControl) ? 1f : 0f;
#endif
        }

        // ── Hitch arm visual ─────────────────────────────────────────────
        private void UpdateHitchArm()
        {
            if (hitchArmTransform == null) return;
            float angle = Mathf.Lerp(hitchLiftAngleMin, hitchLiftAngleMax, hitchPosition);
            hitchArmTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);
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

        private void ConsumeFuel()
        {
            if (fuelLevel > 0f)
                fuelLevel -= Time.fixedDeltaTime * 0.012f * throttlePedal;
        }
    }
}
