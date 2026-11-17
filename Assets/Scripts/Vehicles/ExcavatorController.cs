using UnityEngine;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Tracked excavator (Poclain / Poklen) controller.
    /// Inherits VehicleController.
    ///
    /// Movement     : Tank-style differential drive simulated with 4 WheelColliders
    ///               per side (8 total), arranged as track bogies.
    /// Cabin        : Smooth 360° rotation around local Y via Transform.
    /// Arm chain    : boom → stick → bucket using HydraulicArm components.
    /// Terrain dig  : Calls TerrainDeformer.DigAt when bucket is near terrain.
    /// Soil bucket  : currentSoilVolume (0-1); DumpSoil() pours soil to world pos.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class ExcavatorController : VehicleController
    {
        // ── Track WheelColliders (4 per side, acting as bogies) ────────────
        [Header("Excavator – Tracks (WheelCollider bogies)")]
        [SerializeField] private WheelCollider[] leftTrackWheels  = new WheelCollider[4];
        [SerializeField] private WheelCollider[] rightTrackWheels = new WheelCollider[4];

        [SerializeField] private float trackMotorTorque = 3000f;
        [SerializeField] private float trackBrakeTorque = 5000f;
        [SerializeField] private float topSpeedKmh      = 10f;

        // ── Track speed state ──────────────────────────────────────────────
        private float _leftTrackSpeed;
        private float _rightTrackSpeed;

        // ── Cabin rotation ─────────────────────────────────────────────────
        [Header("Excavator – Cabin")]
        [SerializeField] private Transform cabinTransform;
        [SerializeField] private float cabinRotateSpeed = 60f;    // deg/s

        private float _cabinAngle;

        // ── Arm chain ──────────────────────────────────────────────────────
        [Header("Excavator – Arm")]
        [SerializeField] private HydraulicArm boomArm;      // -10 to 70°
        [SerializeField] private HydraulicArm stickArm;     // 0 to 120°
        [SerializeField] private HydraulicArm bucketArm;    // -40 to 90°

        [SerializeField] private Transform bucketTip;       // world pos used for dig detection

        // ── Terrain deformer ───────────────────────────────────────────────
        [Header("Excavator – Terrain")]
        [SerializeField] private TerrainDeformer terrainDeformer;
        [SerializeField] private float digRadius       = 0.6f;
        [SerializeField] private float digDepth        = 0.5f;
        [SerializeField] private float digGroundOffset = 0.3f;  // bucket tip height threshold for digging

        // ── Soil bucket ────────────────────────────────────────────────────
        [Header("Excavator – Soil Bucket")]
        public float currentSoilVolume;   // 0-1
        [SerializeField] private float soilCollectRate = 0.2f;  // per second while digging
        [SerializeField] private ParticleSystem dirtParticles;

        // ── Engine audio ───────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private float engineIdlePitch = 0.6f;
        [SerializeField] private float engineMaxPitch  = 1.3f;

        // ── Lifecycle ──────────────────────────────────────────────────────
        protected override void Start()
        {
            base.Start();
            vehicleType = VehicleType.Excavator;

            // Excavator is very heavy – tracks don't use suspension the same way
            rb.centerOfMass = new Vector3(0f, -0.8f, 0f);
        }

        protected override void Update()
        {
            base.Update();
            if (!isOccupied || !engineRunning) return;

            ReadTrackInput();
            ReadArmInput();
            RotateCabin();
            CheckDigging();
        }

        private void FixedUpdate()
        {
            ApplyTrackTorques();
        }

        // ── Track driving ──────────────────────────────────────────────────
        public override void ApplyDriving()
        {
            // Called from base; actual torques applied in FixedUpdate via ApplyTrackTorques
        }

        public override void ApplySteering()
        {
            // Steering is differential – handled in ReadTrackInput
        }

        private void ReadTrackInput()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            // Left joystick Y = forward / backward; X = turn (differential)
            float fwd  = input.LeftJoystick.y;
            float turn = input.LeftJoystick.x;

            _leftTrackSpeed  = Mathf.Clamp(fwd + turn, -1f, 1f);
            _rightTrackSpeed = Mathf.Clamp(fwd - turn, -1f, 1f);

            currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        }

        private void ApplyTrackTorques()
        {
            if (!engineRunning) return;

            float topSpeedMs = topSpeedKmh / 3.6f;
            float speedRatio = rb.linearVelocity.magnitude / topSpeedMs;

            ApplyToTrack(leftTrackWheels,  _leftTrackSpeed,  speedRatio);
            ApplyToTrack(rightTrackWheels, _rightTrackSpeed, speedRatio);
        }

        private void ApplyToTrack(WheelCollider[] wheels, float input, float speedRatio)
        {
            float torque = speedRatio < 1f ? input * trackMotorTorque : 0f;
            float brake  = Mathf.Approximately(input, 0f) ? trackBrakeTorque : 0f;

            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                wc.motorTorque = torque;
                wc.brakeTorque = brake;
            }
        }

        // ── Cabin rotation ─────────────────────────────────────────────────
        private void RotateCabin()
        {
            // Left joystick X also rotates the cabin when parked (optional)
            // Right joystick X: cabin rotation while arm is working
            var input = InputManager.Instance;
            if (input == null) return;

            float rotInput = input.RightJoystick.x;
            _cabinAngle += rotInput * cabinRotateSpeed * Time.deltaTime;
            // 360° free rotation – no clamping needed

            if (cabinTransform != null)
                cabinTransform.localRotation = Quaternion.Euler(0f, _cabinAngle, 0f);
        }

        // ── Arm input ──────────────────────────────────────────────────────
        private void ReadArmInput()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            // Left joystick Y: boom up/down
            if (boomArm != null)
                boomArm.AdjustTargetAngle(input.LeftJoystick.y * 40f * Time.deltaTime);

            // Right joystick Y: stick in/out
            if (stickArm != null)
                stickArm.AdjustTargetAngle(-input.RightJoystick.y * 50f * Time.deltaTime);

            // Additional: bucket curl could be driven by a 3rd axis / button
        }

        // ── Digging logic ──────────────────────────────────────────────────
        private void CheckDigging()
        {
            if (bucketTip == null || terrainDeformer == null) return;

            // Check if bucket tip is near ground level
            float groundY = Terrain.activeTerrain != null
                ? Terrain.activeTerrain.SampleHeight(bucketTip.position) +
                  Terrain.activeTerrain.transform.position.y
                : 0f;

            bool nearGround = bucketTip.position.y < groundY + digGroundOffset;

            if (nearGround && currentSoilVolume < 1f)
            {
                terrainDeformer.DigAt(bucketTip.position, digRadius, digDepth * Time.deltaTime);
                currentSoilVolume += soilCollectRate * Time.deltaTime;
                currentSoilVolume  = Mathf.Clamp01(currentSoilVolume);

                if (dirtParticles != null && !dirtParticles.isPlaying)
                    dirtParticles.Play();
            }
            else
            {
                if (dirtParticles != null && dirtParticles.isPlaying)
                    dirtParticles.Stop();
            }
        }

        /// <summary>
        /// Empties the soil bucket at the current bucket-tip world position.
        /// Call this when the operator dumps a load.
        /// </summary>
        public void DumpSoil()
        {
            if (currentSoilVolume <= 0f) return;

            Vector3 dumpPos = bucketTip != null ? bucketTip.position : transform.position;

            if (terrainDeformer != null)
                terrainDeformer.RaiseAt(dumpPos, digRadius * 1.5f, currentSoilVolume * 0.3f);

            currentSoilVolume = 0f;
        }

        // ── Engine sound ───────────────────────────────────────────────────
        public override void UpdateEngineSound()
        {
            if (engineAudioSource == null) return;
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();

            float load = Mathf.Clamp01(currentSpeed / topSpeedKmh);
            engineAudioSource.pitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, load);
            engineRPM = Mathf.Lerp(600f, 3500f, load);
        }

        public override string GetVehicleInfo()
        {
            return $"Excavator | Speed: {currentSpeed:F1} km/h | " +
                   $"Soil: {currentSoilVolume * 100f:F0}% | Fuel: {fuelLevel:F0}%";
        }
    }
}
