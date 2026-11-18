using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

namespace HeavyVehiclesWorld.Controls
{
    /// <summary>
    /// Singleton wrapper around Unity's new Input System.
    /// Exposes all axes and buttons used by HeavyVehiclesWorld.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        public static InputManager Instance { get; private set; }

        // ── Generated InputActions asset ──────────────────────────────────
        // Replace "HeavyVehiclesInputActions" with the actual generated class name
        // once the Input Actions asset is created in the Unity Editor.
        private HeavyVehiclesInputActions _actions;

        // ── Mobile Inputs ────────────────────────────────────────────────
        public float MobileThrottle { get; set; }
        public float MobileBrake    { get; set; }
        public float MobileSteer    { get; set; }
        public float MobileHandbrake{ get; set; }
        public Vector2 MobileLeftJoystick { get; set; }
        public Vector2 MobileRightJoystick { get; set; }

        // ── Axes ──────────────────────────────────────────────────────────
        public float MoveX    { get; private set; }
        public float MoveY    { get; private set; }
        public float Steer    { get; private set; }
        public float Throttle { get; private set; }
        public float Brake    { get; private set; }
        public float Handbrake{ get; private set; }
        public float CameraX  { get; private set; }
        public float CameraY  { get; private set; }

        // ── Touch joysticks ───────────────────────────────────────────────
        public Vector2 LeftJoystick  { get; private set; }   // Player / vehicle movement
        public Vector2 RightJoystick { get; private set; }   // JCB / Excavator arm controls

        // ── Buttons (true the frame pressed) ─────────────────────────────
        public bool EnterExitPressed    { get; private set; }
        public bool ToggleEnginePressed { get; private set; }
        public bool HeadlightsPressed   { get; private set; }
        public bool HornPressed         { get; private set; }
        public bool GearUpPressed       { get; private set; }
        public bool GearDownPressed     { get; private set; }
        public bool PausePressed        { get; private set; }

        // ── Mobile on-screen sticks ───────────────────────────────────────
        [Header("Mobile – On-Screen Sticks")]
        [SerializeField] private OnScreenStick leftVirtualStick;
        [SerializeField] private OnScreenStick rightVirtualStick;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _actions = new HeavyVehiclesInputActions();
            _actions.Enable();
        }

        private void OnDestroy()
        {
            _actions?.Disable();
        }

        private void Update()
        {
            ReadAxes();
            ReadButtons();
        }

        // ── Private helpers ───────────────────────────────────────────────
        private void ReadAxes()
        {
            var player  = _actions.Player;
            var vehicle = _actions.Vehicle;

            Vector2 move = player.Move.ReadValue<Vector2>();
            MoveX = move.x;
            MoveY = move.y;

            Steer     = vehicle.Steer.ReadValue<float>();
            Throttle  = vehicle.Throttle.ReadValue<float>();
            Brake     = vehicle.Brake.ReadValue<float>();
            Handbrake = vehicle.Handbrake.ReadValue<float>();

            Vector2 look = player.Look.ReadValue<Vector2>();
            CameraX = look.x;
            CameraY = look.y;

            // Touch joysticks
            LeftJoystick  = player.Move.ReadValue<Vector2>();
            RightJoystick = vehicle.ArmControl.ReadValue<Vector2>();

            // Apply Mobile Overrides if active
            if (Mathf.Abs(MobileThrottle) > 0.01f || Mathf.Abs(MobileBrake) > 0.01f || Mathf.Abs(MobileSteer) > 0.01f || MobileHandbrake > 0.01f || MobileLeftJoystick.sqrMagnitude > 0.01f || MobileRightJoystick.sqrMagnitude > 0.01f)
            {
                if (Mathf.Abs(MobileThrottle) > 0.01f) Throttle = MobileThrottle;
                if (Mathf.Abs(MobileBrake) > 0.01f) Brake = MobileBrake;
                if (Mathf.Abs(MobileSteer) > 0.01f)
                {
                    Steer = MobileSteer;
                    MoveX = MobileSteer;
                }
                if (MobileHandbrake > 0.01f) Handbrake = MobileHandbrake;
                MoveY = Throttle - Brake;

                if (MobileLeftJoystick.sqrMagnitude > 0.01f) LeftJoystick = MobileLeftJoystick;
                else LeftJoystick = new Vector2(Steer, MoveY);

                if (MobileRightJoystick.sqrMagnitude > 0.01f) RightJoystick = MobileRightJoystick;
            }
        }

        private void ReadButtons()
        {
            var player  = _actions.Player;
            var vehicle = _actions.Vehicle;
            var ui      = _actions.UI;

            EnterExitPressed    = player.EnterExit.WasPressedThisFrame();
            ToggleEnginePressed = vehicle.ToggleEngine.WasPressedThisFrame();
            HeadlightsPressed   = vehicle.Headlights.WasPressedThisFrame();
            HornPressed         = vehicle.Horn.WasPressedThisFrame();
            GearUpPressed       = vehicle.GearUp.WasPressedThisFrame();
            GearDownPressed     = vehicle.GearDown.WasPressedThisFrame();
            PausePressed        = ui.Pause.WasPressedThisFrame();
        }
    }
}
