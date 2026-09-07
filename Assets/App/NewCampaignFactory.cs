using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Items;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Builds a starting campaign: the guided start (a handful of survivors, the Aftermath laid out
    /// around them) or the sandbox (the full century, the open map). Warbands and places are the
    /// seeder's business (<see cref="CampaignSeeder"/>), so the guided start can seed the open
    /// world later, the moment the passage is taken.
    /// </summary>
    public static class NewCampaignFactory
    {
        private static readonly string[] Praenomina =
            { "Gaius", "Lucius", "Marcus", "Publius", "Quintus", "Titus", "Aulus", "Decimus", "Servius", "Spurius" };

        private static readonly string[] Nomina =
            { "Valerius", "Aquilius", "Vorenus", "Secundus", "Felix", "Cornelius", "Antonius", "Fabius", "Iulius", "Sergius" };

        // --- The guided start -----------------------------------------------------------------

        /// <summary>
        /// The morning after Teutoburg: a wounded Centurion, a decanus and four men, at the edge of
        /// the killing ground. The chapter opens on the opening sequence; the App layer moves it to
        /// the Aftermath when that is done.
        /// </summary>
        public static CampaignState CreateOnboarding(int seed, CampaignSettings settings)
        {
            // Day one, first light. Everything is counted from the massacre.
            var clock = new CampaignClock(CampaignTime.FromHours(6.5d));
            var state = new CampaignState(clock) { RandomSeed = seed };

            Random.State previousRandomState = Random.state;
            Random.InitState(seed);

            state.AddParty(CreateSurvivors(state, settings));
            CampaignSeeder.SeedAftermath(state, settings);
            state.Onboarding.Chapter = OnboardingChapter.Opening;

            Random.state = previousRandomState;
            return state;
        }

        private static PartyState CreateSurvivors(CampaignState state, CampaignSettings settings)
        {
            var party = new PartyState
            {
                Id = state.MintId("party"),
                DisplayName = "Centuria Cornelia",
                Faction = PartyFaction.Roman,
                IsPlayer = true,
                WorldPosition = CampaignSeeder.AftermathStart,
                DetectionRadius = settings.PlayerDetectionRadius,
                SpeedModifier = 1f,
                Stores =
                {
                    Food = 9f,                   // a day and a half, scraped from the packs of the dead
                    Coin = 14,
                    Denarii = 36,
                    EquipmentCondition01 = 0.52f
                }
            };

            party.Roster.Capacity = 120;

            // The Centurion, wounded but on his feet; the decanus who kept four men alive; the four.
            // Experience is spread so two of them can hold the Speculator's office when asked.
            SoldierRecord centurion = MakeSoldier("M. Cornelius", "centurion", "legionary_officer", 0.72f, 910, 0.55f);
            SoldierRecord decanus = MakeSoldier(OnboardingDirector.DecanusName, "legionary", "legionary_heavy", 0.66f, 430, 0.7f);
            party.Roster.Add(centurion);
            party.Roster.Add(decanus);
            party.Roster.Add(MakeSoldier("Q. Fabius", "legionary", "legionary_heavy", 0.52f, 160, 0.62f));
            party.Roster.Add(MakeSoldier("S. Antonius", "legionary", "legionary_heavy", 0.48f, 135, 0.45f));
            party.Roster.Add(MakeSoldier("L. Sergius", "legionary", "legionary_heavy", 0.44f, 60, 0.8f));
            party.Roster.Add(MakeSoldier("A. Iulius", "legionary", "legionary_heavy", 0.40f, 20, 0.58f));

            ContuberniumLedger.EnsureAssigned(party.Roster);
            ContuberniumLedger.SetDecanus(party.Roster, decanus.Id);

            // What six men carried out of the forest.
            PartyInventory inv = party.Inventory;
            inv.Add("hardtack", 3);
            inv.Add("salt_pork", 1);
            inv.Add("firewood_bundle", 6);
            inv.Add("linen_bandages", 2);
            inv.Add("healing_herbs", 1);
            inv.Add("gladius_spare", 1);
            inv.Add("pila_bundle", 1);
            inv.Add("dolabra", 2);
            inv.Add("whetstone", 1);
            inv.Add("rope_coils", 1);
            inv.Add("timber", 2);

            // A fire, and nothing else standing. Every other station is theirs to raise.
            party.Facilities.SetLevel(CampStationId.CookingFire, 1);

            party.Morale.Value01 = party.Roster.AverageMorale01;
            return party;
        }

        // --- The sandbox ----------------------------------------------------------------------

        /// <summary>The free start: the full century on the open map, warbands and places seeded.</summary>
        public static CampaignState CreateSandbox(int seed, CampaignSettings settings)
        {
            var clock = new CampaignClock(CampaignTime.FromDays(22).Plus(12 * CampaignTime.MinutesPerHour));
            var state = new CampaignState(clock) { RandomSeed = seed };

            Random.State previousRandomState = Random.state;
            Random.InitState(seed);

            state.AddParty(CreatePlayerParty(state, settings));
            CampaignSeeder.SeedOpenWorld(state, settings);
            state.Onboarding.Chapter = OnboardingChapter.None;

            Random.state = previousRandomState;
            return state;
        }

        private static PartyState CreatePlayerParty(CampaignState state, CampaignSettings settings)
        {
            var party = new PartyState
            {
                Id = state.MintId("party"),
                DisplayName = "Centuria Valeria",
                Faction = PartyFaction.Roman,
                IsPlayer = true,
                WorldPosition = Vector3.zero,
                DetectionRadius = settings.PlayerDetectionRadius,
                SpeedModifier = 1f,
                Stores =
                {
                    Food = 220f,                 // three days cooked and ready; the rest rides raw in the packs
                    Coin = 342,
                    Denarii = 1245,
                    EquipmentCondition01 = 0.76f
                }
            };

            party.Roster.Capacity = 120;

            // Named officers carry real experience so their veterancy tiers read correctly and they
            // are legitimately eligible for the senior posts the camp screen lets the player assign.
            SoldierRecord centurion = MakeSoldier("G. Valerius", "centurion", "legionary_officer", 0.86f, 910);
            SoldierRecord optio = MakeSoldier("M. Aquilius", "optio", "legionary_officer", 0.74f, 520);
            SoldierRecord tesserarius = MakeSoldier("L. Vorenus", "tesserarius", "legionary_heavy", 0.68f, 240);
            party.Roster.Add(centurion);
            party.Roster.Add(optio);
            party.Roster.Add(tesserarius);
            party.Roster.Add(MakeSoldier("D. Secundus", "medicus", "legionary_support", 0.62f, 150));
            party.Roster.Add(MakeSoldier("R. Felix", "immunis", "legionary_heavy", 0.71f, 180));

            // A spread of experience so the century shows recruits, soldiers and a few veterans.
            while (party.Roster.Soldiers.Count < 73)
                party.Roster.Add(MakeSoldier(
                    RandomRomanName(), "legionary", "legionary_heavy", Random.Range(0.45f, 1f), Random.Range(0, 450)));

            // Sort every man into his tent group. The same structure serves the camp chart and the
            // battle line, so it is fixed here at muster and only patched as men are lost or gained.
            ContuberniumLedger.EnsureAssigned(party.Roster);

            SeedInventory(party);

            // Two posts already filled, two left open so the camp screen shows both states; a modest
            // camp already standing, with room to build.
            party.Appointments.Assign(CampRole.Optio, optio.Id);
            party.Appointments.Assign(CampRole.Tesserarius, tesserarius.Id);
            party.Facilities.SetLevel(CampStationId.CookingFire, 1);
            party.Facilities.SetLevel(CampStationId.MedicalTent, 1);
            party.Facilities.SetLevel(CampStationId.WatchPost, 1);

            party.Morale.Value01 = party.Roster.AverageMorale01;
            return party;
        }

        /// <summary>
        /// What a century that fought its way out of the forest still carries: working spares and
        /// tools, a modest baggage train, a few things worth trading, and its own standard.
        /// </summary>
        private static void SeedInventory(PartyState party)
        {
            PartyInventory inv = party.Inventory;

            // Foodstuffs: some ready for the road, more raw for the cooking fire.
            inv.Add("grain_sack", 6);
            inv.Add("flour_sack", 2);
            inv.Add("hardtack", 4);
            inv.Add("salt_pork", 3);
            inv.Add("trail_mix", 2);

            // Fuel for the night fires.
            inv.Add("firewood_bundle", 40);
            inv.Add("charcoal_sack", 3);

            // The medicus' chest: some usable, some waiting on the tent.
            inv.Add("linen_bandages", 6);
            inv.Add("healing_herbs", 4);
            inv.Add("vinegar_jar", 2);
            inv.Add("honey_pot", 2);

            // Materials and repairs.
            inv.Add("leather_hides", 6);
            inv.Add("timber", 8);
            inv.Add("iron_ingots", 3);
            inv.Add("rope_coils", 4);
            inv.Add("pitch_pot", 2);
            inv.Add("wool_cloth", 5);

            // A little comfort, tightly rationed.
            inv.Add("wine_amphora", 2);
            inv.Add("olive_oil", 1);
            inv.Add("dried_figs", 3);

            // Something to trade when coin won't do.
            inv.Add("fur_pelts", 5);
            inv.Add("salt_blocks", 4);
            inv.Add("bronze_fibulae", 3);
            inv.Add("silver_torc", 1);

            // Spare kit: the difference between a loss and a bad day.
            inv.Add("gladius_spare", 5);
            inv.Add("pugio_spare", 4);
            inv.Add("scutum_spare", 4);
            inv.Add("helmet_spare", 5);
            inv.Add("pila_bundle", 6);
            inv.Add("dolabra", 8);
            inv.Add("whetstone", 6);
            inv.Add("saw_frame", 2);
            inv.Add("smith_hammer", 2);
            inv.Add("cobbler_kit", 3);

            // Paper that survived the forest.
            inv.Add("dispatch_xviii", 1);
            inv.Add("map_weser", 1);

            // What cannot be weighed.
            inv.Add("vexillum_xix", 1);
            inv.Add("carved_horse", 1);

            // The baggage train.
            inv.Add("ox_wagon", 1);
            inv.Add("mule", 3);
            inv.Add("pack_horse", 1);
            inv.Add("riding_horse", 2);
        }

        // --- Shared -----------------------------------------------------------------------------

        private static SoldierRecord MakeSoldier(
            string displayName, string rankId, string archetypeId, float morale, int experience = 0,
            float health = -1f)
        {
            return new SoldierRecord
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = displayName,
                RankId = rankId,
                ArchetypeId = archetypeId,
                Health01 = health >= 0f ? health : Random.Range(0.55f, 1f),
                Stamina01 = Random.Range(0.6f, 1f),
                Morale01 = morale,
                Loyalty01 = Mathf.Clamp01(morale + Random.Range(-0.2f, 0.2f)),
                Experience = experience
            };
        }

        private static string RandomRomanName()
        {
            string praenomen = Praenomina[Random.Range(0, Praenomina.Length)];
            string nomen = Nomina[Random.Range(0, Nomina.Length)];
            return $"{praenomen[0]}. {nomen}";
        }
    }
}
