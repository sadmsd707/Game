using UnityEngine;
using UnityEngine.Rendering;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Step 3 — Vehicle Lighting: headlights, brake lights, wheel dust.
    ///
    /// Added to each vehicle at runtime by HVW_WorldInitializer.
    /// Does NOT touch WheelColliders, Rigidbody, or existing vehicle logic.
    /// </summary>
    public class HVW_VehicleLights : MonoBehaviour
    {
        [Header("Headlights")]
        public Light leftHeadlight;
        public Light rightHeadlight;

        [Header("Brake Lights")]
        public Renderer leftBrakeRenderer;
        public Renderer rightBrakeRenderer;
        public Color brakeEmissiveOn  = new Color(2.5f, 0.1f, 0.05f); // HDR red
        public Color brakeEmissiveOff = Color.black;

        [Header("Wheel Dust")]
        public ParticleSystem[] wheelDustSystems;

        private VehicleController _ctrl;
        private bool _brakeLit;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _ctrl = GetComponent<VehicleController>()
                 ?? GetComponentInParent<VehicleController>();
        }

        private void Start()
        {
            // Auto-build lights if none assigned in inspector
            if (leftHeadlight  == null) leftHeadlight  = CreateHeadlight("HL_Left",  new Vector3(-0.4f, 0.7f, 1.8f));
            if (rightHeadlight == null) rightHeadlight = CreateHeadlight("HL_Right", new Vector3( 0.4f, 0.7f, 1.8f));

            // Turn headlights on (they're always on in-game for visibility)
            if (leftHeadlight)  leftHeadlight.enabled  = true;
            if (rightHeadlight) rightHeadlight.enabled = true;

            // Auto-find brake light renderers by name
            if (leftBrakeRenderer  == null) leftBrakeRenderer  = FindChildRenderer("BrakeLight_L");
            if (rightBrakeRenderer == null) rightBrakeRenderer = FindChildRenderer("BrakeLight_R");

            // Build wheel dust if wheel dust systems are not assigned
            if (wheelDustSystems == null || wheelDustSystems.Length == 0)
                wheelDustSystems = BuildWheelDust();

            SetBrakeLights(false);
        }

        private void Update()
        {
            if (_ctrl == null) return;

            // Brake light: active when braking (negative throttle or space)
            bool braking = _ctrl.IsBraking;
            if (braking != _brakeLit)
            {
                SetBrakeLights(braking);
                _brakeLit = braking;
            }

            // Wheel dust: active when moving on ground
            bool moving = _ctrl.rb != null && _ctrl.rb.linearVelocity.magnitude > 0.5f;
            foreach (var ps in wheelDustSystems)
            {
                if (ps == null) continue;
                if (moving && !ps.isPlaying) ps.Play();
                else if (!moving && ps.isPlaying) ps.Stop();
            }
        }

        private void SetBrakeLights(bool on)
        {
            SetEmissive(leftBrakeRenderer,  on ? brakeEmissiveOn : brakeEmissiveOff);
            SetEmissive(rightBrakeRenderer, on ? brakeEmissiveOn : brakeEmissiveOff);
        }

        private static void SetEmissive(Renderer r, Color col)
        {
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColor, col);
            r.SetPropertyBlock(mpb);
        }

        private Light CreateHeadlight(string childName, Vector3 localPos)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(10f, 0f, 0f); // slightly downward
            var l = go.AddComponent<Light>();
            l.type      = LightType.Spot;
            l.color     = new Color(1.0f, 0.98f, 0.90f); // warm white
            l.intensity = 2.0f;
            l.range     = 25f;
            l.spotAngle = 55f;
            l.shadows   = LightShadows.None; // mobile perf
            return l;
        }

        private Renderer FindChildRenderer(string childName)
        {
            var t = transform.Find(childName);
            if (t == null) t = transform.Find($"Model/{childName}");
            if (t == null) t = transform.Find($"ImportedModel/{childName}");
            return t?.GetComponent<Renderer>();
        }

        private ParticleSystem[] BuildWheelDust()
        {
            // Find WheelCollider positions and add a small dust particle system above each
            var wcs = GetComponentsInChildren<UnityEngine.WheelCollider>();
            if (wcs == null || wcs.Length == 0) return new ParticleSystem[0];

            var list = new System.Collections.Generic.List<ParticleSystem>();
            foreach (var wc in wcs)
            {
                var go  = new GameObject($"WheelDust_{wc.name}");
                go.transform.SetParent(wc.transform, false);
                go.transform.localPosition = Vector3.zero;

                var ps  = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime  = 0.6f;
                main.startSpeed     = 1.5f;
                main.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startColor     = new Color(0.72f, 0.62f, 0.48f, 0.5f); // dusty tan
                main.maxParticles   = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.loop = true;
                main.playOnAwake = false;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 15f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.3f;

                // URP particle shader
                var mr = go.GetComponent<ParticleSystemRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                if (mat != null) mr.material = mat;

                ps.Stop();
                list.Add(ps);
            }
            return list.ToArray();
        }
    }
}
