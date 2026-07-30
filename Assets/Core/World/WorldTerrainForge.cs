using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// THE shape of the world, as one deterministic function of world position. The overmap editor
    /// tool sculpts the static campaign terrain from it once; the battle scene samples the SAME
    /// function around the encounter point at load — which is the whole trick: fight beside the
    /// river and the battlefield has the river, hold the ridge on the overmap and you hold it in
    /// the fight. No data is passed between the two beyond a coordinate.
    /// </summary>
    /// <remarks>
    /// Everything is seeded by fixed offsets, so the world is identical across machines, sessions
    /// and builds. Heights are in metres; world XZ is shared by both scenes (the overmap terrain
    /// spans roughly -500..+500 on each axis).
    /// </remarks>
    public static class WorldTerrainForge
    {
        /// <summary>Tallest ground in the world, in metres. TerrainData.size.y must use this.</summary>
        public const float MaxHeight = 24f;

        /// <summary>World y of the river's standing water. Ground below this is underwater.</summary>
        public const float WaterLevel = 1.1f;

        // Seed offsets: the world's identity. Changing these changes Germania.
        private const float SeedA = 137.31f;
        private const float SeedB = 411.77f;
        private const float SeedC = 269.53f;

        // --- The land --------------------------------------------------------------------------

        /// <summary>Ground height in metres at a world XZ position.</summary>
        public static float HeightAt(float x, float z)
        {
            // Rolling hills: two octaves of broad Perlin.
            float hills = Fbm(x * 0.006f, z * 0.006f, SeedA) * 8.5f;

            // The ridge: a long diagonal spine across the north-east, softly peaked.
            float ridgeAxis = (x * 0.62f + z * 0.78f - 210f) / 130f;
            float ridge = Mathf.Exp(-ridgeAxis * ridgeAxis) * 9f
                          * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.01f + SeedB, z * 0.01f));

            float height = 2.2f + hills + ridge;

            // The river carves last, cutting whatever stood in its way: a winding south-north
            // valley with soft banks, its bed pinned near zero so the water level stays honest.
            float riverDistance = RiverDistance(x, z);
            float carve = Mathf.Exp(-(riverDistance * riverDistance) / (2f * 14f * 14f));
            height = Mathf.Lerp(height, 0.35f, Mathf.Clamp01(carve * 1.25f));

            return Mathf.Clamp(height, 0f, MaxHeight);
        }

        /// <summary>Metres from the river's centreline (the channel winds; this is flat distance).</summary>
        public static float RiverDistance(float x, float z)
        {
            float centreX = 130f * Mathf.Sin(z * 0.0052f + 1.35f)
                            + 45f * Mathf.Sin(z * 0.013f + 0.4f)
                            + 40f;
            return Mathf.Abs(x - centreX);
        }

        /// <summary>Forest density 0..1: broad woodland patches, thinning near water and steep rock.</summary>
        public static float Forest01(float x, float z)
        {
            float noise = Fbm(x * 0.011f, z * 0.011f, SeedC);
            float band = Mathf.InverseLerp(0.45f, 0.7f, noise);

            // Trees stand back from the water and give up on the high bare ridge.
            band *= Mathf.Clamp01((RiverDistance(x, z) - 16f) / 12f);
            band *= 1f - Mathf.InverseLerp(13f, 17f, HeightRaw(x, z));

            return Mathf.Clamp01(band);
        }

        /// <summary>Hills+ridge height without the river carve — used to keep forest off the peaks.</summary>
        private static float HeightRaw(float x, float z)
        {
            float hills = Fbm(x * 0.006f, z * 0.006f, SeedA) * 8.5f;
            float ridgeAxis = (x * 0.62f + z * 0.78f - 210f) / 130f;
            return 2.2f + hills + Mathf.Exp(-ridgeAxis * ridgeAxis) * 9f;
        }

        // --- Painting --------------------------------------------------------------------------

        /// <summary>
        /// Splat weights at a point for the standard four layers: 0 meadow, 1 forest floor,
        /// 2 bare rock, 3 riverbank. Normalised; slope is 0..1 (0 flat).
        /// </summary>
        public static Vector4 SplatWeights(float x, float z, float height, float slope01)
        {
            float bank = Mathf.Clamp01(1f - (RiverDistance(x, z) - 8f) / 10f);
            float rock = Mathf.InverseLerp(0.28f, 0.5f, slope01)
                         + Mathf.InverseLerp(14f, 18f, height) * 0.7f;
            float forest = Forest01(x, z) * 0.9f;

            rock = Mathf.Clamp01(rock);
            float meadow = Mathf.Clamp01(1f - bank - rock - forest);

            var weights = new Vector4(meadow, forest, rock, bank);
            float total = weights.x + weights.y + weights.z + weights.w;
            return total > 0.001f ? weights / total : new Vector4(1f, 0f, 0f, 0f);
        }

        /// <summary>The four layer palettes (low tone, high tone, tile size) shared by both scenes.</summary>
        public static readonly (Color low, Color high, float tile)[] LayerPalettes =
        {
            (new Color(0.16f, 0.19f, 0.11f), new Color(0.30f, 0.32f, 0.19f), 42f),   // meadow
            (new Color(0.13f, 0.12f, 0.08f), new Color(0.24f, 0.20f, 0.12f), 34f),   // forest floor
            (new Color(0.28f, 0.27f, 0.26f), new Color(0.42f, 0.40f, 0.37f), 26f),   // rock
            (new Color(0.30f, 0.27f, 0.19f), new Color(0.45f, 0.40f, 0.28f), 22f)    // riverbank
        };

        // --- Sculpting (shared by the editor tool and the battle builder) -----------------------

        /// <summary>
        /// Writes heights for a terrain whose south-west corner sits at <paramref name="originWorld"/>.
        /// Sets nothing else; the caller owns resolution and layer setup.
        /// </summary>
        public static void SculptHeights(TerrainData data, Vector3 originWorld)
        {
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            Vector3 size = data.size;

            for (int zi = 0; zi < res; zi++)
            {
                float wz = originWorld.z + zi / (float)(res - 1) * size.z;
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = originWorld.x + xi / (float)(res - 1) * size.x;
                    heights[zi, xi] = HeightAt(wx, wz) / size.y;   // heightmap is [z, x]
                }
            }

            data.SetHeights(0, 0, heights);
        }

        /// <summary>Paints the four-layer splat for a terrain at <paramref name="originWorld"/>.
        /// The TerrainData must already carry exactly four layers in palette order.</summary>
        public static void PaintSplats(TerrainData data, Vector3 originWorld)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, 4];
            Vector3 size = data.size;

            for (int zi = 0; zi < res; zi++)
            {
                float nz = zi / (float)(res - 1);
                float wz = originWorld.z + nz * size.z;
                for (int xi = 0; xi < res; xi++)
                {
                    float nx = xi / (float)(res - 1);
                    float wx = originWorld.x + nx * size.x;

                    float slope = data.GetSteepness(nx, nz) / 90f;
                    Vector4 w = SplatWeights(wx, wz, HeightAt(wx, wz), slope);

                    maps[zi, xi, 0] = w.x;
                    maps[zi, xi, 1] = w.y;
                    maps[zi, xi, 2] = w.z;
                    maps[zi, xi, 3] = w.w;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        /// <summary>
        /// Deterministic tree positions inside a world-space rectangle: a jittered grid thinned by
        /// forest density. Same rectangle in, same forest out — on either map.
        /// </summary>
        public static void TreePositions(
            Vector2 minWorld, Vector2 maxWorld, float gridStep, System.Collections.Generic.List<Vector3> results)
        {
            results.Clear();

            for (float z = Mathf.Ceil(minWorld.y / gridStep) * gridStep; z < maxWorld.y; z += gridStep)
            {
                for (float x = Mathf.Ceil(minWorld.x / gridStep) * gridStep; x < maxWorld.x; x += gridStep)
                {
                    float density = Forest01(x, z);
                    if (density < 0.35f) continue;

                    // Hash the cell for jitter and thinning, so the woods are ragged, not gridded.
                    float h1 = Hash01(x * 12.9898f + z * 78.233f);
                    float h2 = Hash01(x * 39.346f + z * 11.135f);
                    if (h1 > density) continue;

                    float px = x + (h2 - 0.5f) * gridStep * 0.9f;
                    float pz = z + (h1 - 0.5f) * gridStep * 0.9f;
                    results.Add(new Vector3(px, HeightAt(px, pz), pz));
                }
            }
        }

        // --- Noise -----------------------------------------------------------------------------

        private static float Fbm(float x, float z, float seed)
        {
            return Mathf.PerlinNoise(x + seed, z + seed) * 0.65f
                   + Mathf.PerlinNoise(x * 2.13f + seed * 1.7f, z * 2.13f + seed * 1.7f) * 0.35f;
        }

        private static float Hash01(float value)
        {
            float f = Mathf.Sin(value) * 43758.5453f;
            return f - Mathf.Floor(f);
        }
    }
}
