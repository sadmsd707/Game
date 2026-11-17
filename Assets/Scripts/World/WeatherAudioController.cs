using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// WeatherAudioController – fades rain, wind, and thunder audio in/out
    /// as the WeatherSystem transitions between states.
    /// Requires three AudioSource components on the same GameObject (or assign in Inspector).
    /// </summary>
    public class WeatherAudioController : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource rainSource;
        [SerializeField] private AudioSource windSource;
        [SerializeField] private AudioSource thunderSource;

        [Header("Rain Clips")]
        [SerializeField] private AudioClip lightRainClip;
        [SerializeField] private AudioClip heavyRainClip;

        [Header("Wind Clips")]
        [SerializeField] private AudioClip lightWindClip;
        [SerializeField] private AudioClip stormWindClip;

        [Header("Thunder")]
        [SerializeField] private AudioClip[] thunderClips;
        [SerializeField] private float thunderMinInterval = 5f;
        [SerializeField] private float thunderMaxInterval = 20f;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 2f;

        private WeatherSystem _weatherSystem;
        private WeatherState  _currentState = WeatherState.Clear;
        private Coroutine     _rainFade;
        private Coroutine     _windFade;
        private Coroutine     _thunderRoutine;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            _weatherSystem = FindObjectOfType<WeatherSystem>();
            if (_weatherSystem != null)
                _weatherSystem.OnWeatherChanged += HandleWeatherChanged;

            // ensure all sources loop
            if (rainSource)    { rainSource.loop    = true; rainSource.volume    = 0f; rainSource.Play(); }
            if (windSource)    { windSource.loop    = true; windSource.volume    = 0f; windSource.Play(); }
            if (thunderSource) { thunderSource.loop = false; }
        }

        private void OnDestroy()
        {
            if (_weatherSystem != null)
                _weatherSystem.OnWeatherChanged -= HandleWeatherChanged;
        }

        // ── Weather callback ───────────────────────────────────────────────────────

        private void HandleWeatherChanged(WeatherState newState)
        {
            _currentState = newState;

            switch (newState)
            {
                case WeatherState.Clear:
                    SetTargets(rain: 0f, wind: 0f);
                    StopThunder();
                    break;

                case WeatherState.Rain:
                    SwapClip(rainSource, lightRainClip);
                    SwapClip(windSource, lightWindClip);
                    SetTargets(rain: 0.6f, wind: 0.3f);
                    StopThunder();
                    break;

                case WeatherState.Fog:
                    SetTargets(rain: 0f, wind: 0.15f);
                    SwapClip(windSource, lightWindClip);
                    StopThunder();
                    break;

                case WeatherState.Storm:
                    SwapClip(rainSource, heavyRainClip);
                    SwapClip(windSource, stormWindClip);
                    SetTargets(rain: 0.9f, wind: 0.8f);
                    StartThunder();
                    break;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private void SetTargets(float rain, float wind)
        {
            if (_rainFade != null) StopCoroutine(_rainFade);
            if (_windFade != null) StopCoroutine(_windFade);

            if (rainSource) _rainFade = StartCoroutine(FadeVolume(rainSource, rain, fadeDuration));
            if (windSource) _windFade = StartCoroutine(FadeVolume(windSource, wind, fadeDuration));
        }

        private IEnumerator FadeVolume(AudioSource src, float target, float duration)
        {
            float start   = src.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed   += Time.deltaTime;
                src.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            src.volume = target;
        }

        private void SwapClip(AudioSource src, AudioClip clip)
        {
            if (src == null || src.clip == clip) return;
            bool wasPlaying = src.isPlaying;
            src.clip = clip;
            if (clip != null && wasPlaying) src.Play();
            else if (clip == null) src.Stop();
        }

        private void StartThunder()
        {
            StopThunder();
            if (thunderClips != null && thunderClips.Length > 0)
                _thunderRoutine = StartCoroutine(ThunderRoutine());
        }

        private void StopThunder()
        {
            if (_thunderRoutine != null) { StopCoroutine(_thunderRoutine); _thunderRoutine = null; }
        }

        private IEnumerator ThunderRoutine()
        {
            while (true)
            {
                float wait = Random.Range(thunderMinInterval, thunderMaxInterval);
                yield return new WaitForSeconds(wait);
                if (_currentState == WeatherState.Storm && thunderSource != null && thunderClips.Length > 0)
                {
                    AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
                    thunderSource.PlayOneShot(clip, Random.Range(0.6f, 1f));
                }
            }
        }
    }
}

