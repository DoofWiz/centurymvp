using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// The sight veil: a vast ground overlay that stays clear around the column and deepens into
    /// darkness beyond its line of sight, the way an RTS dims ground you cannot currently see. A
    /// baked radial texture on one quad that follows the player — cheap, and the gradient scales with
    /// the sight radius, so a better scout will literally push the darkness back.
    /// </summary>
    public sealed class OvermapFog : MonoBehaviour
    {
        [Tooltip("How dark the world gets beyond the line of sight. 0 = off, 1 = pitch black.")]
        [Range(0f, 1f)] [SerializeField] private float _maxDarkness = 0.62f;

        // The clear hole ends at this fraction of the quad's width, and full darkness begins at the
        // second. The quad is scaled so the first lands exactly on the line-of-sight radius. The
        // fractions are kept small so the quad stretches far past the map edge — ground beyond the
        // quad shows at full brightness, and that seam was visible from the map's corners.
        private const float ClearFraction = 0.06f;
        private const float DarkFraction = 0.091f;
        private const float QuadWidthPerSightRadius = 1f / ClearFraction;

        // Cell size decides how faithfully the veil hugs steep ground: at 48 cells the river banks
        // poked THROUGH the drape between vertices, reading as shifting patches of false vision.
        private const int GridCells = 96;
        private const float DrapeClearance = 3.5f;

        private CampaignState _state;
        private Transform _veil;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3 _lastDrapeCentre = new Vector3(float.MaxValue, 0f, 0f);
        private float _lastDrapeWidth;

        public void Bind(CampaignState state)
        {
            _state = state;
            BuildVeil();
        }

        /// <summary>
        /// A grid mesh rather than a flat quad: the veil DRAPES the sculpted terrain a couple of
        /// metres up, so hills neither pierce the darkness nor slide out from under it. Vertex
        /// heights come straight from <see cref="Century.Core.World.WorldTerrainForge"/> — the same
        /// function the ground itself was sculpted from, so the drape can never disagree with it.
        /// </summary>
        private void BuildVeil()
        {
            var go = new GameObject("SightVeil");
            go.transform.SetParent(transform, false);

            int side = GridCells + 1;
            _vertices = new Vector3[side * side];
            var uv = new Vector2[side * side];
            var triangles = new int[GridCells * GridCells * 6];

            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                    uv[z * side + x] = new Vector2(x / (float)GridCells, z / (float)GridCells);

            int t = 0;
            for (int z = 0; z < GridCells; z++)
            {
                for (int x = 0; x < GridCells; x++)
                {
                    int i = z * side + x;
                    triangles[t++] = i; triangles[t++] = i + side; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = i + side; triangles[t++] = i + side + 1;
                }
            }

            _mesh = new Mesh { name = "SightVeil" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uv;
            _mesh.triangles = triangles;

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Vertex-tinted (Sprites/Default): always present, transparent, unlit, double-sided.
            Material material = Century.Core.World.FxMaterials.VertexTinted();
            material.mainTexture = BakeVeilTexture();
            renderer.material = material;

            _veil = go.transform;
        }

        /// <summary>Radial gradient: clear centre, smooth ramp to darkness, dark to the corners.</summary>
        private Texture2D BakeVeilTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1) - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v);

                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClearFraction, DarkFraction, r))
                                  * _maxDarkness;

                    pixels[y * size + x] = new Color32(4, 6, 10, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private void LateUpdate()
        {
            if (_state?.PlayerParty == null || _veil == null) return;

            Vector3 centre = _state.PlayerParty.WorldPosition;

            // Scaled so the clear hole's edge sits exactly on the line of sight — future sight bonuses
            // widen the hole with no further work here.
            float width = LineOfSight.Radius(_state) * QuadWidthPerSightRadius;

            // Re-draping samples ~9.4k heights, so it happens only when the column has actually
            // moved (or sight changed), not every frame.
            if ((centre - _lastDrapeCentre).sqrMagnitude < 9f && Mathf.Approximately(width, _lastDrapeWidth))
                return;

            _lastDrapeCentre = centre;
            _lastDrapeWidth = width;
            Drape(centre, width);
        }

        private void Drape(Vector3 centre, float width)
        {
            _veil.position = new Vector3(centre.x, 0f, centre.z);

            int side = GridCells + 1;
            for (int z = 0; z < side; z++)
            {
                float lz = (z / (float)GridCells - 0.5f) * width;
                for (int x = 0; x < side; x++)
                {
                    float lx = (x / (float)GridCells - 0.5f) * width;
                    float height = Century.Core.World.WorldTerrainForge.HeightAt(centre.x + lx, centre.z + lz);
                    _vertices[z * side + x] = new Vector3(lx, height + DrapeClearance, lz);
                }
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }
    }
}
