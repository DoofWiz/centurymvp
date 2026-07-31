using System.Collections.Generic;
using Century.Core.World;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Century.Battle.View
{
    /// <summary>
    /// Builds the battlefield from the campaign world: the scene's terrain is sculpted at load from
    /// <see cref="WorldTerrainForge"/> sampled around the encounter's overmap position — fight
    /// beside the river and the river runs through the field; hold the ridge and the ridge is here
    /// to hold. Trees stand with their trunks solid BEFORE the NavMesh rebakes, so woodland
    /// genuinely breaks a line of battle.
    /// </summary>
    public static class BattleTerrainBuilder
    {
        /// <summary>Battlefield terrain span in metres — the 70m field plus margin to the horizon.</summary>
        private const float Span = 240f;

        private static Terrain _terrain;

        private static WorldTerrainForge.BattleRecipe _recipe;

        /// <summary>
        /// Sculpts the battlefield around <paramref name="worldCentre"/> (overmap XZ) and rebakes
        /// the scene NavMesh over the result. Call before anything is spawned.
        ///
        /// NOT a literal window of the overmap: the overmap is regional, so a recipe reads its
        /// facts at the encounter point — river, woods, which way the land rises — and IMAGINES a
        /// battlefield at tactical scale, with hillsides steep enough to fight over.
        /// </summary>
        public static void Build(Vector2 worldCentre, TerrainDecorProfile decor)
        {
            _terrain = Terrain.activeTerrain;
            if (_terrain == null || _terrain.terrainData == null)
            {
                Debug.LogWarning("[BattleTerrain] No terrain in the battle scene; fighting flat.");
                return;
            }

            _recipe = WorldTerrainForge.ComposeBattle(worldCentre);

            // Clone the TerrainData so Editor play mode never dirties the shared asset.
            TerrainData data = Object.Instantiate(_terrain.terrainData);
            data.name = "Battlefield (sculpted)";
            data.heightmapResolution = 257;
            data.size = new Vector3(Span, WorldTerrainForge.MaxHeight, Span);
            data.alphamapResolution = 256;

            _terrain.transform.position = new Vector3(-Span * 0.5f, 0f, -Span * 0.5f);

            SculptFromRecipe(data);
            data.terrainLayers = MakeLayers();
            PaintFromRecipe(data);

            _terrain.terrainData = data;
            TerrainCollider collider = _terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;

            PlaceWater();
            PlaceForest(decor);
            PlaceRocks(decor);

            // Rebake over the sculpt from PHYSICS colliders: terrain, trunks and rocks carve the
            // mesh; colliderless dressing (water, canopies, shrubs) never pollutes it.
            NavMeshSurface surface = _terrain.GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
            }
            else Debug.LogWarning("[BattleTerrain] Terrain has no NavMeshSurface; agents will misbehave.");

            // A grim haze closes the distance without touching the fight: clarity near, dread far.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 420f;
        }

        private static void SculptFromRecipe(TerrainData data)
        {
            int res = data.heightmapResolution;
            var heights = new float[res, res];

            for (int zi = 0; zi < res; zi++)
            {
                float lz = zi / (float)(res - 1) * Span - Span * 0.5f;
                for (int xi = 0; xi < res; xi++)
                {
                    float lx = xi / (float)(res - 1) * Span - Span * 0.5f;
                    heights[zi, xi] = WorldTerrainForge.BattleHeightAt(_recipe, lx, lz)
                                      / WorldTerrainForge.MaxHeight;
                }
            }

            data.SetHeights(0, 0, heights);
        }

        private static void PaintFromRecipe(TerrainData data)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, 4];

            for (int zi = 0; zi < res; zi++)
            {
                float nz = zi / (float)(res - 1);
                float lz = nz * Span - Span * 0.5f;
                for (int xi = 0; xi < res; xi++)
                {
                    float nx = xi / (float)(res - 1);
                    float lx = nx * Span - Span * 0.5f;

                    float slope = data.GetSteepness(nx, nz) / 90f;
                    Vector4 w = WorldTerrainForge.BattleSplatWeights(_recipe, lx, lz, slope);

                    maps[zi, xi, 0] = w.x;
                    maps[zi, xi, 1] = w.y;
                    maps[zi, xi, 2] = w.z;
                    maps[zi, xi, 3] = w.w;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        private static TerrainLayer[] MakeLayers()
        {
            var layers = new TerrainLayer[WorldTerrainForge.LayerPalettes.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                (Color low, Color high, float tile) = WorldTerrainForge.LayerPalettes[i];
                layers[i] = TerrainDressing.MakeLayer(low, high, tile);
            }
            return layers;
        }

        /// <summary>A still water sheet at world level, if the recipe put the river on this field.</summary>
        private static void PlaceWater()
        {
            if (!_recipe.HasRiver) return;

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "River";
            Object.Destroy(water.GetComponent<Collider>());
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.position = new Vector3(0f, WorldTerrainForge.WaterLevel, 0f);
            water.transform.localScale = new Vector3(Span, Span, 1f);

            Material material = FxMaterials.VertexTinted();
            material.color = new Color(0.16f, 0.22f, 0.26f, 0.82f);
            water.GetComponent<Renderer>().sharedMaterial = material;

            // The runtime bake collects render meshes; without this the water sheet becomes an
            // invisible walkable plane and men fight standing on the river.
            water.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        private static readonly List<Vector3> TreeScratch = new List<Vector3>(256);
        private static readonly List<Vector3> ShrubScratch = new List<Vector3>(128);

        /// <summary>Battle woods as GROVES: stands a line can fight around and through, with shrub
        /// skirts — the same composition rule as the overmap, at tactical size.</summary>
        private static void PlaceForest(TerrainDecorProfile decor)
        {
            TreeScratch.Clear();
            ShrubScratch.Clear();

            System.Func<float, float, float> ground = GroundLocal;

            const float step = 22f;
            for (float lz = -Span * 0.5f + 12f; lz < Span * 0.5f - 12f; lz += step)
            {
                for (float lx = -Span * 0.5f + 12f; lx < Span * 0.5f - 12f; lx += step)
                {
                    float density = WorldTerrainForge.BattleForest01(_recipe, lx, lz);
                    if (density < 0.35f) continue;

                    float seed = Mathf.Repeat(Mathf.Sin(lx * 12.9898f + lz * 78.233f) * 43758.5453f, 1f);
                    if (seed > density * 1.2f) continue;

                    float ax = lx + (seed - 0.5f) * 15f;
                    float az = lz + (Mathf.Repeat(seed * 7.31f, 1f) - 0.5f) * 15f;

                    int stand = 5 + Mathf.RoundToInt(density * 6f);
                    float radius = 8f + density * 5f;
                    ForestBuilder.ClusterPositions(ax, az, stand, radius, seed, ground, TreeScratch);
                    ForestBuilder.ClusterPositions(ax, az, 3, radius * 1.35f, seed * 3.7f, ground, ShrubScratch);
                }
            }

            ForestBuilder.BuildForest(_terrain.transform.parent, TreeScratch, withColliders: true, decor);
            if (decor != null)
                ForestBuilder.ScatterClutter(
                    _terrain.transform.parent, "Underbrush", ShrubScratch, decor.Shrubs, withColliders: false);
        }

        private static float GroundLocal(float lx, float lz) =>
            WorldTerrainForge.BattleHeightAt(_recipe, lx, lz);

        /// <summary>Rock OUTCROPS of two to four where the ground is steep or wet — cover on
        /// exactly the ground the splat paints grey, and never a lone pebble in a meadow.</summary>
        private static void PlaceRocks(TerrainDecorProfile decor)
        {
            if (decor == null || decor.Rocks == null || decor.Rocks.Length == 0) return;

            TreeScratch.Clear();
            const float step = 32f;
            for (float lz = -Span * 0.5f; lz < Span * 0.5f; lz += step)
            {
                for (float lx = -Span * 0.5f; lx < Span * 0.5f; lx += step)
                {
                    float h1 = Mathf.Repeat(Mathf.Sin(lx * 91.17f + lz * 53.71f) * 43758.5453f, 1f);
                    if (h1 > 0.32f) continue;

                    float here = GroundLocal(lx, lz);
                    float ahead = GroundLocal(lx + 6f, lz);
                    bool steep = Mathf.Abs(ahead - here) > 1.1f;
                    bool wet = _recipe.HasRiver && WorldTerrainForge.LocalRiverDistance(_recipe, lx, lz) < 16f;
                    if (!steep && !wet) continue;

                    ForestBuilder.ClusterPositions(
                        lx, lz, 2 + (int)(h1 * 10f) % 3, 3.5f, h1, GroundLocal, TreeScratch);
                }
            }

            ForestBuilder.ScatterClutter(
                _terrain.transform.parent, "Outcrops", TreeScratch, decor.Rocks, withColliders: true);
        }

        /// <summary>World y of the battlefield ground under a flat position. Safe pre-build (0).</summary>
        public static float GroundHeight(Vector3 position)
        {
            if (_terrain == null) return 0f;
            return _terrain.SampleHeight(position) + _terrain.transform.position.y;
        }

        /// <summary>The position dropped onto the ground. Placement maths runs flat; the world is not.</summary>
        public static Vector3 Grounded(Vector3 position)
        {
            position.y = GroundHeight(position);
            return position;
        }

        /// <summary>Woodland density at a battle position, for map painting.</summary>
        public static float ForestDensity(Vector3 position) =>
            _terrain == null ? 0f : WorldTerrainForge.BattleForest01(_recipe, position.x, position.z);

        /// <summary>Standing in the river? (Feet below the waterline on a field that has one.)</summary>
        public static bool IsInWater(Vector3 position) =>
            _recipe.HasRiver && _terrain != null
            && position.y < WorldTerrainForge.WaterLevel - 0.05f;

        /// <summary>Local ground steepness, 0 flat to 1 at a 45-degree slope. Two-sample gradient —
        /// cheap enough for every moving man every frame.</summary>
        public static float Steepness01(Vector3 position)
        {
            if (_terrain == null) return 0f;

            const float probe = 1.6f;
            float gx = GroundHeight(position + Vector3.right * probe)
                       - GroundHeight(position - Vector3.right * probe);
            float gz = GroundHeight(position + Vector3.forward * probe)
                       - GroundHeight(position - Vector3.forward * probe);

            float grade = Mathf.Sqrt(gx * gx + gz * gz) / (2f * probe);
            return Mathf.Clamp01(grade);
        }
    }
}
