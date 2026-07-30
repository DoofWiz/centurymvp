using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// Gives the scene's bare terrain an actual ground colour. The terrains were shipped with no
    /// paint layers at all, so URP's TerrainLit fell back to a plain white albedo — under the noon
    /// sun that clamps to sheer, eye-watering white (worst in WebGL, where nothing tempers it).
    /// This bakes a small mottled earth texture, wraps it in a runtime TerrainLayer and applies it
    /// to every terrain in the scene. Code-gen like the rest of the look; no designer setup, and
    /// real painted layers can replace it later by simply painting the terrain.
    /// </summary>
    public static class TerrainDressing
    {
        /// <summary>Dresses all active terrains in a mottled blend of the two albedo tones.</summary>
        public static void Apply(Color low, Color high, float tileSizeWorldUnits = 42f)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains.Length == 0) return;

            var layer = new TerrainLayer
            {
                diffuseTexture = BakeGroundTexture(low, high),
                tileSize = new Vector2(tileSizeWorldUnits, tileSizeWorldUnits),
                tileOffset = Vector2.zero
            };

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];

                // A terrain that already has painted layers is designer work — leave it alone.
                if (terrain.terrainData == null || terrain.terrainData.terrainLayers.Length > 0) continue;

                // Clone the TerrainData: it is a shared asset, and mutating it in Editor play mode
                // would write a dangling runtime-layer reference into the project.
                TerrainData data = Object.Instantiate(terrain.terrainData);
                data.name = terrain.terrainData.name + " (dressed)";
                data.terrainLayers = new[] { layer };

                // Guarantee the single layer at full weight everywhere; a fresh terrain has no
                // control map, and the splat shader must not be left to guess one.
                int res = data.alphamapResolution;
                var weights = new float[res, res, 1];
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        weights[y, x, 0] = 1f;
                data.SetAlphamaps(0, 0, weights);

                terrain.terrainData = data;

                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null) collider.terrainData = data;
            }
        }

        /// <summary>A ready TerrainLayer over a baked two-tone ground texture. Shared by the sculpted
        /// overmap/battlefield builders, which paint several of these by splat weight.</summary>
        public static TerrainLayer MakeLayer(Color low, Color high, float tileSizeWorldUnits, bool keepReadable = false)
        {
            return new TerrainLayer
            {
                diffuseTexture = BakeGroundTexture(low, high, keepReadable: keepReadable),
                tileSize = new Vector2(tileSizeWorldUnits, tileSizeWorldUnits),
                tileOffset = Vector2.zero
            };
        }

        /// <summary>Two octaves of Perlin between the tones, plus a whisper of per-pixel grain so
        /// large flat stretches read as ground rather than a colour swatch. Keep it readable when
        /// it is destined for an asset (the editor tool saves these).</summary>
        public static Texture2D BakeGroundTexture(Color low, Color high, int size = 128, bool keepReadable = false)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "GroundAlbedo (TerrainDressing)"
            };

            var pixels = new Color32[size * size];
            const float coarse = 5f;    // broad patches of lighter and darker ground
            const float fine = 23f;     // small breakup inside them

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;

                    float n = TileableNoise(u, v, coarse, 100f) * 0.7f
                            + TileableNoise(u, v, fine, 300f) * 0.3f;

                    // Deterministic grain, ±0.03, so the tile stays identical run to run.
                    float grain = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f)
                                  * 0.06f - 0.03f;

                    Color c = Color.Lerp(low, high, Mathf.Clamp01(n + grain));
                    pixels[y * size + x] = new Color32(
                        (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: !keepReadable);
            return texture;
        }

        /// <summary>
        /// Perlin made seamless over the unit square by cross-fading four period-offset samples —
        /// the texture repeats across the terrain, so a hard tile edge would read as a world grid.
        /// </summary>
        private static float TileableNoise(float u, float v, float frequency, float offset)
        {
            float x = u * frequency, y = v * frequency;

            float n00 = Mathf.PerlinNoise(offset + x, offset + y);
            float n10 = Mathf.PerlinNoise(offset + x - frequency, offset + y);
            float n01 = Mathf.PerlinNoise(offset + x, offset + y - frequency);
            float n11 = Mathf.PerlinNoise(offset + x - frequency, offset + y - frequency);

            return Mathf.Lerp(
                Mathf.Lerp(n00, n10, u),
                Mathf.Lerp(n01, n11, u),
                v);
        }
    }
}
