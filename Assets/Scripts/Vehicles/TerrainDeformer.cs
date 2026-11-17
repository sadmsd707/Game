using UnityEngine;

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// Deforms a Unity Terrain in real-time for excavator digging simulation.
    /// Attach to a GameObject in the scene that holds a reference to the active Terrain.
    /// </summary>
    public class TerrainDeformer : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────
        [Header("Terrain Reference")]
        [SerializeField] private Terrain targetTerrain;

        [Header("Settings")]
        [Tooltip("Scale factor applied to depth / amount when modifying heightmap values.")]
        [SerializeField] private float heightScale = 0.002f;

        // ── Cached data ────────────────────────────────────────────────────
        private TerrainData TerrainData => targetTerrain ? targetTerrain.terrainData : null;

        // ── Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (targetTerrain == null)
                targetTerrain = Terrain.activeTerrain;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Lowers terrain heights within a circular footprint centred on worldPos.
        /// </summary>
        /// <param name="worldPos">World-space centre of the dig area.</param>
        /// <param name="radius">Radius in world units.</param>
        /// <param name="depth">How deep to dig (world units, positive = deeper).</param>
        public void DigAt(Vector3 worldPos, float radius, float depth)
        {
            ModifyHeights(worldPos, radius, -depth * heightScale);
        }

        /// <summary>
        /// Raises terrain heights within a circular footprint centred on worldPos.
        /// </summary>
        /// <param name="worldPos">World-space centre of the raise area.</param>
        /// <param name="radius">Radius in world units.</param>
        /// <param name="amount">How much to raise (world units, positive = higher).</param>
        public void RaiseAt(Vector3 worldPos, float radius, float amount)
        {
            ModifyHeights(worldPos, radius, amount * heightScale);
        }

        /// <summary>
        /// Estimates the volume of soil that would be removed by digging a circular
        /// area of the given radius (returns a normalised 0-1 value).
        /// </summary>
        public float GetSoilVolumeAt(Vector3 worldPos, float radius)
        {
            if (targetTerrain == null) return 0f;

            TerrainData td = TerrainData;
            int hmRes = td.heightmapResolution;
            Vector3 terrainPos = targetTerrain.transform.position;
            Vector3 terrainSize = td.size;

            // Centre in heightmap space
            float cx = (worldPos.x - terrainPos.x) / terrainSize.x * (hmRes - 1);
            float cz = (worldPos.z - terrainPos.z) / terrainSize.z * (hmRes - 1);
            int pixelRadius = Mathf.CeilToInt(radius / terrainSize.x * (hmRes - 1));

            int xMin = Mathf.Clamp(Mathf.RoundToInt(cx) - pixelRadius, 0, hmRes - 1);
            int zMin = Mathf.Clamp(Mathf.RoundToInt(cz) - pixelRadius, 0, hmRes - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(cx) + pixelRadius, 0, hmRes - 1);
            int zMax = Mathf.Clamp(Mathf.RoundToInt(cz) + pixelRadius, 0, hmRes - 1);

            float[,] heights = td.GetHeights(xMin, zMin, xMax - xMin + 1, zMax - zMin + 1);

            float total = 0f;
            int count = 0;
            for (int z = 0; z < heights.GetLength(0); z++)
            {
                for (int x = 0; x < heights.GetLength(1); x++)
                {
                    float dx = (xMin + x) - cx;
                    float dz = (zMin + z) - cz;
                    if (dx * dx + dz * dz <= pixelRadius * pixelRadius)
                    {
                        total += heights[z, x];
                        count++;
                    }
                }
            }

            if (count == 0) return 0f;
            return Mathf.Clamp01(total / count); // normalised average height as proxy for volume
        }

        // ── Private helpers ────────────────────────────────────────────────

        private void ModifyHeights(Vector3 worldPos, float radius, float deltaHeight)
        {
            if (targetTerrain == null) return;

            TerrainData td = TerrainData;
            int hmRes = td.heightmapResolution;
            Vector3 terrainPos = targetTerrain.transform.position;
            Vector3 terrainSize = td.size;

            // Convert world position to heightmap pixel coords
            float cx = (worldPos.x - terrainPos.x) / terrainSize.x * (hmRes - 1);
            float cz = (worldPos.z - terrainPos.z) / terrainSize.z * (hmRes - 1);
            int pixelRadius = Mathf.CeilToInt(radius / terrainSize.x * (hmRes - 1));

            int xMin = Mathf.Clamp(Mathf.RoundToInt(cx) - pixelRadius, 0, hmRes - 1);
            int zMin = Mathf.Clamp(Mathf.RoundToInt(cz) - pixelRadius, 0, hmRes - 1);
            int width  = Mathf.Clamp(Mathf.RoundToInt(cx) + pixelRadius, 0, hmRes - 1) - xMin + 1;
            int height = Mathf.Clamp(Mathf.RoundToInt(cz) + pixelRadius, 0, hmRes - 1) - zMin + 1;

            float[,] heights = td.GetHeights(xMin, zMin, width, height);

            float radiusSq = pixelRadius * (float)pixelRadius;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (xMin + x) - cx;
                    float dz = (zMin + z) - cz;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        // Smooth falloff from centre to edge
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        float falloff = 1f - Mathf.Clamp01(dist / pixelRadius);
                        float newH = heights[z, x] + deltaHeight * falloff;
                        heights[z, x] = Mathf.Max(0f, newH); // never below 0
                    }
                }
            }

            td.SetHeights(xMin, zMin, heights);
        }
    }
}
