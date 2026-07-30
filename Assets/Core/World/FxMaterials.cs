using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// The two runtime materials every code-generated visual in the project is built from. This
    /// fallback dance was copy-pasted into fourteen files before it lived here; it exists because
    /// builds strip unreferenced shaders, so every Shader.Find must land somewhere safe.
    /// </summary>
    /// <remarks>
    /// Which helper to use is a real distinction, learned the hard way:
    /// <see cref="Unlit"/> (URP-first) renders the MATERIAL's colour — right for solid marker
    /// geometry. <see cref="VertexTinted"/> (Sprites/Default-first) renders VERTEX colour with
    /// alpha — the only correct choice for LineRenderers and Trails that fade or recolour per
    /// instance, because the URP Unlit shader ignores vertex colour entirely.
    /// </remarks>
    public static class FxMaterials
    {
        /// <summary>Opaque unlit material, optionally tinted. URP Unlit first, legacy fallbacks after.</summary>
        public static Material Unlit(Color? colour = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader);
            if (colour.HasValue) SetColour(material, colour.Value);
            return material;
        }

        /// <summary>Matte LIT material: shades with the sun and ambient, no specular glint. For
        /// bodies and props that must sit IN the world's light rather than float over it.</summary>
        public static Material Lit(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader);
            SetColour(material, colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        /// <summary>Transparent material that honours vertex colour and alpha (Sprites/Default).
        /// Use for anything a LineRenderer or TrailRenderer needs to fade or recolour.</summary>
        public static Material VertexTinted()
        {
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        /// <summary>Sets a colour across pipeline conventions (_BaseColor for URP, _Color for legacy).</summary>
        public static void SetColour(Material material, Color colour)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }
    }
}
