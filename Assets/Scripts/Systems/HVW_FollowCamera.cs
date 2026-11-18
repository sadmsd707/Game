using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Smooth follow camera designed specifically for heavy vehicles and mobile touch controls.
    /// Supports multiple camera modes: Chase (Third Person), Hood / Cockpit, and Drone (Top Down).
    /// </summary>
    public class HVW_FollowCamera : MonoBehaviour
    {
        public enum ViewMode
        {
            ThirdPerson,
            Cockpit,
            Drone
        }

        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Settings")]
        public ViewMode currentMode = ViewMode.ThirdPerson;
        public float followSpeed = 6f;
        public float rotationSpeed = 5f;

        private Vector3 _currentVelocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                // Snap directly behind target on target switch
                Vector3 desiredPos = GetDesiredPosition();
                transform.position = desiredPos;
                transform.LookAt(GetLookTarget());
            }
        }

        public void ToggleCameraMode()
        {
            switch (currentMode)
            {
                case ViewMode.ThirdPerson:
                    currentMode = ViewMode.Cockpit;
                    break;
                case ViewMode.Cockpit:
                    currentMode = ViewMode.Drone;
                    break;
                case ViewMode.Drone:
                    currentMode = ViewMode.ThirdPerson;
                    break;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPos = GetDesiredPosition();
            Vector3 lookTarget = GetLookTarget();

            // Smooth position interpolation
            float smoothTime = currentMode == ViewMode.Cockpit ? 0.02f : 0.12f;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, smoothTime);

            // Smooth rotation interpolation
            Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotationSpeed);
        }

        private Vector3 GetDesiredPosition()
        {
            if (target == null) return transform.position;

            switch (currentMode)
            {
                case ViewMode.Cockpit:
                    return target.position + target.forward * 0.5f + Vector3.up * 1.5f;

                case ViewMode.Drone:
                    return target.position + Vector3.up * 20f - target.forward * 4f;

                case ViewMode.ThirdPerson:
                default:
                    return target.position - target.forward * 7.5f + Vector3.up * 3.6f;
            }
        }

        private Vector3 GetLookTarget()
        {
            if (target == null) return transform.position + transform.forward;

            switch (currentMode)
            {
                case ViewMode.Cockpit:
                    return target.position + target.forward * 20f + Vector3.up * 1.4f;

                case ViewMode.Drone:
                    return target.position;

                case ViewMode.ThirdPerson:
                default:
                    return target.position + Vector3.up * 1.3f + target.forward * 2f;
            }
        }
    }
}
