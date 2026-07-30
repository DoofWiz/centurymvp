using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;
using UnityEngine.AI;

namespace Century.Battle.View
{
    /// <summary>
    /// Drives a squad's anchor — the moving point its formation is built around — according to its
    /// current order, and resolves pending orders once they have propagated.
    /// </summary>
    /// <remarks>
    /// Soldiers never path to the enemy or to the player directly. They path to a slot, and the slot
    /// comes from the anchor. Concentrating all squad-level movement in one place is what stops ten
    /// men from each making their own decision about where the line should be.
    /// </remarks>
    public sealed class SquadView : MonoBehaviour
    {
        [SerializeField] private float _anchorTurnSpeed = 180f;

        [Header("Order behaviour")]
        [Tooltip("How close an advancing formation's ANCHOR stops to the enemy squad's centre. This " +
                 "must clear both formations' half-depths: at 3 the halt point sat INSIDE the enemy " +
                 "line, so the two slot grids interleaved and every clash dissolved into a blob. At " +
                 "6.5 the line halts a spear-length short and receives the charge front to front.")]
        [SerializeField] private float _advanceContactStandoff = 6.5f;

        [Tooltip("A falling-back squad keeps backing away until this gap to the enemy is opened.")]
        [SerializeField] private float _fallbackHoldGap = 26f;

        [Tooltip("A retreating squad is counted out of the fight once this far from the enemy.")]
        [SerializeField] private float _retreatEscapeDistance = 50f;

        [Tooltip("While following, a squad turns to face an enemy within this range instead of the Centurion.")]
        [SerializeField] private float _followFaceEnemyRange = 11f;

        [Header("Squad separation")]
        [Tooltip("Squads within this range push apart so lines do not cross while manoeuvring.")]
        [SerializeField] private float _squadSeparationRadius = 6f;
        [SerializeField] private float _squadSeparationStrength = 4f;

        private BattleSquad _squad;
        private BattleSettings _settings;
        private BattleState _state;
        private PlayerCharacterController _player;

        public BattleSquad Squad => _squad;

        public void Bind(
            BattleSquad squad, BattleState state, BattleSettings settings, PlayerCharacterController player)
        {
            _squad = squad;
            _state = state;
            _settings = settings;
            _player = player;

            name = $"Squad [{squad.DisplayName}]";
            transform.position = squad.AnchorPosition;
        }

        private void Update()
        {
            if (_squad == null || _squad.IsDestroyed) return;

            _squad.ResolvePendingOrder(_state.ElapsedSeconds);

            // A broken or withdrawn squad's anchor follows the men rather than leading them. Nobody is
            // dressing ranks during a rout, and leaving the anchor behind would drag them back in.
            if (_squad.IsRouted || _squad.IsWithdrawn)
            {
                _squad.AnchorPosition = _squad.CentreOfMass();
                transform.position = _squad.AnchorPosition;
                return;
            }

            Vector3 target = ResolveAnchorTarget(out Vector3 facing);
            MoveAnchorToward(target);
            TurnAnchorToward(facing);

            // Bind men to the slots nearest where they stand, so a turn pivots the unit as a body
            // rather than scattering it across the ranks. A no-op while the line is in contact.
            FormationAssignment.Reassign(_squad, _settings);

            transform.position = _squad.AnchorPosition;
        }

