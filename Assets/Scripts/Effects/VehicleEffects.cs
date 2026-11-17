using UnityEngine;

namespace HeavyVehiclesWorld.Effects
{
    /// <summary>
    /// Visual effects controller for vehicles.
    /// Attach to the vehicle root. Wire up particle systems and trail renderers in the Inspector.
    /// Reads speed / throttle / brake state from the VehicleController on the same GameObject.
    /// </summary>
    public class VehicleEffects : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Inspector References
        // ------------------------------------------------------------------ //

        [Header("Exhaust")]
        [Tooltip("Exhaust smoke particle system (emits on engine on, more at high throttle).")]
        public ParticleSystem exhaustSmoke;

        [Header("Dust")]
        [Tooltip("Dust trail for each rear wheel. Emits when speed > 5 km/h on dirt surfaces.")]
        public ParticleSystem dustTrail;

        [Header("Tyre Smoke")]
        [Tooltip("Left tyre smoke (hard brake / handbrake).")]
        public ParticleSystem tyreSmokeLeft;
        [Tooltip("Right tyre smoke (hard brake / handbrake).")]
        public ParticleSystem tyreSmokeRight;

        [Header("Tyre Trails")]
        [Tooltip("One TrailRenderer per wheel (FL, FR, RL, RR order).")]
        public TrailRenderer[] tyreTrails = new TrailRenderer[4];

        // ------------------------------------------------------------------ //
        // Tuning
        // ------------------------------------------------------------------ //

        [Header("Thresholds")]
        [Tooltip("Minimum speed (km/h) before dust emits.")]
        public float dustSpeedThreshold = 5f;

        [Tooltip("Brake input above this triggers tyre smoke (0–1).")]
        public float smokeBrakeThreshold = 0.7f;

        [Tooltip("Max exhaust emission rate at full throttle.")]
        public float maxExhaustRate = 40f;

        [Tooltip("Idle exhaust emission rate.")]
        public float idleExhaustRate = 8f;

        // ------------------------------------------------------------------ //
        // Private
        // ------------------------------------------------------------------ //

        private HeavyVehiclesWorld.Vehicles.VehicleController _vehicle;

        // ------------------------------------------------------------------ //
        // Unity Lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _vehicle = GetComponent<HeavyVehiclesWorld.Vehicles.VehicleController>();
            if (_vehicle == null)
                Debug.LogWarning("[VehicleEffects] No VehicleController found on " + gameObject.name);
        }

        private void Update()
        {
            if (_vehicle == null) return;

            float speedKph  = _vehicle.currentSpeed * 3.6f;        // m/s → km/h
            float throttle  = Mathf.Clamp01(_vehicle.throttleInput);
            float brakeInput = Mathf.Clamp01(_vehicle.brakeInput);
            bool  hardBrake = brakeInput > smokeBrakeThreshold;
            bool  engineOn  = _vehicle.isEngineOn;

            UpdateExhaustSmoke(engineOn, throttle);
            UpdateDustTrail(speedKph);
            UpdateTyreSmoke(hardBrake, speedKph);
            UpdateTyreTrails(hardBrake, speedKph);
        }

        // ------------------------------------------------------------------ //
        // Private Helpers
        // ------------------------------------------------------------------ //

        private void UpdateExhaustSmoke(bool engineOn, float throttle)
        {
            if (exhaustSmoke == null) return;

            var emission = exhaustSmoke.emission;

            if (!engineOn)
            {
                emission.enabled = false;
                return;
            }

            emission.enabled = true;
            float rate = Mathf.Lerp(idleExhaustRate, maxExhaustRate, throttle);
            emission.rateOverTime = rate;
        }

        private void UpdateDustTrail(float speedKph)
        {
            if (dustTrail == null) return;

            var emission = dustTrail.emission;
            emission.enabled = speedKph > dustSpeedThreshold;

            if (emission.enabled)
            {
                // Scale emission rate with speed
                float rate = Mathf.Lerp(5f, 60f,
                    Mathf.InverseLerp(dustSpeedThreshold, 120f, speedKph));
                emission.rateOverTime = rate;
            }
        }

        private void UpdateTyreSmoke(bool hardBrake, float speedKph)
        {
            SetSmokeEmission(tyreSmokeLeft,  hardBrake && speedKph > 10f);
            SetSmokeEmission(tyreSmokeRight, hardBrake && speedKph > 10f);
        }

        private static void SetSmokeEmission(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.enabled = active;
        }

        private void UpdateTyreTrails(bool hardBrake, float speedKph)
        {
            bool emitTrails = hardBrake && speedKph > 10f;
            foreach (var tr in tyreTrails)
            {
                if (tr == null) continue;
                tr.emitting = emitTrails;
            }
        }
    }
}
