using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Effects
{
    /// <summary>
    /// ParticlePoolManager – object-pool specifically for ParticleSystem GameObjects.
    /// Pre-warms pools at startup to avoid runtime instantiation spikes on mobile.
    ///
    /// Usage:
    ///   ParticlePoolManager.Instance.Play("Dust", worldPosition);
    ///   // The particle auto-returns to pool when it finishes.
    /// </summary>
    public class ParticlePoolManager : MonoBehaviour
    {
        // ── Serialised types ───────────────────────────────────────────────────────

        [System.Serializable]
        public class ParticleEntry
        {
            public string         poolId;
            public ParticleSystem prefab;
            [Min(1)] public int   preWarmCount = 5;
            [Min(1)] public int   maxCount     = 20;
        }

        // ── Inspector ──────────────────────────────────────────────────────────────

        [Header("Particle Pools")]
        [SerializeField] private ParticleEntry[] entries;

        // ── Singleton ──────────────────────────────────────────────────────────────

        public static ParticlePoolManager Instance { get; private set; }

        // ── Pool data ──────────────────────────────────────────────────────────────

        private readonly Dictionary<string, Queue<ParticleSystem>>  _pools   = new();
        private readonly Dictionary<string, ParticleEntry>          _entries = new();
        private readonly Dictionary<ParticleSystem, string>         _active  = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (var e in entries)
            {
                if (e == null || e.prefab == null) continue;
                _entries[e.poolId] = e;
                _pools[e.poolId]   = new Queue<ParticleSystem>();

                // Pre-warm
                for (int i = 0; i < e.preWarmCount; i++)
                    _pools[e.poolId].Enqueue(CreateInstance(e));
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Play a pooled particle at a world position and optional rotation.</summary>
        public ParticleSystem Play(string poolId, Vector3 worldPos, Quaternion? rotation = null)
        {
            ParticleSystem ps = GetFromPool(poolId);
            if (ps == null) return null;

            ps.transform.position = worldPos;
            ps.transform.rotation = rotation ?? Quaternion.identity;
            ps.gameObject.SetActive(true);
            ps.Play();

            StartCoroutine(ReturnWhenFinished(ps, poolId));
            return ps;
        }

        /// <summary>Play attached to a transform (follows a vehicle wheel, etc.).</summary>
        public ParticleSystem PlayAttached(string poolId, Transform parent, Vector3 localOffset = default)
        {
            ParticleSystem ps = GetFromPool(poolId);
            if (ps == null) return null;

            ps.transform.SetParent(parent, false);
            ps.transform.localPosition = localOffset;
            ps.transform.localRotation = Quaternion.identity;
            ps.gameObject.SetActive(true);
            ps.Play();

            StartCoroutine(ReturnWhenFinished(ps, poolId, parent));
            return ps;
        }

        /// <summary>Manually stop and return a particle to its pool.</summary>
        public void Return(ParticleSystem ps)
        {
            if (ps == null) return;
            if (_active.TryGetValue(ps, out string id))
            {
                ReturnToPool(ps, id);
                _active.Remove(ps);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private ParticleSystem GetFromPool(string poolId)
        {
            if (!_pools.TryGetValue(poolId, out var queue))
            {
                Debug.LogWarning($"[ParticlePoolManager] Unknown pool id: {poolId}");
                return null;
            }

            if (queue.Count > 0)
            {
                var ps = queue.Dequeue();
                _active[ps] = poolId;
                return ps;
            }

            // Pool empty — create new if under cap
            if (!_entries.TryGetValue(poolId, out var entry)) return null;
            int activeCount = _active.Values.Count;   // rough estimate
            if (activeCount < entry.maxCount)
            {
                var ps = CreateInstance(entry);
                _active[ps] = poolId;
                return ps;
            }

            Debug.LogWarning($"[ParticlePoolManager] Pool '{poolId}' at max capacity ({entry.maxCount}).");
            return null;
        }

        private ParticleSystem CreateInstance(ParticleEntry e)
        {
            var go = Instantiate(e.prefab.gameObject, transform);
            go.SetActive(false);
            return go.GetComponent<ParticleSystem>();
        }

        private void ReturnToPool(ParticleSystem ps, string poolId)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(transform, false);
            ps.gameObject.SetActive(false);
            _pools[poolId].Enqueue(ps);
        }

        private System.Collections.IEnumerator ReturnWhenFinished(ParticleSystem ps, string poolId,
                                                                   Transform originalParent = null)
        {
            // Wait until the particle has finished playing
            while (ps != null && ps.isPlaying)
                yield return new WaitForSeconds(0.1f);

            if (ps == null) yield break;

            if (originalParent != null)
                ps.transform.SetParent(transform, false);

            if (_active.ContainsKey(ps))
                _active.Remove(ps);

            ReturnToPool(ps, poolId);
        }
    }
}
