using UnityEngine;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// [ExecuteInEditMode] validation component for the JCB prefab.
    ///
    /// Add to the JCB root alongside JCBController and JCBDiggingSystem.
    /// It checks every required reference at edit-time and logs a warning
    /// (with the exact field name) for any that are missing.
    ///
    /// Open the prefab in the Unity editor and check the Console to see
    /// a full list of unassigned references.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(JCBController))]
    public class JCBPrefabSetupGuide : MonoBehaviour
    {
        [Header("Wheel Colliders (all 4 required)")]
        [SerializeField] private WheelCollider wheelFL;
        [SerializeField] private WheelCollider wheelFR;
        [SerializeField] private WheelCollider wheelRL;
        [SerializeField] private WheelCollider wheelRR;

        [Header("Loader Hydraulic Arms")]
        [SerializeField] private HydraulicArm loaderBoom;
        [SerializeField] private HydraulicArm loaderArm;
        [SerializeField] private HydraulicArm loaderBucket;

        [Header("Backhoe Hydraulic Arms")]
        [SerializeField] private HydraulicArm backhoeSwing;
        [SerializeField] private HydraulicArm backhoeBoom;
        [SerializeField] private HydraulicArm backhoeDipper;
        [SerializeField] private HydraulicArm backhoeBucket;

        [Header("Stabilizers")]
        [SerializeField] private Transform stabilizerLeft;
        [SerializeField] private Transform stabilizerRight;

        [Header("Digging System")]
        [SerializeField] private JCBDiggingSystem diggingSystem;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void OnEnable()  => Validate();
        private void OnValidate() => Validate();

        // ── Validation ────────────────────────────────────────────────────
        private void Validate()
        {
            int issueCount = 0;

            // ── Wheel Colliders ───────────────────────────────────────────
            issueCount += Warn(wheelFL,  nameof(wheelFL));
            issueCount += Warn(wheelFR,  nameof(wheelFR));
            issueCount += Warn(wheelRL,  nameof(wheelRL));
            issueCount += Warn(wheelRR,  nameof(wheelRR));

            // ── Loader Arms ───────────────────────────────────────────────
            issueCount += Warn(loaderBoom,   nameof(loaderBoom));
            issueCount += Warn(loaderArm,    nameof(loaderArm));
            issueCount += Warn(loaderBucket, nameof(loaderBucket));

            // ── Backhoe Arms ──────────────────────────────────────────────
            issueCount += Warn(backhoeSwing,   nameof(backhoeSwing));
            issueCount += Warn(backhoeBoom,    nameof(backhoeBoom));
            issueCount += Warn(backhoeDipper,  nameof(backhoeDipper));
            issueCount += Warn(backhoeBucket,  nameof(backhoeBucket));

            // ── Stabilizers ───────────────────────────────────────────────
            issueCount += Warn(stabilizerLeft,  nameof(stabilizerLeft));
            issueCount += Warn(stabilizerRight, nameof(stabilizerRight));

            // ── Digging System + bucket tip ───────────────────────────────
            issueCount += Warn(diggingSystem, nameof(diggingSystem));

            if (diggingSystem != null)
            {
                // Reflect into private field via SerializedObject in Editor; in play-mode
                // we rely on the system itself to validate its own tip reference.
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var so  = new UnityEditor.SerializedObject(diggingSystem);
                    var tip = so.FindProperty("backhoeBucketTip");

                    if (tip != null && tip.objectReferenceValue == null)
                    {
                        Debug.LogWarning(
                            $"[JCBPrefabSetupGuide] '{name}' – " +
                            "JCBDiggingSystem.backhoeBucketTip is not assigned.",
                            gameObject);
                        issueCount++;
                    }
                }
#endif
            }

            if (issueCount == 0)
                Debug.Log($"[JCBPrefabSetupGuide] '{name}' – All 13 required references are assigned. ✓",
                          gameObject);
            else
                Debug.LogWarning(
                    $"[JCBPrefabSetupGuide] '{name}' – {issueCount} reference(s) missing. " +
                    "Assign them in the Inspector before entering Play mode.",
                    gameObject);
        }

        // ── Helper ────────────────────────────────────────────────────────
        /// <summary>Logs a warning if the object reference is null; returns 1 on issue, 0 otherwise.</summary>
        private int Warn(Object obj, string fieldName)
        {
            if (obj != null) return 0;

            Debug.LogWarning(
                $"[JCBPrefabSetupGuide] '{name}' – field '{fieldName}' is not assigned.",
                gameObject);
            return 1;
        }
    }
}
