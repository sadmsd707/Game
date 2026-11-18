using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// PerformanceMonitor – in-game overlay showing FPS, memory, and draw calls.
    /// Toggle with the P key (or call Toggle() from code).
    /// Designed to stay on-screen during mobile QA testing.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private TextMeshProUGUI fpsLabel;
        [SerializeField] private TextMeshProUGUI memLabel;
        [SerializeField] private TextMeshProUGUI drawCallLabel;
        [SerializeField] private TextMeshProUGUI qualityLabel;
        [SerializeField] private Image           fpsBarFill;       // color-coded bar

        [Header("Settings")]
        [SerializeField] private float updateInterval  = 0.5f;      // seconds between refreshes
        [SerializeField] private bool  showOnStart     = false;
        [SerializeField] private KeyCode toggleKey     = KeyCode.P;

        // FPS tracking
        private int   _frameCount;
        private float _deltaAccum;
        private float _fps;

        // Peak tracking
        private float _peakFPS;
        private float _lowestFPS = 9999f;

        // Timing
        private float _timer;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            SetVisible(showOnStart);
        }

        private void Update()
        {
            // Keyboard toggle
            if (Input.GetKeyDown(toggleKey)) Toggle();

            if (panelGroup == null || panelGroup.alpha < 0.01f) return;

            // Accumulate frame data
            _deltaAccum += Time.unscaledDeltaTime;
            _frameCount++;

            _timer += Time.unscaledDeltaTime;
            if (_timer >= updateInterval)
            {
                _timer = 0f;
                _fps   = _frameCount / _deltaAccum;
                _deltaAccum = 0f;
                _frameCount = 0;

                _peakFPS   = Mathf.Max(_peakFPS,   _fps);
                _lowestFPS = Mathf.Min(_lowestFPS, _fps);

                RefreshDisplay();
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void Toggle()   => SetVisible(panelGroup == null || panelGroup.alpha < 0.5f);
        public void Show()     => SetVisible(true);
        public void HidePanel()=> SetVisible(false);

        // ── Refresh ────────────────────────────────────────────────────────────────

        private void RefreshDisplay()
        {
            // FPS label + colour
            if (fpsLabel != null)
            {
                string color = _fps >= 55f ? "#00FF88" : _fps >= 30f ? "#FFDD00" : "#FF4444";
                fpsLabel.text = $"<color={color}>{_fps:F0}</color> FPS  " +
                                $"<size=70%>(low: {_lowestFPS:F0}  peak: {_peakFPS:F0})</size>";
            }

            // FPS bar (green → yellow → red)
            if (fpsBarFill != null)
            {
                float t = Mathf.Clamp01(_fps / 60f);
                fpsBarFill.fillAmount = t;
                fpsBarFill.color      = Color.Lerp(Color.red, Color.green, t);
            }

            // Memory
            if (memLabel != null)
            {
                long totalMB = (long)(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576L);
                long resMB   = (long)(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()  / 1048576L);
                memLabel.text = $"MEM  {totalMB} MB used / {resMB} MB reserved";
            }

            // Quality level
            if (qualityLabel != null)
            {
                string q = Systems.GraphicsSettingsManager.Instance != null
                    ? Systems.GraphicsSettingsManager.Instance.CurrentQuality.ToString()
                    : QualitySettings.names[QualitySettings.GetQualityLevel()];
                qualityLabel.text = $"Quality: {q}  |  Target: {Application.targetFrameRate} FPS";
            }

            // Draw calls are not easily accessible at runtime in URP without profiler;
            // show a placeholder that devs can replace with UnityStats.drawCalls in Editor
            if (drawCallLabel != null)
            {
#if UNITY_EDITOR
                drawCallLabel.text = $"Draw calls: {UnityEditor.UnityStats.drawCalls}";
#else
                drawCallLabel.text = "Draw calls: (Editor only)";
#endif
            }
        }

        // ── Visibility ────────────────────────────────────────────────────────────

        private void SetVisible(bool on)
        {
            if (panelGroup == null) return;
            panelGroup.alpha          = on ? 1f : 0f;
            panelGroup.interactable   = on;
            panelGroup.blocksRaycasts = on;

            if (on)
            {
                _peakFPS   = 0f;
                _lowestFPS = 9999f;
            }
        }
    }
}
