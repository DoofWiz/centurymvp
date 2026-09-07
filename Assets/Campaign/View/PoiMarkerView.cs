using Century.Campaign.Model;
using Century.Core.World;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// A place worth stopping for, shown as a place and not a peg: a pulsing ring on the ground, a
    /// shaft of light standing up out of it, a turning sigil, and a name in the world font that
    /// says what it is ("A HOLLOW IN THE ROCKS / CAMP GROUND"). The current objective wears gold
    /// and a brighter, wider ring, so the place the story wants is the place the eye goes.
    /// </summary>
    /// <remarks>
    /// Code-built, no prefab, no scene setup. Fades with the line of sight like everything else on
    /// the map; the overmap world drives that through <see cref="SetVisibility"/>.
    /// </remarks>
    public sealed class PoiMarkerView : MonoBehaviour
    {
        private const int RingSegments = 72;
        private const float RingRadius = 6f;
        private const float BeamHeight = 44f;
        private const float LabelHeight = 12f;

        private static readonly Color ObjectiveColour = new Color(1f, 0.84f, 0.32f);

        private PointOfInterest _poi;
        private Color _colour;
        private bool _objective;
        private float _visibility = 1f;
        private float _time;

        private LineRenderer _ring;
        private Transform _beam, _sigil, _labelRoot;
        private Material _beamMaterial, _sigilMaterial;
        private Color _beamBase, _sigilBase;
        private TextMesh _name, _kind, _tag;
        private Camera _camera;

        public static PoiMarkerView Create(Transform parent, PointOfInterest poi, Color colour, Font font)
        {
            var view = new GameObject($"Place [{poi.DisplayName}]").AddComponent<PoiMarkerView>();
            view.transform.SetParent(parent, false);

            Vector3 grounded = poi.WorldPosition;
            grounded.y = WorldTerrainForge.HeightAt(grounded.x, grounded.z);
            view.transform.position = grounded;

            view._poi = poi;
            view._colour = colour;
            view._camera = Camera.main;
            view.Build(font);
            return view;
        }

        /// <summary>The story's current target: gold, wider, brighter, and labelled as such.</summary>
        public void SetObjective(bool objective)
        {
            if (_objective == objective && _ring != null) return;
            _objective = objective;
            ApplyPalette();
        }

        /// <summary>How much of the marker the column can see, 0..1.</summary>
        public void SetVisibility(float alpha01)
        {
            _visibility = Mathf.Clamp01(alpha01);
        }

        // --- Build --------------------------------------------------------------------------------

        private void Build(Font font)
        {
            // The ring on the ground.
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = RingSegments;
            _ring.startWidth = 0.55f;
            _ring.endWidth = 0.55f;
            _ring.alignment = LineAlignment.TransformZ;
            _ring.material = FxMaterials.VertexTinted();
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // TransformZ up: lie flat
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * RingRadius, Mathf.Sin(a) * RingRadius, 0f));
            }

            // The shaft of light: a thin unlit column, translucent, standing out of the ring.
            _beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _beam.name = "Beam";
            _beam.SetParent(transform, false);
            _beam.localPosition = new Vector3(0f, BeamHeight * 0.5f, 0f);
            _beam.localScale = new Vector3(1.1f, BeamHeight * 0.5f, 1.1f);
            StripCollider(_beam.gameObject);
            _beamMaterial = new Material(FxMaterials.Unlit(_colour));
            PartyVisibility.MakeTransparentCapable(_beamMaterial);
            _beamBase = PartyVisibility.BaseColour(_beamMaterial);
            Renderer beamRenderer = _beam.GetComponent<Renderer>();
            beamRenderer.sharedMaterial = _beamMaterial;
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The sigil: a turning diamond above the ground.
            _sigil = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            _sigil.name = "Sigil";
            _sigil.SetParent(transform, false);
            _sigil.localPosition = new Vector3(0f, 5f, 0f);
            _sigil.localScale = new Vector3(2.2f, 2.2f, 2.2f);
            StripCollider(_sigil.gameObject);
            _sigilMaterial = new Material(FxMaterials.Unlit(_colour));
            PartyVisibility.MakeTransparentCapable(_sigilMaterial);
            _sigilBase = PartyVisibility.BaseColour(_sigilMaterial);
            Renderer sigilRenderer = _sigil.GetComponent<Renderer>();
            sigilRenderer.sharedMaterial = _sigilMaterial;
            sigilRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The name, facing the camera: what it is called, then what it IS.
            _labelRoot = new GameObject("Label").transform;
            _labelRoot.SetParent(transform, false);
            _labelRoot.localPosition = new Vector3(0f, LabelHeight, 0f);

            _name = MakeText(_labelRoot, font, 64, 0.30f, new Vector3(0f, 0.9f, 0f));
            _kind = MakeText(_labelRoot, font, 40, 0.24f, new Vector3(0f, -0.9f, 0f));
            _tag = MakeText(_labelRoot, font, 40, 0.24f, new Vector3(0f, -2.6f, 0f));

            _name.text = (_poi.DisplayName ?? string.Empty).ToUpperInvariant();
            _kind.text = KindWord(_poi.Kind);
            _tag.text = "◆ OBJECTIVE";

            ApplyPalette();
        }

        private static TextMesh MakeText(Transform parent, Font font, int fontSize, float characterSize, Vector3 offset)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;

            TextMesh text = go.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            if (font != null)
            {
                text.font = font;
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null && font.material != null) renderer.sharedMaterial = font.material;
            }
            return text;
        }

        private static void StripCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);   // must never eat the click-to-move ray
        }

        /// <summary>What a kind of place IS, in plain words under its name.</summary>
        public static string KindWord(PoiKind kind)
        {
            switch (kind)
            {
                case PoiKind.Settlement: return "SETTLEMENT";
                case PoiKind.Grove: return "GROVE";
                case PoiKind.Ruin: return "RUIN";
                case PoiKind.Refugees: return "REFUGEES";
                case PoiKind.RaiderCamp: return "RAIDER CAMP";
                case PoiKind.Battlefield: return "OLD BATTLEFIELD";
                case PoiKind.Opportunity: return "SCOUTED PRIZE";
                case PoiKind.Corpses: return "BATTLE SITE";
                case PoiKind.Hounds: return "BATTLE SITE";
                case PoiKind.Looters: return "LOOTERS' FIRE";
                case PoiKind.SafePlace: return "CAMP GROUND";
                case PoiKind.Passage: return "THE WAY OUT";
                default: return "A PLACE";
            }
        }

        // --- Look ---------------------------------------------------------------------------------

        private void ApplyPalette()
        {
            Color c = _objective ? ObjectiveColour : _colour;

            if (_beamMaterial != null)
            {
                PartyVisibility.SetAlpha(_beamMaterial, c, 1f);
                _beamBase = PartyVisibility.BaseColour(_beamMaterial);
            }
            if (_sigilMaterial != null)
            {
                PartyVisibility.SetAlpha(_sigilMaterial, c, 1f);
                _sigilBase = PartyVisibility.BaseColour(_sigilMaterial);
            }

            if (_ring != null)
            {
                _ring.startWidth = _objective ? 0.9f : 0.55f;
                _ring.endWidth = _ring.startWidth;
            }

            if (_name != null) _name.color = _objective ? ObjectiveColour : new Color(0.92f, 0.88f, 0.78f);
            if (_kind != null) _kind.color = _objective ? ObjectiveColour : new Color(0.72f, 0.68f, 0.58f);
            if (_tag != null) _tag.gameObject.SetActive(_objective);
        }

        private void LateUpdate()
        {
            _time += Time.deltaTime;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * (_objective ? 3.2f : 2f));
            float alpha = _visibility;

            // Ring: breathes in size and brightness.
            if (_ring != null)
            {
                Color c = _objective ? ObjectiveColour : _colour;
                c.a = alpha * Mathf.Lerp(0.45f, 1f, pulse);
                _ring.startColor = c;
                _ring.endColor = c;
                float radius = _objective ? Mathf.Lerp(1f, 1.18f, pulse) : 1f;
                _ring.transform.localScale = new Vector3(radius, radius, 1f);
            }

            // Beam: faint, brighter for the objective, fading with sight.
            if (_beamMaterial != null)
                PartyVisibility.SetAlpha(_beamMaterial, _beamBase,
                    alpha * (_objective ? Mathf.Lerp(0.22f, 0.38f, pulse) : 0.16f));

            // Sigil: turns and bobs.
            if (_sigil != null)
            {
                _sigil.localRotation = Quaternion.Euler(45f, _time * 40f, 45f);
                _sigil.localPosition = new Vector3(0f, 5f + Mathf.Sin(_time * 1.6f) * 0.5f, 0f);
                if (_sigilMaterial != null) PartyVisibility.SetAlpha(_sigilMaterial, _sigilBase, alpha);
            }

            // Label: faces the camera, fades with sight.
            if (_labelRoot != null)
            {
                if (_camera == null) _camera = Camera.main;
                if (_camera != null)
                    _labelRoot.rotation = Quaternion.LookRotation(_labelRoot.position - _camera.transform.position, Vector3.up);

                SetTextAlpha(_name, alpha);
                SetTextAlpha(_kind, alpha);
                SetTextAlpha(_tag, alpha * Mathf.Lerp(0.6f, 1f, pulse));
            }
        }

        private static void SetTextAlpha(TextMesh text, float alpha)
        {
            if (text == null) return;
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private void OnDestroy()
        {
            if (_beamMaterial != null) Destroy(_beamMaterial);
            if (_sigilMaterial != null) Destroy(_sigilMaterial);
        }
    }
}
