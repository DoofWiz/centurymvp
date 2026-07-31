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

            foreach (string post in PostId.Tier1)
            {
                SoldierRecord holder = _state.Posts.HolderOf(post, roster);

                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("doctrine-row");

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("doctrine-row__text");

                var name = new Label(
                    holder != null
                        ? $"{PostRoster.DisplayName(post).ToUpperInvariant()}  —  {holder.DisplayName}"
                        : $"{PostRoster.DisplayName(post).ToUpperInvariant()}  —  VACANT")
                    { pickingMode = PickingMode.Ignore };
                name.AddToClassList("doctrine-row__name");
                if (holder == null) name.AddToClassList("text-danger");
                text.Add(name);

                var detail = new Label(
                    holder != null
                        ? $"{PostRoster.Charge(post)}  ·  {holder.Kills} kills, {holder.BattlesSurvived} battles" +
                          (holder.IsWounded ? "  ·  WOUNDED" : "")
                        : PostRoster.VacancyPenalty(post))
                    { pickingMode = PickingMode.Ignore };
                detail.AddToClassList("doctrine-row__effect");
                if (holder == null) detail.AddToClassList("text-danger");
                text.Add(detail);

                // The office's traditions travel with the standard; the ARMY screen shows them
                // anywhere, the camp is where points are spent (brief §4.2).
                PostRecord record = _state.Posts.Find(post);
                if (record != null && (record.InvestedNodeIds.Count > 0 || record.UnspentPoints >= 1f))
                {
                    var names = new System.Text.StringBuilder("Traditions: ");
                    bool any = false;

                    foreach (PostNodeDef node in PostTreeCatalog.For(post))
                    {
                        if (!record.InvestedNodeIds.Contains(node.Id)) continue;
                        if (any) names.Append(", ");
                        names.Append(node.Name);
                        any = true;
                    }

                    if (!any) names.Append("none yet");

                    int points = Mathf.FloorToInt(record.UnspentPoints);
                    if (points > 0) names.Append($"  ·  {points} to invest in camp");

                    var traditions = new Label(names.ToString()) { pickingMode = PickingMode.Ignore };
                    traditions.AddToClassList("doctrine-row__effect");
                    text.Add(traditions);
                }

                row.Add(text);
                _body.Add(row);
            }
        }

        // --- The contubernia ---------------------------------------------------------------------

        private void RenderContubernia(Roster roster)
        {
            AddHeading("THE CONTUBERNIA");

            for (int group = 0; group < 16; group++)
            {
                var members = new System.Collections.Generic.List<SoldierRecord>();
                for (int i = 0; i < roster.Soldiers.Count; i++)
                {
                    SoldierRecord man = roster.Soldiers[i];
                    if (man.IsAlive && man.Contubernium == group) members.Add(man);
                }

                if (members.Count == 0) continue;

                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("doctrine-row");

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("doctrine-row__text");

                SoldierRecord decanus = FindDecanus(members);
                var title = new Label(
                    $"CONTUBERNIUM {ToRoman(group + 1)}  ·  {members.Count}/{ContuberniumLedger.Size}" +
                    (decanus != null ? $"  ·  Decanus: {decanus.DisplayName}" : "  ·  no decanus"))
                    { pickingMode = PickingMode.Ignore };
                title.AddToClassList("doctrine-row__name");
                text.Add(title);

                var line = new System.Text.StringBuilder();
                for (int i = 0; i < members.Count; i++)
                {
                    if (i > 0) line.Append("   ");
                    line.Append(members[i].DisplayName);
                    if (members[i].IsWounded) line.Append(" (wounded)");
                }

                var men = new Label(line.ToString()) { pickingMode = PickingMode.Ignore };
                men.AddToClassList("doctrine-row__effect");
                text.Add(men);

                row.Add(text);
                _body.Add(row);
            }
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
