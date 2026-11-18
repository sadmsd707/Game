using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// VehicleLODSetup – [ExecuteInEditMode] helper that auto-configures LOD Groups
    /// on vehicle root GameObjects.
    ///
    /// How it works:
    ///   1. Attach this script to a vehicle root.
    ///   2. In the Inspector assign LOD0 (full detail), LOD1 (medium), LOD2 (low / billboard).
    ///   3. Click [Auto Configure LOD Group] – the script creates/updates the LODGroup component.
    ///   4. Remove this script from the prefab before shipping (it's editor-only setup).
    ///
    /// LOD thresholds used (percentage of screen height):
    ///   LOD0 → ≥ 15%   (close up, full mesh)
    ///   LOD1 → ≥  5%   (medium distance, reduced mesh)
    ///   LOD2 → ≥  1%   (far, low poly or impostor)
    ///   Culled below 1%
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class VehicleLODSetup : MonoBehaviour
    {
        [Header("LOD Renderers")]
        [Tooltip("All MeshRenderers/SkinnedMeshRenderers at full detail (close-up).")]
        public Renderer[] lod0Renderers;

        [Tooltip("Reduced-poly renderers for medium distance.")]
        public Renderer[] lod1Renderers;

        [Tooltip("Very low-poly or billboard renderers for far distance.")]
        public Renderer[] lod2Renderers;

        [Header("Thresholds (% screen height)")]
        [Range(0.01f, 1f)] public float lod0Threshold = 0.15f;
        [Range(0.01f, 1f)] public float lod1Threshold = 0.05f;
        [Range(0.01f, 1f)] public float lod2Threshold = 0.01f;

        [Header("Fade")]
        [Tooltip("Cross-fade between LOD levels instead of hard-switching.")]
        public bool crossFade = true;
        [Range(0.1f, 1f)] public float fadeTransitionWidth = 0.5f;

#if UNITY_EDITOR
        [ContextMenu("Auto Configure LOD Group")]
        public void AutoConfigure()
        {
            LODGroup lodGroup = GetComponent<LODGroup>();
            if (lodGroup == null) lodGroup = gameObject.AddComponent<LODGroup>();

            var lods = new System.Collections.Generic.List<LOD>();

            if (lod0Renderers != null && lod0Renderers.Length > 0)
                lods.Add(new LOD(lod0Threshold, lod0Renderers));

            if (lod1Renderers != null && lod1Renderers.Length > 0)
                lods.Add(new LOD(lod1Threshold, lod1Renderers));

            if (lod2Renderers != null && lod2Renderers.Length > 0)
                lods.Add(new LOD(lod2Threshold, lod2Renderers));

            if (lods.Count == 0)
            {
                Debug.LogWarning("[VehicleLODSetup] No renderers assigned — LOD group not created.");
                return;
            }

            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();

            if (crossFade)
                lodGroup.fadeMode = LODFadeMode.CrossFade;
            else
                lodGroup.fadeMode = LODFadeMode.None;

            EditorUtility.SetDirty(gameObject);
            Debug.Log($"[VehicleLODSetup] LOD group configured on '{gameObject.name}' " +
                      $"with {lods.Count} LOD levels.");
        }

        [ContextMenu("Remove LOD Group")]
        private void RemoveLODGroup()
        {
            var lg = GetComponent<LODGroup>();
            if (lg != null)
            {
                DestroyImmediate(lg);
                Debug.Log($"[VehicleLODSetup] Removed LODGroup from '{gameObject.name}'.");
            }
        }
#endif

        // Runtime: nothing — this is purely an editor configuration helper.
        // The LODGroup component does all the work at runtime.
    }
}
