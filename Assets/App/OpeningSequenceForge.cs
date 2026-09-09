using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Contracts;

namespace Century.App
{
    /// <summary>
    /// Builds the battle request for the OPENING SEQUENCE: the wounded Centurion alone, a passing
    /// warband of four and a single looter, on the ground where the survivors wake. The battle scene
    /// reads the <see cref="BattleRequest.OpeningSequence"/> flag and runs its director instead of
    /// a fight.
    /// </summary>
    public static class OpeningSequenceForge
    {
        public const string EnemyPartyId = "opening_barbarians";

        public static BattleRequest Build(CampaignState state)
        {
            PartyState player = state.PlayerParty;
            SoldierRecord centurion = player?.Roster.FindByRank("centurion") ?? player?.Roster.Soldiers[0];

            var request = new BattleRequest
            {
                PlayerPartyId = player != null ? player.Id : "opening_player",
                EnemyPartyId = EnemyPartyId,
                EnemyDisplayName = "Barbarians",
                TimeOfDay = CampaignTime.FromHours(7.3d),   // grey first light, in the rain
                WorldX = CampaignSeeder.AftermathStart.x,
                WorldZ = CampaignSeeder.AftermathStart.z,
                EnemyBehaviour = CommanderBehaviour.Fanatic,   // whoever wakes charges
                RandomSeed = state.RandomSeed ^ 0x0BE1,
                TerrainId = "forest",
                OpeningSequence = true,
                ShowIntro = false
            };

            if (centurion != null)
            {
                request.PlayerCombatants.Add(new CombatantSpec
                {
                    SoldierId = centurion.Id,
                    DisplayName = centurion.DisplayName,
                    RankId = "centurion",
                    ArchetypeId = centurion.ArchetypeId,
                    Health01 = 0.55f,   // wounded: he wakes at half his blood
                    Stamina01 = 1f,
                    Morale01 = 0.7f,
                    IsPlayerControlled = true,
                    DamageMultiplier = 1.6f,   // a veteran's blade: the first fight must be won
                    VeterancyLabel = VeterancyLadder.Word(centurion.Tier)
                });
            }

            // Group 0: the warband that passes (three men, worn from the day's slaughter, so a
            // player who is spotted after all has a fight and not an execution). Group 1: the looter.
            for (int i = 0; i < 3; i++)
                request.EnemyCombatants.Add(Warrior($"opening_warrior_{i}", "Cherusci warrior", 0, 0.7f));
            CombatantSpec looter = Warrior("opening_looter", "Pilfering barbarian", 1, 0.55f);
            looter.Loadout = "longsword";     // a blade and nothing else: the first fight is a duel
            looter.DamageMultiplier = 0.6f;   // he hurts, and he loses
            request.EnemyCombatants.Add(looter);

            return request;
        }

        private static CombatantSpec Warrior(string id, string name, int group, float health)
        {
            return new CombatantSpec
            {
                SoldierId = id,
                DisplayName = name,
                RankId = "warrior",
                ArchetypeId = "cherusci_warrior",
                Health01 = health,
                Stamina01 = 0.9f,
                Morale01 = 0.85f,
                GroupIndex = group,
                Missile = "none"   // the opening is taught blade to blade; nobody javelins the lesson
            };
        }
    }
}
