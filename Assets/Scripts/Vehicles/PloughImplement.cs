using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.World;

namespace HeavyVehiclesWorld.Vehicles
{
    /// <summary>
    /// Plough implement. Attaches to TractorController rear hitch.
    /// When PTO is active and hitch is lowered (hitchPosition &lt; 0.2), deforms terrain.
    /// </summary>
    public class PloughImplement : MonoBehaviour
    {
        [Header("Attachment")]
        [SerializeField] private TractorController tractor;
        [SerializeField] private Transform attachPoint;      // local pivot on this plough

        [Header("Plough Blades")]
        [SerializeField] private Transform[] bladeTransforms; // 3-5 blade rows
        [SerializeField] private float bladeDepth = 0.25f;    // metres into terrain
        [SerializeField] private float bladeWidth = 0.4f;     // per blade deformation radius

        [Header("Terrain Deformation")]
        [SerializeField] private TerrainDeformer terrainDeformer;
        [SerializeField] private float deformRate = 0.02f;    // depth per FixedUpdate step

        [Header("Effects")]
        [SerializeField] private ParticleSystem soilDustParticles;
        [SerializeField] private TrailRenderer[] soilTrails;  // one per blade

        // ── State ─────────────────────────────────────────────────────────
        private bool _isPloughing;
        private Joint _hitchJoint;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            gameObject.tag = "Implement";
        }

        private void FixedUpdate()
        {
            if (tractor == null) return;

            bool shouldPlough = tractor.engineRunning
                             && tractor.ptoActive
                             && tractor.hitchPosition < 0.2f
                             && tractor.currentSpeed > 0.5f;

            if (shouldPlough != _isPloughing)
            {
                _isPloughing = shouldPlough;
                SetEffectsActive(shouldPlough);
            }

            if (_isPloughing)
                DeformAlongBlades();
        }

        // ── Attachment ────────────────────────────────────────────────────

        /// <summary>Snaps this plough to the tractor's rear hitch.</summary>
        public void AttachToTractor(TractorController t)
        {
            tractor = t;
            if (t.rearHitch == null) return;

            // Position plough at hitch
            transform.position = t.rearHitch.position;
            transform.rotation = t.rearHitch.rotation;

            // Create a fixed joint to tractor rigidbody
            var joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = t.GetComponent<Rigidbody>();
            joint.breakForce    = Mathf.Infinity;
            joint.breakTorque   = Mathf.Infinity;
            _hitchJoint = joint;

            t.AttachImplement(AttachedImplement.Plough);
        }

        /// <summary>Detaches this plough from the tractor.</summary>
        public void DetachFromTractor()
        {
            if (_hitchJoint != null) Destroy(_hitchJoint);
            tractor?.DetachImplement();
            tractor = null;
        }

        // ── Deformation ───────────────────────────────────────────────────

        private void DeformAlongBlades()
        {
            if (terrainDeformer == null || bladeTransforms == null) return;

            foreach (var blade in bladeTransforms)
            {
                if (blade == null) continue;
                terrainDeformer.DigAt(blade.position, bladeWidth, deformRate);
            }
        }

        // ── Effects ───────────────────────────────────────────────────────

        private void SetEffectsActive(bool active)
        {
            if (soilDustParticles != null)
            {
                if (active && !soilDustParticles.isPlaying) soilDustParticles.Play();
                else if (!active) soilDustParticles.Stop();
            }

            if (soilTrails != null)
                foreach (var t in soilTrails)
                    if (t != null) t.emitting = active;
        }

        private void OnDrawGizmosSelected()
        {
            if (bladeTransforms == null) return;
            Gizmos.color = Color.yellow;
            foreach (var b in bladeTransforms)
                if (b != null) Gizmos.DrawWireSphere(b.position, bladeWidth);
        }
    }
}
