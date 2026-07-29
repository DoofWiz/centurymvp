using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Items;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Builds a starting campaign. Temporary: this will be replaced by save-file loading and by
    /// designer-authored scenario assets, but having a concrete state on boot means every system
    /// below it can be exercised from day one.
    /// </summary>
    public static class NewCampaignFactory
    {
        private static readonly string[] Praenomina =
            { "Gaius", "Lucius", "Marcus", "Publius", "Quintus", "Titus", "Aulus", "Decimus", "Servius", "Spurius" };

        private static readonly string[] Nomina =
            { "Valerius", "Aquilius", "Vorenus", "Secundus", "Felix", "Cornelius", "Antonius", "Fabius", "Iulius", "Sergius" };

        private static readonly string[] WarbandNames =
            { "Marsi Warband", "Bructeri Hunters", "Chatti Outriders", "Cherusci Scouts", "Sicambri Spears" };

        private static readonly string[] RaiderNames =
            { "Cherusci Raiders", "Broken Men", "Wolf Brothers", "River Wolves" };

        private static readonly string[] WarHostNames =
            { "Host of the Cherusci", "Arminius' Vanguard", "The Gathered Tribes" };

        public static CampaignState Create(int seed, CampaignSettings settings)
        {
            var clock = new CampaignClock(CampaignTime.FromDays(22).Plus(12 * CampaignTime.MinutesPerHour));
            var state = new CampaignState(clock) { RandomSeed = seed };

            Random.State previousRandomState = Random.state;
            Random.InitState(seed);

            state.AddParty(CreatePlayerParty(state, settings));

            // A mixed opposition: quick opportunists, middling warbands, and one host you should
            // think very hard about meeting at all.
            state.AddParty(CreateWarband(state, 0, settings, PartyKind.Raiders));
            state.AddParty(CreateWarband(state, 1, settings, PartyKind.Raiders));
            state.AddParty(CreateWarband(state, 2, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 3, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 4, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 5, settings, PartyKind.WarHost));

            SeedPointsOfInterest(state, settings);

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
        /// tools, a modest baggage train, a few things worth trading — and its own standard.
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

            // Spare kit — the difference between a loss and a bad day.
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

        private static PartyState CreateWarband(
            CampaignState state, int index, CampaignSettings settings, PartyKind kind)
        {
            float angle = index / 6f * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);

            // The host starts far out — a storm on the horizon, not a doorstep surprise.
            float radius = kind == PartyKind.WarHost ? Random.Range(380f, 520f) : Random.Range(180f, 420f);

            // Clamped so a warband cannot spawn beyond the terrain and fail to reach the NavMesh.
            Vector3 spawn = settings.ClampToWorld(
                new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));

            string displayName;
            int strength;
            switch (kind)
            {
                case PartyKind.Raiders:
                    displayName = RaiderNames[index % RaiderNames.Length];
                    strength = Random.Range(8, 17);
                    break;
                case PartyKind.WarHost:
                    displayName = WarHostNames[index % WarHostNames.Length];
                    strength = Random.Range(55, 91);
                    break;
                default:
                    displayName = WarbandNames[index % WarbandNames.Length];
                    strength = Random.Range(18, 46);
                    break;
            }

            var party = new PartyState
            {
                Id = state.MintId("party"),
                DisplayName = displayName,
                Faction = PartyFaction.Germanic,
                Kind = kind,
                WorldPosition = spawn,
                DetectionRadius = Random.Range(settings.WarbandDetectionRadiusMin, settings.WarbandDetectionRadiusMax),
                SpeedModifier = Random.Range(settings.WarbandSpeedModifierMin, settings.WarbandSpeedModifierMax),
                Behaviour = kind == PartyKind.Raiders
                    ? Century.Core.Contracts.CommanderBehaviour.Skirmisher
                    : CommanderBehaviourPool.Pick(),
                Stores = { Food = 120f + strength * 2f }
            };

            party.Roster.Capacity = strength;
            for (int i = 0; i < strength; i++)
                party.Roster.Add(MakeSoldier($"Warrior {i + 1}", "warrior", "cherusci_warrior", Random.Range(0.55f, 1f)));

            party.Morale.Value01 = party.Roster.AverageMorale01;
            return party;
        }

        /// <summary>
        /// Scatters the opening points of interest across the map. A couple sit inside the column's
        /// starting sight so the player sees the mechanic at once; the rest reward marching out.
        /// </summary>
        private static void SeedPointsOfInterest(CampaignState state, CampaignSettings settings)
        {
            void Add(PoiKind kind, string eventId, string name, Vector3 pos) =>
                state.PointsOfInterest.Add(new PointOfInterest
                {
                    Id = state.MintId("poi"),
                    Kind = kind,
                    EventId = eventId,
                    DisplayName = name,
                    WorldPosition = settings.ClampToWorld(pos)
                });

            Add(PoiKind.Settlement, "shrine", "A roadside shrine", new Vector3(70f, 0f, 45f));
            Add(PoiKind.Grove, "grove", "A druid's grove", new Vector3(165f, 0f, 130f));
            Add(PoiKind.Refugees, "refugees", "A refugee column", new Vector3(-185f, 0f, 95f));
            Add(PoiKind.Settlement, "merchant", "A travelling merchant", new Vector3(75f, 0f, -195f));
            Add(PoiKind.Ruin, "watchtower", "An abandoned watchtower", new Vector3(-150f, 0f, -155f));
            Add(PoiKind.RaiderCamp, "raiders", "A raider camp", new Vector3(255f, 0f, -70f));
            Add(PoiKind.Battlefield, "battlefield", "An old battlefield", new Vector3(-60f, 0f, 235f));
        }

        private static SoldierRecord MakeSoldier(
            string displayName, string rankId, string archetypeId, float morale, int experience = 0)
        {
            return new SoldierRecord
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = displayName,
                RankId = rankId,
                ArchetypeId = archetypeId,
                Health01 = Random.Range(0.55f, 1f),
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
