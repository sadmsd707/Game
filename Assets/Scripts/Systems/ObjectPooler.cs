using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Generic GameObject pool.
    /// Call CreatePool() once per tag, then GetFromPool() / ReturnToPool().
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        public static ObjectPooler Instance { get; private set; }

        // ── Pool storage ──────────────────────────────────────────────────
        private readonly Dictionary<string, Queue<GameObject>> _pools    = new();
        private readonly Dictionary<string, GameObject>        _prefabs  = new();

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>Create a pool of <paramref name="size"/> inactive clones of <paramref name="prefab"/>.</summary>
        public void CreatePool(string tag, GameObject prefab, int size)
        {
            if (_pools.ContainsKey(tag))
            {
                Debug.LogWarning($"[ObjectPooler] Pool '{tag}' already exists.");
                return;
            }

            _prefabs[tag] = prefab;
            var queue = new Queue<GameObject>(size);

            for (int i = 0; i < size; i++)
            {
                var obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                queue.Enqueue(obj);
            }

            _pools[tag] = queue;
        }

        /// <summary>Returns an object from the pool, expanding by 1 if empty.</summary>
        public GameObject GetPooledObject(string tag) => GetFromPool(tag);

        public GameObject GetFromPool(string tag)
        {
            if (!_pools.TryGetValue(tag, out var queue))
            {
                Debug.LogError($"[ObjectPooler] Pool '{tag}' does not exist.");
                return null;
            }

            GameObject obj;
            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
            }
            else
            {
                // Pool exhausted – grow by one
                obj = Instantiate(_prefabs[tag], transform);
                Debug.LogWarning($"[ObjectPooler] Pool '{tag}' exhausted; expanded by 1.");
            }

            obj.SetActive(true);
            return obj;
        }

        /// <summary>Returns <paramref name="obj"/> to its pool and deactivates it.</summary>
        public void ReturnToPool(string tag, GameObject obj)
        {
            if (!_pools.ContainsKey(tag))
            {
                Debug.LogError($"[ObjectPooler] Pool '{tag}' does not exist. Destroying object instead.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform);
            _pools[tag].Enqueue(obj);
        }
    }
}
