using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Reusable component for a single hydraulic arm joint.
    /// Attach to any GameObject that represents one segment of an articulated arm.
    /// </summary>
    public class HydraulicArm : MonoBehaviour
    {
        // ── Inspector references ───────────────────────────────────────────
        [Header("Arm Segment")]
        [SerializeField] private Transform armSegment;

        [Header("Angle Settings")]
        [SerializeField] private float targetAngle  = 0f;
        [SerializeField] private float currentAngle = 0f;
        [SerializeField] private float speed        = 45f;   // degrees per second
        [SerializeField] private float minAngle     = -90f;
        [SerializeField] private float maxAngle     =  90f;

        [Header("Rotation Axis")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right; // local axis to rotate around

        [Header("Audio")]
        [SerializeField] private AudioSource hydraulicSound;

        // ── Public accessors ───────────────────────────────────────────────
        public float CurrentAngle => currentAngle;
        public float TargetAngle  => targetAngle;
        public bool  IsMoving     => !Mathf.Approximately(currentAngle, targetAngle);

        // ── Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (armSegment == null)
                armSegment = transform;

            currentAngle = targetAngle;
        }

        private void Update()
        {
            MoveToTarget();
            UpdateAudio();
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Sets the desired angle, clamped to [minAngle, maxAngle].</summary>
        public void SetTargetAngle(float angle)
        {
            targetAngle = Mathf.Clamp(angle, minAngle, maxAngle);
        }

        /// <summary>Adjusts the target angle by a delta, clamped to limits.</summary>
        public void AdjustTargetAngle(float delta)
        {
            SetTargetAngle(targetAngle + delta);
        }

        /// <summary>Steps currentAngle toward targetAngle and applies the rotation to the transform.</summary>
        public void MoveToTarget()
        {
            if (armSegment == null) return;

            float prev = currentAngle;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.deltaTime);

            if (!Mathf.Approximately(currentAngle, prev))
            {
                // Apply rotation relative to the segment's initial orientation.
                // We store rotation as a local-space rotation around rotationAxis.
                armSegment.localRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────
        private void UpdateAudio()
        {
            if (hydraulicSound == null) return;

            if (IsMoving)
            {
                if (!hydraulicSound.isPlaying)
                    hydraulicSound.Play();
            }
            else
            {
                if (hydraulicSound.isPlaying)
                    hydraulicSound.Stop();
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (armSegment == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(armSegment.position,
                            armSegment.position + armSegment.TransformDirection(rotationAxis) * 0.5f);
        }
    }
}
