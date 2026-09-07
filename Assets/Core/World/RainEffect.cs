using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// Rain over the opening sequence: a code-built particle sheet that follows the Centurion, with
    /// the light pulled down and the fog drawn in so the morning reads as the grey, sodden thing it
    /// was. No assets; nothing to set up in the scene.
    /// </summary>
    /// <remarks>
    /// The day/night cycle re-lights the scene every Update; this runs in LateUpdate and dims what it
    /// left, so the weather sits on top of the hour rather than fighting it.
    /// </remarks>
    public sealed class RainEffect : MonoBehaviour
    {
        private const float SheetHeight = 24f;

        private ParticleSystem _system;
        private Transform _follow;
        private Light _sun;
        private float _sunDimming = 0.55f;

        /// <param name="sunDimming">Multiplier on the sun while it rains: the opening's sodden grey
        /// at 0.55, the title's bright wet morning nearer 0.8.</param>
        public static RainEffect Create(Transform parent, Transform follow, float sunDimming = 0.55f)
        {
            var rain = new GameObject("Rain").AddComponent<RainEffect>();
            rain.transform.SetParent(parent, false);
            rain._follow = follow;
            rain._sunDimming = Mathf.Clamp01(sunDimming);
            rain.Build();
            return rain;
        }

        private void Build()
        {
            _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.5f;
            main.startSpeed = 22f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new Color(0.72f, 0.78f, 0.86f, 0.38f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 6000;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = 2200f;

            // A flat sheet high above the ground, emitting straight down.
            ParticleSystem.ShapeModule shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 70f, 1f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 7f;
            renderer.velocityScale = 0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = FxMaterials.VertexTinted();

            _sun = RenderSettings.sun;

            // A wetter, closer fog than the battle's dread haze: the wood ends thirty metres out.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 150f;

            _system.Play();
        }

        private void Update()
        {
            if (_follow == null) return;
            transform.position = _follow.position + Vector3.up * SheetHeight;
        }

        private void LateUpdate()
        {
            if (_sun == null) _sun = RenderSettings.sun;
            if (_sun != null) _sun.intensity *= _sunDimming;

            Color grey = new Color(0.36f, 0.39f, 0.41f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, grey, 0.6f);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, grey, 0.35f);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, grey * 0.8f, 0.35f);
        }
    }
}
