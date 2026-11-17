using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeavyVehiclesWorld.World
{
    public enum WeatherState
    {
        Clear,
        Rain,
        Fog,
        Storm
    }

    [Serializable]
    public class WeatherAudioSettings
    {
        public AudioClip rainAmbience;
        public AudioClip thunderClip;
        public AudioClip windAmbience;
    }

    public class WeatherSystem : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private WeatherState currentState = WeatherState.Clear;
        [SerializeField] private float transitionDuration = 5f;

        [Header("References")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private Volume fogVolume;
        [SerializeField] private WindZone windZone;
        [SerializeField] private Light sunLight;

        [Header("Random Weather")]
        [Tooltip("Time in minutes between automatic weather changes")]
        [SerializeField] private float randomWeatherTimer = 10f;
        [SerializeField] private bool enableRandomWeather = true;

        [Header("Audio")]
        [SerializeField] private AudioSource weatherAudioSource;
        [SerializeField] private AudioSource thunderAudioSource;
        [SerializeField] private WeatherAudioSettings audioSettings;

        [Header("Weather Intensities")]
        [SerializeField] private float rainHeavyEmissionRate = 500f;
        [SerializeField] private float rainLightEmissionRate = 150f;
        [SerializeField] private float stormWindMain = 2.5f;
        [SerializeField] private float calmWindMain = 0.1f;

        // Ambient colours per state
        private static readonly Color AmbientClear  = new Color(0.6f, 0.7f, 1.0f);
        private static readonly Color AmbientRain   = new Color(0.3f, 0.35f, 0.45f);
        private static readonly Color AmbientFog    = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color AmbientStorm  = new Color(0.15f, 0.17f, 0.22f);

        private Coroutine _transitionCoroutine;
        private Coroutine _randomWeatherCoroutine;
        private UnityEngine.Rendering.Universal.Vignette _vignetteOverride;

        // ─── Fog volume profile override ───────────────────────────────────────
        private UnityEngine.Rendering.Universal.ColorAdjustments _colorAdjustments;
        private bool _fogVolumeReady;

        public WeatherState CurrentState   => currentState;
        /// <summary>Alias for CurrentState — used by HUDWeatherIndicator and WeatherAudioController.</summary>
        public WeatherState CurrentWeather => currentState;

        /// <summary>Fired whenever the weather finishes transitioning to a new state.</summary>
        public event System.Action<WeatherState> OnWeatherChanged;

        // ───────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (fogVolume != null && fogVolume.profile != null)
            {
                _fogVolumeReady = fogVolume.profile.TryGet(out _colorAdjustments);
            }

            // Ensure rain particles start stopped
            if (rainParticles != null)
                rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Start()
        {
            ApplyStateInstant(currentState);

            if (enableRandomWeather)
                _randomWeatherCoroutine = StartCoroutine(RandomWeatherRoutine());
        }

        // ───────────────────────────────────────────────────────────────────────
        /// <summary>Smoothly transitions to <paramref name="newState"/> over
        /// <paramref name="transitionTime"/> seconds.</summary>
        public void TransitionToWeather(WeatherState newState, float transitionTime)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(WeatherTransitionRoutine(newState, transitionTime));
        }

        // Convenience overload using the inspector-configured duration.
        public void TransitionToWeather(WeatherState newState)
            => TransitionToWeather(newState, transitionDuration);

        // ───────────────────────────────────────────────────────────────────────
        private IEnumerator WeatherTransitionRoutine(WeatherState newState, float duration)
        {
            WeatherState previousState = currentState;
            currentState = newState;

            float elapsed = 0f;
            float startFogWeight = fogVolume != null ? fogVolume.weight : 0f;
            float targetFogWeight = GetFogWeightForState(newState);

            Color startAmbient = RenderSettings.ambientLight;
            Color targetAmbient = GetAmbientForState(newState);

            float startSunIntensity = sunLight != null ? sunLight.intensity : 1f;
            float targetSunIntensity = GetSunIntensityForState(newState);

            // Start weather-specific effects immediately (particles, wind)
            PrepareEffectsForState(newState, previousState);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Lerp ambient colour
                RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);

                // Lerp fog volume weight
                if (fogVolume != null)
                    fogVolume.weight = Mathf.Lerp(startFogWeight, targetFogWeight, t);

                // Lerp sun intensity
                if (sunLight != null)
                    sunLight.intensity = Mathf.Lerp(startSunIntensity, targetSunIntensity, t);

                yield return null;
            }

            RenderSettings.ambientLight = targetAmbient;
            if (fogVolume != null) fogVolume.weight = targetFogWeight;
            if (sunLight != null) sunLight.intensity = targetSunIntensity;

            FinaliseEffectsForState(newState);

            // Notify subscribers (WeatherAudioController, HUDWeatherIndicator, etc.)
            OnWeatherChanged?.Invoke(newState);
        }

        // ───────────────────────────────────────────────────────────────────────
        private void PrepareEffectsForState(WeatherState newState, WeatherState prev)
        {
            switch (newState)
            {
                case WeatherState.Clear:
                    StopRain();
                    SetWind(calmWindMain);
                    StopWeatherAudio();
                    break;

                case WeatherState.Rain:
                    StartRain(rainLightEmissionRate);
                    SetWind(0.5f);
                    PlayAmbience(audioSettings?.rainAmbience);
                    break;

                case WeatherState.Fog:
                    StopRain();
                    SetWind(0.2f);
                    StopWeatherAudio();
                    break;

                case WeatherState.Storm:
                    StartRain(rainHeavyEmissionRate);
                    SetWind(stormWindMain);
                    PlayAmbience(audioSettings?.rainAmbience);
                    StartCoroutine(ThunderRoutine());
                    break;
            }
        }

        private void FinaliseEffectsForState(WeatherState state)
        {
            // Any post-transition cleanup if needed
        }

        private void ApplyStateInstant(WeatherState state)
        {
            RenderSettings.ambientLight = GetAmbientForState(state);
            if (fogVolume != null) fogVolume.weight = GetFogWeightForState(state);
            if (sunLight != null) sunLight.intensity = GetSunIntensityForState(state);
            PrepareEffectsForState(state, state);
        }

        // ───────────────────────────────────────────────────────────────────────
        private IEnumerator RandomWeatherRoutine()
        {
            while (true)
            {
                float waitSeconds = randomWeatherTimer * 60f;
                yield return new WaitForSeconds(waitSeconds);

                WeatherState[] allStates = (WeatherState[])Enum.GetValues(typeof(WeatherState));
                WeatherState next = allStates[UnityEngine.Random.Range(0, allStates.Length)];
                TransitionToWeather(next, transitionDuration);
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        private IEnumerator ThunderRoutine()
        {
            while (currentState == WeatherState.Storm)
            {
                float delay = UnityEngine.Random.Range(5f, 20f);
                yield return new WaitForSeconds(delay);

                if (currentState != WeatherState.Storm) yield break;

                if (thunderAudioSource != null && audioSettings?.thunderClip != null)
                    thunderAudioSource.PlayOneShot(audioSettings.thunderClip);
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        #region Helpers

        private void StartRain(float emissionRate)
        {
            if (rainParticles == null) return;
            var emission = rainParticles.emission;
            emission.rateOverTime = emissionRate;
            if (!rainParticles.isPlaying)
                rainParticles.Play();
        }

        private void StopRain()
        {
            if (rainParticles == null) return;
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void SetWind(float mainValue)
        {
            if (windZone == null) return;
            windZone.windMain = mainValue;
        }

        private void PlayAmbience(AudioClip clip)
        {
            if (weatherAudioSource == null || clip == null) return;
            if (weatherAudioSource.clip == clip && weatherAudioSource.isPlaying) return;
            weatherAudioSource.clip = clip;
            weatherAudioSource.loop = true;
            weatherAudioSource.Play();
        }

        private void StopWeatherAudio()
        {
            if (weatherAudioSource != null)
                weatherAudioSource.Stop();
        }

        private static Color GetAmbientForState(WeatherState state) => state switch
        {
            WeatherState.Clear  => AmbientClear,
            WeatherState.Rain   => AmbientRain,
            WeatherState.Fog    => AmbientFog,
            WeatherState.Storm  => AmbientStorm,
            _                   => AmbientClear
        };

        private static float GetFogWeightForState(WeatherState state) => state switch
        {
            WeatherState.Clear  => 0f,
            WeatherState.Rain   => 0.3f,
            WeatherState.Fog    => 1.0f,
            WeatherState.Storm  => 0.6f,
            _                   => 0f
        };

        private static float GetSunIntensityForState(WeatherState state) => state switch
        {
            WeatherState.Clear  => 1.0f,
            WeatherState.Rain   => 0.4f,
            WeatherState.Fog    => 0.5f,
            WeatherState.Storm  => 0.1f,
            _                   => 1.0f
        };

        #endregion
    }
}
