using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// JCB-specific HUD. Attach to the Canvas that is parented under the JCB
    /// root or activated when the player enters the JCB.
    ///
    /// Panels:
    ///   backhoePanel  – visible when stabilizers are DOWN (backhoe mode)
    ///   drivePanel    – visible when stabilizers are UP  (drive mode)
    ///   loaderPanel   – always visible while occupied
    /// </summary>
    public class JCBUIController : MonoBehaviour
    {
        // ── Vehicle refs ──────────────────────────────────────────────────
        [Header("Vehicle References")]
        [SerializeField] private JCBController    jcb;
        [SerializeField] private JCBDiggingSystem diggingSystem;

        // ── Soil UI ───────────────────────────────────────────────────────
        [Header("Soil UI")]
        [SerializeField] private Slider    soilBar;           // 0–1
        [SerializeField] private TMP_Text  soilVolumeText;    // "Soil: 75%"

        // ── Stabilizer UI ─────────────────────────────────────────────────
        [Header("Stabilizer UI")]
        [SerializeField] private TMP_Text  stabilizerStatusText; // "Stabs: DOWN" / "Stabs: UP"
        [SerializeField] private Button    lowerStabsButton;
        [SerializeField] private Button    raiseStabsButton;

        // ── Dump ──────────────────────────────────────────────────────────
        [Header("Dump Button")]
        [SerializeField] private Button dumpSoilButton;

        // ── Panels ────────────────────────────────────────────────────────
        [Header("Panels")]
        [SerializeField] private GameObject backhoePanel;   // shown when stabs DOWN
        [SerializeField] private GameObject drivePanel;     // shown when stabs UP
        [SerializeField] private GameObject loaderPanel;    // always shown when occupied

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            // Wire buttons
            if (lowerStabsButton != null)
                lowerStabsButton.onClick.AddListener(() => jcb?.LowerStabilizers());

            if (raiseStabsButton != null)
                raiseStabsButton.onClick.AddListener(() => jcb?.RaiseStabilizers());

            if (dumpSoilButton != null)
                dumpSoilButton.onClick.AddListener(() => diggingSystem?.DumpSoil());
        }

        private void Update()
        {
            if (jcb == null) return;

            bool occupied = jcb.isOccupied;

            // ── Root panel visibility ─────────────────────────────────────
            if (loaderPanel  != null) loaderPanel.SetActive(occupied);

            if (!occupied)
            {
                SetPanelActive(backhoePanel, false);
                SetPanelActive(drivePanel,   false);
                return;
            }

            bool stabsDown = jcb.StabilizersDown;
            SetPanelActive(backhoePanel, stabsDown);
            SetPanelActive(drivePanel,  !stabsDown);

            // ── Stabilizer status text ────────────────────────────────────
            if (stabilizerStatusText != null)
                stabilizerStatusText.text = stabsDown ? "Stabs: DOWN" : "Stabs: UP";

            // ── Stab button interactability ───────────────────────────────
            if (lowerStabsButton != null) lowerStabsButton.interactable = !stabsDown;
            if (raiseStabsButton != null) raiseStabsButton.interactable =  stabsDown;

            // ── Soil UI ───────────────────────────────────────────────────
            if (diggingSystem != null)
            {
                float vol = diggingSystem.soilVolume;

                if (soilBar       != null) soilBar.value    = vol;
                if (soilVolumeText != null)
                    soilVolumeText.text = $"Soil: {Mathf.RoundToInt(vol * 100f)}%";

                if (dumpSoilButton != null)
                    dumpSoilButton.interactable = vol > 0f;
            }
        }

        // ── Helper ────────────────────────────────────────────────────────
        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
                panel.SetActive(active);
        }
    }
}
