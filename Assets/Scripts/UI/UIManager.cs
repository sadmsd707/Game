using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// Controls all in-game UI panels and updates the vehicle HUD each frame.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Singleton (optional convenience) ──────────────────────────────
        public static UIManager Instance { get; private set; }

        // ── Panels ────────────────────────────────────────────────────────
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject vehicleHUDPanel;
        [SerializeField] private GameObject minimapPanel;
        [SerializeField] private GameObject fullMapPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject loadingPanel;

        // ── Vehicle HUD elements ──────────────────────────────────────────
        [Header("Vehicle HUD")]
        [SerializeField] private TMP_Text speedometerText;
        [SerializeField] private Slider   fuelBar;
        [SerializeField] private Image    damageIndicator;
        [SerializeField] private Slider   rpmGauge;

        [Header("Loading")]
        [SerializeField] private TMP_Text loadingMessageText;

        // ── Map state ─────────────────────────────────────────────────────
        private bool _isFullMapOpen;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            ShowMainMenu();
        }

        // ── Panel helpers ─────────────────────────────────────────────────
        private void HideAll()
        {
            SetActive(mainMenuPanel,   false);
            SetActive(pauseMenuPanel,  false);
            SetActive(hudPanel,        false);
            SetActive(vehicleHUDPanel, false);
            SetActive(minimapPanel,    false);
            SetActive(fullMapPanel,    false);
            SetActive(settingsPanel,   false);
            // loadingPanel is managed independently
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        // ── Public API ────────────────────────────────────────────────────
        public void ShowMainMenu()
        {
            HideAll();
            SetActive(mainMenuPanel, true);
        }

        public void ShowHUD()
        {
            HideAll();
            SetActive(hudPanel,     true);
            SetActive(minimapPanel, true);
        }

        public void ShowPauseMenu()
        {
            SetActive(pauseMenuPanel, true);
        }

        public void ShowSettings()
        {
            SetActive(settingsPanel, true);
        }

        public void ShowVehicleHUD(VehicleController v)
        {
            ShowHUD();
            SetActive(vehicleHUDPanel, true);
        }

        public void HideVehicleHUD()
        {
            SetActive(vehicleHUDPanel, false);
        }

        public void ToggleMap()
        {
            _isFullMapOpen = !_isFullMapOpen;
            SetActive(fullMapPanel,  _isFullMapOpen);
            SetActive(minimapPanel, !_isFullMapOpen);
        }

        public void ShowLoading(string message)
        {
            if (loadingMessageText != null) loadingMessageText.text = message;
            SetActive(loadingPanel, true);
        }

        public void HideLoading()
        {
            SetActive(loadingPanel, false);
        }

        /// <summary>Call every frame while in a vehicle to keep the HUD current.</summary>
        public void UpdateVehicleHUD(float speed, float fuel, float damage, float rpm)
        {
            if (speedometerText != null)
                speedometerText.text = $"{Mathf.RoundToInt(speed)} km/h";

            if (fuelBar != null)
                fuelBar.value = fuel / 100f;

            if (damageIndicator != null)
                damageIndicator.color = Color.Lerp(Color.green, Color.red, damage / 100f);

            if (rpmGauge != null)
                rpmGauge.value = rpm / 8000f;   // normalised to 0-1 assuming 8 000 rpm max
        }
    }
}
