using System;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using Century.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// The pre-battle screen: a modal overlay with a top-down tactical map. Choose which contubernia
    /// take the field, where the line forms, and which edge the reserve marches in from.
    /// </summary>
    /// <remarks>
    /// Placement happens on the map widget rather than in the world, and that is the important
    /// decision. Clicking the world made the camera a dependency of a choice that should not need one:
    /// the far edge of the field is unreachable while the camera is locked to a commander who is not
    /// yet supposed to be moving. A 2D map has no such problem — every part of the field is on screen
    /// at once, at a glance, which is what a deployment decision actually wants.
    ///
    /// The phase is genuinely modal. World input is gated off by the bootstrap while this is up, so
    /// there is no possibility of driving the Centurion around behind the overlay.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleDeploymentController : MonoBehaviour
    {
        private enum PlacementStep
        {
            Vanguard = 0,
            Reinforcement = 1
        }

        [Header("Field extents")]
        [Tooltip("Half-width of the battlefield in world units. Should match your battle ground.")]
        [SerializeField] private float _fieldHalfWidth = 70f;

        [Tooltip("Half-depth of the battlefield in world units.")]
        [SerializeField] private float _fieldHalfDepth = 70f;

        [Tooltip("How close to an edge a click must land to count as choosing that edge, in world units.")]
        [SerializeField] private float _edgeBandWidth = 18f;

        private UIDocument _document;
        private BattleState _state;
        private BattleSettings _settings;

        private VisualElement _panel, _squadList, _map, _zoneMarker, _entryMarker, _commanderMarker;
        private Label _subtitle, _deployedCount, _reservedCount, _enemyCount, _status;
        private Button _confirmButton, _stepVanguard, _stepReinforce;

        private readonly List<Action> _rowRefreshers = new List<Action>();
        private readonly List<VisualElement> _unitMarkers = new List<VisualElement>();

        private PlacementStep _step = PlacementStep.Vanguard;

        public bool IsActive { get; private set; }

        /// <summary>Raised when the player commits. The bootstrap starts the fight.</summary>
        public event Action Confirmed;

        private void Awake() => _document = GetComponent<UIDocument>();

        public void Begin(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;

            VisualElement root = _document.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[Deployment] UIDocument has no root; skipping deployment.", this);
                Confirmed?.Invoke();
                return;
            }

            // Without an EventSystem this whole screen renders correctly and accepts no clicks at all.
            UiInputBootstrapper.EnsureEventSystem();

            // Hidden here rather than via the HUD controller, so a missing inspector reference cannot
            // leave the combat interface showing underneath the overlay.
            BattleHudPanels.SetCombatVisible(root, false);

            BattleDeployment.PrepareZone(state, settings, _fieldHalfDepth);
            BattleDeployment.ApplyDeployment(state, settings);

            CacheElements(root);
            BuildMapChrome();
            BuildSquadList();
            SetStep(PlacementStep.Vanguard);
            RefreshAll();

            if (_panel != null) _panel.style.display = DisplayStyle.Flex;
            IsActive = true;
        }

        private void CacheElements(VisualElement root)
        {
            _panel = root.Q<VisualElement>("deployment-panel");
            _squadList = root.Q<VisualElement>("deployment-squads");
            _map = root.Q<VisualElement>("deployment-map");
            _subtitle = root.Q<Label>("deployment-subtitle");
            _deployedCount = root.Q<Label>("deployment-deployed");
            _reservedCount = root.Q<Label>("deployment-reserved");
            _enemyCount = root.Q<Label>("deployment-enemy");
            _status = root.Q<Label>("deployment-status");

            _confirmButton = root.Q<Button>("deployment-confirm");
            if (_confirmButton != null) _confirmButton.clicked += Confirm;

            _stepVanguard = root.Q<Button>("deployment-step-vanguard");
            if (_stepVanguard != null) _stepVanguard.clicked += () => SetStep(PlacementStep.Vanguard);

            _stepReinforce = root.Q<Button>("deployment-step-reinforce");
            if (_stepReinforce != null) _stepReinforce.clicked += () => SetStep(PlacementStep.Reinforcement);

            if (_subtitle != null) _subtitle.text = _state.PlayerAmbushed
                ? $"Ambushed by {_state.EnemyDisplayName} — little time to form"
                : _state.EnemyAmbushed
                    ? $"The trap is sprung — {_state.EnemyDisplayName} never saw you. Deploy freely."
                    : $"Facing {_state.EnemyDisplayName}";

            if (_map != null) _map.RegisterCallback<PointerDownEvent>(OnMapClicked);
        }

        // --- Map ------------------------------------------------------------------------------

        /// <summary>Builds the static furniture of the map: midline, deployment zone, edge bands.</summary>
        private void BuildMapChrome()
        {
            if (_map == null) return;

            _map.Clear();
            _unitMarkers.Clear();

            var midline = new VisualElement { pickingMode = PickingMode.Ignore };
            midline.AddToClassList("tacmap__midline");
            midline.style.top = Length.Percent(50f);
            _map.Add(midline);

            // The band along the player's rear and flanks where reinforcements may enter.
            AddEdgeBand(0f, 100f - PercentFromWorldDepth(_edgeBandWidth), 100f, PercentFromWorldDepth(_edgeBandWidth));

            _zoneMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _zoneMarker.AddToClassList("tacmap__zone");
            _map.Add(_zoneMarker);

            _entryMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _entryMarker.AddToClassList("tacmap__entry");
            _map.Add(_entryMarker);

            _commanderMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _commanderMarker.AddToClassList("tacmap__commander");
            _map.Add(_commanderMarker);

            AddMapLabel("ENEMY", 4f, 2f);
            AddMapLabel("YOUR SIDE", 4f, 92f);
        }

        private void AddEdgeBand(float leftPercent, float topPercent, float widthPercent, float heightPercent)
        {
            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.AddToClassList("tacmap__edge-band");
            band.style.left = Length.Percent(leftPercent);
            band.style.top = Length.Percent(topPercent);
            band.style.width = Length.Percent(widthPercent);
            band.style.height = Length.Percent(heightPercent);
            _map.Add(band);
        }

        private void AddMapLabel(string text, float leftPercent, float topPercent)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("tacmap__label");
            label.style.left = Length.Percent(leftPercent);
            label.style.top = Length.Percent(topPercent);
            _map.Add(label);
        }

        /// <summary>Redraws every unit marker. Cheap enough to rebuild wholesale on any change.</summary>
        private void RefreshMap()
        {
            if (_map == null) return;

            for (int i = 0; i < _unitMarkers.Count; i++) _map.Remove(_unitMarkers[i]);
            _unitMarkers.Clear();

            // Deployment zone rectangle.
            if (_zoneMarker != null)
            {
                Vector2 centre = WorldToPercent(_state.DeploymentZoneCentre);
                float halfWidthPercent = PercentFromWorldWidth(_state.DeploymentZoneExtents.x);
                float halfDepthPercent = PercentFromWorldDepth(_state.DeploymentZoneExtents.y);

                _zoneMarker.style.left = Length.Percent(centre.x - halfWidthPercent);
                _zoneMarker.style.top = Length.Percent(centre.y - halfDepthPercent);
                _zoneMarker.style.width = Length.Percent(halfWidthPercent * 2f);
                _zoneMarker.style.height = Length.Percent(halfDepthPercent * 2f);
            }

            // No enemy markers: the player has no intelligence on where the enemy commander is
            // deploying or what he holds back. Until scouting skills exist, all he gets is the
            // rough strength estimate in the forces tally.

            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                BattleSquad squad = _state.PlayerSquads[s];
                bool reserved = _state.ReservedSquadIndices.Contains(squad.Index);
                AddSquadMarkers(squad, reserved ? "tacmap__marker--reserve" : "tacmap__marker--ours");
            }

            if (_entryMarker != null)
            {
                Vector2 entry = WorldToPercent(_state.ReinforcementPoint);
                _entryMarker.style.left = Length.Percent(entry.x);
                _entryMarker.style.top = Length.Percent(entry.y);
                _entryMarker.style.translate = new Translate(-10f, -10f);
            }

            if (_commanderMarker != null && _state.PlayerCharacter != null)
            {
                Vector2 commander = WorldToPercent(_state.PlayerCharacter.WorldPosition);
                _commanderMarker.style.left = Length.Percent(commander.x);
                _commanderMarker.style.top = Length.Percent(commander.y);
                _commanderMarker.style.translate = new Translate(-6.5f, -6.5f);
            }
        }

        private void AddSquadMarkers(BattleSquad squad, string markerClass)
        {
            for (int m = 0; m < squad.Members.Count; m++)
            {
                BattleCombatant man = squad.Members[m];
                if (!man.IsAlive) continue;

                Vector2 position = WorldToPercent(man.WorldPosition);

                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("tacmap__marker");
                marker.AddToClassList(markerClass);
                marker.style.left = Length.Percent(position.x);
                marker.style.top = Length.Percent(position.y);
                marker.style.translate = new Translate(-3.5f, -3.5f);

                _map.Add(marker);
                _unitMarkers.Add(marker);
            }
        }

        // --- Coordinate mapping ---------------------------------------------------------------

        /// <summary>
        /// World to map percentage. The player's side is drawn at the bottom, which is how anyone
        /// reading a battle map expects to see their own line.
        /// </summary>
        private Vector2 WorldToPercent(Vector3 world)
        {
            float x = Mathf.InverseLerp(-_fieldHalfWidth, _fieldHalfWidth, world.x) * 100f;
            float y = Mathf.InverseLerp(_fieldHalfDepth, -_fieldHalfDepth, world.z) * 100f;
            return new Vector2(x, y);
        }

        private Vector3 LocalToWorld(Vector2 localPoint)
        {
            Rect bounds = _map.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return Vector3.zero;

            float u = Mathf.Clamp01(localPoint.x / bounds.width);
            float v = Mathf.Clamp01(localPoint.y / bounds.height);

            return new Vector3(
                Mathf.Lerp(-_fieldHalfWidth, _fieldHalfWidth, u),
                0f,
                Mathf.Lerp(_fieldHalfDepth, -_fieldHalfDepth, v));
        }

        private float PercentFromWorldWidth(float worldUnits) => worldUnits / (_fieldHalfWidth * 2f) * 100f;
        private float PercentFromWorldDepth(float worldUnits) => worldUnits / (_fieldHalfDepth * 2f) * 100f;

        // --- Interaction ----------------------------------------------------------------------

        private void OnMapClicked(PointerDownEvent evt)
        {
            if (!IsActive || _state == null) return;

            Vector3 world = LocalToWorld(evt.localPosition);

            if (_step == PlacementStep.Vanguard) PlaceVanguard(world);
            else PlaceReinforcement(world);

            evt.StopPropagation();
        }

        /// <summary>
        /// Clamps into the deployment zone rather than refusing. Rejecting a click with an error
        /// teaches the player nothing; snapping to the nearest legal point shows them the boundary.
        /// </summary>
        private void PlaceVanguard(Vector3 world)
        {
            Vector3 centre = _state.DeploymentZoneCentre;
            Vector2 extents = _state.DeploymentZoneExtents;

            var clamped = new Vector3(
                Mathf.Clamp(world.x, centre.x - extents.x, centre.x + extents.x),
                0f,
                Mathf.Clamp(world.z, centre.z - extents.y, centre.z + extents.y));

            _state.VanguardAnchor = clamped;
            _state.HasChosenVanguard = true;

            BattleDeployment.ApplyDeployment(_state, _settings);
            SnapViewsToDeployment();
            RefreshAll();
        }

        private void PlaceReinforcement(Vector3 world)
        {
            Vector3 snapped = BattleDeployment.SnapToFieldEdge(world, _fieldHalfWidth, _fieldHalfDepth);

            _state.ReinforcementPoint = snapped;
            _state.HasChosenReinforcementPoint = true;

            BattleDeployment.ApplyDeployment(_state, _settings);
            SnapViewsToDeployment();
            RefreshAll();
        }

        private void SetStep(PlacementStep step)
        {
            _step = step;

            _stepVanguard?.EnableInClassList("step--active", step == PlacementStep.Vanguard);
            _stepReinforce?.EnableInClassList("step--active", step == PlacementStep.Reinforcement);

            RefreshStatus();
        }

        // --- Squad list -----------------------------------------------------------------------

        /// <summary>
        /// One row per contubernium with an explicit two-state switch. The previous version toggled on
        /// a click anywhere in the row with nothing to indicate it was clickable, which is why it read
        /// as inert.
        /// </summary>
        private void BuildSquadList()
        {
            if (_squadList == null) return;

            _squadList.Clear();
            _rowRefreshers.Clear();

            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                int squadIndex = squad.Index;

                var row = new VisualElement();
                row.AddToClassList("squad-pick");

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.AddToClassList("squad-pick__info");

                var name = new Label(squad.DisplayName) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("squad-pick__name");
                info.Add(name);

                var detail = new Label(DescribeSquad(squad)) { pickingMode = PickingMode.Ignore };
                detail.AddToClassList("squad-pick__detail");
                info.Add(detail);

                row.Add(info);

                var switchRoot = new VisualElement();
                switchRoot.AddToClassList("switch");

                Button fieldOption = MakeSwitchOption("FIELD");
                Button reserveOption = MakeSwitchOption("RESERVE");

                fieldOption.clicked += () =>
                {
                    _state.ReservedSquadIndices.Remove(squadIndex);
                    ApplyAndRefresh();
                };

                reserveOption.clicked += () =>
                {
                    _state.ReservedSquadIndices.Add(squadIndex);
                    ApplyAndRefresh();
                };

                switchRoot.Add(fieldOption);
                switchRoot.Add(reserveOption);
                row.Add(switchRoot);

                _squadList.Add(row);

                _rowRefreshers.Add(() =>
                {
                    bool reserved = _state.ReservedSquadIndices.Contains(squadIndex);
                    row.EnableInClassList("squad-pick--reserve", reserved);
                    fieldOption.EnableInClassList("switch__option--on", !reserved);
                    reserveOption.EnableInClassList("switch__option--on", reserved);
                });
            }
        }

        private static Button MakeSwitchOption(string text)
        {
            var button = new Button();
            button.AddToClassList("switch__option");

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("switch__label");
            button.Add(label);

            return button;
        }

        private static string DescribeSquad(BattleSquad squad)
        {
            int officers = 0;
            for (int i = 0; i < squad.Members.Count; i++)
                if (squad.Members[i].IsAlive && squad.Members[i].IsOfficer) officers++;

            string officerText = officers > 0
                ? $"{officers} officer{(officers > 1 ? "s" : "")}"
                : "no officers";

            return $"{squad.AliveCount} men · {officerText}";
        }

        private void ApplyAndRefresh()
        {
            BattleDeployment.ApplyDeployment(_state, _settings);
            SnapViewsToDeployment();
            RefreshAll();
        }

        // --- Refresh --------------------------------------------------------------------------

        private void RefreshAll()
        {
            for (int i = 0; i < _rowRefreshers.Count; i++) _rowRefreshers[i]();

            if (_deployedCount != null)
                _deployedCount.text = BattleDeployment.CountDeployed(_state).ToString();

            if (_reservedCount != null)
                _reservedCount.text = BattleDeployment.CountReserved(_state).ToString();

            // An estimate, not a count: without a scout's report the player knows roughly how many
            // stand against him, never exactly, and nothing of what is held back.
            if (_enemyCount != null)
            {
                int estimate = Mathf.Max(10, Mathf.RoundToInt(_state.EnemyAliveCount / 10f) * 10);
                _enemyCount.text = $"~{estimate}";
            }

            _stepVanguard?.EnableInClassList("step--done", _state.HasChosenVanguard);
            _stepReinforce?.EnableInClassList("step--done", _state.HasChosenReinforcementPoint);

            RefreshMap();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_status == null) return;

            _status.text = _step == PlacementStep.Vanguard
                ? "Click the gold ground to form your line, or press START BATTLE to accept the default."
                : "Click near an edge of your own half to choose where the reserve marches in.";
        }

        /// <summary>
        /// Teleports the spawned soldiers onto their new slots. Without this they would walk there,
        /// which looks odd during a phase that is supposed to be outside time.
        /// </summary>
        private void SnapViewsToDeployment() => SnapRequested?.Invoke();

        /// <summary>The bootstrap owns the soldier views, so it performs the snap.</summary>
        public event Action SnapRequested;

        private void Update()
        {
            if (!IsActive) return;

            if (Input.GetKeyDown(KeyCode.Tab))
                SetStep(_step == PlacementStep.Vanguard ? PlacementStep.Reinforcement : PlacementStep.Vanguard);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        }

        private void Confirm()
        {
            if (!IsActive) return;

            BattleDeployment.ApplyDeployment(_state, _settings);
            SnapViewsToDeployment();

            IsActive = false;
            if (_panel != null) _panel.style.display = DisplayStyle.None;

            BattleHudPanels.SetCombatVisible(_document.rootVisualElement, true);

            Confirmed?.Invoke();
        }
    }
}
