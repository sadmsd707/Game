using UnityEngine;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Master environment controller — ties DayNightCycle, WeatherSystem, and ZoneManager together.
    /// Applies per-zone fog/ambient settings and schedules randomised weather events.
    /// </summary>
    public class WorldEnvironmentController : MonoBehaviour
    {
        [Header("System References (auto-found if not assigned)")]
        public DayNightCycle  dayNight;
        public WeatherSystem  weather;
        public ZoneManager    zones;

        [Header("Zone Transition")]
        [Tooltip("How fast fog and ambient smoothly blend when entering a new zone.")]
        public float zoneBlendSpeed = 2f;

        [Header("Weather Scheduling")]
        [Tooltip("Minimum real minutes between random weather changes (game-time scaled).")]
        public float minWeatherIntervalMinutes = 5f;
        [Tooltip("Maximum real minutes between random weather changes (game-time scaled).")]
        public float maxWeatherIntervalMinutes = 15f;

        [Header("World Map Data (optional — used for zone definitions)")]
        public WorldMapData mapData;

        // ── Private state ─────────────────────────────────────────────────────
        private WorldZone _currentZone;
        private float     _zoneTransitionTimer;

        private float _weatherTimer;
        private float _nextWeatherInterval;

        private float _targetFogDensity;
        private float _targetAmbientIntensity;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            // Auto-resolve references if not wired in the Inspector
            if (dayNight == null) dayNight = FindObjectOfType<DayNightCycle>();
            if (weather  == null) weather  = FindObjectOfType<WeatherSystem>();
            if (zones    == null) zones    = FindObjectOfType<ZoneManager>();

            if (dayNight == null) Debug.LogWarning("[Environment] DayNightCycle not found.");
            if (weather  == null) Debug.LogWarning("[Environment] WeatherSystem not found.");
            if (zones    == null) Debug.LogWarning("[Environment] ZoneManager not found.");

            // Initialise weather scheduler
            _nextWeatherInterval = RandomWeatherInterval();

            // Seed current zone environment
            _currentZone = zones != null ? zones.CurrentZone : WorldZone.Highway;
            ApplyZoneEnvironmentImmediate(_currentZone);
        }

        private void Update()
        {
            HandleZoneTransition();
            HandleWeatherScheduler();
            SmoothEnvironmentBlend();
        }

        // ── Zone handling ─────────────────────────────────────────────────────

        private void HandleZoneTransition()
        {
            if (zones == null) return;

            WorldZone newZone = zones.CurrentZone;
            if (newZone != _currentZone)
            {
                _currentZone = newZone;
                _zoneTransitionTimer = 0f;
                SetZoneTargets(_currentZone);
                Debug.Log($"[Environment] Entered zone: {_currentZone}");
            }

            _zoneTransitionTimer += Time.deltaTime;
        }

        private void SmoothEnvironmentBlend()
        {
            RenderSettings.fogDensity = Mathf.Lerp(
                RenderSettings.fogDensity,
                _targetFogDensity,
                Time.deltaTime * zoneBlendSpeed);

            RenderSettings.ambientIntensity = Mathf.Lerp(
                RenderSettings.ambientIntensity,
                _targetAmbientIntensity,
                Time.deltaTime * zoneBlendSpeed);
        }

        private void SetZoneTargets(WorldZone zone)
        {
            if (mapData == null || mapData.zones == null) return;

            foreach (var def in mapData.zones)
            {
                if (def.zoneType == zone)
                {
                    _targetFogDensity       = def.fogDensity;
                    _targetAmbientIntensity = def.ambientIntensity;
                    return;
                }
            }
        }

        private void ApplyZoneEnvironmentImmediate(WorldZone zone)
        {
            SetZoneTargets(zone);
            RenderSettings.fogDensity       = _targetFogDensity;
            RenderSettings.ambientIntensity = _targetAmbientIntensity;
        }

        /// <summary>
        /// Smoothly applies a zone definition's fog and ambient settings.
        /// Call this directly when you have a ZoneDefinition reference.
        /// </summary>
        public void ApplyZoneEnvironment(ZoneDefinition zone)
        {
            if (zone == null) return;
            _targetFogDensity       = zone.fogDensity;
            _targetAmbientIntensity = zone.ambientIntensity;
        }

        // ── Weather scheduling ────────────────────────────────────────────────

        private void HandleWeatherScheduler()
        {
            if (weather == null) return;

            // Scale by time-of-day: rain more likely at dawn (5-8) and dusk (18-21)
            float tod           = dayNight != null ? dayNight.timeOfDay : 12f;
            float dawnDuskBoost = IsDawnOrDusk(tod) ? 1.5f : 1f;

            _weatherTimer += Time.deltaTime * dawnDuskBoost;

            if (_weatherTimer >= _nextWeatherInterval * 60f)   // convert minutes → seconds
            {
                _weatherTimer        = 0f;
                _nextWeatherInterval = RandomWeatherInterval();

                WeatherState next = PickRandomWeather(tod);
                TriggerWeatherEvent(next);
            }
        }

        private WeatherState PickRandomWeather(float timeOfDay)
        {
            // Higher rain/fog weight at dawn/dusk, storm rarer at night
            float r = Random.value;
            if (IsNight)
            {
                if (r < 0.50f) return WeatherState.Clear;
                if (r < 0.70f) return WeatherState.Fog;
                if (r < 0.90f) return WeatherState.Rain;
                return WeatherState.Storm;
            }
            else if (IsDawnOrDusk(timeOfDay))
            {
                if (r < 0.30f) return WeatherState.Clear;
                if (r < 0.60f) return WeatherState.Rain;
                if (r < 0.80f) return WeatherState.Fog;
                return WeatherState.Storm;
            }
            else
            {
                if (r < 0.50f) return WeatherState.Clear;
                if (r < 0.75f) return WeatherState.Rain;
                if (r < 0.90f) return WeatherState.Fog;
                return WeatherState.Storm;
            }
        }

        private float RandomWeatherInterval()
            => Random.Range(minWeatherIntervalMinutes, maxWeatherIntervalMinutes);

        private bool IsDawnOrDusk(float tod)
            => (tod >= 5f && tod <= 8f) || (tod >= 18f && tod <= 21f);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>True when the in-game time is between 20:00 and 06:00.</summary>
        public bool IsNight
        {
            get
            {
                if (dayNight == null) return false;
                float t = dayNight.timeOfDay;
                return t >= 20f || t < 6f;
            }
        }

        /// <summary>Immediately transitions to the specified weather state and logs it.</summary>
        public void TriggerWeatherEvent(WeatherState state)
        {
            if (weather == null) return;
            weather.TransitionToWeather(state);
            Debug.Log($"[Environment] Weather: {state}");
        }
    }
}
