using UnityEngine;
using HeavyVehiclesWorld.World;
using HeavyVehiclesWorld.Controls;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Front loader bucket implement for TractorController.
    /// Attaches to the tractor's frontHitch transform.
    /// Uses HydraulicArm to raise/lower the loader arm and tilts the bucket.
    /// Scoops soil via TerrainDeformer when the bucket tip contacts terrain.
    /// </summary>
    public class FrontLoaderImplement : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Attachment")]
        [SerializeField] private TractorController tractor;

        [Header("Arm & Bucket")]
        [SerializeField] private Transform loaderArm;        // pivot for raising/lowering
        [SerializeField] private Transform loaderBucket;     // child of loaderArm, pivot for tilt
        [SerializeField] private Transform bucketTip;        // tip used for terrain contact check

        [Header("Hydraulic Arm")]
        [SerializeField] private HydraulicArm armJoint;      // HydraulicArm component on loaderArm

        [Header("Bucket Tilt")]
        [SerializeField] [Range(0f, 60f)] private float bucketTiltAngle = 0f; // 0=scoop, 60=dump
        [SerializeField] private float tiltSpeed = 45f;      // degrees per second
        [SerializeField] private float armSpeed  = 30f;      // degrees per second (arm raise)

        [Header("Soil / Terrain")]
        [SerializeField] private TerrainDeformer terrainDeformer;
        [SerializeField] private float soilCheckRadius  = 0.2f;  // bucket tip check sphere
        [SerializeField] private float soilDigRate      = 0.015f;
        [SerializeField] private float maxSoilVolume    = 1.5f;  // m³
        [SerializeField] private LayerMask terrainLayer;

        [Header("Effects")]
        [SerializeField] private ParticleSystem soilParticles;

        // ── State ─────────────────────────────────────────────────────────

        private bool  _isCarryingSoil;
        private float _soilVolume;
        private Joint _hitchJoint;

        // ── Properties ────────────────────────────────────────────────────

        public bool  isCarryingSoil => _isCarryingSoil;
        public float soilVolume     => _soilVolume;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Start()
        {
            gameObject.tag = "Implement";
            ApplyBucketTilt(bucketTiltAngle);
        }

        private void Update()
        {
            HandleInput();
            CheckTerrainContact();
        }

        // ── Input ─────────────────────────────────────────────────────────

        private void HandleInput()
        {
            if (InputManager.Instance == null) return;

            Vector2 leftJoystick = InputManager.Instance.LeftJoystick;

            // Left joystick Y → raise / lower arm
            float armInput = leftJoystick.y;
            if (Mathf.Abs(armInput) > 0.05f)
            {
                if (armInput > 0f) RaiseArm();
                else               LowerArm();
            }

            // Left joystick X → tilt bucket
            float tiltInput = leftJoystick.x;
            if (Mathf.Abs(tiltInput) > 0.05f)
            {
                float newAngle = bucketTiltAngle + tiltInput * tiltSpeed * Time.deltaTime;
                TiltBucket(newAngle);
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Raises the loader arm by applying upward angular velocity to the HydraulicArm.</summary>
        public void RaiseArm()
        {
            if (armJoint != null)
                armJoint.AdjustTargetAngle(armSpeed * Time.deltaTime);
        }

        /// <summary>Lowers the loader arm.</summary>
        public void LowerArm()
        {
            if (armJoint != null)
                armJoint.AdjustTargetAngle(-armSpeed * Time.deltaTime);
        }

        /// <summary>Sets the bucket tilt angle (0 = scooping, 60 = dump).</summary>
        public void TiltBucket(float angle)
        {
            bucketTiltAngle = Mathf.Clamp(angle, 0f, 60f);
            ApplyBucketTilt(bucketTiltAngle);
        }

        /// <summary>Dumps all carried soil and resets bucket to scooping position.</summary>
        public void DumpBucket()
        {
            if (!_isCarryingSoil) return;

            _soilVolume     = 0f;
            _isCarryingSoil = false;

            if (soilParticles != null)
                soilParticles.Play();

            TiltBucket(60f); // tip forward to dump
        }

        // ── Attachment ────────────────────────────────────────────────────

        /// <summary>Attaches this front loader to the tractor's frontHitch.</summary>
        public void AttachToTractor(TractorController t)
        {
            tractor = t;
            if (t.frontHitch == null) return;

            transform.position = t.frontHitch.position;
            transform.rotation = t.frontHitch.rotation;

            var joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = t.GetComponent<Rigidbody>();
            joint.breakForce    = Mathf.Infinity;
            joint.breakTorque   = Mathf.Infinity;
            _hitchJoint = joint;

            t.AttachImplement(AttachedImplement.FrontLoader);
        }

        /// <summary>Detaches this front loader from the tractor.</summary>
        public void DetachFromTractor()
        {
            if (_hitchJoint != null) Destroy(_hitchJoint);
            tractor?.DetachImplement();
            tractor = null;
        }

        // ── Terrain Interaction ───────────────────────────────────────────

        private void CheckTerrainContact()
        {
            if (bucketTip == null || terrainDeformer == null) return;
            if (bucketTiltAngle > 10f) return; // only dig when near-scooping angle

            bool hitTerrain = Physics.CheckSphere(bucketTip.position, soilCheckRadius, terrainLayer);
            if (!hitTerrain) return;

            if (_soilVolume < maxSoilVolume)
            {
                terrainDeformer.DigAt(bucketTip.position, soilCheckRadius, soilDigRate);
                _soilVolume     += soilDigRate * Time.fixedDeltaTime * 10f;
                _soilVolume      = Mathf.Clamp(_soilVolume, 0f, maxSoilVolume);
                _isCarryingSoil  = _soilVolume > 0.01f;

                if (soilParticles != null && !soilParticles.isPlaying)
                    soilParticles.Play();
            }
            else
            {
                if (soilParticles != null && soilParticles.isPlaying)
                    soilParticles.Stop();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void ApplyBucketTilt(float angle)
        {
            if (loaderBucket == null) return;
            Vector3 euler = loaderBucket.localEulerAngles;
            euler.x = -angle; // negative = curl forward (scooping)
            loaderBucket.localEulerAngles = euler;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (bucketTip == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bucketTip.position, soilCheckRadius);
        }
    }
}
