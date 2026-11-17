using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Excavator-specific HUD controller.
    /// Shows soil volume, speed, cabin angle, and provides dump controls.
    /// Also detects nearby TruckController within 8 m and reveals the dump-to-truck button.
    /// </summary>
    public class ExcavatorUIController : MonoBehaviour
    {
        [Header("Excavator Reference")]
        [SerializeField] private ExcavatorController excavator;

        [Header("Soil / Status Labels")]
        [SerializeField] private Slider      soilBar;          // 0–1 slider for currentSoilVolume
        [SerializeField] private TMP_Text    soilText;         // "Soil: 80%"
        [SerializeField] private TMP_Text    speedText;        // "Speed: 2.3 m/s"
        [SerializeField] private TMP_Text    cabinAngleText;   // "Cabin: 45°"

        [Header("Buttons")]
        [SerializeField] private Button      dumpButton;         // dumps soil onto ground
        [SerializeField] private Button      dumpToTruckButton;  // dumps soil into nearby truck

        [Header("Truck Scan")]
        [SerializeField] private float truckScanRadius  = 8f;
        [SerializeField] private float truckScanInterval = 0.5f;

        // ── Runtime ─────────────────────────────────────────────────────
        private TruckController _nearbyTruck;
        private Rigidbody       _rb;

        // ----------------------------------------------------------------
        private void Awake()
        {
            if (excavator == null)
                excavator = FindObjectOfType<ExcavatorController>();

            _rb = excavator != null ? excavator.GetComponent<Rigidbody>() : null;

            // Wire buttons
            if (dumpButton != null)
                dumpButton.onClick.AddListener(OnDumpPressed);

            if (dumpToTruckButton != null)
            {
                dumpToTruckButton.onClick.AddListener(OnDumpToTruckPressed);
                dumpToTruckButton.gameObject.SetActive(false);   // hidden until truck found
            }
        }

        private void Start()
        {
            InvokeRepeating(nameof(FindNearbyTruck), 0f, truckScanInterval);
        }

        // ----------------------------------------------------------------
        private void Update()
        {
            if (excavator == null) return;

            // Soil bar + text
            float soil = excavator.currentSoilVolume;
            if (soilBar  != null) soilBar.value = soil;
            if (soilText != null) soilText.text  = $"Soil: {Mathf.RoundToInt(soil * 100f)}%";

            // Speed
            if (speedText != null)
            {
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                speedText.text = $"Speed: {speed:F1} m/s";
            }

            // Cabin angle — ExcavatorController exposes cabinTransform
            if (cabinAngleText != null && excavator.cabinTransform != null)
            {
                float angle = excavator.cabinTransform.localEulerAngles.y;
                // Normalise to -180..180 for readability
                if (angle > 180f) angle -= 360f;
                cabinAngleText.text = $"Cabin: {angle:F0}°";
            }

            // Enable dump button only when there's soil to dump
            if (dumpButton != null)
                dumpButton.interactable = soil > 0f;
        }

        // ── Truck scanning ──────────────────────────────────────────────
        private void FindNearbyTruck()
        {
            if (excavator == null) return;

            Vector3 origin = excavator.transform.position;
            Collider[] hits = Physics.OverlapSphere(origin, truckScanRadius);

            TruckController found = null;
            foreach (var hit in hits)
            {
                var tc = hit.GetComponentInParent<TruckController>();
                if (tc != null) { found = tc; break; }
            }

            _nearbyTruck = found;
            if (dumpToTruckButton != null)
                dumpToTruckButton.gameObject.SetActive(_nearbyTruck != null);
        }

        // ── Button handlers ─────────────────────────────────────────────
        private void OnDumpPressed()
        {
            if (excavator != null)
                excavator.DumpSoil();
        }

        private void OnDumpToTruckPressed()
        {
            if (excavator == null || _nearbyTruck == null) return;

            // Transfer the excavator's soil payload into the truck
            float soilToTransfer = excavator.currentSoilVolume;
            _nearbyTruck.AddPayload(soilToTransfer);   // TruckController.AddPayload
            excavator.DumpSoil();                      // clear excavator bucket
        }
    }
}
