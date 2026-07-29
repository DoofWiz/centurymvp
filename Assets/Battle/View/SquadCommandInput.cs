using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Issues orders to the selected squads, with a propagation delay so that commands are not
    /// telepathic. Selection is spatial: hover a squad in the world and press Tab to toggle it into
    /// the selection; with nothing selected, orders go to the whole century.
    /// </summary>
    /// <remarks>
    /// Formation and behaviour are separate axes, which is why key 1 cycles formation while 2 to 7
    /// set behaviour. That gives the combinations from seven keys instead of seven mutually exclusive
    /// states, and makes "advance in testudo" expressible — which under missile fire is the whole
    /// point of having a testudo at all.
    /// </remarks>
    public sealed class SquadCommandInput : MonoBehaviour
    {
        [Tooltip("How close the cursor must be to a squad (its centre or nearest man) to hover it.")]
        [SerializeField] private float _hoverRadius = 4.5f;

        private BattleState _state;
        private BattleSettings _settings;
        private BattleCameraRig _cameraRig;
        private PlayerCharacterController _player;

        private readonly List<SquadView> _squadViews = new List<SquadView>();
        private readonly HashSet<int> _selected = new HashSet<int>();

        /// <summary>Fired when an order goes out, for UI feedback. Null means a formation was set.</summary>
        public event System.Action<SquadOrder?> OrderIssued;

        /// <summary>Fired when the reinforcements are summoned, for UI feedback.</summary>
        public event System.Action ReinforcementsSummoned;

        /// <summary>True while any of the player's squads wait off the field.</summary>
        public bool HasReinforcements
        {
            get
            {
                for (int i = 0; i < _squadViews.Count; i++)
                {
                    BattleSquad squad = _squadViews[i].Squad;
                    if (squad.IsOffField && !squad.IsDestroyed) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// True while the formation picker is up: key 1 opens it, and the number keys then choose a
        /// formation instead of issuing orders. The HUD mirrors this state visually.
        /// </summary>
        public bool FormationMenuOpen { get; private set; }

        /// <summary>The picker closes itself after this long without a choice, so it cannot get stuck.</summary>
        private const float MenuTimeoutSeconds = 5f;
        private float _menuOpenedAt;

        /// <summary>Player squad under the cursor this frame, or -1. The reticle and Tab both use it.</summary>
        public int HoveredSquadIndex { get; private set; } = -1;

        /// <summary>Enemy squad under the cursor this frame, or null. Reticle information only.</summary>
        public BattleSquad HoveredEnemySquad { get; private set; }

        /// <summary>True when no explicit selection exists, so orders address the whole century.</summary>
        public bool AllSelected => _selected.Count == 0;

        public bool IsSelected(int index) => _selected.Contains(index);

        public int SelectedCount => _selected.Count;

        /// <summary>False outside the fighting phase. Orders make no sense before the battle starts.</summary>
        public bool IsEnabled { get; set; } = true;

        public IReadOnlyList<SquadView> SquadViews => _squadViews;

        public void Bind(
            BattleState state, BattleSettings settings, BattleCameraRig cameraRig,
            PlayerCharacterController player, IEnumerable<SquadView> playerSquadViews)
        {
            _state = state;
            _settings = settings;
            _cameraRig = cameraRig;
            _player = player;

            _squadViews.Clear();
            _squadViews.AddRange(playerSquadViews);
            _selected.Clear();
        }

        private void Update()
        {
            if (_state == null || !IsEnabled)
            {
                HoveredSquadIndex = -1;
                HoveredEnemySquad = null;
                FormationMenuOpen = false;
                return;
            }

            UpdateHover();
            HandleSelection();
            HandleOrders();
        }

        // --- Hover -----------------------------------------------------------------------------

        /// <summary>
        /// What the cursor is over, measured on the ground: the nearest squad whose centre or nearest
        /// man is within the hover radius. Friendlies win ties, because they are the ones Tab acts on.
        /// </summary>
        private void UpdateHover()
        {
            HoveredSquadIndex = -1;
            HoveredEnemySquad = null;

            if (_cameraRig == null || !_cameraRig.TryGetCursorGroundPoint(out Vector3 cursor)) return;

            float best = _hoverRadius;
            for (int i = 0; i < _squadViews.Count; i++)
            {
                BattleSquad squad = _squadViews[i].Squad;
                if (squad.IsDestroyed || squad.IsWithdrawn || squad.IsOffField) continue;

                float d = DistanceToSquad(cursor, squad);
                if (d >= best) continue;
                best = d;
                HoveredSquadIndex = i;
            }

            if (HoveredSquadIndex >= 0) return;

            best = _hoverRadius;
            for (int i = 0; i < _state.EnemySquads.Count; i++)
            {
                BattleSquad squad = _state.EnemySquads[i];
                if (squad.IsDestroyed || squad.IsWithdrawn || squad.IsOffField) continue;

                float d = DistanceToSquad(cursor, squad);
                if (d >= best) continue;
                best = d;
                HoveredEnemySquad = squad;
            }
        }

        private static float DistanceToSquad(Vector3 point, BattleSquad squad)
        {
            Vector3 centre = squad.CentreOfMass();
            centre.y = point.y;
            float best = Vector3.Distance(point, centre);

            List<BattleCombatant> members = squad.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (!members[i].IsAlive) continue;
                Vector3 pos = members[i].WorldPosition;
                pos.y = point.y;
                float d = Vector3.Distance(point, pos);
                if (d < best) best = d;
            }

            return best;
        }

        // --- Selection ---------------------------------------------------------------------------

        /// <summary>Toggles a squad in or out of the selection. Called by Tab and by the HUD blocks.</summary>
        public void ToggleSquad(int index)
        {
            if (index < 0 || index >= _squadViews.Count) return;
            if (!_selected.Remove(index)) _selected.Add(index);
        }

        /// <summary>Clears the selection, returning orders to the whole century.</summary>
        public void SelectAll() => _selected.Clear();

        private void HandleSelection()
        {
            // Tab toggles the squad under the cursor into the selection; more Tabs build the group.
            if (Input.GetKeyDown(KeyCode.Tab) && HoveredSquadIndex >= 0)
                ToggleSquad(HoveredSquadIndex);

            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Alpha0))
                SelectAll();
        }

        // --- Orders ------------------------------------------------------------------------------

        /// <summary>Public entry point for the HUD's formation button: opens or closes the picker.</summary>
        public void ToggleFormationMenu()
        {
            FormationMenuOpen = !FormationMenuOpen;
            if (FormationMenuOpen) _menuOpenedAt = Time.unscaledTime;
        }

        /// <summary>Public entry point for the HUD's formation options. Also closes the picker.</summary>
        public void RequestFormation(FormationType formation) => SetFormation(formation);

        /// <summary>
        /// Public entry point for the HUD's order buttons. Advance uses the cursor position, matching
        /// the keyboard behaviour exactly — the two input surfaces must not diverge.
        /// </summary>
        public void RequestOrder(SquadOrder order) =>
            IssueOrder(order, useCursor: order == SquadOrder.Advance);

        private void HandleOrders()
        {
            if (FormationMenuOpen)
            {
                HandleFormationMenu();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleFormationMenu();
            if (Input.GetKeyDown(KeyCode.Alpha2)) IssueOrder(SquadOrder.Advance, useCursor: true);
            if (Input.GetKeyDown(KeyCode.Alpha3)) IssueOrder(SquadOrder.HoldPosition, useCursor: false);
            if (Input.GetKeyDown(KeyCode.Alpha4)) IssueOrder(SquadOrder.FollowMe, useCursor: false);
            if (Input.GetKeyDown(KeyCode.Alpha5)) IssueOrder(SquadOrder.Skirmish, useCursor: false);
            if (Input.GetKeyDown(KeyCode.Alpha6)) IssueOrder(SquadOrder.Fallback, useCursor: false);
            if (Input.GetKeyDown(KeyCode.Alpha7)) IssueOrder(SquadOrder.Retreat, useCursor: false);
            if (Input.GetKeyDown(KeyCode.Alpha8)) RequestSummon();
        }

        /// <summary>
        /// Calls the reinforcements onto the field: every held-back squad marches on at the entry
        /// point the player marked before the battle, under orders to close on the Centurion.
        /// </summary>
        public void RequestSummon()
        {
            if (!HasReinforcements) return;

            BattleDeployment.Summon(_state.PlayerSquads, SquadOrder.FollowMe);
            ReinforcementsSummoned?.Invoke();
        }

        /// <summary>While the picker is up, the number keys choose a formation rather than an order.</summary>
        private void HandleFormationMenu()
        {
            if (Time.unscaledTime - _menuOpenedAt > MenuTimeoutSeconds
                || Input.GetKeyDown(KeyCode.Escape)
                || Input.GetKeyDown(KeyCode.BackQuote)
                || Input.GetKeyDown(KeyCode.Alpha0))
            {
                FormationMenuOpen = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetFormation(FormationType.Line);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetFormation(FormationType.Testudo);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetFormation(FormationType.Wedge);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SetFormation(FormationType.Loose);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) SetFormation(FormationType.Column);
        }

        private void SetFormation(FormationType formation)
        {
            foreach (BattleSquad squad in SelectedSquads())
            {
                squad.PendingFormation = formation;
                squad.PendingReadyAt = _state.ElapsedSeconds + DelayFor(squad);
            }

            FormationMenuOpen = false;
            OrderIssued?.Invoke(null);
        }

        private void IssueOrder(SquadOrder order, bool useCursor)
        {
            Vector3 cursor = Vector3.zero;
            bool hasCursor = useCursor && _cameraRig != null && _cameraRig.TryGetCursorGroundPoint(out cursor);

            foreach (BattleSquad squad in SelectedSquads())
            {
                squad.PendingOrder = order;

                squad.PendingOrderedPosition = order == SquadOrder.HoldPosition
                    ? squad.AnchorPosition
                    : hasCursor
                        ? cursor
                        : squad.AnchorPosition;

                // Skirmishing and running for the edge both imply open order — nobody does either
                // shoulder to shoulder.
                if (order == SquadOrder.Skirmish || order == SquadOrder.Retreat)
                    squad.PendingFormation = FormationType.Loose;

                squad.PendingReadyAt = _state.ElapsedSeconds + DelayFor(squad);
            }

            OrderIssued?.Invoke(order);
        }

        /// <summary>
        /// Distance from the commander, shortened by the squad's tesserarius. Standing where your
        /// men can hear you is a real tactical consideration rather than flavour.
        /// </summary>
        private float DelayFor(BattleSquad squad)
        {
            float distance = _player == null
                ? 0f
                : Vector3.Distance(_player.transform.position, squad.AnchorPosition);

            float delay = _settings.OrderBaseDelaySeconds + distance * _settings.OrderDelayPerMetre;
            if (squad.HasOfficer(OfficerRole.Tesserarius)) delay *= _settings.TesserariusDelayMultiplier;

            // "Roman Order": drilled until the order and the act are the same thing.
            if (_state.HasSkill("roman_order")) delay *= 0.7f;

            return Mathf.Min(delay, _settings.MaxOrderDelaySeconds);
        }

        private IEnumerable<BattleSquad> SelectedSquads()
        {
            for (int i = 0; i < _squadViews.Count; i++)
            {
                if (_selected.Count > 0 && !_selected.Contains(i)) continue;

                BattleSquad squad = _squadViews[i].Squad;

                // Broken men cannot be commanded, only rallied; men off the field cannot hear you
                // at all until summoned. Skipping them here rather than silently queueing orders
                // they will discard keeps the feedback honest.
                if (squad.IsDestroyed || squad.IsRouted || squad.IsWithdrawn || squad.IsOffField) continue;
                yield return squad;
            }
        }
    }
}
