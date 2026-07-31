using System.Collections.Generic;
using Century.Battle.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Decides when a battle is over and who won.
    /// </summary>
    /// <remarks>
    /// A side is beaten when it is dead <em>or</em> broken, not merely dead — routed men have left
    /// the field as surely as fallen ones. The condition must hold for a couple of seconds before
    /// the battle concludes, which stops a momentary rout from ending a fight the player could still
    /// have rallied.
    /// </remarks>
    public sealed class BattleOutcomeEvaluator
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        private readonly int _playerStartingStrength;
        private readonly int _enemyStartingStrength;

        private BattleOutcome _candidate = BattleOutcome.Aborted;
        private float _candidateHeldFor;

        /// <summary>Why the concluded outcome happened, in words the banner can show. A battle that
        /// ends without saying why reads as a bug even when it is working exactly as designed.</summary>
        public string Reason { get; private set; } = string.Empty;

        public BattleOutcomeEvaluator(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;

            _playerStartingStrength = Mathf.Max(1, CountMembers(state.PlayerSquads));
            _enemyStartingStrength = Mathf.Max(1, CountMembers(state.EnemySquads));
        }

        private static int CountMembers(List<BattleSquad> squads)
        {
            int count = 0;
            for (int i = 0; i < squads.Count; i++) count += squads[i].Members.Count;
            return count;
        }

        /// <summary>Effective strength: men alive and still willing to fight.</summary>
        private static int CountEffective(List<BattleSquad> squads)
        {
            int count = 0;
            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i].IsRouted || squads[i].IsWithdrawn) continue;
                count += squads[i].AliveCount;
            }

            return count;
        }

        public bool TryEvaluate(float deltaSeconds, out BattleOutcome outcome)
        {
            outcome = BattleOutcome.Aborted;

            int playerEffective = CountEffective(_state.PlayerSquads);
            int enemyEffective = CountEffective(_state.EnemySquads);

            float playerLost = 1f - playerEffective / (float)_playerStartingStrength;
            float enemyLost = 1f - enemyEffective / (float)_enemyStartingStrength;

            bool commanderDown = _state.PlayerCharacter == null || !_state.PlayerCharacter.IsAlive;

            // Centurio in Waiting: with a living Optio on the field, the commander's fall opens a
            // succession window instead of ending the battle. Defeat only if there is no successor,
            // the successor also falls, or the window has closed (which withdraws, not defeats:
            // the Optio's whole office is bringing the century home).
            bool optioHolds = _state.CommandDevolved && _state.SuccessionSecondsLeft > 0f
                              && AnyLivingOptio();
            if (commanderDown && _state.CommandDevolved && _state.SuccessionSecondsLeft <= 0f
                && AnyLivingOptio())
            {
                Reason = "The Optio pulls the century out";
                outcome = BattleOutcome.Withdrawal;
                return true;
            }

            BattleOutcome candidate;

            if (commanderDown && !optioHolds) candidate = BattleOutcome.Defeat;
            else if (playerLost >= _settings.PlayerBrokenFraction) candidate = BattleOutcome.Defeat;
            else if (enemyLost >= _settings.EnemyBrokenFraction) candidate = BattleOutcome.Victory;
            else candidate = BattleOutcome.Aborted;

            if (candidate == BattleOutcome.Aborted)
            {
                _candidate = BattleOutcome.Aborted;
                _candidateHeldFor = 0f;
                return false;
            }

            if (candidate != _candidate)
            {
                _candidate = candidate;
                _candidateHeldFor = 0f;
            }

            _candidateHeldFor += deltaSeconds;

            // A commander's death with no successor ends things immediately.
            float required = commanderDown && !optioHolds ? 0f : _settings.OutcomeConfirmSeconds;
            if (_candidateHeldFor < required) return false;

            Reason = commanderDown && !optioHolds
                ? (_state.CommandDevolved ? "Command died with the Optio" : "The Centurion has fallen")
                : candidate == BattleOutcome.Defeat ? "The century is broken"
                : "The enemy is broken";

            outcome = candidate;
            return true;
        }

        private bool AnyLivingOptio()
        {
            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                var members = _state.PlayerSquads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].IsAlive && members[m].Role == OfficerRole.Optio) return true;
            }
            return false;
        }
    }
}
