using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The army at a glance, available anywhere on the march — and navigable: THE ESTABLISHMENT's
    /// holders open the man himself, a contubernium opens its squad page, a squad's men open their
    /// records, and a bond in a man's record walks the web to the other man. Read-only by design:
    /// this screen answers "what is my army and what does it think?"; the camp is where the player
    /// acts on the answer (army brief §1, §4.2).
    /// </summary>
    public sealed class ArmyScreen
    {
        private enum ViewKind { Root, Squad, Man }

        private readonly CampaignState _state;

        private readonly VisualElement _modal;
        private readonly Label _summary;
        private readonly ScrollView _body;

        private ViewKind _view = ViewKind.Root;
        private int _openGroup = -1;
        private string _openSoldierId;

        /// <summary>Where BACK leads: the trail of (view, group, soldier) behind the current page.</summary>
        private readonly Stack<(ViewKind view, int group, string soldierId)> _trail =
            new Stack<(ViewKind, int, string)>();

        public bool IsOpen => _modal != null && _modal.style.display == DisplayStyle.Flex;

        public ArmyScreen(VisualElement root, CampaignState state)
        {
            _state = state;
            _modal = root.Q<VisualElement>("army-modal");
            _summary = root.Q<Label>("army-summary");
            _body = root.Q<ScrollView>("army-body");
        }

        public void Open()
        {
            if (_modal == null) return;
            _modal.style.display = DisplayStyle.Flex;

            _view = ViewKind.Root;
            _trail.Clear();
            Render();
        }

        public void Close()
        {
            if (_modal != null) _modal.style.display = DisplayStyle.None;
        }

        // --- Navigation --------------------------------------------------------------------------

        private void Navigate(ViewKind view, int group = -1, string soldierId = null)
        {
            _trail.Push((_view, _openGroup, _openSoldierId));
            _view = view;
            _openGroup = group;
            _openSoldierId = soldierId;
            Render();
        }

        private void NavigateBack()
        {
            if (_trail.Count == 0) return;
            (_view, _openGroup, _openSoldierId) = _trail.Pop();
            Render();
        }

        private void Render()
        {
            Roster roster = _state.PlayerParty?.Roster;
            if (roster == null || _body == null) return;

            _state.Posts.EnsureSeeded(roster, _state.Clock.Now.DayNumber, _state.PlayerParty?.Appointments);
            ContuberniumLedger.EnsureAssigned(roster);

            int wounded = 0;
            for (int i = 0; i < roster.Soldiers.Count; i++)
                if (roster.Soldiers[i].IsAlive && roster.Soldiers[i].IsWounded) wounded++;

            if (_summary != null)
                _summary.text = $"{roster.ActiveCount} men under the standard  ·  " +
                                $"{roster.CombatReadyCount} fit to fight  ·  {wounded} wounded";

            _body.Clear();

            if (_trail.Count > 0)
            {
                var back = new Button { text = "<  BACK" };
                back.AddToClassList("army-back");
                back.clicked += NavigateBack;
                _body.Add(back);
            }

            switch (_view)
            {
                case ViewKind.Squad:
                    RenderSquad(roster, _openGroup);
                    break;
                case ViewKind.Man:
                    RenderMan(roster, _openSoldierId);
                    break;
                default:
                    RenderEstablishment(roster);
                    RenderContubernia(roster);
                    break;
            }
        }

        // --- The establishment -------------------------------------------------------------------

        private void RenderEstablishment(Roster roster)
        {
            AddHeading("THE ESTABLISHMENT");

            // The same tradition cards the camp invests through, read-only here — except the
            // holders' names, which open the men themselves.
            _body.Add(OfficeCards.BuildRow(_state, roster, null,
                soldier => Navigate(ViewKind.Man, soldierId: soldier.Id)));
        }

        // --- The contubernia ---------------------------------------------------------------------

        private void RenderContubernia(Roster roster)
        {
            AddHeading("THE CONTUBERNIA");

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("squad-grid");

            for (int group = 0; group < 16; group++)
            {
                List<SoldierRecord> members = CollectMembers(roster, group);
                if (members.Count == 0) continue;

                grid.Add(MakeSquadCard(group, members));
            }

            _body.Add(grid);
        }

        /// <summary>One tent group as a clickable card: numeral, strength pips (fit / wounded /
        /// empty bunk), the decanus — strength readable before a single name is.</summary>
        private VisualElement MakeSquadCard(int group, List<SoldierRecord> members)
        {
            var card = new Button();
            card.AddToClassList("squad-card");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("squad-card__head");

            var title = new Label($"CONTUBERNIUM {ToRoman(group + 1)}") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("squad-card__title");
            head.Add(title);

            var count = new Label($"{members.Count}/{ContuberniumLedger.Size}") { pickingMode = PickingMode.Ignore };
            count.AddToClassList("squad-card__count");
            head.Add(count);

            card.Add(head);
            card.Add(MakePipRow(members));

            SoldierRecord decanus = FindDecanus(members);
            var lead = new Label(decanus != null
                ? $"Decanus — {decanus.DisplayName}"
                : "No decanus appointed") { pickingMode = PickingMode.Ignore };
            lead.AddToClassList("squad-card__decanus");
            if (decanus == null) lead.AddToClassList("squad-card__decanus--none");
            card.Add(lead);

            int captured = group;
            card.clicked += () => Navigate(ViewKind.Squad, captured);
            return card;
        }

        private static VisualElement MakePipRow(List<SoldierRecord> members)
        {
            // Wounded pips gather at the right so the healthy block reads as one bar.
            var pips = new VisualElement { pickingMode = PickingMode.Ignore };
            pips.AddToClassList("pip-row");

            int fit = 0, wounded = 0;
            for (int i = 0; i < members.Count; i++)
                if (members[i].IsWounded) wounded++; else fit++;

            for (int slot = 0; slot < ContuberniumLedger.Size; slot++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("pip");
                if (slot >= fit + wounded) pip.AddToClassList("pip--empty");
                else if (slot >= fit) pip.AddToClassList("pip--wounded");
                pips.Add(pip);
            }

            return pips;
        }

        // --- One squad ---------------------------------------------------------------------------

        private void RenderSquad(Roster roster, int group)
        {
            List<SoldierRecord> members = CollectMembers(roster, group);

            AddHeading($"CONTUBERNIUM {ToRoman(group + 1)}");
            _body.Add(MakePipRow(members));

            SoldierRecord decanus = FindDecanus(members);
            var lead = new Label(decanus != null
                ? $"Decanus — {decanus.DisplayName}"
                : "No decanus appointed") { pickingMode = PickingMode.Ignore };
            lead.AddToClassList("squad-card__decanus");
            if (decanus == null) lead.AddToClassList("squad-card__decanus--none");
            _body.Add(lead);

            for (int i = 0; i < members.Count; i++)
            {
                SoldierRecord man = members[i];

                var row = new Button();
                row.AddToClassList("pick-row");

                var disc = new VisualElement { pickingMode = PickingMode.Ignore };
                disc.AddToClassList("tier-disc");
                disc.AddToClassList(TierDiscClass(man));
                disc.AddToClassList("tier-disc--pick");
                row.Add(disc);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.style.flexGrow = 1f;

                var name = new Label(man.DisplayName + (man.IsDecanus ? "  ·  Decanus" : string.Empty))
                    { pickingMode = PickingMode.Ignore };
                name.AddToClassList("pick-row__name");
                text.Add(name);

                var detail = new Label(
                    $"{man.Tier} · {man.Kills} kills · {man.BattlesSurvived} fights" +
                    (man.IsWounded ? " · WOUNDED" : string.Empty))
                    { pickingMode = PickingMode.Ignore };
                detail.AddToClassList("pick-row__detail");
                text.Add(detail);
                row.Add(text);

                var attitude = new Label(RelationshipLedger.AttitudeWord(man.Loyalty01))
                    { pickingMode = PickingMode.Ignore };
                attitude.AddToClassList("pick-row__detail");
                attitude.AddToClassList(RelationshipLedger.AttitudeColorClass(man.Loyalty01));
                row.Add(attitude);

                string capturedId = man.Id;
                row.clicked += () => Navigate(ViewKind.Man, group, capturedId);
                _body.Add(row);
            }
        }

        // --- One man -----------------------------------------------------------------------------

        private void RenderMan(Roster roster, string soldierId)
        {
            SoldierRecord man = roster.Find(soldierId);
            if (man == null) return;

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("army-man__head");

            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.AddToClassList("tier-disc");
            disc.AddToClassList("tier-disc--large");
            disc.AddToClassList(TierDiscClass(man));
            head.Add(disc);

            var names = new VisualElement { pickingMode = PickingMode.Ignore };

            var name = new Label(man.DisplayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("army-man__name");
            names.Add(name);

            var rank = new Label(RankLine(man)) { pickingMode = PickingMode.Ignore };
            rank.AddToClassList("army-man__rank");
            names.Add(rank);

            head.Add(names);
            _body.Add(head);

            AddDetailRow("ATTITUDE TO YOU",
                RelationshipLedger.AttitudeWord(man.Loyalty01),
                RelationshipLedger.AttitudeColorClass(man.Loyalty01));
            AddDetailRow("CONDITION",
                (man.IsWounded ? "Wounded — " : string.Empty) + FitnessWord(man.Health01),
                man.IsWounded ? "text-danger" : null);
            AddDetailRow("RECORD", $"{man.Kills} kills · {man.BattlesSurvived} battles survived", null);
            if (man.Contubernium >= 0)
                AddDetailRow("TENT GROUP", $"Contubernium {ToRoman(man.Contubernium + 1)}", null);

            AddHeading("BONDS AND GRUDGES");

            var ties = new List<RelationTie>(man.Ties);
            ties.Sort((a, b) => Mathf.Abs(b.Value).CompareTo(Mathf.Abs(a.Value)));

            int shown = 0;
            for (int i = 0; i < ties.Count && shown < 8; i++)
            {
                RelationTie tie = ties[i];
                if (Mathf.Abs(tie.Value) < 0.1f) continue;

                SoldierRecord other = roster.Find(tie.OtherId);
                if (other == null) continue;

                // Each bond is a door to the other man: the web can be walked end to end.
                var row = new Button();
                row.AddToClassList("tie-row");
                row.AddToClassList("tie-row--link");

                var tieHead = new VisualElement { pickingMode = PickingMode.Ignore };
                tieHead.AddToClassList("tie-row__head");

                var otherName = new Label(other.IsAlive ? other.DisplayName : $"{other.DisplayName} (fallen)")
                    { pickingMode = PickingMode.Ignore };
                otherName.AddToClassList("tie-row__name");
                tieHead.Add(otherName);

                var band = new Label(RelationshipLedger.Band(tie.Value)) { pickingMode = PickingMode.Ignore };
                band.AddToClassList("tie-row__band");
                string colour = RelationshipLedger.BandColorClass(tie.Value);
                if (colour != null) band.AddToClassList(colour);
                tieHead.Add(band);

                row.Add(tieHead);

                if (!string.IsNullOrEmpty(tie.Why))
                {
                    var why = new Label(Capitalise(tie.Why)) { pickingMode = PickingMode.Ignore };
                    why.AddToClassList("tie-row__why");
                    row.Add(why);
                }

                if (other.IsAlive)
                {
                    string capturedId = other.Id;
                    row.clicked += () => Navigate(ViewKind.Man, other.Contubernium, capturedId);
                }

                _body.Add(row);
                shown++;
            }

            if (shown == 0)
            {
                var none = new Label("No strong ties yet. Marches and battles will forge them.")
                    { pickingMode = PickingMode.Ignore };
                none.AddToClassList("tie-empty");
                _body.Add(none);
            }
        }

        private string RankLine(SoldierRecord man)
        {
            if (man.RankId == "centurion") return "Centurion — you";
            if (_state.PlayerParty.Appointments.HoldsAnyRole(man.Id, out CampRole role))
                return $"{CampRoleInfo.For(role).Latin} — {man.Tier}";
            return man.Tier.ToString();
        }

        private string TierDiscClass(SoldierRecord man)
        {
            if (man.RankId == "centurion") return "tier-disc--officer";
            if (_state.PlayerParty.Appointments.HoldsAnyRole(man.Id, out _)) return "tier-disc--officer";

            switch (man.Tier)
            {
                case VeterancyTier.Evocatus: return "tier-disc--evocatus";
                case VeterancyTier.Veteranus: return "tier-disc--veteranus";
                case VeterancyTier.Miles: return "tier-disc--miles";
                default: return "tier-disc--tiro";
            }
        }

        private static string FitnessWord(float health01)
        {
            if (health01 >= 0.75f) return "Hale";
            if (health01 >= 0.5f) return "Fit";
            if (health01 >= 0.25f) return "Hurt";
            return "In a bad way";
        }

        private void AddDetailRow(string label, string value, string valueClass)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("detail-row");

            var key = new Label(label) { pickingMode = PickingMode.Ignore };
            key.AddToClassList("detail-label");
            row.Add(key);

            var val = new Label(value) { pickingMode = PickingMode.Ignore };
            val.AddToClassList("detail-value");
            if (valueClass != null) val.AddToClassList(valueClass);
            row.Add(val);

            _body.Add(row);
        }

        // --- Shared helpers ----------------------------------------------------------------------

        private static List<SoldierRecord> CollectMembers(Roster roster, int group)
        {
            var members = new List<SoldierRecord>();
            for (int i = 0; i < roster.Soldiers.Count; i++)
            {
                SoldierRecord man = roster.Soldiers[i];
                if (man.IsAlive && man.Contubernium == group) members.Add(man);
            }
            return members;
        }

        private static SoldierRecord FindDecanus(List<SoldierRecord> members)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i].IsDecanus) return members[i];
            return null;
        }

        private void AddHeading(string title)
        {
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("inv-section");

            var label = new Label(title) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("heading");
            head.Add(label);

            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.AddToClassList("rule");
            rule.AddToClassList("rule--short");
            head.Add(rule);

            _body.Add(head);
        }

        private static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static string ToRoman(int value)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
                                  "XI", "XII", "XIII", "XIV", "XV", "XVI" };
            return value >= 1 && value <= numerals.Length ? numerals[value - 1] : value.ToString();
        }
    }
}
