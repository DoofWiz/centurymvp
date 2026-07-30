using System.Collections.Generic;
using Century.Core.World;
using Unity.AI.Navigation;
using UnityEngine.AI;
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
            PlaceLandmarks();

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

            ForestBuilder.BuildForest(null, positions, withColliders: true, LoadOrBuildDecorProfile());
            Debug.Log($"[Overmap] {positions.Count} trees planted.");
        }

        /// <summary>Menhirs on the high ground, rock piles along the river, dead shrubs at the
        /// forest edges: landmarks that make the static world a place rather than a heightmap.</summary>
        private static void PlaceLandmarks()
        {
            GameObject existing = GameObject.Find("Landmarks");
            if (existing != null) Object.DestroyImmediate(existing);

            TerrainDecorProfile decor = LoadOrBuildDecorProfile();
            if (decor == null) return;

            var rocks = new List<Vector3>(128);
            var stones = new List<Vector3>(8);
            var shrubs = new List<Vector3>(256);

            for (float z = -Span * 0.5f + 20f; z < Span * 0.5f - 20f; z += 23f)
            {
                for (float x = -Span * 0.5f + 20f; x < Span * 0.5f - 20f; x += 23f)
                {
                    float h1 = Mathf.Repeat(Mathf.Sin(x * 71.13f + z * 37.77f) * 43758.5453f, 1f);
                    float height = WorldTerrainForge.HeightAt(x, z);
                    float river = WorldTerrainForge.RiverDistance(x, z);
                    float forest = WorldTerrainForge.Forest01(x, z);

                    if (river < 22f && height > WorldTerrainForge.WaterLevel && h1 < 0.22f)
                        rocks.Add(new Vector3(x, height, z));
                    else if (height > 14.5f && h1 < 0.04f && stones.Count < 5)
                        stones.Add(new Vector3(x, height, z));
                    else if (height > 9f && h1 < 0.06f)
                        rocks.Add(new Vector3(x, height, z));
                    else if (forest > 0.05f && forest < 0.4f && h1 < 0.16f)
                        shrubs.Add(new Vector3(x, height, z));
                }
            }

            var root = new GameObject("Landmarks").transform;
            ForestBuilder.ScatterClutter(root, "Rocks", rocks, decor.Rocks, withColliders: true);
            ForestBuilder.ScatterClutter(root, "Stones", stones, decor.Monuments, withColliders: true);
            ForestBuilder.ScatterClutter(root, "Shrubs", shrubs, decor.Shrubs, withColliders: false);
            Debug.Log($"[Overmap] Landmarks: {rocks.Count} rocks, {stones.Count} stones, {shrubs.Count} shrubs.");
        }

        /// <summary>
        /// The shared decor wardrobe: loaded from Assets/Terrain, or built there on first run by
        /// searching the Polytope Studio pack for the grim-appropriate pieces (pines and their
        /// dead, rocks, dead shrubs, menhirs — no orchards, no poppies). The designer re-dresses
        /// the asset freely afterwards; both maps read from it.
        /// </summary>
        private static TerrainDecorProfile LoadOrBuildDecorProfile()
        {
            const string path = "Assets/Terrain/TerrainDecorProfile.asset";

            var profile = AssetDatabase.LoadAssetAtPath<TerrainDecorProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<TerrainDecorProfile>();
            profile.Trees = FindPrefabs("PT_Pine_Tree_03_green");
            profile.DeadTrees = FindPrefabs("PT_Pine_Tree_03_dead");
            profile.Rocks = FindPrefabs("PT_Generic_Rock_01", "PT_River_Rock_Pile_02", "PT_Ore_Rock_01");
            profile.Shrubs = FindPrefabs("PT_Generic_Shrub_01_dead", "PT_Generic_Shrub_01_green");
            profile.Monuments = FindPrefabs("PT_Menhir_Rock_02");

            if (!AssetDatabase.IsValidFolder("Assets/Terrain"))
                AssetDatabase.CreateFolder("Assets", "Terrain");
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log("[Overmap] Decor profile built from the Polytope pack " +
                      (profile.HasTrees ? "(trees found)." : "(NO TREES FOUND — check the pack path)."));
            return profile;
        }

        private static GameObject[] FindPrefabs(params string[] names)
        {
            var found = new List<GameObject>();
            foreach (string name in names)
            {
                // Exact-name match only, and never the demo copies.
                foreach (string guid in AssetDatabase.FindAssets($"{name} t:Prefab",
                             new[] { "Assets/Polytope Studio" }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(assetPath) != name) continue;
                    if (assetPath.Contains("Demos")) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null) { found.Add(prefab); break; }
                }
            }
            return found.ToArray();
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

            // Physics colliders, not render meshes: terrain, trunks and rocks shape the mesh;
            // colliderless dressing (water, canopies, shrubs) can never pollute it again.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

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
