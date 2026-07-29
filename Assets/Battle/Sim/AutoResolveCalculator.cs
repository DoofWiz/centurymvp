using System;
using System.Collections.Generic;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle
{
    /// <summary>
    /// Resolves a battle arithmetically, without a battle scene.
    /// </summary>
    /// <remarks>
    /// PLACEHOLDER. This exists to close the campaign loop so that scene transitions, casualty
    /// application and loot can be verified before any real-time combat exists. It will be replaced
    /// wholesale by the playable battle in step 3 — but it should survive as the resolver for
    /// AI-versus-AI fights the player never sees, so it is written to be kept rather than deleted.
    /// </remarks>
    public static class AutoResolveCalculator
    {
        private const int MaxRounds = 24;

        public static BattleResult Resolve(BattleRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var random = new System.Random(request.RandomSeed);

            List<Fighter> playerSide = BuildSide(request.PlayerCombatants);
            List<Fighter> enemySide = BuildSide(request.EnemyCombatants);

            // An ambush costs the defender a free exchange before the line forms.
            if (request.PlayerAmbushed) ApplyCasualties(playerSide, SideStrength(enemySide) * 0.25f, random);

            int rounds = 0;
            while (rounds++ < MaxRounds && AnyStanding(playerSide) && AnyStanding(enemySide))
            {
                float playerStrength = SideStrength(playerSide);
                float enemyStrength = SideStrength(enemySide);

                ApplyCasualties(playerSide, enemyStrength * RoundIntensity(random), random);
                ApplyCasualties(enemySide, playerStrength * RoundIntensity(random), random);

                if (SideStrength(enemySide) < enemyStrength * 0.35f) break;   // enemy breaks
                if (SideStrength(playerSide) < playerStrength * 0.30f) break; // line collapses
            }

            return BuildResult(request, playerSide, enemySide, rounds);
        }

        private static float RoundIntensity(System.Random random) => 0.05f + (float)random.NextDouble() * 0.06f;

        private static List<Fighter> BuildSide(List<CombatantSpec> specs)
        {
            var fighters = new List<Fighter>(specs.Count);
            for (int i = 0; i < specs.Count; i++) fighters.Add(new Fighter(specs[i]));
            return fighters;
        }

        private static float SideStrength(List<Fighter> side)
        {
            float total = 0f;
            for (int i = 0; i < side.Count; i++) total += side[i].Strength;
            return total;
        }

        private static bool AnyStanding(List<Fighter> side)
        {
            for (int i = 0; i < side.Count; i++)
                if (side[i].Health01 > 0f) return true;
            return false;
        }

        /// <summary>Spreads damage across the side, weighted so the fit take more of it than the hurt.</summary>
        private static void ApplyCasualties(List<Fighter> side, float damage, System.Random random)
        {
            if (damage <= 0f) return;

            float capacity = 0f;
            for (int i = 0; i < side.Count; i++) capacity += side[i].Health01;
            if (capacity <= 0f) return;

            for (int i = 0; i < side.Count; i++)
            {
                Fighter fighter = side[i];
                if (fighter.Health01 <= 0f) continue;

                float share = fighter.Health01 / capacity;
                float jitter = 0.6f + (float)random.NextDouble() * 0.8f;
                float taken = damage * share * jitter * 0.02f;

                fighter.Health01 = Mathf.Max(0f, fighter.Health01 - taken);
                fighter.Stamina01 = Mathf.Max(0f, fighter.Stamina01 - taken * 1.5f);
                if (fighter.Health01 <= 0f) fighter.Morale01 = 0f;
                else fighter.Morale01 = Mathf.Clamp01(fighter.Morale01 - taken * 0.5f);

                side[i] = fighter;
            }
        }

        private static BattleResult BuildResult(
            BattleRequest request, List<Fighter> playerSide, List<Fighter> enemySide, int rounds)
        {
            int enemiesKilled = 0;
            for (int i = 0; i < enemySide.Count; i++)
                if (enemySide[i].Health01 <= 0f) enemiesKilled++;

            bool playerStanding = AnyStanding(playerSide);
            bool enemyStanding = AnyStanding(enemySide);

            var result = new BattleResult
            {
                PlayerPartyId = request.PlayerPartyId,
                EnemyPartyId = request.EnemyPartyId,
                EnemiesKilled = enemiesKilled,
                MinutesElapsed = 25d + rounds * 4d,
                Outcome = !playerStanding ? BattleOutcome.Defeat
                    : !enemyStanding ? BattleOutcome.Victory
                    : SideStrength(playerSide) >= SideStrength(enemySide)
                        ? BattleOutcome.Victory
                        : BattleOutcome.Withdrawal
            };

            for (int i = 0; i < playerSide.Count; i++)
            {
                Fighter fighter = playerSide[i];
                result.Combatants.Add(new CombatantOutcome
                {
                    SoldierId = fighter.SoldierId,
                    Survived = fighter.Health01 > 0f,
                    Health01 = fighter.Health01,
                    Stamina01 = fighter.Stamina01,
                    Morale01 = fighter.Morale01
                });
            }

            if (result.Outcome == BattleOutcome.Victory)
            {
                result.Loot.Food = enemiesKilled * 1.5f;
                result.Loot.Coin = enemiesKilled * 3;
                result.Loot.EquipmentConditionDelta = -0.02f;
                if (enemiesKilled >= 3)
                    result.Loot.Items.Add(new LootItem("framea_captured", enemiesKilled / 3));
            }
            else
            {
                result.Loot.EquipmentConditionDelta = -0.05f;
            }

            return result;
        }

        private struct Fighter
        {
            public readonly string SoldierId;
            public float Health01;
            public float Stamina01;
            public float Morale01;
            private readonly float _archetypeWeight;

            public Fighter(CombatantSpec spec)
            {
                SoldierId = spec.SoldierId;
                Health01 = spec.Health01;
                Stamina01 = spec.Stamina01;
                Morale01 = spec.Morale01;
                _archetypeWeight = WeightFor(spec.ArchetypeId);
            }

            /// <summary>Contribution to the side's fighting power this round.</summary>
            public float Strength => Health01 <= 0f
                ? 0f
                : _archetypeWeight * (0.5f + Health01 * 0.5f) * (0.6f + Morale01 * 0.4f) * (0.7f + Stamina01 * 0.3f);

            private static float WeightFor(string archetypeId)
            {
                switch (archetypeId)
                {
                    case "legionary_officer": return 1.6f;
                    case "legionary_heavy": return 1.3f;   // kit and drill, not heroics
                    case "legionary_support": return 0.7f;
                    case "cherusci_warrior": return 1.0f;
                    default: return 1.0f;
                }
            }
        }
    }
}
