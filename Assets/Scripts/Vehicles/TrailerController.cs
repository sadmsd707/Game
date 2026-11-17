using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Standalone trailer that can be attached to a TruckController's hitch point.
    /// Uses a ConfigurableJoint for realistic coupling physics.
    /// When detached, rear-axle brakes engage automatically.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrailerController : MonoBehaviour
    {
        // ── Rear-axle wheel colliders ─────────────────────────────────────
        [Header("Rear Axle")]
        public WheelCollider rearAxleLeft;
        public WheelCollider rearAxleRight;

        [Header("Wheel Meshes")]
        public Transform rearAxleLeftMesh;
        public Transform rearAxleRightMesh;

        // ── Coupling ──────────────────────────────────────────────────────
        [Header("Coupling")]
        [SerializeField] private float detachedBrakeTorque = 3000f;

        // ── Cargo ─────────────────────────────────────────────────────────
        [Header("Cargo")]
        public List<GameObject> cargoItems = new List<GameObject>();
        public bool             cargoLoaded;

        // ── State ─────────────────────────────────────────────────────────
        public bool IsAttached { get; private set; }

        // ── Internal ──────────────────────────────────────────────────────
        private Rigidbody           _rb;
        private ConfigurableJoint   _joint;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 3000f;
            _rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        }

        private void FixedUpdate()
        {
            // When detached, apply holding brakes so the trailer doesn't roll away
            if (!IsAttached)
                ApplyDetachedBrakes();

            SyncWheelMeshes();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Attaches this trailer to a truck hitch transform.</summary>
        public void AttachToTruck(Transform hitchPoint)
        {
            if (IsAttached) return;
            IsAttached = true;

            // Release brakes
            WheelPhysicsHelper.ApplyBrakeTorque(
                new[] { rearAxleLeft, rearAxleRight }, 0f);

            // Create ConfigurableJoint coupling
            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.connectedBody = hitchPoint.GetComponentInParent<Rigidbody>();
            _joint.anchor        = transform.InverseTransformPoint(hitchPoint.position);

            // Allow rotation around Y (yaw) only — trailer should swing horizontally
            _joint.xMotion  = ConfigurableJointMotion.Locked;
            _joint.yMotion  = ConfigurableJointMotion.Locked;
            _joint.zMotion  = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = ConfigurableJointMotion.Locked;
            _joint.angularYMotion = ConfigurableJointMotion.Free;
            _joint.angularZMotion = ConfigurableJointMotion.Locked;

            // Soft linear limits for realistic flex
            var drive = new JointDrive
            {
                positionSpring = 50000f,
                positionDamper = 2000f,
                maximumForce   = float.MaxValue
            };
            _joint.xDrive = drive;
            _joint.yDrive = drive;
            _joint.zDrive = drive;
        }

        /// <summary>Detaches this trailer from its truck.</summary>
        public void Detach()
        {
            if (!IsAttached) return;
            IsAttached = false;

            if (_joint != null)
            {
                Destroy(_joint);
                _joint = null;
            }

            // Apply parking brakes
            ApplyDetachedBrakes();
        }

        // ── Cargo API ─────────────────────────────────────────────────────

        /// <summary>Loads an array of cargo GameObjects onto the trailer.</summary>
        public void LoadCargo(GameObject[] items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                cargoItems.Add(item);
                // Parent cargo to trailer so it moves with it
                item.transform.SetParent(transform);
                // Optionally disable individual rigidbodies
                var crb = item.GetComponent<Rigidbody>();
                if (crb != null) crb.isKinematic = true;
            }
            cargoLoaded = cargoItems.Count > 0;
        }

        /// <summary>Unloads all cargo from the trailer.</summary>
        public void DumpCargo()
        {
            foreach (var item in cargoItems)
            {
                if (item == null) continue;
                item.transform.SetParent(null);
                var crb = item.GetComponent<Rigidbody>();
                if (crb != null)
                {
                    crb.isKinematic = false;
                    crb.AddForce(transform.right * Random.Range(-2f, 2f) + Vector3.up * 2f,
                                 ForceMode.Impulse);
                }
            }
            cargoItems.Clear();
            cargoLoaded = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void ApplyDetachedBrakes()
        {
            WheelPhysicsHelper.ApplyBrakeTorque(
                new[] { rearAxleLeft, rearAxleRight }, detachedBrakeTorque);
        }

        private void SyncWheelMeshes()
        {
            WheelPhysicsHelper.UpdateWheelPose(rearAxleLeft,  rearAxleLeftMesh);
            WheelPhysicsHelper.UpdateWheelPose(rearAxleRight, rearAxleRightMesh);
        }
    }
}
