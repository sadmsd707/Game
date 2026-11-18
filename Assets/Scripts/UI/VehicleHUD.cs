using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// In-game HUD displayed while a VehicleController is active.
    /// Assign all references via the Inspector (Canvas children).
    /// Call <see cref="UpdateHUD"/> every frame from the active vehicle
    /// or from a GameManager that owns the camera/player reference.
    /// </summary>
    public class VehicleHUD : MonoBehaviour
    {
        // ── Text labels ───────────────────────────────────────────────────────
        [Header("Text Labels")]
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text fuelText;
        [SerializeField] private TMP_Text vehicleNameText;

        // ── Gauges ────────────────────────────────────────────────────────────
        [Header("Gauges")]
        [SerializeField] private Slider   fuelBar;
        /// <summary>Needle transform rotated around its pivot to represent RPM.
        /// Set min/max rotation angles below.</summary>
        [SerializeField] private Transform rpmNeedle;
        [SerializeField] private float     rpmNeedleMinAngle = 135f;   // at 0 rpm
        [SerializeField] private float     rpmNeedleMaxAngle = -135f;  // at max rpm

        // ── Damage indicator ──────────────────────────────────────────────────
        [Header("Damage")]
        [SerializeField] private Image damageIndicator;
        private static readonly Color DamageGreen  = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color DamageYellow = new Color(0.9f, 0.75f, 0.1f);
        private static readonly Color DamageRed    = new Color(0.9f, 0.1f, 0.1f);

        // ── Vehicle-specific UI ───────────────────────────────────────────────
        [Header("Vehicle-Specific Buttons")]
        [SerializeField] private GameObject[] vehicleSpecificButtons;
        [SerializeField] private VehicleType[] buttonVehicleTypes; // parallel array

        // ── Enter / Exit prompt ───────────────────────────────────────────────
        [Header("Enter / Exit Prompt")]
        [SerializeField] private bool showEnterExitPrompt;
        [SerializeField] private TMP_Text promptText;

        // ── State ─────────────────────────────────────────────────────────────
        private VehicleController _trackedVehicle;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (promptText != null)
                promptText.gameObject.SetActive(false);
        }

        // ── Main update call ──────────────────────────────────────────────────

        /// <summary>Call each frame while this vehicle is the active one.</summary>
        public void UpdateHUD(VehicleController v)
        {
            if (v == null) return;
            _trackedVehicle = v;

            // Speed (km/h)
            if (speedText    != null)
                speedText.text = $"{Mathf.RoundToInt(v.SpeedKmh)} km/h";

            // Gear
            if (gearText     != null)
                gearText.text  = v.CurrentGear == 0 ? "N"
                               : v.CurrentGear < 0  ? "R"
                               : v.CurrentGear.ToString();

            // Fuel
            float fuelNorm = v.MaxFuel > 0f ? v.CurrentFuel / v.MaxFuel : 0f;
            if (fuelText != null)
                fuelText.text = $"{Mathf.RoundToInt(v.CurrentFuel)} L";
            if (fuelBar  != null)
                fuelBar.value = fuelNorm;

            // Vehicle name
            if (vehicleNameText != null)
                vehicleNameText.text = v.VehicleName;

            // RPM needle
            if (rpmNeedle != null)
            {
                float rpmNorm  = v.MaxRPM > 0f ? v.CurrentRPM / v.MaxRPM : 0f;
                float angle    = Mathf.Lerp(rpmNeedleMinAngle, rpmNeedleMaxAngle, rpmNorm);
                rpmNeedle.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // Damage colour
            if (damageIndicator != null)
            {
                float hp = v.MaxHealth > 0f ? v.CurrentHealth / v.MaxHealth : 1f;
                damageIndicator.color = hp > 0.6f
                    ? Color.Lerp(DamageYellow, DamageGreen,  (hp - 0.6f) / 0.4f)
                    : Color.Lerp(DamageRed,    DamageYellow, hp / 0.6f);
            }

            // Vehicle-specific buttons
            ShowButtonsForVehicle(v.Type);
        }

        // ── Prompt ────────────────────────────────────────────────────────────

        public void ShowEnterExitPrompt(bool show, bool isInVehicle = false)
        {
            if (promptText == null) return;
            promptText.gameObject.SetActive(show);
            if (show)
                promptText.text = isInVehicle ? "Press E to Exit" : "Tap to Enter / Press E";
            showEnterExitPrompt = show;
        }

        // ── Vehicle-specific buttons ──────────────────────────────────────────

        private void ShowButtonsForVehicle(VehicleType type)
        {
            if (vehicleSpecificButtons == null) return;
            for (int i = 0; i < vehicleSpecificButtons.Length; i++)
            {
                if (vehicleSpecificButtons[i] == null) continue;
                bool visible = i < buttonVehicleTypes.Length && buttonVehicleTypes[i] == type;
                vehicleSpecificButtons[i].SetActive(visible);
            }
        }

        // ── Utility ───────────────────────────────────────────────────────────

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
