using Century.Battle.Model;
using Century.Core.World;
using System.Collections.Generic;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The soldier figure, second build: a man of real proportions, 1.75 m to the crown of his
    /// helmet, with a head, a neck, a banded cuirass over a tunic, two-segment arms that actually
    /// reach for the weapon and the shield, striding legs with knees, and boots. Still built
    /// entirely from primitives, but no longer a box on legs; the gear it holds is sized to him.
    /// </summary>
    /// <remarks>
    /// Pivot at the FEET: the figure stands with its soles at the body root's origin, which is
    /// where the prefab controllers put the ground. <see cref="CombatantGear"/> poses weapon and
    /// shield in body-root space and hands their positions back through <see cref="SetHands"/>;
    /// a two-bone solve then aims each arm at what it is holding, so blades and boards read as
    /// HELD rather than floating beside the man.
    /// </remarks>
    public sealed class SoldierRig : MonoBehaviour
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        // The Roman palette: uniform men of a uniform army.
        private static readonly Color RomanTunic = new Color(0.50f, 0.13f, 0.11f);
        private static readonly Color RomanIron = new Color(0.44f, 0.46f, 0.50f);
        private static readonly Color RomanBronze = new Color(0.52f, 0.40f, 0.22f);
        private static readonly Color Leather = new Color(0.30f, 0.21f, 0.13f);
        private static readonly Color Skin = new Color(0.70f, 0.55f, 0.42f);
        private static readonly Color CrestRed = new Color(0.66f, 0.14f, 0.10f);
        private static readonly Color CrestWhite = new Color(0.80f, 0.78f, 0.72f);
        private static readonly Color CrestDark = new Color(0.16f, 0.13f, 0.10f);

        // The German wardrobe: no two warriors dressed alike.
        private static readonly Color[] GermanTunics =
        {
            new Color(0.32f, 0.27f, 0.18f), new Color(0.24f, 0.29f, 0.20f),
            new Color(0.36f, 0.24f, 0.16f), new Color(0.27f, 0.22f, 0.25f)
        };
        private static readonly Color[] GermanHair =
        {
            new Color(0.64f, 0.50f, 0.28f), new Color(0.38f, 0.22f, 0.12f),
            new Color(0.54f, 0.29f, 0.14f), new Color(0.20f, 0.16f, 0.12f)
        };
        private static readonly Color GermanFur = new Color(0.35f, 0.28f, 0.20f);
        private static readonly Color GermanTrousers = new Color(0.26f, 0.22f, 0.17f);

        // --- Proportions (metres from the soles, Roman; Germans are scaled up a shade) --------
        private const float HipHeight = 0.92f;
        private const float ThighLength = 0.42f;
        private const float ShinLength = 0.40f;
        private const float ShoulderHeight = 1.42f;
        private const float ShoulderHalfWidth = 0.245f;
        private const float UpperArmLength = 0.30f;
        private const float ForearmLength = 0.28f;

        private Transform _figure;
        private Transform _legLeft, _legRight, _kneeLeft, _kneeRight;
        private Transform _armLeft, _armRight, _elbowLeft, _elbowRight;
        private bool _armLeftHeld, _armRightHeld;
        private float _figureScale = 1f;
        private float _phase, _swing, _stride;
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

        /// <summary>The renderer wound tint targets (the tunic: blood shows on cloth).</summary>
        public Renderer TintTarget { get; private set; }

        /// <summary>The colour that renderer starts at, for the tint lerp.</summary>
        public Color TintBase { get; private set; }

        /// <summary>
        /// Builds a figure under <paramref name="bodyRoot"/> with its soles at the root's origin.
        /// <paramref name="chestHeight"/> is accepted for the callers that measured a placeholder
        /// and no longer needed: the figure carries its own proportions.
        /// </summary>
        public static SoldierRig Build(
            Transform bodyRoot, float chestHeight, bool roman, OfficerRole role, bool leader, int variant)
        {
            var rig = new GameObject("Rig").AddComponent<SoldierRig>();
            rig.transform.SetParent(bodyRoot, false);
            rig.transform.localPosition = Vector3.zero;

            rig._figure = new GameObject("Figure").transform;
            rig._figure.SetParent(rig.transform, false);

            // One height for every man: a taller enemy read as a bigger enemy, which he is not.
            rig._figureScale = 1f;
            rig._figure.localScale = Vector3.one * rig._figureScale;

            rig.Assemble(roman, role, leader, variant);
            return rig;
        }

        private void Assemble(bool roman, OfficerRole role, bool leader, int variant)
        {
            Color tunic = roman ? RomanTunic : GermanTunics[Mathf.Abs(variant) % GermanTunics.Length];
            Color legCloth = roman ? Skin : GermanTrousers;

            // Tunic: hips to chest, and the wound-tint target.
            TintTarget = Part(PrimitiveType.Cube, new Vector3(0f, 0.98f, 0f), new Vector3(0.38f, 0.40f, 0.24f), tunic);
            TintBase = tunic;

            if (roman)
            {
                // Lorica segmentata: banded iron over the chest, shoulder guards layered on top.
                Part(PrimitiveType.Cube, new Vector3(0f, 1.30f, 0f), new Vector3(0.40f, 0.30f, 0.25f), RomanIron);
                for (int i = 0; i < 4; i++)
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.14f + i * 0.08f, 0f),
                        new Vector3(0.41f + i * 0.008f, 0.07f, 0.27f + i * 0.004f), RomanIron);
                Part(PrimitiveType.Cube, new Vector3(-0.235f, 1.45f, 0f), new Vector3(0.15f, 0.06f, 0.25f), RomanIron);
                Part(PrimitiveType.Cube, new Vector3(0.235f, 1.45f, 0f), new Vector3(0.15f, 0.06f, 0.25f), RomanIron);
                Part(PrimitiveType.Cube, new Vector3(-0.265f, 1.40f, 0f), new Vector3(0.12f, 0.05f, 0.23f), RomanIron);
                Part(PrimitiveType.Cube, new Vector3(0.265f, 1.40f, 0f), new Vector3(0.12f, 0.05f, 0.23f), RomanIron);

                // Pteruges: leather strips over the thighs.
                for (int i = -2; i <= 2; i++)
                    Part(PrimitiveType.Cube, new Vector3(i * 0.075f, 0.76f, 0.12f), new Vector3(0.06f, 0.14f, 0.02f), Leather);

                // Belt.
                Part(PrimitiveType.Cube, new Vector3(0f, 1.02f, 0f), new Vector3(0.40f, 0.05f, 0.26f), Leather);
            }
            else
            {
                // A fur wrap over the tunic, tied with a cord.
                Part(PrimitiveType.Cube, new Vector3(0f, 1.24f, 0f), new Vector3(0.42f, 0.40f, 0.28f), GermanFur);
                Part(PrimitiveType.Cube, new Vector3(0f, 1.03f, 0f), new Vector3(0.40f, 0.04f, 0.26f), Leather);
            }

            // Neck and head.
            Part(PrimitiveType.Cylinder, new Vector3(0f, 1.52f, 0f), new Vector3(0.11f, 0.04f, 0.11f), Skin);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.63f, 0f), new Vector3(0.20f, 0.22f, 0.21f), Skin);

            if (roman)
            {
                // Galea: bowl, neck-guard and cheek pieces; bronze for men with a post.
                Color metal = role != OfficerRole.None ? RomanBronze : RomanIron;
                Part(PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0f), new Vector3(0.24f, 0.20f, 0.25f), metal);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 1.615f, -0.03f), new Vector3(0.27f, 0.012f, 0.30f), metal);
                Part(PrimitiveType.Cube, new Vector3(0f, 1.665f, 0.115f), new Vector3(0.22f, 0.04f, 0.03f), metal);
                Part(PrimitiveType.Cube, new Vector3(-0.105f, 1.58f, 0.03f), new Vector3(0.03f, 0.10f, 0.09f), metal);
                Part(PrimitiveType.Cube, new Vector3(0.105f, 1.58f, 0.03f), new Vector3(0.03f, 0.10f, 0.09f), metal);

                BuildCrest(role, leader);
            }
            else
            {
                // Hair to the shoulders, and a beard on most of them.
                Color hair = GermanHair[Mathf.Abs(variant / 3) % GermanHair.Length];
                Part(PrimitiveType.Sphere, new Vector3(0f, 1.70f, -0.02f), new Vector3(0.24f, 0.16f, 0.25f), hair);
                Part(PrimitiveType.Cube, new Vector3(0f, 1.58f, -0.08f), new Vector3(0.20f, 0.18f, 0.08f), hair);
                if (variant % 5 != 0)
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.56f, 0.09f), new Vector3(0.14f, 0.12f, 0.06f), hair);
            }

            // Limbs, hung from pivots so they stride and reach.
            _legLeft = BuildLeg(-0.10f, legCloth, roman, out _kneeLeft);
            _legRight = BuildLeg(0.10f, legCloth, roman, out _kneeRight);
            _armLeft = BuildArm(-ShoulderHalfWidth, roman ? tunic : Skin, out _elbowLeft);
            _armRight = BuildArm(ShoulderHalfWidth, roman ? tunic : Skin, out _elbowRight);
        }

        /// <summary>Rank at a glance, down the whole line: the Centurion's transverse crest, the
        /// Optio's white plume, dark for the Signifer, a short red brush for a Decanus.</summary>
        private void BuildCrest(OfficerRole role, bool leader)
        {
            switch (role)
            {
                case OfficerRole.Centurion:
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.78f, 0f), new Vector3(0.30f, 0.03f, 0.05f), RomanBronze);
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.87f, 0f), new Vector3(0.40f, 0.15f, 0.07f), CrestRed);
                    return;
                case OfficerRole.Optio:
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.85f, -0.02f), new Vector3(0.06f, 0.13f, 0.30f), CrestWhite);
                    return;
                case OfficerRole.Signifer:
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.85f, -0.02f), new Vector3(0.06f, 0.13f, 0.30f), CrestDark);
                    return;
                default:
                    if (leader)
                        Part(PrimitiveType.Cube, new Vector3(0f, 1.82f, 0f), new Vector3(0.05f, 0.09f, 0.22f), CrestRed);
                    return;
            }
        }

        private Transform BuildLeg(float x, Color cloth, bool roman, out Transform knee)
        {
            var hip = new GameObject("Leg").transform;
            hip.SetParent(_figure, false);
            hip.localPosition = new Vector3(x, HipHeight, 0f);

            // Thigh: a capsule hanging from the hip.
            GameObject thigh = MakePart(PrimitiveType.Capsule, new Vector3(0f, -ThighLength * 0.5f, 0f),
                new Vector3(0.17f, ThighLength * 0.5f + 0.02f, 0.17f), cloth);
            thigh.transform.SetParent(hip, false);

            knee = new GameObject("Knee").transform;
            knee.SetParent(hip, false);
            knee.localPosition = new Vector3(0f, -ThighLength, 0f);

            GameObject shin = MakePart(PrimitiveType.Capsule, new Vector3(0f, -ShinLength * 0.5f, 0f),
                new Vector3(0.14f, ShinLength * 0.5f + 0.02f, 0.14f), roman ? Skin : cloth);
            shin.transform.SetParent(knee, false);

            // Caligae: a boot with a little toe forward.
            GameObject boot = MakePart(PrimitiveType.Cube, new Vector3(0f, -ShinLength + 0.045f, 0.04f),
                new Vector3(0.12f, 0.08f, 0.25f), Leather);
            boot.transform.SetParent(knee, false);

            return hip;
        }

        private Transform BuildArm(float x, Color sleeve, out Transform elbow)
        {
            var shoulder = new GameObject("Arm").transform;
            shoulder.SetParent(_figure, false);
            shoulder.localPosition = new Vector3(x, ShoulderHeight, 0f);

            GameObject upper = MakePart(PrimitiveType.Capsule, new Vector3(0f, -UpperArmLength * 0.5f, 0f),
                new Vector3(0.12f, UpperArmLength * 0.5f + 0.02f, 0.12f), sleeve);
            upper.transform.SetParent(shoulder, false);

            elbow = new GameObject("Elbow").transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(0f, -UpperArmLength, 0f);

            GameObject fore = MakePart(PrimitiveType.Capsule, new Vector3(0f, -ForearmLength * 0.5f, 0f),
                new Vector3(0.105f, ForearmLength * 0.5f + 0.015f, 0.105f), Skin);
            fore.transform.SetParent(elbow, false);

            GameObject hand = MakePart(PrimitiveType.Sphere, new Vector3(0f, -ForearmLength - 0.02f, 0f),
                new Vector3(0.09f, 0.09f, 0.09f), Skin);
            hand.transform.SetParent(elbow, false);

            // At rest the arms hang a little forward and out from the body.
            shoulder.localRotation = Quaternion.Euler(-10f, 0f, x < 0f ? 9f : -9f);
            return shoulder;
        }

        // --- Motion ------------------------------------------------------------------------------

        /// <summary>Strides the legs, bobs the figure with movement, and composes the combat body
        /// english (twist/lean from the melee animator, plus the flinch of a taken blow).</summary>
        public void Animate(float speed, float dt)
        {
            float targetSwing = Mathf.Clamp01(speed / 3.5f);
            _swing = Mathf.MoveTowards(_swing, targetSwing, dt * 4f);
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 3.6f);

            if (_swing >= 0.02f) _phase += speed * dt * 3.4f;
            _stride = _swing < 0.02f ? 0f : Mathf.Sin(_phase) * 30f * _swing;

            // Legs swing from the hip; the trailing knee bends as the foot lifts.
            if (_legLeft != null) _legLeft.localRotation = Quaternion.Euler(_stride, 0f, 0f);
            if (_legRight != null) _legRight.localRotation = Quaternion.Euler(-_stride, 0f, 0f);
            if (_kneeLeft != null) _kneeLeft.localRotation = Quaternion.Euler(Mathf.Max(0f, _stride) * 0.9f, 0f, 0f);
            if (_kneeRight != null) _kneeRight.localRotation = Quaternion.Euler(Mathf.Max(0f, -_stride) * 0.9f, 0f, 0f);

            // A free arm swings against its leg; a held one is aimed by SetHands after this.
            if (!_armLeftHeld && _armLeft != null)
                _armLeft.localRotation = Quaternion.Euler(-10f - _stride * 0.7f, 0f, 9f);
            if (!_armRightHeld && _armRight != null)
                _armRight.localRotation = Quaternion.Euler(-10f + _stride * 0.7f, 0f, -9f);
            if (!_armLeftHeld && _elbowLeft != null) _elbowLeft.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            if (!_armRightHeld && _elbowRight != null) _elbowRight.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            if (_figure == null) return;

            float bob = _swing < 0.02f ? 0f : Mathf.Abs(Mathf.Sin(_phase)) * 0.035f * _swing;

            // The flinch: a blow knocks the whole man back a step and doubles him away from it,
            // hardest the instant it lands and gone in a third of a second. It has to be read
            // at battle-camera height, so it is a lurch, not a shiver.
            float hit = Mathf.Sin(_flinch * Mathf.PI);
            float jolt = hit * -14f;
            _figure.localPosition = new Vector3(0f, bob - hit * 0.05f, -hit * 0.26f);
            _figure.localRotation = Quaternion.Euler(_lean - hit * 18f, _twist + jolt, jolt * 0.4f);
        }

        /// <summary>
        /// Aims the arms at what the hands hold. Positions are in BODY-ROOT space (the gear's
        /// frame, pivot at the feet); null leaves that arm swinging free. Call after
        /// <see cref="Animate"/>, which resets free arms, and after the gear has been posed.
        /// </summary>
        public void SetHands(Vector3? rightHand, Vector3? leftHand)
        {
            _armRightHeld = rightHand.HasValue;
            _armLeftHeld = leftHand.HasValue;

            Transform bodyRoot = transform.parent != null ? transform.parent : transform;
            if (rightHand.HasValue) Reach(_armRight, _elbowRight, bodyRoot.TransformPoint(rightHand.Value), Vector3.right);
            if (leftHand.HasValue) Reach(_armLeft, _elbowLeft, bodyRoot.TransformPoint(leftHand.Value), Vector3.left);
        }

        /// <summary>Two-bone solve in world space: the elbow bends out to the man's side and back,
        /// the way an elbow does, and the segments are rotationally symmetric so no twist is needed.</summary>
        private void Reach(Transform shoulder, Transform elbow, Vector3 targetWorld, Vector3 outward)
        {
            if (shoulder == null || elbow == null) return;

            float upper = UpperArmLength * _figureScale;
            float fore = (ForearmLength + 0.02f) * _figureScale;

            Vector3 s = shoulder.position;
            Vector3 toTarget = targetWorld - s;
            float d = Mathf.Clamp(toTarget.magnitude, 0.05f, upper + fore - 0.01f);
            Vector3 dir = toTarget.normalized;

            // Law of cosines: how far along the shoulder-target line the elbow projects, and how far off it.
            float a = (upper * upper - fore * fore + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, upper * upper - a * a));

            Vector3 hint = (transform.TransformDirection(outward) * 0.7f - transform.forward * 0.5f - Vector3.up * 0.3f).normalized;
            Vector3 perp = hint - Vector3.Dot(hint, dir) * dir;
            if (perp.sqrMagnitude < 0.0001f) perp = Vector3.Cross(dir, Vector3.up);
            perp.Normalize();

            Vector3 e = s + dir * a + perp * h;

            shoulder.rotation = Quaternion.FromToRotation(Vector3.down, (e - s).normalized);
            elbow.rotation = Quaternion.FromToRotation(Vector3.down, (targetWorld - e).normalized);
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
