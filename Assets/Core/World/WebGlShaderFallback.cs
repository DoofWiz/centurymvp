using System.Collections.Generic;
using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// The Polytope vegetation/rock shaders are written for an older URP and fail on the WebGL
    /// target — every tree renders compile-error pink in the web build. On WebGL only, this
    /// sweeps the loaded scene and swaps any Polytope-shaded material for a stock URP Lit clone
    /// that keeps the asset's palette (the PT shaders paint with a Ground→Top height gradient;
    /// the clone takes a blend of the two). Wind sway and snow are lost on web; the colours and
    /// the silhouette are kept. Editor and desktop builds are untouched.
    /// </summary>
    public static class WebGlShaderFallback
    {
        private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();

        /// <summary>Replaces Polytope-shaded materials on every renderer in the loaded scenes.
        /// Idempotent and cached per source material — call freely after anything spawns decor.</summary>
        public static void Sweep()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null) return;

            Renderer[] renderers =
                Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] materials = renderers[r].sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == null) continue;
                    if (!source.shader.name.StartsWith("Polytope Studio/")) continue;

                    if (!Cache.TryGetValue(source, out Material replacement))
                    {
                        replacement = Build(source, lit);
                        Cache[source] = replacement;
                    }

                    materials[i] = replacement;
                    changed = true;
                }

                if (changed) renderers[r].sharedMaterials = materials;
            }
        }

        private static Material Build(Material source, Shader lit)
        {
            var replacement = new Material(lit) { name = source.name + " (WebGL fallback)" };

            // The PT look is a vertical gradient; a 65/35 blend toward the top colour reads as
            // the same palette from the game's camera heights.
            Color top = source.HasProperty("_TopColor") ? source.GetColor("_TopColor") : Color.white;
            Color ground = source.HasProperty("_GroundColor") ? source.GetColor("_GroundColor") : top;
            Color body = Color.Lerp(ground, top, 0.65f);
            if (source.HasProperty("_Color")) body *= source.GetColor("_Color");
            body.a = 1f;
            replacement.SetColor("_BaseColor", body);

            if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
                replacement.SetTexture("_BaseMap", source.GetTexture("_MainTex"));

            replacement.SetFloat("_Smoothness", 0f);

            // Leaf-card shaders need their cutout back or cards render as solid quads.
            string shaderName = source.shader.name;
            if (shaderName.Contains("Foliage") || shaderName.Contains("Leaf")
                || shaderName.Contains("Plants") || shaderName.Contains("Flowers")
                || shaderName.Contains("Grass"))
            {
                replacement.SetFloat("_AlphaClip", 1f);
                replacement.SetFloat("_Cutoff", 0.4f);
                replacement.EnableKeyword("_ALPHATEST_ON");
            }

            return replacement;
        }
    }
}
