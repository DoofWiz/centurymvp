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

        private static void PlaceForest(TerrainDecorProfile decor)
        {
            TreeScratch.Clear();

            const float step = 7.5f;
            for (float lz = -Span * 0.5f; lz < Span * 0.5f; lz += step)
            {
                for (float lx = -Span * 0.5f; lx < Span * 0.5f; lx += step)
                {
                    float density = WorldTerrainForge.BattleForest01(_recipe, lx, lz);
                    if (density < 0.35f) continue;

                    float h1 = Mathf.Repeat(Mathf.Sin(lx * 12.9898f + lz * 78.233f) * 43758.5453f, 1f);
                    float h2 = Mathf.Repeat(Mathf.Sin(lx * 39.346f + lz * 11.135f) * 43758.5453f, 1f);
                    if (h1 > density) continue;

                    float px = lx + (h2 - 0.5f) * step * 0.9f;
                    float pz = lz + (h1 - 0.5f) * step * 0.9f;
                    TreeScratch.Add(new Vector3(px, WorldTerrainForge.BattleHeightAt(_recipe, px, pz), pz));
                }
            }

            ForestBuilder.BuildForest(_terrain.transform.parent, TreeScratch, withColliders: true, decor);
        }

        /// <summary>Rocks gather where the ground is steep or wet — cover on exactly the ground
        /// the splat paints grey, reinforcing the slope read.</summary>
        private static void PlaceRocks(TerrainDecorProfile decor)
        {
            if (decor == null || decor.Rocks == null || decor.Rocks.Length == 0) return;

            TreeScratch.Clear();
            const float step = 21f;
            for (float lz = -Span * 0.5f; lz < Span * 0.5f; lz += step)
            {
                for (float lx = -Span * 0.5f; lx < Span * 0.5f; lx += step)
                {
                    float h1 = Mathf.Repeat(Mathf.Sin(lx * 91.17f + lz * 53.71f) * 43758.5453f, 1f);
                    if (h1 > 0.3f) continue;

                    float here = WorldTerrainForge.BattleHeightAt(_recipe, lx, lz);
                    float ahead = WorldTerrainForge.BattleHeightAt(_recipe, lx + 6f, lz);
                    bool steep = Mathf.Abs(ahead - here) > 1.1f;
                    bool wet = _recipe.HasRiver && WorldTerrainForge.LocalRiverDistance(_recipe, lx, lz) < 16f;
                    if (!steep && !wet) continue;

                    TreeScratch.Add(new Vector3(lx, here, lz));
                }
            }

            ForestBuilder.ScatterClutter(
                _terrain.transform.parent, "Rocks", TreeScratch, decor.Rocks, withColliders: true);
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
    }
}
