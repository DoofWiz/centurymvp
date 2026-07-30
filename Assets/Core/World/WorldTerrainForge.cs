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

        // --- The battlefield recipe --------------------------------------------------------------
        //
        // The overmap is a REGIONAL map: it can say "there is a river here, woods, rising ground
        // to the north-east" but it has no idea what a hundred metres of Germania actually looks
        // like. The recipe reads those regional facts at the encounter point and then IMAGINES a
        // battlefield at tactical scale — real hillsides, a proper riverbank, ground worth
        // choosing — deterministically, so the same place always births the same field.

        /// <summary>Regional facts + a locale seed, from which battle-scale ground is invented.</summary>
        public struct BattleRecipe
        {
            public Vector2 WorldCentre;
            /// <summary>Regional lie of the land: which way it rises, amplified to tactical grade.</summary>
            public Vector2 GradePerMetre;
            public bool HasRiver;
            /// <summary>Local-space signed offset of the river channel from field centre, and its axis.</summary>
            public float RiverOffset;
            public Vector2 RiverAxis;
            /// <summary>Regional woodland character, 0..1 — scales the battle map's own woods.</summary>
            public float Woodland;
            public float LocaleSeed;
        }

        public static BattleRecipe ComposeBattle(Vector2 centre)
        {
            // Regional gradient by sampling the overmap function coarsely either side.
            float step = 60f;
            var grade = new Vector2(
                (HeightAt(centre.x + step, centre.y) - HeightAt(centre.x - step, centre.y)) / (2f * step),
                (HeightAt(centre.x, centre.y + step) - HeightAt(centre.x, centre.y - step)) / (2f * step));

            float riverDistance = RiverDistance(centre.x, centre.y);

            // The river's local course: signed offset from field centre, axis from the centreline's drift.
            float xHere = centre.x - SignedRiverOffset(centre.x, centre.y);
            float xAhead = xHere + (SignedRiverOffset(centre.x, centre.y + 40f) - SignedRiverOffset(centre.x, centre.y));
            Vector2 axis = new Vector2(xAhead - xHere, 40f).normalized;

            float woodland = 0f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    woodland += Forest01(centre.x + dx * 45f, centre.y + dz * 45f);

            return new BattleRecipe
            {
                WorldCentre = centre,
                GradePerMetre = grade * 1.7f,   // the tactical map FEELS the regional rise
                HasRiver = riverDistance < 70f,
                RiverOffset = Mathf.Clamp(SignedRiverOffset(centre.x, centre.y), -80f, 80f),
                RiverAxis = axis,
                Woodland = Mathf.Clamp01(woodland / 9f * 1.4f),
                LocaleSeed = Mathf.Round(centre.x / 12f) * 131.7f + Mathf.Round(centre.y / 12f) * 517.3f
            };
        }

        private static float SignedRiverOffset(float x, float z)
        {
            float centreX = 130f * Mathf.Sin(z * 0.0052f + 1.35f)
                            + 45f * Mathf.Sin(z * 0.013f + 0.4f)
                            + 40f;
            return x - centreX;
        }

        /// <summary>Battlefield ground height at battle-LOCAL coordinates (field centre = 0,0).</summary>
        public static float BattleHeightAt(in BattleRecipe recipe, float lx, float lz)
        {
            // Base plateau + the regional tilt.
            float height = 4.5f + lx * recipe.GradePerMetre.x + lz * recipe.GradePerMetre.y;

            // Battle-frequency relief the overmap could never carry: real hillsides at ~90m and
            // ~40m wavelengths, sharpened so shoulders are steep enough to matter and to READ.
            float s = recipe.LocaleSeed;
            float broad = Mathf.PerlinNoise(lx * 0.011f + s, lz * 0.011f + s) - 0.5f;
            float fine = Mathf.PerlinNoise(lx * 0.026f + s * 1.7f, lz * 0.026f + s * 1.7f) - 0.5f;
            float relief = broad * 2f;
            relief = Mathf.Sign(relief) * Mathf.Pow(Mathf.Abs(relief), 1.35f);
            height += relief * 7.5f + fine * 3f;

            // The river, at battle width: a decisive channel with real banks, not a regional smear.
            if (recipe.HasRiver)
            {
                float d = LocalRiverDistance(recipe, lx, lz);
                float carve = Mathf.Exp(-(d * d) / (2f * 9f * 9f));
                height = Mathf.Lerp(height, 0.3f, Mathf.Clamp01(carve * 1.35f));
            }

            return Mathf.Clamp(height, 0f, MaxHeight);
        }

        /// <summary>Distance from the battle map's own river course (local space).</summary>
        public static float LocalRiverDistance(in BattleRecipe recipe, float lx, float lz)
        {
            // A line through (RiverOffset, 0) along RiverAxis, with a gentle battle-scale wobble.
            Vector2 toPoint = new Vector2(lx - recipe.RiverOffset, lz);
            float along = Vector2.Dot(toPoint, recipe.RiverAxis);
            float across = Mathf.Abs(toPoint.x * recipe.RiverAxis.y - toPoint.y * recipe.RiverAxis.x);
            return Mathf.Abs(across + Mathf.Sin(along * 0.05f + recipe.LocaleSeed) * 6f);
        }

        /// <summary>Battle woodland density at local coords: regional character, tactical clumps.</summary>
        public static float BattleForest01(in BattleRecipe recipe, float lx, float lz)
        {
            if (recipe.Woodland < 0.08f) return 0f;

            float s = recipe.LocaleSeed;
            float clumps = Mathf.PerlinNoise(lx * 0.02f + s * 2.3f, lz * 0.02f + s * 2.3f);
            float band = Mathf.InverseLerp(0.62f - recipe.Woodland * 0.35f, 0.78f, clumps);

            if (recipe.HasRiver)
                band *= Mathf.Clamp01((LocalRiverDistance(recipe, lx, lz) - 12f) / 8f);

            return Mathf.Clamp01(band);
        }

        /// <summary>
        /// Battle splat weights: same four layers, but slope shows EARLIER and harder — grey rock is
        /// the player's contour map, the visual grammar for "this is a slope, that is flat".
        /// </summary>
        public static Vector4 BattleSplatWeights(in BattleRecipe recipe, float lx, float lz, float slope01)
        {
            float bank = 0f;
            if (recipe.HasRiver)
                bank = Mathf.Clamp01(1f - (LocalRiverDistance(recipe, lx, lz) - 6f) / 8f);

            float rock = Mathf.InverseLerp(0.14f, 0.34f, slope01);
            float forest = BattleForest01(recipe, lx, lz) * 0.9f;
            float meadow = Mathf.Clamp01(1f - bank - rock - forest);

            var weights = new Vector4(meadow, forest, Mathf.Clamp01(rock), bank);
            float total = weights.x + weights.y + weights.z + weights.w;
            return total > 0.001f ? weights / total : new Vector4(1f, 0f, 0f, 0f);
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
