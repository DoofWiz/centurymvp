using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Contracts;
using Century.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// Binds campaign state to the overmap HUD document.
    /// </summary>
    /// <remarks>
    /// Reads the model and writes to labels; it holds no state of its own beyond cached element
    /// references. Polling on a short interval rather than subscribing to a dozen events is the right
    /// trade for a HUD: a handful of string assignments a few times a second is cheaper than the
    /// plumbing needed to make every field observable, and it cannot go stale.
    ///
    /// Element lookups are all resolved once in OnEnable and logged if missing, so a typo in the UXML
    /// surfaces as a clear console warning instead of a silent dead panel.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OvermapHudController : MonoBehaviour
    {
        [Tooltip("Seconds between HUD refreshes. Four a second is imperceptible and nearly free.")]
        [SerializeField] private float _refreshInterval = 0.25f;

        [SerializeField] private int _visibleEventCount = 5;

        private UIDocument _document;
        private CampaignState _state;
        private CampaignSettings _settings;
        private ITimeControlSource _ticker;
        private CampaignEventLog _log;

        private float _accumulator;
        private bool _bound;

        // Cached elements.
        private Label _unitName, _valueMen, _valueFirewood, _valueFood, _valueEquipment;
        private Label _valueMedicine, _valueCoin, _valueDenarii, _valueMorale;
        private VisualElement _barMorale;
        private VisualElement _partyPortraits, _eventsList;
        private Label _timeReadout;
        private Label _columnPace, _columnOrders, _columnWounded, _columnVeterans;
        private Button _pillCamp, _pillStealth;
        private readonly Dictionary<TimeControl, Button> _timeButtons = new Dictionary<TimeControl, Button>();
        private VisualElement _contactPanel;
        private Label _contactName, _contactDetail;

        // Inventory screen (lives inside this document; the clock pauses while it is open).
        private InventoryScreen _inventory;
        private TimeControl _controlBeforeInventory = TimeControl.Normal;

        // Commander screen, same pattern.
        private CommanderScreen _commander;
        private TimeControl _controlBeforeCommander = TimeControl.Normal;

        // Point-of-interest event popup.
        private VisualElement _poiModal, _poiChoices;
        private Label _poiTitle, _poiKind, _poiBody, _poiOfficer;
        private PointOfInterest _activePoi;
        private TimeControl _controlBeforePoi = TimeControl.Normal;

        // Sighting tooltip (cursor over a warband, or an officer portrait in the bottom bar).
        private VisualElement _hoverTip;
        private Label _hoverTipTitle, _hoverTipDetail;
        private Camera _camera;

        // Runtime UI Toolkit does not render the built-in tooltip, so portrait hovers go through the
        // same cursor tip the world uses. Index of the hovered portrait, or -1.
        private int _hoveredPortrait = -1;
        private readonly string[] _portraitTitles = new string[5];
        private readonly string[] _portraitDetails = new string[5];

        [Tooltip("How close the cursor must be to a warband, in world units, to sight it.")]
        [SerializeField] private float _hoverRadius = 6f;

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            if (_document.rootVisualElement == null) return;
            Bind();
        }

        private void Start()
        {
            if (!_bound) Bind();
        }

        private void Bind()
        {
            VisualElement root = _document.rootVisualElement;
            if (root == null) return;

            // The time controls and camp/stealth toggles are dead without this, and silently so.
            UiInputBootstrapper.EnsureEventSystem();

            if (!ServiceLocator.TryGet(out _state)) return;
            ServiceLocator.TryGet(out _settings);
            ServiceLocator.TryGet(out _ticker);
            ServiceLocator.TryGet(out _log);

            _unitName = Find<Label>(root, "unit-name");
            _valueMen = Find<Label>(root, "value-men");
            _valueFirewood = Find<Label>(root, "value-firewood");
            _valueFood = Find<Label>(root, "value-food");
            _valueEquipment = Find<Label>(root, "value-equipment");
            _valueMedicine = Find<Label>(root, "value-medicine");
            _valueCoin = Find<Label>(root, "value-coin");
            _valueDenarii = Find<Label>(root, "value-denarii");
            _valueMorale = Find<Label>(root, "value-morale");
            _barMorale = Find<VisualElement>(root, "bar-morale");

            _partyPortraits = Find<VisualElement>(root, "party-portraits");
            _eventsList = Find<VisualElement>(root, "events-list");
            _timeReadout = Find<Label>(root, "time-readout");

            _columnPace = Find<Label>(root, "column-pace");
            _columnOrders = Find<Label>(root, "column-orders");
            _columnWounded = Find<Label>(root, "column-wounded");
            _columnVeterans = Find<Label>(root, "column-veterans");

            _contactPanel = Find<VisualElement>(root, "contact-panel");
            _contactName = Find<Label>(root, "contact-name");
            _contactDetail = Find<Label>(root, "contact-detail");

            BindToggles(root);
            BindTimeButtons(root);
            BuildPortraits();

            _hoverTip = Find<VisualElement>(root, "hover-tip");
            _hoverTipTitle = Find<Label>(root, "hover-tip-title");
            _hoverTipDetail = Find<Label>(root, "hover-tip-detail");
            _camera = Camera.main;

            _inventory = new InventoryScreen(root, _state, _settings);
            Button navInventory = Find<Button>(root, "nav-inventory");
            if (navInventory != null) navInventory.clicked += OpenInventory;
            Button invClose = Find<Button>(root, "inv-close");
            if (invClose != null) invClose.clicked += CloseInventory;

            _commander = new CommanderScreen(root, _state);
            Button navCommander = Find<Button>(root, "nav-commander");
            if (navCommander != null) navCommander.clicked += OpenCommander;
            Button cmdClose = Find<Button>(root, "cmd-close");
            if (cmdClose != null) cmdClose.clicked += CloseCommander;

            _poiModal = Find<VisualElement>(root, "poi-modal");
            _poiTitle = Find<Label>(root, "poi-title");
            _poiKind = Find<Label>(root, "poi-kind");
            _poiBody = Find<Label>(root, "poi-body");
            _poiOfficer = Find<Label>(root, "poi-officer");
            _poiChoices = Find<VisualElement>(root, "poi-choices");
            if (_poiModal != null) _poiModal.style.display = DisplayStyle.None;

            OvermapDirectorRunner.PoiTriggered -= OnPoiTriggered;
            OvermapDirectorRunner.PoiTriggered += OnPoiTriggered;

            if (_log != null) _log.Added += OnEventAdded;

            _bound = true;
            Refresh();
            RebuildEvents();
        }

        private void OnDisable()
        {
            if (_log != null) _log.Added -= OnEventAdded;
            OvermapDirectorRunner.PoiTriggered -= OnPoiTriggered;
            _bound = false;
        }

        private static T Find<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
                Debug.LogWarning($"[OvermapHud] UXML element '{name}' not found. Check OvermapHud.uxml.");
            return element;
        }

        // --- Interaction ----------------------------------------------------------------------

        private void BindToggles(VisualElement root)
        {
            _pillCamp = Find<Button>(root, "pill-camp");
            _pillStealth = Find<Button>(root, "pill-stealth");

            if (_pillCamp != null) _pillCamp.clicked += ToggleCamp;
            if (_pillStealth != null) _pillStealth.clicked += ToggleStealth;
        }

        private void ToggleCamp()
        {
            PartyState party = _state?.PlayerParty;
            if (party == null) return;

            // Making camp now means entering the camp scene, where the men are rested and the century
            // is managed. Striking camp happens from there (Break Camp), so this is one-way: the
            // player is never camped while still standing on the overmap.
            party.IsCamped = true;

            // Making camp abandons the march. Keeping the destination would have the column set off
            // again the moment camp is struck, which is not what the player asked for.
            party.Destination = null;

            _log?.Push(
                CampaignEventKind.Supply,
                "Made camp",
                "The century pitches its tents",
                _state.Clock.Now.DayNumber);

            if (ServiceLocator.TryGet(out ISceneNavigator navigator))
                navigator.LoadCamp();
            else
                Debug.LogWarning("[OvermapHud] No ISceneNavigator registered; cannot open the camp scene.");
        }

        private void ToggleStealth()
        {
            PartyState party = _state?.PlayerParty;
            if (party == null) return;

            party.IsStealthed = !party.IsStealthed;

            _log?.Push(
                CampaignEventKind.Threat,
                party.IsStealthed ? "Moving unseen" : "Marching openly",
                party.IsStealthed ? "Slower, but harder to find" : "Full marching pace",
                _state.Clock.Now.DayNumber);
        }

        private void BindTimeButtons(VisualElement root)
        {
            Register(root, "time-pause", TimeControl.Paused);
            Register(root, "time-normal", TimeControl.Normal);
            Register(root, "time-fast", TimeControl.Fast);
            Register(root, "time-fastest", TimeControl.Fastest);
        }

        private void Register(VisualElement root, string name, TimeControl control)
        {
            Button button = Find<Button>(root, name);
            if (button == null) return;

            _timeButtons[control] = button;
            button.clicked += () => _ticker?.SetTimeControl(control);
        }

        // --- Refresh --------------------------------------------------------------------------

        private void Update()
        {
            if (!_bound || _state == null) return;

            if (_inventory != null && _inventory.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseInventory();

            if (_commander != null && _commander.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseCommander();

            // The tooltip tracks the cursor, so it runs every frame; the panels poll at the interval.
            RefreshHoverTip();

            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator < _refreshInterval) return;
            _accumulator = 0f;

            Refresh();
        }

        /// <summary>Opens the inventory and freezes the campaign while the player takes stock.</summary>
        private void OpenInventory()
        {
            if (_inventory == null || _inventory.IsOpen || _activePoi != null) return;

            if (_ticker != null)
            {
                _controlBeforeInventory = _ticker.Current;
                _ticker.SetTimeControl(TimeControl.Paused);
            }

            _inventory.Open();
        }

        private void CloseInventory()
        {
            if (_inventory == null || !_inventory.IsOpen) return;

            _inventory.Close();
            if (_ticker != null) _ticker.SetTimeControl(_controlBeforeInventory);
        }

        /// <summary>Opens the commander's screen; the campaign holds its breath meanwhile.</summary>
        private void OpenCommander()
        {
            if (_commander == null || _commander.IsOpen || _activePoi != null) return;
            if (_inventory != null && _inventory.IsOpen) return;

            if (_ticker != null)
            {
                _controlBeforeCommander = _ticker.Current;
                _ticker.SetTimeControl(TimeControl.Paused);
            }

            _commander.Open();
        }

        private void CloseCommander()
        {
            if (_commander == null || !_commander.IsOpen) return;

            _commander.Close();
            if (_ticker != null) _ticker.SetTimeControl(_controlBeforeCommander);
        }

        /// <summary>
        /// What the cursor has sighted. Distance decides how much is known: a half-seen shape at the
        /// edge of the line of sight is only "a warband on the move"; one well inside it gives up its
        /// faction and strength. Nothing is shown for what the column cannot see at all.
        /// </summary>
        private void RefreshHoverTip()
        {
            if (_hoverTip == null) return;

            // A modal owns the screen; the world tooltip stays out of it.
            if ((_inventory != null && _inventory.IsOpen) || (_commander != null && _commander.IsOpen))
            {
                _hoverTip.style.display = DisplayStyle.None;
                return;
            }

            // An officer portrait under the cursor wins over the world behind it.
            if (_hoveredPortrait >= 0 && _activePoi == null)
            {
                _hoverTip.style.display = DisplayStyle.Flex;
                SetText(_hoverTipTitle, _portraitTitles[_hoveredPortrait] ?? "—");
                SetText(_hoverTipDetail, _portraitDetails[_hoveredPortrait] ?? "");
                PositionHoverTip(above: true);   // the bar is at the screen edge; below would clip
                return;
            }

            PartyState hovered = null;
            float visibility = 0f;
            bool show = _activePoi == null && TryGetHoveredParty(out hovered, out visibility);
            _hoverTip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            if (visibility >= 0.85f)
            {
                SetText(_hoverTipTitle, hovered.DisplayName.ToUpperInvariant());
                SetText(_hoverTipDetail,
                    $"{hovered.Faction} · {hovered.Roster.ActiveCount} men" +
                    (PartyRelations.IsHostile(_state.PlayerParty, hovered) ? " · HOSTILE" : ""));
            }
            else
            {
                SetText(_hoverTipTitle, "A WARBAND ON THE MOVE");
                SetText(_hoverTipDetail, "Too distant to make out. Close, or send scouts.");
            }

            PositionHoverTip();
        }

        /// <summary>Pins the tip beside the cursor, in panel space; above it when near the bottom edge.</summary>
        private void PositionHoverTip(bool above = false)
        {
            Vector2 screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(_hoverTip.panel, screen);
            _hoverTip.style.left = panel.x + 18f;
            _hoverTip.style.top = above ? panel.y - 72f : panel.y + 12f;
        }

        private bool TryGetHoveredParty(out PartyState hovered, out float visibility)
        {
            hovered = null;
            visibility = 0f;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _state.PlayerParty == null) return false;

            // Cursor to ground plane, then nearest living non-player party within the hover radius.
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return false;
            Vector3 point = ray.GetPoint(enter);

            float best = _hoverRadius;
            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState party = parties[i];
                if (party.IsPlayer || party.IsDisbanded) continue;

                Vector3 to = party.WorldPosition - point;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance >= best) continue;

                best = distance;
                hovered = party;
            }

            if (hovered == null) return false;

            // A warband the column cannot see at all is not hoverable — there is nothing there to
            // point at. Stealthy stalkers are seen far later than open marchers.
            visibility = LineOfSight.Visibility01(_state, hovered);
            return visibility > 0.05f;
        }

        private void Refresh()
        {
            PartyState party = _state.PlayerParty;
            if (party == null) return;

            int men = party.Roster.ActiveCount;

            SetText(_unitName, party.DisplayName.ToUpperInvariant());
            SetText(_valueMen, $"{men} / {party.Roster.Capacity}");

            if (_settings != null)
            {
                // Food days include what the men can eat straight from the packs — the true horizon.
                float readyFood = party.Stores.Food + TotalReadyFood(party);
                float foodDays = readyFood / Mathf.Max(0.0001f, men * _settings.RationsPerManPerDay);

                SetText(_valueFood, FormatDays(foodDays));

                // Running out of food is the campaign's core threat, so it earns a colour change.
                ApplyScarcity(_valueFood, foodDays);
            }

            float fuel = Century.Core.Items.ItemCatalog.TotalFuel(party.Inventory);
            SetText(_valueFirewood, $"{fuel:0}");
            ApplyScarcity(_valueFirewood, fuel / Mathf.Max(1f, men * 0.16f));

            SetText(_valueEquipment, $"{party.Stores.EquipmentCondition01 * 100f:0}%");
            SetText(_valueMedicine, $"{Century.Core.Items.ItemCatalog.TotalMedicineDoses(party.Inventory):0}");
            SetText(_valueCoin, $"{party.Stores.Coin:n0}");
            SetText(_valueDenarii, $"{party.Stores.Denarii:n0}");

            RefreshMorale(party);
            RefreshColumn(party);
            RefreshPortraits(party);
            RefreshTime();
            RefreshToggles(party);
            RefreshContact(party);
        }

        private void RefreshMorale(PartyState party)
        {
            SetText(_valueMorale, party.Morale.ToDisplayString());

            if (_barMorale == null) return;

            _barMorale.style.width = Length.Percent(Mathf.Clamp01(party.Morale.Value01) * 100f);

            _barMorale.EnableInClassList("bar__fill--danger", party.Morale.Value01 < 0.35f);
            _barMorale.EnableInClassList("bar__fill--gold", party.Morale.Value01 >= 0.35f);
        }

        /// <summary>
        /// March state — everything the old debug overlay reported about the column itself, now part of
        /// the interface rather than sitting on top of it.
        /// </summary>
        private void RefreshColumn(PartyState party)
        {
            // Pace is a word, not a figure: the player thinks in march tempo, not km/h. Stealth is
            // the current "slow"; a forced march will be the "quick" when it exists.
            SetText(_columnPace, party.IsCamped ? "Camped" : party.IsStealthed ? "Slow" : "Normal");

            SetText(_columnOrders, party.Destination.HasValue ? "On the march" : "Halted");

            int wounded = party.Roster.WoundedCount;
            SetText(_columnWounded, wounded == 0 ? "None" : wounded.ToString());
            _columnWounded?.EnableInClassList("text-danger", wounded > party.Roster.ActiveCount / 4);

            int veterans = 0;
            for (int i = 0; i < party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = party.Roster.Soldiers[i];
                if (soldier.IsAlive && soldier.Tier >= VeterancyTier.Veteranus) veterans++;
            }

            SetText(_columnVeterans, veterans.ToString());
            _columnVeterans?.EnableInClassList("text-good", veterans > 0);
        }

        /// <summary>
        /// One portrait per officer, then a count of the rank and file. Showing all seventy-three men
        /// as tiles would be noise; the named officers are the ones the player forms attachments to.
        /// </summary>
        private void BuildPortraits()
        {
            if (_partyPortraits == null) return;
            _partyPortraits.Clear();

            for (int i = 0; i < 5; i++)
            {
                var portrait = new VisualElement();
                portrait.AddToClassList("portrait");

                // Role art from the icon pack; dimmed by portrait--empty while the post is unfilled.
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("portrait__icon");
                icon.AddToClassList($"gi-{OfficerIcons[i]}");
                portrait.Add(icon);

                int index = i;
                portrait.RegisterCallback<PointerEnterEvent>(_ => _hoveredPortrait = index);
                portrait.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredPortrait == index) _hoveredPortrait = -1;
                });

                _partyPortraits.Add(portrait);
            }
        }

        private static readonly string[] OfficerRanks =
            { "centurion", "optio", "tesserarius", "medicus", "immunis" };

        private static readonly string[] OfficerIcons =
            { "centurion-helmet", "crested-helmet", "scroll-quill", "healing", "toolbox" };

        private void RefreshPortraits(PartyState party)
        {
            if (_partyPortraits == null) return;

            for (int i = 0; i < _partyPortraits.childCount && i < OfficerRanks.Length; i++)
            {
                VisualElement portrait = _partyPortraits[i];
                SoldierRecord officer = party.Roster.FindByRank(OfficerRanks[i]);

                portrait.EnableInClassList("portrait--empty", officer == null);

                _portraitTitles[i] = officer == null ? "POST UNFILLED" : officer.DisplayName.ToUpperInvariant();
                _portraitDetails[i] = OfficerRanks[i].ToUpperInvariant();
            }
        }

        private void RefreshTime()
        {
            CampaignTime now = _state.Clock.Now;
            SetText(_timeReadout, $"{now.ToWatchString().ToUpperInvariant()}   {now.ToClockString()}");

            if (_ticker == null) return;

            foreach (KeyValuePair<TimeControl, Button> pair in _timeButtons)
                pair.Value.EnableInClassList("time-button--active", _ticker.Current == pair.Key);
        }

        private void RefreshToggles(PartyState party)
        {
            _pillCamp?.EnableInClassList("pill--active", party.IsCamped);
            _pillStealth?.EnableInClassList("pill--active", party.IsStealthed);
        }

        /// <summary>Nearest hostile, so the player can see a threat closing without hunting for it.</summary>
        private void RefreshContact(PartyState party)
        {
            if (_contactPanel == null) return;

            PartyState nearest = null;
            float nearestDistance = float.MaxValue;

            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState other = parties[i];
                if (other.IsDisbanded || !PartyRelations.IsHostile(party, other)) continue;

                float distance = Vector3.Distance(other.WorldPosition, party.WorldPosition);
                if (distance >= nearestDistance) continue;

                nearest = other;
                nearestDistance = distance;
            }

            bool pursuing = nearest != null && nearest.AiState == PartyAiState.Pursuing;
            bool show = nearest != null && (pursuing || nearestDistance < party.DetectionRadius);

            _contactPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            SetText(_contactName, nearest.DisplayName);

            // A state, not a range — the player reads "pursuing" and acts; the number was noise.
            SetText(_contactDetail, pursuing ? "Pursuing" : "Sighted");

            _contactName?.EnableInClassList("text-danger", pursuing);
        }

        // --- Events ---------------------------------------------------------------------------

        private void OnEventAdded(CampaignEvent _) => RebuildEvents();

        private void RebuildEvents()
        {
            if (_eventsList == null) return;
            _eventsList.Clear();

            if (_log == null)
            {
                _eventsList.Add(MakeEventRow("◦", "No reports", "", CampaignEventKind.Discovery));
                return;
            }

            foreach (CampaignEvent entry in _log.MostRecent(_visibleEventCount))
                _eventsList.Add(MakeEventRow(GlyphFor(entry.Kind), entry.Headline, entry.Detail, entry.Kind));

            if (_eventsList.childCount == 0)
                _eventsList.Add(MakeEventRow("◦", "No reports", "", CampaignEventKind.Discovery));
        }

        private static VisualElement MakeEventRow(
            string glyph, string headline, string detail, CampaignEventKind kind)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("event");
            row.AddToClassList(ModifierFor(kind));

            var glyphLabel = new Label(glyph) { pickingMode = PickingMode.Ignore };
            glyphLabel.AddToClassList("event__glyph");
            row.Add(glyphLabel);

            var textColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            textColumn.style.flexGrow = 1f;

            var headlineLabel = new Label(headline) { pickingMode = PickingMode.Ignore };
            headlineLabel.AddToClassList("event__text");
            textColumn.Add(headlineLabel);

            if (!string.IsNullOrEmpty(detail))
            {
                var detailLabel = new Label(detail) { pickingMode = PickingMode.Ignore };
                detailLabel.AddToClassList("event__text");
                detailLabel.AddToClassList("text-faint");
                textColumn.Add(detailLabel);
            }

            row.Add(textColumn);
            return row;
        }

        private static string ModifierFor(CampaignEventKind kind)
        {
            switch (kind)
            {
                case CampaignEventKind.Threat: return "event--warning";
                case CampaignEventKind.Loss: return "event--critical";
                case CampaignEventKind.Gain: return "event--good";
                default: return "event";
            }
        }

        private static string GlyphFor(CampaignEventKind kind)
        {
            switch (kind)
            {
                case CampaignEventKind.Threat: return "◆";
                case CampaignEventKind.Loss: return "✖";
                case CampaignEventKind.Gain: return "✚";
                case CampaignEventKind.Supply: return "▣";
                default: return "◦";
            }
        }

        // --- Point-of-interest events ---------------------------------------------------------

        private void OnPoiTriggered(PointOfInterest poi)
        {
            if (_poiModal == null || poi == null || _activePoi != null) return;

            PoiEvent evt = PoiCatalog.Find(poi.EventId);
            if (evt == null)
            {
                // No authored event for this POI; resolve it silently rather than block the march.
                poi.Resolved = true;
                return;
            }

            _activePoi = poi;

            // Freeze the campaign while the player decides, remembering their speed to restore it.
            if (_ticker != null)
            {
                _controlBeforePoi = _ticker.Current;
                _ticker.SetTimeControl(TimeControl.Paused);
            }

            SetText(_poiTitle, evt.Title.ToUpperInvariant());
            SetText(_poiKind, evt.Subtitle);
            SetText(_poiBody, evt.Body);

            if (_poiOfficer != null)
            {
                bool hasRemark = !string.IsNullOrEmpty(evt.OfficerRemark);
                _poiOfficer.text = hasRemark ? evt.OfficerRemark : string.Empty;
                _poiOfficer.style.display = hasRemark ? DisplayStyle.Flex : DisplayStyle.None;
            }

            BuildChoices(poi, evt);
            _poiModal.style.display = DisplayStyle.Flex;
        }

        private void BuildChoices(PointOfInterest poi, PoiEvent evt)
        {
            if (_poiChoices == null || evt.Choices == null) return;
            _poiChoices.Clear();

            for (int i = 0; i < evt.Choices.Length; i++)
            {
                PoiChoice choice = evt.Choices[i];

                var button = new Button { text = string.Empty };
                button.AddToClassList("poi-choice");

                var label = new Label(choice.Label) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("poi-choice__label");
                button.Add(label);

                if (!string.IsNullOrEmpty(choice.Hint))
                {
                    var hint = new Label(choice.Hint) { pickingMode = PickingMode.Ignore };
                    hint.AddToClassList("poi-choice__hint");
                    button.Add(hint);
                }

                PoiChoice captured = choice;
                button.clicked += () => ResolveChoice(poi, captured);
                _poiChoices.Add(button);
            }
        }

        private void ResolveChoice(PointOfInterest poi, PoiChoice choice)
        {
            if (_state != null)
                choice.Outcome.Apply(_state, _state.PlayerParty, _log, poi.WorldPosition);

            poi.Resolved = true;

            if (_poiModal != null) _poiModal.style.display = DisplayStyle.None;
            if (_ticker != null) _ticker.SetTimeControl(_controlBeforePoi);
            _activePoi = null;
        }

        // --- Helpers --------------------------------------------------------------------------

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text;
        }

        private static string FormatDays(float days) =>
            days >= 10f ? $"{days:0} days" : $"{days:0.0} days";

        /// <summary>Food the men can eat without a fire: ready items still in the packs.</summary>
        private static float TotalReadyFood(PartyState party)
        {
            float total = 0f;
            for (int i = 0; i < party.Inventory.Stacks.Count; i++)
            {
                var stack = party.Inventory.Stacks[i];
                Century.Core.Items.ItemDef def = Century.Core.Items.ItemCatalog.Find(stack.ItemId);
                if (def != null && !def.RawFood) total += def.FoodValue * stack.Count;
            }

            return total;
        }

        private static void ApplyScarcity(Label label, float days)
        {
            if (label == null) return;
            label.EnableInClassList("text-danger", days < 2f);
            label.EnableInClassList("text-gold", days >= 2f && days < 5f);
        }
    }
}
