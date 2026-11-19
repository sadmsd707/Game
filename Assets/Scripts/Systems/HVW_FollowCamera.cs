using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Smooth follow camera for heavy vehicles and mobile touch controls.
    /// Supports Chase (Third Person), Hood / Cockpit, and Drone (Top Down) modes.
    ///
    /// Step 0 fix: collider exclusion uses a HashSet<Collider> built from ALL
    /// child colliders of the target vehicle — not just a transform hierarchy
    /// check — so the camera never clips inside the vehicle body.
    /// A hard minimum distance (MIN_HARD_DIST) is enforced AFTER the
    /// spherecast resolves so the camera is always at least 3.5 m away.
    /// </summary>
    public class HVW_FollowCamera : MonoBehaviour
    {
        public enum ViewMode { ThirdPerson, Cockpit, Drone }

        // Hard minimum: camera is never closer than this to the vehicle pivot
        private const float MIN_HARD_DIST = 3.5f;

        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Settings")]
        public ViewMode currentMode = ViewMode.ThirdPerson;
        public float followSpeed  = 6f;
        public float rotationSpeed = 5f;

        private Vector3 _currentVelocity;

        // All colliders belonging to the active vehicle — rebuilt on target change
        private readonly HashSet<Collider> _vehicleColliders = new HashSet<Collider>();

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            RebuildColliderExclusion();

            if (target != null)
            {
                // Snap directly to desired position on target switch
                transform.position = GetDesiredPosition();
                transform.LookAt(GetLookTarget());
            }
        }

        /// <summary>Called by WorldInitializer when the player switches vehicles.</summary>
        public void NotifyVehicleChanged(Transform newTarget) => SetTarget(newTarget);

        public void ToggleCameraMode()
        {
            switch (currentMode)
            {
                case ViewMode.ThirdPerson: currentMode = ViewMode.Cockpit;      break;
                case ViewMode.Cockpit:     currentMode = ViewMode.Drone;        break;
                case ViewMode.Drone:       currentMode = ViewMode.ThirdPerson;  break;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPos  = GetDesiredPosition();
            Vector3 lookTarget  = GetLookTarget();

            float smoothTime = currentMode == ViewMode.Cockpit ? 0.02f : 0.12f;
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _currentVelocity, smoothTime);

            Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRot, Time.deltaTime * rotationSpeed);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internals
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Collect every Collider under the target so they can be excluded from
        /// the occlusion spherecast.  WheelColliders are included intentionally.
        /// </summary>
        private void RebuildColliderExclusion()
        {
            _vehicleColliders.Clear();
            if (target == null) return;

            foreach (var col in target.GetComponentsInChildren<Collider>(true))
                _vehicleColliders.Add(col);
        }

        private Vector3 GetDesiredPosition()
        {
            if (target == null) return transform.position;

            Vector3 rawPos;

            switch (currentMode)
            {
                // ── Cockpit ───────────────────────────────────────────────────
                case ViewMode.Cockpit:
                    rawPos = target.position
                           + target.forward * 0.5f
                           + Vector3.up     * 1.5f;
                    break;

                // ── Drone (top-down) ──────────────────────────────────────────
                case ViewMode.Drone:
                    rawPos = target.position
                           + Vector3.up      * 20f
                           - target.forward  * 4f;
                    break;

                // ── Third Person (default) ────────────────────────────────────
                case ViewMode.ThirdPerson:
                default:
                    float followDist   = 6.8f;
                    float followHeight = 2.8f;

                    var vc = target.GetComponent<VehicleController>();
                    if (vc != null)
                    {
                        switch (vc.vehicleType)
                        {
                            case VehicleType.Truck:
                                followDist = 8.8f; followHeight = 3.6f; break;
                            case VehicleType.Tractor:
                                followDist = 7.2f; followHeight = 2.9f; break;
                            case VehicleType.Harvester:
                                followDist = 9.5f; followHeight = 3.8f; break;
                            case VehicleType.Excavator:
                            case VehicleType.JCB:
                                followDist = 7.8f; followHeight = 3.2f; break;
                        }
                    }

                    // Desired ideal position
                    rawPos = target.position
                           - target.forward * followDist
                           + Vector3.up     * followHeight;

                    // Occlusion avoidance — sweep from pivot toward desired pos
                    Vector3 origin = target.position + Vector3.up * 1.6f;
                    Vector3 rayDir = rawPos - origin;
                    float   dist   = rayDir.magnitude;

                    if (dist > 0.01f)
                    {
                        var hits = Physics.SphereCastAll(
                            origin, 0.25f, rayDir.normalized, dist,
                            ~0, QueryTriggerInteraction.Ignore);

                        float closestValidDist = dist;
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var h = hits[i];
                            if (h.collider == null) continue;

                            // ── KEY FIX: exclude by Collider instance, not transform ──
                            if (_vehicleColliders.Contains(h.collider)) continue;

                            if (h.distance < closestValidDist)
                                closestValidDist = h.distance;
                        }

                        if (closestValidDist < dist)
                        {
                            // Pull the camera in front of the occluder, but never
                            // closer than MIN_HARD_DIST from the vehicle pivot
                            float safePull = Mathf.Max(closestValidDist - 0.3f, 0f);
                            rawPos = origin + rayDir.normalized * safePull;
                        }
                    }

                    // ── HARD MINIMUM DISTANCE clamp ───────────────────────────
                    // Ensures camera is never inside / flush against the vehicle
                    // regardless of geometry or wall proximity.
                    Vector3 toCamera = rawPos - target.position;
                    if (toCamera.magnitude < MIN_HARD_DIST)
                    {
                        Vector3 dir = toCamera.magnitude > 0.001f
                            ? toCamera.normalized
                            : (-target.forward + Vector3.up).normalized;
                        rawPos = target.position + dir * MIN_HARD_DIST;
                    }
                    break;
            }

            return rawPos;
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
                default:
                    return target.position + Vector3.up * 1.2f + target.forward * 1.8f;
            }
        }
    }
}
