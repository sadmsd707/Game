using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.World
{
    /// <summary>Minimap and optional full-screen map controller.</summary>
    public class MinimapController : MonoBehaviour
    {
        // ── Camera / RenderTexture ─────────────────────────────────────────────
        [Header("Camera & RT")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RenderTexture minimapRT;

        // ── UI ────────────────────────────────────────────────────────────────
        [Header("UI")]
        [SerializeField] private RawImage minimapUI;        // corner minimap
        [SerializeField] private RawImage fullMapUI;        // full-screen map panel
        [SerializeField] private RectTransform minimapContainer;
        [SerializeField] private RectTransform fullMapContainer;

        // ── Markers ───────────────────────────────────────────────────────────
        [Header("Markers")]
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private RectTransform markerRoot; // parent for all markers
        [SerializeField] private Sprite defaultVehicleIcon;

        // ── Zoom ──────────────────────────────────────────────────────────────
        [Header("Zoom")]
        [SerializeField] private float zoomLevel = 100f;        // orthographic size
        [SerializeField] private float minZoom    = 30f;
        [SerializeField] private float maxZoom    = 300f;
        [SerializeField] private float zoomSpeed  = 20f;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isFullMap;
        private Transform _playerTransform;

        // Vehicle markers: vehicle → its RectTransform on the map
        private readonly Dictionary<VehicleController, RectTransform> vehicleMarkers
            = new Dictionary<VehicleController, RectTransform>();

        // ── World bounds (used for position mapping) ──────────────────────────
        [Header("World")]
        [SerializeField] private Vector2 worldSize = new Vector2(4000f, 4000f); // 4 km × 4 km
        [SerializeField] private Vector2 worldOrigin = new Vector2(-2000f, -2000f);

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Assign RT to both camera and UI
            if (minimapCamera != null && minimapRT != null)
                minimapCamera.targetTexture = minimapRT;

            if (minimapUI != null && minimapRT != null)
                minimapUI.texture = minimapRT;

            if (fullMapUI != null && minimapRT != null)
                fullMapUI.texture = minimapRT;

            // Start in mini mode
            if (fullMapContainer != null)
                fullMapContainer.gameObject.SetActive(false);

            if (minimapContainer != null)
                minimapContainer.gameObject.SetActive(true);
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                _playerTransform = playerGo.transform;
        }

        private void Update()
        {
            HandleZoomInput();
            FollowPlayer();
            UpdateMarkers();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Register a vehicle to show as a marker on the minimap.</summary>
        public void RegisterVehicleMarker(VehicleController vehicle, Sprite icon)
        {
            if (vehicle == null || vehicleMarkers.ContainsKey(vehicle)) return;

            GameObject markerGo = new GameObject(vehicle.name + "_Marker");
            markerGo.transform.SetParent(markerRoot, false);

            Image img = markerGo.AddComponent<Image>();
            img.sprite = icon != null ? icon : defaultVehicleIcon;
            img.SetNativeSize();

            RectTransform rt = markerGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(16f, 16f);

            vehicleMarkers[vehicle] = rt;
        }

        /// <summary>Unregister a vehicle marker (call on vehicle destroyed).</summary>
        public void UnregisterVehicleMarker(VehicleController vehicle)
        {
            if (!vehicleMarkers.TryGetValue(vehicle, out RectTransform rt)) return;
            if (rt != null) Destroy(rt.gameObject);
            vehicleMarkers.Remove(vehicle);
        }

        /// <summary>Update all marker positions and rotations to match the world.</summary>
        public void UpdateMarkers()
        {
            // Update vehicle markers
            foreach (var kvp in vehicleMarkers)
            {
                VehicleController vc = kvp.Key;
                RectTransform rt    = kvp.Value;

                if (vc == null || rt == null) continue;

                rt.anchoredPosition = WorldToMapPosition(vc.transform.position);
                rt.localEulerAngles = new Vector3(0f, 0f, -vc.transform.eulerAngles.y);
            }

            // Update player marker
            if (playerMarker != null && _playerTransform != null)
            {
                playerMarker.anchoredPosition = WorldToMapPosition(_playerTransform.position);
                playerMarker.localEulerAngles = new Vector3(0f, 0f, -_playerTransform.eulerAngles.y);
            }
        }

        /// <summary>Toggle between corner mini-map and full-screen map.</summary>
        public void ToggleFullMap()
        {
            _isFullMap = !_isFullMap;

            if (minimapContainer != null)
                minimapContainer.gameObject.SetActive(!_isFullMap);

            if (fullMapContainer != null)
                fullMapContainer.gameObject.SetActive(_isFullMap);

            // Adjust camera orthographic size for context
            if (minimapCamera != null)
            {
                minimapCamera.orthographicSize = _isFullMap
                    ? Mathf.Max(worldSize.x, worldSize.y) * 0.5f
                    : zoomLevel;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void FollowPlayer()
        {
            if (minimapCamera == null || _playerTransform == null || _isFullMap) return;
            Vector3 cam = minimapCamera.transform.position;
            cam.x = _playerTransform.position.x;
            cam.z = _playerTransform.position.z;
            minimapCamera.transform.position = cam;
            minimapCamera.orthographicSize   = zoomLevel;
        }

        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                zoomLevel = Mathf.Clamp(zoomLevel - scroll * zoomSpeed * 100f, minZoom, maxZoom);
            }

            // Pinch-to-zoom (mobile)
            if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                Vector2 prevT0 = t0.position - t0.deltaPosition;
                Vector2 prevT1 = t1.position - t1.deltaPosition;

                float prevDist = (prevT0 - prevT1).magnitude;
                float currDist = (t0.position - t1.position).magnitude;
                float delta    = prevDist - currDist;

                zoomLevel = Mathf.Clamp(zoomLevel + delta * zoomSpeed * Time.deltaTime, minZoom, maxZoom);
            }
        }

        /// <summary>Convert a world XZ position to a map UI anchoredPosition.</summary>
        private Vector2 WorldToMapPosition(Vector3 worldPos)
        {
            if (markerRoot == null) return Vector2.zero;

            Rect r = markerRoot.rect;
            float nx = (worldPos.x - worldOrigin.x) / worldSize.x;
            float ny = (worldPos.z - worldOrigin.y) / worldSize.y;

            return new Vector2(
                Mathf.Lerp(r.xMin, r.xMax, nx),
                Mathf.Lerp(r.yMin, r.yMax, ny)
            );
        }
    }
}
