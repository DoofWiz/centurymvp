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
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < positions.Count; i++)
                BuildTree(root, positions[i], withColliders, i);

            return root;
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
