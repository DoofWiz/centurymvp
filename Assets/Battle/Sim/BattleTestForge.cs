using System.Collections.Generic;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// The knobs of the battle test environment, edited on BattleBootstrap in the Inspector of the
    /// Battle scene. Press Play with the scene open directly (no campaign running) and a battle is
    /// forged from these instead of aborting; F5 restarts it instantly with the same settings.
    /// </summary>
    [System.Serializable]
    public sealed class BattleTestConfig
    {
        [Header("Forces")]
        [Range(9, 96)] public int RomanCount = 40;
        [Range(8, 120)] public int GermanCount = 48;

        [Header("Enemy commander")]
        public CommanderBehaviour EnemyBehaviour = CommanderBehaviour.Disciplined;

        [Header("Condition on arrival")]
        [Range(0.1f, 1f)] public float RomanStamina01 = 0.9f;
        [Range(0.1f, 1f)] public float GermanStamina01 = 0.9f;
        [Range(0.1f, 1f)] public float RomanMorale01 = 0.7f;
        [Range(0.1f, 1f)] public float GermanMorale01 = 0.65f;

        [Header("Scenario")]
        public bool PlayerAmbushed;
        public bool EnemyAmbushed;

        [Tooltip("Skip the deployment screen and go straight to the field.")]
        public bool SkipDeployment = true;

        [Range(0f, 24f)] public float HourOfDay = 10f;

        [Tooltip("0 = a fresh random battle every restart; any other value replays the same fight.")]
        public int Seed;

        [Tooltip("Commander doctrine ids to test (e.g. war_cry, roman_order, no_shield_but_courage).")]
        public List<string> CommanderSkills = new List<string>();
    }

    /// <summary>
    /// Forges a synthetic <see cref="BattleRequest"/> for the test environment: a full century in
    /// its contubernia of eight with a working officer corps, against a warband of the chosen
    /// doctrine — everything the real campaign would send, without the campaign.
    /// </summary>
    public static class BattleTestForge
    {
        private static readonly string[] RomanNames =
        {
            "Varro", "Celsus", "Niger", "Priscus", "Rufus", "Macro", "Bassus", "Crispus",
            "Longus", "Paulus", "Severus", "Albinus", "Fronto", "Gallus", "Lucanus", "Montanus"
        };

        private static readonly string[] GermanNames =
        {
            "Adalhard", "Baldric", "Chlodo", "Diethelm", "Erminaz", "Frotho", "Gervas", "Hariman",
            "Ingomar", "Leudast", "Marbod", "Odo", "Radulf", "Sigimund", "Theudebald", "Wulfila"
        };

        public static BattleRequest Build(BattleTestConfig config)
        {
            int seed = config.Seed != 0 ? config.Seed : System.Environment.TickCount;
            var random = new System.Random(seed);

            var request = new BattleRequest
            {
                PlayerPartyId = "test_player",
                EnemyPartyId = "test_enemy",
                EnemyDisplayName = "Test Warband",
                EnemyBehaviour = config.EnemyBehaviour,
                PlayerAmbushed = config.PlayerAmbushed,
                EnemyAmbushed = config.EnemyAmbushed && !config.PlayerAmbushed,
                TimeOfDay = CampaignTime.FromHours(config.HourOfDay),
                RandomSeed = seed
            };

            request.CommanderSkills.AddRange(config.CommanderSkills);

            BuildRomans(request.PlayerCombatants, config, random);
            BuildGermans(request.EnemyCombatants, config, random);

            return request;
        }

        /// <summary>The century: the Centurion himself, an officer corps that makes every battle
        /// system live (optio, tesserarius, signifer, medicus), and legionaries in tent groups of
        /// eight with a Decanus each — the same shape the campaign sends.</summary>
        private static void BuildRomans(List<CombatantSpec> specs, BattleTestConfig config, System.Random random)
        {
            specs.Add(new CombatantSpec
            {
                SoldierId = "test_centurion",
                DisplayName = "T. Testus",
                RankId = "centurion",
                ArchetypeId = "legionary_heavy",
                Stamina01 = 1f,
                Morale01 = 0.8f,
                IsPlayerControlled = true
            });

            // One of each supporting officer, spread over the first groups.
            string[] officerRanks = { "optio", "tesserarius", "signifer", "medicus" };

            int rankers = Mathf.Max(8, config.RomanCount - 1);
            for (int i = 0; i < rankers; i++)
            {
                int group = i / 8;

                specs.Add(new CombatantSpec
                {
                    SoldierId = $"test_roman_{i}",
                    DisplayName = $"{(char)('A' + group % 26)}. {Pick(RomanNames, random)}",
                    RankId = i < officerRanks.Length ? officerRanks[i] : "legionary",
                    ArchetypeId = "legionary_heavy",
                    Stamina01 = Jitter(config.RomanStamina01, random),
                    Morale01 = Jitter(config.RomanMorale01, random),
                    GroupIndex = group,
                    IsGroupLeader = i % 8 == 0
                });
            }
        }

        /// <summary>The warband: dealt into squads by the factory, no persistent groups.</summary>
        private static void BuildGermans(List<CombatantSpec> specs, BattleTestConfig config, System.Random random)
        {
            for (int i = 0; i < config.GermanCount; i++)
            {
                specs.Add(new CombatantSpec
                {
                    SoldierId = $"test_german_{i}",
                    DisplayName = Pick(GermanNames, random),
                    RankId = "warrior",
                    ArchetypeId = "cherusci_warrior",
                    Stamina01 = Jitter(config.GermanStamina01, random),
                    Morale01 = Jitter(config.GermanMorale01, random)
                });
            }
        }

        private static float Jitter(float value, System.Random random) =>
            Mathf.Clamp(value + ((float)random.NextDouble() - 0.5f) * 0.1f, 0.1f, 1f);

        private static string Pick(string[] pool, System.Random random) =>
            pool[random.Next(pool.Length)];
    }
}
