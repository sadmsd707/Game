using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HeavyVehiclesWorld.World
{
    /// <summary>
    /// [ExecuteInEditMode] editor tool that paints road splatmap textures and flattens road beds
    /// on Unity Terrain using the road network defined in a WorldMapData asset.
    ///
    /// Usage: attach to any GameObject in the scene, assign mapData, then tick generateNow
    /// in the Inspector. The script writes to the active Terrain and clears the flag.
    ///
    /// Road texture is assumed to be splatmap layer index 1 (asphalt layer).
    /// </summary>
    [ExecuteInEditMode]
    public class RoadNetworkGenerator : MonoBehaviour
    {
        [Header("Data")]
        public WorldMapData mapData;

        [Header("Terrain")]
        [Tooltip("Leave null to use Terrain.activeTerrain.")]
        public Terrain targetTerrain;

        [Header("Generation Trigger")]
        [Tooltip("Tick this in the Inspector to run generation once, then it auto-clears.")]
        public bool generateNow = false;

        [Header("Road Settings")]
        [Tooltip("Splatmap layer index to paint as road/asphalt (usually layer 1).")]
        public int  asphaltLayerIndex = 1;
        [Tooltip("Strength of the asphalt blend painted on the alphamap (0–1).")]
        [Range(0f, 1f)]
        public float paintStrength = 0.95f;
        [Tooltip("How many extra alphamap texels to paint each side beyond the road width.")]
        public int   edgeFeather   = 2;

        [Header("Road Bed Flattening")]
        [Tooltip("If true, also flattens the heightmap under each road segment.")]
        public bool flattenRoadBed = true;
        [Tooltip("Half-width in world units used for height flattening.")]
        public float flattenHalfWidth = 6f;
        [Tooltip("Blend falloff (texels) beyond the flatten strip.")]
        public int   flattenFeather   = 4;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Update()
        {
            if (!generateNow) return;
            generateNow = false;

#if UNITY_EDITOR
            GenerateRoads();
#else
            Debug.LogWarning("[RoadNetworkGenerator] Road generation is editor-only.");
#endif
        }

        // ── Main entry point ──────────────────────────────────────────────────

        /// <summary>Paints every road segment in mapData onto the terrain alphamap.</summary>
        public void GenerateRoads()
        {
            Terrain terrain = targetTerrain != null ? targetTerrain : Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[RoadNet] No active Terrain found.");
                return;
            }
            if (mapData == null || mapData.roads == null || mapData.roads.Length == 0)
            {
                Debug.LogWarning("[RoadNet] No road segments in mapData.");
                return;
            }

            TerrainData td = terrain.terrainData;

            // Validate splatmap layer
            if (asphaltLayerIndex >= td.alphamapLayers)
            {
                Debug.LogError($"[RoadNet] asphaltLayerIndex ({asphaltLayerIndex}) >= terrain alphamap layers ({td.alphamapLayers}). " +
                               "Add an asphalt texture layer in Terrain → Paint Texture.");
                return;
            }

            int   res      = td.alphamapResolution;
            float mapSize  = td.size.x; // assumes square terrain

            // Load the entire alphamap once
            float[,,] alphas = td.GetAlphamaps(0, 0, res, res);

            int processedSegments = 0;
            foreach (var seg in mapData.roads)
            {
                if (seg == null || seg.waypoints == null || seg.waypoints.Length < 2) continue;

                if (flattenRoadBed) FlattenRoadBed(terrain, td, seg);

                // Paint alphamap along each waypoint pair
                for (int i = 0; i < seg.waypoints.Length - 1; i++)
                    PaintRoadSegment(alphas, res, mapSize, seg.waypoints[i], seg.waypoints[i + 1], seg.width);

                processedSegments++;
            }

            // Write back
            td.SetAlphamaps(0, 0, alphas);

#if UNITY_EDITOR
            EditorUtility.SetDirty(terrain.terrainData);
#endif
            Debug.Log($"[RoadNet] Generated {processedSegments} road segment(s) on '{terrain.name}'.");
        }

        // ── Alphamap painting ─────────────────────────────────────────────────

        private void PaintRoadSegment(
            float[,,] alphas, int res, float mapSize,
            Vector3 start, Vector3 end, float roadWidth)
        {
            // Convert world XZ → normalised alphamap UV (0–1)
            Vector2 uvA = WorldToAlphamapUV(start, mapSize);
            Vector2 uvB = WorldToAlphamapUV(end,   mapSize);

            // Convert to texel coords
            int ax = Mathf.RoundToInt(uvA.x * (res - 1));
            int ay = Mathf.RoundToInt(uvA.y * (res - 1));
            int bx = Mathf.RoundToInt(uvB.x * (res - 1));
            int by = Mathf.RoundToInt(uvB.y * (res - 1));

            // Half-width in texels
            int halfW = Mathf.Max(1, Mathf.RoundToInt((roadWidth * 0.5f / mapSize) * res)) + edgeFeather;

            // Bresenham walk
            foreach (Vector2Int texel in BresenhamLine(ax, ay, bx, by))
            {
                PaintDisc(alphas, res, texel.x, texel.y, halfW);
            }
        }

        private void PaintDisc(float[,,] alphas, int res, int cx, int cy, int radius)
        {
            int layers = alphas.GetLength(2);

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int tx = cx + dx;
                int ty = cy + dy;
                if (tx < 0 || tx >= res || ty < 0 || ty >= res) continue;

                float dist   = Mathf.Sqrt(dx * dx + dy * dy);
                float weight = Mathf.Clamp01(1f - dist / radius) * paintStrength;

                // Redistribute weights: set asphalt, reduce others proportionally
                float remaining = 1f - weight;
                float otherSum  = 0f;
                for (int l = 0; l < layers; l++)
                    if (l != asphaltLayerIndex) otherSum += alphas[ty, tx, l];

                alphas[ty, tx, asphaltLayerIndex] = weight;

                for (int l = 0; l < layers; l++)
                {
                    if (l == asphaltLayerIndex) continue;
                    alphas[ty, tx, l] = otherSum > 0f
                        ? (alphas[ty, tx, l] / otherSum) * remaining
                        : (remaining / (layers - 1));
                }
            }
        }

        // ── Road bed flattening ───────────────────────────────────────────────

        /// <summary>
        /// Flattens the terrain heightmap under a road segment to simulate a prepared road bed.
        /// </summary>
        public void FlattenRoadBed(Terrain terrain, TerrainData td, RoadSegment seg)
        {
            if (seg == null || seg.waypoints == null || seg.waypoints.Length < 2) return;

            int   hRes    = td.heightmapResolution;
            float mapSize = td.size.x;
            float[,] heights = td.GetHeights(0, 0, hRes, hRes);

            for (int i = 0; i < seg.waypoints.Length - 1; i++)
            {
                Vector3 wA = seg.waypoints[i];
                Vector3 wB = seg.waypoints[i + 1];
                FlattenSegment(heights, hRes, mapSize, td, wA, wB);
            }

            td.SetHeights(0, 0, heights);
#if UNITY_EDITOR
            EditorUtility.SetDirty(td);
#endif
        }

        private void FlattenSegment(
            float[,] heights, int hRes, float mapSize, TerrainData td,
            Vector3 start, Vector3 end)
        {
            Vector2 uvA = WorldToHeightmapUV(start, mapSize);
            Vector2 uvB = WorldToHeightmapUV(end,   mapSize);

            int ax = Mathf.RoundToInt(uvA.x * (hRes - 1));
            int ay = Mathf.RoundToInt(uvA.y * (hRes - 1));
            int bx = Mathf.RoundToInt(uvB.x * (hRes - 1));
            int by = Mathf.RoundToInt(uvB.y * (hRes - 1));

            int halfW = Mathf.Max(1, Mathf.RoundToInt((flattenHalfWidth / mapSize) * hRes));

            foreach (Vector2Int texel in BresenhamLine(ax, ay, bx, by))
            {
                // Sample the target height at the road centre
                float targetH = heights[texel.y, texel.x];

                for (int dy = -(halfW + flattenFeather); dy <= (halfW + flattenFeather); dy++)
                for (int dx = -(halfW + flattenFeather); dx <= (halfW + flattenFeather); dx++)
                {
                    int tx = texel.x + dx;
                    int ty = texel.y + dy;
                    if (tx < 0 || tx >= hRes || ty < 0 || ty >= hRes) continue;

                    float dist  = Mathf.Sqrt(dx * dx + dy * dy);
                    float blend = Mathf.Clamp01(1f - (dist - halfW) / Mathf.Max(1, flattenFeather));
                    heights[ty, tx] = Mathf.Lerp(heights[ty, tx], targetH, blend);
                }
            }
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        /// <summary>World XZ → alphamap UV (0–1). Terrain origin is at world (0,0,0).</summary>
        private Vector2 WorldToAlphamapUV(Vector3 worldPos, float mapSize)
        {
            Terrain t = targetTerrain != null ? targetTerrain : Terrain.activeTerrain;
            Vector3 origin = t != null ? t.transform.position : Vector3.zero;
            return new Vector2(
                (worldPos.x - origin.x) / mapSize,
                (worldPos.z - origin.z) / mapSize);
        }

        private Vector2 WorldToHeightmapUV(Vector3 worldPos, float mapSize)
            => WorldToAlphamapUV(worldPos, mapSize); // same mapping

        // ── Bresenham line iterator ───────────────────────────────────────────

        private System.Collections.Generic.IEnumerable<Vector2Int> BresenhamLine(
            int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2;

            while (true)
            {
                yield return new Vector2Int(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = err;
                if (e2 > -dx) { err -= dy; x0 += sx; }
                if (e2 <  dy) { err += dx; y0 += sy; }
            }
        }

#if UNITY_EDITOR
        // ── Context menu shortcut ─────────────────────────────────────────────

        [ContextMenu("Generate Roads Now")]
        private void GenerateRoadsMenu() => GenerateRoads();
#endif
    }
}
