// HeavyVehiclesInputActions.cs
// Hand-crafted wrapper – replaces Unity's auto-generated class so the project
// compiles without running the Input System code generator.
// Matches all action/map names referenced in InputManager.cs.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Wrapper around all input actions for HeavyVehiclesWorld.
/// Implements IInputActionCollection2 and IDisposable, matching the
/// interface contract that Unity's generated code normally provides.
/// </summary>
public class HeavyVehiclesInputActions : IInputActionCollection2, IDisposable
{
    // -------------------------------------------------------------------------
    // Asset & asset-level members (IInputActionCollection2 / IInputActionCollection)
    // -------------------------------------------------------------------------
    private InputActionAsset m_Asset;

    public InputBinding? bindingMask
    {
        get => m_Asset.bindingMask;
        set => m_Asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => m_Asset.devices;
        set => m_Asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => m_Asset.controlSchemes;

    public bool Contains(InputAction action) => m_Asset.Contains(action);

    public IEnumerator<InputAction> GetEnumerator() => m_Asset.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Enable()  => m_Asset.Enable();
    public void Disable() => m_Asset.Disable();

    public IEnumerable<InputBinding> bindings => m_Asset.bindings;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        => m_Asset.FindAction(actionNameOrId, throwIfNotFound);

    public int FindBinding(InputBinding bindingMask, out InputAction action)
        => m_Asset.FindBinding(bindingMask, out action);

    // -------------------------------------------------------------------------
    // Action-map wrappers
    // -------------------------------------------------------------------------
    private PlayerActions  m_Player;
    private VehicleActions m_Vehicle;
    private UIActions      m_UI;

    public PlayerActions  Player  => m_Player;
    public VehicleActions Vehicle => m_Vehicle;
    public UIActions      UI      => m_UI;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------
    public HeavyVehiclesInputActions()
    {
        // Build the asset programmatically so this class works without the
        // .inputactions file being imported (useful for unit tests / CI).
        m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
        m_Asset.name = "HeavyVehiclesInputActions";

        m_Player  = new PlayerActions(m_Asset);
        m_Vehicle = new VehicleActions(m_Asset);
        m_UI      = new UIActions(m_Asset);
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------
    public void Dispose()
    {
        UnityEngine.Object.Destroy(m_Asset);
    }

    // =========================================================================
    // PlayerActions
    // =========================================================================
    public class PlayerActions
    {
        private InputActionMap m_Map;

        public InputAction Move      { get; private set; }
        public InputAction Look      { get; private set; }
        public InputAction EnterExit { get; private set; }
        public InputAction Sprint    { get; private set; }

        internal PlayerActions(InputActionAsset asset)
        {
            m_Map = asset.AddActionMap("Player");

            // --- Move (Vector2 / WASD + left gamepad stick) ---
            Move = m_Map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w",          "Keyboard&Mouse")
                .With("Down",  "<Keyboard>/s",          "Keyboard&Mouse")
                .With("Left",  "<Keyboard>/a",          "Keyboard&Mouse")
                .With("Right", "<Keyboard>/d",          "Keyboard&Mouse");
            Move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");

            // --- Look (mouse delta + touch delta) ---
            Look = m_Map.AddAction("Look", InputActionType.Value);
            Look.AddBinding("<Mouse>/delta",
                            processors: "ScaleVector2(x=0.1,y=0.1)",
                            groups: "Keyboard&Mouse");
            Look.AddBinding("<Touchscreen>/primaryTouch/delta",
                            processors: "ScaleVector2(x=0.1,y=0.1)",
                            groups: "Touch");

            // --- EnterExit (E / gamepad south) ---
            EnterExit = m_Map.AddAction("EnterExit", InputActionType.Button);
            EnterExit.AddBinding("<Keyboard>/e",          groups: "Keyboard&Mouse");
            EnterExit.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");

            // --- Sprint (LeftShift / left stick press) ---
            Sprint = m_Map.AddAction("Sprint", InputActionType.Button);
            Sprint.AddBinding("<Keyboard>/leftShift",     groups: "Keyboard&Mouse");
            Sprint.AddBinding("<Gamepad>/leftStickPress", groups: "Gamepad");
        }

        public void Enable()  => m_Map.Enable();
        public void Disable() => m_Map.Disable();

        public InputActionMap Get() => m_Map;
        public static implicit operator InputActionMap(PlayerActions actions) => actions.m_Map;
    }

    // =========================================================================
    // VehicleActions
    // =========================================================================
    public class VehicleActions
    {
        private InputActionMap m_Map;

        public InputAction Throttle     { get; private set; }
        public InputAction Brake        { get; private set; }
        public InputAction Steer        { get; private set; }
        public InputAction Handbrake    { get; private set; }
        public InputAction ToggleEngine { get; private set; }
        public InputAction Headlights   { get; private set; }
        public InputAction Horn         { get; private set; }
        public InputAction GearUp       { get; private set; }
        public InputAction GearDown     { get; private set; }
        public InputAction ArmControl   { get; private set; }
        public InputAction CameraSwitch { get; private set; }

        internal VehicleActions(InputActionAsset asset)
        {
            m_Map = asset.AddActionMap("Vehicle");

            // --- Throttle (W / right trigger) ---
            Throttle = m_Map.AddAction("Throttle", InputActionType.Value);
            Throttle.AddBinding("<Keyboard>/w",           groups: "Keyboard&Mouse");
            Throttle.AddBinding("<Gamepad>/rightTrigger", groups: "Gamepad");

            // --- Brake (S / left trigger) ---
            Brake = m_Map.AddAction("Brake", InputActionType.Value);
            Brake.AddBinding("<Keyboard>/s",          groups: "Keyboard&Mouse");
            Brake.AddBinding("<Gamepad>/leftTrigger", groups: "Gamepad");

            // --- Steer (A/D 1D axis + left stick X) ---
            Steer = m_Map.AddAction("Steer", InputActionType.Value);
            Steer.AddCompositeBinding("1DAxis")
                 .With("Negative", "<Keyboard>/a", "Keyboard&Mouse")
                 .With("Positive", "<Keyboard>/d", "Keyboard&Mouse");
            Steer.AddBinding("<Gamepad>/leftStick/x", groups: "Gamepad");

            // --- Handbrake (Space) ---
            Handbrake = m_Map.AddAction("Handbrake", InputActionType.Button);
            Handbrake.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");

            // --- ToggleEngine (F) ---
            ToggleEngine = m_Map.AddAction("ToggleEngine", InputActionType.Button);
            ToggleEngine.AddBinding("<Keyboard>/f", groups: "Keyboard&Mouse");

            // --- Headlights (L) ---
            Headlights = m_Map.AddAction("Headlights", InputActionType.Button);
            Headlights.AddBinding("<Keyboard>/l", groups: "Keyboard&Mouse");

            // --- Horn (H) ---
            Horn = m_Map.AddAction("Horn", InputActionType.Button);
            Horn.AddBinding("<Keyboard>/h", groups: "Keyboard&Mouse");

            // --- GearUp (PageUp) ---
            GearUp = m_Map.AddAction("GearUp", InputActionType.Button);
            GearUp.AddBinding("<Keyboard>/pageUp", groups: "Keyboard&Mouse");

            // --- GearDown (PageDown) ---
            GearDown = m_Map.AddAction("GearDown", InputActionType.Button);
            GearDown.AddBinding("<Keyboard>/pageDown", groups: "Keyboard&Mouse");

            // --- ArmControl (right gamepad stick) ---
            ArmControl = m_Map.AddAction("ArmControl", InputActionType.Value);
            ArmControl.AddBinding("<Gamepad>/rightStick", groups: "Gamepad");

            // --- CameraSwitch (C / right stick press) ---
            CameraSwitch = m_Map.AddAction("CameraSwitch", InputActionType.Button);
            CameraSwitch.AddBinding("<Keyboard>/c",              groups: "Keyboard&Mouse");
            CameraSwitch.AddBinding("<Gamepad>/rightStickPress", groups: "Gamepad");
        }

        public void Enable()  => m_Map.Enable();
        public void Disable() => m_Map.Disable();

        public InputActionMap Get() => m_Map;
        public static implicit operator InputActionMap(VehicleActions actions) => actions.m_Map;
    }

    // =========================================================================
    // UIActions
    // =========================================================================
    public class UIActions
    {
        private InputActionMap m_Map;

        public InputAction Pause { get; private set; }
        public InputAction Map   { get; private set; }

        internal UIActions(InputActionAsset asset)
        {
            m_Map = asset.AddActionMap("UI");

            // --- Pause (Escape / gamepad start) ---
            Pause = m_Map.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape", groups: "Keyboard&Mouse");
            Pause.AddBinding("<Gamepad>/start",   groups: "Gamepad");

            // --- Map (M) ---
            Map = m_Map.AddAction("Map", InputActionType.Button);
            Map.AddBinding("<Keyboard>/m", groups: "Keyboard&Mouse");
        }

        public void Enable()  => m_Map.Enable();
        public void Disable() => m_Map.Disable();

        public InputActionMap Get() => m_Map;
        public static implicit operator InputActionMap(UIActions actions) => actions.m_Map;
    }
}
