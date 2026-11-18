using UnityEngine;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Add this to the JCB root alongside JCBController.
    /// Monitors backhoe bucket tip position and digs terrain when the
    /// bucket is near the ground and the backhoe is being operated.
    /// Also manages the soil-load state for dumping into trucks.
    /// </summary>
    [RequireComponent(typeof(JCBController))]
    public class JCBDiggingSystem : MonoBehaviour
    {
        [Header("Bucket Tip")]
        [SerializeField] private Transform backhoeBucketTip;  // child at the tip of backhoe bucket

        [Header("Terrain")]
        [SerializeField] private TerrainDeformer terrainDeformer;
        [SerializeField] private float digRadius       = 0.5f;
        [SerializeField] private float digDepthPerSec  = 0.08f;
        [SerializeField] private float groundThreshold = 0.4f;  // metres above ground = digging

        [Header("Soil State")]
        [Range(0f, 1f)] public float soilVolume = 0f;
        [SerializeField] private float collectRate = 0.25f;    // fill per second while digging

        [Header("Effects")]
        [SerializeField] private ParticleSystem dirtParticles;
        [SerializeField] private ParticleSystem dumpParticles;

        // ── Private ───────────────────────────────────────────────────────
        private JCBController _jcb;
        private bool _isDigging;

        private void Awake()  => _jcb = GetComponent<JCBController>();

        private void Update()
        {
            if (!_jcb.isOccupied || !_jcb.engineRunning) return;
            if (!_jcb.StabilizersDown) { StopDigging(); return; }

            CheckDigging();
        }

        // ── Dig check ─────────────────────────────────────────────────────
        private void CheckDigging()
        {
            if (backhoeBucketTip == null || terrainDeformer == null) return;

            float groundY    = GetTerrainHeight(backhoeBucketTip.position);
            bool  nearGround = backhoeBucketTip.position.y < groundY + groundThreshold;

            if (nearGround && soilVolume < 1f)
            {
                StartDigging();
                terrainDeformer.DigAt(backhoeBucketTip.position, digRadius, digDepthPerSec * Time.deltaTime);
                soilVolume = Mathf.Clamp01(soilVolume + collectRate * Time.deltaTime);
            }
            else
            {
                StopDigging();
            }
        }

        private void StartDigging()
        {
            if (_isDigging) return;
            _isDigging = true;
            if (dirtParticles != null && !dirtParticles.isPlaying) dirtParticles.Play();
        }

        private void StopDigging()
        {
            if (!_isDigging) return;
            _isDigging = false;
            if (dirtParticles != null) dirtParticles.Stop();
        }

        // ── Dump ──────────────────────────────────────────────────────────
        /// <summary>Dumps soil at the current bucket position (raises terrain slightly).</summary>
        public void DumpSoil()
        {
            if (soilVolume <= 0f) return;

            Vector3 pos = backhoeBucketTip != null ? backhoeBucketTip.position : transform.position;

            if (terrainDeformer != null)
                terrainDeformer.RaiseAt(pos, digRadius * 2f, soilVolume * 0.25f);

            if (dumpParticles != null) dumpParticles.Play();

            soilVolume = 0f;
        }

        /// <summary>Dumps soil into a truck bed (no terrain raise).</summary>
        public void DumpSoilIntoTruck(TruckController truck)
        {
            if (soilVolume <= 0f || truck == null) return;
            // In a full implementation this would add weight/cargo to the truck
            if (dumpParticles != null) dumpParticles.Play();
            soilVolume = 0f;
        }

        // ── Helper ────────────────────────────────────────────────────────
        private float GetTerrainHeight(Vector3 worldPos)
        {
            if (Terrain.activeTerrain == null) return 0f;
            return Terrain.activeTerrain.SampleHeight(worldPos)
                 + Terrain.activeTerrain.transform.position.y;
        }

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (backhoeBucketTip == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(backhoeBucketTip.position, digRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawSphere(backhoeBucketTip.position, digRadius);
        }
    }
}
