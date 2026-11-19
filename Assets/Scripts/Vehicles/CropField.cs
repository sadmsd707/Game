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
        public float rowSpacing    = 0.75f;
        public float columnSpacing = 0.5f;

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
            GameObject prefab = (cropStagePrefabs != null && cropStagePrefabs.Length > 0 && currentStage < cropStagePrefabs.Length)
                ? cropStagePrefabs[currentStage]
                : null;

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

                    GameObject crop;
                    if (prefab != null)
                    {
                        crop = Instantiate(prefab, transform);
                    }
                    else
                    {
                        crop = CreateProceduralWheatStalk();
                        crop.transform.SetParent(transform, false);
                    }
                    crop.transform.localPosition = localPos;
                    crop.tag = "Crop";
                    _cropInstances[r, c] = crop;
                }
            }
            _fieldSpawned = true;
        }

        private GameObject CreateProceduralWheatStalk()
        {
            var stalk = new GameObject("Wheat_Stalk");
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(stalk.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            stem.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
            var sc = stem.GetComponent<Collider>();
            if (sc != null) DestroyImmediate(sc);

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(stalk.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            head.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
            var hc = head.GetComponent<Collider>();
            if (hc != null) DestroyImmediate(hc);

            Shader safeShader = Shader.Find("Universal Render Pipeline/Lit");
            if (safeShader == null) safeShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (safeShader == null) safeShader = Shader.Find("Standard");
            if (safeShader == null) safeShader = Shader.Find("Hidden/InternalErrorShader");
            Material stalkMat = new Material(safeShader) { color = new Color(0.92f, 0.78f, 0.25f) };
            stem.GetComponent<Renderer>().sharedMaterial = stalkMat;
            head.GetComponent<Renderer>().sharedMaterial = stalkMat;

            var col = stalk.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.3f, 1.0f, 0.3f);
            col.center = new Vector3(0f, 0.5f, 0f);

            return stalk;
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
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
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
