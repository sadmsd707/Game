using System.Collections;
using UnityEngine;
#if UNITY_CINEMACHINE
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HeavyVehiclesWorld.Vehicles
{
    public enum CameraMode
    {
        ThirdPerson,
        FirstPerson,
        FreeLook
    }

    /// <summary>
    /// Manages all camera modes for heavy-vehicle gameplay.
    /// Requires three CinemachineVirtualCamera references (one per mode).
    /// Call SetTarget() first, then SwitchCamera() to activate a mode.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        // ── Mode ──────────────────────────────────────────────────────────
        [Header("Camera Mode")]
        public CameraMode currentMode = CameraMode.ThirdPerson;

        // ── Cinemachine virtual cameras ───────────────────────────────────
        [Header("Cinemachine Virtual Cameras")]
        [SerializeField] private CinemachineVirtualCamera thirdPersonCamera;
        [SerializeField] private CinemachineVirtualCamera firstPersonCamera;
        [SerializeField] private CinemachineVirtualCamera freeLookCamera;

        // ── Smooth parameters ─────────────────────────────────────────────
        [Header("Smooth Settings")]
        [SerializeField] private float followDampTime  = 0.1f;
        [SerializeField] private float lookLerpSpeed   = 8f;

        // ── Third-person offset ───────────────────────────────────────────
        [Header("Third Person")]
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 3f, -8f);
        [SerializeField] private float   thirdPersonFOV    = 60f;

        // ── Zoom (pinch) ──────────────────────────────────────────────────
        [Header("Pinch-to-Zoom (Third Person)")]
        [SerializeField] private float minFOV        = 30f;
        [SerializeField] private float maxFOV        = 80f;
        [SerializeField] private float zoomSensitivity = 0.05f;

        // ── Free-look orbit ───────────────────────────────────────────────
        [Header("Free Look")]
        [SerializeField] private float freeLookRadius       = 10f;
        [SerializeField] private float freeLookSensitivity  = 0.3f;
        [SerializeField] private float freeLookPitchMin     = -20f;
        [SerializeField] private float freeLookPitchMax     = 60f;

        // ── Targets ───────────────────────────────────────────────────────
        private Transform _vehicleRoot;
        private Transform _driverSeat;
        private Transform _thirdPersonAnchor;

        // ── State ─────────────────────────────────────────────────────────
        private float   _currentFOV;
        private float   _freeLookYaw;
        private float   _freeLookPitch;
        private Vector3 _thirdPersonVelocity;

        // ── Touch ─────────────────────────────────────────────────────────
        private float _prevPinchDist;
        private bool  _isPinching;

        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _currentFOV = thirdPersonFOV;
        }

        private void Start()
        {
            SwitchCamera(currentMode);
        }

        private void LateUpdate()
        {
            if (_vehicleRoot == null) return;

            switch (currentMode)
            {
                case CameraMode.ThirdPerson: UpdateThirdPerson(); break;
                case CameraMode.FirstPerson: UpdateFirstPerson(); break;
                case CameraMode.FreeLook:    UpdateFreeLook();    break;
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Sets the transforms the camera tracks.</summary>
        public void SetTarget(Transform vehicleRoot, Transform driverSeat, Transform thirdPersonAnchor)
        {
            _vehicleRoot        = vehicleRoot;
            _driverSeat         = driverSeat;
            _thirdPersonAnchor  = thirdPersonAnchor;

            // Wire Cinemachine follow targets
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.Follow = vehicleRoot;
                thirdPersonCamera.LookAt = vehicleRoot;
            }
            if (firstPersonCamera != null)
            {
                firstPersonCamera.Follow = driverSeat;
                firstPersonCamera.LookAt = driverSeat;
            }
            if (freeLookCamera != null)
            {
                freeLookCamera.Follow = vehicleRoot;
                freeLookCamera.LookAt = vehicleRoot;
            }
        }

        /// <summary>Activates the requested camera mode.</summary>
        public void SwitchCamera(CameraMode mode)
        {
            currentMode = mode;

            if (thirdPersonCamera) thirdPersonCamera.Priority = (mode == CameraMode.ThirdPerson) ? 20 : 0;
            if (firstPersonCamera) firstPersonCamera.Priority = (mode == CameraMode.FirstPerson) ? 20 : 0;
            if (freeLookCamera)    freeLookCamera.Priority    = (mode == CameraMode.FreeLook)    ? 20 : 0;

            // Reset free-look angles when entering that mode
            if (mode == CameraMode.FreeLook && _vehicleRoot != null)
            {
                _freeLookYaw   = _vehicleRoot.eulerAngles.y;
                _freeLookPitch = 20f;
            }
        }

        // ── Third-person logic ────────────────────────────────────────────
        private void UpdateThirdPerson()
        {
            HandlePinchZoom();

            if (thirdPersonCamera == null) return;
            thirdPersonCamera.m_Lens.FieldOfView = Mathf.Lerp(
                thirdPersonCamera.m_Lens.FieldOfView, _currentFOV, Time.deltaTime * lookLerpSpeed);

            // Adjust body offset dynamically if Cinemachine body is Transposer
            var transposer = thirdPersonCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_FollowOffset = Vector3.SmoothDamp(
                    transposer.m_FollowOffset, thirdPersonOffset,
                    ref _thirdPersonVelocity, followDampTime);
            }
        }

        // ── First-person logic ────────────────────────────────────────────
        private void UpdateFirstPerson()
        {
            if (_driverSeat == null || firstPersonCamera == null) return;
            // Cinemachine follows driver seat transform — nothing extra needed.
            // Forward look is naturally aligned with vehicle.
        }

        // ── Free-look logic ───────────────────────────────────────────────
        private void UpdateFreeLook()
        {
            if (_vehicleRoot == null || freeLookCamera == null) return;

            // Touch drag
            HandleFreeLookTouchDrag();

            // Right-stick (gamepad)
            HandleFreeLookGamepad();

            // Clamp pitch
            _freeLookPitch = Mathf.Clamp(_freeLookPitch, freeLookPitchMin, freeLookPitchMax);

            // Convert spherical coords to position
            Quaternion rotation = Quaternion.Euler(_freeLookPitch, _freeLookYaw, 0f);
            Vector3 desiredPos  = _vehicleRoot.position + rotation * new Vector3(0f, 0f, -freeLookRadius);

            if (freeLookCamera.transform.parent == null)
            {
                freeLookCamera.transform.position = desiredPos;
                freeLookCamera.transform.LookAt(_vehicleRoot.position + Vector3.up * 1.5f);
            }
        }

        // ── Pinch-to-zoom ─────────────────────────────────────────────────
        private void HandlePinchZoom()
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System: handled via InputManager if available
            return;
