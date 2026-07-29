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

        public void Pose(BattleCombatant man, float dt)
        {
            GetWeaponPose(man, out Vector3 wPos, out Quaternion wRot);
            float speed = man.Stance == MeleeStance.Striking ? 20f : 11f;
            float k = 1f - Mathf.Exp(-speed * dt);

            _weapon.localPosition = Vector3.Lerp(_weapon.localPosition, wPos, k);
            _weapon.localRotation = Quaternion.Slerp(_weapon.localRotation, wRot, k);

            if (_shield == null) return;

            GetShieldPose(man, out Vector3 sPos, out Quaternion sRot);
            _shield.localPosition = Vector3.Lerp(_shield.localPosition, sPos, k);
            _shield.localRotation = Quaternion.Slerp(_shield.localRotation, sRot, k);
        }

        private void GetWeaponPose(BattleCombatant man, out Vector3 pos, out Quaternion rot)
        {
            switch (man.Stance)
            {
                case MeleeStance.Charging:
                    if (man.Form == AttackForm.Thrust) { pos = new Vector3(0.28f, 0.05f, -0.15f); rot = Quaternion.Euler(0f, 0f, 0f); }
                    else { pos = new Vector3(0.4f, 0.5f, -0.2f); rot = Quaternion.Euler(-60f, 30f, 0f); }
                    return;

                case MeleeStance.Striking:
                    if (man.Form == AttackForm.Thrust) { pos = new Vector3(0.14f, 0.05f, 0.75f); rot = Quaternion.Euler(0f, 0f, 0f); }
                    else { pos = new Vector3(-0.15f, 0.18f, 0.55f); rot = Quaternion.Euler(10f, -70f, 0f); }
                    return;

                default: // Idle / Recovering: at the side, ready
                    pos = new Vector3(0.35f, 0.0f, 0.18f);
                    rot = Quaternion.Euler(18f, 0f, 0f);
                    return;
            }
        }

        private static void GetShieldPose(BattleCombatant man, out Vector3 pos, out Quaternion rot)
        {
            if (man.ShieldRaised)
            {
                pos = new Vector3(0.0f, 0.2f, 0.62f);
                rot = Quaternion.identity;   // full scutum swung across the front
            }
            else
            {
                pos = new Vector3(-0.45f, 0.1f, 0.05f);
                rot = Quaternion.Euler(0f, 80f, 0f);   // carried at the left side, edge forward
            }
        }

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

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);

            Materials[colour] = material;
            return material;
        }
    }
}
