#if CENTURY_HAS_VECTOR_GRAPHICS
using Unity.VectorGraphics.Editor;
using UnityEditor;

namespace Century.EditorTools
{
    /// <summary>
    /// Imports every SVG in the icon pack as a UI Toolkit VectorImage, so the generated .gi-*
    /// classes in century.uss can reference them directly as backgrounds. Without this, each of the
    /// four thousand icons would need its import type set by hand.
    /// </summary>
    /// <remarks>
    /// Uses the Vector Graphics MODULE built into Unity 6.3+ (com.unity.modules.vectorgraphics) —
    /// no external package. The optional com.unity.vector-graphics package only adds Sprite-Editor
    /// and uGUI support, which this project does not need.
    /// </remarks>
    public sealed class SvgIconImportSettings : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/UI/IconsNET/") || !assetPath.EndsWith(".svg")) return;
            if (assetImporter is SVGImporter importer && importer.SvgType != SVGType.VectorImage)
                importer.SvgType = SVGType.VectorImage;
        }
    }
}
#endif
