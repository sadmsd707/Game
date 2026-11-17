using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Static utility methods for WheelCollider-based vehicle physics.
    /// All methods are safe to call with null arrays/colliders — they skip nulls silently.
    /// </summary>
    public static class WheelPhysicsHelper
    {
        // ── Motor ─────────────────────────────────────────────────────────

        /// <summary>Applies motor torque to every wheel in the array.</summary>
        public static void ApplyMotorTorque(WheelCollider[] wheels, float torque)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc != null)
                    wc.motorTorque = torque;
            }
        }

        // ── Brakes ────────────────────────────────────────────────────────

        /// <summary>Applies brake torque to every wheel in the array.</summary>
        public static void ApplyBrakeTorque(WheelCollider[] wheels, float brake)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc != null)
                    wc.brakeTorque = brake;
            }
        }

        // ── Steering ──────────────────────────────────────────────────────

        /// <summary>
        /// Applies Ackermann-ish steering to the given front wheels.
        /// <paramref name="steerAngle"/> is in the range [-1, 1]; it is mapped to
        /// [-<paramref name="maxSteerAngle"/>, +<paramref name="maxSteerAngle"/>].
        /// </summary>
        public static void ApplySteering(WheelCollider[] steerWheels, float steerAngle, float maxSteerAngle)
        {
            if (steerWheels == null) return;
            float angle = Mathf.Clamp(steerAngle, -1f, 1f) * maxSteerAngle;
            foreach (var wc in steerWheels)
            {
                if (wc != null)
                    wc.steerAngle = angle;
            }
        }

        // ── Mesh sync ─────────────────────────────────────────────────────

        /// <summary>
        /// Syncs a visual wheel mesh's world position and rotation to the
        /// WheelCollider's simulated pose so the mesh tracks the suspension travel.
        /// </summary>
        public static void UpdateWheelPose(WheelCollider wc, Transform mesh)
        {
            if (wc == null || mesh == null) return;
            wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        // ── RPM helpers ───────────────────────────────────────────────────

        /// <summary>Returns the current RPM of a single wheel collider.</summary>
        public static float GetWheelRPM(WheelCollider wc)
        {
            if (wc == null) return 0f;
            return wc.rpm;
        }

        /// <summary>
        /// Derives engine RPM from wheel RPM given a gear ratio and final-drive ratio.
        /// Engine RPM = wheelRPM × |gearRatio| × finalDriveRatio
        /// </summary>
        public static float ComputeEngineRPM(float wheelRPM, float gearRatio, float finalDriveRatio)
        {
            return Mathf.Abs(wheelRPM) * Mathf.Abs(gearRatio) * finalDriveRatio;
        }
    }
}
