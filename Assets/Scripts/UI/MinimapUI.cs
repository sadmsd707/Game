using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// MinimapUI – corner minimap that expands to fullscreen map on button press.
    /// Requires a RenderTexture assigned to a RawImage component (minimap camera target).
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private RectTransform minimapPanel;      // corner minimap rect
        [SerializeField] private RectTransform fullMapPanel;      // fullscreen overlay

        [Header("Minimap Camera")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RawImage minimapImage;           // receives RenderTexture
        [SerializeField] private RawImage fullMapImage;           // same or separate RT

        [Header("Zoom")]
        [SerializeField] private float minZoom = 50f;
        [SerializeField] private float maxZoom = 500f;
        [SerializeField] private float zoomStep = 50f;
        private float _currentZoom = 150f;

        [Header("UI Labels")]
        [SerializeField] private TextMeshProUGUI zoneLabel;       // "FARMLAND", "QUARRY", etc.
        [SerializeField] private TextMeshProUGUI gameTimeLabel;   // "14:35"
        [SerializeField] private TextMeshProUGUI coordsLabel;     // optional X/Z coords

        [Header("Toggle Button")]
        [SerializeField] private Button toggleButton;             // corner toggle button

        [Header("Zoom Buttons")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;

        [Header("Player Marker")]
        [SerializeField] private RectTransform playerMarker;      // arrow icon on map

        // references set at runtime
        private ZoneManager _zoneManager;
        private DayNightCycle _dayNight;
        private Transform _playerTransform;

        private bool _isFullscreen = false;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            _zoneManager = FindObjectOfType<ZoneManager>();
            _dayNight    = FindObjectOfType<DayNightCycle>();

            // cache player (try tag first)
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;

            // hide fullscreen at start
            if (fullMapPanel != null) fullMapPanel.gameObject.SetActive(false);

            // wire buttons
            if (toggleButton  != null) toggleButton.onClick.AddListener(ToggleFullscreen);
            if (zoomInButton  != null) zoomInButton.onClick.AddListener(ZoomIn);
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);

            ApplyZoom();
        }

        private void Update()
        {
            UpdateZoneLabel();
            UpdateTimeLabel();
            UpdatePlayerMarker();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;

            if (fullMapPanel != null)
                fullMapPanel.gameObject.SetActive(_isFullscreen);

            // in full-screen mode hide the corner panel to avoid overlap
            if (minimapPanel != null)
                minimapPanel.gameObject.SetActive(!_isFullscreen);
        }

        public void ZoomIn()
        {
            _currentZoom = Mathf.Max(minZoom, _currentZoom - zoomStep);
            ApplyZoom();
        }

        public void ZoomOut()
        {
            _currentZoom = Mathf.Min(maxZoom, _currentZoom + zoomStep);
            ApplyZoom();
        }

        /// <summary>Call from external code to focus the minimap on a world position.</summary>
        public void FocusOn(Vector3 worldPos)
        {
            if (minimapCamera == null) return;
            var pos = minimapCamera.transform.position;
            pos.x = worldPos.x;
            pos.z = worldPos.z;
            minimapCamera.transform.position = pos;
        }

        /// <summary>Register a transform so the minimap camera follows it.</summary>
        public void SetTrackedTransform(Transform t)
        {
            _playerTransform = t;
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private void ApplyZoom()
        {
            if (minimapCamera != null && minimapCamera.orthographic)
                minimapCamera.orthographicSize = _currentZoom;
        }

        private void UpdateZoneLabel()
        {
            if (zoneLabel == null || _zoneManager == null) return;
            zoneLabel.text = _zoneManager.CurrentZone.ToString().ToUpper();
        }

        private void UpdateTimeLabel()
        {
            if (gameTimeLabel == null || _dayNight == null) return;
            float t  = _dayNight.TimeOfDay;           // 0-24
            int hours = Mathf.FloorToInt(t);
            int mins  = Mathf.FloorToInt((t - hours) * 60f);
            gameTimeLabel.text = $"{hours:D2}:{mins:D2}";
        }

        private void UpdatePlayerMarker()
        {
            if (playerMarker == null || minimapCamera == null || _playerTransform == null) return;

            // Convert world → viewport → local rect
            Vector3 vp = minimapCamera.WorldToViewportPoint(_playerTransform.position);
            if (vp.z < 0f) return;  // behind camera

            // Map viewport (0-1) to RawImage rect
            RectTransform rt = minimapImage != null ? minimapImage.rectTransform : null;
            if (rt == null) return;

            Vector2 rectSize = rt.rect.size;
            playerMarker.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * rectSize.x,
                (vp.y - 0.5f) * rectSize.y
            );

            // Rotate marker to match player heading
            float heading = _playerTransform.eulerAngles.y;
            playerMarker.localEulerAngles = new Vector3(0f, 0f, -heading);
        }
    }
}

