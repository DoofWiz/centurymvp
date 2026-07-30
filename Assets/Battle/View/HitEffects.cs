using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The read of a landed blow: a small dark-red burst at the wound, gone in half a second. With a
    /// hundred men trading blows, THIS is how the player tells a hit from a miss at a glance — the
    /// tint flash alone was invisible in the scrum.
    /// </summary>
    /// <remarks>
    /// One pooled ParticleSystem, entirely code-generated, emitting bursts at world positions via
    /// <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> — a single renderer for
    /// every wound on the field, no per-hit instantiation. Views call the static
    /// <see cref="Spawn"/>; the instance is created by BattleBootstrap and cleans itself up with
    /// the scene.
    /// </remarks>
    public sealed class HitEffects : MonoBehaviour
    {
        private static HitEffects _instance;

        private ParticleSystem _system;

        public static HitEffects Create(Transform parent)
        {
            var effects = new GameObject("HitEffects").AddComponent<HitEffects>();
            effects.transform.SetParent(parent, false);
            effects.Build();
            _instance = effects;
            return effects;
        }

        private static readonly Color BloodColour = new Color(0.55f, 0.07f, 0.05f);
        private static readonly Color BlockColour = new Color(0.75f, 0.72f, 0.62f);
        private static readonly Color GuardBreakColour = new Color(0.95f, 0.72f, 0.25f);

        /// <summary>A blow has landed at <paramref name="position"/>. Safe to call from any view.</summary>
        public static void Spawn(Vector3 position) => Emit(position, BloodColour, 7);

        /// <summary>A shield turned the blow: a small dry puff off the board. The read that the
        /// wall is HOLDING — the counterpart to blood.</summary>
        public static void SpawnBlock(Vector3 position) => Emit(position, BlockColour, 4);

        /// <summary>A tired guard smashed open: an amber flash. THE tactical read of the melee —
        /// where this sparks along a line is where it is about to crack.</summary>
        public static void SpawnGuardBreak(Vector3 position) => Emit(position, GuardBreakColour, 12);

        /// <summary>A man goes down: a heavier burst than a wound, so a death reads differently
        /// from a hit even at full camera height.</summary>
        public static void SpawnDeath(Vector3 position) => Emit(position, BloodColour, 18);

        private static void Emit(Vector3 position, Color colour, int count)
        {
            if (_instance == null || _instance._system == null) return;

            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = colour
            };
            _instance._system.Emit(emit, count);
        }

        private void Build()
        {
            _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.48f, 0.05f, 0.04f), new Color(0.66f, 0.10f, 0.07f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 512;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = _system.emission;
            emission.enabled = false;   // bursts come only through Emit()

            ParticleSystem.ShapeModule shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            // Droplets shrink as they fall rather than popping out of existence.
            ParticleSystem.SizeOverLifetimeModule size = _system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = BurstMaterial();

            _system.Play();
        }

        private static Material BurstMaterial()
        {
            // Particles want their own shader family first; the shared helpers cover the rest.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader);
            Century.Core.World.FxMaterials.SetColour(material, new Color(0.55f, 0.07f, 0.05f));
            return material;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
