using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// The medicus treats wounded men beside him, but only while his squad is out of contact.
    /// </summary>
    /// <remarks>
    /// Small system, real consequence: it means pulling a mauled squad out of the line to recover is
    /// a viable decision rather than a waste of a turn, and it gives the medicus a reason to exist in
    /// battle rather than only between them.
    /// </remarks>
    public sealed class MedicusSystem
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        public MedicusSystem(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;
        }

        public void Tick(float deltaSeconds)
        {
            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                BattleSquad squad = _state.PlayerSquads[s];
                if (squad.IsRouted || squad.EngagedCount > 0) continue;

                for (int m = 0; m < squad.Members.Count; m++)
                {
                    BattleCombatant medicus = squad.Members[m];
                    if (!medicus.IsAlive || medicus.Role != OfficerRole.Medicus) continue;
                    if (medicus.IsInCombat) continue;

                    Treat(medicus, deltaSeconds);
                }
            }
        }

        private void Treat(BattleCombatant medicus, float deltaSeconds)
        {
            float radiusSqr = _settings.MedicusRadius * _settings.MedicusRadius;
            float heal = _settings.MedicusHealPerSecond * deltaSeconds * _state.Effects.MedicusSpeedMultiplier;

            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                List<BattleCombatant> members = _state.PlayerSquads[s].Members;

                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant patient = members[m];
                    if (!patient.IsAlive || patient.Health01 >= 0.999f) continue;
                    if ((patient.WorldPosition - medicus.WorldPosition).sqrMagnitude > radiusSqr) continue;

                    patient.Health01 = Mathf.Min(1f, patient.Health01 + heal);
                }
            }
        }
    }
}
