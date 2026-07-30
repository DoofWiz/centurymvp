using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Core.Ui
{
    /// <summary>
    /// The game's face: applies the designer-provided display font to a whole UI document the
    /// moment one exists at <c>Assets/Resources/Fonts/CenturyUI.ttf</c>. Until then every screen
    /// keeps the engine default — no missing-asset errors, no designer setup beyond dropping in
    /// the file. Suggested face: Cinzel (an inscription-Roman with modern readability; the UI is
    /// already largely capitals, which is exactly what it is best at).
    /// </summary>
    /// <remarks>
    /// World-space labels (TextMesh) load <c>Fonts/CenturyWorld</c> separately — a legacy Font
    /// asset renders those, and one file can serve both names if the designer duplicates it.
    /// </remarks>
    public static class UiFont
    {
        private static Font _font;
        private static bool _searched;

        /// <summary>Applies the display font to a document root. Inherits to every child.</summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;

            if (!_searched)
            {
                _searched = true;
                _font = Resources.Load<Font>("Fonts/CenturyUI");
            }

            if (_font != null)
                root.style.unityFontDefinition = FontDefinition.FromFont(_font);
        }
    }
}
