// MobileTouchControls.cs
// Runtime mobile touch-input handler for HeavyVehiclesWorld.
//
// Provides two virtual joystick zones read directly from Unity's Input.touches
// (compatible with both the legacy and the new Input System in "Both" mode):
//   • Left half of screen  → move joystick  (GetLeftJoystick)
//   • Right half of screen → look / steer joystick (GetRightJoystick)
//
// Also exposes:
//   • IsTapping       – single-tap event (enter / exit vehicle)
//   • IsDoubleTap     – double-tap event (camera switch)

using System.Collections.Generic;
using UnityEngine;

public class MobileTouchControls : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector tunables
    // -------------------------------------------------------------------------
    [Header("Joystick Settings")]
    [Tooltip("Radius (pixels) a touch must move before the joystick activates.")]
    [SerializeField] private float joystickRadius = 150f;

    [Tooltip("Values below this magnitude are treated as zero (dead zone, 0–1).")]
    [SerializeField] private float deadZone = 0.1f;

    [Header("Tap Settings")]
    [Tooltip("Maximum time (s) between tap start and release to count as a tap.")]
    [SerializeField] private float tapMaxDuration = 0.25f;

    [Tooltip("Maximum time (s) between two taps to count as a double-tap.")]
    [SerializeField] private float doubleTapWindow = 0.4f;

    [Tooltip("Maximum pixels a finger may drift and still register as a tap.")]
    [SerializeField] private float tapMaxDrift = 20f;

    // -------------------------------------------------------------------------
    // Public state (read by InputManager every frame)
    // -------------------------------------------------------------------------
    /// <summary>Normalised direction from the left-screen joystick (-1..1 on each axis).</summary>
    public Vector2 LeftJoystick  { get; private set; }

    /// <summary>Normalised direction from the right-screen joystick (-1..1 on each axis).</summary>
    public Vector2 RightJoystick { get; private set; }

    /// <summary>True for exactly one frame when a single tap is recognised.</summary>
    public bool IsTapping    { get; private set; }

    /// <summary>True for exactly one frame when a double-tap is recognised.</summary>
    public bool IsDoubleTap  { get; private set; }

    // -------------------------------------------------------------------------
    // Internal tracking
    // -------------------------------------------------------------------------

    // Key = fingerId, Value = screen position where that finger first touched
    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();

    // Per-joystick zone data
    private struct JoystickState
    {
        public bool    active;
        public int     fingerId;
        public Vector2 origin;   // position where the controlling finger started
    }

    private JoystickState m_Left;
    private JoystickState m_Right;

    // Tap detection
    private float m_LastTapTime   = -999f;
    private bool  m_PendingTap    = false;      // tap started, waiting for release
    private int   m_TapFingerId   = -1;
    private float m_TapStartTime;
    private Vector2 m_TapStartPos;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Update()
    {
        // Reset one-frame flags
        IsTapping   = false;
        IsDoubleTap = false;

        ProcessTouches();

        // Compute joystick vectors from active finger positions
        LeftJoystick  = ComputeJoystickVector(m_Left);
        RightJoystick = ComputeJoystickVector(m_Right);
    }

    // -------------------------------------------------------------------------
    // Core touch processing
    // -------------------------------------------------------------------------

    private void ProcessTouches()
    {
        Touch[] touches = Input.touches;

        foreach (Touch touch in touches)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnTouchBegan(touch);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    OnTouchMoved(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnTouchEnded(touch);
                    break;
            }
        }

        // If a joystick finger is no longer in the touches array, release it.
        ReleaseOrphanedJoysticks(touches);
    }

    private void OnTouchBegan(Touch touch)
    {
        activeTouches[touch.fingerId] = touch.position;

        bool isLeft = IsLeftSide(touch.position);

        // Assign to joystick zone if the zone is free
        if (isLeft && !m_Left.active)
        {
            m_Left.active   = true;
            m_Left.fingerId = touch.fingerId;
            m_Left.origin   = touch.position;
        }
        else if (!isLeft && !m_Right.active)
        {
            m_Right.active   = true;
            m_Right.fingerId = touch.fingerId;
            m_Right.origin   = touch.position;
        }

        // Start tap tracking for this finger (only on right side, or either—your choice)
        if (!m_PendingTap)
        {
            m_PendingTap  = true;
            m_TapFingerId = touch.fingerId;
            m_TapStartTime = Time.unscaledTime;
            m_TapStartPos  = touch.position;
        }
    }

    private void OnTouchMoved(Touch touch)
    {
        if (activeTouches.ContainsKey(touch.fingerId))
            activeTouches[touch.fingerId] = touch.position;

        // Cancel tap if the finger drifted too far
        if (m_PendingTap && touch.fingerId == m_TapFingerId)
        {
            if (Vector2.Distance(touch.position, m_TapStartPos) > tapMaxDrift)
                m_PendingTap = false;
        }
    }

    private void OnTouchEnded(Touch touch)
    {
        activeTouches.Remove(touch.fingerId);

        // Release joystick zones
        if (m_Left.active  && m_Left.fingerId  == touch.fingerId)
        {
            m_Left.active = false;
            m_Left.fingerId = -1;
        }
        if (m_Right.active && m_Right.fingerId == touch.fingerId)
        {
            m_Right.active = false;
            m_Right.fingerId = -1;
        }

        // Evaluate tap
        if (m_PendingTap && touch.fingerId == m_TapFingerId)
        {
            m_PendingTap = false;
            float duration = Time.unscaledTime - m_TapStartTime;

            if (duration <= tapMaxDuration)
            {
                float timeSinceLast = Time.unscaledTime - m_LastTapTime;
                if (timeSinceLast <= doubleTapWindow)
                {
                    IsDoubleTap  = true;
                    m_LastTapTime = -999f; // reset so triple-tap doesn't double-fire
                }
                else
                {
                    IsTapping    = true;
                    m_LastTapTime = Time.unscaledTime;
                }
            }

            m_TapFingerId = -1;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// If a joystick's controlling finger has disappeared from the touches array,
    /// release that joystick.  Handles edge cases where Ended events are missed.
    /// </summary>
    private void ReleaseOrphanedJoysticks(Touch[] touches)
    {
        if (m_Left.active  && !FingerIsActive(m_Left.fingerId,  touches))
        {
            m_Left.active   = false;
            m_Left.fingerId = -1;
        }
        if (m_Right.active && !FingerIsActive(m_Right.fingerId, touches))
        {
            m_Right.active   = false;
            m_Right.fingerId = -1;
        }
    }

    private static bool FingerIsActive(int fingerId, Touch[] touches)
    {
        foreach (var t in touches)
            if (t.fingerId == fingerId &&
                t.phase != TouchPhase.Ended &&
                t.phase != TouchPhase.Canceled)
                return true;
        return false;
    }

    private Vector2 ComputeJoystickVector(JoystickState state)
    {
        if (!state.active)
            return Vector2.zero;

        if (!activeTouches.TryGetValue(state.fingerId, out Vector2 current))
            return Vector2.zero;

        Vector2 delta = current - state.origin;
        Vector2 norm  = delta / joystickRadius;         // scale to radius

        // Clamp to unit circle
        if (norm.magnitude > 1f)
            norm = norm.normalized;

        // Apply dead zone
        if (norm.magnitude < deadZone)
            return Vector2.zero;

        // Remap so the dead zone edge maps to 0
        float remapped = (norm.magnitude - deadZone) / (1f - deadZone);
        return norm.normalized * remapped;
    }

    private static bool IsLeftSide(Vector2 screenPos)
        => screenPos.x < Screen.width * 0.5f;

    // -------------------------------------------------------------------------
    // Public convenience API (mirrors the naming expected by InputManager)
    // -------------------------------------------------------------------------

    /// <summary>Returns the left (move) joystick vector in the range [-1,1] per axis.</summary>
    public Vector2 GetLeftJoystick()  => LeftJoystick;

    /// <summary>Returns the right (look/steer) joystick vector in the range [-1,1] per axis.</summary>
    public Vector2 GetRightJoystick() => RightJoystick;

    /// <summary>Returns the full active-touch dictionary (fingerId → current screen position).</summary>
    public IReadOnlyDictionary<int, Vector2> GetActiveTouches()
        => activeTouches;

    // -------------------------------------------------------------------------
    // Gizmos – draw joystick zones in the Game view (Editor only)
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUI.color = new Color(1f, 1f, 1f, 0.15f);

        // Left zone
        GUI.Box(new Rect(0, 0, Screen.width * 0.5f, Screen.height),
                "MOVE");

        // Right zone
        GUI.Box(new Rect(Screen.width * 0.5f, 0, Screen.width * 0.5f, Screen.height),
                "LOOK / STEER");

        GUI.color = Color.white;

        // Joystick debug values
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, normal = { textColor = Color.yellow } };
        GUI.Label(new Rect(20, Screen.height - 80, 400, 50),
                  $"L: {LeftJoystick:F2}  R: {RightJoystick:F2}", style);

        if (IsTapping)
            GUI.Label(new Rect(20, Screen.height - 130, 300, 50), "TAP", style);
        if (IsDoubleTap)
            GUI.Label(new Rect(20, Screen.height - 180, 300, 50), "DOUBLE-TAP", style);
    }
#endif
}
