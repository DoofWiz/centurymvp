using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The commander's screen: who they have become. A header with name, level and the road to the
    /// next doctrine; the identity line the men would give them; then — when a pick is earned — the
    /// OFFER: three doctrine cards dealt from the deck, weighted by how this campaign has actually
    /// been led. Below it, every doctrine already held, by philosophy. Percentages are never shown;
    /// the player reads their identity, not their maths.
    /// </summary>
    /// <remarks>
    /// A plain class over elements inside OvermapHud.uxml, owned by <see cref="OvermapHudController"/>
    /// (which pauses the clock while it is open). No scene setup.
    /// </remarks>
    public sealed class CommanderScreen
    {
        private readonly CampaignState _state;

        private readonly VisualElement _modal;
        private readonly Label _identity, _name, _record, _level;
        private readonly VisualElement _xpFill;
        private readonly ScrollView _body;

        public bool IsOpen => _modal != null && _modal.style.display == DisplayStyle.Flex;

        public CommanderScreen(VisualElement root, CampaignState state)
        {
            _state = state;

            _modal = root.Q<VisualElement>("commander-modal");
            _identity = root.Q<Label>("cmd-identity");
            _name = root.Q<Label>("cmd-name");
            _record = root.Q<Label>("cmd-record");
            _level = root.Q<Label>("cmd-level");
            _xpFill = root.Q<VisualElement>("cmd-xp-fill");
            _body = root.Q<ScrollView>("cmd-body");
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

        // --- Rendering ---------------------------------------------------------------------------

        private void Render()
        {
            SoldierRecord centurion = _state.PlayerParty?.Roster.FindByRank("centurion");
            CommanderProgress progress = _state.Commander;
            if (centurion == null || _body == null) return;

            int xp = centurion.Experience;
            int level = CommanderSkillCatalog.LevelFor(xp);
            float toNext = CommanderSkillCatalog.ProgressToNext(xp, out int into, out int cost);
            int points = CommanderSkillCatalog.SkillPoints(progress, xp);

            if (_name != null) _name.text = centurion.DisplayName;
            if (_record != null)
                _record.text = $"{centurion.Kills} men slain by your hand · " +
                               $"{centurion.BattlesSurvived} battles survived";
            if (_level != null) _level.text = $"LEVEL {level}   ·   {into} / {cost} TO THE NEXT";
            if (_xpFill != null) _xpFill.style.width = Length.Percent(toNext * 100f);
            if (_identity != null) _identity.text = CommanderSkillCatalog.IdentityLine(progress);

            _body.Clear();

            if (points > 0)
            {
                RenderOffer(progress, level, points);
            }
            else
            {
                var quiet = new Label(
                    "No new doctrine to choose. Lead — the campaign will shape what comes next.")
                    { pickingMode = PickingMode.Ignore };
                quiet.AddToClassList("inv-empty");
                _body.Add(quiet);
            }

            RenderOwned(progress);
        }

        private void RenderOffer(CommanderProgress progress, int level, int points)
        {
            // The standing offer persists until spent, so re-opening the screen never re-deals.
            if (progress.CurrentOffer.Count == 0)
                progress.CurrentOffer.AddRange(CommanderSkillCatalog.GenerateOffer(
                    progress, _state.RandomSeed ^ (level * 397) ^ progress.OwnedSkills.Count));

            AddHeading(points > 1
                ? $"CHOOSE A DOCTRINE   ·   {points} PICKS EARNED"
                : "CHOOSE A DOCTRINE");

            var cards = new VisualElement { pickingMode = PickingMode.Ignore };
            cards.AddToClassList("skill-cards");

            for (int i = 0; i < progress.CurrentOffer.Count; i++)
            {
                CommanderSkillDef skill = CommanderSkillCatalog.Find(progress.CurrentOffer[i]);
                if (skill != null) cards.Add(MakeCard(skill));
            }

            _body.Add(cards);
        }

        private VisualElement MakeCard(CommanderSkillDef skill)
        {
            string tone = ToneOf(skill.Philosophy);

            var card = new Button { text = string.Empty };
            card.AddToClassList("skill-card");
            card.AddToClassList($"skill-card--{tone}");

            var philosophy = new Label(PhilosophyName(skill.Philosophy)) { pickingMode = PickingMode.Ignore };
            philosophy.AddToClassList("skill-card__philosophy");
            philosophy.AddToClassList($"skill-card__philosophy--{tone}");
            card.Add(philosophy);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("skill-card__icon");
            icon.AddToClassList($"gi-{skill.Icon}");
            card.Add(icon);

            var name = new Label(skill.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("skill-card__name");
            card.Add(name);

            var flavour = new Label($"“{skill.Flavour}”") { pickingMode = PickingMode.Ignore };
            flavour.AddToClassList("skill-card__flavour");
            card.Add(flavour);

            var effect = new Label(skill.Effect) { pickingMode = PickingMode.Ignore };
            effect.AddToClassList("skill-card__effect");
            card.Add(effect);

            card.clicked += () => Take(skill);
            return card;
        }

        private void Take(CommanderSkillDef skill)
        {
            CommanderProgress progress = _state.Commander;
            if (progress.Has(skill.Id)) return;

            progress.OwnedSkills.Add(skill.Id);
            progress.CurrentOffer.Clear();   // the next pick deals a fresh hand

            // Taking a doctrine is itself an act of identity.
            progress.Note(skill.Philosophy, 0.5f);

            Render();
        }

        private void RenderOwned(CommanderProgress progress)
        {
            AddHeading("DOCTRINES HELD");

            var columns = new VisualElement { pickingMode = PickingMode.Ignore };
            columns.AddToClassList("doctrine-cols");

            columns.Add(MakeColumn(CommanderPhilosophy.TrueRoman, progress));
            columns.Add(MakeColumn(CommanderPhilosophy.Survivor, progress));
            columns.Add(MakeColumn(CommanderPhilosophy.Barbarian, progress));

            _body.Add(columns);
        }

        private VisualElement MakeColumn(CommanderPhilosophy philosophy, CommanderProgress progress)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.AddToClassList("doctrine-col");

            var title = new Label(PhilosophyName(philosophy)) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("skill-card__philosophy");
            title.AddToClassList($"skill-card__philosophy--{ToneOf(philosophy)}");
            column.Add(title);

            int shown = 0;
            for (int i = 0; i < progress.OwnedSkills.Count; i++)
            {
                CommanderSkillDef skill = CommanderSkillCatalog.Find(progress.OwnedSkills[i]);
                if (skill == null || skill.Philosophy != philosophy) continue;

                column.Add(MakeOwnedRow(skill));
                shown++;
            }

            if (shown == 0)
            {
                var none = new Label("No doctrine yet.") { pickingMode = PickingMode.Ignore };
                none.AddToClassList("doctrine-row__effect");
                column.Add(none);
            }

            return column;
        }

        private static VisualElement MakeOwnedRow(CommanderSkillDef skill)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("doctrine-row");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("doctrine-row__icon");
            icon.AddToClassList($"gi-{skill.Icon}");
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("doctrine-row__text");

            var name = new Label(skill.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("doctrine-row__name");
            text.Add(name);

            var effect = new Label(skill.Effect) { pickingMode = PickingMode.Ignore };
            effect.AddToClassList("doctrine-row__effect");
            text.Add(effect);

            row.Add(text);
            return row;
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

        private static string ToneOf(CommanderPhilosophy philosophy)
        {
            switch (philosophy)
            {
                case CommanderPhilosophy.TrueRoman: return "roman";
                case CommanderPhilosophy.Survivor: return "survivor";
                default: return "barbarian";
            }
        }

        private static string PhilosophyName(CommanderPhilosophy philosophy)
        {
            switch (philosophy)
            {
                case CommanderPhilosophy.TrueRoman: return "TRUE ROMAN";
                case CommanderPhilosophy.Survivor: return "SURVIVOR";
                default: return "BARBARIAN";
            }
        }
    }
}
