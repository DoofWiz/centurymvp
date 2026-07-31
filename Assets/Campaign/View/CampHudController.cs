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
    /// Binds the camp screen to the player's party. The centrepiece is the Organisation chart —
    /// command band plus contubernia — where each man is a button that opens a detail sheet. Rest,
    /// orders and stations sit in the side column; resources and two radial dials run across the top.
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="OvermapHudController"/>: references resolved once and logged if
    /// missing, scalar readouts polled on a short interval, heavier lists rebuilt only on change. All
    /// mutation goes through <see cref="CampService"/>, which writes to the shared model.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CampHudController : MonoBehaviour
    {
        private const string DefaultOrgHint =
            "Click a man to inspect him. Click an office to see its traditions, invest, or appoint.";

        [SerializeField] private float _refreshInterval = 0.25f;

        private UIDocument _document;
        private CampaignState _state;
        private CampaignSettings _settings;
        private CampaignEventLog _log;
        private ISceneNavigator _navigator;
        private CampService _camp;

        private float _accumulator;
        private bool _bound;

        // Top bar.
        private Label _campEyebrow, _campTitle;
        private Label _valueMen, _valueFood, _valueFirewood;
        private Label _valueMedicine, _valueCoin, _valueDenarii, _valueEquipment, _valueMorale;
        private RadialMeter _equipMeter, _moraleMeter;
        private Label _equipMeterText, _moraleMeterText;

        // Organisation.
        private Label _orgHint;
        private ScrollView _orgList;

        // Side column.
        private ScrollView _stationsList;
        private ScrollView _craftingList;

        // Sub-screen tabs: 0 Century, 1 Rest & Orders, 2 Crafting, 3 Stations.
        private readonly Button[] _campTabs = new Button[4];
        private readonly VisualElement[] _campPages = new VisualElement[4];

        // Office overlay: opened by clicking an officer slot in the COMMAND band.
        private VisualElement _officeModal;
        private Label _officeSubtitle;
        private ScrollView _officeBody;
        private Button _officeAppoint, _officeCloseButton;
        private Label _officeAppointLabel;
        private CampRole _openOfficeRole;
        private Label _restReadout, _orderScoutHint, _orderTrainHint, _orderHuntHint;
        private Button _orderHunt;
        private Button _restWatch, _restDawn, _orderScout, _orderTrain, _breakCamp;

        // Picker modal.
        private VisualElement _pickerModal;
        private Label _pickerTitle, _pickerSubtitle;
        private ScrollView _pickerList;
        private Button _pickerVacate, _pickerClose;
        private CampRole _pickerRole;

        // Soldier detail modal.
        private VisualElement _soldierModal;
        private Button _soldierDecanus;
        private Label _soldierDecanusLabel;
        private SoldierRecord _openSoldier;
        private Label _soldierAvatar, _soldierName, _soldierRank, _soldierFlavour, _soldierAttitude;
        private Label _soldierRankLabel, _soldierMoraleLabel, _soldierFitnessLabel, _soldierEquipment;
        private VisualElement _soldierRankFill, _soldierMoraleFill, _soldierFitnessFill;
        private Button _soldierClose;

        // Scout modal.
        private VisualElement _scoutModal;
        private Label _scoutTitle, _scoutDetail, _scoutReward, _scoutRisk, _scoutDistance;
        private Button _scoutClose;

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

            UiInputBootstrapper.EnsureEventSystem();
            Century.Core.Ui.UiFont.Apply(root);

            if (!ServiceLocator.TryGet(out _state)) return;
            if (!ServiceLocator.TryGet(out _settings))
            {
                Debug.LogWarning("[CampHud] No CampaignSettings registered; camp actions are disabled.");
                return;
            }

            ServiceLocator.TryGet(out _log);
            ServiceLocator.TryGet(out _navigator);

            PartyState party = _state.PlayerParty;
            if (party == null) return;

            _camp = new CampService(_state, party, _settings, _state.Clock, _log);

            CacheElements(root);
            BuildMeters(root);
            WireButtons();

            CloseAllModals();

            _bound = true;
            RebuildOrganisation();
            RebuildStations();
            RebuildCrafting();
            Refresh();
        }

        private void OnDisable() => _bound = false;

        private int _shownCampTab = -1;

        /// <summary>Shows one camp sub-screen and marks its tab. The incoming page slides in with a
        /// short fade so a tab change reads as movement, not as content teleporting.</summary>
        private void SelectCampTab(int index)
        {
            bool changed = index != _shownCampTab;
            _shownCampTab = index;

            for (int i = 0; i < _campPages.Length; i++)
            {
                _campTabs[i]?.EnableInClassList("pill--active", i == index);
                if (_campPages[i] != null)
                    _campPages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            }

            VisualElement page = index >= 0 && index < _campPages.Length ? _campPages[index] : null;
            if (!changed || page == null) return;

            page.style.opacity = 0f;
            page.experimental.animation.Start(0f, 1f, 260, (element, t) =>
            {
                element.style.opacity = t;
                element.style.translate = new Translate(10f * (1f - t), 0f);
            });
        }

        private void CacheElements(VisualElement root)
        {
            _campEyebrow = Find<Label>(root, "camp-eyebrow");
            _campTitle = Find<Label>(root, "camp-title");
            _valueMen = Find<Label>(root, "value-men");
            _valueFood = Find<Label>(root, "value-food");
            _valueFirewood = Find<Label>(root, "value-firewood");
            _valueMedicine = Find<Label>(root, "value-medicine");
            _valueCoin = Find<Label>(root, "value-coin");
            _valueDenarii = Find<Label>(root, "value-denarii");
            _valueEquipment = Find<Label>(root, "value-equipment");
            _valueMorale = Find<Label>(root, "value-morale");

            _orgHint = Find<Label>(root, "org-hint");
            _orgList = Find<ScrollView>(root, "org-list");

            _officeModal = Find<VisualElement>(root, "office-modal");
            _officeSubtitle = Find<Label>(root, "office-subtitle");
            _officeBody = Find<ScrollView>(root, "office-body");
            _officeAppoint = Find<Button>(root, "office-appoint");
            _officeAppointLabel = Find<Label>(root, "office-appoint-label");
            _officeCloseButton = Find<Button>(root, "office-close");
            if (_officeAppoint != null)
                _officeAppoint.clicked += () =>
                {
                    CampRole role = _openOfficeRole;
                    CloseOffice();
                    OpenPicker(role);
                };
            if (_officeCloseButton != null) _officeCloseButton.clicked += CloseOffice;

            _stationsList = Find<ScrollView>(root, "stations-list");
            _craftingList = Find<ScrollView>(root, "crafting-list");

            for (int i = 0; i < _campTabs.Length; i++)
            {
                _campTabs[i] = Find<Button>(root, $"camp-tab-{i}");
                _campPages[i] = Find<VisualElement>(root, $"camp-page-{i}");

                if (_campTabs[i] == null) continue;
                int index = i;
                _campTabs[i].clicked += () => SelectCampTab(index);
            }

            SelectCampTab(0);
            _restReadout = Find<Label>(root, "rest-readout");
            _orderScoutHint = Find<Label>(root, "order-scout-hint");
            _orderTrainHint = Find<Label>(root, "order-train-hint");
            _orderHuntHint = Find<Label>(root, "order-hunt-hint");
            _orderHunt = Find<Button>(root, "order-hunt");
            _restWatch = Find<Button>(root, "rest-watch");
            _restDawn = Find<Button>(root, "rest-dawn");
            _orderScout = Find<Button>(root, "order-scout");
            _orderTrain = Find<Button>(root, "order-train");
            _breakCamp = Find<Button>(root, "break-camp");

            _pickerModal = Find<VisualElement>(root, "picker-modal");
            _pickerTitle = Find<Label>(root, "picker-title");
            _pickerSubtitle = Find<Label>(root, "picker-subtitle");
            _pickerList = Find<ScrollView>(root, "picker-list");
            _pickerVacate = Find<Button>(root, "picker-vacate");
            _pickerClose = Find<Button>(root, "picker-close");

            _soldierModal = Find<VisualElement>(root, "soldier-modal");
            _soldierAvatar = Find<Label>(root, "soldier-avatar");
            _soldierName = Find<Label>(root, "soldier-name");
            _soldierRank = Find<Label>(root, "soldier-rank");
            _soldierFlavour = Find<Label>(root, "soldier-flavour");
            _soldierAttitude = Find<Label>(root, "soldier-attitude");
            _soldierRankLabel = Find<Label>(root, "soldier-rankprog-label");
            _soldierMoraleLabel = Find<Label>(root, "soldier-morale-label");
            _soldierFitnessLabel = Find<Label>(root, "soldier-fitness-label");
            _soldierEquipment = Find<Label>(root, "soldier-equipment");
            _soldierRankFill = Find<VisualElement>(root, "soldier-rankprog-fill");
            _soldierMoraleFill = Find<VisualElement>(root, "soldier-morale-fill");
            _soldierFitnessFill = Find<VisualElement>(root, "soldier-fitness-fill");
            _soldierClose = Find<Button>(root, "soldier-close");
            _soldierDecanus = Find<Button>(root, "soldier-decanus");
            _soldierDecanusLabel = Find<Label>(root, "soldier-decanus-label");

            _scoutModal = Find<VisualElement>(root, "scout-modal");
            _scoutTitle = Find<Label>(root, "scout-title");
            _scoutDetail = Find<Label>(root, "scout-detail");
            _scoutReward = Find<Label>(root, "scout-reward");
            _scoutRisk = Find<Label>(root, "scout-risk");
            _scoutDistance = Find<Label>(root, "scout-distance");
            _scoutClose = Find<Button>(root, "scout-close");
        }

        private void BuildMeters(VisualElement root)
        {
            _equipMeter = AttachMeter(root, "equipment-meter", out _equipMeterText);
            _moraleMeter = AttachMeter(root, "morale-meter", out _moraleMeterText);
        }

        private RadialMeter AttachMeter(VisualElement root, string containerName, out Label centreLabel)
        {
            centreLabel = null;
            VisualElement container = Find<VisualElement>(root, containerName);
            if (container == null) return null;

            container.Clear();

            var meter = new RadialMeter();
            meter.style.position = Position.Absolute;
            meter.style.left = 0f;
            meter.style.right = 0f;
            meter.style.top = 0f;
            meter.style.bottom = 0f;
            container.Add(meter);

            centreLabel = new Label("—") { pickingMode = PickingMode.Ignore };
            centreLabel.AddToClassList("meter__value");
            container.Add(centreLabel);

            return meter;
        }

        private void WireButtons()
        {
            if (_restWatch != null) _restWatch.clicked += () => DoRest(RestSpan.OneWatch);
            if (_restDawn != null) _restDawn.clicked += () => DoRest(RestSpan.UntilDawn);
            if (_orderScout != null) _orderScout.clicked += DoScout;
            if (_orderTrain != null) _orderTrain.clicked += DoTrain;
            if (_orderHunt != null) _orderHunt.clicked += DoHunt;
            if (_breakCamp != null) _breakCamp.clicked += BreakCamp;
            if (_pickerClose != null) _pickerClose.clicked += ClosePicker;
            if (_pickerVacate != null) _pickerVacate.clicked += VacatePost;
            if (_soldierClose != null) _soldierClose.clicked += () => Hide(_soldierModal);
            if (_soldierDecanus != null) _soldierDecanus.clicked += AppointDecanus;
            if (_scoutClose != null) _scoutClose.clicked += () => Hide(_scoutModal);
        }

        private static T Find<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
                Debug.LogWarning($"[CampHud] UXML element '{name}' not found. Check CampHud.uxml.");
            return element;
        }

        // --- Actions ---------------------------------------------------------------------------

        private void DoRest(RestSpan span)
        {
            if (_camp == null) return;
            RestResult result = _camp.Rest(span);
            SetText(_restReadout, DescribeRest(result));
            RebuildOrganisation();
            Refresh();
        }

        private static string DescribeRest(RestResult r)
        {
            string cold = r.WasCold ? "  The fires ran low." : string.Empty;
            string wounded = r.WoundedHealed > 0 ? $"{r.WoundedHealed} back on their feet. " : string.Empty;
            return $"Rested {r.Hours:0} hours. {wounded}" +
                   $"Morale {Signed(r.MoraleDelta)}, stamina {Signed(r.StaminaDelta)}. " +
                   $"Ate {r.FoodSpent:0} rations, burned {r.FirewoodSpent:0} firewood.{cold}";
        }

        private static string Signed(float delta01)
        {
            int points = Mathf.RoundToInt(delta01 * 100f);
            return points >= 0 ? $"+{points}" : points.ToString();
        }

        private void DoScout()
        {
            if (_camp == null || !_camp.CanScout) return;
            _camp.SendScouting();
            ShowScoutResult();
            Refresh();
        }

        private void DoTrain()
        {
            if (_camp == null || !_camp.CanTrain) return;
            _camp.TrainRanks();
            SetText(_restReadout, "The century drills. The green men are the better for it.");
            RebuildOrganisation();
            Refresh();
        }

        private void DoHunt()
        {
            if (_camp == null) return;

            CampService.HuntResult hunt = _camp.SendHunters();
            SetText(_restReadout, hunt.Pelts > 0
                ? $"The hunters return with {hunt.Game} game and {hunt.Pelts} pelts."
                : $"The hunters return with {hunt.Game} game for the cooking fire.");

            RebuildCrafting();
            Refresh();
        }

        private void BreakCamp()
        {
            PartyState party = _state?.PlayerParty;
            if (party != null) party.IsCamped = false;

            _log?.Push(CampaignEventKind.Supply, "Struck camp", "The column forms up",
                _state != null ? _state.Clock.Now.DayNumber : 0);

            if (_navigator != null) _navigator.LoadOvermap();
            else Debug.LogWarning("[CampHud] No ISceneNavigator registered; cannot return to the overmap.");
        }

        // --- Organisation ----------------------------------------------------------------------

        private void RebuildOrganisation()
        {
            if (_orgList == null) return;
            _orgList.Clear();

            PartyState party = _state.PlayerParty;
            List<SoldierRecord> soldiers = party.Roster.Soldiers;

            // Sync the establishment with the appointment chart before drawing either: rank-held
            // officers surface as appointments, and the office cards agree with these slots.
            _state.Posts.EnsureSeeded(party.Roster, _state.Clock.Now.DayNumber, party.Appointments);

            // Command band: the senior posts. The centurion is the player and gets no card here.
            _orgList.Add(SectionHeader("COMMAND"));

            var command = MakeGrid();
            foreach (CampRole role in CampRoleInfo.All)
                command.Add(MakeOfficerSlot(role));

            _orgList.Add(command);

            // The rank and file in their PERSISTENT tent groups — the same contubernia that form up
            // as battle squads, so the chart here is the line there. Officers are drawn out above
            // but keep their assignment and fight with their group.
            ContuberniumLedger.EnsureAssigned(party.Roster);

            var indices = new List<int>();
            ContuberniumLedger.CollectIndices(party.Roster, indices);

            var members = new List<SoldierRecord>();
            for (int g = 0; g < indices.Count; g++)
            {
                int index = indices[g];

                members.Clear();
                for (int i = 0; i < soldiers.Count; i++)
                {
                    SoldierRecord soldier = soldiers[i];
                    if (!soldier.IsAlive || soldier.Contubernium != index || IsOfficer(soldier)) continue;
                    members.Add(soldier);
                }

                if (members.Count == 0) continue;

                // The appointed Decanus leads; failing one, the senior man stands as acting.
                SoldierRecord decanus = ContuberniumLedger.DecanusOf(party.Roster, index);
                int leadIndex = 0;
                bool appointed = false;

                if (decanus != null && members.Contains(decanus))
                {
                    leadIndex = members.IndexOf(decanus);
                    appointed = true;
                }
                else
                {
                    for (int i = 1; i < members.Count; i++)
                        if (members[i].Experience > members[leadIndex].Experience) leadIndex = i;
                }

                _orgList.Add(SectionHeader($"CONTUBERNIUM {Roman(index + 1)}", $"{members.Count} men"));

                var grid = MakeGrid();

                // The Decanus always leads his row, on the far left.
                grid.Add(MakeSoldierCard(members[leadIndex], appointed ? "Decanus" : "Decanus (acting)", false, true));
                for (int i = 0; i < members.Count; i++)
                {
                    if (i == leadIndex) continue;
                    grid.Add(MakeSoldierCard(members[i], null, false, false));
                }

                _orgList.Add(grid);
            }
        }

        // --- The offices -------------------------------------------------------------------------

        /// <summary>
        /// The office overlay: clicking an officer slot opens that office's tradition card (the
        /// same card the ARMY screen shows read-only) with invest live and the appointment flow
        /// a button away. The camp is where the player acts (army brief §4.2).
        /// </summary>
        private void OpenOffice(CampRole role)
        {
            if (_officeModal == null) return;
            _openOfficeRole = role;
            RefreshOfficeModal();
            Show(_officeModal);
        }

        private void RefreshOfficeModal()
        {
            PartyState party = _state.PlayerParty;
            if (_officeBody == null || party == null) return;

            string post = PostRoster.PostFor(_openOfficeRole);
            if (post == null) return;

            SetText(_officeSubtitle, PostRoster.Charge(post));

            _officeBody.Clear();
            _officeBody.Add(OfficeCards.BuildCard(_state, party.Roster, post, InvestInTradition));

            bool filled = party.Appointments.IsFilled(_openOfficeRole);
            SetText(_officeAppointLabel, filled ? "REPLACE THE MAN" : "APPOINT A MAN");
        }

        private void CloseOffice() => Hide(_officeModal);

        private void InvestInTradition(PostNodeDef node)
        {
            PartyState party = _state.PlayerParty;
            if (party == null || !PostTreeCatalog.CanInvest(_state, party.Roster, node)) return;

            PostRecord record = _state.Posts.Find(node.Post);
            record.UnspentPoints -= node.Cost;
            record.InvestedNodeIds.Add(node.Id);

            _log?.Push(
                CampaignEventKind.Gain,
                $"{PostRoster.DisplayName(node.Post)}: {node.Name}",
                node.Effect,
                _state.Clock.Now.DayNumber);

            RefreshOfficeModal();
            RebuildOrganisation();   // the slot badges show unspent points
        }

        private bool IsOfficer(SoldierRecord soldier)
        {
            if (soldier.RankId == "centurion") return true;
            return _state.PlayerParty.Appointments.HoldsAnyRole(soldier.Id, out _);
        }

        private static VisualElement SectionHeader(string title, string note = null)
        {
            var head = new VisualElement();
            head.AddToClassList("org-section");

            var label = new Label(title);
            label.AddToClassList("org-section__title");
            head.Add(label);

            if (!string.IsNullOrEmpty(note))
            {
                var noteLabel = new Label(note);
                noteLabel.AddToClassList("org-section__note");
                head.Add(noteLabel);
            }

            return head;
        }

        private static VisualElement MakeGrid()
        {
            var grid = new VisualElement();
            grid.AddToClassList("org-grid");
            return grid;
        }

        private VisualElement MakeSoldierCard(SoldierRecord soldier, string overrideRank, bool officer, bool decanus)
        {
            var card = new Button { text = string.Empty };
            card.AddToClassList("soldier-card");
            card.EnableInClassList("soldier-card--officer", officer);
            card.EnableInClassList("soldier-card--decanus", decanus);

            var avatar = new Label(TierGlyph(soldier)) { pickingMode = PickingMode.Ignore };
            avatar.AddToClassList("soldier-card__avatar");
            avatar.AddToClassList(TierColorClass(soldier));
            card.Add(avatar);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("soldier-card__body");

            var name = new Label(soldier.DisplayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("soldier-card__name");
            body.Add(name);

            var rank = new Label(overrideRank ?? soldier.Tier.ToString()) { pickingMode = PickingMode.Ignore };
            rank.AddToClassList("soldier-card__rank");
            rank.AddToClassList(TierColorClass(soldier));
            body.Add(rank);
            card.Add(body);

            var status = new VisualElement { pickingMode = PickingMode.Ignore };
            status.AddToClassList("soldier-card__status");

            status.Add(StatusChip("✚", HealthColorClass(soldier.Health01),
                $"Fitness: {FitnessWord(soldier.Health01)}"));
            status.Add(StatusChip("◆", MoraleColorClass(soldier.Morale01),
                $"Morale: {new MoraleState { Value01 = soldier.Morale01 }.Band}"));
            card.Add(status);

            card.clicked += () => OpenSoldierDetail(soldier);
            return card;
        }

        private Label StatusChip(string glyph, string colorClass, string hint)
        {
            var chip = new Label(glyph);
            chip.AddToClassList("status-chip");
            if (!string.IsNullOrEmpty(colorClass)) chip.AddToClassList(colorClass);
            chip.tooltip = hint;

            // Reveal the reading in the panel header too, since runtime tooltips are easy to miss.
            chip.RegisterCallback<PointerEnterEvent>(_ => SetText(_orgHint, hint));
            chip.RegisterCallback<PointerLeaveEvent>(_ => SetText(_orgHint, DefaultOrgHint));
            return chip;
        }

        private VisualElement MakeOfficerSlot(CampRole role)
        {
            CampRoleInfo info = CampRoleInfo.For(role);
            string holderId = _state.PlayerParty.Appointments.GetHolderId(role);
            SoldierRecord holder = holderId == null ? null : _state.PlayerParty.Roster.Find(holderId);

            var slot = new Button { text = string.Empty };
            slot.AddToClassList("officer-slot");
            slot.EnableInClassList("officer-slot--vacant", holder == null);

            var crest = new Label(holder == null ? "○" : "✦") { pickingMode = PickingMode.Ignore };
            crest.AddToClassList("officer-slot__crest");
            slot.Add(crest);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("officer-slot__text");

            var post = new Label(info.Latin.ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            post.AddToClassList("officer-slot__post");
            text.Add(post);

            var name = new Label(holder != null ? holder.DisplayName : "Vacant — appoint")
                { pickingMode = PickingMode.Ignore };
            name.AddToClassList("officer-slot__holder");
            name.EnableInClassList("text-faint", holder == null);
            text.Add(name);

            var title = new Label(info.Title) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("officer-slot__title");
            text.Add(title);

            slot.Add(text);

            // Unspent tradition points wait on the office: the badge is the call to action.
            PostRecord record = _state.Posts.Find(PostRoster.PostFor(role));
            int points = record != null ? Mathf.FloorToInt(record.UnspentPoints) : 0;
            if (points > 0) slot.Add(Badge($"{points} pts", "badge--points"));

            slot.clicked += () => OpenOffice(role);
            return slot;
        }

        // --- Soldier detail --------------------------------------------------------------------

        private void OpenSoldierDetail(SoldierRecord soldier)
        {
            if (_soldierModal == null) return;

            _openSoldier = soldier;
            RefreshDecanusButton(soldier);

            SetText(_soldierAvatar, TierGlyph(soldier));
            Recolour(_soldierAvatar, TierColorClass(soldier));

            SetText(_soldierName, soldier.DisplayName);
            SetText(_soldierRank, RankTitle(soldier));
            SetText(_soldierFlavour, FlavourFor(soldier));

            string attitude = AttitudeWord(soldier.Loyalty01);
            SetText(_soldierAttitude, attitude);
            Recolour(_soldierAttitude, LoyaltyColorClass(soldier.Loyalty01));

            // Rank progress.
            VeterancyTier tier = soldier.Tier;
            int next = VeterancyLadder.NextThreshold(tier);
            if (next <= 0)
            {
                SetText(_soldierRankLabel, $"RANK — {tier} (the steadiest of the steady)");
                SetFill(_soldierRankFill, 1f);
            }
            else
            {
                SetText(_soldierRankLabel,
                    $"RANK — {tier} · {soldier.Experience}/{next} xp to {tier + 1}");
                SetFill(_soldierRankFill, VeterancyLadder.Progress01(soldier.Experience));
            }

            var morale = new MoraleState { Value01 = soldier.Morale01 };
            SetText(_soldierMoraleLabel, $"MORALE — {morale.Band}");
            SetFill(_soldierMoraleFill, soldier.Morale01);
            RecolourFill(_soldierMoraleFill, MoraleFillClass(soldier.Morale01));

            SetText(_soldierFitnessLabel, $"FITNESS — {FitnessWord(soldier.Health01)}");
            SetFill(_soldierFitnessFill, soldier.Health01);
            RecolourFill(_soldierFitnessFill, HealthFillClass(soldier.Health01));

            SetText(_soldierEquipment, EquipmentFor(soldier));

            Show(_soldierModal);
        }

        /// <summary>
        /// The Decanus is an appointment, not seniority: any rank-and-file man of a contubernium can
        /// be given his tent group. Officers hold senior posts and are not eligible.
        /// </summary>
        private void RefreshDecanusButton(SoldierRecord soldier)
        {
            if (_soldierDecanus == null) return;

            bool eligible = soldier.IsAlive && soldier.Contubernium >= 0 && !IsOfficer(soldier);
            _soldierDecanus.style.display = eligible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!eligible) return;

            SetText(_soldierDecanusLabel, soldier.IsDecanus ? "DECANUS OF HIS TENT" : "APPOINT DECANUS");
            _soldierDecanus.SetEnabled(!soldier.IsDecanus);
        }

        private void AppointDecanus()
        {
            if (_openSoldier == null || _state?.PlayerParty == null) return;

            ContuberniumLedger.SetDecanus(_state.PlayerParty.Roster, _openSoldier.Id);

            _log?.Push(CampaignEventKind.Gain,
                $"{_openSoldier.DisplayName} made Decanus",
                $"He leads Contubernium {Roman(_openSoldier.Contubernium + 1)} now",
                _state.Clock.Now.DayNumber);

            RefreshDecanusButton(_openSoldier);
            RebuildOrganisation();
        }

        private string RankTitle(SoldierRecord soldier)
        {
            if (soldier.RankId == "centurion") return "Centurion — you";
            if (_state.PlayerParty.Appointments.HoldsAnyRole(soldier.Id, out CampRole role))
                return $"{CampRoleInfo.For(role).Latin} — {soldier.Tier}";
            return soldier.Tier.ToString();
        }

        private static readonly string[] IdleFlavour =
        {
            "Sharpening a gladius that is already keen.",
            "Mending a strap on his kit by the firelight.",
            "Dicing for another man's ration of wine.",
            "Trading lies about the women of Rome.",
            "Oiling leather gone stiff with the damp."
        };

        private string FlavourFor(SoldierRecord soldier)
        {
            if (soldier.Health01 < 0.25f) return "Laid up under the medicus, in a bad way.";
            if (soldier.Health01 < 0.5f) return "Nursing a wound by the fire, teeth gritted.";
            if (soldier.Stamina01 < 0.4f) return "Bone-tired, asleep almost where he sat.";
            if (soldier.Morale01 < 0.35f) return "Sullen, muttering with the other malcontents.";
            if (soldier.Morale01 > 0.8f) return "In high spirits, loud with wine and boasts.";
            int index = Mathf.Abs(soldier.Id.GetHashCode()) % IdleFlavour.Length;
            return IdleFlavour[index];
        }

        private string EquipmentFor(SoldierRecord soldier)
        {
            string kit;
            switch (soldier.RankId)
            {
                case "centurion": kit = "Gladius, scutum, transverse crest, mail hamata."; break;
                case "medicus": kit = "Medical kit, bandages, dagger."; break;
                case "optio":
                case "tesserarius": kit = "Gladius, scutum, two pila, mail hamata."; break;
                default: kit = "Gladius, scutum, two pila."; break;
            }

            float cond = _state.PlayerParty.Stores.EquipmentCondition01;
            return $"{kit}\nCondition: {EquipmentWord(cond)} ({cond * 100f:0}%).";
        }

        // --- Scout result ----------------------------------------------------------------------

        private struct Opportunity
        {
            public string Title, Detail, Reward, Risk;
        }

        private static readonly Opportunity[] Opportunities =
        {
            new Opportunity { Title = "A druid's grove, half-hidden in the pines",
                Detail = "Smoke curls above the trees, and the scout marks old votive stones among them. Someone tends this place.",
                Reward = "A rare relic; healing herbs", Risk = "Lightly held — a few zealots" },
            new Opportunity { Title = "An abandoned watchtower on the ridge",
                Detail = "A Roman tower, its garrison long fled. It commands the valley — and could shelter the column for a night.",
                Reward = "A vantage point; a claimable base", Risk = "Beasts have denned inside" },
            new Opportunity { Title = "A baggage train, mired and abandoned",
                Detail = "Wagons sunk to the axle in a bog, their oxen long butchered. Roman kit still lies scattered in the mud.",
                Reward = "Salvaged arms and coin", Risk = "Scavengers already picking it over" },
            new Opportunity { Title = "A column of refugees on the old road",
                Detail = "Frightened folk, Roman and native both, fleeing the same war you are. Some carry blades. Some could carry them for you.",
                Reward = "Recruits and goodwill", Risk = "None, but they are desperate" },
        };

        private void ShowScoutResult()
        {
            if (_scoutModal == null) return;

            int scoutFire = _state.PlayerParty.Facilities.LevelOf(CampStationId.ScoutFire);
            Opportunity opportunity = Opportunities[Random.Range(0, Opportunities.Length)];

            SetText(_scoutTitle, opportunity.Title);
            SetText(_scoutDetail, opportunity.Detail);
            SetText(_scoutReward, scoutFire > 0 ? opportunity.Reward + " (richer, for the scout's fire)" : opportunity.Reward);
            SetText(_scoutRisk, opportunity.Risk);

            float miles = Random.Range(3f, 9f) - scoutFire;
            SetText(_scoutDistance, $"{Mathf.Max(1f, miles):0} miles out");

            Show(_scoutModal);
        }

        // --- Stations --------------------------------------------------------------------------

        private void RebuildStations()
        {
            if (_stationsList == null) return;
            _stationsList.Clear();

            PartyState party = _state.PlayerParty;

            foreach (CampStationId id in CampStationCatalog.All)
            {
                CampStationCatalog info = CampStationCatalog.Info(id);
                int level = party.Facilities.LevelOf(id);

                var row = new VisualElement();
                row.AddToClassList("station-row");

                var head = new VisualElement();
                head.AddToClassList("station-row__head");

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("station-row__icon");
                icon.AddToClassList($"gi-{info.Icon}");
                head.Add(icon);

                var name = new Label(info.Name.ToUpperInvariant());
                name.AddToClassList("station-row__name");
                head.Add(name);

                var pips = new VisualElement();
                pips.AddToClassList("pips");
                for (int i = 0; i < info.MaxLevel; i++)
                {
                    var pip = new VisualElement();
                    pip.AddToClassList("pip");
                    pip.EnableInClassList("pip--on", i < level);
                    pips.Add(pip);
                }
                head.Add(pips);
                row.Add(head);

                var effect = new Label(info.EffectAt(level));
                effect.AddToClassList("station-row__effect");
                effect.EnableInClassList("text-faint", level <= 0);
                row.Add(effect);

                var build = new Button();
                build.AddToClassList("station-build");

                bool maxed = level >= info.MaxLevel;
                if (maxed)
                {
                    build.text = "FULLY BUILT";
                    build.SetEnabled(false);
                }
                else
                {
                    string verb = level == 0 ? "BUILD" : "UPGRADE";
                    build.text = $"{verb}   {info.TimberCost(level)} timber · {info.DenariiCost(level)} den";
                    build.SetEnabled(_camp != null && _camp.CanAfford(id));
                    CampStationId captured = id;
                    build.clicked += () => DoBuild(captured);
                }

                row.Add(build);
                _stationsList.Add(row);
            }
        }

        private void DoBuild(CampStationId id)
        {
            if (_camp == null) return;
            if (_camp.BuildOrUpgrade(id))
            {
                RebuildStations();
                RebuildCrafting();
                Refresh();
            }
        }

        // --- Crafting --------------------------------------------------------------------------

        /// <summary>
        /// One row per recipe, station-gated: the cooking fire feeds the men, the medical tent works
        /// raw materials into medicine. Rows grey out when the station is missing or the inputs are.
        /// </summary>
        private void RebuildCrafting()
        {
            if (_craftingList == null) return;
            _craftingList.Clear();

            foreach (CraftingRecipe recipe in CraftingCatalog.All)
            {
                CampStationCatalog station = CampStationCatalog.Info(recipe.Station);
                bool stationBuilt = _state.PlayerParty.Facilities.LevelOf(recipe.Station) > 0;

                var row = new VisualElement();
                row.AddToClassList("station-row");

                var head = new VisualElement();
                head.AddToClassList("station-row__head");

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("station-row__icon");
                icon.AddToClassList($"gi-{recipe.Icon}");
                head.Add(icon);

                var name = new Label(recipe.Name.ToUpperInvariant());
                name.AddToClassList("station-row__name");
                head.Add(name);

                var where = new Label(station.Name.ToUpperInvariant());
                where.AddToClassList("station-row__effect");
                head.Add(where);
                row.Add(head);

                var detail = new Label($"{DescribeInputs(recipe)}  →  {DescribeOutput(recipe)}");
                detail.AddToClassList("station-row__effect");
                detail.EnableInClassList("text-faint", !stationBuilt);
                row.Add(detail);

                if (!stationBuilt)
                {
                    var blocked = new Button { text = $"NEEDS THE {station.Name.ToUpperInvariant()}" };
                    blocked.AddToClassList("station-build");
                    blocked.SetEnabled(false);
                    row.Add(blocked);
                }
                else
                {
                    // CRAFT makes one; CRAFT ALL empties the larder in a click.
                    int most = _camp != null ? _camp.MaxCraftable(recipe) : 0;
                    CraftingRecipe captured = recipe;

                    var buttons = new VisualElement();
                    buttons.AddToClassList("craft-buttons");

                    var craft = new Button { text = "CRAFT" };
                    craft.AddToClassList("station-build");
                    craft.AddToClassList("craft-buttons__one");
                    craft.SetEnabled(most > 0);
                    craft.clicked += () => DoCraft(captured);
                    buttons.Add(craft);

                    var craftAll = new Button { text = most > 1 ? $"CRAFT ALL × {most}" : "CRAFT ALL" };
                    craftAll.AddToClassList("station-build");
                    craftAll.AddToClassList("craft-buttons__all");
                    craftAll.SetEnabled(most > 1);
                    craftAll.clicked += () => DoCraftAll(captured);
                    buttons.Add(craftAll);

                    row.Add(buttons);
                }

                _craftingList.Add(row);
            }
        }

        private void DoCraft(CraftingRecipe recipe)
        {
            if (_camp == null) return;
            if (_camp.Craft(recipe))
            {
                RebuildCrafting();
                Refresh();
            }
        }

        private void DoCraftAll(CraftingRecipe recipe)
        {
            if (_camp == null) return;
            if (_camp.CraftAll(recipe) > 0)
            {
                RebuildCrafting();
                Refresh();
            }
        }

        private static string DescribeInputs(CraftingRecipe recipe)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                if (i > 0) text.Append(" + ");
                Century.Core.Items.ItemDef def = Century.Core.Items.ItemCatalog.Find(recipe.Inputs[i].ItemId);
                text.Append(recipe.Inputs[i].Count).Append("× ")
                    .Append(def != null ? def.Name : recipe.Inputs[i].ItemId);
            }

            return text.ToString();
        }

        private static string DescribeOutput(CraftingRecipe recipe)
        {
            if (recipe.FoodYield > 0f) return $"{recipe.FoodYield:0.#} food";

            Century.Core.Items.ItemDef def = Century.Core.Items.ItemCatalog.Find(recipe.OutputItemId);
            return $"{recipe.OutputCount}× {(def != null ? def.Name : recipe.OutputItemId)}";
        }

        // --- Soldier picker --------------------------------------------------------------------

        private void OpenPicker(CampRole role)
        {
            if (_pickerModal == null || _camp == null) return;
            _pickerRole = role;

            CampRoleInfo info = CampRoleInfo.For(role);
            SetText(_pickerTitle, $"APPOINT {info.Latin.ToUpperInvariant()}");
            SetText(_pickerSubtitle, info.Benefit);

            if (_pickerList != null)
            {
                _pickerList.Clear();
                List<SoldierRecord> eligible = _camp.EligibleFor(role);

                if (eligible.Count == 0)
                {
                    var empty = new Label("No man yet meets this post's standing.");
                    empty.AddToClassList("body");
                    empty.AddToClassList("text-faint");
                    _pickerList.Add(empty);
                }

                string currentHolder = _state.PlayerParty.Appointments.GetHolderId(role);
                for (int i = 0; i < eligible.Count; i++)
                    _pickerList.Add(MakePickRow(eligible[i], currentHolder));
            }

            Show(_pickerModal);
        }

        private VisualElement MakePickRow(SoldierRecord soldier, string currentHolderId)
        {
            var row = new Button { text = string.Empty };
            row.AddToClassList("pick-row");
            row.EnableInClassList("pick-row--current", soldier.Id == currentHolderId);
            // A distinguished man is flagged so an accomplished choice stands out from a warm body.
            row.EnableInClassList("pick-row--veteran", soldier.Tier >= VeterancyTier.Veteranus);

            var avatar = new Label(TierGlyph(soldier)) { pickingMode = PickingMode.Ignore };
            avatar.AddToClassList("pick-row__avatar");
            avatar.AddToClassList(TierColorClass(soldier));
            row.Add(avatar);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.style.flexGrow = 1f;

            var name = new Label(soldier.DisplayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("pick-row__name");
            text.Add(name);

            var detail = new Label(
                $"{soldier.Tier} · {soldier.Kills} kills · {soldier.BattlesSurvived} fights")
                { pickingMode = PickingMode.Ignore };
            detail.AddToClassList("pick-row__detail");
            text.Add(detail);
            row.Add(text);

            // Badges: whether he already holds a post, and whether he is a cut above.
            if (_state.PlayerParty.Appointments.HoldsAnyRole(soldier.Id, out CampRole other))
                row.Add(Badge($"holds {CampRoleInfo.For(other).Latin}", "badge--posted"));
            else if (soldier.Tier >= VeterancyTier.Veteranus)
                row.Add(Badge("distinguished", "badge--veteran"));

            string soldierId = soldier.Id;
            row.clicked += () =>
            {
                _camp.AssignRole(_pickerRole, soldierId);
                ClosePicker();
                RebuildOrganisation();
                OpenOffice(_pickerRole);   // straight back to the office, new man in place
            };

            return row;
        }

        private static Label Badge(string text, string modifier)
        {
            var badge = new Label(text) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("badge");
            badge.AddToClassList(modifier);
            return badge;
        }

        private void VacatePost()
        {
            if (_camp == null) return;
            _camp.AssignRole(_pickerRole, null);
            ClosePicker();
            RebuildOrganisation();
            OpenOffice(_pickerRole);   // the card now shows the vacancy and what it costs
        }

        private void ClosePicker() => Hide(_pickerModal);

        // --- Refresh ---------------------------------------------------------------------------

        private void Update()
        {
            if (!_bound || _state == null) return;

            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator < _refreshInterval) return;
            _accumulator = 0f;

            Refresh();
        }

        private void Refresh()
        {
            PartyState party = _state.PlayerParty;
            if (party == null) return;

            int men = party.Roster.ActiveCount;

            SetText(_campEyebrow, $"MARCHING CAMP · {_state.Clock.Now.ToWatchString().ToUpperInvariant()}");
            SetText(_campTitle, party.DisplayName.ToUpperInvariant());
            SetText(_valueMen, $"{men} / {party.Roster.Capacity}");

            float foodDays = party.Stores.FoodDaysRemaining(men, _settings.RationsPerManPerDay);
            SetText(_valueFood, FormatDays(foodDays));
            ApplyScarcity(_valueFood, foodDays);

            float fuel = Century.Core.Items.ItemCatalog.TotalFuel(party.Inventory);
            SetText(_valueFirewood, $"{fuel:0} bundles");
            ApplyScarcity(_valueFirewood, fuel / Mathf.Max(1f, men * 0.16f));

            float doses = Century.Core.Items.ItemCatalog.TotalMedicineDoses(party.Inventory);
            SetText(_valueMedicine, $"{doses:0} doses");
            SetText(_valueCoin, $"{party.Stores.Coin:n0}");
            SetText(_valueDenarii, $"{party.Stores.Denarii:n0}");

            RefreshMeters(party);
            RefreshOrderHints();
        }

        /// <summary>Each order says what it will do, or what it is waiting on — never a bare dash.</summary>
        private void RefreshOrderHints()
        {
            if (_camp == null) return;

            SetText(_orderScoutHint, _camp.CanScout
                ? "The Speculator rides out · costs a watch"
                : "Needs a Speculator appointed");
            _orderScout?.SetEnabled(_camp.CanScout);

            SetText(_orderTrainHint, _camp.CanTrain
                ? "The green men drill · costs a watch"
                : "Needs a Training Ground built");
            _orderTrain?.SetEnabled(_camp.CanTrain);

            int scoutFire = _state.PlayerParty.Facilities.LevelOf(CampStationId.ScoutFire);
            SetText(_orderHuntHint, scoutFire > 0
                ? "Raw game for the fire · the scout's fire finds richer ground · costs a watch"
                : "Raw game for the cooking fire · costs a watch");
        }

        private void RefreshMeters(PartyState party)
        {
            float condition = party.Stores.EquipmentCondition01;
            if (_equipMeter != null)
            {
                _equipMeter.Value01 = condition;
                _equipMeter.FillColor = ColorFor(EquipmentFillClass(condition));
            }
            SetText(_equipMeterText, $"{condition * 100f:0}%");
            SetText(_valueEquipment, EquipmentWord(condition));
            Recolour(_valueEquipment, EquipmentTextClass(condition));

            float morale = party.Morale.Value01;
            if (_moraleMeter != null)
            {
                _moraleMeter.Value01 = morale;
                _moraleMeter.FillColor = ColorFor(MoraleFillClass(morale));
            }
            SetText(_moraleMeterText, $"{morale * 100f:0}%");
            SetText(_valueMorale, party.Morale.Band.ToString());
            Recolour(_valueMorale, MoraleTextClass(morale));
        }

        // --- Glyphs, words and colours ---------------------------------------------------------

        private string TierGlyph(SoldierRecord soldier)
        {
            if (soldier.RankId == "centurion") return "✦";
            if (_state.PlayerParty.Appointments.HoldsAnyRole(soldier.Id, out _)) return "✦";
            switch (soldier.Tier)
            {
                case VeterancyTier.Evocatus: return "★";
                case VeterancyTier.Veteranus: return "◆";
                case VeterancyTier.Miles: return "●";
                default: return "○";
            }
        }

        private string TierColorClass(SoldierRecord soldier)
        {
            if (soldier.RankId == "centurion") return "text-gold-bright";
            if (_state.PlayerParty.Appointments.HoldsAnyRole(soldier.Id, out _)) return "text-gold-bright";
            switch (soldier.Tier)
            {
                case VeterancyTier.Evocatus: return "text-gold-bright";
                case VeterancyTier.Veteranus: return "text-gold";
                case VeterancyTier.Miles: return "text-dim";
                default: return "text-faint";
            }
        }

        private static string FitnessWord(float health01)
        {
            if (health01 >= 0.75f) return "Hale";
            if (health01 >= 0.5f) return "Fit";
            if (health01 >= 0.25f) return "Wounded";
            return "Grave";
        }

        private static string EquipmentWord(float condition01)
        {
            if (condition01 >= 0.85f) return "Pristine";
            if (condition01 >= 0.6f) return "Serviceable";
            if (condition01 >= 0.35f) return "Worn";
            return "Failing";
        }

        private static string AttitudeWord(float loyalty01)
        {
            if (loyalty01 >= 0.8f) return "Devoted to you";
            if (loyalty01 >= 0.6f) return "Loyal";
            if (loyalty01 >= 0.4f) return "Dutiful";
            if (loyalty01 >= 0.2f) return "Resentful";
            return "Near mutiny";
        }

        private static string HealthColorClass(float v) =>
            v >= 0.75f ? "text-good" : v >= 0.5f ? "text-gold" : "text-danger";

        private static string MoraleColorClass(float v) =>
            v >= 0.7f ? "text-good" : v >= 0.4f ? "text-gold" : v >= 0.2f ? "text-brass" : "text-danger";

        private static string LoyaltyColorClass(float v) =>
            v >= 0.6f ? "text-good" : v >= 0.4f ? "text-dim" : "text-danger";

        private static string MoraleTextClass(float v) =>
            v >= 0.7f ? "text-good" : v >= 0.35f ? "text-gold" : "text-danger";

        private static string EquipmentTextClass(float v) =>
            v >= 0.6f ? "text-good" : v >= 0.35f ? "text-gold" : "text-danger";

        private static string MoraleFillClass(float v) =>
            v >= 0.4f ? "bar__fill--morale" : v >= 0.2f ? "bar__fill--stamina" : "bar__fill--danger";

        private static string HealthFillClass(float v) =>
            v >= 0.5f ? "bar__fill--morale" : "bar__fill--health";

        private static string EquipmentFillClass(float v) =>
            v >= 0.6f ? "bar__fill--morale" : v >= 0.35f ? "bar__fill--gold" : "bar__fill--danger";

        /// <summary>Resolves the handful of fill class names used for meters to their palette colour.</summary>
        private static Color ColorFor(string fillClass)
        {
            switch (fillClass)
            {
                case "bar__fill--morale": return new Color(0.52f, 0.63f, 0.35f); // --olive
                case "bar__fill--stamina": return new Color(0.75f, 0.57f, 0.29f); // --brass
                case "bar__fill--danger":
                case "bar__fill--health": return new Color(0.77f, 0.27f, 0.23f); // --blood
                default: return new Color(0.79f, 0.64f, 0.15f); // --gold
            }
        }

        private static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                case 7: return "VII";
                case 8: return "VIII";
                case 9: return "IX";
                case 10: return "X";
                default: return n.ToString();
            }
        }

        // --- Small helpers ---------------------------------------------------------------------

        private static void Show(VisualElement modal)
        {
            if (modal != null) modal.style.display = DisplayStyle.Flex;
        }

        private static void Hide(VisualElement modal)
        {
            if (modal != null) modal.style.display = DisplayStyle.None;
        }

        private void CloseAllModals()
        {
            Hide(_pickerModal);
            Hide(_soldierModal);
            Hide(_scoutModal);
            Hide(_officeModal);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text;
        }

        private static void SetFill(VisualElement fill, float value01)
        {
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(value01) * 100f);
        }

        private static void Recolour(Label label, string colorClass)
        {
            if (label == null) return;
            label.RemoveFromClassList("text-good");
            label.RemoveFromClassList("text-danger");
            label.RemoveFromClassList("text-gold");
            label.RemoveFromClassList("text-gold-bright");
            label.RemoveFromClassList("text-dim");
            label.RemoveFromClassList("text-faint");
            label.RemoveFromClassList("text-brass");
            if (!string.IsNullOrEmpty(colorClass)) label.AddToClassList(colorClass);
        }

        private static void RecolourFill(VisualElement fill, string fillClass)
        {
            if (fill == null) return;
            fill.RemoveFromClassList("bar__fill--gold");
            fill.RemoveFromClassList("bar__fill--morale");
            fill.RemoveFromClassList("bar__fill--health");
            fill.RemoveFromClassList("bar__fill--stamina");
            fill.RemoveFromClassList("bar__fill--danger");
            if (!string.IsNullOrEmpty(fillClass)) fill.AddToClassList(fillClass);
        }

        private static string FormatDays(float days) =>
            days >= 10f ? $"{days:0} days" : $"{days:0.0} days";

        private static void ApplyScarcity(Label label, float days)
        {
            if (label == null) return;
            label.EnableInClassList("text-danger", days < 2f);
            label.EnableInClassList("text-gold", days >= 2f && days < 5f);
        }
    }
}
