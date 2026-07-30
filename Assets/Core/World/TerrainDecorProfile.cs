using UnityEngine;

namespace Century.Core.World
{
    /// <summary>
    /// The world's dressing, as designer-assigned prefabs (the Polytope Studio pack): trees for the
    /// woods with a share of dead ones, rocks for slopes and riverbeds, shrubs for the in-between.
    /// One asset, referenced by the overmap sculpt tool and the battle terrain builder alike, so
    /// both maps are dressed from the same wardrobe. Leave arrays empty and the builders fall back
    /// to the primitive stand-ins.
    /// </summary>
    [CreateAssetMenu(menuName = "Century/Terrain Decor Profile", fileName = "TerrainDecorProfile")]
    public sealed class TerrainDecorProfile : ScriptableObject
    {
        [Header("Woods")]
        public GameObject[] Trees;
        public GameObject[] DeadTrees;

        [Tooltip("Share of the woods that stands dead. Germania is not a garden.")]
        [Range(0f, 1f)] public float DeadShare = 0.22f;

        [Header("Ground clutter")]
        public GameObject[] Rocks;
        public GameObject[] Shrubs;

        [Tooltip("Landmark stones, placed rarely and alone (menhirs). Optional.")]
        public GameObject[] Monuments;

        public bool HasTrees => Trees != null && Trees.Length > 0;
    }
}
