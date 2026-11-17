using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// HUDWeatherIndicator – small HUD widget showing:
    ///   • Current weather icon + label (Clear / Rain / Fog / Storm)
    ///   • Sun or moon icon based on time of day
    /// Attach to any HUD Canvas. Assign sprite references in the Inspector.
    /// </summary>
    public class HUDWeatherIndicator : MonoBehaviour
    {
        [Header("Weather Icon & Label")]
        [SerializeField] private Image            weatherIcon;
        [SerializeField] private TextMeshProUGUI  weatherLabel;

        [Header("Weather Sprites")]
        [SerializeField] private Sprite clearSprite;
        [SerializeField] private Sprite rainSprite;
        [SerializeField] private Sprite fogSprite;
        [SerializeField] private Sprite stormSprite;

        [Header("Sun / Moon")]
        [SerializeField] private Image  sunMoonIcon;
        [SerializeField] private Sprite sunSprite;
        [SerializeField] private Sprite moonSprite;
        [SerializeField] [Range(0f, 24f)] private float sunriseHour = 6f;
        [SerializeField] [Range(0f, 24f)] private float sunsetHour  = 20f;

        // runtime refs
        private WeatherSystem _weatherSystem;
        private DayNightCycle _dayNight;

        private void Start()
        {
            _weatherSystem = FindObjectOfType<WeatherSystem>();
            _dayNight      = FindObjectOfType<DayNightCycle>();

            if (_weatherSystem != null)
                _weatherSystem.OnWeatherChanged += RefreshWeatherDisplay;

            // initial display
            if (_weatherSystem != null) RefreshWeatherDisplay(_weatherSystem.CurrentWeather);
        }

        private void OnDestroy()
        {
            if (_weatherSystem != null)
                _weatherSystem.OnWeatherChanged -= RefreshWeatherDisplay;
        }

        private void Update()
        {
            UpdateSunMoon();
        }

        // ── Weather display ────────────────────────────────────────────────────────

        private void RefreshWeatherDisplay(WeatherState state)
        {
            if (weatherLabel != null) weatherLabel.text = state.ToString().ToUpper();

            if (weatherIcon == null) return;

            switch (state)
            {
                case WeatherState.Clear: weatherIcon.sprite = clearSprite; break;
                case WeatherState.Rain:  weatherIcon.sprite = rainSprite;  break;
                case WeatherState.Fog:   weatherIcon.sprite = fogSprite;   break;
                case WeatherState.Storm: weatherIcon.sprite = stormSprite; break;
                default:                 weatherIcon.sprite = clearSprite; break;
            }
        }

        // ── Sun / Moon display ─────────────────────────────────────────────────────

        private void UpdateSunMoon()
        {
            if (sunMoonIcon == null || _dayNight == null) return;
            float t    = _dayNight.TimeOfDay;
            bool isDay = t >= sunriseHour && t < sunsetHour;
            sunMoonIcon.sprite = isDay ? sunSprite : moonSprite;
        }
    }
}

