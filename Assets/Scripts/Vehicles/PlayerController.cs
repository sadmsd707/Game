using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeavyVehiclesWorld.Vehicles
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // ── Movement ──────────────────────────────────────────────────────
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float runSpeed  = 10f;

        // ── Interaction ───────────────────────────────────────────────────
        [Header("Interaction")]
        public float interactRadius = 4f;
        public VehicleController nearestVehicle;

        // ── Camera ────────────────────────────────────────────────────────
        [Header("Camera")]
        [SerializeField] private Camera playerCamera;

        // ── Components ────────────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerInput        _playerInput;

        // ── Input state ───────────────────────────────────────────────────
        private Vector2 _moveInput;
        private bool    _isSprinting;
        private bool    _inVehicle;

        // ── Gravity ───────────────────────────────────────────────────────
        private float _verticalVelocity;
        private const float Gravity = -9.81f;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _cc          = GetComponent<CharacterController>();
            _playerInput = GetComponent<PlayerInput>();
        }

        private void Update()
        {
            if (_inVehicle) return;

            HandleMovement();
            FindNearestVehicle();

            // E / GamePad South – enter vehicle
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryEnterVehicle();
        }

        // ── Input callbacks (called by PlayerInput component) ─────────────
        public void OnMove(InputValue value)   => _moveInput   = value.Get<Vector2>();
        public void OnSprint(InputValue value) => _isSprinting = value.isPressed;

        // ── Movement ──────────────────────────────────────────────────────
        private void HandleMovement()
        {
            float speed = _isSprinting ? runSpeed : walkSpeed;
            Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;

            // Gravity
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += Gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _cc.Move(move * speed * Time.deltaTime);
        }

        // ── Vehicle detection ─────────────────────────────────────────────
        public void FindNearestVehicle()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius);
            float bestDist = float.MaxValue;
            nearestVehicle = null;

            foreach (var hit in hits)
            {
                var vc = hit.GetComponent<VehicleController>();
                if (vc == null) continue;
                float d = Vector3.Distance(transform.position, vc.transform.position);
                if (d < bestDist)
                {
                    bestDist       = d;
                    nearestVehicle = vc;
                }
            }
        }

        // ── Vehicle enter / exit ──────────────────────────────────────────
        public void TryEnterVehicle()
        {
            if (nearestVehicle == null) return;
            float dist = Vector3.Distance(transform.position, nearestVehicle.transform.position);
            if (dist <= nearestVehicle.EnterRadius)
            {
                nearestVehicle.EnterVehicle(this);
                SetPlayerActive(false);
                _inVehicle = true;
            }
        }

        public void TryExitVehicle()
        {
            if (!_inVehicle || nearestVehicle == null) return;
            nearestVehicle.ExitVehicle();
            SetPlayerActive(true);
            _inVehicle = false;
        }

        // ── Helper ────────────────────────────────────────────────────────
        private void SetPlayerActive(bool active)
        {
            _cc.enabled = active;
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(active);
        }

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
