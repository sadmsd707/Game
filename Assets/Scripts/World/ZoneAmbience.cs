using System.Collections;
using UnityEngine;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// ZoneAmbience – plays a looping ambient AudioClip for each WorldZone
    /// and crossfades smoothly when the player moves between zones.
    /// Attach to a persistent GameObject in the scene.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ZoneAmbience : MonoBehaviour
    {
        [System.Serializable]
        public struct ZoneClip
        {
            public WorldZone zone;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [Header("Zone Audio Clips")]
        [SerializeField] private ZoneClip[] zoneClips;

        [Header("Crossfade")]
        [SerializeField] private float crossfadeDuration = 2f;

        // two AudioSources for smooth crossfade
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _usingA = true;

        private ZoneManager _zoneManager;
        private WorldZone   _currentZone = (WorldZone)(-1); // invalid sentinel
        private Coroutine   _fadeCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _sourceA = GetComponent<AudioSource>();
            _sourceA.loop   = true;
            _sourceA.volume = 0f;
            _sourceA.playOnAwake = false;

            // second source for crossfade
            _sourceB = gameObject.AddComponent<AudioSource>();
            _sourceB.loop   = true;
            _sourceB.volume = 0f;
            _sourceB.playOnAwake = false;
        }

        private void Start()
        {
            _zoneManager = FindObjectOfType<ZoneManager>();
        }

        private void Update()
        {
            if (_zoneManager == null) return;
            WorldZone zone = _zoneManager.CurrentZone;
            if (zone != _currentZone)
            {
                _currentZone = zone;
                TransitionTo(zone);
            }
        }

        // ── Transition logic ───────────────────────────────────────────────────────

        private void TransitionTo(WorldZone zone)
        {
            AudioClip newClip = GetClipForZone(zone, out float targetVol);

            AudioSource fadeOut = _usingA ? _sourceA : _sourceB;
            AudioSource fadeIn  = _usingA ? _sourceB : _sourceA;
            _usingA = !_usingA;

            if (fadeIn.clip != newClip)
            {
                fadeIn.clip = newClip;
                if (newClip != null) fadeIn.Play();
            }

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(CrossFade(fadeOut, fadeIn, targetVol));
        }

        private IEnumerator CrossFade(AudioSource fadeOut, AudioSource fadeIn, float targetVol)
        {
            float startOut = fadeOut.volume;
            float elapsed  = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / crossfadeDuration;
                fadeOut.volume = Mathf.Lerp(startOut, 0f, t);
                fadeIn.volume  = Mathf.Lerp(0f, targetVol, t);
                yield return null;
            }

            fadeOut.volume = 0f;
            fadeOut.Stop();
            fadeIn.volume = targetVol;
        }

        private AudioClip GetClipForZone(WorldZone zone, out float volume)
        {
            foreach (var zc in zoneClips)
            {
                if (zc.zone == zone)
                {
                    volume = zc.volume;
                    return zc.clip;
                }
            }
            volume = 0f;
            return null;
        }
    }
}

