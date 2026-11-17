using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Lightweight trailer designed specifically for the Tractor.
    /// Couples to TractorController's rearHitch via a HingeJoint.
    /// Supports cargo loading/unloading with dynamic mass adjustment.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TractorTrailerController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Hitch")]
        [SerializeField] private Transform hitchPoint; // local attach point on trailer

        [Header("Wheels")]
        [SerializeField] private WheelCollider wheelLeft;
        [SerializeField] private WheelCollider wheelRight;
        [SerializeField] private Transform wheelLeftMesh;
        [SerializeField] private Transform wheelRightMesh;

        [Header("Cargo")]
        [SerializeField] [Range(0f, 2000f)] private float cargoMass;
        [SerializeField] private bool isLoaded;

        [Header("Suspension Tuning")]
        [SerializeField] private float suspensionDistance = 0.3f;
        [SerializeField] private float suspensionSpring   = 35000f;
        [SerializeField] private float suspensionDamper   = 4500f;
        [SerializeField] private float wheelMass          = 80f;

        // ── State ─────────────────────────────────────────────────────────

        private Rigidbody _rb;
        private HingeJoint _hitchJoint;
        private TractorController _tractor;

        private const float BaseMass = 1500f;

        // ── Properties ────────────────────────────────────────────────────

        public float CargoMass => cargoMass;
        public bool  IsLoaded  => isLoaded;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            _rb      = GetComponent<Rigidbody>();
            _rb.mass = BaseMass;
            _rb.centerOfMass = new Vector3(0f, -0.4f, 0f);

            ConfigureWheels();
        }

        private void Update()
        {
            SyncWheelMeshes();
        }

        // ── Attachment ────────────────────────────────────────────────────

        /// <summary>Attaches this trailer to a tractor's rear hitch via a HingeJoint.</summary>
        public void AttachToTractor(TractorController t)
        {
            if (t == null || t.rearHitch == null) return;
            _tractor = t;

            // Snap to hitch position
            Vector3 offset = hitchPoint != null
                ? transform.position - hitchPoint.position
                : Vector3.zero;
            transform.position = t.rearHitch.position + offset;
            transform.rotation = t.rearHitch.rotation;

            // Create HingeJoint
            var joint = gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = t.GetComponent<Rigidbody>();
            joint.anchor        = hitchPoint != null ? hitchPoint.localPosition : Vector3.zero;
            joint.axis          = Vector3.up;          // yaw freedom for cornering
            joint.useSpring     = false;
            joint.useLimits     = false;
            joint.breakForce    = Mathf.Infinity;
            joint.breakTorque   = Mathf.Infinity;
            _hitchJoint = joint;

            t.AttachImplement(AttachedImplement.Trailer);
        }

        /// <summary>Detaches this trailer from the tractor.</summary>
        public void Detach()
        {
            if (_hitchJoint != null) Destroy(_hitchJoint);
            _tractor?.DetachImplement();
            _tractor = null;
        }

        // ── Cargo ─────────────────────────────────────────────────────────

        /// <summary>Loads cargo, increasing the trailer Rigidbody mass.</summary>
        /// <param name="mass">Cargo mass in kg (clamped 0–2000).</param>
        public void LoadCargo(float mass)
        {
            cargoMass          = Mathf.Clamp(mass, 0f, 2000f);
            isLoaded           = cargoMass > 0f;
            _rb.mass           = BaseMass + cargoMass;
            _rb.centerOfMass   = new Vector3(0f, -0.3f + (cargoMass / 2000f) * 0.2f, 0f);
        }

        /// <summary>Unloads all cargo, restoring base mass.</summary>
        public void UnloadCargo()
        {
            cargoMass        = 0f;
            isLoaded         = false;
            _rb.mass         = BaseMass;
            _rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        }

        // ── Wheel helpers ─────────────────────────────────────────────────

        private void ConfigureWheels()
        {
            ConfigureWheel(wheelLeft);
            ConfigureWheel(wheelRight);
        }

        private void ConfigureWheel(WheelCollider wc)
        {
            if (wc == null) return;
            wc.mass              = wheelMass;
            wc.suspensionDistance = suspensionDistance;

            var spring = wc.suspensionSpring;
            spring.spring   = suspensionSpring;
            spring.damper   = suspensionDamper;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;

            // Trailers don't drive or brake actively; zero out motor/brake torque
            wc.motorTorque  = 0f;
            wc.brakeTorque  = 0f;
        }

        private void SyncWheelMeshes()
        {
            SyncMesh(wheelLeft,  wheelLeftMesh);
            SyncMesh(wheelRight, wheelRightMesh);
        }

        private static void SyncMesh(WheelCollider wc, Transform mesh)
        {
            if (wc == null || mesh == null) return;
            wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (hitchPoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(hitchPoint.position, 0.1f);
        }
    }
}
