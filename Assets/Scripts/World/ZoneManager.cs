using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HeavyVehiclesWorld.World
{
    public enum WorldZone
    {
        Farmland,
        ConstructionSite,
        Quarry,
        Highway,
        Village,
        Forest,
        River
    }

    [Serializable]
    public struct ZoneData
    {
        public string    zoneName;
        public WorldZone zone;
        public Bounds    bounds;
        public Color     ambientColor;
        [Range(0f, 0.1f)]
        public float     fogDensity;
        public AudioClip musicClip;
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class ZoneManager : MonoBehaviour
    {
        [Header("Zone Definitions")]
        [SerializeField] private ZoneData[] zones;

        [Header("Player")]
        [SerializeField] private Transform playerTransform;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private float musicCrossfadeTime = 2f;

        // ── Events ────────────────────────────────────────────────────────────
        public UnityEvent<WorldZone> OnZoneEntered  = new UnityEvent<WorldZone>();
        public UnityEvent<WorldZone> OnZoneExited   = new UnityEvent<WorldZone>();

        // ── State ─────────────────────────────────────────────────────────────
        private WorldZone _currentZone      = (WorldZone)(-1); // invalid sentinel
        private bool      _inZone           = false;
        private Coroutine _musicCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Auto-find player if not assigned
            if (playerTransform == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTransform = go.transform;
            }

            // Build default zones if none assigned in inspector
            if (zones == null || zones.Length == 0)
                zones = BuildDefaultZones();
        }

        private void Update()
        {
            if (playerTransform == null) return;
            DetectZone(playerTransform.position);
        }

        // ── Zone detection ────────────────────────────────────────────────────

        private void DetectZone(Vector3 playerPos)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (!zones[i].bounds.Contains(playerPos)) continue;

                WorldZone detected = zones[i].zone;
                if (_inZone && _currentZone == detected) return; // same zone, no change

                // Exiting previous zone
                if (_inZone)
                    OnZoneExited.Invoke(_currentZone);

                _currentZone = detected;
                _inZone      = true;
                ApplyZone(zones[i]);
                OnZoneEntered.Invoke(detected);
                return;
            }

            // Player not in any zone
            if (_inZone)
            {
                OnZoneExited.Invoke(_currentZone);
                _inZone = false;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns the zone containing <paramref name="worldPos"/>,
        /// or the first zone if none match.</summary>
        public WorldZone GetZoneAt(Vector3 worldPos)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].bounds.Contains(worldPos))
                    return zones[i].zone;
            }
            return zones.Length > 0 ? zones[0].zone : WorldZone.Farmland;
        }

        public WorldZone CurrentZone => _inZone ? _currentZone : (WorldZone)(-1);

        // ── Zone application ──────────────────────────────────────────────────

        private void ApplyZone(in ZoneData data)
        {
            RenderSettings.ambientLight  = data.ambientColor;
            RenderSettings.fogDensity    = data.fogDensity;
            RenderSettings.fog           = data.fogDensity > 0f;

            if (musicSource != null && data.musicClip != null)
            {
                if (_musicCoroutine != null) StopCoroutine(_musicCoroutine);
                _musicCoroutine = StartCoroutine(CrossfadeMusic(data.musicClip));
            }
        }

        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            float elapsed = 0f;
            float startVol = musicSource.volume;

            while (elapsed < musicCrossfadeTime * 0.5f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (musicCrossfadeTime * 0.5f));
                yield return null;
            }

            musicSource.clip  = newClip;
            musicSource.Play();
            elapsed = 0f;

            while (elapsed < musicCrossfadeTime * 0.5f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, startVol, elapsed / (musicCrossfadeTime * 0.5f));
                yield return null;
            }

            musicSource.volume = startVol;
        }

        // ── Default zone layout (4 km × 4 km, origin at world centre) ─────────
        private static ZoneData[] BuildDefaultZones()
        {
            // World: -2000 to +2000 on X and Z
            return new ZoneData[]
            {
                new ZoneData
                {
                    zone        = WorldZone.Farmland,
                    zoneName    = "Farmland",
                    bounds      = new Bounds(new Vector3(-1000, 50, -1000), new Vector3(1500, 300, 1500)),
                    ambientColor = new Color(0.6f, 0.7f, 0.4f),
                    fogDensity  = 0.002f,
                },
                new ZoneData
                {
                    zone        = WorldZone.ConstructionSite,
                    zoneName    = "Construction Site",
                    bounds      = new Bounds(new Vector3(800, 50, -800), new Vector3(600, 200, 600)),
                    ambientColor = new Color(0.5f, 0.45f, 0.35f),
                    fogDensity  = 0.005f,
                },
                new ZoneData
                {
                    zone        = WorldZone.Quarry,
                    zoneName    = "Quarry",
                    bounds      = new Bounds(new Vector3(1400, 0, 400), new Vector3(600, 200, 600)),
                    ambientColor = new Color(0.55f, 0.5f, 0.45f),
                    fogDensity  = 0.008f,
                },
                new ZoneData
                {
                    zone        = WorldZone.Highway,
                    zoneName    = "Highway",
                    bounds      = new Bounds(new Vector3(0, 50, 0), new Vector3(4000, 100, 200)),
                    ambientColor = new Color(0.6f, 0.65f, 0.7f),
                    fogDensity  = 0.001f,
                },
                new ZoneData
                {
                    zone        = WorldZone.Village,
                    zoneName    = "Village",
                    bounds      = new Bounds(new Vector3(-800, 50, 800), new Vector3(700, 200, 700)),
                    ambientColor = new Color(0.65f, 0.6f, 0.5f),
                    fogDensity  = 0.002f,
                },
                new ZoneData
                {
                    zone        = WorldZone.Forest,
                    zoneName    = "Forest",
                    bounds      = new Bounds(new Vector3(-1600, 50, 1200), new Vector3(800, 300, 800)),
                    ambientColor = new Color(0.2f, 0.45f, 0.2f),
                    fogDensity  = 0.015f,
                },
                new ZoneData
                {
                    zone        = WorldZone.River,
                    zoneName    = "River",
                    bounds      = new Bounds(new Vector3(0, 0, -1600), new Vector3(400, 100, 2000)),
                    ambientColor = new Color(0.35f, 0.5f, 0.65f),
                    fogDensity  = 0.006f,
                },
            };
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (zones == null) return;
            Color[] palette = { Color.green, Color.yellow, Color.red, Color.cyan, Color.white, Color.magenta, Color.blue };
            for (int i = 0; i < zones.Length; i++)
            {
                Gizmos.color = palette[i % palette.Length];
                Gizmos.DrawWireCube(zones[i].bounds.center, zones[i].bounds.size);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(zones[i].bounds.center + Vector3.up * 10f, zones[i].zoneName);
#endif
            }
        }
#endif
    }
}
