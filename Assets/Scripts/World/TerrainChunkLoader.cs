using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.World
{
    // ─────────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class TerrainChunkData
    {
        public Bounds   bounds;
        public Terrain  terrain;      // null when not loaded
        public bool     isLoaded;
        public int      lodLevel;     // 0 = full detail, 1 = medium, 2 = low
        public int      gridX;
        public int      gridZ;
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Streams an 8×8 grid of 500 m terrain chunks around the player.
    /// Only chunks within <see cref="loadRadius"/> grid cells are active;
    /// the rest are disabled.  Chunks approaching the view distance boundary
    /// receive a reduced heightmap resolution (LOD).
    /// </summary>
    public class TerrainChunkLoader : MonoBehaviour
    {
        // ── Grid constants ────────────────────────────────────────────────────
        public const int   GridSize        = 8;        // 8×8 = 64 chunks
        public const float ChunkSize       = 500f;     // metres per chunk
        public const float WorldSize       = GridSize * ChunkSize; // 4 000 m

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Player")]
        [SerializeField] private Transform playerTransform;

        [Header("Loading")]
        [Tooltip("Number of chunks in each direction around the player to keep loaded")]
        [SerializeField] private int loadRadius = 3;
        [SerializeField] private float updateInterval = 0.5f; // seconds between re-checks

        [Header("LOD Heightmap Resolutions")]
        [SerializeField] private int lodResolutionFull   = 513;
        [SerializeField] private int lodResolutionMedium = 257;
        [SerializeField] private int lodResolutionLow    = 129;
        [Tooltip("Grid-distance threshold for medium LOD")]
        [SerializeField] private int lodMediumThreshold  = 2;
        [Tooltip("Grid-distance threshold for low LOD")]
        [SerializeField] private int lodLowThreshold     = 3;

        [Header("Terrain Template")]
        [Tooltip("Optionally assign a template TerrainData to clone from")]
        [SerializeField] private TerrainData terrainDataTemplate;

        [Header("World Origin")]
        [SerializeField] private Vector3 worldOrigin = new Vector3(-2000f, 0f, -2000f);

        // ── Internal state ────────────────────────────────────────────────────
        private TerrainChunkData[,] _grid;
        private float               _nextUpdateTime;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            InitialiseGrid();

            if (playerTransform == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTransform = go.transform;
            }
        }

        private void Start() => StartCoroutine(UpdateLoop());

        // ── Grid initialisation ───────────────────────────────────────────────

        private void InitialiseGrid()
        {
            _grid = new TerrainChunkData[GridSize, GridSize];

            for (int z = 0; z < GridSize; z++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    Vector3 origin = worldOrigin + new Vector3(x * ChunkSize, 0f, z * ChunkSize);
                    Bounds  bounds = new Bounds(origin + new Vector3(ChunkSize * 0.5f, 150f, ChunkSize * 0.5f),
                                               new Vector3(ChunkSize, 300f, ChunkSize));

                    _grid[x, z] = new TerrainChunkData
                    {
                        bounds  = bounds,
                        gridX   = x,
                        gridZ   = z,
                        lodLevel = 2,
                        isLoaded = false,
                    };
                }
            }
        }

        // ── Main update coroutine ─────────────────────────────────────────────

        private IEnumerator UpdateLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(updateInterval);
                if (playerTransform != null)
                    UpdateChunks(playerTransform.position);
            }
        }

        /// <summary>Load/unload chunks based on player position.</summary>
        public void UpdateChunks(Vector3 playerPos)
        {
            // Determine player's grid cell
            int px = Mathf.FloorToInt((playerPos.x - worldOrigin.x) / ChunkSize);
            int pz = Mathf.FloorToInt((playerPos.z - worldOrigin.z) / ChunkSize);
            px = Mathf.Clamp(px, 0, GridSize - 1);
            pz = Mathf.Clamp(pz, 0, GridSize - 1);

            for (int z = 0; z < GridSize; z++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int dist = Mathf.Max(Mathf.Abs(x - px), Mathf.Abs(z - pz)); // Chebyshev
                    TerrainChunkData chunk = _grid[x, z];

                    if (dist <= loadRadius)
                    {
                        int targetLod = CalculateLod(dist);
                        if (!chunk.isLoaded)
                            LoadChunk(chunk);
                        if (chunk.isLoaded && chunk.lodLevel != targetLod)
                            SetChunkLod(chunk, targetLod);
                    }
                    else
                    {
                        if (chunk.isLoaded)
                            UnloadChunk(chunk);
                    }
                }
            }
        }

        // ── Chunk lifecycle ───────────────────────────────────────────────────

        private void LoadChunk(TerrainChunkData chunk)
        {
            if (chunk.terrain != null)
            {
                chunk.terrain.gameObject.SetActive(true);
                chunk.isLoaded = true;
                return;
            }

            // Create terrain
            TerrainData td = CreateTerrainData(lodResolutionFull);

            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = $"Chunk_{chunk.gridX}_{chunk.gridZ}";
            terrainGo.transform.SetParent(transform, true);
            terrainGo.transform.position = new Vector3(
                worldOrigin.x + chunk.gridX * ChunkSize,
                worldOrigin.y,
                worldOrigin.z + chunk.gridZ * ChunkSize);

            chunk.terrain  = terrainGo.GetComponent<Terrain>();
            chunk.isLoaded = true;
            chunk.lodLevel = 0;
        }

        private void UnloadChunk(TerrainChunkData chunk)
        {
            if (chunk.terrain != null)
                chunk.terrain.gameObject.SetActive(false);

            chunk.isLoaded = false;
        }

        private void SetChunkLod(TerrainChunkData chunk, int lod)
        {
            if (chunk.terrain == null) return;

            int resolution = lod switch
            {
                0 => lodResolutionFull,
                1 => lodResolutionMedium,
                _ => lodResolutionLow
            };

            // Replace TerrainData with one at the correct resolution
            TerrainData newTd = CreateTerrainData(resolution);
            chunk.terrain.terrainData = newTd;
            var collider = chunk.terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = newTd;

            chunk.lodLevel = lod;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private int CalculateLod(int gridDist)
        {
            if (gridDist <= lodMediumThreshold - 1) return 0;
            if (gridDist <= lodLowThreshold - 1)    return 1;
            return 2;
        }

        private TerrainData CreateTerrainData(int resolution)
        {
            TerrainData td;

            if (terrainDataTemplate != null)
            {
                td = Instantiate(terrainDataTemplate);
            }
            else
            {
                td = new TerrainData();
                td.heightmapResolution = resolution;
                td.size = new Vector3(ChunkSize, 300f, ChunkSize);
            }

            td.heightmapResolution = resolution;
            return td;
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_grid == null) return;
            for (int z = 0; z < GridSize; z++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    var chunk = _grid[x, z];
                    Gizmos.color = chunk.isLoaded
                        ? (chunk.lodLevel == 0 ? Color.green : chunk.lodLevel == 1 ? Color.yellow : Color.red)
                        : new Color(0.3f, 0.3f, 0.3f, 0.3f);
                    Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
                }
            }
        }
#endif
    }
}
