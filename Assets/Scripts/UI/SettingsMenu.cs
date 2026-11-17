using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// Settings panel controller.
    /// Attach to the settings panel GameObject in the main menu or pause menu.
    /// All UI controls are wired via the Inspector.
    /// Settings are persisted with PlayerPrefs.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  PlayerPrefs Keys                                                    //
        // ------------------------------------------------------------------ //
        private const string KEY_QUALITY      = "Settings_GraphicsQuality";
        private const string KEY_FPS          = "Settings_FPSCap";
        private const string KEY_VOL_MASTER   = "Settings_VolumeMaster";
        private const string KEY_VOL_SFX      = "Settings_VolumeSFX";
        private const string KEY_VOL_MUSIC    = "Settings_VolumeMusic";
        private const string KEY_SENSITIVITY  = "Settings_Sensitivity";

        // ------------------------------------------------------------------ //
        //  Graphics                                                            //
        // ------------------------------------------------------------------ //
        [Header("Graphics")]
        [Tooltip("Dropdown options must be in order: Low, Medium, High, Ultra")]
        [SerializeField] private TMP_Dropdown graphicsQualityDropdown;
        [Tooltip("Dropdown options must be in order: 30, 60, Unlimited")]
        [SerializeField] private TMP_Dropdown fpsCapDropdown;

        // ------------------------------------------------------------------ //
        //  Audio                                                               //
        // ------------------------------------------------------------------ //
        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;

        [Header("Audio Labels (optional)")]
        [SerializeField] private TMP_Text masterVolumeLabel;
        [SerializeField] private TMP_Text sfxVolumeLabel;
        [SerializeField] private TMP_Text musicVolumeLabel;

        // ------------------------------------------------------------------ //
        //  Controls                                                            //
        // ------------------------------------------------------------------ //
        [Header("Controls")]
        [Tooltip("Touch steering sensitivity, range 0.1 – 2.0")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityLabel;

        // ------------------------------------------------------------------ //
        //  Buttons                                                             //
        // ------------------------------------------------------------------ //
        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button backButton;

        // ------------------------------------------------------------------ //
        //  FPS cap values matching dropdown order                              //
        // ------------------------------------------------------------------ //
        private static readonly int[] FpsCapValues = { 30, 60, -1 }; // -1 = unlimited

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (backButton  != null) backButton.onClick.AddListener(OnBackClicked);

            // Wire live-update labels
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(v => UpdateLabel(masterVolumeLabel, v, "Master"));
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(v => UpdateLabel(sfxVolumeLabel, v, "SFX"));
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(v => UpdateLabel(musicVolumeLabel, v, "Music"));
            if (sensitivitySlider != null)
                sensitivitySlider.onValueChanged.AddListener(v =>
                {
                    if (sensitivityLabel != null)
                        sensitivityLabel.text = $"Sensitivity: {v:F2}";
                });
        }

        private void Start()
        {
            LoadSettings();
        }

        // ------------------------------------------------------------------ //
        //  Load / Save                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Populates all controls from PlayerPrefs (or defaults).</summary>
        public void LoadSettings()
        {
            int quality     = PlayerPrefs.GetInt(KEY_QUALITY,    2);  // default: High
            int fps         = PlayerPrefs.GetInt(KEY_FPS,        1);  // default: 60
            float volMaster = PlayerPrefs.GetFloat(KEY_VOL_MASTER, 1.0f);
            float volSFX    = PlayerPrefs.GetFloat(KEY_VOL_SFX,   1.0f);
            float volMusic  = PlayerPrefs.GetFloat(KEY_VOL_MUSIC,  0.7f);
            float sens      = PlayerPrefs.GetFloat(KEY_SENSITIVITY, 1.0f);

            if (graphicsQualityDropdown != null) graphicsQualityDropdown.value = quality;
            if (fpsCapDropdown          != null) fpsCapDropdown.value          = fps;

            if (masterVolumeSlider != null) masterVolumeSlider.value = volMaster;
            if (sfxVolumeSlider    != null) sfxVolumeSlider.value    = volSFX;
            if (musicVolumeSlider  != null) musicVolumeSlider.value  = volMusic;

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.1f;
                sensitivitySlider.maxValue = 2.0f;
                sensitivitySlider.value    = sens;
            }

            // Force label refresh
            UpdateLabel(masterVolumeLabel, volMaster, "Master");
            UpdateLabel(sfxVolumeLabel,    volSFX,    "SFX");
            UpdateLabel(musicVolumeLabel,  volMusic,  "Music");
            if (sensitivityLabel != null)
                sensitivityLabel.text = $"Sensitivity: {sens:F2}";
        }

        /// <summary>Writes current control values to PlayerPrefs.</summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetInt(KEY_QUALITY,
                graphicsQualityDropdown != null ? graphicsQualityDropdown.value : 2);
            PlayerPrefs.SetInt(KEY_FPS,
                fpsCapDropdown != null ? fpsCapDropdown.value : 1);

            PlayerPrefs.SetFloat(KEY_VOL_MASTER,
                masterVolumeSlider != null ? masterVolumeSlider.value : 1f);
            PlayerPrefs.SetFloat(KEY_VOL_SFX,
                sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f);
            PlayerPrefs.SetFloat(KEY_VOL_MUSIC,
                musicVolumeSlider != null ? musicVolumeSlider.value : 0.7f);
            PlayerPrefs.SetFloat(KEY_SENSITIVITY,
                sensitivitySlider != null ? sensitivitySlider.value : 1f);

            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ //
        //  Apply                                                               //
        // ------------------------------------------------------------------ //

        private void OnApplyClicked()
        {
            ApplyAll();
            SaveSettings();
        }

        /// <summary>Applies all settings to running systems.</summary>
        public void ApplyAll()
        {
            // Graphics quality
            if (graphicsQualityDropdown != null)
            {
                int q = graphicsQualityDropdown.value;
                QualitySettings.SetQualityLevel(q, true);

                if (GraphicsSettingsManager.Instance != null)
                    GraphicsSettingsManager.Instance.ApplyQuality(q);
            }

            // FPS cap
            if (fpsCapDropdown != null)
            {
                int idx = fpsCapDropdown.value;
                Application.targetFrameRate = (idx < FpsCapValues.Length) ? FpsCapValues[idx] : -1;
            }

            // Volume
            float master = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
            float sfx    = sfxVolumeSlider    != null ? sfxVolumeSlider.value    : 1f;
            float music  = musicVolumeSlider  != null ? musicVolumeSlider.value  : 0.7f;

            AudioListener.volume = master;

            // If a dedicated AudioMixer is wired up, set groups here.
            // AudioMixer.SetFloat("SFX",   Mathf.Log10(sfx)   * 20);
            // AudioMixer.SetFloat("Music", Mathf.Log10(music) * 20);
            // For now, PlayerPrefs values are the source of truth for other systems to read.
        }

        // ------------------------------------------------------------------ //
        //  Navigation                                                          //
        // ------------------------------------------------------------------ //

        private void OnBackClicked()
        {
            // Simply hide this panel; parent (MainMenu or PauseMenu) shows itself
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static void UpdateLabel(TMP_Text label, float value, string prefix)
        {
            if (label != null)
                label.text = $"{prefix}: {Mathf.RoundToInt(value * 100)}%";
        }
    }
}
