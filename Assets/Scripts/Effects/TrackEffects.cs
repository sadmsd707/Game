using UnityEngine;

namespace HeavyVehiclesWorld.Effects
{
    /// <summary>
    /// Manages visual track effects: ground impression decals, dust clouds,
    /// and mud splatter based on surface type and speed.
    /// Attach to the excavator or tractor root.
    /// </summary>
    public class TrackEffects : MonoBehaviour
    {
        [Header("Track Points (ground-contact positions)")]
        [SerializeField] private Transform[] leftTrackGroundPoints;   // 4 points along left track
        [SerializeField] private Transform[] rightTrackGroundPoints;  // 4 points along right track

        [Header("Effects")]
        [SerializeField] private ParticleSystem leftDustPS;
        [SerializeField] private ParticleSystem rightDustPS;
        [SerializeField] private TrailRenderer  leftTrackTrail;    // imprint on ground
        [SerializeField] private TrailRenderer  rightTrackTrail;

        [Header("Surface Detection")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float rayLength = 1.5f;

        [Header("Thresholds")]
        [SerializeField] private float dustSpeedThreshold = 1f;    // m/s above = emit dust
        [SerializeField] private float mudSpeedThreshold  = 0.3f;

        // ── Runtime ───────────────────────────────────────────────────────
        private Rigidbody _rb;
        private float _speed;

        private void Start() => _rb = GetComponentInParent<Rigidbody>();

        private void Update()
        {
            _speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;

            bool onGround = IsTrackOnGround();
            bool fast     = _speed > dustSpeedThreshold;

            // Dust
            SetParticleEmission(leftDustPS,  onGround && fast);
            SetParticleEmission(rightDustPS, onGround && fast);

            // Track trails
            if (leftTrackTrail  != null) leftTrackTrail.emitting  = onGround && _speed > mudSpeedThreshold;
            if (rightTrackTrail != null) rightTrackTrail.emitting = onGround && _speed > mudSpeedThreshold;

            // Scale emission rate with speed
            if (onGround)
            {
                float rate = Mathf.Lerp(5f, 60f, Mathf.Clamp01(_speed / 5f));
                SetEmissionRate(leftDustPS,  rate);
                SetEmissionRate(rightDustPS, rate);
            }
        }

        private bool IsTrackOnGround()
        {
            // Check if any ground point is near the terrain
            var points = leftTrackGroundPoints;
            if (points == null) return false;
            foreach (var p in points)
            {
                if (p == null) continue;
                if (Physics.Raycast(p.position, Vector3.down, rayLength, groundLayer))
                    return true;
            }
            return false;
        }

        private void SetParticleEmission(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.enabled = active;
            if (active && !ps.isPlaying) ps.Play();
            else if (!active && ps.isPlaying) ps.Stop();
        }

        private void SetEmissionRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
