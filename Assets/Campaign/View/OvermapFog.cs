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
        // second. The quad is scaled so the first lands exactly on the line-of-sight radius.
        private const float ClearFraction = 0.135f;
        private const float DarkFraction = 0.205f;
        private const float QuadWidthPerSightRadius = 1f / ClearFraction;

        private CampaignState _state;
        private Transform _quad;

        public void Bind(CampaignState state)
        {
            _state = state;
            BuildQuad();
        }

        private void BuildQuad()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SightVeil";
            quad.transform.SetParent(transform, false);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the ground

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);   // must never eat the click-to-move ray

            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Sprites/Default: always present, transparent, unlit, and double-sided.
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = BakeVeilTexture();
            renderer.material = material;

            _quad = quad.transform;
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
            if (_state?.PlayerParty == null || _quad == null) return;

            Vector3 centre = _state.PlayerParty.WorldPosition;
            _quad.position = new Vector3(centre.x, centre.y + 0.55f, centre.z);

            // Scaled so the clear hole's edge sits exactly on the line of sight — future sight bonuses
            // widen the hole with no further work here.
            float width = LineOfSight.Radius(_state) * QuadWidthPerSightRadius;
            _quad.localScale = new Vector3(width, width, 1f);
        }
    }
}
