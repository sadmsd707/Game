using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Effects
{
    /// <summary>
    /// Attach to a vehicle root.  On Start it auto-discovers all HydraulicArm
    /// components in children and manages a shared looping hydraulic hiss audio
    /// source, fading in/out smoothly and modulating pitch by average arm speed.
    /// </summary>
    public class HydraulicSoundManager : MonoBehaviour
    {
        // ── Refs ──────────────────────────────────────────────────────────
        [Header("Arms (auto-found on Start)")]
        [SerializeField] private HydraulicArm[] arms;

        [Header("Audio")]
        [SerializeField] private AudioSource hydraulicLoopSource;
        [SerializeField] private AudioClip   hydraulicLoopClip;
        [SerializeField] private AudioClip   extendClip;
        [SerializeField] private AudioClip   retractClip;

        // ── Tuning ────────────────────────────────────────────────────────
        [Header("Tuning")]
        [SerializeField] private float fadeSpeed    = 3f;   // vol units per second
        [SerializeField] private float pitchMin     = 0.8f;
        [SerializeField] private float pitchMax     = 1.2f;
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 0.9f;

        // ── State ─────────────────────────────────────────────────────────
        private bool  _anyMovingPrev;
        private float _targetVolume;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            // Auto-discover arms
            if (arms == null || arms.Length == 0)
                arms = GetComponentsInChildren<HydraulicArm>(true);

            // Setup loop source
            if (hydraulicLoopSource == null)
                hydraulicLoopSource = gameObject.AddComponent<AudioSource>();

            hydraulicLoopSource.clip        = hydraulicLoopClip;
            hydraulicLoopSource.loop        = true;
            hydraulicLoopSource.playOnAwake = false;
            hydraulicLoopSource.volume      = 0f;
            hydraulicLoopSource.spatialBlend = 1f; // 3-D audio

            if (hydraulicLoopClip != null)
                hydraulicLoopSource.Play(); // play silent; faded in when moving
        }

        private void Update()
        {
            if (arms == null || arms.Length == 0) return;

            // ── Check which arms are moving ───────────────────────────────
            int   movingCount  = 0;
            float totalSpeed   = 0f;

            foreach (HydraulicArm arm in arms)
            {
                if (arm == null) continue;
                if (arm.IsMoving)
                {
                    movingCount++;
                    // Approximate "speed" as how far from target (angle delta proxy)
                    totalSpeed += 1f; // Arms don't expose speed directly; use count as proxy
                }
            }

            bool anyMoving = movingCount > 0;

            // ── One-shot extend/retract transient cues ────────────────────
            if (anyMoving && !_anyMovingPrev)
                PlayOneShot(extendClip);
            else if (!anyMoving && _anyMovingPrev)
                PlayOneShot(retractClip);

            _anyMovingPrev = anyMoving;

            // ── Target volume ─────────────────────────────────────────────
            _targetVolume = anyMoving ? maxVolume : 0f;

            // ── Smooth volume fade ────────────────────────────────────────
            if (hydraulicLoopSource != null)
            {
                hydraulicLoopSource.volume = Mathf.MoveTowards(
                    hydraulicLoopSource.volume,
                    _targetVolume,
                    fadeSpeed * Time.deltaTime);

                // ── Pitch modulation ──────────────────────────────────────
                if (anyMoving)
                {
                    float fraction = (float)movingCount / Mathf.Max(1, arms.Length);
                    hydraulicLoopSource.pitch = Mathf.Lerp(pitchMin, pitchMax, fraction);
                }
            }
        }

        // ── Helper ────────────────────────────────────────────────────────
        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || hydraulicLoopSource == null) return;
            hydraulicLoopSource.PlayOneShot(clip);
        }

        // ── Editor ────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Refresh arm list in editor when inspector changes
            if (!Application.isPlaying)
                arms = GetComponentsInChildren<HydraulicArm>(true);
        }
#endif
    }
}
