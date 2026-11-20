using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Debug "photo camera" that orbits the active vehicle at a configurable
    /// azimuth / elevation / radius.  Defaults to a 3/4 front view so model
    /// quality can be inspected without loading a separate scene.
    ///
    /// Activated / deactivated by the PHOTO button in the HUD top bar.
    /// Single-finger drag (or mouse drag in the Editor) to orbit.
    /// When active the main FollowCamera component is disabled so only one
    /// camera renders.
    ///
    /// Added in Step 0 (Change #44).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HVW_PhotoCamera : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static HVW_PhotoCamera Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Orbit")]
        public float orbitRadius    = 6f;
        public float orbitAzimuth   = 45f;   // 0 = directly behind; 45 = 3/4 front-right
        public float orbitElevation = 25f;   // degrees above horizon
        public float orbitSpeed     = 90f;   // degrees per 100 px swipe

        [Header("Look offset")]
        public float lookHeightOffset = 1f;  // how high above vehicle pivot to look at

        // ── State ─────────────────────────────────────────────────────────────
        private Camera    _cam;
        private Transform _target;
        private bool      _isActive;

        private Vector2 _lastPointerPos;
        private bool    _pointerDragging;

        // ── Public API ────────────────────────────────────────────────────────
        public bool IsActive => _isActive;

        /// <summary>Set orbit azimuth in degrees (0=behind, 45=front-right, 90=side, 180=front).</summary>
        public void SetAzimuth(float degrees) { orbitAzimuth = degrees; ApplyOrbit(); }

        public void Activate(Transform target)
        {
            _target   = target;
            _isActive = true;
            _cam.enabled = true;
            ApplyOrbit();
        }

        public void Deactivate()
        {
            _isActive    = false;
            _cam.enabled = false;
            _target      = null;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _cam         = GetComponent<Camera>();
            _cam.enabled = false;

            var mainCam = Camera.main;
            if (mainCam != null && mainCam != _cam)
            {
                _cam.clearFlags      = mainCam.clearFlags;
                _cam.fieldOfView     = mainCam.fieldOfView;
                _cam.nearClipPlane   = mainCam.nearClipPlane;
                _cam.farClipPlane    = mainCam.farClipPlane;
                _cam.backgroundColor = mainCam.backgroundColor;
            }
            else
            {
                _cam.clearFlags    = CameraClearFlags.Skybox;
                _cam.fieldOfView   = 55f;
                _cam.nearClipPlane = 0.3f;
                _cam.farClipPlane  = 1000f;
            }
        }

        private void LateUpdate()
        {
            if (!_isActive || _target == null) return;
            HandleInput();
            ApplyOrbit();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void HandleInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0))
            {
                _lastPointerPos  = Input.mousePosition;
                _pointerDragging = true;
            }
            if (Input.GetMouseButtonUp(0)) _pointerDragging = false;

            if (_pointerDragging)
            {
                Vector2 current = Input.mousePosition;
                Vector2 delta   = current - _lastPointerPos;
                _lastPointerPos = current;
                ApplyDelta(delta);
            }
#else
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _lastPointerPos  = t.position;
                        _pointerDragging = true;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        _pointerDragging = false;
                        break;
                    case TouchPhase.Moved:
                        if (_pointerDragging)
                        {
                            Vector2 delta = t.position - _lastPointerPos;
                            _lastPointerPos = t.position;
                            ApplyDelta(delta);
                        }
                        break;
                }
            }
            else
            {
                _pointerDragging = false;
            }
#endif
        }

        private void ApplyDelta(Vector2 screenDelta)
        {
            float normX = screenDelta.x / Screen.height;
            float normY = screenDelta.y / Screen.height;
            orbitAzimuth   += normX * orbitSpeed;
            orbitElevation -= normY * orbitSpeed;
            orbitElevation  = Mathf.Clamp(orbitElevation, 5f, 80f);
        }

        // ── Orbit math ────────────────────────────────────────────────────────
        private void ApplyOrbit()
        {
            if (_target == null) return;
            float azRad = orbitAzimuth   * Mathf.Deg2Rad;
            float elRad = orbitElevation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(azRad) * Mathf.Cos(elRad),
                Mathf.Sin(elRad),
                Mathf.Cos(azRad) * Mathf.Cos(elRad)) * orbitRadius;
            Vector3 lookAt     = _target.position + Vector3.up * lookHeightOffset;
            transform.position = lookAt + offset;
            transform.LookAt(lookAt);
        }
    }
}
