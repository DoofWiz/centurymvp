using System.Collections.Generic;
using Century.Core.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Century.EditorTools
{
    /// <summary>
    /// Sculpts the STATIC campaign overmap from <see cref="WorldTerrainForge"/>: heights, painted
    /// layers, the river's water sheet and the forests — written into the terrain asset and the
    /// open scene, so the world is versioned like any other authored content. Run it once (or
    /// whenever the forge changes) with the Overmap scene open:
    ///
    ///     Century → Sculpt Overmap Terrain
    ///
    /// then re-bake the scene's NavMeshSurface (the tool attempts it automatically).
    /// </summary>
    public static class OvermapTerrainTool
    {
        private const float Span = 1000f;

        [MenuItem("Century/Sculpt Overmap Terrain")]
        public static void Sculpt()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                EditorUtility.DisplayDialog("Sculpt Overmap",
                    "No terrain found. Open the Overmap scene first.", "OK");
                return;
            }

            TerrainData data = terrain.terrainData;

            // Resolution first (it resets heights), then size, then the sculpt itself.
            data.heightmapResolution = 513;
            data.size = new Vector3(Span, WorldTerrainForge.MaxHeight, Span);
            terrain.transform.position = new Vector3(-Span * 0.5f, 0f, -Span * 0.5f);

            Vector3 origin = terrain.transform.position;
            WorldTerrainForge.SculptHeights(data, origin);

            data.terrainLayers = SaveLayers();
            data.alphamapResolution = 512;
            WorldTerrainForge.PaintSplats(data, origin);

            PlaceWater();
            PlaceForests();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();

            BakeNavMesh(terrain);

            Debug.Log("[Overmap] Sculpted from the world forge. Save the scene; " +
                      "if the NavMesh did not bake automatically, click Bake on the terrain's NavMeshSurface.");
        }

        /// <summary>Layer + texture assets under Assets/Terrain, so the paint survives as content.</summary>
        private static TerrainLayer[] SaveLayers()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Terrain"))
                AssetDatabase.CreateFolder("Assets", "Terrain");

            string[] names = { "Meadow", "ForestFloor", "Rock", "Riverbank" };
            var layers = new TerrainLayer[WorldTerrainForge.LayerPalettes.Length];

            for (int i = 0; i < layers.Length; i++)
            {
                (Color low, Color high, float tile) = WorldTerrainForge.LayerPalettes[i];

                Texture2D texture = TerrainDressing.BakeGroundTexture(low, high, keepReadable: true);
                texture.name = $"Ground_{names[i]}";
                ReplaceAsset(texture, $"Assets/Terrain/Ground_{names[i]}.asset");

                var layer = new TerrainLayer
                {
                    name = $"Layer_{names[i]}",
                    diffuseTexture = texture,
                    tileSize = new Vector2(tile, tile)
                };
                ReplaceAsset(layer, $"Assets/Terrain/Layer_{names[i]}.terrainlayer");
                layers[i] = layer;
            }

            return layers;
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void PlaceWater()
        {
            GameObject existing = GameObject.Find("River Water");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "River Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.position = new Vector3(0f, WorldTerrainForge.WaterLevel, 0f);
            water.transform.localScale = new Vector3(Span, Span, 1f);

            Material material = FxMaterials.VertexTinted();
            material.color = new Color(0.16f, 0.22f, 0.26f, 0.82f);
            water.GetComponent<Renderer>().sharedMaterial = material;

            // CRITICAL: the bake collects RENDER meshes, and this quad is a map-wide flat renderer.
            // Without the modifier it bakes into the NavMesh as an invisible walkable plane at
            // water level — every pawn then marches on it at constant height, under the hills.
            water.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        private static void PlaceForests()
        {
            GameObject existing = GameObject.Find("Forest");
            if (existing != null) Object.DestroyImmediate(existing);

            var positions = new List<Vector3>(1024);
            WorldTerrainForge.TreePositions(
                new Vector2(-Span * 0.5f + 15f, -Span * 0.5f + 15f),
                new Vector2(Span * 0.5f - 15f, Span * 0.5f - 15f),
                gridStep: 12f,
                positions);

            Transform forest = ForestBuilder.BuildForest(null, positions, withColliders: true);
            ExcludeCrownsFromBake(forest);
            Debug.Log($"[Overmap] {positions.Count} trees planted.");
        }

        /// <summary>Crowns are colliderless render meshes: left in the bake they leave walkable
        /// navmesh islands on top of the woods, which position sampling can snap pawns onto.</summary>
        private static void ExcludeCrownsFromBake(Transform forest)
        {
            foreach (Transform child in forest.GetComponentsInChildren<Transform>())
                if (child.name == "Crown")
                    child.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        private static void BakeNavMesh(Terrain terrain)
        {
            NavMeshSurface surface = terrain.GetComponent<NavMeshSurface>()
                                     ?? Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[Overmap] No NavMeshSurface found; parties will need one baked.");
                return;
            }

            // Editor-time bake through the package's own manager, so the data saves as an asset.
            try
            {
                Unity.AI.Navigation.Editor.NavMeshAssetManager.instance.StartBakingSurfaces(
                    new Object[] { surface });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Overmap] Automatic NavMesh bake failed ({e.Message}). " +
                                 "Click Bake on the NavMeshSurface component instead.");
            }
        }
    }
}
