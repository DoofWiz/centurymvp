using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// Shared knowledge of which elements make up the fighting interface, so any modal phase can hide
    /// it without needing a reference to the HUD controller.
    /// </summary>
    /// <remarks>
    /// Deliberately a static helper operating on a root element rather than a method on the HUD
    /// controller. The deployment and summary screens each already hold the document root, and routing
    /// through a controller reference meant a single unassigned inspector field left the combat HUD
    /// visible underneath a modal — a failure with no error attached to it.
    /// </remarks>
    public static class BattleHudPanels
    {
        private static readonly string[] CombatPanelNames =
        {
            "engagement-panel",
            "squad-panel",
            "log-panel",
            "commander-panel",
            "command-bar",
            "selection-hint",
            "roster",
            "alert"
        };

        public static void SetCombatVisible(VisualElement root, bool visible)
        {
            if (root == null) return;

            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < CombatPanelNames.Length; i++)
            {
                VisualElement panel = root.Q<VisualElement>(CombatPanelNames[i]);
                if (panel != null) panel.style.display = display;
            }
        }

        /// <summary>Panel names, exposed so a controller can cache them if it prefers.</summary>
        public static IReadOnlyList<string> Names => CombatPanelNames;
    }
}
