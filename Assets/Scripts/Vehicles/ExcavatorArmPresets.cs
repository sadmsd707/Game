using UnityEngine;
using UnityEngine.UI;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Saves and recalls named arm-angle presets for the excavator's
    /// boom, stick, and bucket hydraulic arms.
    /// Keyboard: 1–4 applies presets 0–3.
    /// Mobile: assign 4 UI Buttons to mobilePresetButtons[].
    /// </summary>
    public class ExcavatorArmPresets : MonoBehaviour
    {
        // ── Preset data ───────────────────────────────────────────────
        [System.Serializable]
        public struct ArmPreset
        {
            public string name;
            public float  boomAngle;
            public float  stickAngle;
            public float  bucketAngle;

            public ArmPreset(string n, float boom, float stick, float bucket)
            {
                name        = n;
                boomAngle   = boom;
                stickAngle  = stick;
                bucketAngle = bucket;
            }
        }

        // ── Inspector ─────────────────────────────────────────────────
        [Header("Excavator")]
        [SerializeField] private ExcavatorController excavator;

        [Header("Presets (4 slots)")]
        [SerializeField] private ArmPreset[] presets = new ArmPreset[4]
        {
            new ArmPreset("Travel",       20f,  90f,   0f),
            new ArmPreset("Dig Position", 10f,  45f, -30f),
            new ArmPreset("Dump High",    70f,  20f,  60f),
            new ArmPreset("Curl",         30f, 110f,  80f),
        };

        [Header("Mobile UI Buttons (optional, 4 slots)")]
        [SerializeField] private Button[] mobilePresetButtons;  // assigned in Inspector

        // ── Keyboard mappings ─────────────────────────────────────────
        private static readonly KeyCode[] PresetKeys = new KeyCode[]
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
        };

        // ── Unity lifecycle ───────────────────────────────────────────
        private void Awake()
        {
            if (excavator == null)
                excavator = GetComponentInParent<ExcavatorController>();

            WireMobileButtons();
        }

        private void Update()
        {
            // Keyboard shortcuts 1-4
            for (int i = 0; i < PresetKeys.Length && i < presets.Length; i++)
            {
                if (Input.GetKeyDown(PresetKeys[i]))
                    ApplyPreset(i);
            }
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Applies the named preset to the excavator's hydraulic arms.
        /// </summary>
        public void ApplyPreset(int index)
        {
            if (excavator == null) return;
            if (index < 0 || index >= presets.Length)
            {
                Debug.LogWarning($"[ExcavatorArmPresets] Preset index {index} out of range.");
                return;
            }

            ArmPreset p = presets[index];
            excavator.boomArm  ?.SetTargetAngle(p.boomAngle);
            excavator.stickArm ?.SetTargetAngle(p.stickAngle);
            excavator.bucketArm?.SetTargetAngle(p.bucketAngle);

            Debug.Log($"[ExcavatorArmPresets] Applied preset '{p.name}' " +
                      $"(boom={p.boomAngle}, stick={p.stickAngle}, bucket={p.bucketAngle})");
        }

        /// <summary>
        /// Reads the current arm angles and stores them in the given preset slot.
        /// </summary>
        public void SaveCurrentAsPreset(int index, string presetName)
        {
            if (excavator == null) return;
            if (index < 0 || index >= presets.Length)
            {
                Debug.LogWarning($"[ExcavatorArmPresets] Preset index {index} out of range.");
                return;
            }

            float boom   = excavator.boomArm   != null ? excavator.boomArm.CurrentAngle   : 0f;
            float stick  = excavator.stickArm  != null ? excavator.stickArm.CurrentAngle  : 0f;
            float bucket = excavator.bucketArm != null ? excavator.bucketArm.CurrentAngle : 0f;

            presets[index] = new ArmPreset(presetName, boom, stick, bucket);
            Debug.Log($"[ExcavatorArmPresets] Saved preset '{presetName}' at slot {index}.");
        }

        // ── Private helpers ───────────────────────────────────────────
        private void WireMobileButtons()
        {
            if (mobilePresetButtons == null) return;

            for (int i = 0; i < mobilePresetButtons.Length && i < presets.Length; i++)
            {
                if (mobilePresetButtons[i] == null) continue;

                int captured = i;   // capture for closure
                mobilePresetButtons[i].onClick.AddListener(() => ApplyPreset(captured));
            }
        }
    }
}
