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

        /// <summary>
        /// The woods as GROVES: cluster anchors where the forest function runs high, each grown into
        /// a stand with a dense heart and ragged edge, skirted by shrubs — not one tree per grid
        /// cell sprinkled across the map. Adjacent groves merge into forest belts naturally where
        /// the function stays high.
        /// </summary>
        private static void PlaceForests()
        {
            GameObject existing = GameObject.Find("Forest");
            if (existing != null) Object.DestroyImmediate(existing);
            GameObject oldLandmarks = GameObject.Find("Landmarks");
            if (oldLandmarks != null) Object.DestroyImmediate(oldLandmarks);

            TerrainDecorProfile decor = LoadOrBuildDecorProfile();
            System.Func<float, float, float> ground = WorldTerrainForge.HeightAt;

            var trees = new List<Vector3>(2048);
            var shrubs = new List<Vector3>(512);
            int groves = 0;

            for (float z = -Span * 0.5f + 25f; z < Span * 0.5f - 25f; z += 30f)
            {
                for (float x = -Span * 0.5f + 25f; x < Span * 0.5f - 25f; x += 30f)
                {
                    float density = WorldTerrainForge.Forest01(x, z);
                    if (density < 0.4f) continue;

                    float seed = Mathf.Repeat(Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f, 1f);
                    if (seed > density * 1.15f) continue;

                    // Anchor jitter so grove hearts don't sit on the survey grid.
                    float ax = x + (seed - 0.5f) * 22f;
                    float az = z + (Mathf.Repeat(seed * 7.31f, 1f) - 0.5f) * 22f;

                    int stand = 7 + Mathf.RoundToInt(density * 9f);
                    float radius = 11f + density * 9f;
                    ForestBuilder.ClusterPositions(ax, az, stand, radius, seed, ground, trees);
                    ForestBuilder.ClusterPositions(ax, az, 4, radius * 1.35f, seed * 3.7f, ground, shrubs);
                    groves++;
                }
            }

            ForestBuilder.BuildForest(null, trees, withColliders: true, decor);
            var root = new GameObject("Landmarks").transform;
            ForestBuilder.ScatterClutter(root, "Underbrush", shrubs, decor.Shrubs, withColliders: false);

            PlaceOutcrops(root, decor, ground);
            Debug.Log($"[Overmap] {groves} groves, {trees.Count} trees planted.");
        }

        /// <summary>Rocks in OUTCROPS of two to four along the river and on the high shoulders — a
        /// lone rock reads as litter; a group reads as geology. Menhirs alone, deliberately: a
        /// standing stone is the one thing on this map that SHOULD stand apart.</summary>
        private static void PlaceOutcrops(
            Transform root, TerrainDecorProfile decor, System.Func<float, float, float> ground)
        {
            var rocks = new List<Vector3>(128);
            var stones = new List<Vector3>(8);

            for (float z = -Span * 0.5f + 20f; z < Span * 0.5f - 20f; z += 44f)
            {
                for (float x = -Span * 0.5f + 20f; x < Span * 0.5f - 20f; x += 44f)
                {
                    float h1 = Mathf.Repeat(Mathf.Sin(x * 71.13f + z * 37.77f) * 43758.5453f, 1f);
                    float height = WorldTerrainForge.HeightAt(x, z);
                    float river = WorldTerrainForge.RiverDistance(x, z);

                    bool riverside = river < 22f && height > WorldTerrainForge.WaterLevel && h1 < 0.3f;
                    bool shoulder = height > 10f && h1 < 0.12f;
                    if (riverside || shoulder)
                        ForestBuilder.ClusterPositions(x, z, 2 + (int)(h1 * 10f) % 3, 4.5f, h1, ground, rocks);
                    else if (height > 14.5f && h1 < 0.035f && stones.Count < 5)
                        stones.Add(new Vector3(x, height, z));
                }
            }

            ForestBuilder.ScatterClutter(root, "Outcrops", rocks, decor.Rocks, withColliders: true);
            ForestBuilder.ScatterClutter(root, "Stones", stones, decor.Monuments, withColliders: true);
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