        /// <summary>Where the anchor wants to be, and which way the formation should face, per order.</summary>
        private Vector3 ResolveAnchorTarget(out Vector3 facing)
        {
            facing = _squad.AnchorFacing;

            switch (_squad.Order)
            {
                case SquadOrder.FollowMe:
                {
                    if (_player == null || !_squad.IsPlayerSide) return _squad.AnchorPosition;

                    // Form a screen IN FRONT of the Centurion — he commands from just behind the
                    // line, where a commander belongs. Each squad takes the position nearest it (so
                    // it doesn't run the length of the line), and holds that spot when he turns
                    // rather than swapping sides with its neighbour.
                    Vector3 playerFacing = SquadFormationSolver.FlatFacing(_player.Facing);
                    Vector3 right = Vector3.Cross(Vector3.up, playerFacing);

                    float lateral = FollowFormation.LateralFor(
                        _squad, _state.PlayerSquads, _player.transform.position, _player.Facing, _settings);

                    Vector3 abreast = _player.transform.position
                                      + right * lateral
                                      + playerFacing * (_settings.FollowDistance * 0.9f);

                    // Turn to meet a nearby enemy, but stay in position; otherwise march with the Centurion.
                    facing = playerFacing;
                    if (TryNearestOpponent(out Vector3 threat, out float gap) && gap < _followFaceEnemyRange)
                    {
                        Vector3 toThreat = threat - _squad.AnchorPosition;
                        toThreat.y = 0f;
                        if (toThreat.sqrMagnitude > 0.01f) facing = toThreat.normalized;
                    }

                    return abreast + FriendlySeparation();
                }

                case SquadOrder.Advance:
                {
                    // The player's advance is an attack-move: walk the formation onto the nearest enemy
                    // and stop at contact so the lines meet front to front. Enemy squads keep their AI's
                    // aim point (which encodes flanking), so only the player side re-seeks here.
                    Vector3 goal = _squad.OrderedPosition;

                    if (_squad.IsPlayerSide && TryNearestOpponent(out Vector3 enemyPos, out _))
                    {
                        Vector3 to = enemyPos - _squad.AnchorPosition;
                        to.y = 0f;
                        goal = to.magnitude > _advanceContactStandoff
                            ? enemyPos - to.normalized * _advanceContactStandoff
                            : _squad.AnchorPosition;
                    }

                    Vector3 toGoal = goal - _squad.AnchorPosition;
                    toGoal.y = 0f;
                    if (toGoal.sqrMagnitude > 0.25f) facing = toGoal.normalized;

                    // Men on the edge of breaking will not walk forwards, whatever they are told.
                    // The order is not forgotten — it resumes if they are steadied.
                    return _squad.Band.WillAdvance() ? goal + FriendlySeparation() : _squad.AnchorPosition;
                }

                case SquadOrder.Fallback:
                {
                    // A fighting withdrawal: back away from the enemy while keeping the shields to the
                    // front, until a clean gap is opened. Separation keeps squads from crossing.
                    if (!TryNearestOpponent(out Vector3 threat, out float gap))
                        return _squad.AnchorPosition;

                    Vector3 toThreat = threat - _squad.AnchorPosition;
                    toThreat.y = 0f;
                    Vector3 enemyDir = toThreat.sqrMagnitude > 0.01f
                        ? toThreat.normalized
                        : SquadFormationSolver.FlatFacing(_squad.AnchorFacing);
                    facing = enemyDir;   // shields stay to the enemy

                    Vector3 goal = gap < _fallbackHoldGap
                        ? _squad.AnchorPosition - enemyDir * 4f
                        : _squad.AnchorPosition;

                    return goal + FriendlySeparation();
                }

                case SquadOrder.Retreat:
                {
                    // Break clear of the field. Run straight away from the enemy; once well clear, the
                    // squad is marked withdrawn and leaves the fight for good.
                    Vector3 awayDir = SquadFormationSolver.FlatFacing(_squad.AnchorFacing);

                    if (TryNearestOpponent(out Vector3 threat, out float gap))
                    {
                        Vector3 toThreat = threat - _squad.AnchorPosition;
                        toThreat.y = 0f;
                        if (toThreat.sqrMagnitude > 0.01f) awayDir = -toThreat.normalized;
                        if (gap >= _retreatEscapeDistance) _squad.IsWithdrawn = true;
                    }

                    facing = awayDir;
                    return _squad.AnchorPosition + awayDir * 40f;
                }

                case SquadOrder.Skirmish:
                case SquadOrder.HoldPosition:
                default:
                    return _squad.OrderedPosition;
            }
        }

