using Century.Campaign.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Puts things in the world: the sandbox's warbands and places, and the Aftermath's story
    /// ground. Lives in the campaign layer so the onboarding director can seed the open world the
    /// moment the passage is taken, without reaching up into the App layer.
    /// </summary>
    /// <remarks>
    /// Positions are authored flat (y = 0); the overmap grounds them on load. Every world position
    /// runs through <see cref="CampaignSettings.ClampToWorld"/> so nothing spawns beyond the mesh.
    /// </remarks>
    public static class CampaignSeeder
    {
        private static readonly string[] WarbandNames =
            { "Marsi Warband", "Bructeri Hunters", "Chatti Outriders", "Cherusci Scouts", "Sicambri Spears" };

        private static readonly string[] RaiderNames =
            { "Cherusci Raiders", "Broken Men", "Wolf Brothers", "River Wolves" };

        private static readonly string[] WarHostNames =
            { "Host of the Cherusci", "Arminius' Vanguard", "The Gathered Tribes" };

        private static readonly string[] LooterNames =
            { "Battlefield Looters", "Bructeri Scavengers", "Corpse-pickers" };

        // --- The open world -----------------------------------------------------------------------

        /// <summary>
        /// The sandbox: a mixed opposition (quick opportunists, middling warbands, one host you should
        /// think hard about meeting) and the opening points of interest. Seeded at a sandbox start,
        /// or the moment the guided start's passage is taken. Idempotent per campaign.
        /// </summary>
        public static void SeedOpenWorld(CampaignState state, CampaignSettings settings)
        {
            if (state.OpenWorldSeeded) return;
            state.OpenWorldSeeded = true;

            Random.State previous = Random.state;
            Random.InitState(state.RandomSeed ^ 0x51ED);

            state.AddParty(CreateWarband(state, 0, settings, PartyKind.Raiders));
            state.AddParty(CreateWarband(state, 1, settings, PartyKind.Raiders));
            state.AddParty(CreateWarband(state, 2, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 3, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 4, settings, PartyKind.Warband));
            state.AddParty(CreateWarband(state, 5, settings, PartyKind.WarHost));

            SeedPointsOfInterest(state, settings);

            Random.state = previous;
        }

        private static PartyState CreateWarband(
            CampaignState state, int index, CampaignSettings settings, PartyKind kind)
        {
            float angle = index / 6f * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);

            // The host starts far out: a storm on the horizon, not a doorstep surprise.
            float radius = kind == PartyKind.WarHost ? Random.Range(380f, 520f) : Random.Range(180f, 420f);

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

            return MakeGermanicParty(state, settings, displayName, kind, spawn, strength,
                kind == PartyKind.Raiders ? CommanderBehaviour.Skirmisher : CommanderBehaviourPool.Pick());
        }

        /// <summary>
        /// Scatters the sandbox's opening points of interest. A couple sit inside the column's
        /// starting sight so the player sees the mechanic at once; the rest reward marching out.
        /// </summary>
        private static void SeedPointsOfInterest(CampaignState state, CampaignSettings settings)
        {
            AddPoi(state, settings, PoiKind.Settlement, "shrine", "A roadside shrine", new Vector3(70f, 0f, 45f));
            AddPoi(state, settings, PoiKind.Grove, "grove", "A druid's grove", new Vector3(165f, 0f, 130f));
            AddPoi(state, settings, PoiKind.Refugees, "refugees", "A refugee column", new Vector3(-185f, 0f, 95f));
            AddPoi(state, settings, PoiKind.Settlement, "merchant", "A travelling merchant", new Vector3(75f, 0f, -195f));
            AddPoi(state, settings, PoiKind.Ruin, "watchtower", "An abandoned watchtower", new Vector3(-150f, 0f, -155f));
            AddPoi(state, settings, PoiKind.RaiderCamp, "raiders", "A raider camp", new Vector3(255f, 0f, -70f));
            AddPoi(state, settings, PoiKind.Battlefield, "battlefield", "An old battlefield", new Vector3(-60f, 0f, 235f));
        }

        // --- The Aftermath ------------------------------------------------------------------------

        /// <summary>The guided start's ground: south-east of the map, east of the river, a killing
        /// field two hundred and eighty units on a side.</summary>
        public static readonly Vector2 AftermathMin = new Vector2(150f, -430f);
        public static readonly Vector2 AftermathMax = new Vector2(430f, -150f);

        /// <summary>Where the survivors wake on the overmap.</summary>
        public static readonly Vector3 AftermathStart = new Vector3(330f, 0f, -330f);

        /// <summary>The far side of the ravine: the open world's first ground.</summary>
        public static readonly Vector3 OpenWorldEntry = new Vector3(-20f, 0f, -60f);

        /// <summary>
        /// Lays out the Aftermath: the leash, the story places (two in sight, the rest hidden until
        /// the story reaches them) and two bands of looters working the field. The party itself is
        /// the factory's business.
        /// </summary>
        public static void SeedAftermath(CampaignState state, CampaignSettings settings)
        {
            OnboardingState ob = state.Onboarding;
            ob.Chapter = OnboardingChapter.Aftermath;
            ob.Objective = AftermathObjective.GatherSurvivors;
            ob.SurvivorTarget = 12;
            ob.ZoneMinX = AftermathMin.x;
            ob.ZoneMinZ = AftermathMin.y;
            ob.ZoneMaxX = AftermathMax.x;
            ob.ZoneMaxZ = AftermathMax.y;
            ob.OpenWorldEntry = OpenWorldEntry;

            // In sight from the start: the two battle sites. Discovered by proximity as usual.
            ob.CorpsesPoiId = AddPoi(state, settings, PoiKind.Corpses, "aftermath_corpses",
                "A wasteland of corpses", new Vector3(285f, 0f, -368f)).Id;
            ob.HoundsPoiId = AddPoi(state, settings, PoiKind.Hounds, "aftermath_hounds",
                "Hounds on the field", new Vector3(386f, 0f, -292f)).Id;

            // Hidden until the story reaches them.
            ob.LootersPoiId = AddPoi(state, settings, PoiKind.Looters, "aftermath_looters",
                "Looters at a fire", new Vector3(302f, 0f, -236f), hidden: true).Id;
            ob.SafePlacePoiId = AddPoi(state, settings, PoiKind.SafePlace, "aftermath_safe_place",
                "A hollow in the rocks", new Vector3(238f, 0f, -302f), hidden: true).Id;
            ob.PassagePoiId = AddPoi(state, settings, PoiKind.Passage, "aftermath_passage",
                "The passage", new Vector3(176f, 0f, -172f), hidden: true).Id;

            // The looters: too many for the survivors to face at once, few enough for twelve.
            Random.State previous = Random.state;
            Random.InitState(state.RandomSeed ^ 0x100E);

            state.AddParty(MakeLooters(state, settings, 0, new Vector3(395f, 0f, -395f), 6));
            state.AddParty(MakeLooters(state, settings, 1, new Vector3(205f, 0f, -405f), 8));
            state.AddParty(MakeLooters(state, settings, 2, new Vector3(400f, 0f, -190f), 7));

            Random.state = previous;
        }

        private static PartyState MakeLooters(
            CampaignState state, CampaignSettings settings, int index, Vector3 spawn, int strength)
        {
            PartyState party = MakeGermanicParty(state, settings,
                LooterNames[index % LooterNames.Length], PartyKind.Warband, spawn, strength,
                CommanderBehaviour.Skirmisher);

            // Looters see less far than a warband on the hunt, and never leave the killing ground.
            party.DetectionRadius = 58f;
            party.SpeedModifier = 0.95f;
            party.HasHomeZone = true;
            party.HomeMinX = AftermathMin.x;
            party.HomeMinZ = AftermathMin.y;
            party.HomeMaxX = AftermathMax.x;
            party.HomeMaxZ = AftermathMax.y;
            return party;
        }

        // --- Shared -------------------------------------------------------------------------------

        private static PartyState MakeGermanicParty(
            CampaignState state, CampaignSettings settings, string displayName, PartyKind kind,
            Vector3 spawn, int strength, CommanderBehaviour behaviour)
        {
            var party = new PartyState
            {
                Id = state.MintId("party"),
                DisplayName = displayName,
                Faction = PartyFaction.Germanic,
                Kind = kind,
                WorldPosition = settings.ClampToWorld(spawn),
                DetectionRadius = Random.Range(settings.WarbandDetectionRadiusMin, settings.WarbandDetectionRadiusMax),
                SpeedModifier = Random.Range(settings.WarbandSpeedModifierMin, settings.WarbandSpeedModifierMax),
                Behaviour = behaviour,
                Stores = { Food = 120f + strength * 2f }
            };

            party.Roster.Capacity = strength;
            for (int i = 0; i < strength; i++)
                party.Roster.Add(MakeWarrior($"Warrior {i + 1}", Random.Range(0.55f, 1f)));

            party.Morale.Value01 = party.Roster.AverageMorale01;
            return party;
        }

        public static SoldierRecord MakeWarrior(string displayName, float morale)
        {
            return new SoldierRecord
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = displayName,
                RankId = "warrior",
                ArchetypeId = "cherusci_warrior",
                Health01 = Random.Range(0.55f, 1f),
                Stamina01 = Random.Range(0.6f, 1f),
                Morale01 = morale,
                Loyalty01 = 0.5f
            };
        }

        private static PointOfInterest AddPoi(
            CampaignState state, CampaignSettings settings, PoiKind kind, string eventId, string name,
            Vector3 position, bool hidden = false)
        {
            var poi = new PointOfInterest
            {
                Id = state.MintId("poi"),
                Kind = kind,
                EventId = eventId,
                DisplayName = name,
                WorldPosition = settings.ClampToWorld(position),
                Hidden = hidden
            };
            state.PointsOfInterest.Add(poi);
            return poi;
        }
    }
}
