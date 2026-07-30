using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// A floating tag above each player squad: its numeral always, and — whenever its orders change —
    /// a brief popup naming the order it has received (amber while the command is still travelling,
    /// parchment once the squad is enacting it). This is the information layer that lets the player
    /// tell his contubernia apart and see that a command actually landed, without looking at the HUD.
    /// All code-generated; no scene setup.
    /// </summary>
    public sealed class SquadLabels : MonoBehaviour
    {
        private const float PopupSeconds = 3f;
        private const float Height = 3.6f;

        private static readonly Color IdleColour = new Color(0.85f, 0.8f, 0.68f, 0.65f);
        private static readonly Color SelectedColour = new Color(1f, 0.92f, 0.6f, 1f);
        private static readonly Color PendingColour = new Color(0.94f, 0.72f, 0.3f, 1f);
        private static readonly Color BrokenColour = new Color(0.85f, 0.3f, 0.24f, 1f);

        private sealed class Tag
        {
            public BattleSquad Squad;
            public int Index;
            public Transform Root;
            public TextMesh Numeral;
            public TextMesh Order;

            // The cohesion pip: a sliver of bar under the numeral. THE glance answer to the only
            // question that decides battles — who is about to break.
            public Transform BarFill;
            public Material BarFillMaterial;
            public Material BarBackMaterial;

            // Last seen order state, to notice changes and time the popup.
            public SquadOrder SeenOrder;
            public FormationType SeenFormation;
            public bool SeenPending;
            public float PopupUntil;
        }

        private const float BarWidth = 1.7f;
        private const float BarHeight = 0.14f;

        private static readonly Color BarSteady = new Color(0.88f, 0.76f, 0.42f);
        private static readonly Color BarWavering = new Color(0.92f, 0.58f, 0.22f);
        private static readonly Color BarBreaking = new Color(0.85f, 0.28f, 0.2f);

        private readonly List<Tag> _tags = new List<Tag>();
        private SquadCommandInput _commands;
        private Camera _camera;
        private float _now;

        public void Initialise(List<BattleSquad> playerSquads, SquadCommandInput commands)
        {
            _commands = commands;
            _camera = Camera.main;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < playerSquads.Count; i++)
            {
                BattleSquad squad = playerSquads[i];

                var tag = new Tag
                {
                    Squad = squad,
                    Index = i,
                    SeenOrder = squad.Order,
                    SeenFormation = squad.Formation,
                };

                tag.Root = new GameObject($"SquadTag [{squad.DisplayName}]").transform;
                tag.Root.SetParent(transform, false);

                tag.Numeral = MakeText(tag.Root, font, 60, 0.07f, Vector3.zero);
                tag.Order = MakeText(tag.Root, font, 38, 0.05f, new Vector3(0f, -0.55f, 0f));
                BuildCohesionBar(tag);

                tag.Numeral.text = ShortName(squad);
                _tags.Add(tag);
            }
        }

        /// <summary>A dark backing sliver and a coloured fill quad, scaled by cohesion each frame.</summary>
        private static void BuildCohesionBar(Tag tag)
        {
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            tag.BarBackMaterial = new Material(shader) { color = new Color(0.05f, 0.05f, 0.06f, 0.7f) };
            tag.BarFillMaterial = new Material(shader) { color = BarSteady };

            Transform back = MakeBarQuad(tag.Root, "BarBack", tag.BarBackMaterial);
            back.localPosition = new Vector3(0f, -0.32f, 0.01f);   // pushed slightly behind the fill
            back.localScale = new Vector3(BarWidth, BarHeight, 1f);

            tag.BarFill = MakeBarQuad(tag.Root, "BarFill", tag.BarFillMaterial);
            tag.BarFill.localPosition = new Vector3(0f, -0.32f, 0f);
            tag.BarFill.localScale = new Vector3(BarWidth, BarHeight, 1f);
        }

        private static Transform MakeBarQuad(Transform parent, string name, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        private static TextMesh MakeText(Transform parent, Font font, int size, float charSize, Vector3 offset)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;

            TextMesh text = go.AddComponent<TextMesh>();
            text.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
            text.fontSize = size;
            text.characterSize = charSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            return text;
        }

        private void LateUpdate()
        {
            _now = Time.time;
            Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;

            for (int i = 0; i < _tags.Count; i++)
            {
                Tag tag = _tags[i];
                BattleSquad squad = tag.Squad;

                if (squad.IsDestroyed || squad.IsOffField)
                {
                    tag.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!tag.Root.gameObject.activeSelf) tag.Root.gameObject.SetActive(true);
                tag.Root.position = squad.CentreOfMass() + Vector3.up * Height;
                tag.Root.rotation = facing;

                NoticeOrderChanges(tag, squad);
                Style(tag, squad);
                StyleCohesionBar(tag, squad);
            }
        }

        /// <summary>Fill and colour track cohesion; the bar itself pulses once a squad is on the
        /// edge, because that is the moment the eye must be dragged to it.</summary>
        private void StyleCohesionBar(Tag tag, BattleSquad squad)
        {
            if (tag.BarFill == null) return;

            float cohesion = Mathf.Clamp01(squad.Cohesion01);
            float width = BarWidth * Mathf.Max(0.02f, cohesion);
            tag.BarFill.localScale = new Vector3(width, BarHeight, 1f);
            tag.BarFill.localPosition = new Vector3(-(BarWidth - width) * 0.5f, -0.32f, 0f);

            CohesionBand band = squad.Band;
            Color colour = band >= CohesionBand.Confident ? BarSteady
                : band == CohesionBand.Wavering ? BarWavering
                : BarBreaking;

            // Breaking (or already broken): the pulse. A steady line asks nothing of the player.
            if (band <= CohesionBand.Breaking)
                colour.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_now * 6f));

            tag.BarFillMaterial.color = colour;
        }

        /// <summary>A new pending or resolved order restarts the popup clock.</summary>
        private void NoticeOrderChanges(Tag tag, BattleSquad squad)
        {
            bool pending = squad.HasPendingOrder;

            if (pending && !tag.SeenPending) tag.PopupUntil = _now + PopupSeconds;

            if (squad.Order != tag.SeenOrder || squad.Formation != tag.SeenFormation)
            {
                tag.SeenOrder = squad.Order;
                tag.SeenFormation = squad.Formation;
                tag.PopupUntil = _now + PopupSeconds;
            }

            tag.SeenPending = pending;
        }

        private void Style(Tag tag, BattleSquad squad)
        {
            bool selected = _commands != null && _commands.IsSelected(tag.Index);

            if (squad.IsRouted || squad.IsWithdrawn)
            {
                // A rout PULSES. Amid a hundred moving men, a steady red word is invisible;
                // a blinking one is a hand on the player's shoulder.
                Color broken = BrokenColour;
                if (squad.IsRouted) broken.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_now * 5f));

                tag.Numeral.color = broken;
                tag.Order.color = broken;
                tag.Order.text = squad.IsRouted ? "ROUTED" : "WITHDRAWN";
                return;
            }

            tag.Numeral.color = selected ? SelectedColour : IdleColour;

            // The popup: while the order is travelling it shows in amber with an ellipsis; once the
            // squad picks it up it shows plainly for a few seconds; then only the numeral remains.
            if (squad.HasPendingOrder)
            {
                tag.Order.color = PendingColour;
                SquadOrder incoming = squad.PendingOrder ?? squad.Order;
                FormationType incomingFormation = squad.PendingFormation ?? squad.Formation;
                tag.Order.text = $"… {Describe(incoming, incomingFormation)}";
            }
            else if (_now < tag.PopupUntil)
            {
                tag.Order.color = selected ? SelectedColour : IdleColour;
                tag.Order.text = Describe(squad.Order, squad.Formation);
            }
            else
            {
                tag.Order.text = selected ? Describe(squad.Order, squad.Formation) : string.Empty;
                tag.Order.color = SelectedColour;
            }
        }

        private static string Describe(SquadOrder order, FormationType formation)
        {
            string verb;
            switch (order)
            {
                case SquadOrder.Advance: verb = "ADVANCE"; break;
                case SquadOrder.HoldPosition: verb = "HOLD"; break;
                case SquadOrder.FollowMe: verb = "FOLLOW"; break;
                case SquadOrder.Skirmish: verb = "SKIRMISH"; break;
                case SquadOrder.Fallback: verb = "FALL BACK"; break;
                case SquadOrder.Retreat: verb = "RETREAT"; break;
                default: verb = order.ToString().ToUpperInvariant(); break;
            }

            return $"{verb} · {formation.ToString().ToUpperInvariant()}";
        }

        private static string ShortName(BattleSquad squad)
        {
            string name = squad.DisplayName ?? "";
            int cut = name.LastIndexOf(' ');
            return (cut >= 0 && cut < name.Length - 1 ? name.Substring(cut + 1) : name).ToUpperInvariant();
        }
    }
}
