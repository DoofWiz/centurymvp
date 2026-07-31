using System.Collections.Generic;
using Century.Battle.Model;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Decides, at the moment a man goes down, whether he is dead or merely WOUNDED — out of this
    /// battle exactly like a dead man, but found alive when the field is cleared and carried with
    /// the column until he heals. This is the single knob every "keep my men alive" investment
    /// hangs off: the base chance, the medicus, commander doctrine, and (in time) camp medicine.
    /// </summary>
    /// <remarks>
    /// Player side only. Enemy dead are dead: their wounded have no column to carry them, and the
    /// campaign accounts for broken warbands in prisoners and scattered survivors instead.
    /// </remarks>
    public static class CasualtyFate
    {
        /// <summary>True if the fallen man is wounded rather than killed. Call exactly once, at the
        /// moment Health01 reaches zero.</summary>
        public static bool RollWoundedOut(
            BattleState state, BattleSettings settings, BattleCombatant victim, System.Random random)
        {
            // The Centurion's fate is the campaign's fate, not a dice roll here.
            if (!victim.IsPlayerSide || victim.IsPlayerControlled) return false;

            float chance = settings.WoundedOutChance;

            if (AnyLivingMedicus(state)) chance += settings.MedicusWoundedOutBonus;

            // The medicus's OFFICE: invested nodes raise the chance; a vacant office lowers it.
            chance += state.Effects.WoundedOutBonus;

            // "He Lives": the medicus refuses a death outright, a limited number of times.
            if (state.DeathsNegated < state.Effects.DeathNegations)
            {
                state.DeathsNegated++;
                victim.Health01 = 0.12f;   // back from the brink, barely standing
                return false;
            }

            // "One More Day": men of a survivor's column refuse to die on this particular field.
            if (state.HasSkill("one_more_day")) chance += 0.10f;

            return random.NextDouble() < chance;
        }

        private static bool AnyLivingMedicus(BattleState state)
        {
            List<BattleSquad> squads = state.PlayerSquads;
            for (int s = 0; s < squads.Count; s++)
            {
                if (squads[s].IsOffField) continue;
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].IsAlive && members[m].Role == OfficerRole.Medicus) return true;
            }

            return false;
        }
    }
}
