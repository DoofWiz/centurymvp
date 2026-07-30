using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The visible weapon and shield on a fighter, generated in code and posed each frame to match his
    /// melee stance — sheathed at rest, drawn back on the wind-up, thrown forward on the strike, and
    /// the shield swung across the front when raised. Purely cosmetic: the sim owns the timing and the
    /// hit; this makes the fight read as blades and boards rather than sliding capsules.
    /// </summary>
    public sealed class CombatantGear
    {
        private readonly Transform _weapon;
        private readonly Transform _shield;   // null for a shield-less fighter
        private readonly WeaponClass _class;

        /// <param name="emphasise">The Centurion's own gear — built larger and in brighter steel so the
        /// player can read his blade against his body.</param>
        public CombatantGear(Transform bodyRoot, WeaponClass weaponClass, bool hasShield, bool emphasise = false)
        {
            _class = weaponClass;

            _weapon = BuildWeapon(bodyRoot, weaponClass, emphasise);

            if (hasShield)
                _shield = BuildShield(bodyRoot, weaponClass);

            Pose(new BattleCombatant { Weapon = weaponClass }, 1f); // snap to the rest pose
        }

        // --- Procedural swing animation ---------------------------------------------------------
        //
        // Not pose-lerps: a strike TRAVELS. The blade is driven parametrically along a real arc
        // (or a real thrust line) by the phase of the current stance, the body twists into and
        // through the blow, the shield recoils when it takes one, and a broken guard sags and
        // sways. All positions are body-root local with the pivot at the FEET.

        private static readonly Vector3 Shoulder = new Vector3(0.28f, 1.18f, 0.05f);
        private static readonly Vector3 RestPos = new Vector3(0.35f, 0.9f, 0.18f);
        private static readonly Quaternion RestRot = Quaternion.Euler(18f, 0f, 0f);

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
                Smooth(_weapon, new Vector3(0.4f, 0.7f, 0.1f), Quaternion.Euler(55f, 10f, 0f), 8f, dt);
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
                                new Vector3(0.3f, 0.98f, Mathf.Lerp(0.1f, -0.32f, phase)),
                                Quaternion.identity, 10f, dt);
                            targetTwist = -12f * phase;
                        }
                        else
                        {
                            // Cocking: up and behind the shoulder, blade turned for the cut.
                            Smooth(_weapon,
                                Vector3.Lerp(RestPos, new Vector3(0.44f, 1.34f, -0.26f), phase),
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
                                Mathf.Lerp(0.3f, 0.16f, drive), 1.0f, Mathf.Lerp(-0.32f, 0.95f, drive));
                            _weapon.localRotation = Quaternion.identity;
                            targetTwist = Mathf.Lerp(-12f, 16f, drive);
                            targetLean = 8f * drive;
                        }
                        else
                        {
                            // The cut: a diagonal arc swept around the shoulder, high-right to
                            // low-left, fastest through the middle — driven directly so the blade
                            // genuinely TRAVELS through the space in front of the man.
                            float sweep = EaseOutQuad(phase);
                            float yaw = Mathf.Lerp(62f, -58f, sweep);
                            float pitch = Mathf.Lerp(-32f, 14f, sweep);

                            Quaternion swing = Quaternion.Euler(pitch, yaw, 0f);
                            _weapon.localPosition = Shoulder + swing * (Vector3.forward * 0.55f)
                                                    - Vector3.forward * 0.1f;
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
                pos = new Vector3(0f, 1.1f, 0.62f - 0.15f * _blockRecoil);
                rot = Quaternion.Euler(-13f * _blockRecoil, 0f, 0f);
            }
            else
            {
                pos = new Vector3(-0.45f, 1.0f, 0.05f);
                rot = Quaternion.Euler(0f, 80f, 0f);   // carried at the left side, edge forward
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

        /// <summary>
        /// An unscaled root we pose each frame, with the blade and its furniture hung beneath it at their
        /// own scales — so the parts swing as one piece without the root's scale distorting them, and a
        /// gladius reads as a hilted sword rather than a thin stick.
        /// </summary>
        private static Transform BuildWeapon(Transform bodyRoot, WeaponClass weaponClass, bool emphasise)
        {
            var root = new GameObject("Weapon").transform;
            root.SetParent(bodyRoot, false);

            Color steel = WeaponColour(weaponClass, emphasise);
            var grip = new Color(0.28f, 0.22f, 0.12f);

            if (weaponClass == WeaponClass.Spear)
            {
                float shaft = emphasise ? 2.5f : 2.3f;
                AddBox(root, "Shaft", new Vector3(0.09f, 0.09f, shaft), new Color(0.42f, 0.31f, 0.2f),
                    new Vector3(0f, 0f, shaft * 0.35f));
                AddBox(root, "Head", new Vector3(0.15f, 0.15f, 0.45f), steel,
                    new Vector3(0f, 0f, shaft * 0.85f));
            }
            else
            {
                float blade = emphasise ? 1.45f : 1.2f;
                float thick = emphasise ? 0.2f : 0.15f;
                AddBox(root, "Blade", new Vector3(thick, thick, blade), steel,
                    new Vector3(0f, 0f, blade * 0.45f));
                AddBox(root, "Guard", new Vector3(thick * 2.6f, thick * 1.3f, thick * 0.5f), grip,
                    Vector3.zero);
                AddBox(root, "Pommel", new Vector3(thick * 1.5f, thick * 1.5f, thick * 1.2f), grip,
                    new Vector3(0f, 0f, -thick * 1.2f));
            }

            return root;
        }

        /// <summary>Roman scutum — a tall oxblood board; German board — rounder, wider, pale wood. The two
        /// silhouettes read the lines apart at a glance.</summary>
        private static Transform BuildShield(Transform bodyRoot, WeaponClass weaponClass) =>
            weaponClass == WeaponClass.Spear
                ? BuildBox(bodyRoot, "Shield", new Vector3(1.15f, 1.15f, 0.1f), new Color(0.46f, 0.35f, 0.2f))
                : BuildBox(bodyRoot, "Shield", new Vector3(0.95f, 1.35f, 0.12f), new Color(0.5f, 0.17f, 0.13f));

        private static void AddBox(Transform parent, string name, Vector3 scale, Color colour, Vector3 localPos)
        {
            Transform box = BuildBox(parent, name, scale, colour);
            box.localPosition = localPos;
        }

        private static Color WeaponColour(WeaponClass weaponClass, bool emphasise)
        {
            if (weaponClass == WeaponClass.Spear)
                return emphasise ? new Color(0.78f, 0.8f, 0.86f) : new Color(0.6f, 0.62f, 0.66f);
            return emphasise ? new Color(0.82f, 0.84f, 0.9f) : new Color(0.62f, 0.63f, 0.67f);
        }

        private static Transform BuildBox(Transform parent, string name, Vector3 scale, Color colour)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localScale = scale;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);   // cosmetic only; the sim resolves hits

            if (box.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = Material(colour);
            return box.transform;
        }

        // A tiny material cache keyed by colour, shared across all gear.
        private static readonly System.Collections.Generic.Dictionary<Color, Material> Materials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        private static Material Material(Color colour)
        {
            if (Materials.TryGetValue(colour, out Material cached) && cached != null) return cached;

            Material material = Century.Core.World.FxMaterials.Unlit(colour);
            Materials[colour] = material;
            return material;
        }
    }
}
