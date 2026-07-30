using System.Collections.Generic;
using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// Code-generated stand-in trees, shared by the overmap and the battlefield: a dark trunk and a
    /// deep-green crown, readable as woodland from strategy height. On the battlefield the trunk
    /// keeps its collider and is present BEFORE the NavMesh bakes, so woods genuinely break up a
    /// line of battle rather than being scenery.
    /// </summary>
    public static class ForestBuilder
    {
        private static Material _trunkMaterial;
        private static Material _crownMaterial;

        /// <summary>Builds one tree per position under a common root, and returns the root.</summary>
        public static Transform BuildForest(Transform parent, List<Vector3> positions, bool withColliders)
            => BuildForest(parent, positions, withColliders, null);

        /// <summary>
        /// Prefab woods when a decor profile provides them (a share standing dead), primitive
        /// stand-ins otherwise. Every tree is guaranteed a trunk collider when asked for one, so
        /// the NavMesh bake (physics-collider mode) carves around it regardless of the prefab.
        /// </summary>
        public static Transform BuildForest(
            Transform parent, List<Vector3> positions, bool withColliders, TerrainDecorProfile profile)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(parent, false);

            bool usePrefabs = profile != null && profile.HasTrees;

            for (int i = 0; i < positions.Count; i++)
            {
                if (!usePrefabs)
                {
                    BuildTree(root, positions[i], withColliders, i);
                    continue;
                }

                float roll = i * 0.6180339887f % 1f;
                bool dead = profile.DeadTrees != null && profile.DeadTrees.Length > 0
                            && roll < profile.DeadShare;
                GameObject[] pool = dead ? profile.DeadTrees : profile.Trees;
                GameObject prefab = pool[(i * 7919) % pool.Length];
                if (prefab == null) continue;

                GameObject tree = Object.Instantiate(prefab, positions[i],
                    Quaternion.Euler(0f, roll * 360f, 0f), root);
                float scale = 0.9f + (i * 0.7548776662f % 1f) * 0.35f;
                tree.transform.localScale *= scale;

                EnsureTrunkCollider(tree, withColliders);
            }

            return root;
        }

        /// <summary>Scatters clutter prefabs (rocks, shrubs) at given positions; no colliders unless
        /// the prefab brings one and <paramref name="withColliders"/> keeps it.</summary>
        public static Transform ScatterClutter(
            Transform parent, string rootName, List<Vector3> positions, GameObject[] pool, bool withColliders)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(parent, false);
            if (pool == null || pool.Length == 0) return root;

            for (int i = 0; i < positions.Count; i++)
            {
                GameObject prefab = pool[(i * 6151) % pool.Length];
                if (prefab == null) continue;

                float roll = i * 0.6180339887f % 1f;
                GameObject item = Object.Instantiate(prefab, positions[i],
                    Quaternion.Euler(0f, roll * 360f, 0f), root);
                item.transform.localScale *= 0.85f + roll * 0.5f;

                if (!withColliders)
                    foreach (Collider c in item.GetComponentsInChildren<Collider>()) Remove(c);
            }

            return root;
        }

        /// <summary>
        /// Deterministic golden-angle scatter around an anchor: the shape of a natural stand — dense
        /// heart, ragged edge — rather than a survey grid. This is what turns "assets sprinkled on a
        /// map" into copses, thickets and outcrops: callers cluster positions here, then build.
        /// </summary>
        public static void ClusterPositions(
            float cx, float cz, int count, float radius, float seed,
            System.Func<float, float, float> heightAt, List<Vector3> into)
        {
            for (int i = 0; i < count; i++)
            {
                float jitter = Mathf.Repeat(Mathf.Sin(seed * 77.7f + i * 13.13f) * 43758.5453f, 1f);
                float angle = seed * 6.2831f + i * 2.39996f;   // the golden angle
                float r = radius * Mathf.Sqrt((i + jitter) / count);

                float px = cx + Mathf.Cos(angle) * r;
                float pz = cz + Mathf.Sin(angle) * r;
                into.Add(new Vector3(px, heightAt(px, pz), pz));
            }
        }

        /// <summary>The NavMesh carves around physics colliders; a prefab tree without one would be
        /// walked through. A capsule at the trunk is enough, and men never path into the canopy.</summary>
        private static void EnsureTrunkCollider(GameObject tree, bool wanted)
        {
            if (!wanted)
            {
                foreach (Collider c in tree.GetComponentsInChildren<Collider>()) Remove(c);
                return;
            }

            if (tree.GetComponentInChildren<Collider>() != null) return;

            var capsule = tree.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.5f, 0f);
            capsule.radius = 0.4f;
            capsule.height = 3f;
        }

        private static void BuildTree(Transform root, Vector3 position, bool withCollider, int index)
        {
            // Deterministic per-tree variation off the index, so the same forest grows twice.
            float scale = 0.85f + (index * 0.6180339887f % 1f) * 0.5f;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            trunk.transform.SetParent(root, false);
            trunk.transform.position = position + Vector3.up * (1.4f * scale);
            trunk.transform.localScale = new Vector3(0.4f * scale, 1.4f * scale, 0.4f * scale);

            if (!withCollider) Remove(trunk.GetComponent<Collider>());

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(trunk.transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            crown.transform.localScale = new Vector3(6.5f, 1.9f, 6.5f);

            Remove(crown.GetComponent<Collider>());

            Shade(trunk, crown);
        }

        /// <summary>The editor tool builds forests too, where Destroy() is illegal.</summary>
        private static void Remove(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private static void Shade(GameObject trunk, GameObject crown)
        {
            if (_trunkMaterial == null) _trunkMaterial = FxMaterials.Unlit(new Color(0.19f, 0.14f, 0.10f));
            if (_crownMaterial == null) _crownMaterial = FxMaterials.Unlit(new Color(0.10f, 0.17f, 0.09f));

            SetShared(trunk, _trunkMaterial);
            SetShared(crown, _crownMaterial);
        }

        private static void SetShared(GameObject go, Material material)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
