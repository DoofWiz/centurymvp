using System.Collections.Generic;
using Century.Battle.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Drives enemy squads according to their commander's doctrine (<see cref="BattleState.EnemyBehaviour"/>):
    /// a Fanatic charges, a Skirmisher keeps its distance, a Disciplined commander holds a line and
    /// works a flank, a Wary one waits for the Romans to overextend.
    /// </summary>
    /// <remarks>
    /// The AI only ever writes squad-level intent — <see cref="SquadOrder"/>, formation, ordered
    /// position and facing. The views turn that into movement, and the melee resolver into blows.
    /// Skirmisher javelins are the one thing it does not own: those are thrown by BattleMissiles in the
    /// view, which reads the same behaviour flag.
    /// </remarks>
    public sealed class EnemySquadAi
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private float _timer;

        private int _committedAtStart = -1;
        private bool _reserveSummoned;
        private bool _fightJoined;

        public EnemySquadAi(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;
        }

        public void Tick(float deltaSeconds)
        {
            _timer += deltaSeconds;
            if (_timer < _settings.EnemyDecisionSeconds) return;
            _timer = 0f;

            ConsiderSummoningReserve();

            // Once ANY warband is trading blows the waiting game is over for the whole warhost —
            // this is what frees a Wary commander's reinforcements to march instead of standing at
            // the field edge waiting for a Roman to wander within commit range of each of them.
            _fightJoined = _fightJoined || AnyEngaged();

            for (int i = 0; i < _state.EnemySquads.Count; i++)
            {
                BattleSquad squad = _state.EnemySquads[i];
                if (!squad.IsEffective || squad.IsOffField) continue;
                Decide(squad);
            }
        }

        private bool AnyEngaged()
        {
            for (int i = 0; i < _state.EnemySquads.Count; i++)
            {
                BattleSquad squad = _state.EnemySquads[i];
                if (!squad.IsOffField && squad.EngagedCount > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// The enemy commander commits his held-back warbands when his line is telling him to: a
        /// third of the men he committed are down, or the committed line has broken entirely.
        /// </summary>
        private void ConsiderSummoningReserve()
        {
            if (_reserveSummoned) return;

            int committedAlive = 0;
            bool anyCommittedEffective = false;
            bool anyHeld = false;

            for (int i = 0; i < _state.EnemySquads.Count; i++)
            {
                BattleSquad squad = _state.EnemySquads[i];
                if (squad.IsOffField)
                {
                    if (!squad.IsDestroyed) anyHeld = true;
                    continue;
                }

                committedAlive += squad.AliveCount;
                if (squad.IsEffective) anyCommittedEffective = true;
            }

            if (!anyHeld)
            {
                _reserveSummoned = true;   // nothing to summon, stop checking
                return;
            }

            if (_committedAtStart < 0)
            {
                _committedAtStart = Mathf.Max(1, committedAlive);
                return;
            }

            bool bleeding = committedAlive <= _committedAtStart * 0.65f;
            if (!bleeding && anyCommittedEffective) return;

            _reserveSummoned = true;
            BattleDeployment.Summon(_state.EnemySquads, SquadOrder.Advance);
        }

        private void Decide(BattleSquad squad)
        {
            Vector3 centre = squad.CentreOfMass();
            Vector3 target = FindNearestPlayerPosition(centre, out float distance);

            if (distance >= float.MaxValue)
            {
                squad.Order = SquadOrder.HoldPosition;
                squad.OrderedPosition = squad.AnchorPosition;
                return;
            }

            // Fresh reinforcements swing at a flank while the committed line holds the front —
            // marching them into the back of their own fight wastes the one advantage they bring.
            if (squad.ArrivedAsReserve && distance > _settings.EnemyChargeRange * 0.6f)
            {
                Vector3 toTarget = target - centre;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized)
                                   * (squad.Index % 2 == 0 ? 1f : -1f);
                    squad.Order = SquadOrder.Advance;
                    squad.OrderedPosition = target + side * (_settings.SquadFrontage * 2f);
                    squad.Formation = FormationType.Line;
                    return;
                }
            }

            switch (_state.EnemyBehaviour)
            {
                case CommanderBehaviour.Skirmisher: DecideSkirmisher(squad, centre, target, distance); break;
                case CommanderBehaviour.Wary: DecideWary(squad, target, distance); break;
                case CommanderBehaviour.Disciplined: DecideDisciplined(squad, centre, target, distance); break;
                default: DecideFanatic(squad, target, distance); break;
            }
        }

        /// <summary>Headlong at the nearest Roman, tightening into a wedge for the charge.</summary>
        private void DecideFanatic(BattleSquad squad, Vector3 target, float distance)
        {
            if (HoldWhenEngaged(squad)) return;

            squad.Order = SquadOrder.Advance;
            squad.OrderedPosition = target;
            squad.Formation = distance <= _settings.EnemyChargeRange ? FormationType.Wedge : FormationType.Loose;
        }

        /// <summary>Advance in a shield line; odd squads swing wide to take a flank.</summary>
        private void DecideDisciplined(BattleSquad squad, Vector3 centre, Vector3 target, float distance)
        {
            if (HoldWhenEngaged(squad)) return;

            Vector3 aim = target;

            // Every other squad peels off to come in from the side rather than piling into the front.
            if (squad.Index % 2 == 1 && distance > _settings.EnemyChargeRange * 0.5f)
            {
                Vector3 toTarget = target - centre;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
                    aim = target + side * (_settings.SquadFrontage * 1.6f);
                }
            }

            squad.Order = SquadOrder.Advance;
            squad.OrderedPosition = aim;
            squad.Formation = FormationType.Line;
        }

        /// <summary>Hold off until a Roman comes within committing range — or until the fight is
        /// joined anywhere, after which waiting is desertion, not doctrine.</summary>
        private void DecideWary(BattleSquad squad, Vector3 target, float distance)
        {
            if (HoldWhenEngaged(squad)) return;

            if (!_fightJoined && distance > _settings.WaryCommitRange)
            {
                squad.Order = SquadOrder.HoldPosition;
                squad.OrderedPosition = squad.AnchorPosition;
                squad.Formation = FormationType.Loose;
                return;
            }

            squad.Order = SquadOrder.Advance;
            squad.OrderedPosition = target;
            squad.Formation = distance <= _settings.EnemyChargeRange ? FormationType.Wedge : FormationType.Loose;
        }

        /// <summary>Keep to javelin range, give ground when pressed, and never willingly close.</summary>
        private void DecideSkirmisher(BattleSquad squad, Vector3 centre, Vector3 target, float distance)
        {
            squad.Formation = FormationType.Loose;
            squad.Order = SquadOrder.Skirmish;

            // Face the enemy while kiting, so a caught skirmisher at least fights front-on.
            Vector3 toTarget = target - centre;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f) squad.AnchorFacing = toTarget.normalized;

            if (distance < _settings.SkirmishMinRange)
            {
                Vector3 away = centre - target;
                away.y = 0f;
                Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : -squad.AnchorFacing;
                squad.OrderedPosition = centre + dir * _settings.SkirmishPreferredRange;
            }
            else if (distance > _settings.SkirmishPreferredRange)
            {
                Vector3 toward = (target - centre).normalized;
                squad.OrderedPosition = target - toward * _settings.SkirmishPreferredRange;
            }
            else
            {
                squad.OrderedPosition = centre;
            }
        }

        /// <summary>Once the lines meet, stand and fight rather than shuffling into the enemy.</summary>
        private static bool HoldWhenEngaged(BattleSquad squad)
        {
            if (squad.EngagedCount <= 0) return false;
            squad.Order = SquadOrder.HoldPosition;
            squad.OrderedPosition = squad.AnchorPosition;
            return true;
        }

        private Vector3 FindNearestPlayerPosition(Vector3 from, out float distance)
        {
            Vector3 best = from;
            distance = float.MaxValue;

            List<BattleSquad> squads = _state.PlayerSquads;
            for (int i = 0; i < squads.Count; i++)
            {
                // Squads waiting off the field cannot be seen, let alone marched on.
                if (!squads[i].IsEffective || squads[i].IsOffField) continue;

                Vector3 centre = squads[i].CentreOfMass();
                float d = Vector3.Distance(from, centre);
                if (d >= distance) continue;

                best = centre;
                distance = d;
            }

            // The Centurion alone is a target if nothing else is standing.
            BattleCombatant player = _state.PlayerCharacter;
            if (player != null && player.IsAlive)
            {
                float d = Vector3.Distance(from, player.WorldPosition);
                if (d < distance)
                {
                    best = player.WorldPosition;
                    distance = d;
                }
            }

            return best;
        }
    }
}
