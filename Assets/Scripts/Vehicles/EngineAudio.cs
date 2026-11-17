using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Dedicated engine sound controller. Attach to the same GameObject as VehicleController.
    /// Handles start clip, idle loop, rev pitch driven by engineRPM, and fade-out on stop.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Inspector Fields
        // ------------------------------------------------------------------ //

        [Header("Audio Clips")]
        public AudioClip engineStartClip;
        public AudioClip engineIdleClip;
        public AudioClip engineRevClip;

        [Header("Pitch Settings")]
        public float idlePitch      = 0.6f;
        public float maxPitch       = 2.2f;
        public float pitchSmoothing = 3f;

        // ------------------------------------------------------------------ //
        // Private State
        // ------------------------------------------------------------------ //

        private AudioSource   _source;
        private VehicleController _vehicle;

        private float  _currentPitch;
        private bool   _engineRunning;
        private bool   _fadingOut;

        // ------------------------------------------------------------------ //
        // Unity Lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _source  = GetComponent<AudioSource>();
            _vehicle = GetComponent<VehicleController>();

            if (_vehicle == null)
                Debug.LogWarning("[EngineAudio] No VehicleController found on " + gameObject.name);

            _source.loop        = true;
            _source.playOnAwake = false;
            _currentPitch       = idlePitch;
        }

        private void Update()
        {
            if (!_engineRunning || _fadingOut) return;
            if (_vehicle == null) return;

            // Determine normalised RPM ratio (0–1)
            float rpmRatio = (_vehicle.maxRPM > 0f)
                ? Mathf.Clamp01(_vehicle.engineRPM / _vehicle.maxRPM)
                : 0f;

            // Choose clip: idle below 20 %, rev above
            AudioClip targetClip = (rpmRatio > 0.2f && engineRevClip != null)
                ? engineRevClip
                : engineIdleClip;

            if (_source.clip != targetClip)
            {
                _source.clip = targetClip;
                _source.Play();
            }

            // Smoothly lerp pitch
            float targetPitch = Mathf.Lerp(idlePitch, maxPitch, rpmRatio);
            _currentPitch     = Mathf.Lerp(_currentPitch, targetPitch,
                                            Time.deltaTime * pitchSmoothing);
            _source.pitch = _currentPitch;
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>Call when the engine is started. Plays start clip then transitions to idle loop.</summary>
        public void OnEngineStart()
        {
            if (_engineRunning) return;
            _engineRunning = true;
            _fadingOut     = false;

            StartCoroutine(PlayStartThenIdle());
        }

        /// <summary>Call when the engine is stopped. Fades out over 0.5 s.</summary>
        public void OnEngineStop()
        {
            if (!_engineRunning) return;
            _engineRunning = false;
            StartCoroutine(FadeOut(0.5f));
        }

        // ------------------------------------------------------------------ //
        // Coroutines
        // ------------------------------------------------------------------ //

        private IEnumerator PlayStartThenIdle()
        {
            if (engineStartClip != null)
            {
                _source.loop  = false;
                _source.clip  = engineStartClip;
                _source.pitch = 1f;
                _source.Play();

                yield return new WaitForSeconds(engineStartClip.length);
            }

            if (!_engineRunning) yield break; // stopped before idle

            _source.loop  = true;
            _source.clip  = engineIdleClip;
            _source.pitch = idlePitch;
            _source.Play();
        }

        private IEnumerator FadeOut(float duration)
        {
            _fadingOut = true;
            float startVolume = _source.volume;
            float elapsed     = 0f;

            while (elapsed < duration)
            {
                elapsed       += Time.deltaTime;
                _source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            _source.Stop();
            _source.volume = startVolume; // restore for next start
            _fadingOut     = false;
        }
    }
}
