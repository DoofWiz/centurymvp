using System.Collections.Generic;
using Century.Core.World;
using Unity.AI.Navigation;
using UnityEngine;

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

        /// <summary>Sculpts the battle terrain around <paramref name="worldCentre"/> (overmap XZ)
        /// and rebakes the scene NavMesh over the result. Call before anything is spawned.</summary>
        public static void Build(Vector2 worldCentre)
        {
            _terrain = Terrain.activeTerrain;
            if (_terrain == null || _terrain.terrainData == null)
            {
                Debug.LogWarning("[BattleTerrain] No terrain in the battle scene; fighting flat.");
                return;
            }

            // Clone the TerrainData so Editor play mode never dirties the shared asset, then shape
            // it: battle-local (0,0) is the encounter's world position.
            TerrainData data = Object.Instantiate(_terrain.terrainData);
            data.name = "Battlefield (sculpted)";
            data.heightmapResolution = 257;
            data.size = new Vector3(Span, WorldTerrainForge.MaxHeight, Span);
            data.alphamapResolution = 256;

            _terrain.transform.position = new Vector3(-Span * 0.5f, 0f, -Span * 0.5f);
            var originWorld = new Vector3(worldCentre.x - Span * 0.5f, 0f, worldCentre.y - Span * 0.5f);

            WorldTerrainForge.SculptHeights(data, originWorld);

            data.terrainLayers = MakeLayers();
            WorldTerrainForge.PaintSplats(data, originWorld);

            _terrain.terrainData = data;
            TerrainCollider collider = _terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;

            PlaceWater(originWorld);
            PlaceForest(worldCentre);

            // The baked flat NavMesh no longer matches the ground; rebake over the sculpt (and
            // around the tree trunks, which is what makes woods tactical).
            NavMeshSurface surface = _terrain.GetComponent<NavMeshSurface>();
            if (surface != null) surface.BuildNavMesh();
            else Debug.LogWarning("[BattleTerrain] Terrain has no NavMeshSurface; agents will misbehave.");
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

        /// <summary>A still water sheet at world level, if the river crosses this field at all.</summary>
        private static void PlaceWater(Vector3 originWorld)
        {
            bool wet = false;
            for (float z = 0f; z <= Span && !wet; z += 24f)
                for (float x = 0f; x <= Span && !wet; x += 24f)
                    wet = WorldTerrainForge.HeightAt(originWorld.x + x, originWorld.z + z)
                          < WorldTerrainForge.WaterLevel;

            if (!wet) return;

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "River";
            Object.Destroy(water.GetComponent<Collider>());
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.position = new Vector3(0f, WorldTerrainForge.WaterLevel, 0f);
            water.transform.localScale = new Vector3(Span, Span, 1f);

            Material material = FxMaterials.VertexTinted();
            material.color = new Color(0.16f, 0.22f, 0.26f, 0.82f);
            water.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static readonly List<Vector3> TreeScratch = new List<Vector3>(256);

        private static void PlaceForest(Vector2 worldCentre)
        {
            WorldTerrainForge.TreePositions(
                worldCentre - new Vector2(Span * 0.5f, Span * 0.5f),
                worldCentre + new Vector2(Span * 0.5f, Span * 0.5f),
                gridStep: 7.5f,
                TreeScratch);

            // Into battle-local space (terrain origin already shifted).
            for (int i = 0; i < TreeScratch.Count; i++)
            {
                Vector3 p = TreeScratch[i];
                TreeScratch[i] = new Vector3(p.x - worldCentre.x, p.y, p.z - worldCentre.y);
            }

            ForestBuilder.BuildForest(_terrain.transform.parent, TreeScratch, withColliders: true);
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
