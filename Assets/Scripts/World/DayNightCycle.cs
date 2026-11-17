using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Drives a 24-hour day/night cycle by rotating the sun and lerping
    /// sky + ambient colours through designer-specified gradients.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        // ── Sun ───────────────────────────────────────────────────────────
        [Header("Sun")]
        [SerializeField] private Light sun;

        // ── Colour gradients ──────────────────────────────────────────────
        [Header("Sky & Ambient")]
        [SerializeField] private Gradient skyColorGradient;
        [SerializeField] private Gradient ambientColorGradient;

        // ── Time settings ─────────────────────────────────────────────────
        [Header("Time")]
        [Range(0f, 24f)] public float timeOfDay = 6f;         // 6 = sunrise
        public float dayDurationSeconds = 600f;                 // 10 real minutes = 1 game day

        // ── Events ────────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent OnSunrise;
        public UnityEvent OnSunset;

        // ── Public read-only property (used by MinimapUI, HUDWeatherIndicator) ─
        public float TimeOfDay => timeOfDay;

        // ── Internal state ────────────────────────────────────────────────
        private bool _sunriseTriggered;
        private bool _sunsetTriggered;

        // Sunrise / sunset thresholds (hours)
        private const float SunriseHour = 6f;
        private const float SunsetHour  = 18f;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Update()
        {
            AdvanceTime();
            RotateSun();
            UpdateSkyAndAmbient();
            CheckDawnDusk();
        }

        // ── Private helpers ───────────────────────────────────────────────
        private void AdvanceTime()
        {
            float increment = (24f / dayDurationSeconds) * Time.deltaTime;
            timeOfDay = (timeOfDay + increment) % 24f;
        }

        private void RotateSun()
        {
            if (sun == null) return;
            // Map 0-24 h → 0-360°; midday (12 h) = directly overhead (pitch 90°)
            float pitch = (timeOfDay / 24f) * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(pitch, 170f, 0f);

            // Fade sun intensity: full between sunrise and sunset, zero at night
            float t = Mathf.InverseLerp(SunriseHour - 1f, SunriseHour + 1f, timeOfDay)
                    * Mathf.InverseLerp(SunsetHour  + 1f, SunsetHour  - 1f, timeOfDay);
            sun.intensity = Mathf.Lerp(0f, 1.2f, t);
        }

        /// <summary>Lerps sky colour and ambient light through the gradient based on time of day.</summary>
        private void UpdateSkyAndAmbient()
        {
            float t = timeOfDay / 24f;

            if (skyColorGradient != null)
                RenderSettings.fogColor = skyColorGradient.Evaluate(t);

            if (ambientColorGradient != null)
                RenderSettings.ambientLight = ambientColorGradient.Evaluate(t);
        }

        private void CheckDawnDusk()
        {
            // Sunrise – fires once per cycle
            if (!_sunriseTriggered && timeOfDay >= SunriseHour && timeOfDay < SunriseHour + 0.1f)
            {
                _sunriseTriggered = true;
                _sunsetTriggered  = false;
                OnSunrise?.Invoke();
            }

            // Sunset – fires once per cycle
            if (!_sunsetTriggered && timeOfDay >= SunsetHour && timeOfDay < SunsetHour + 0.1f)
            {
                _sunsetTriggered  = true;
                _sunriseTriggered = false;
                OnSunset?.Invoke();
            }
        }
    }
}
