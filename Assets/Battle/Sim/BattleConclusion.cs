using Century.Battle.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Flattens a finished <see cref="BattleState"/> back into the campaign contract.
    /// </summary>
    /// <remarks>
    /// Reserve men are reported too, unchanged. Leaving them out would silently delete anyone who
    /// did not deploy — the kind of bug that only surfaces three battles later when the roster is
    /// mysteriously short.
    /// </remarks>
    public static class BattleConclusion
    {
        /// <summary>Experience for a kill.</summary>
        private const int ExperiencePerKill = 15;

        /// <summary>Experience for coming through the fight at all. Attendance counts.</summary>
        private const int ExperienceForSurviving = 22;

        /// <summary>Extra experience for being wounded and living. Hard lessons teach most.</summary>
        private const int ExperienceForWound = 14;

        /// <summary>Deducted for breaking. Not punitive, just no credit for the part spent running.</summary>
        private const int ExperiencePenaltyForRout = 10;

        private const int CommanderExperienceBase = 40;
        private const int CommanderExperiencePerKill = 12;

        /// <summary>Fraction of broken-but-living enemy who are run down and taken.</summary>
        private const float PrisonerCaptureRate = 0.45f;

        public static BattleResult Build(BattleState state, BattleOutcome outcome, double minutesElapsed)
        {
            var result = new BattleResult
            {
                Outcome = outcome,
                PlayerPartyId = state.PlayerPartyId,
                EnemyPartyId = state.EnemyPartyId,
                MinutesElapsed = minutesElapsed
            };

            foreach (BattleCombatant man in state.AllPlayerSideCombatants())
            {
                // Men who broke and ran are alive but shaken. Carrying that back to the campaign is
                // what makes a costly victory feel different from a clean one on the next march.
                bool routed = WasInRoutedSquad(state, man);

                result.Combatants.Add(new CombatantOutcome
                {
                    SoldierId = man.SoldierId,
                    Survived = man.IsAlive,
                    Health01 = man.Health01,
                    Stamina01 = man.Stamina01,
                    Morale01 = man.Morale01,
                    Kills = man.Kills,
                    Routed = routed,
                    ExperienceGained = ExperienceFor(man, routed, outcome)
                });
            }

            int enemyStart = 0;
            int enemyRoutedAlive = 0;

            for (int i = 0; i < state.EnemySquads.Count; i++)
            {
                BattleSquad squad = state.EnemySquads[i];
                enemyStart += squad.Members.Count;
                if (squad.IsRouted) enemyRoutedAlive += squad.AliveCount;
            }

            result.EnemiesKilled = enemyStart - state.EnemyAliveCount;
            result.EnemiesRouted = enemyRoutedAlive;
            result.EnemyAnnihilated = state.EnemyAliveCount <= 0;

            result.CommanderExperience = state.PlayerCharacter != null && state.PlayerCharacter.IsAlive
                ? CommanderExperienceBase + state.PlayerCharacter.Kills * CommanderExperiencePerKill
                : 0;

            ApplyLoot(result, outcome, enemyRoutedAlive);
            return result;
        }

        /// <summary>
        /// Spoils. Only a victory lets you strip the field: withdrawing means leaving the dead where
        /// they lie, which is most of why withdrawal is expensive even when it saves the century.
        /// </summary>
        private static void ApplyLoot(BattleResult result, BattleOutcome outcome, int enemyRoutedAlive)
        {
            if (outcome != BattleOutcome.Victory)
            {
                result.Loot.EquipmentConditionDelta = -0.05f;
                return;
            }

            result.Loot.Food = result.EnemiesKilled * 1.5f;
            result.Loot.Coin = result.EnemiesKilled * 3;
            result.Loot.Prisoners = Mathf.RoundToInt(enemyRoutedAlive * PrisonerCaptureRate);

            // Stripped kit offsets some of the wear the fight caused.
            result.Loot.EquipmentConditionDelta = -0.02f + result.EnemiesKilled * 0.0015f;

            // Itemised spoils: dead men's arms, their stores, and their pride. Decided here so the
            // summary can show them; the campaign layer moves them into the inventory afterwards.
            int dead = result.EnemiesKilled;
            if (dead > 0)
            {
                result.Loot.Items.Add(new LootItem("framea_captured", Mathf.Max(1, dead / 3)));
                result.Loot.Items.Add(new LootItem("round_shield", Mathf.Max(1, dead / 4)));
                if (dead >= 4) result.Loot.Items.Add(new LootItem("grain_sack", dead / 4));
                if (dead >= 6) result.Loot.Items.Add(new LootItem("raw_game", dead / 6));
                if (dead >= 8) result.Loot.Items.Add(new LootItem("fur_pelts", dead / 8));
                if (dead >= 10) result.Loot.Items.Add(new LootItem("linen_bandages", dead / 10));
            }

            // A broken warband abandons its banner; an annihilated one has no say.
            if (result.EnemyAnnihilated || result.EnemiesRouted >= 10)
                result.Loot.Items.Add(new LootItem("banner_cherusci", 1));
        }

        private static int ExperienceFor(BattleCombatant man, bool routed, BattleOutcome outcome)
        {
            if (!man.IsAlive) return 0;

            int experience = man.Kills * ExperiencePerKill;

            // Reserve men were never in it, so they get nothing but the day's marching.
            bool wasEngaged = man.SquadIndex >= 0;
            if (wasEngaged) experience += ExperienceForSurviving;

            if (wasEngaged && man.Health01 < 0.6f) experience += ExperienceForWound;
            if (routed) experience = Mathf.Max(0, experience - ExperiencePenaltyForRout);
            if (outcome == BattleOutcome.Victory && wasEngaged) experience += 8;

            return experience;
        }

        private static bool WasInRoutedSquad(BattleState state, BattleCombatant man)
        {
            if (man.SquadIndex < 0 || man.SquadIndex >= state.PlayerSquads.Count) return false;
            return state.PlayerSquads[man.SquadIndex].IsRouted;
        }
    }
}
