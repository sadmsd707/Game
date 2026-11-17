using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Controls
{
    /// <summary>
    /// Manages the mobile touch HUD: joystick images, action buttons,
    /// vehicle-specific UI visibility, and touch-press highlight feedback.
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        // ── Joystick images ───────────────────────────────────────────────
        [Header("Joysticks")]
        [SerializeField] private RectTransform leftJoystickImage;
        [SerializeField] private RectTransform rightJoystickImage;

        // ── Buttons ───────────────────────────────────────────────────────
        [Header("Common Buttons")]
        [SerializeField] private Button accelerateButton;
        [SerializeField] private Button brakeButton;
        [SerializeField] private Button handbrakeButton;
        [SerializeField] private Button gearUpButton;
        [SerializeField] private Button gearDownButton;
        [SerializeField] private Button enterExitButton;
        [SerializeField] private Button headlightsButton;
        [SerializeField] private Button hornButton;
        [SerializeField] private Button pauseButton;

        // ── Vehicle-specific button groups ────────────────────────────────
        [Header("Vehicle-Specific Buttons")]
        [SerializeField] private GameObject jcbArmControls;
        [SerializeField] private GameObject excavatorArmControls;
        [SerializeField] private GameObject harvesterAttachmentControls;

        // ── Highlight settings ────────────────────────────────────────────
        [Header("Highlight")]
        [SerializeField] private Color pressColor   = new Color(1f, 1f, 0f, 0.6f);
        [SerializeField] private float pressDuration = 0.12f;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            RegisterFeedback(accelerateButton);
            RegisterFeedback(brakeButton);
            RegisterFeedback(handbrakeButton);
            RegisterFeedback(gearUpButton);
            RegisterFeedback(gearDownButton);
            RegisterFeedback(enterExitButton);
            RegisterFeedback(headlightsButton);
            RegisterFeedback(hornButton);
            RegisterFeedback(pauseButton);
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Show / hide buttons appropriate for the given vehicle type.</summary>
        public void ShowVehicleUI(VehicleType type)
        {
            HideAllVehicleUI();

            // All vehicles share these buttons
            SetActive(accelerateButton, true);
            SetActive(brakeButton,      true);
            SetActive(handbrakeButton,  true);
            SetActive(gearUpButton,     true);
            SetActive(gearDownButton,   true);
            SetActive(headlightsButton, true);
            SetActive(hornButton,       true);

            // Vehicle-specific extras
            switch (type)
            {
                case VehicleType.JCB:
                    if (jcbArmControls != null)        jcbArmControls.SetActive(true);
                    if (rightJoystickImage != null)    rightJoystickImage.gameObject.SetActive(true);
                    break;
                case VehicleType.Excavator:
                    if (excavatorArmControls != null)  excavatorArmControls.SetActive(true);
                    if (rightJoystickImage != null)    rightJoystickImage.gameObject.SetActive(true);
                    break;
                case VehicleType.Harvester:
                    if (harvesterAttachmentControls != null) harvesterAttachmentControls.SetActive(true);
                    break;
            }
        }

        /// <summary>Hide all vehicle-specific action buttons.</summary>
        public void HideAllVehicleUI()
        {
            SetActive(accelerateButton, false);
            SetActive(brakeButton,      false);
            SetActive(handbrakeButton,  false);
            SetActive(gearUpButton,     false);
            SetActive(gearDownButton,   false);
            SetActive(headlightsButton, false);
            SetActive(hornButton,       false);

            if (jcbArmControls != null)             jcbArmControls.SetActive(false);
            if (excavatorArmControls != null)        excavatorArmControls.SetActive(false);
            if (harvesterAttachmentControls != null) harvesterAttachmentControls.SetActive(false);
            if (rightJoystickImage != null)          rightJoystickImage.gameObject.SetActive(false);
        }

        // ── Private helpers ───────────────────────────────────────────────
        private void SetActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }

        /// <summary>Register a brief highlight coroutine on button press.</summary>
        private void RegisterFeedback(Button btn)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            Color original = img.color;
            btn.onClick.AddListener(() => StartCoroutine(HighlightRoutine(img, original)));
        }

        private IEnumerator HighlightRoutine(Image img, Color original)
        {
            img.color = pressColor;
            yield return new WaitForSecondsRealtime(pressDuration);
            img.color = original;
        }
    }
}
