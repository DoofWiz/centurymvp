using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The army at a glance, available anywhere on the march: first THE ESTABLISHMENT — the
    /// century's offices with their holders, or their vacancies named in red — then every
    /// contubernium with its decanus and men. Read-only by design: this screen answers "what is
    /// my army?", and the camp is where the player acts on the answer (army brief §1, §4.2).
    /// </summary>
    public sealed class ArmyScreen
    {
        private readonly CampaignState _state;

        private readonly VisualElement _modal;
        private readonly Label _summary;
        private readonly ScrollView _body;

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
            Render();
        }

        public void Close()
        {
            if (_modal != null) _modal.style.display = DisplayStyle.None;
        }

        private void Render()
        {
            Roster roster = _state.PlayerParty?.Roster;
            if (roster == null || _body == null) return;

            _state.Posts.EnsureSeeded(roster, _state.Clock.Now.DayNumber);
            ContuberniumLedger.EnsureAssigned(roster);

            int wounded = 0;
            for (int i = 0; i < roster.Soldiers.Count; i++)
                if (roster.Soldiers[i].IsAlive && roster.Soldiers[i].IsWounded) wounded++;

            if (_summary != null)
                _summary.text = $"{roster.ActiveCount} men under the standard  ·  " +
                                $"{roster.CombatReadyCount} fit to fight  ·  {wounded} wounded";

            _body.Clear();
            RenderEstablishment(roster);
            RenderContubernia(roster);
        }

        // --- The establishment -------------------------------------------------------------------

        private void RenderEstablishment(Roster roster)
        {
            AddHeading("THE ESTABLISHMENT");

            // The same tradition cards the camp invests through, read-only here: the points
            // banner names what is waiting and the camp is where it gets spent (brief §4.2).
            _body.Add(OfficeCards.BuildRow(_state, roster, null));
        }

        // --- The contubernia ---------------------------------------------------------------------

        private void RenderContubernia(Roster roster)
        {
            AddHeading("THE CONTUBERNIA");

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("squad-grid");

            for (int group = 0; group < 16; group++)
            {
                var members = new System.Collections.Generic.List<SoldierRecord>();
                for (int i = 0; i < roster.Soldiers.Count; i++)
                {
                    SoldierRecord man = roster.Soldiers[i];
                    if (man.IsAlive && man.Contubernium == group) members.Add(man);
                }

                if (members.Count == 0) continue;

                grid.Add(MakeSquadCard(group, members));
            }

            _body.Add(grid);
        }

        /// <summary>One tent group as a card: numeral, strength pips (fit / wounded / empty
        /// bunk), the decanus, and the men — strength readable before a single name is.</summary>
        private static VisualElement MakeSquadCard(
            int group, System.Collections.Generic.List<SoldierRecord> members)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
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

            card.Add(pips);

            SoldierRecord decanus = FindDecanus(members);
            var lead = new Label(decanus != null
                ? $"Decanus — {decanus.DisplayName}"
                : "No decanus appointed") { pickingMode = PickingMode.Ignore };
            lead.AddToClassList("squad-card__decanus");
            if (decanus == null) lead.AddToClassList("squad-card__decanus--none");
            card.Add(lead);

            var line = new System.Text.StringBuilder();
            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0) line.Append(", ");
                line.Append(members[i].DisplayName);
                if (members[i].IsWounded) line.Append(" (wounded)");
            }

            var men = new Label(line.ToString()) { pickingMode = PickingMode.Ignore };
            men.AddToClassList("squad-card__men");
            card.Add(men);

            return card;
        }

        private static SoldierRecord FindDecanus(System.Collections.Generic.List<SoldierRecord> members)
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

        private static string ToRoman(int value)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
                                  "XI", "XII", "XIII", "XIV", "XV", "XVI" };
            return value >= 1 && value <= numerals.Length ? numerals[value - 1] : value.ToString();
        }
    }
}
