using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeavyVehiclesWorld.Systems
{
    public enum QualityLevel { Low, Medium, High, Ultra }
    public enum FPSCap       { Cap30, Cap60, Uncapped }

    // ─────────────────────────────────────────────────────────────────────────
    public class GraphicsSettingsManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("URP Assets – one per quality tier")]
        [SerializeField] private UniversalRenderPipelineAsset urpLow;
        [SerializeField] private UniversalRenderPipelineAsset urpMedium;
        [SerializeField] private UniversalRenderPipelineAsset urpHigh;
        [SerializeField] private UniversalRenderPipelineAsset urpUltra;

        [Header("Post-Processing Volume")]
        [SerializeField] private Volume globalPostProcessVolume;

        // ── PlayerPrefs keys ──────────────────────────────────────────────────
        private const string KeyQuality = "GFX_Quality";
        private const string KeyFPS     = "GFX_FPSCap";

        // ── State ─────────────────────────────────────────────────────────────
        private QualityLevel _currentQuality = QualityLevel.Medium;
        private FPSCap       _currentFPSCap  = FPSCap.Cap60;

        public QualityLevel CurrentQuality => _currentQuality;
        public FPSCap       CurrentFPSCap  => _currentFPSCap;

        // Singleton
        public static GraphicsSettingsManager Instance { get; private set; }

        // ── Cached volume overrides ───────────────────────────────────────────
        private Bloom           _bloom;
        private MotionBlur      _motionBlur;
        private ColorAdjustments _colorAdj;
        private DepthOfField    _dof;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (globalPostProcessVolume != null && globalPostProcessVolume.profile != null)
            {
                globalPostProcessVolume.profile.TryGet(out _bloom);
                globalPostProcessVolume.profile.TryGet(out _motionBlur);
                globalPostProcessVolume.profile.TryGet(out _colorAdj);
                globalPostProcessVolume.profile.TryGet(out _dof);
            }

            LoadSettings();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ApplyQuality(QualityLevel level)
        {
            _currentQuality = level;

            // Set Unity built-in quality
            int unityIdx = level switch
            {
                QualityLevel.Low    => 0,
                QualityLevel.Medium => 2,
                QualityLevel.High   => 4,
                QualityLevel.Ultra  => 5,
                _                   => 2
            };
            QualitySettings.SetQualityLevel(unityIdx, applyExpensiveChanges: true);

            // Assign the matching URP pipeline asset
            UniversalRenderPipelineAsset urpAsset = level switch
            {
                QualityLevel.Low    => urpLow,
                QualityLevel.Medium => urpMedium,
                QualityLevel.High   => urpHigh,
                QualityLevel.Ultra  => urpUltra,
                _                   => urpMedium
            };
            if (urpAsset != null)
                GraphicsSettings.renderPipelineAsset = urpAsset;

            // Post-processing per tier
            switch (level)
            {
                case QualityLevel.Low:
                    QualitySettings.shadows          = ShadowQuality.Disable;
                    QualitySettings.shadowDistance   = 0f;
                    SetPostProcessing(bloom: false, motionBlur: false, dof: false);
                    SetLightmapMode(LightmapsMode.NonDirectional);
                    break;

                case QualityLevel.Medium:
                    QualitySettings.shadows          = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance   = 80f;
                    SetPostProcessing(bloom: true, motionBlur: false, dof: false);
                    SetBloomIntensity(0.4f);
                    break;

                case QualityLevel.High:
                    QualitySettings.shadows          = ShadowQuality.All;
                    QualitySettings.shadowDistance   = 150f;
                    SetPostProcessing(bloom: true, motionBlur: false, dof: true);
                    SetBloomIntensity(0.7f);
                    break;

                case QualityLevel.Ultra:
                    QualitySettings.shadows          = ShadowQuality.All;
                    QualitySettings.shadowDistance   = 250f;
                    SetPostProcessing(bloom: true, motionBlur: true, dof: true);
                    SetBloomIntensity(1.0f);
                    break;
            }

            SaveSettings();
        }

        public void SetFPSCap(FPSCap cap)
        {
            _currentFPSCap = cap;
            Application.targetFrameRate = cap switch
            {
                FPSCap.Cap30    => 30,
                FPSCap.Cap60    => 60,
                FPSCap.Uncapped => -1,
                _               => 60
            };
            SaveSettings();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetInt(KeyQuality, (int)_currentQuality);
            PlayerPrefs.SetInt(KeyFPS,     (int)_currentFPSCap);
            PlayerPrefs.Save();
        }

        public void LoadSettings()
        {
            _currentQuality = (QualityLevel)PlayerPrefs.GetInt(KeyQuality, (int)QualityLevel.Medium);
            _currentFPSCap  = (FPSCap)PlayerPrefs.GetInt(KeyFPS,          (int)FPSCap.Cap60);

            ApplyQuality(_currentQuality);
            SetFPSCap(_currentFPSCap);
        }

        // ── Post-processing helpers ───────────────────────────────────────────

        private void SetPostProcessing(bool bloom, bool motionBlur, bool dof)
        {
            if (_bloom != null)      _bloom.active      = bloom;
            if (_motionBlur != null) _motionBlur.active = motionBlur;
            if (_dof != null)        _dof.active        = dof;

            if (globalPostProcessVolume != null)
                globalPostProcessVolume.enabled = bloom || motionBlur || dof;
        }

        private void SetBloomIntensity(float intensity)
        {
            if (_bloom == null) return;
            _bloom.intensity.Override(intensity);
        }

        private static void SetLightmapMode(LightmapsMode mode)
        {
            LightmapSettings.lightmapsMode = mode;
        }
    }
}
