using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Harvester-specific HUD panel.
    /// Wire up all references in the Inspector, then attach this MonoBehaviour
    /// to the HUD canvas root or a dedicated panel child.
    /// </summary>
    public class HarvesterUIController : MonoBehaviour
    {
        // ── Inspector: Core reference ─────────────────────────────────────
        [Header("Harvester Reference")]
        [SerializeField] public HarvesterController harvester;

        // ── Inspector: Grain display ──────────────────────────────────────
        [Header("Grain Display")]
        [SerializeField] private TMP_Text grainLevelText;      // e.g. "Grain: 85%"
        [SerializeField] private Slider   grainBar;            // 0 → 100
        [SerializeField] private GameObject tankFullWarning;   // shown when grain > 95 %

        // ── Inspector: Header & cutting ───────────────────────────────────
        [Header("Header / Cutting Buttons")]
        [SerializeField] private Button headerButton;          // raise / lower header
        [SerializeField] private Button cuttingButton;         // start / stop cutting

        [SerializeField] private TMP_Text headerButtonLabel;
        [SerializeField] private TMP_Text cuttingButtonLabel;

        // ── Inspector: Auger / unload ─────────────────────────────────────
        [Header("Auger / Unload Buttons")]
        [SerializeField] private Button augerExtendButton;
        [SerializeField] private Button augerRetractButton;
        [SerializeField] private Button unloadButton;
        [SerializeField] private GameObject augerPanel;        // only shown near delivery point

        // ── Inspector: Delivery point proximity ───────────────────────────
        [Header("Delivery Point")]
        [SerializeField] private GrainDeliveryPoint deliveryPoint; // drag silo here

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            // Wire button callbacks
            if (headerButton  != null) headerButton .onClick.AddListener(OnHeaderButtonClicked);
            if (cuttingButton != null) cuttingButton.onClick.AddListener(OnCuttingButtonClicked);
            if (augerExtendButton  != null) augerExtendButton .onClick.AddListener(OnAugerExtend);
            if (augerRetractButton != null) augerRetractButton.onClick.AddListener(OnAugerRetract);
            if (unloadButton       != null) unloadButton      .onClick.AddListener(OnUnload);
        }

        private void Update()
        {
            if (harvester == null) return;

            // ── Grain bar & text ──────────────────────────────────────────
            float grainPct = harvester.grainLevel; // 0–100
            if (grainBar       != null) grainBar.value         = grainPct;
            if (grainLevelText != null) grainLevelText.text     = $"Grain: {grainPct:F0}%";
            if (tankFullWarning != null) tankFullWarning.SetActive(grainPct > 95f);

            // ── Header button label ───────────────────────────────────────
            if (headerButtonLabel != null)
                headerButtonLabel.text = harvester.headerLowered ? "Raise Header" : "Lower Header";

            // ── Cutting button label ──────────────────────────────────────
            if (cuttingButtonLabel != null)
                cuttingButtonLabel.text = harvester.isCutting ? "Stop Cutting" : "Start Cutting";

            // ── Auger panel visibility (only near delivery point) ─────────
            bool nearSilo = deliveryPoint != null && deliveryPoint.harvesterInRange;
            if (augerPanel != null) augerPanel.SetActive(nearSilo);

            // ── Individual button interactability ─────────────────────────
            if (augerExtendButton  != null)
                augerExtendButton.interactable  = nearSilo && !harvester.augerExtended;
            if (augerRetractButton != null)
                augerRetractButton.interactable = harvester.augerExtended;
            if (unloadButton != null)
                unloadButton.interactable = harvester.augerExtended && !harvester.isUnloading && grainPct > 0f;
        }

        // ── Button handlers ───────────────────────────────────────────────
        private void OnHeaderButtonClicked()
        {
            if (harvester == null) return;
            harvester.RaiseLowerHeader();
        }

        private void OnCuttingButtonClicked()
        {
            if (harvester == null) return;
            if (harvester.isCutting)
                harvester.OnStopCutting();
            else
                harvester.OnStartCutting();
        }

        private void OnAugerExtend()
        {
            if (harvester == null) return;
            harvester.ExtendAuger();
        }

        private void OnAugerRetract()
        {
            if (harvester == null) return;
            harvester.RetractAuger();
        }

        private void OnUnload()
        {
            if (harvester == null) return;
            harvester.UnloadGrain();
        }
    }
}
