using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The visible weapon and shield on a fighter, generated in code and posed each frame to match his
    /// melee stance: at guard at rest, drawn back on the wind-up, thrown forward on the strike, and
    /// the shield swung across the front when raised. Purely cosmetic: the sim owns the timing and the
    /// hit; this makes the fight read as blades and boards rather than sliding capsules.
    /// </summary>
    /// <remarks>
    /// Second build, sized to the 1.75 m figure: a gladius is a short sword (half a metre of blade
    /// behind a hilt), a scutum a curved board a little over a metre tall, a framea a two-metre
    /// spear held a third of the way up, the German board a broad oval. Every piece is hung under
    /// an unscaled root at the HAND, so the rig can aim its arms at the roots and the parts swing
    /// as one without the root's scale distorting them.
    /// </remarks>
    public sealed class CombatantGear
    {
        private readonly Transform _weapon;
        private readonly Transform _shield;   // null for a shield-less fighter
        private readonly WeaponClass _class;

        /// <param name="emphasise">The Centurion's own gear: brighter steel, so the player can read
        /// his blade against his body. Same size as anyone's; a bigger sword is not a better one.</param>
        public CombatantGear(Transform bodyRoot, WeaponClass weaponClass, bool hasShield, bool emphasise = false)
        {
            _class = weaponClass;

            _weapon = BuildWeapon(bodyRoot, weaponClass, emphasise);

            if (hasShield)
                _shield = BuildShield(bodyRoot, weaponClass, emphasise);

            Pose(new BattleCombatant { Weapon = weaponClass }, 1f); // snap to the rest pose
        }

        // --- Procedural swing animation ---------------------------------------------------------
        //
        // Not pose-lerps: a strike TRAVELS. The blade is driven parametrically along a real arc
        // (or a real thrust line) by the phase of the current stance, the body twists into and
        // through the blow, the shield recoils when it takes one, and a broken guard sags and
        // sways. All positions are body-root local with the pivot at the FEET; the weapon root
        // is the sword hand, the shield root the shield hand.

        private static readonly Vector3 Shoulder = new Vector3(0.245f, 1.42f, 0.0f);
        private static readonly Vector3 RestPos = new Vector3(0.33f, 0.92f, 0.16f);
        private static readonly Quaternion RestRot = Quaternion.Euler(18f, 0f, 0f);

        /// <summary>The shield hand sits just behind the boss.</summary>
        private static readonly Vector3 ShieldGrip = new Vector3(0f, 0f, -0.06f);

        private MeleeStance _seenStance = MeleeStance.Idle;
        private float _stanceStart;
        private float _blockRecoil;
        private float _twist, _lean;

        public void Pose(BattleCombatant man, float dt, SoldierRig rig = null)
        {
            if (man.Stance != _seenStance)
            {
                _seenStance = man.Stance;
                _stanceStart = Time.time;
            }

            float phase = StancePhase(man);
            float targetTwist = 0f, targetLean = 0f;

            if (man.IsStaggered)
            {
                // The guard is smashed open: the blade sags, the body sways on its feet.
                Smooth(_weapon, new Vector3(0.38f, 0.72f, 0.1f), Quaternion.Euler(55f, 10f, 0f), 8f, dt);
                targetTwist = Mathf.Sin(Time.time * 9f) * 9f;
                targetLean = -7f;
            }
            else
            {
                switch (man.Stance)
                {
                    case MeleeStance.Charging:
                        if (man.Form == AttackForm.Thrust)
                        {
                            // Coiling: the point draws back along the line it will travel.
                            Smooth(_weapon,
                                new Vector3(0.3f, 1.0f, Mathf.Lerp(0.1f, -0.28f, phase)),
                                Quaternion.identity, 10f, dt);
                            targetTwist = -12f * phase;
                        }
                        else
                        {
                            // Cocking: up and behind the shoulder, blade turned for the cut.
                            Smooth(_weapon,
                                Vector3.Lerp(RestPos, new Vector3(0.42f, 1.46f, -0.22f), phase),
                                Quaternion.Slerp(RestRot, Quaternion.Euler(-38f, 52f, 18f), phase),
                                10f, dt);
                            targetTwist = -20f * phase;
                            targetLean = -4f * phase;
                        }
                        break;

                    case MeleeStance.Striking:
                        if (man.Form == AttackForm.Thrust)
                        {
                            // The drive: point-first, fast out of the gate, dying at full extension.
                            float drive = EaseOutCubic(phase);
                            _weapon.localPosition = new Vector3(
                                Mathf.Lerp(0.3f, 0.18f, drive), 1.02f, Mathf.Lerp(-0.28f, 0.62f, drive));
                            _weapon.localRotation = Quaternion.identity;
                            targetTwist = Mathf.Lerp(-12f, 16f, drive);
                            targetLean = 8f * drive;
                        }
                        else
                        {
                            // The cut: a diagonal arc swept around the shoulder, high-right to
                            // low-left, fastest through the middle, driven directly so the blade
                            // genuinely TRAVELS through the space in front of the man. The hand
                            // rides an arm's length from the shoulder.
                            float sweep = EaseOutQuad(phase);
                            float yaw = Mathf.Lerp(62f, -58f, sweep);
                            float pitch = Mathf.Lerp(-32f, 14f, sweep);

                            Quaternion swing = Quaternion.Euler(pitch, yaw, 0f);
                            _weapon.localPosition = Shoulder + swing * (Vector3.forward * 0.56f);
                            _weapon.localRotation = swing;
                            targetTwist = Mathf.Lerp(-20f, 24f, sweep);
                            targetLean = 6f * sweep;
                        }
                        break;

                    default:   // Idle / Recovering: settle back to guard.
                        Smooth(_weapon, RestPos, RestRot,
                            man.Stance == MeleeStance.Recovering ? 9f : 6f, dt);
                        break;
                }
            }

            PoseShield(man, dt);

            // Body english rides along: wind into the blow, turn through it.
            float k = 1f - Mathf.Exp(-10f * dt);
            _twist = Mathf.Lerp(_twist, targetTwist, k);
            _lean = Mathf.Lerp(_lean, targetLean, k);
            rig?.SetCombatPose(_twist, _lean);

            // The arms follow what the hands hold.
            Vector3? shieldHand = _shield != null
                ? _shield.localPosition + _shield.localRotation * ShieldGrip
                : (Vector3?)null;
            rig?.SetHands(_weapon.localPosition, shieldHand);
        }

        private void PoseShield(BattleCombatant man, float dt)
        {
            if (_shield == null) return;

            if (man.WasBlockThisTick) _blockRecoil = 1f;
            _blockRecoil = Mathf.MoveTowards(_blockRecoil, 0f, dt * 5f);

            Vector3 pos;
            Quaternion rot;
            if (man.ShieldRaised)
            {
                // Braced across the front; a caught blow shoves the whole board back for a beat.
                pos = new Vector3(-0.08f, 1.08f, 0.48f - 0.12f * _blockRecoil);
                rot = Quaternion.Euler(-13f * _blockRecoil, 0f, 0f);
            }
            else
            {
                // Carried at the left side, edge forward, hanging from the hand.
                pos = new Vector3(-0.40f, 0.98f, 0.06f);
                rot = Quaternion.Euler(0f, 78f, 0f);
            }

            Smooth(_shield, pos, rot, _blockRecoil > 0.01f ? 22f : 11f, dt);
        }

        /// <summary>Where this stance is between its beginning and its end, 0..1. Durations mirror
        /// the profile's; a held player charge simply saturates at fully drawn.</summary>
        private float StancePhase(BattleCombatant man)
        {
            MeleeProfile profile = MeleeProfile.For(_class);
            float duration = man.Stance == MeleeStance.Charging
                ? profile.ChargeSeconds * (man.Form == AttackForm.Thrust ? 1f : 0.4f)
                : man.Stance == MeleeStance.Striking
                    ? profile.StrikeSeconds
                    : profile.RecoverSeconds;

            if (duration <= 0.001f) return 1f;
            return Mathf.Clamp01((Time.time - _stanceStart) / duration);
        }

        private static void Smooth(Transform target, Vector3 pos, Quaternion rot, float speed, float dt)
        {
            float k = 1f - Mathf.Exp(-speed * dt);
            target.localPosition = Vector3.Lerp(target.localPosition, pos, k);
            target.localRotation = Quaternion.Slerp(target.localRotation, rot, k);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        // --- Weapons ------------------------------------------------------------------------------

        private static readonly Color Grip = new Color(0.30f, 0.22f, 0.12f);
        private static readonly Color Ash = new Color(0.44f, 0.33f, 0.21f);
        private static readonly Color Brass = new Color(0.62f, 0.48f, 0.22f);
        private static readonly Color Oxblood = new Color(0.50f, 0.15f, 0.12f);
        private static readonly Color PaleWood = new Color(0.56f, 0.45f, 0.29f);
        private static readonly Color PaintedRing = new Color(0.28f, 0.22f, 0.18f);

        /// <summary>
        /// The weapon under an unscaled root at the hand, blade along +Z. A gladius: pommel, grip,
        /// guard, a fifty-centimetre blade with a tapered point. A framea: a two-metre ash shaft with
        /// a leaf head, held a third of the way from the butt.
        /// </summary>
        private static Transform BuildWeapon(Transform bodyRoot, WeaponClass weaponClass, bool emphasise)
        {
            var root = new GameObject("Weapon").transform;
            root.SetParent(bodyRoot, false);

            Color steel = WeaponColour(weaponClass, emphasise);

            if (weaponClass == WeaponClass.Spear)
            {
                const float shaft = 2.0f;
                const float butt = -0.55f;
                Transform pole = AddCylinder(root, "Shaft", 0.014f, shaft, Ash, new Vector3(0f, 0f, butt + shaft * 0.5f));
                AddCylinder(root, "Socket", 0.02f, 0.08f, steel, new Vector3(0f, 0f, butt + shaft - 0.02f));
                AddBox(root, "Head", new Vector3(0.06f, 0.012f, 0.28f), steel, new Vector3(0f, 0f, butt + shaft + 0.13f));
                AddBox(root, "Point", new Vector3(0.028f, 0.010f, 0.08f), steel, new Vector3(0f, 0f, butt + shaft + 0.30f));
                AddBox(root, "Binding", new Vector3(0.036f, 0.036f, 0.12f), Grip, Vector3.zero);
                _ = pole;
            }
            else if (weaponClass == WeaponClass.Longsword)
            {
                // A Germanic long blade: a wooden grip long enough for two hands, a plain iron
                // cross, and seventy centimetres of straight steel.
                AddBox(root, "Pommel", new Vector3(0.05f, 0.05f, 0.04f), steel, new Vector3(0f, 0f, -0.12f));
                AddCylinder(root, "Grip", 0.018f, 0.18f, Ash, new Vector3(0f, 0f, -0.02f));
                AddBox(root, "Cross", new Vector3(0.16f, 0.025f, 0.022f), steel, new Vector3(0f, 0f, 0.08f));
                AddBox(root, "Blade", new Vector3(0.05f, 0.011f, 0.62f), steel, new Vector3(0f, 0f, 0.08f + 0.31f));
                AddBox(root, "Point", new Vector3(0.03f, 0.009f, 0.10f), steel, new Vector3(0f, 0f, 0.08f + 0.62f + 0.045f));
            }
            else
            {
                AddBox(root, "Pommel", new Vector3(0.05f, 0.05f, 0.04f), Grip, new Vector3(0f, 0f, -0.075f));
                AddCylinder(root, "Grip", 0.018f, 0.10f, Grip, Vector3.zero);
                AddBox(root, "Guard", new Vector3(0.10f, 0.03f, 0.022f), Grip, new Vector3(0f, 0f, 0.062f));
                AddBox(root, "Blade", new Vector3(0.055f, 0.012f, 0.46f), steel, new Vector3(0f, 0f, 0.062f + 0.23f));
                AddBox(root, "Point", new Vector3(0.035f, 0.010f, 0.10f), steel, new Vector3(0f, 0f, 0.062f + 0.46f + 0.045f));
            }

            return root;
        }

        /// <summary>
        /// Roman scutum: a tall oxblood board curved around the man, five slats on an arc with a
        /// brass boss and spine. German board: a broad pale oval with a boss and a painted ring. The
        /// two silhouettes read the lines apart at a glance. Root at the grip, face toward +Z.
        /// </summary>
        private static Transform BuildShield(Transform bodyRoot, WeaponClass weaponClass, bool emphasise)
        {
            var root = new GameObject("Shield").transform;
            root.SetParent(bodyRoot, false);

            if (weaponClass == WeaponClass.Spear)
            {
                // Flattened cylinder: axis along Z after the turn, a 0.8 by 0.9 m oval.
                Transform board = BuildPrimitive(root, "Board", PrimitiveType.Cylinder,
                    new Vector3(0.80f, 0.015f, 0.90f), PaleWood);
                board.localRotation = Quaternion.Euler(90f, 0f, 0f);

                Transform ring = BuildPrimitive(root, "Ring", PrimitiveType.Cylinder,
                    new Vector3(0.52f, 0.017f, 0.60f), PaintedRing);
                ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.localPosition = new Vector3(0f, 0f, 0.004f);

                Transform boss = BuildPrimitive(root, "Boss", PrimitiveType.Sphere,
                    new Vector3(0.16f, 0.16f, 0.10f), WeaponColour(weaponClass, emphasise));
                boss.localPosition = new Vector3(0f, 0f, 0.03f);
                return root;
            }

            Color board2 = emphasise ? new Color(0.56f, 0.17f, 0.13f) : Oxblood;
            const float radius = 0.50f;
            for (int i = -2; i <= 2; i++)
            {
                float angle = i * 16f * Mathf.Deg2Rad;
                Transform slat = BuildPrimitive(root, "Slat", PrimitiveType.Cube,
                    new Vector3(0.135f, 1.05f, 0.02f), board2);
                slat.localPosition = new Vector3(Mathf.Sin(angle) * radius, 0f, -(1f - Mathf.Cos(angle)) * radius);
                slat.localRotation = Quaternion.Euler(0f, i * 16f, 0f);
            }

            Transform spine = BuildPrimitive(root, "Spine", PrimitiveType.Cube, new Vector3(0.035f, 0.92f, 0.018f), Brass);
            spine.localPosition = new Vector3(0f, 0f, 0.012f);

            Transform umbo = BuildPrimitive(root, "Boss", PrimitiveType.Sphere, new Vector3(0.15f, 0.15f, 0.10f), Brass);
            umbo.localPosition = new Vector3(0f, 0f, 0.03f);

            Transform rimTop = BuildPrimitive(root, "Rim", PrimitiveType.Cube, new Vector3(0.62f, 0.025f, 0.03f), Brass);
            rimTop.localPosition = new Vector3(0f, 0.525f, -0.02f);
            Transform rimBottom = BuildPrimitive(root, "Rim", PrimitiveType.Cube, new Vector3(0.62f, 0.025f, 0.03f), Brass);
            rimBottom.localPosition = new Vector3(0f, -0.525f, -0.02f);

            return root;
        }

        private static void AddBox(Transform parent, string name, Vector3 scale, Color colour, Vector3 localPos)
        {
            Transform box = BuildPrimitive(parent, name, PrimitiveType.Cube, scale, colour);
            box.localPosition = localPos;
        }

        /// <summary>A cylinder laid along +Z (Unity's stand along Y), given as radius and length.</summary>
        private static Transform AddCylinder(
            Transform parent, string name, float radius, float length, Color colour, Vector3 localPos)
        {
            Transform cylinder = BuildPrimitive(parent, name, PrimitiveType.Cylinder,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), colour);
            cylinder.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cylinder.localPosition = localPos;
            return cylinder;
        }

        private static Color WeaponColour(WeaponClass weaponClass, bool emphasise)
        {
            if (weaponClass == WeaponClass.Spear)
                return emphasise ? new Color(0.80f, 0.82f, 0.88f) : new Color(0.62f, 0.64f, 0.68f);
            return emphasise ? new Color(0.84f, 0.86f, 0.92f) : new Color(0.66f, 0.67f, 0.71f);
        }

        private static Transform BuildPrimitive(
            Transform parent, string name, PrimitiveType type, Vector3 scale, Color colour)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);   // cosmetic only; the sim resolves hits

            if (part.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = Material(colour);
                renderer.receiveShadows = false;
            }
            return part.transform;
        }

        // A tiny material cache keyed by colour, shared across all gear.
        private static readonly System.Collections.Generic.Dictionary<Color, Material> Materials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>Gear sits in the light like the men who carry it.</summary>
        private static Material Material(Color colour)
        {
            if (Materials.TryGetValue(colour, out Material cached) && cached != null) return cached;

            Material material = Century.Core.World.FxMaterials.Lit(colour);
            Materials[colour] = material;
            return material;
        }
    }
}
