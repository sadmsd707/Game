using UnityEngine;
using HeavyVehiclesWorld.Vehicles;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// [ExecuteInEditMode] validation script for the Excavator prefab.
    /// Logs a warning for every required reference that is missing or incomplete.
    /// Attach this component to the Excavator root GameObject and check the Console
    /// in the Editor to verify the prefab is properly wired.
    /// </summary>
    [ExecuteInEditMode]
    public class ExcavatorPrefabSetupGuide : MonoBehaviour
    {
        [Header("References to validate")]
        [SerializeField] private ExcavatorController excavator;

        // ── Unity lifecycle ───────────────────────────────────────────
        private void OnEnable()
        {
            // Auto-find on the same GameObject if not assigned
            if (excavator == null)
                excavator = GetComponent<ExcavatorController>();

            ValidateAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (excavator == null)
                excavator = GetComponent<ExcavatorController>();

            ValidateAll();
        }
#endif

        // ── Validation ────────────────────────────────────────────────
        private void ValidateAll()
        {
            if (excavator == null)
            {
                Debug.LogWarning("[ExcavatorPrefabSetupGuide] ExcavatorController component not found on this GameObject.", this);
                return;
            }

            // ── Track wheel arrays ─────────────────────────────────
            ValidateWheelArray(excavator.leftTrackWheels,  "leftTrackWheels",  4);
            ValidateWheelArray(excavator.rightTrackWheels, "rightTrackWheels", 4);

            // ── Cabin ──────────────────────────────────────────────
            if (excavator.cabinTransform == null)
                Warn("cabinTransform is not assigned.");

            // ── Hydraulic arms ─────────────────────────────────────
            if (excavator.boomArm == null)
                Warn("boomArm (HydraulicArm) is not assigned.");

            if (excavator.stickArm == null)
                Warn("stickArm (HydraulicArm) is not assigned.");

            if (excavator.bucketArm == null)
                Warn("bucketArm (HydraulicArm) is not assigned.");

            // ── Bucket tip ─────────────────────────────────────────
            if (excavator.bucketTip == null)
                Warn("bucketTip (Transform) is not assigned.");

            // ── Terrain deformer ───────────────────────────────────
            if (excavator.terrainDeformer == null)
                Warn("terrainDeformer (TerrainDeformer) is not assigned.");

#if UNITY_EDITOR
            if (IsFullyConfigured())
                Debug.Log("[ExcavatorPrefabSetupGuide] ✓ All 10 required references are correctly assigned.", this);
#endif
        }

        private void ValidateWheelArray(WheelCollider[] wheels, string fieldName, int expectedCount)
        {
            if (wheels == null || wheels.Length == 0)
            {
                Warn($"{fieldName} array is empty or null — expected {expectedCount} WheelColliders.");
                return;
            }

            if (wheels.Length != expectedCount)
                Warn($"{fieldName} has {wheels.Length} elements — expected exactly {expectedCount}.");

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null)
                    Warn($"{fieldName}[{i}] is null — assign a WheelCollider.");
            }
        }

        private bool IsFullyConfigured()
        {
            if (excavator == null) return false;

            bool leftOk  = IsWheelArrayValid(excavator.leftTrackWheels,  4);
            bool rightOk = IsWheelArrayValid(excavator.rightTrackWheels, 4);

            return leftOk
                && rightOk
                && excavator.cabinTransform  != null
                && excavator.boomArm         != null
                && excavator.stickArm        != null
                && excavator.bucketArm       != null
                && excavator.bucketTip       != null
                && excavator.terrainDeformer != null;
        }

        private bool IsWheelArrayValid(WheelCollider[] wheels, int expected)
        {
            if (wheels == null || wheels.Length != expected) return false;
            foreach (var w in wheels)
                if (w == null) return false;
            return true;
        }

        private void Warn(string message)
        {
            Debug.LogWarning($"[ExcavatorPrefabSetupGuide] ⚠ {message}", this);
        }
    }
}
