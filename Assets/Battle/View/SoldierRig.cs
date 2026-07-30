using Century.Battle.Model;
using Century.Core.World;
using System.Collections.Generic;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The modular soldier figure that replaces the placeholder capsule: helmeted head, banded
    /// cuirass over a tunic, legs that actually stride, officer crests you can read down a line —
    /// a legionary silhouette at the concept art's framing, still built entirely from code.
    /// Germans get hair, beards and furs in varied colours instead, standing a shade taller.
    /// </summary>
    /// <remarks>
    /// Pure dressing: no colliders, no sim contact. The existing <see cref="CombatantGear"/> keeps
    /// posing weapon and shield beside it; the rig only owns the body. Legs swing and the torso
    /// bobs from <see cref="Animate"/>, driven by whoever moves the transform.
    /// </remarks>
    public sealed class SoldierRig : MonoBehaviour
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        // The Roman palette: uniform men of a uniform army.
        private static readonly Color RomanTunic = new Color(0.48f, 0.13f, 0.11f);
        private static readonly Color RomanIron = new Color(0.42f, 0.44f, 0.48f);
        private static readonly Color RomanBronze = new Color(0.45f, 0.36f, 0.22f);
        private static readonly Color Skin = new Color(0.68f, 0.54f, 0.42f);
        private static readonly Color CrestRed = new Color(0.62f, 0.14f, 0.10f);
        private static readonly Color CrestWhite = new Color(0.78f, 0.76f, 0.70f);

        // The German wardrobe: no two warriors dressed alike.
        private static readonly Color[] GermanTunics =
        {
            new Color(0.30f, 0.26f, 0.18f), new Color(0.24f, 0.28f, 0.20f),
            new Color(0.34f, 0.24f, 0.16f), new Color(0.26f, 0.22f, 0.24f)
        };
        private static readonly Color[] GermanHair =
        {
            new Color(0.62f, 0.48f, 0.28f), new Color(0.38f, 0.22f, 0.12f),
            new Color(0.52f, 0.28f, 0.14f), new Color(0.20f, 0.16f, 0.12f)
        };
        private static readonly Color GermanFur = new Color(0.33f, 0.27f, 0.20f);

        /// <summary>Whole-figure scale: the first build stood noticeably small against the gear.</summary>
        private const float FigureScale = 1.24f;

        private Transform _legLeft, _legRight, _figure;
        private float _figureBaseY;
        private float _phase;
        private float _swing;
        private float _twist, _lean, _flinch;

        /// <summary>Body english from the melee animator: yaw wind-up/follow-through and forward
        /// lean, degrees. Set every frame by <see cref="CombatantGear.Pose"/>.</summary>
        public void SetCombatPose(float twistDegrees, float leanDegrees)
        {
            _twist = twistDegrees;
            _lean = leanDegrees;
        }

        /// <summary>A blow landed: the figure snaps aside for a beat.</summary>
        public void NotifyHit() => _flinch = 1f;

        /// <summary>The renderer wound tint targets (the tunic — blood shows on cloth).</summary>
        public Renderer TintTarget { get; private set; }

        /// <summary>The colour that renderer starts at, for the tint lerp.</summary>
        public Color TintBase { get; private set; }

        /// <summary>
        /// Builds a figure under <paramref name="bodyRoot"/> with its chest at local height
        /// <paramref name="chestHeight"/> (measured off whatever placeholder it replaces, so the
        /// rig stands on the same ground whatever the prefab's layout was).
        /// </summary>
        public static SoldierRig Build(
            Transform bodyRoot, float chestHeight, bool roman, OfficerRole role, bool leader, int variant)
        {
            var rig = new GameObject("Rig").AddComponent<SoldierRig>();
            rig.transform.SetParent(bodyRoot, false);
            rig.transform.localPosition = new Vector3(0f, chestHeight, 0f);

            rig._figure = new GameObject("Figure").transform;
            rig._figure.SetParent(rig.transform, false);

            // Scale up around the chest anchor, then lift so the feet stay on the same ground.
            float scale = FigureScale * (roman ? 1f : 1.06f);   // the Germans stood taller
            rig._figure.localScale = Vector3.one * scale;
            rig._figureBaseY = (scale - 1f) * 0.95f;
            rig._figure.localPosition = new Vector3(0f, rig._figureBaseY, 0f);

            rig.Assemble(roman, role, leader, variant);
            return rig;
        }

        private void Assemble(bool roman, OfficerRole role, bool leader, int variant)
        {
            Color tunic = roman ? RomanTunic : GermanTunics[Mathf.Abs(variant) % GermanTunics.Length];
            Color torso = roman ? RomanIron : GermanFur;

            // Tunic skirt — and the wound-tint target: blood shows on cloth.
            TintTarget = Part(PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f),
                new Vector3(0.42f, 0.30f, 0.30f), tunic);
            TintBase = tunic;

            // Cuirass (banded iron) or fur wrap.
            Part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.42f, 0.31f), torso);
            Part(PrimitiveType.Cube, new Vector3(-0.28f, 0.22f, 0f), new Vector3(0.14f, 0.12f, 0.26f), torso);
            Part(PrimitiveType.Cube, new Vector3(0.28f, 0.22f, 0f), new Vector3(0.14f, 0.12f, 0.26f), torso);

            // Arms: enough silhouette that the floating gear reads as HELD.
            Part(PrimitiveType.Capsule, new Vector3(-0.30f, -0.05f, 0.05f),
                new Vector3(0.10f, 0.24f, 0.10f), roman ? tunic : Skin);
            Part(PrimitiveType.Capsule, new Vector3(0.30f, -0.05f, 0.05f),
                new Vector3(0.10f, 0.24f, 0.10f), roman ? tunic : Skin);

            // Head.
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.44f, 0f), new Vector3(0.24f, 0.26f, 0.24f), Skin);

            if (roman)
            {
                // Galea: bowl and brim; iron for the ranks, bronze for men with a post.
                Color metal = role != OfficerRole.None ? RomanBronze : RomanIron;
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f), new Vector3(0.28f, 0.22f, 0.28f), metal);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.47f, -0.02f), new Vector3(0.32f, 0.02f, 0.34f), metal);

                BuildCrest(role, leader);
            }
            else
            {
                // Hair, and a beard on most of them.
                Color hair = GermanHair[Mathf.Abs(variant / 3) % GermanHair.Length];
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.54f, -0.02f), new Vector3(0.27f, 0.17f, 0.27f), hair);
                if (variant % 5 != 0)
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.36f, 0.10f), new Vector3(0.16f, 0.14f, 0.08f), hair);
            }

            // Legs, hung from pivots so they can stride.
            _legLeft = BuildLeg(-0.11f, roman);
            _legRight = BuildLeg(0.11f, roman);
        }

        /// <summary>Rank at a glance, down the whole line: the Centurion's transverse crest, the
        /// Optio's white plume, dark for the Signifer, a short red brush for a Decanus.</summary>
        private void BuildCrest(OfficerRole role, bool leader)
        {
            switch (role)
            {
                case OfficerRole.Centurion:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.68f, 0f), new Vector3(0.5f, 0.14f, 0.08f), CrestRed);
                    return;
                case OfficerRole.Optio:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.66f, 0f), new Vector3(0.07f, 0.12f, 0.38f), CrestWhite);
                    return;
                case OfficerRole.Signifer:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.66f, 0f), new Vector3(0.07f, 0.12f, 0.38f),
                        new Color(0.16f, 0.13f, 0.10f));
                    return;
                default:
                    if (leader)
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.64f, 0.02f), new Vector3(0.06f, 0.09f, 0.26f), CrestRed);
                    return;
            }
        }

        private Transform BuildLeg(float x, bool roman)
        {
            var pivot = new GameObject("Leg").transform;
            pivot.SetParent(_figure, false);
            pivot.localPosition = new Vector3(x, -0.42f, 0f);

            GameObject shin = MakePart(PrimitiveType.Capsule, new Vector3(0f, -0.26f, 0f),
                new Vector3(0.13f, 0.27f, 0.13f), roman ? Skin : new Color(0.24f, 0.20f, 0.16f));
            shin.transform.SetParent(pivot, false);

            return pivot;
        }

        // --- Motion ------------------------------------------------------------------------------

        /// <summary>Strides the legs, bobs the figure with movement, and composes the combat body
        /// english (twist/lean from the melee animator, plus the flinch of a taken blow).</summary>
        public void Animate(float speed, float dt)
        {
            float targetSwing = Mathf.Clamp01(speed / 3.5f);
            _swing = Mathf.MoveTowards(_swing, targetSwing, dt * 4f);
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 4.5f);

            if (_swing >= 0.02f) _phase += speed * dt * 3.4f;
            float stride = _swing < 0.02f ? 0f : Mathf.Sin(_phase) * 28f * _swing;

            if (_legLeft != null) _legLeft.localRotation = Quaternion.Euler(stride, 0f, 0f);
            if (_legRight != null) _legRight.localRotation = Quaternion.Euler(-stride, 0f, 0f);

            if (_figure == null) return;

            float bob = _swing < 0.02f ? 0f : Mathf.Abs(Mathf.Sin(_phase)) * 0.035f * _swing;
            _figure.localPosition = new Vector3(0f, _figureBaseY + bob, 0f);

            // Flinch is a sharp jolt that rings down: strongest the instant the blow lands.
            float jolt = Mathf.Sin(_flinch * Mathf.PI) * -13f;
            _figure.localRotation = Quaternion.Euler(_lean, _twist + jolt, jolt * 0.4f);
        }

        // --- Parts -------------------------------------------------------------------------------

        private Renderer Part(PrimitiveType type, Vector3 position, Vector3 scale, Color colour) =>
            MakePart(type, position, scale, colour, _figure).GetComponent<Renderer>();

        private GameObject MakePart(PrimitiveType type, Vector3 position, Vector3 scale, Color colour)
            => MakePart(type, position, scale, colour, null);

        private static GameObject MakePart(
            PrimitiveType type, Vector3 position, Vector3 scale, Color colour, Transform parent)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = "Part";
            if (parent != null) part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(colour);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = false;
            return part;
        }

        private static Material MaterialFor(Color colour)
        {
            if (Materials.TryGetValue(colour, out Material cached) && cached != null) return cached;
            Material material = FxMaterials.Lit(colour);   // bodies must sit IN the light
            Materials[colour] = material;
            return material;
        }
    }
}
