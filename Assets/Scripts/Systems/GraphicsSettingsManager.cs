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
            EnsureURPAssetsLoaded();

            // Set Unity built-in quality
            int unityIdx = level switch
            {
                QualityLevel.Low    => 1,
                QualityLevel.Medium => 2,
                QualityLevel.High   => 3,
                QualityLevel.Ultra  => 5,
                _                   => 3
            };
            QualitySettings.SetQualityLevel(unityIdx, applyExpensiveChanges: true);

            // Assign the matching URP pipeline asset
            UniversalRenderPipelineAsset urpAsset = level switch
            {
                QualityLevel.Low    => urpLow,
                QualityLevel.Medium => urpMedium,
                QualityLevel.High   => urpHigh,
                QualityLevel.Ultra  => urpUltra,
                _                   => urpHigh
            };
            if (urpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
            }

            // Post-processing, anisotropic filtering & shadows per tier
            switch (level)
            {
                case QualityLevel.Low:
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.shadows          = UnityEngine.ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance   = 40f;
                    SetPostProcessing(bloom: false, motionBlur: false, dof: false);
                    SetLightmapMode(LightmapsMode.NonDirectional);
                    break;

                case QualityLevel.Medium:
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    QualitySettings.shadows          = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowDistance   = 60f;
                    SetPostProcessing(bloom: true, motionBlur: false, dof: false);
                    SetBloomIntensity(0.15f);
                    break;

                case QualityLevel.High:
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    QualitySettings.shadows          = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowDistance   = 100f;
                    SetPostProcessing(bloom: true, motionBlur: false, dof: false);
                    SetBloomIntensity(0.20f);
                    break;

                case QualityLevel.Ultra:
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    QualitySettings.shadows          = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowDistance   = 120f;
                    SetPostProcessing(bloom: true, motionBlur: false, dof: true);
                    SetBloomIntensity(0.25f);
                    break;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.86f);
            RenderSettings.fogDensity = 0.0015f;

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

        public static QualityLevel DetectBestQualityLevel()
        {
            int ramMB = SystemInfo.systemMemorySize;
            if (ramMB > 0)
            {
                if (ramMB < 3500) return QualityLevel.Low;
                if (ramMB < 5500) return QualityLevel.Medium;
                if (ramMB < 7500) return QualityLevel.High;
                return QualityLevel.Ultra;
            }
            return QualityLevel.High;
        }

        private void EnsureURPAssetsLoaded()
        {
            if (urpLow == null) urpLow = Resources.Load<UniversalRenderPipelineAsset>("Settings/URP_Quality_Low");
            if (urpMedium == null) urpMedium = Resources.Load<UniversalRenderPipelineAsset>("Settings/URP_Quality_Medium");
            if (urpHigh == null) urpHigh = Resources.Load<UniversalRenderPipelineAsset>("Settings/URP_Quality_High");
            if (urpUltra == null) urpUltra = Resources.Load<UniversalRenderPipelineAsset>("Settings/URP_Quality_Ultra");
        }

        public void LoadSettings()
        {
            EnsureURPAssetsLoaded();

            if (PlayerPrefs.HasKey("GFX_Quality_UserOverride") && PlayerPrefs.GetInt("GFX_Quality_UserOverride") == 1)
            {
                _currentQuality = (QualityLevel)PlayerPrefs.GetInt(KeyQuality, (int)QualityLevel.High);
            }
            else
            {
                _currentQuality = DetectBestQualityLevel();
                Debug.Log($"[GraphicsSettingsManager] Auto-detected quality tier: {_currentQuality} (RAM: {SystemInfo.systemMemorySize} MB, GPU: {SystemInfo.graphicsDeviceName})");
            }

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
