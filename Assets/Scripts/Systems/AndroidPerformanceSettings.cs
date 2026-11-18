using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// AndroidPerformanceSettings – applies Android-specific runtime optimizations
    /// on Awake. Attach to any persistent GameObject (e.g. SceneBootstrapper root).
    ///
    /// Settings applied:
    ///   - Target frame rate from GraphicsSettingsManager (60 or 30)
    ///   - Screen.sleepTimeout = NeverSleep (prevents screen-off during gameplay)
    ///   - Physics.defaultSolverIterations tuned for mobile
    ///   - Application.backgroundLoadingPriority = Low (don't stall main thread)
    ///   - Screen brightness kept at system default
    ///   - AudioSettings buffer size tuned for low latency
    /// </summary>
    public class AndroidPerformanceSettings : MonoBehaviour
    {
        [Header("Frame Rate")]
        [SerializeField] private int targetFPS       = 60;
        [SerializeField] private bool useVSync       = false;   // VSync OFF for mobile — use targetFrameRate

        [Header("Physics (Mobile Tuning)")]
        [SerializeField] private int solverIterations         = 6;    // default 6, reduce to 4 for low-end
        [SerializeField] private int solverVelocityIterations = 1;    // default 1
        [SerializeField] private float fixedTimestep          = 0.02f; // 50 Hz, increase to 0.033 for low-end

        [Header("Loading")]
        [SerializeField] private ThreadPriority bgLoadPriority = ThreadPriority.Low;

        [Header("Audio")]
        [Tooltip("DSP buffer size: Best Performance = smallest latency, may cause crackle on low-end.")]
        [SerializeField] private AudioSpeakerMode speakerMode  = AudioSpeakerMode.Mono;  // Mono saves CPU on mobile

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            ApplyAll();
        }

        // ── Application ────────────────────────────────────────────────────────────

        private void ApplyAll()
        {
#if UNITY_ANDROID
            // Frame rate
            QualitySettings.vSyncCount = useVSync ? 1 : 0;
            Application.targetFrameRate = targetFPS;

            // Prevent screen sleep during gameplay
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Background loading thread priority
            Application.backgroundLoadingPriority = bgLoadPriority;

            // Physics
            Physics.defaultSolverIterations         = solverIterations;
            Physics.defaultSolverVelocityIterations = solverVelocityIterations;
            Time.fixedDeltaTime                     = fixedTimestep;

            // Audio
            AudioSettings.speakerMode = speakerMode;

            // Texture streaming (reduce VRAM pressure)
            QualitySettings.streamingMipmapsActive       = true;
            QualitySettings.streamingMipmapsMemoryBudget = 256f; // MB

            // GPU skinning (better on mobile)
            QualitySettings.skinWeights = SkinWeights.TwoBones; // 2 bones sufficient for vehicles

            Debug.Log($"[AndroidPerformanceSettings] Applied: {targetFPS} FPS, " +
                      $"solver={solverIterations}, fixedDT={fixedTimestep:F3}, " +
                      $"streamingMipmaps=ON(256MB)");
#else
            Debug.Log("[AndroidPerformanceSettings] Skipped – not Android platform.");
#endif
        }

        /// <summary>
        /// Call at runtime to switch between performance presets based on detected device tier.
        /// Tier 0 = low-end (Snapdragon 665 class)
        /// Tier 1 = mid-range (Snapdragon 720G class) [default]
        /// Tier 2 = flagship (Snapdragon 8 Gen 1+)
        /// </summary>
        public void ApplyDeviceTier(int tier)
        {
            switch (tier)
            {
                case 0: // Low-end
                    Application.targetFrameRate = 30;
                    Physics.defaultSolverIterations = 4;
                    Time.fixedDeltaTime = 0.033f;
                    QualitySettings.streamingMipmapsMemoryBudget = 128f;
                    if (GraphicsSettingsManager.Instance != null)
                        GraphicsSettingsManager.Instance.ApplyQuality(QualityLevel.Low);
                    Debug.Log("[AndroidPerformanceSettings] Tier 0 (low-end) applied.");
                    break;

                case 1: // Mid-range (default)
                    Application.targetFrameRate = 60;
                    Physics.defaultSolverIterations = 6;
                    Time.fixedDeltaTime = 0.02f;
                    QualitySettings.streamingMipmapsMemoryBudget = 256f;
                    if (GraphicsSettingsManager.Instance != null)
                        GraphicsSettingsManager.Instance.ApplyQuality(QualityLevel.Medium);
                    Debug.Log("[AndroidPerformanceSettings] Tier 1 (mid-range) applied.");
                    break;

                case 2: // Flagship
                    Application.targetFrameRate = 60;
                    Physics.defaultSolverIterations = 8;
                    Time.fixedDeltaTime = 0.0167f; // 60 Hz physics
                    QualitySettings.streamingMipmapsMemoryBudget = 512f;
                    if (GraphicsSettingsManager.Instance != null)
                        GraphicsSettingsManager.Instance.ApplyQuality(QualityLevel.High);
                    Debug.Log("[AndroidPerformanceSettings] Tier 2 (flagship) applied.");
                    break;
            }
        }

        /// <summary>Auto-detects device tier based on SystemInfo.</summary>
        public int DetectDeviceTier()
        {
            int memMB = SystemInfo.systemMemorySize;
            int gpuMem = SystemInfo.graphicsMemorySize;

            if (memMB >= 8000 && gpuMem >= 4000) return 2; // flagship
            if (memMB >= 4000 && gpuMem >= 2000) return 1; // mid-range
            return 0;                                        // low-end
        }

        /// <summary>Call once at game start to auto-apply the right tier.</summary>
        public void AutoApplyDeviceTier()
        {
            int tier = DetectDeviceTier();
            Debug.Log($"[AndroidPerformanceSettings] Detected device tier: {tier} " +
                      $"(RAM: {SystemInfo.systemMemorySize}MB, VRAM: {SystemInfo.graphicsMemorySize}MB)");
            ApplyDeviceTier(tier);
        }
    }
}
