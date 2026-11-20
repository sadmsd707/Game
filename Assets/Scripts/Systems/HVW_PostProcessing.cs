using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Step 3 — Post-processing Global Volume + URP polish.
    ///
    /// Applied at runtime (Awake) so it works in both Editor Play mode and
    /// built APK/Windows builds without any manual setup.
    ///
    /// Settings tuned for a bright, clean, stylized-realistic mobile look:
    ///   • Bloom: soft glow on headlights and bright surfaces
    ///   • Color Grading (ACES tonemapping): punchy, saturated, no yellow blowout
    ///   • Vignette: subtle edge darkening
    ///   • Depth of Field: disabled (mobile perf + open-world readability)
    ///   • Film Grain: very subtle
    ///   • Chromatic Aberration: off (too harsh on mobile)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HVW_PostProcessing : MonoBehaviour
    {
        public static HVW_PostProcessing Instance { get; private set; }

        // ── Inspector knobs (safe defaults for mobile) ───────────────────────
        [Header("Bloom")]
        [Range(0f, 1f)]  public float bloomThreshold  = 0.85f;
        [Range(0f, 1f)]  public float bloomIntensity  = 0.35f;
        [Range(0f, 1f)]  public float bloomScatter    = 0.70f;

        [Header("Color Grading")]
        [Range(-1f, 1f)] public float postExposure    = 0.1f;
        [Range(-100f, 100f)] public float contrast    = 15f;
        [Range(-100f, 100f)] public float saturation  = 18f;
        [Range(1500f, 20000f)] public float colorTempK = 6500f; // neutral-cool daylight

        [Header("Vignette")]
        [Range(0f, 1f)]  public float vignetteIntensity = 0.22f;
        [Range(0f, 1f)]  public float vignetteSmoothness = 0.45f;

        [Header("Film Grain")]
        [Range(0f, 1f)]  public float grainIntensity  = 0.08f;

        private Volume _volume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            SetupGlobalVolume();
        }

        private void SetupGlobalVolume()
        {
            // Find or create the global post-processing volume
            _volume = GetComponent<Volume>();
            if (_volume == null) _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal   = true;
            _volume.priority   = 10;
            _volume.profile    = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile.name = "HVW_PostProcessProfile";

            // ── Tonemapping (ACES) ───────────────────────────────────────────
            var tone = _volume.profile.Add<Tonemapping>(overrides: true);
            tone.mode.value    = TonemappingMode.ACES;
            tone.mode.overrideState = true;

            // ── Bloom ────────────────────────────────────────────────────────
            var bloom = _volume.profile.Add<Bloom>(overrides: true);
            bloom.threshold.value   = bloomThreshold;
            bloom.intensity.value   = bloomIntensity;
            bloom.scatter.value     = bloomScatter;
            bloom.highQualityFiltering.value = false; // mobile perf
            bloom.threshold.overrideState = bloom.intensity.overrideState =
            bloom.scatter.overrideState   = bloom.highQualityFiltering.overrideState = true;

            // ── Color Adjustments ────────────────────────────────────────────
            var ca = _volume.profile.Add<ColorAdjustments>(overrides: true);
            ca.postExposure.value  = postExposure;
            ca.contrast.value      = contrast;
            ca.saturation.value    = saturation;
            ca.colorFilter.value   = Color.white;
            ca.postExposure.overrideState = ca.contrast.overrideState =
            ca.saturation.overrideState   = ca.colorFilter.overrideState = true;

            // ── White Balance (cool/neutral daylight) ────────────────────────
            var wb = _volume.profile.Add<WhiteBalance>(overrides: true);
            // Convert colour temperature to tint — rough approximation
            float tintK = Mathf.Clamp((colorTempK - 6500f) / 100f, -100f, 100f);
            wb.temperature.value   = tintK;
            wb.tint.value          = 0f;
            wb.temperature.overrideState = wb.tint.overrideState = true;

            // ── Vignette ─────────────────────────────────────────────────────
            var vig = _volume.profile.Add<Vignette>(overrides: true);
            vig.intensity.value    = vignetteIntensity;
            vig.smoothness.value   = vignetteSmoothness;
            vig.rounded.value      = true;
            vig.intensity.overrideState = vig.smoothness.overrideState =
            vig.rounded.overrideState   = true;

            // ── Film Grain ───────────────────────────────────────────────────
            var fg = _volume.profile.Add<FilmGrain>(overrides: true);
            fg.type.value          = FilmGrainLookup.Thin1;
            fg.intensity.value     = grainIntensity;
            fg.response.value      = 0.8f;
            fg.type.overrideState  = fg.intensity.overrideState = fg.response.overrideState = true;

            Debug.Log("[HVW_PostProcessing] Global Volume applied: ACES + Bloom + ColorGrading + Vignette + Grain");
        }
    }
}