#else
            if (Input.touchCount < 2)
            {
                _isPinching = false;
                return;
            }

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            float pinchDist = Vector2.Distance(t0.position, t1.position);

            if (!_isPinching)
            {
                _prevPinchDist = pinchDist;
                _isPinching    = true;
                return;
            }

            float delta = pinchDist - _prevPinchDist;
            _currentFOV  = Mathf.Clamp(_currentFOV - delta * zoomSensitivity, minFOV, maxFOV);
            _prevPinchDist = pinchDist;
#endif
        }

        // ── Touch drag for free-look ──────────────────────────────────────
        private void HandleFreeLookTouchDrag()
        {
#if !ENABLE_INPUT_SYSTEM
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    _freeLookYaw   += t.deltaPosition.x * freeLookSensitivity;
                    _freeLookPitch -= t.deltaPosition.y * freeLookSensitivity;
                }
            }
#endif
        }

        // ── Right-stick (gamepad) for free-look ───────────────────────────
        private void HandleFreeLookGamepad()
        {
#if ENABLE_INPUT_SYSTEM
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.rightStick.ReadValue();
                _freeLookYaw   += stick.x * freeLookSensitivity * 100f * Time.deltaTime;
                _freeLookPitch -= stick.y * freeLookSensitivity * 100f * Time.deltaTime;
            }
#else
            float h = Input.GetAxis("RightStickHorizontal");
            float v = Input.GetAxis("RightStickVertical");
            _freeLookYaw   += h * freeLookSensitivity * 100f * Time.deltaTime;
            _freeLookPitch -= v * freeLookSensitivity * 100f * Time.deltaTime;
#endif
        }
    }
}
