using System;
using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>How long the century rests in one sitting.</summary>
    public enum RestSpan
    {
        /// <summary>A single three-hour watch.</summary>
        OneWatch = 0,
        /// <summary>Straight through to first light (06:00).</summary>
        UntilDawn = 1
    }

    /// <summary>What a rest actually did, so the UI can report it back to the player.</summary>
    public readonly struct RestResult
    {
        public readonly double Hours;
        public readonly float FoodSpent;
        public readonly float FirewoodSpent;
        public readonly int WoundedHealed;
        public readonly float MoraleDelta;
        public readonly float StaminaDelta;
        public readonly bool WasCold;

        public RestResult(double hours, float foodSpent, float firewoodSpent, int woundedHealed,
            float moraleDelta, float staminaDelta, bool wasCold)
        {
            Hours = hours;
            FoodSpent = foodSpent;
            FirewoodSpent = firewoodSpent;
            WoundedHealed = woundedHealed;
            MoraleDelta = moraleDelta;
            StaminaDelta = staminaDelta;
            WasCold = wasCold;
        }
    }

    /// <summary>
    /// The camp layer's mutations of party state: resting the men, appointing officers, raising
    /// stations, and giving orders. Plain C# — it holds no scene references and can be exercised
    /// without Unity, matching <see cref="CampaignSimulation"/>.
    /// </summary>
    /// <remarks>
    /// Resting works by advancing the campaign clock. <see cref="CampaignSimulation"/> is already
    /// subscribed to <see cref="CampaignClock.HourElapsed"/> and, seeing <c>IsCamped</c>, burns
    /// rations at the camped rate and recovers stamina hour by hour. This service only layers on the
    /// effects that simulation does not model — firewood, healing the wounded, morale recovery and kit
    /// repair — so nothing is counted twice.
    /// </remarks>
    public sealed class CampService
    {
        // --- Tuning. Kept here as consts so the existing CampaignSettings asset needs no edits. ----
        private const float FirewoodPerManPerHour = 0.02f;   // ~3.8 bundles/night for 80 men
        private const float MoralePerHour = 0.010f;          // rest alone, before officer bonuses
        private const float OptioMoralePerHour = 0.006f;
        private const float SigniferMoralePerHour = 0.005f;
        private const float ColdMoralePerHour = 0.008f;      // drained instead of gained in a cold camp
        private const float WoundHealPerHour = 0.020f;       // Health01, before the medical tent factor
        private const float MedicinePerWoundedPerHour = 0.03f;
        private const float FabricaRepairPerHourPerLevel = 0.0025f;
        private const int TrainingBaseXp = 60;               // per man, per training order, per level
        private const int TrainingBatchSize = 12;

        private readonly CampaignState _state;
        private readonly PartyState _party;
        private readonly CampaignSettings _settings;
        private readonly CampaignClock _clock;
        private readonly CampaignEventLog _log;

        public CampService(
            CampaignState state, PartyState party, CampaignSettings settings, CampaignClock clock, CampaignEventLog log)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _party = party ?? throw new ArgumentNullException(nameof(party));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log;
        }

        // --- Rest ------------------------------------------------------------------------------

        public RestResult Rest(RestSpan span)
        {
            double hours = span == RestSpan.OneWatch
                ? CampaignTime.MinutesPerWatch / (double)CampaignTime.MinutesPerHour
                : HoursUntilDawn(_clock.Now);

            int mouths = _party.Roster.ActiveCount;

            float foodBefore = _party.Stores.Food;
            float moraleBefore = _party.Roster.AverageMorale01;
            float staminaBefore = AverageStamina();

            // Advancing the clock is what burns rations and recovers stamina — that lives in
            // CampaignSimulation, which is watching HourElapsed. We only add camp-only effects after.
            _clock.Advance(hours * CampaignTime.MinutesPerHour);

            // The night fires burn real firewood from the inventory; charcoal burns longer. A camp
            // short of fuel is a cold camp, with everything that follows from that.
            float firewoodNeed = FirewoodPerManPerHour * mouths * (float)hours;
            float fuelHave = Core.Items.ItemCatalog.TotalFuel(_party.Inventory);
            float firewoodSpent = Core.Items.ItemCatalog.BurnFuel(
                _party.Inventory, Mathf.Min(firewoodNeed, fuelHave));
            float warmth = firewoodNeed <= 0.0001f ? 1f : Mathf.Clamp01(fuelHave / firewoodNeed);
            bool wasCold = warmth < 0.6f;

            int woundedHealed = HealWounded(hours, warmth);
            RecoverMorale(hours, warmth);
            RepairEquipment(hours);

            // The scout comes in with the report at the end of the rest, not the moment he left.
            LastRestScoutReport = default;
            DeliverScoutReport();

            // Individual morale changed above; roll it back up to the party figure.
            _party.Morale.Value01 = _party.Roster.AverageMorale01;

            var result = new RestResult(
                hours,
                foodBefore - _party.Stores.Food,
                firewoodSpent,
                woundedHealed,
                _party.Roster.AverageMorale01 - moraleBefore,
                AverageStamina() - staminaBefore,
                wasCold);

            _log?.Push(CampaignEventKind.Supply,
                span == RestSpan.UntilDawn ? "The camp rests until dawn" : "The camp takes a watch's rest",
                wasCold ? "A cold camp — the fires burned low" : $"{woundedHealed} wounded on the mend",
                _clock.Now.DayNumber);

            return result;
        }

        private int HealWounded(double hours, float warmth)
        {
            int medicalLevel = _party.Facilities.LevelOf(CampStationId.MedicalTent);
            float healFactor = (1f + 0.25f * medicalLevel) * Mathf.Lerp(0.4f, 1f, warmth);

            // "Fortify the Camp": every night, a little Rome.
            if (_state.Commander.Has("fortify_the_camp")) healFactor *= 1.25f;

            // Convalescence: the medicus runs a proper sick line, and the wounded mend faster.
            if (PostTreeCatalog.Invested(_state, "convalescence")) healFactor *= 1.3f;
            float healPerMan = WoundHealPerHour * healFactor * (float)hours;

            int startingWounded = _party.Roster.WoundedCount;

            // Medicine is real items now: total the doses needed for this rest, see what the chest
            // holds, and heal in proportion. The medicus spends whole dressings, not fractions.
            float dosesNeeded = startingWounded * MedicinePerWoundedPerHour * (float)hours;
            float dosesHave = Core.Items.ItemCatalog.TotalMedicineDoses(_party.Inventory);
            float supply01 = dosesNeeded <= 0.0001f ? 1f : Mathf.Clamp01(dosesHave / dosesNeeded);

            if (supply01 > 0f)
            {
                for (int i = 0; i < _party.Roster.Soldiers.Count; i++)
                {
                    SoldierRecord soldier = _party.Roster.Soldiers[i];
                    if (!soldier.IsAlive || soldier.Health01 >= 0.5f) continue;
                    soldier.Health01 = Mathf.Clamp01(soldier.Health01 + healPerMan * supply01);
                }

                Core.Items.ItemCatalog.SpendMedicine(_party.Inventory, dosesNeeded * supply01);
            }

            // Men who crossed back above the wound threshold this rest.
            return Mathf.Max(0, startingWounded - _party.Roster.WoundedCount);
        }

        private void RecoverMorale(double hours, float warmth)
        {
            bool optio = _party.Appointments.IsFilled(CampRole.Optio);
            bool signifer = _party.Appointments.IsFilled(CampRole.Signifer);

            float perHour = MoralePerHour * warmth
                            + (optio ? OptioMoralePerHour : 0f)
                            + (signifer ? SigniferMoralePerHour : 0f)
                            - ColdMoralePerHour * (1f - warmth);

            float delta = perHour * (float)hours;
            if (delta > 0f && _state.Commander.Has("fortify_the_camp")) delta *= 1.25f;
            if (Mathf.Abs(delta) < 0.0001f) return;

            for (int i = 0; i < _party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = _party.Roster.Soldiers[i];
                if (!soldier.IsAlive) continue;
                soldier.Morale01 = Mathf.Clamp01(soldier.Morale01 + delta);
            }
        }

        private void RepairEquipment(double hours)
        {
            int fabricaLevel = _party.Facilities.LevelOf(CampStationId.Fabrica);
            if (fabricaLevel <= 0) return;

            float repair = FabricaRepairPerHourPerLevel * fabricaLevel * (float)hours;
            _party.Stores.EquipmentCondition01 = Mathf.Clamp01(_party.Stores.EquipmentCondition01 + repair);
        }

        // --- Officer roles ---------------------------------------------------------------------

        /// <summary>Living men who may hold this post, ordered most-senior first.</summary>
        public List<SoldierRecord> EligibleFor(CampRole role)
        {
            CampRoleInfo info = CampRoleInfo.For(role);
            var eligible = new List<SoldierRecord>();

            for (int i = 0; i < _party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = _party.Roster.Soldiers[i];
                if (info.IsEligible(soldier)) eligible.Add(soldier);
            }

            eligible.Sort((a, b) => b.Experience.CompareTo(a.Experience));
            return eligible;
        }

        public void AssignRole(CampRole role, string soldierId)
        {
            string previousId = _party.Appointments.GetHolderId(role);
            _party.Appointments.Assign(role, soldierId);

            SoldierRecord holder = soldierId == null ? null : _party.Roster.Find(soldierId);
            CampRoleInfo info = CampRoleInfo.For(role);

            _log?.Push(CampaignEventKind.Gain,
                holder == null ? $"{info.Latin} left vacant" : $"{holder.DisplayName} named {info.Latin}",
                info.Benefit,
                _clock.Now.DayNumber);

            // Appointment politics: an office given is a favour; an office taken away is a wound,
            // and the man it was taken FROM knows exactly who it was given to.
            if (holder != null) RelationshipLedger.AdjustLoyalty(holder, 0.08f);

            SoldierRecord displaced = previousId != null && previousId != soldierId
                ? _party.Roster.Find(previousId) : null;
            if (displaced != null && displaced.IsAlive)
            {
                RelationshipLedger.AdjustLoyalty(displaced, -0.05f);
                if (holder != null)
                {
                    RelationshipLedger.Adjust(displaced, holder, -0.2f,
                        $"stood down from the {info.Latin}'s post to make way for him");

                    _log?.Push(CampaignEventKind.Loss,
                        $"{displaced.DisplayName} takes it ill",
                        $"Stood down from {info.Latin} to make way",
                        _clock.Now.DayNumber);
                }
            }
        }

        // --- Stations --------------------------------------------------------------------------

        public bool CanAfford(CampStationId id)
        {
            CampStationCatalog info = CampStationCatalog.Info(id);
            int level = _party.Facilities.LevelOf(id);
            if (level >= info.MaxLevel) return false;

            // Stations are raised from real timber in the inventory, plus coin for the rest.
            return _party.Inventory.CountOf("timber") >= info.TimberCost(level)
                && _party.Stores.Denarii >= info.DenariiCost(level);
        }

        public bool BuildOrUpgrade(CampStationId id)
        {
            if (!CanAfford(id)) return false;

            CampStationCatalog info = CampStationCatalog.Info(id);
            int level = _party.Facilities.LevelOf(id);

            _party.Inventory.Remove("timber", info.TimberCost(level));
            _party.Stores.Denarii -= info.DenariiCost(level);
            _party.Facilities.SetLevel(id, level + 1);

            _state.Commander.Note(CommanderPhilosophy.TrueRoman, 0.3f);

            _log?.Push(CampaignEventKind.Gain,
                level == 0 ? $"{info.Name} raised" : $"{info.Name} improved",
                info.EffectAt(level + 1),
                _clock.Now.DayNumber);

            return true;
        }

        // --- Crafting --------------------------------------------------------------------------

        /// <summary>How many times the recipe could be made with what the inventory holds now.</summary>
        public int MaxCraftable(CraftingRecipe recipe)
        {
            if (_party.Facilities.LevelOf(recipe.Station) <= 0) return 0;

            int most = int.MaxValue;
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                int have = _party.Inventory.CountOf(recipe.Inputs[i].ItemId);
                most = Mathf.Min(most, have / Mathf.Max(1, recipe.Inputs[i].Count));
            }

            return most == int.MaxValue ? 0 : most;
        }

        /// <summary>Batch craft: makes the recipe as many times as the inputs allow. Returns the count.</summary>
        public int CraftAll(CraftingRecipe recipe)
        {
            int made = 0;
            while (Craft(recipe)) made++;
            return made;
        }

        /// <summary>The station is built and every input is in the inventory.</summary>
        public bool CanCraft(CraftingRecipe recipe)
        {
            if (_party.Facilities.LevelOf(recipe.Station) <= 0) return false;

            for (int i = 0; i < recipe.Inputs.Length; i++)
                if (_party.Inventory.CountOf(recipe.Inputs[i].ItemId) < recipe.Inputs[i].Count)
                    return false;

            return true;
        }

        /// <summary>
        /// Makes the recipe: consumes the inputs, adds Food to the stores (scaled up by the cooking
        /// fire's level) and/or the output items to the inventory, and logs the work.
        /// </summary>
        public bool Craft(CraftingRecipe recipe)
        {
            if (!CanCraft(recipe)) return false;

            for (int i = 0; i < recipe.Inputs.Length; i++)
                _party.Inventory.Remove(recipe.Inputs[i].ItemId, recipe.Inputs[i].Count);

            _state.Commander.Note(CommanderPhilosophy.Survivor, 0.15f);

            string detail;
            if (recipe.FoodYield > 0f)
            {
                int stationLevel = _party.Facilities.LevelOf(recipe.Station);
                float yield = recipe.FoodYield * (1f + 0.15f * Mathf.Max(0, stationLevel - 1));
                _party.Stores.Food += yield;
                detail = $"+{yield:0.#} food for the stores";
            }
            else
            {
                _party.Inventory.Add(recipe.OutputItemId, recipe.OutputCount);
                Core.Items.ItemDef output = Core.Items.ItemCatalog.Find(recipe.OutputItemId);
                detail = $"+{recipe.OutputCount} {(output != null ? output.Name : recipe.OutputItemId)}";
            }

            _log?.Push(CampaignEventKind.Supply, recipe.Name, detail, _clock.Now.DayNumber);
            return true;
        }

        // --- Orders ----------------------------------------------------------------------------

        public bool CanScout => _party.Appointments.IsFilled(CampRole.Speculator) && !_party.ScoutRidingOut;

        /// <summary>The scout is out and will report when the column next rests.</summary>
        public bool ScoutIsOut => _party.ScoutRidingOut;

        /// <summary>What the last rest's scouting brought in, if the scout was out. The camp screen
        /// reads this straight after <see cref="Rest"/>; <c>Sent</c> is false when there was none.</summary>
        public ScoutReport LastRestScoutReport { get; private set; }

        /// <summary>What the speculator reports back, for the scout-result modal. HONEST about
        /// what was rolled — vague or exact by the office's intel quality — except that a false
        /// lead reads exactly like a solid find, because he cannot know his report was wrong.</summary>
        public readonly struct ScoutReport
        {
            public readonly bool Sent;
            public readonly string Title, Detail, Reward, Risk, Distance;

            /// <summary>The modal's headline: "Opportunity sighted" for a prize, something else for
            /// a report that is not one (the Aftermath's passage).</summary>
            public readonly string Heading;

            public ScoutReport(string title, string detail, string reward, string risk, string distance,
                string heading = "Opportunity sighted")
            {
                Sent = true;
                Title = title;
                Detail = detail;
                Reward = reward;
                Risk = risk;
                Distance = distance;
                Heading = heading;
            }
        }

        /// <summary>
        /// Sends the Speculator ranging. What he finds is rolled through the
        /// <see cref="SpeculatorInfluence"/> seam and seeded as a real POI; the report the player
        /// reads describes THAT find, at whatever precision the office has earned.
        /// </summary>
        public bool SendScouting()
        {
            if (!CanScout) return false;

            // He rides out now; what he found comes back with him at the end of the next rest.
            _party.ScoutRidingOut = true;
            _state.Commander.Note(CommanderPhilosophy.Survivor, 0.4f);

            _log?.Push(CampaignEventKind.Discovery, "The scout rides out",
                "He reports when the column next rests", _clock.Now.DayNumber);
            return true;
        }

        /// <summary>
        /// The scout's return, at the end of a rest. In the Aftermath the first ride finds the
        /// passage; otherwise a real prize is seeded through the speculator seam and the report
        /// describes THAT find, at whatever precision the office has earned.
        /// </summary>
        private void DeliverScoutReport()
        {
            if (OnboardingDirector.TryDeliverPassage(_state, _settings, _log, out ScoutReport passage))
            {
                LastRestScoutReport = passage;
                return;
            }

            if (!_party.ScoutRidingOut) return;
            _party.ScoutRidingOut = false;

            PointOfInterest poi = PoiCatalog.CreateOpportunity(_state, _settings, _party.WorldPosition);

            // The speculator's office learns by ranging: his points come from rides, not battles.
            PostTreeCatalog.AwardScoutPoint(_state, _party.Roster);

            ScoutReport report = ComposeReport(poi);
            _log?.Push(
                CampaignEventKind.Discovery,
                poi.EventId == "signum_held" ? "Word of the signum" : "Opportunity sighted",
                report.Detail,
                _clock.Now.DayNumber);
            LastRestScoutReport = report;
        }

        private ScoutReport ComposeReport(PointOfInterest poi)
        {
            bool exact = SpeculatorInfluence.Current.ExactIntel(_state);
            float miles = Mathf.Max(1f, Vector3.Distance(poi.WorldPosition, _party.WorldPosition) / 25f);
            string distance = $"{miles:0} miles out";

            switch (poi.EventId)
            {
                case "signum_held":
                    return new ScoutReport(
                        "The signum, run to ground",
                        "He has found it: the warband that took the century's standard, camped and celebrating.",
                        "The signum — the century's honour",
                        "They know what they hold. It will be defended hard",
                        distance);

                case "opportunity_rich" when exact:
                    return new ScoutReport(
                        "A chieftain's holding, heavy with plunder",
                        "Full granaries, penned cattle, amber and silver. His count was careful and his eye is good.",
                        "Rich — a chieftain's hoard",
                        "Two dozen spears at least, and stout walls",
                        distance);

                case "opportunity_meagre" when exact:
                    return new ScoutReport(
                        "A poor steading behind a hurdle fence",
                        "Held, but barely, and holding little. He reports it because it is there, not because it is worth much.",
                        "Meagre — grain and small coin",
                        "A handful of farmers with spears",
                        distance);

                default:
                    // Vague intel — and every false lead, at any intel level: a promise, no more.
                    return new ScoutReport(
                        "A strongpoint, lightly held",
                        "Something worth the taking behind a palisade, by his account. He could not linger to count.",
                        exact ? "Worth the march, by his count" : "Plunder, if his word is good",
                        exact ? "About a dozen spears" : "He could not count them all",
                        distance);
            }
        }

        /// <summary>What a hunt brought back, for the UI to report.</summary>
        public readonly struct HuntResult
        {
            public readonly int Game;
            public readonly int Pelts;

            public HuntResult(int game, int pelts)
            {
                Game = game;
                Pelts = pelts;
            }
        }

        /// <summary>
        /// Sends hunting parties into the woods for a watch. They bring back raw game for the cooking
        /// fire — more of it when the scout's fire has taught them the ground — and sometimes pelts.
        /// The forest owes nobody: some watches the snares come back empty-handed but for a hare.
        /// </summary>
        public HuntResult SendHunters()
        {
            _clock.Advance(CampaignTime.MinutesPerWatch);

            int scoutFire = _party.Facilities.LevelOf(CampStationId.ScoutFire);
            int game = 1 + UnityEngine.Random.Range(1, 4) + scoutFire;
            int pelts = UnityEngine.Random.value < 0.35f ? 1 + scoutFire / 2 : 0;

            _party.Inventory.Add("raw_game", game);
            _state.Commander.Note(CommanderPhilosophy.Survivor, 0.4f);
            if (pelts > 0) _party.Inventory.Add("fur_pelts", pelts);

            _log?.Push(CampaignEventKind.Supply, "The hunters return",
                pelts > 0 ? $"{game} game and {pelts} pelts taken" : $"{game} game taken",
                _clock.Now.DayNumber);

            return new HuntResult(game, pelts);
        }

        public bool CanTrain => _party.Facilities.LevelOf(CampStationId.TrainingGround) > 0;

        /// <summary>Drills the greenest men, trading a watch for experience across the ranks.</summary>
        public bool TrainRanks()
        {
            int trainingLevel = _party.Facilities.LevelOf(CampStationId.TrainingGround);
            if (trainingLevel <= 0) return false;

            _clock.Advance(CampaignTime.MinutesPerWatch);

            bool optio = _party.Appointments.IsFilled(CampRole.Optio);
            int xpPerMan = Mathf.RoundToInt(TrainingBaseXp * trainingLevel * (optio ? 1.25f : 1f));

            // Drill: the optio's tradition of the hastile — training bites half again as deep.
            if (PostTreeCatalog.Invested(_state, "drill"))
                xpPerMan = Mathf.RoundToInt(xpPerMan * 1.5f);

            _state.Commander.Note(CommanderPhilosophy.TrueRoman, 0.5f);

            int trained = 0;
            for (int i = 0; i < _party.Roster.Soldiers.Count && trained < TrainingBatchSize; i++)
            {
                SoldierRecord soldier = _party.Roster.Soldiers[i];
                if (!soldier.IsAlive || soldier.Tier >= VeterancyTier.Veteranus) continue;
                soldier.Experience += xpPerMan;
                trained++;
            }

            _log?.Push(CampaignEventKind.Gain, "The ranks drill",
                $"{trained} men gained {xpPerMan} experience apiece", _clock.Now.DayNumber);
            return trained > 0;
        }

        // --- Helpers ---------------------------------------------------------------------------

        private float AverageStamina()
        {
            float total = 0f;
            int active = 0;
            for (int i = 0; i < _party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = _party.Roster.Soldiers[i];
                if (!soldier.IsAlive) continue;
                total += soldier.Stamina01;
                active++;
            }

            return active <= 0 ? 0f : total / active;
        }

        /// <summary>Hours from <paramref name="now"/> to the next 06:00. A full day when already at dawn.</summary>
        private static double HoursUntilDawn(CampaignTime now)
        {
            double minutesNow = now.Hour * CampaignTime.MinutesPerHour + now.Minute;
            double dawn = CampaignTime.FirstWatchStartHour * CampaignTime.MinutesPerHour;
            double delta = dawn - minutesNow;
            if (delta <= 0d) delta += CampaignTime.MinutesPerDay;
            return delta / CampaignTime.MinutesPerHour;
        }
    }
}
