using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// One crop field: spawns a grid of crop GameObjects, tracks harvested state,
    /// and can regrow for the next season.
    /// Works with HarvesterController and CropManager.
    /// </summary>
    public class CropField : MonoBehaviour
    {
        [Header("Field Layout")]
        public int rows    = 20;
        public int columns = 40;
        [SerializeField] private float rowSpacing    = 0.75f;
        [SerializeField] private float columnSpacing = 0.5f;

        [Header("Crop Prefabs")]
        [SerializeField] private GameObject[] cropStagePrefabs;
        // Index 0 = seedling, 1 = half-grown, 2 = mature (harvestable)
        [SerializeField] private int currentStage = 2;  // start mature

        [Header("State")]
        [SerializeField] private bool[] harvestedMask;   // length = rows*columns
        public float HarvestProgress => GetHarvestProgress();

        // ── Runtime ───────────────────────────────────────────────────────
        private GameObject[,] _cropInstances;
        private bool _fieldSpawned;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            SpawnField();
        }

        // ── Spawn ─────────────────────────────────────────────────────────
        public void SpawnField()
        {
            ClearField();
            if (cropStagePrefabs == null || cropStagePrefabs.Length == 0) return;
            if (currentStage >= cropStagePrefabs.Length) currentStage = cropStagePrefabs.Length - 1;

            GameObject prefab = cropStagePrefabs[currentStage];
            if (prefab == null) return;

            harvestedMask   = new bool[rows * columns];
            _cropInstances  = new GameObject[rows, columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 localPos = new Vector3(
                        c * columnSpacing - (columns * columnSpacing) * 0.5f,
                        0f,
                        r * rowSpacing   - (rows   * rowSpacing)   * 0.5f);

                    GameObject crop = Instantiate(prefab, transform);
                    crop.transform.localPosition = localPos;
                    crop.tag = "Crop";
                    _cropInstances[r, c] = crop;
                }
            }
            _fieldSpawned = true;
        }

        // ── Harvest ───────────────────────────────────────────────────────
        /// <summary>Cuts all crops whose world position falls inside 'area'.</summary>
        public int CutCropsInArea(Bounds area)
        {
            if (!_fieldSpawned) return 0;
            int cut = 0;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int idx = r * columns + c;
                    if (harvestedMask[idx]) continue;

                    GameObject crop = _cropInstances[r, c];
                    if (crop == null) continue;
                    if (!area.Contains(crop.transform.position)) continue;

                    // Knock the crop down: scale to near zero, disable collider
                    StartCoroutine(CropCutAnimation(crop, idx));
                    cut++;
                }
            }
            return cut;
        }

        private IEnumerator CropCutAnimation(GameObject crop, int idx)
        {
            harvestedMask[idx] = true;
            float t = 0f;
            Vector3 originalScale = crop.transform.localScale;

            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 0.05f, t / 0.3f);
                crop.transform.localScale = originalScale * s;
                yield return null;
            }
            crop.SetActive(false);
        }

        // ── Regrow ────────────────────────────────────────────────────────
        public void RegrowField(int stage = 2)
        {
            currentStage = Mathf.Clamp(stage, 0, cropStagePrefabs.Length - 1);
            SpawnField();
        }

        // ── Progress ──────────────────────────────────────────────────────
        private float GetHarvestProgress()
        {
            if (harvestedMask == null || harvestedMask.Length == 0) return 0f;
            int count = 0;
            foreach (bool h in harvestedMask) if (h) count++;
            return (float)count / harvestedMask.Length;
        }

        // ── Cleanup ───────────────────────────────────────────────────────
        public void ClearField()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _cropInstances = null;
            _fieldSpawned = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Vector3 size = new Vector3(columns * columnSpacing, 0.5f, rows * rowSpacing);
            Gizmos.DrawCube(transform.position, size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
