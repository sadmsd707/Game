using UnityEngine;

namespace HeavyVehiclesWorld.Effects
{
    /// <summary>
    /// Manages per-wheel dust trail emission based on detected terrain surface type.
    /// Call SetDustEmission() from VehicleEffects or directly from a vehicle controller.
    /// </summary>
    public class DustTrailManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Surface Type Enum
        // ------------------------------------------------------------------ //

        public enum SurfaceType
        {
            Asphalt,
            Dirt,
            Grass,
            Gravel,
            Mud
        }

        // ------------------------------------------------------------------ //
        // Inspector
        // ------------------------------------------------------------------ //

        [Header("Raycast")]
        [Tooltip("Distance to cast downward from wheel centre when detecting surface.")]
        public float raycastDistance = 1.5f;

        [Tooltip("Layer mask to include in surface detection raycast.")]
        public LayerMask surfaceLayerMask = ~0;   // all layers by default

        [Header("Emission Rates per Surface")]
        public float dirtRate    = 50f;
        public float grassRate   = 30f;
        public float gravelRate  = 45f;
        public float mudRate     = 60f;
        public float asphaltRate = 0f;    // no dust on asphalt

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Detects the surface type beneath the given WheelCollider using a downward raycast.
        /// Returns SurfaceType.Asphalt when nothing is hit or the tag is unrecognised.
        /// </summary>
        public SurfaceType GetSurfaceUnder(WheelCollider wc)
        {
            Vector3 origin = wc.transform.TransformPoint(wc.center);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                raycastDistance + wc.radius, surfaceLayerMask))
            {
                return ClassifySurface(hit);
            }

            return SurfaceType.Asphalt;
        }

        /// <summary>Returns the representative dust colour for a given surface type.</summary>
        public static Color GetDustColor(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Dirt:    return new Color(0.60f, 0.42f, 0.22f, 0.80f);  // brown
                case SurfaceType.Grass:   return new Color(0.40f, 0.60f, 0.20f, 0.60f);  // green-tint
                case SurfaceType.Gravel:  return new Color(0.65f, 0.63f, 0.60f, 0.80f);  // grey
                case SurfaceType.Mud:     return new Color(0.35f, 0.25f, 0.12f, 0.90f);  // dark brown
                case SurfaceType.Asphalt:
                default:                  return new Color(0.80f, 0.80f, 0.80f, 0.00f);  // transparent
            }
        }

        /// <summary>
        /// Configures a ParticleSystem's emission rate and start colour based on surface and speed.
        /// Pass speed in km/h. Dust is suppressed on Asphalt or when speed is too low.
        /// </summary>
        public void SetDustEmission(ParticleSystem ps, SurfaceType surface, float speedKph)
        {
            if (ps == null) return;

            var emission = ps.emission;
            var main     = ps.main;

            if (surface == SurfaceType.Asphalt || speedKph < 5f)
            {
                emission.enabled = false;
                return;
            }

            emission.enabled = true;

            float maxRate = GetMaxRate(surface);
            // Scale linearly from 5 km/h (threshold) up to 120 km/h
            float t    = Mathf.InverseLerp(5f, 120f, speedKph);
            float rate = Mathf.Lerp(5f, maxRate, t);
            emission.rateOverTime = rate;

            // Colour
            Color dustColor          = GetDustColor(surface);
            main.startColor          = new ParticleSystem.MinMaxGradient(dustColor);
        }

        // ------------------------------------------------------------------ //
        // Private Helpers
        // ------------------------------------------------------------------ //

        private SurfaceType ClassifySurface(RaycastHit hit)
        {
            // Primary method: check the GameObject tag
            string tag = hit.collider.tag;
            switch (tag)
            {
                case "Dirt":    return SurfaceType.Dirt;
                case "Grass":   return SurfaceType.Grass;
                case "Gravel":  return SurfaceType.Gravel;
                case "Mud":     return SurfaceType.Mud;
                case "Asphalt":
                case "Road":    return SurfaceType.Asphalt;
            }

            // Fallback: check physics material name
            if (hit.collider.sharedMaterial != null)
            {
                string matName = hit.collider.sharedMaterial.name.ToLowerInvariant();
                if (matName.Contains("dirt"))   return SurfaceType.Dirt;
                if (matName.Contains("grass"))  return SurfaceType.Grass;
                if (matName.Contains("gravel")) return SurfaceType.Gravel;
                if (matName.Contains("mud"))    return SurfaceType.Mud;
                if (matName.Contains("road") || matName.Contains("asphalt"))
                    return SurfaceType.Asphalt;
            }

            // Default: Dirt for unpaved surfaces
            return SurfaceType.Dirt;
        }

        private float GetMaxRate(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Dirt:    return dirtRate;
                case SurfaceType.Grass:   return grassRate;
                case SurfaceType.Gravel:  return gravelRate;
                case SurfaceType.Mud:     return mudRate;
                default:                  return asphaltRate;
            }
        }
    }
}