        /// <summary>Nearest opposing squad centre (or the lone Centurion, to an enemy squad).</summary>
        private bool TryNearestOpponent(out Vector3 position, out float distance)
        {
            position = _squad.AnchorPosition;
            distance = float.MaxValue;

            System.Collections.Generic.List<BattleSquad> opponents =
                _squad.IsPlayerSide ? _state.EnemySquads : _state.PlayerSquads;

            for (int i = 0; i < opponents.Count; i++)
            {
                if (!opponents[i].IsEffective) continue;

                Vector3 centre = opponents[i].CentreOfMass();
                float d = Vector3.Distance(_squad.AnchorPosition, centre);
                if (d >= distance) continue;

                position = centre;
                distance = d;
            }

            if (!_squad.IsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive)
                {
                    float d = Vector3.Distance(_squad.AnchorPosition, player.WorldPosition);
                    if (d < distance) { position = player.WorldPosition; distance = d; }
                }
            }

            return distance < float.MaxValue;
        }

        /// <summary>A soft push away from friendly squads within range, so lines do not slide through one another.</summary>
        private Vector3 FriendlySeparation()
        {
            Vector3 push = Vector3.zero;

            System.Collections.Generic.List<BattleSquad> friends =
                _squad.IsPlayerSide ? _state.PlayerSquads : _state.EnemySquads;

            for (int i = 0; i < friends.Count; i++)
            {
                BattleSquad other = friends[i];
                if (other == _squad || !other.IsEffective) continue;

                Vector3 away = _squad.AnchorPosition - other.AnchorPosition;
                away.y = 0f;
                float d = away.magnitude;
                if (d < 0.001f || d > _squadSeparationRadius) continue;

                push += away.normalized * ((_squadSeparationRadius - d) / _squadSeparationRadius) * _squadSeparationStrength;
            }

            return push;
        }

        private void MoveAnchorToward(Vector3 target)
        {
            // Formations march at the ordinary pace — the sprint belongs to individual men
            // (charging, catching up, fleeing), never to a dressed line keeping step.
            float speed = _settings.SoldierBaseSpeed * _settings.NormalMoveFraction
                          * _squad.Formation.SpeedMultiplier();

            // Once the lines meet, the anchor STOPS and the men fight where they stand — even a 15%
            // creep, held for a whole melee, walked formations through each other into one brawl.
            // Only a withdrawal keeps moving under contact: that is what a fighting withdrawal is.
            if (_squad.EngagedCount > 0)
            {
                bool withdrawing = _squad.Order == SquadOrder.Fallback || _squad.Order == SquadOrder.Retreat;
                speed *= withdrawing ? 0.35f : 0f;
            }

            Vector3 next = Vector3.MoveTowards(_squad.AnchorPosition, target, speed * Time.deltaTime);

            // Keep the anchor on walkable ground so slots never resolve into scenery.
            if (NavMesh.SamplePosition(next, out NavMeshHit hit, 4f, NavMesh.AllAreas)) next = hit.position;

            _squad.AnchorPosition = next;
        }

        private void TurnAnchorToward(Vector3 facing)
        {
            facing = SquadFormationSolver.FlatFacing(facing);

            _squad.AnchorFacing = Vector3.RotateTowards(
                SquadFormationSolver.FlatFacing(_squad.AnchorFacing),
                facing,
                _anchorTurnSpeed * Mathf.Deg2Rad * Time.deltaTime,
                0f);
        }

        private void OnDrawGizmos()
        {
            if (_squad == null || _settings == null) return;

            Gizmos.color = _squad.IsRouted
                ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                : _squad.IsPlayerSide
                    ? new Color(0.9f, 0.75f, 0.35f, 0.8f)
                    : new Color(0.8f, 0.3f, 0.3f, 0.8f);

            for (int i = 0; i < _squad.Members.Count; i++)
            {
                if (!_squad.Members[i].IsAlive) continue;
                Gizmos.DrawWireCube(SquadFormationSolver.GetWorldSlot(_squad, i, _settings) + Vector3.up * 0.1f,
                    new Vector3(0.5f, 0.05f, 0.5f));
            }

            Gizmos.DrawLine(_squad.AnchorPosition, _squad.AnchorPosition + SquadFormationSolver.FlatFacing(_squad.AnchorFacing) * 3f);
        }
    }
}
