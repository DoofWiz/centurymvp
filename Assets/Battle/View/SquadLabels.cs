using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The floating tag above every squad, BOTH sides: numeral, and three stacked slivers — health,
    /// stamina, morale — always readable at a glance. Hovering a squad expands the tag with the
    /// sim's own condition words ("BLOODIED · WINDED · WAVERING") instead of summoning a panel over
    /// the fight: information lives above the men, never on top of the action.
    /// </summary>
    /// <remarks>All code-generated; no scene setup. Player tags also carry the order popup and the
    /// selection highlight; enemy tags stay minimal — name, condition, and ROUTED when it matters.</remarks>
    public sealed class SquadLabels : MonoBehaviour
    {
        private const float PopupSeconds = 3f;
        private const float Height = 3.9f;

        private static readonly Color IdleColour = new Color(0.85f, 0.8f, 0.68f, 0.65f);
        private static readonly Color SelectedColour = new Color(1f, 0.92f, 0.6f, 1f);
        private static readonly Color PendingColour = new Color(0.94f, 0.72f, 0.3f, 1f);
        private static readonly Color BrokenColour = new Color(0.85f, 0.3f, 0.24f, 1f);
        private static readonly Color EnemyColour = new Color(0.85f, 0.5f, 0.42f, 0.7f);

        private const float BarWidth = 1.7f;
        private const float BarHeight = 0.11f;
        private const float BarGap = 0.135f;
        private const float BarsTop = -0.30f;

        private static readonly Color HealthColour = new Color(0.72f, 0.22f, 0.18f);
        private static readonly Color StaminaColour = new Color(0.70f, 0.56f, 0.26f);
        private static readonly Color MoraleSteady = new Color(0.88f, 0.76f, 0.42f);
        private static readonly Color MoraleWavering = new Color(0.92f, 0.58f, 0.22f);
        private static readonly Color MoraleBreaking = new Color(0.85f, 0.28f, 0.2f);

        private sealed class Tag
        {
            public BattleSquad Squad;
            public int Index;               // player squad index, or -1 for enemies
            public bool IsEnemy;
            public Transform Root;
            public TextMesh Numeral;
            public TextMesh Order;
            public TextMesh Detail;         // hover-only condition words

            public Transform[] Fills;       // health, stamina, morale
            public Material[] FillMaterials;

            public SquadOrder SeenOrder;
            public FormationType SeenFormation;
            public bool SeenPending;
            public float PopupUntil;
        }

        private readonly List<Tag> _tags = new List<Tag>();
        private SquadCommandInput _commands;
        private Camera _camera;
        private Font _font;
        private float _now;

        public void Initialise(
            List<BattleSquad> playerSquads, List<BattleSquad> enemySquads, SquadCommandInput commands)
        {
            _commands = commands;
            _camera = Camera.main;

            // A drop-in thematic font is used the moment the designer provides one.
            _font = Resources.Load<Font>("Fonts/CenturyWorld")
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < playerSquads.Count; i++) BuildTag(playerSquads[i], i, isEnemy: false);
            for (int i = 0; i < enemySquads.Count; i++) BuildTag(enemySquads[i], -1, isEnemy: true);
        }

        private void BuildTag(BattleSquad squad, int index, bool isEnemy)
        {
            var tag = new Tag
            {
                Squad = squad,
                Index = index,
                IsEnemy = isEnemy,
                SeenOrder = squad.Order,
                SeenFormation = squad.Formation,
            };

            tag.Root = new GameObject($"SquadTag [{squad.DisplayName}]").transform;
            tag.Root.SetParent(transform, false);

            tag.Numeral = MakeText(tag.Root, _font, 60, 0.07f, Vector3.zero);
            tag.Order = MakeText(tag.Root, _font, 38, 0.05f, new Vector3(0f, 0.42f, 0f));
            tag.Detail = MakeText(tag.Root, _font, 34, 0.045f, new Vector3(0f, -0.86f, 0f));
            tag.Numeral.text = ShortName(squad);
            tag.Detail.text = string.Empty;

            BuildBars(tag);
            _tags.Add(tag);
        }

        /// <summary>Three stacked slivers under the numeral: health, stamina, morale, colour-coded.</summary>
        private void BuildBars(Tag tag)
        {
            tag.Fills = new Transform[3];
            tag.FillMaterials = new Material[3];

            for (int i = 0; i < 3; i++)
            {
                float y = BarsTop - i * BarGap;

                Material back = Century.Core.World.FxMaterials.VertexTinted();
                back.color = new Color(0.05f, 0.05f, 0.06f, 0.7f);
                Transform backQuad = MakeBarQuad(tag.Root, "BarBack", back);
                backQuad.localPosition = new Vector3(0f, y, 0.01f);
                backQuad.localScale = new Vector3(BarWidth, BarHeight, 1f);

                tag.FillMaterials[i] = Century.Core.World.FxMaterials.VertexTinted();
                tag.FillMaterials[i].color = i == 0 ? HealthColour : i == 1 ? StaminaColour : MoraleSteady;
                tag.Fills[i] = MakeBarQuad(tag.Root, "BarFill", tag.FillMaterials[i]);
                tag.Fills[i].localPosition = new Vector3(0f, y, 0f);
                tag.Fills[i].localScale = new Vector3(BarWidth, BarHeight, 1f);
            }
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

                if (squad.IsDestroyed || squad.IsOffField || squad.IsWithdrawn)
                {
                    tag.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!tag.Root.gameObject.activeSelf) tag.Root.gameObject.SetActive(true);
                tag.Root.position = squad.CentreOfMass() + Vector3.up * Height;
                tag.Root.rotation = facing;

                bool hovered = IsHovered(tag);

                if (!tag.IsEnemy) NoticeOrderChanges(tag, squad);
                Style(tag, squad, hovered);
                StyleBars(tag, squad);
                StyleDetail(tag, squad, hovered);
            }
        }

        private bool IsHovered(Tag tag)
        {
            if (_commands == null) return false;
            return tag.IsEnemy
                ? _commands.HoveredEnemySquad == tag.Squad
                : _commands.HoveredSquadIndex == tag.Index;
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

        private void Style(Tag tag, BattleSquad squad, bool hovered)
        {
            if (squad.IsRouted)
            {
                // A rout PULSES: a steady red word is invisible in a moving crowd.
                Color broken = BrokenColour;
                broken.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_now * 5f));
                tag.Numeral.color = broken;
                tag.Order.color = broken;
                tag.Order.text = "ROUTED";
                return;
            }

            if (tag.IsEnemy)
            {
                Color colour = EnemyColour;
                if (hovered) colour.a = 1f;
                tag.Numeral.color = colour;
                tag.Order.text = string.Empty;
                return;
            }

            bool selected = _commands != null && _commands.IsSelected(tag.Index);
            tag.Numeral.color = selected || hovered ? SelectedColour : IdleColour;

            // The popup: amber while the command is travelling, plain once enacted, then quiet.
            if (squad.HasPendingOrder)
            {
                tag.Order.color = PendingColour;
                SquadOrder incoming = squad.PendingOrder ?? squad.Order;
                FormationType incomingFormation = squad.PendingFormation ?? squad.Formation;
                tag.Order.text = $"… {Describe(incoming, incomingFormation)}";
            }
            else if (_now < tag.PopupUntil || selected || hovered)
            {
                tag.Order.color = selected || hovered ? SelectedColour : IdleColour;
                tag.Order.text = Describe(squad.Order, squad.Formation);
            }
            else
            {
                tag.Order.text = string.Empty;
            }
        }

        private void StyleBars(Tag tag, BattleSquad squad)
        {
            SetFill(tag, 0, AverageHealth01(squad), HealthColour, pulse: false);
            SetFill(tag, 1, squad.AverageStamina01, StaminaColour, pulse: false);

            CohesionBand band = squad.Band;
            Color morale = band >= CohesionBand.Confident ? MoraleSteady
                : band == CohesionBand.Wavering ? MoraleWavering
                : MoraleBreaking;
            SetFill(tag, 2, squad.Cohesion01, morale, pulse: band <= CohesionBand.Breaking);
        }

        private void SetFill(Tag tag, int index, float value01, Color colour, bool pulse)
        {
            float width = BarWidth * Mathf.Max(0.02f, Mathf.Clamp01(value01));
            Transform fill = tag.Fills[index];
            fill.localScale = new Vector3(width, BarHeight, 1f);

            Vector3 position = fill.localPosition;
            position.x = -(BarWidth - width) * 0.5f;
            fill.localPosition = position;

            if (pulse) colour.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_now * 6f));
            tag.FillMaterials[index].color = colour;
        }

        /// <summary>The hover expansion: the sim's own words, in the tag, never over the fight.</summary>
        private void StyleDetail(Tag tag, BattleSquad squad, bool hovered)
        {
            if (!hovered || squad.IsRouted)
            {
                tag.Detail.text = string.Empty;
                return;
            }

            tag.Detail.color = tag.IsEnemy ? EnemyColour : SelectedColour;
            tag.Detail.text =
                $"{squad.AliveCount}/{squad.Members.Count}  ·  {HealthWord(AverageHealth01(squad))}" +
                $"  ·  {StaminaProfile.FromStamina(squad.AverageStamina01).ToString().ToUpperInvariant()}" +
                $"  ·  {squad.Band.ToString().ToUpperInvariant()}";
        }

        private static float AverageHealth01(BattleSquad squad)
        {
            float total = 0f;
            int alive = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                if (!squad.Members[i].IsAlive) continue;
                total += squad.Members[i].Health01;
                alive++;
            }
            return alive <= 0 ? 0f : total / alive;
        }

        private static string HealthWord(float health01)
        {
            if (health01 >= 0.85f) return "FIGHTING FIT";
            if (health01 >= 0.55f) return "BLOODIED";
            if (health01 >= 0.3f) return "MAULED";
            return "DYING";
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

            return $"{verb} · {formation.Word()}";
        }

        private static string ShortName(BattleSquad squad)
        {
            string name = squad.DisplayName ?? "";
            int cut = name.LastIndexOf(' ');
            return (cut >= 0 && cut < name.Length - 1 ? name.Substring(cut + 1) : name).ToUpperInvariant();
        }
    }
}
