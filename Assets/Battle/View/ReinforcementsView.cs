using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The view half of reinforcements: keeps every soldier's visibility in step with his squad's
    /// off-field flag — hidden while the squad waits beyond the edge, revealed and snapped to
    /// formation the moment it is summoned (by the player's order or the enemy commander's) — and
    /// draws the battlefield boundary so the field visibly ends where reinforcements enter.
    /// </summary>
    /// <remarks>
    /// The sim only ever toggles <see cref="BattleSquad.IsOffField"/>; this component notices the
    /// change and syncs. That keeps summoning free of any view knowledge, the same split as the rest
    /// of the battle.
    /// </remarks>
    public sealed class ReinforcementsView : MonoBehaviour
    {
        private readonly Dictionary<SoldierView, bool> _hidden = new Dictionary<SoldierView, bool>();
        private List<SoldierView> _soldiers;

        public void Initialise(List<SoldierView> soldiers, BattleSettings settings)
        {
            _soldiers = soldiers;
            BuildBorder(settings.FieldHalfExtent);
        }

        private void LateUpdate()
        {
            if (_soldiers == null) return;

            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierView view = _soldiers[i];
                if (view == null || view.Squad == null) continue;

                bool shouldHide = view.Squad.IsOffField;
                _hidden.TryGetValue(view, out bool isHidden);
                if (shouldHide == isHidden) continue;

                _hidden[view] = shouldHide;
                view.gameObject.SetActive(!shouldHide);

                // Marching on: appear in formation at the entry point rather than mid-stride.
                if (!shouldHide) view.SnapToSlot();
            }
        }

        /// <summary>A dim line square at the field's edge. Cosmetic, but it makes "off the field"
        /// a place you can see rather than a number in the rules.</summary>
        private void BuildBorder(float halfExtent)
        {
            var border = new GameObject("FieldBorder");
            border.transform.SetParent(transform, false);

            var line = border.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.startWidth = 0.35f;
            line.endWidth = 0.35f;
            line.material = new Material(
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));

            var colour = new Color(0.72f, 0.62f, 0.42f, 0.5f);
            line.startColor = colour;
            line.endColor = colour;
            if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", colour);
            if (line.material.HasProperty("_Color")) line.material.SetColor("_Color", colour);

            line.SetPosition(0, new Vector3(-halfExtent, 0.15f, -halfExtent));
            line.SetPosition(1, new Vector3(-halfExtent, 0.15f, halfExtent));
            line.SetPosition(2, new Vector3(halfExtent, 0.15f, halfExtent));
            line.SetPosition(3, new Vector3(halfExtent, 0.15f, -halfExtent));
        }
    }
}
