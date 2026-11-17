using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// ScriptableObject that stores pre-tuned WheelCollider suspension settings
    /// for each vehicle type. Create via Assets > Create > HVW > Wheel Suspension Data.
    /// </summary>
    [CreateAssetMenu(fileName = "WheelSuspensionData", menuName = "HVW/Wheel Suspension Data")]
    public class WheelSuspensionData : ScriptableObject
    {
        [Header("Spring")]
        public float spring    = 35000f;
        public float damper    = 4500f;
        public float targetPos = 0.5f;      // WheelCollider.suspensionSpring.targetPosition

        [Header("WheelCollider Shape")]
        public float radius            = 0.4f;
        public float mass              = 20f;
        public float forwardStiffness  = 1.5f;   // WheelFrictionCurve.stiffness
        public float sidewaysStiffness = 2.0f;

        // ------------------------------------------------------------------ //
        // Factory Presets
        // ------------------------------------------------------------------ //

        [Header("Pre-sets")]
        public static WheelSuspensionData CarPreset()
        {
            var d = CreateInstance<WheelSuspensionData>();
            d.spring = 35000f; d.damper = 4500f; d.targetPos = 0.5f;
            d.radius = 0.35f;  d.mass   = 20f;
            d.forwardStiffness = 1.5f; d.sidewaysStiffness = 2.0f;
            return d;
        }

        public static WheelSuspensionData TruckPreset()
        {
            var d = CreateInstance<WheelSuspensionData>();
            d.spring = 80000f; d.damper = 8000f; d.targetPos = 0.3f;
            d.radius = 0.55f;  d.mass   = 60f;
            d.forwardStiffness = 2.0f; d.sidewaysStiffness = 2.5f;
            return d;
        }

        // ------------------------------------------------------------------ //
        // Runtime Application
        // ------------------------------------------------------------------ //

        /// <summary>Applies this data to a WheelCollider at runtime.</summary>
        public void ApplyTo(WheelCollider wc)
        {
            wc.radius = radius;
            wc.mass   = mass;

            var spring = wc.suspensionSpring;
            spring.spring         = this.spring;
            spring.damper         = damper;
            spring.targetPosition = targetPos;
            wc.suspensionSpring   = spring;

            var fwd = wc.forwardFriction;
            fwd.stiffness      = forwardStiffness;
            wc.forwardFriction = fwd;

            var side = wc.sidewaysFriction;
            side.stiffness       = sidewaysStiffness;
            wc.sidewaysFriction  = side;
        }
    }
}
