using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// Binds battle state to the battle HUD document: officer roster, squad state, the alert banner
    /// and the order bar.
    /// </summary>
    /// <remarks>
    /// The order bar is wired to the same code path as the keyboard, through
    /// <see cref="SquadCommandInput"/>, rather than duplicating the order logic. Two input surfaces
    /// issuing orders by different routes is how the mouse and keyboard end up disagreeing about
    /// what a squad was told.
    ///
    /// Squad and officer rows are built once and then updated in place. Rebuilding the hierarchy every
    /// frame would work at this scale but produces visible churn on hover states and tooltips.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleHudController : MonoBehaviour
    {
        [SerializeField] private float _refreshInterval = 0.1f;
        [SerializeField] private int _visibleLogCount = 5;

        [Header("Alert")]
        [Tooltip("Pulses per second on the alert banner. Zero disables the pulse.")]
        [SerializeField] private float _alertPulseHz = 1.4f;

        private UIDocument _document;
        private BattleState _state;
        private BattleSettings _settings;
        private SquadCommandInput _commands;
        private BattleEventFeed _feed;
        private PlayerCharacterController _player;

        private float _accumulator;
        private bool _bound;

        // Cached elements.
        private Label _engagementTitle, _engagementObjective;
        private Label _statSurvivors, _statKilled, _statReserve, _statEnemy;
        private VisualElement _alert, _alertFrame;
        private Label _alertHeadline, _alertSub;
        private VisualElement _roster, _squadList, _logList;
        private Label _commanderName, _commanderStatus;
        private VisualElement _commanderHealth, _commanderStamina;
        private Label _cmdFormationLabel, _selectionText, _battleTimer;

        private readonly List<OfficerCard> _officerCards = new List<OfficerCard>();
        private readonly List<Contubernium> _contubernia = new List<Contubernium>();

        private void Awake() => _document = GetComponent<UIDocument>();

        /// <summary>Shows or hides the whole fighting interface. See <see cref="BattleHudPanels"/>.</summary>
        public void SetCombatUiVisible(bool visible) =>
            BattleHudPanels.SetCombatVisible(
                _document != null ? _document.rootVisualElement : null, visible);

        /// <summary>Called by <see cref="BattleBootstrap"/> once the battle exists.</summary>
        public void Bind(
            BattleState state, BattleSettings settings, SquadCommandInput commands,
            BattleEventFeed feed, PlayerCharacterController player)
        {
            _state = state;
            _settings = settings;
            _commands = commands;
            _feed = feed;
            _player = player;

            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogWarning("[BattleHud] UIDocument has no root. Is the UXML assigned?", this);
                return;
            }

            CacheElements(root);
            BindOrderBar(root);
            BuildOfficerCards();
            BuildContubernia();

            _bound = true;
            Refresh();
        }

        private void CacheElements(VisualElement root)
        {
            _engagementTitle = Find<Label>(root, "engagement-title");
            _engagementObjective = Find<Label>(root, "engagement-objective");
            _statSurvivors = Find<Label>(root, "stat-survivors");
            _statKilled = Find<Label>(root, "stat-killed");
            _statReserve = Find<Label>(root, "stat-reserve");
            _statEnemy = Find<Label>(root, "stat-enemy");

            _alert = Find<VisualElement>(root, "alert");
            _alertFrame = Find<VisualElement>(root, "alert-frame");
            _alertHeadline = Find<Label>(root, "alert-headline");
            _alertSub = Find<Label>(root, "alert-sub");

            _roster = Find<VisualElement>(root, "roster");
            _squadList = Find<VisualElement>(root, "squad-list");
            _logList = Find<VisualElement>(root, "log-list");

            _commanderName = Find<Label>(root, "commander-name");
            _commanderStatus = Find<Label>(root, "commander-status");
            _commanderHealth = Find<VisualElement>(root, "commander-health");
            _commanderStamina = Find<VisualElement>(root, "commander-stamina");

            _cmdFormationLabel = Find<Label>(root, "cmd-formation-label");
            _selectionText = Find<Label>(root, "selection-text");
            _battleTimer = Find<Label>(root, "battle-timer");
        }

        private static T Find<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
                Debug.LogWarning($"[BattleHud] UXML element '{name}' not found. Check BattleHud.uxml.");
            return element;
        }

        // --- Order bar ------------------------------------------------------------------------

        private readonly Dictionary<SquadOrder, Button> _orderButtons = new Dictionary<SquadOrder, Button>();
        private Button _formationButton;
        private Button _summonButton;
        private VisualElement _formationMenu;

        private void BindOrderBar(VisualElement root)
        {
            _formationButton = Hook(root, "cmd-formation", () => _commands?.ToggleFormationMenu());

            // The formation picker: key 1's menu, mirrored for the mouse.
            _formationMenu = root.Q<VisualElement>("formation-menu");
            Hook(root, "form-line", () => _commands?.RequestFormation(FormationType.Line));
            Hook(root, "form-testudo", () => _commands?.RequestFormation(FormationType.Testudo));
            Hook(root, "form-wedge", () => _commands?.RequestFormation(FormationType.Wedge));
            Hook(root, "form-loose", () => _commands?.RequestFormation(FormationType.Loose));
            Hook(root, "form-column", () => _commands?.RequestFormation(FormationType.Column));
            _orderButtons[SquadOrder.Advance] = Hook(root, "cmd-advance", () => _commands?.RequestOrder(SquadOrder.Advance));
            _orderButtons[SquadOrder.HoldPosition] = Hook(root, "cmd-hold", () => _commands?.RequestOrder(SquadOrder.HoldPosition));
            _orderButtons[SquadOrder.FollowMe] = Hook(root, "cmd-follow", () => _commands?.RequestOrder(SquadOrder.FollowMe));
            _orderButtons[SquadOrder.Skirmish] = Hook(root, "cmd-skirmish", () => _commands?.RequestOrder(SquadOrder.Skirmish));
            _orderButtons[SquadOrder.Fallback] = Hook(root, "cmd-fallback", () => _commands?.RequestOrder(SquadOrder.Fallback));
            _orderButtons[SquadOrder.Retreat] = Hook(root, "cmd-retreat", () => _commands?.RequestOrder(SquadOrder.Retreat));
            _summonButton = Hook(root, "cmd-summon", () => _commands?.RequestSummon());

            // Flash whichever button carried the order, keyboard or click alike, so every command
            // visibly registers somewhere even when the squads take a moment to obey it.
            if (_commands != null) _commands.OrderIssued += OnOrderIssued;
        }

        private void OnDisable()
        {
            if (_commands != null) _commands.OrderIssued -= OnOrderIssued;
        }

        private void OnOrderIssued(SquadOrder? order)
        {
            Button button = order.HasValue
                ? _orderButtons.TryGetValue(order.Value, out Button b) ? b : null
                : _formationButton;

            if (button == null) return;

            button.AddToClassList("command--flash");
            button.schedule.Execute(() => button.RemoveFromClassList("command--flash")).StartingIn(220);
        }

        private static Button Hook(VisualElement root, string name, System.Action action)
        {
            Button button = root.Q<Button>(name);
            if (button == null) return null;
            button.clicked += action;
            return button;
        }

        // --- Officer roster -------------------------------------------------------------------

        private sealed class OfficerCard
        {
            public VisualElement Root;
            public Label Name;
            public Label Rank;
            public Label Cross;
            public VisualElement Health;
            public VisualElement Stamina;
            public VisualElement Morale;
            public BattleCombatant Combatant;
        }

        /// <summary>
        /// The Centurion first, then every surviving officer. These are the named men — the ones whose
        /// loss the player will actually feel on the next march — so they get the detailed treatment
        /// while the rank and file are represented by squad strength alone.
        /// </summary>
        private void BuildOfficerCards()
        {
            if (_roster == null) return;

            _roster.Clear();
            _officerCards.Clear();

            if (_state.PlayerCharacter != null) _roster.Add(CreateCard(_state.PlayerCharacter, true).Root);

            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                List<BattleCombatant> members = _state.PlayerSquads[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];

                    // Command posts only. Immunis are specialists, not officers — the sim's own
                    // IsOfficer says as much — and their cards were drowning the panel.
                    if (man.Role == OfficerRole.None || man.Role == OfficerRole.Centurion
                        || man.Role == OfficerRole.Immunis) continue;

                    _roster.Add(CreateCard(man, false).Root);
                }
            }
        }

        private OfficerCard CreateCard(BattleCombatant man, bool isCommander)
        {
            var card = new OfficerCard { Combatant = man };

            card.Root = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Root.AddToClassList("card");
            if (isCommander) card.Root.AddToClassList("card--commander");

            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("card__portrait");

            // Role art from the icon pack, in place of the old font glyphs.
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("card__icon");
            icon.AddToClassList($"gi-{IconForRole(man.Role)}");
            portrait.Add(icon);
            card.Root.Add(portrait);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("card__body");

            card.Name = new Label(man.DisplayName) { pickingMode = PickingMode.Ignore };
            card.Name.AddToClassList("card__name");
            body.Add(card.Name);

            card.Rank = new Label(man.Role.ToString().ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            card.Rank.AddToClassList("card__rank");
            body.Add(card.Rank);

            card.Health = AddBar(body, "bar__fill--health");
            card.Stamina = AddBar(body, "bar__fill--stamina");
            card.Morale = AddBar(body, "bar__fill--morale");

            card.Root.Add(body);

            // A cross laid over the whole card when the man falls. Hidden while he lives.
            card.Cross = new Label("✕") { pickingMode = PickingMode.Ignore };
            card.Cross.AddToClassList("card__cross");
            card.Cross.style.display = DisplayStyle.None;
            card.Root.Add(card.Cross);

            _officerCards.Add(card);
            return card;
        }

        private static VisualElement AddBar(VisualElement parent, string fillClass)
        {
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("bar");
            track.AddToClassList("bar--hair");

            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("bar__fill");
            fill.AddToClassList(fillClass);

            track.Add(fill);
            parent.Add(track);
            return fill;
        }

        /// <summary>Icon name (the X of .gi-X) for each command role.</summary>
        private static string IconForRole(OfficerRole role)
        {
            switch (role)
            {
                case OfficerRole.Centurion: return "centurion-helmet";
                case OfficerRole.Optio: return "crested-helmet";
                case OfficerRole.Tesserarius: return "scroll-quill";
                case OfficerRole.Medicus: return "healing";
                case OfficerRole.Signifer: return "vertical-banner";
                case OfficerRole.Immunis: return "toolbox";
                default: return "person";
            }
        }

        // --- Contubernium dots ----------------------------------------------------------------

        private sealed class DotView
        {
            public VisualElement Root;
            public Label Cross;
            public VisualElement Flag;
            public BattleCombatant Man;
            public bool Wounded;
        }

        private sealed class Contubernium
        {
            public VisualElement Root;
            public Label Name;
            public Label Order;
            public readonly List<DotView> Dots = new List<DotView>();
            public BattleSquad Squad;
        }

        /// <summary>
        /// One block per contubernium: a header and a dot for every man, each mapped to a real
        /// combatant so the panel is a live picture of who stands, who is hurt, and who has run.
        /// </summary>
        private void BuildContubernia()
        {
            if (_squadList == null) return;

            _squadList.Clear();
            _contubernia.Clear();

            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                int index = i;

                var block = new Contubernium { Squad = squad };

                block.Root = new VisualElement();
                block.Root.AddToClassList("contubernium");
                // Clicking a block toggles that squad in the selection, mirroring Tab in the world.
                block.Root.RegisterCallback<ClickEvent>(_ => _commands?.ToggleSquad(index));

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("contubernium__head");

                block.Name = new Label(squad.DisplayName) { pickingMode = PickingMode.Ignore };
                block.Name.AddToClassList("contubernium__name");
                head.Add(block.Name);

                block.Order = new Label { pickingMode = PickingMode.Ignore };
                block.Order.AddToClassList("contubernium__order");
                head.Add(block.Order);

                block.Root.Add(head);

                var dotRow = new VisualElement { pickingMode = PickingMode.Ignore };
                dotRow.AddToClassList("dot-row");

                for (int m = 0; m < squad.Members.Count; m++)
                    block.Dots.Add(MakeDot(dotRow, squad.Members[m]));

                block.Root.Add(dotRow);
                _squadList.Add(block.Root);
                _contubernia.Add(block);
            }
        }

        private static DotView MakeDot(VisualElement parent, BattleCombatant man)
        {
            var dot = new DotView { Man = man };

            dot.Root = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.Root.AddToClassList("dot");

            dot.Cross = new Label("✕") { pickingMode = PickingMode.Ignore };
            dot.Cross.AddToClassList("dot__cross");
            dot.Cross.style.display = DisplayStyle.None;
            dot.Root.Add(dot.Cross);

            dot.Flag = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.Flag.AddToClassList("dot__flag");
            dot.Flag.style.display = DisplayStyle.None;
            dot.Root.Add(dot.Flag);

            parent.Add(dot.Root);
            return dot;
        }

        // --- Refresh --------------------------------------------------------------------------

        private void Update()
        {
            if (!_bound || _state == null) return;

            UpdateAlert();
            PulseWoundedDots();
            UpdateFormationMenu();

            _accumulator += Time.deltaTime;
            if (_accumulator < _refreshInterval) return;
            _accumulator = 0f;

            Refresh();
        }

        /// <summary>The picker's visibility lives on the command input; the HUD only mirrors it.</summary>
        private void UpdateFormationMenu()
        {
            if (_formationMenu == null || _commands == null) return;

            bool open = _commands.FormationMenuOpen;
            _formationMenu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _formationButton?.EnableInClassList("command--active", open);
        }

        private void PulseWoundedDots()
        {
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 4f));

            for (int i = 0; i < _contubernia.Count; i++)
            {
                List<DotView> dots = _contubernia[i].Dots;
                for (int d = 0; d < dots.Count; d++)
                    if (dots[d].Wounded) dots[d].Root.style.opacity = pulse;
            }
        }

        private void Refresh()
        {
            RefreshHeader();
            RefreshOfficers();
            RefreshContubernia();
            RefreshCommander();
            RefreshLog();
            RefreshSelection();
        }

        private void RefreshHeader()
        {
            SetText(_engagementTitle, _state.EnemyDisplayName.ToUpperInvariant());
            SetText(_engagementObjective, _state.PlayerAmbushed
                ? "Ambushed on the march. Form up and hold."
                : "Break the enemy and hold the field.");

            SetText(_statSurvivors, _state.PlayerSideAliveCount.ToString());
            SetText(_statReserve, CountOffField().ToString());
            SetText(_statEnemy, _state.EnemyAliveCount.ToString());

            // The summon button exists only while there is anyone left to summon.
            if (_summonButton != null)
                _summonButton.style.display = _commands != null && _commands.HasReinforcements
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            int killed = 0;
            for (int s = 0; s < _state.EnemySquads.Count; s++)
            {
                List<BattleCombatant> members = _state.EnemySquads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (!members[m].IsAlive) killed++;
            }

            SetText(_statKilled, killed.ToString());

            SetText(_battleTimer, FormatClock(_state.ElapsedSeconds));
        }

        private int CountOffField()
        {
            int count = 0;
            for (int i = 0; i < _state.PlayerSquads.Count; i++)
                if (_state.PlayerSquads[i].IsOffField) count += _state.PlayerSquads[i].AliveCount;
            return count;
        }

        private static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private void RefreshOfficers()
        {
            for (int i = 0; i < _officerCards.Count; i++)
            {
                OfficerCard card = _officerCards[i];
                BattleCombatant man = card.Combatant;

                bool alive = man.IsAlive;
                bool wounded = alive && man.Health01 < 0.5f;

                card.Root.EnableInClassList("card--wounded", wounded);
                card.Root.EnableInClassList("card--dead", !alive);
                if (card.Cross != null) card.Cross.style.display = alive ? DisplayStyle.None : DisplayStyle.Flex;

                SetWidth(card.Health, man.Health01);
                SetWidth(card.Stamina, man.Stamina01);
                SetWidth(card.Morale, man.Morale01);

                if (!alive) SetText(card.Rank, "FALLEN");
            }
        }

        private void RefreshContubernia()
        {
            for (int i = 0; i < _contubernia.Count; i++)
            {
                Contubernium block = _contubernia[i];
                BattleSquad squad = block.Squad;

                bool selected = _commands != null && _commands.IsSelected(i);
                block.Root.style.backgroundColor = selected
                    ? new StyleColor(new Color(0.20f, 0.16f, 0.10f, 0.5f))
                    : new StyleColor(Color.clear);

                string order = squad.IsDestroyed
                    ? "WIPED OUT"
                    : squad.IsRouted
                        ? "ROUTED"
                        : squad.IsWithdrawn
                            ? "WITHDRAWN"
                            : squad.IsOffField
                                ? "AWAITING SUMMONS"
                                : $"{squad.Order.ToString().ToUpperInvariant()} · {squad.Formation.ToString().ToUpperInvariant()}";

                SetText(block.Order, order);
                block.Order.EnableInClassList("text-danger", squad.IsRouted || squad.IsDestroyed || squad.IsWithdrawn);

                // The command aura is invisible in the world, so it has to be legible here.
                block.Name.EnableInClassList("text-gold", !squad.IsRouted && squad.UnderCommandAura);

                for (int d = 0; d < block.Dots.Count; d++)
                {
                    DotView dot = block.Dots[d];
                    BattleCombatant man = dot.Man;

                    bool alive = man.IsAlive;
                    dot.Wounded = alive && man.Health01 < 0.5f;
                    bool broken = alive && man.Morale01 < 0.25f;

                    dot.Root.EnableInClassList("dot--wounded", dot.Wounded);
                    dot.Root.EnableInClassList("dot--dead", !alive);
                    dot.Cross.style.display = alive ? DisplayStyle.None : DisplayStyle.Flex;
                    dot.Flag.style.display = broken ? DisplayStyle.Flex : DisplayStyle.None;

                    // The healthy and the dead sit steady; the wounded pulse (see PulseWoundedDots).
                    if (!dot.Wounded) dot.Root.style.opacity = 1f;
                }
            }
        }

        private void RefreshCommander()
        {
            BattleCombatant player = _state.PlayerCharacter;
            if (player == null) return;

            SetText(_commanderName, player.DisplayName);
            SetWidth(_commanderHealth, player.Health01);
            SetWidth(_commanderStamina, player.Stamina01);

            string status;
            if (!player.IsAlive)
            {
                status = "FALLEN";
            }
            else
            {
                string core = _player != null && _player.IsRallying
                    ? "RALLYING THE MEN"
                    : player.IsInCombat
                        ? "IN CONTACT"
                        : $"{player.Kills} SLAIN";

                // Pila remaining rides alongside the status so the player can throw without hunting for it.
                status = _player != null ? $"{core}   ·   {_player.PilaRemaining} PILA" : core;
            }

            SetText(_commanderStatus, status);
            _commanderStatus?.EnableInClassList("text-danger", !player.IsAlive);
        }

        private void RefreshLog()
        {
            if (_logList == null || _feed == null) return;

            _logList.Clear();

            IReadOnlyList<BattleEvent> events = _feed.Events;
            int start = Mathf.Max(0, events.Count - _visibleLogCount);

            for (int i = events.Count - 1; i >= start; i--)
            {
                BattleEvent entry = events[i];

                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("event");
                row.AddToClassList(LogModifier(entry.Kind));

                var glyph = new Label(LogGlyph(entry.Kind)) { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList("event__glyph");
                row.Add(glyph);

                var text = new Label(entry.Message) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("event__text");
                row.Add(text);

                _logList.Add(row);
            }
        }

        private static string LogModifier(BattleEventKind kind)
        {
            switch (kind)
            {
                case BattleEventKind.Warning: return "event--warning";
                case BattleEventKind.Critical: return "event--critical";
                case BattleEventKind.Good: return "event--good";
                default: return "event";
            }
        }

        private static string LogGlyph(BattleEventKind kind)
        {
            switch (kind)
            {
                case BattleEventKind.Warning: return "◆";
                case BattleEventKind.Critical: return "✖";
                case BattleEventKind.Good: return "✚";
                default: return "◦";
            }
        }

        private void RefreshSelection()
        {
            if (_selectionText == null) return;

            FormationType current = _state.PlayerSquads.Count > 0
                ? _state.PlayerSquads[0].Formation
                : FormationType.Line;

            if (_commands == null || _commands.AllSelected)
            {
                _selectionText.text = "ALL SQUADS";
            }
            else if (_commands.SelectedCount > 3)
            {
                // Past a few squads the numeral list outgrows the screen; the count reads better.
                _selectionText.text = $"{_commands.SelectedCount} SQUADS SELECTED";
                for (int i = 0; i < _state.PlayerSquads.Count; i++)
                {
                    if (!_commands.IsSelected(i)) continue;
                    current = _state.PlayerSquads[i].Formation;
                    break;
                }
            }
            else
            {
                // Name the selected squads by their numerals: "CONTUBERNIUM II · IV".
                var names = new System.Text.StringBuilder("CONTUBERNIUM ");
                bool first = true;

                for (int i = 0; i < _state.PlayerSquads.Count; i++)
                {
                    if (!_commands.IsSelected(i)) continue;
                    if (!first) names.Append(" · ");
                    names.Append(ShortName(_state.PlayerSquads[i]));
                    if (first) current = _state.PlayerSquads[i].Formation;
                    first = false;
                }

                _selectionText.text = names.ToString();
            }

            if (_cmdFormationLabel != null) _cmdFormationLabel.text = current.ToString().ToUpperInvariant();
        }

        private static string ShortName(BattleSquad squad)
        {
            string name = squad.DisplayName ?? "";
            int cut = name.LastIndexOf(' ');
            return (cut >= 0 && cut < name.Length - 1 ? name.Substring(cut + 1) : name).ToUpperInvariant();
        }

        // --- Alert ----------------------------------------------------------------------------

        private void UpdateAlert()
        {
            if (_alert == null || _feed == null) return;

            if (!_feed.HasAlert)
            {
                _alert.style.display = DisplayStyle.None;
                return;
            }

            _alert.style.display = DisplayStyle.Flex;

            bool critical = _feed.AlertKind == BattleEventKind.Critical;
            _alertFrame?.EnableInClassList("alert__frame--warning", !critical);
            _alertHeadline?.EnableInClassList("alert__headline--warning", !critical);

            SplitAlert(_feed.AlertMessage, out string headline, out string sub);
            SetText(_alertHeadline, headline.ToUpperInvariant());
            SetText(_alertSub, sub);

            if (_alertPulseHz <= 0f)
            {
                _alert.style.opacity = 1f;
                return;
            }

            // A slow pulse rather than a flash: enough to catch the eye at the edge of vision without
            // becoming the brightest thing on a screen the player needs to read.
            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * _alertPulseHz * Mathf.PI * 2f);
            _alert.style.opacity = pulse;
        }

        /// <summary>Splits "X is breaking! Rally them, Centurion!" into headline and instruction.</summary>
        private static void SplitAlert(string message, out string headline, out string sub)
        {
            int split = message.IndexOf('!');

            if (split < 0 || split >= message.Length - 1)
            {
                headline = message;
                sub = string.Empty;
                return;
            }

            headline = message.Substring(0, split + 1);
            sub = message.Substring(split + 1).Trim();
        }

        // --- Helpers --------------------------------------------------------------------------

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text;
        }

        private static void SetWidth(VisualElement fill, float fraction01)
        {
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(fraction01) * 100f);
        }
    }
}
