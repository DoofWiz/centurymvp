using System;
using Century.Campaign.Model;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// The guided start on the overmap: THE AFTERMATH. Watches the campaign state, advances the
    /// objective chain in the brief's order (gather twelve men, find the safe place, make camp,
    /// reach the passage), reveals story places when the story reaches them, and hands the decanus'
    /// lines to whoever is listening. Plain C#, ticked by the overmap director runner on its
    /// heartbeat; nothing here touches a scene object.
    /// </summary>
    /// <remarks>
    /// Everything it decides from is on <see cref="OnboardingState"/>, so a save taken between two
    /// beats resumes exactly where it stood. The director itself holds no state worth keeping.
    /// </remarks>
    public sealed class OnboardingDirector
    {
        /// <summary>A line from the decanus, for the HUD to put in front of the player.</summary>
        public struct Narration
        {
            public string Title;
            public string Speaker;
            public string Body;
            public string Button;
        }

        public const string DecanusName = "T. Valerius";

        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;
        private readonly CampaignEventLog _log;

        /// <summary>The decanus speaks. The HUD pauses the clock and shows it.</summary>
        public event Action<Narration> Narrated;

        /// <summary>The objective line changed; the HUD redraws it.</summary>
        public event Action ObjectiveChanged;

        /// <summary>The passage is taken: the world beyond it has been seeded and the party moved.
        /// The scene must be rebuilt (reload the overmap) for the new parties and places to appear.</summary>
        public event Action OpenWorldEntered;

        public OnboardingDirector(CampaignState state, CampaignSettings settings, CampaignEventLog log)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log;
        }

        // --- The objective line, for the HUD ------------------------------------------------------

        /// <summary>Headline for the objective panel. Null when no guided objective stands.</summary>
        public static string ObjectiveText(CampaignState state)
        {
            OnboardingState ob = state?.Onboarding;
            if (ob == null || !ob.IsAftermath) return null;

            int men = state.PlayerParty != null ? state.PlayerParty.Roster.ActiveCount : 0;
            switch (ob.Objective)
            {
                case AftermathObjective.GatherSurvivors:
                    return $"Gather survivors: {men} of {ob.SurvivorTarget} men";
                case AftermathObjective.FindSafePlace:
                    return "Find a safe place to camp";
                case AftermathObjective.MakeCamp:
                    return "Make camp: press CAMP";
                case AftermathObjective.ReachPassage:
                    return "Make your way to the passage";
                default:
                    return null;
            }
        }

        /// <summary>The tracked sub-line under the objective.</summary>
        public static string ObjectiveTracked(CampaignState state)
        {
            OnboardingState ob = state?.Onboarding;
            if (ob == null || !ob.IsAftermath) return null;

            switch (ob.Objective)
            {
                case AftermathObjective.GatherSurvivors:
                    return ob.LootersRevealed
                        ? "Free the chained men at the looters' fire"
                        : "Search the battle sites for the living";
                case AftermathObjective.FindSafePlace:
                    return "March to the hollow the decanus marked";
                case AftermathObjective.MakeCamp:
                    return "Rest the men; appoint a scout";
                case AftermathObjective.ReachPassage:
                    return "North-west, through the ravine. Avoid the looters";
                default:
                    return null;
            }
        }

        /// <summary>Is this place what the story wants next? Gold on the map. Outside the guided
        /// start only a scouted prize counts.</summary>
        public static bool IsObjectivePoi(CampaignState state, PointOfInterest poi)
        {
            if (state == null || poi == null) return false;

            OnboardingState ob = state.Onboarding;
            if (ob == null || !ob.IsAftermath) return poi.Kind == PoiKind.Opportunity;

            switch (ob.Objective)
            {
                case AftermathObjective.GatherSurvivors:
                    if (ob.LootersRevealed) return poi.Id == ob.LootersPoiId;
                    return poi.Id == ob.CorpsesPoiId || poi.Id == ob.HoundsPoiId;
                case AftermathObjective.FindSafePlace:
                case AftermathObjective.MakeCamp:
                    return poi.Id == ob.SafePlacePoiId;
                case AftermathObjective.ReachPassage:
                    return poi.Id == ob.PassagePoiId;
                default:
                    return false;
            }
        }

        // --- The heartbeat ------------------------------------------------------------------------

        private bool _beatFired;

        /// <summary>Called on the director cadence while the overmap runs and time flows. Returns
        /// true when a beat fired (a narration opened), so the caller skips the rest of its tick:
        /// the modal pauses the clock and nothing else should trigger under it.</summary>
        public bool Tick()
        {
            _beatFired = false;

            OnboardingState ob = _state.Onboarding;
            if (ob == null || !ob.IsAftermath) return false;

            PartyState party = _state.PlayerParty;
            if (party == null) return false;

            if (!ob.ArrivalTold)
            {
                ob.ArrivalTold = true;
                Narrate("The Aftermath",
                    "Centurion. Valerius, decanus of the fourth tent: five of us are all that is left of it. " +
                    "The Seventeenth, the Eighteenth and the Nineteenth are gone, annihilated in the forest. " +
                    "The barbarians are everywhere, but the bulk of them are moving on, hunting the other columns that got out. " +
                    "Behind them come the looters: picking the dead clean and finishing any straggler they find. That is us, if we sit here. " +
                    "There will be others alive in this. We should find them while there is anyone left to find.",
                    "GATHER THE MEN");
                ObjectiveChanged?.Invoke();
                return true;   // one beat per tick; the modal pauses the clock anyway
            }

            switch (ob.Objective)
            {
                case AftermathObjective.GatherSurvivors: TickGather(ob, party); break;
                case AftermathObjective.FindSafePlace: TickSafePlace(ob); break;
                case AftermathObjective.MakeCamp: break;   // the camp screen drives this one
                case AftermathObjective.ReachPassage: TickPassage(ob, party); break;
            }

            return _beatFired;
        }

        private void TickGather(OnboardingState ob, PartyState party)
        {
            if (!ob.LootersRevealed && IsResolved(ob.CorpsesPoiId) && IsResolved(ob.HoundsPoiId))
            {
                ob.LootersRevealed = true;
                Reveal(ob.LootersPoiId);
                Narrate("Smoke to the north",
                    "A fire, sir, in the hollow north of here, and voices round it that are not ours. Looters, " +
                    "by the sound. And Romans chained to a wagon: they mean to sell them. Those men are ours. " +
                    "I have marked the fire on the map.",
                    "MARK IT");
                ObjectiveChanged?.Invoke();
                return;
            }

            if (party.Roster.ActiveCount < ob.SurvivorTarget) return;

            ob.Objective = AftermathObjective.FindSafePlace;
            ob.SurvivorsGatheredTold = true;
            Reveal(ob.SafePlacePoiId);
            _log?.Push(CampaignEventKind.Gain, "Twelve men", "Enough to call a party", _state.Clock.Now.DayNumber);

            Narrate("Twelve men",
                "Twelve, sir. Enough to make a stand; not enough to hold a road. The men are dead on their feet " +
                "and half of them are bleeding. We need ground we can rest on before dark. There is a hollow in the " +
                "rocks to the west, screened by pines. I have marked it.",
                "FIND THE HOLLOW");
            ObjectiveChanged?.Invoke();
        }

        private void TickSafePlace(OnboardingState ob)
        {
            if (!IsResolved(ob.SafePlacePoiId)) return;

            ob.Objective = AftermathObjective.MakeCamp;
            ob.SafePlaceTold = true;
            ob.CampPromptTold = true;

            Narrate("The hollow",
                "This will do, sir. Press CAMP and let them sleep. I will walk you through how a camp is kept: " +
                "who does what, and what each part of it means for the men.",
                "MAKE CAMP");
            ObjectiveChanged?.Invoke();
        }

        private void TickPassage(OnboardingState ob, PartyState party)
        {
            if (!ob.PassageTold)
            {
                ob.PassageTold = true;
                Narrate("The way out",
                    "The scout found it, sir: a passage through the high ground to the north-west, an old ravine, " +
                    "rock and root and forest. Beyond it the killing ground ends. Looters roam between here and there. " +
                    "Get us through without being caught in the open.",
                    "MARCH");
                ObjectiveChanged?.Invoke();
                return;
            }

            if (!IsResolved(ob.PassagePoiId)) return;
            EnterOpenWorld(ob, party);
        }

        // --- The camp beats, driven from the camp screen -------------------------------------------

        /// <summary>
        /// The Aftermath's rest: with a Speculator appointed, the night's ride finds the passage.
        /// Called by <see cref="CampService.Rest"/>; returns the report the scout modal shows. The
        /// passage is a real place from this moment, and the objective moves on to reaching it.
        /// </summary>
        public static bool TryDeliverPassage(
            CampaignState state, CampaignSettings settings, CampaignEventLog log,
            out CampService.ScoutReport report)
        {
            report = default;
            OnboardingState ob = state?.Onboarding;
            PartyState party = state?.PlayerParty;
            if (ob == null || party == null || !ob.IsAftermath) return false;
            if (ob.Objective != AftermathObjective.MakeCamp) return false;
            if (!party.Appointments.IsFilled(CampRole.Speculator)) return false;

            PointOfInterest passage = Find(state, ob.PassagePoiId);
            if (passage == null) return false;

            passage.Hidden = false;
            passage.Discovered = true;
            party.ScoutRidingOut = false;

            ob.Objective = AftermathObjective.ReachPassage;
            ob.CampWalkthroughDone = true;

            PostTreeCatalog.AwardScoutPoint(state, party.Roster);

            float miles = Mathf.Max(1f, Vector3.Distance(passage.WorldPosition, party.WorldPosition) / 25f);
            report = new CampService.ScoutReport(
                "A passage to the north-west",
                "An old ravine through the forested rock north-west of here: stone walls, an ancient track, " +
                "and open country beyond. Looters roam the ground between. He could not linger to count them.",
                "The way out of the Aftermath",
                "Roaming looters on the road",
                $"{miles:0} miles out",
                "The scout reports");

            log?.Push(CampaignEventKind.Discovery, "The passage is found",
                "The scout's report: a ravine to the north-west", state.Clock.Now.DayNumber);
            return true;
        }

        // --- The open world -----------------------------------------------------------------------

        /// <summary>
        /// Through the ravine. The party is moved to the far side, the sandbox world is seeded
        /// around it (warbands, places worth stopping) and the leash comes off. The scene must be
        /// reloaded afterwards: that is the transition into the large area the brief asks for.
        /// </summary>
        private void EnterOpenWorld(OnboardingState ob, PartyState party)
        {
            ob.Chapter = OnboardingChapter.OpenWorld;
            ob.Objective = AftermathObjective.Complete;
            ob.OpenWorldTold = true;

            party.Destination = null;
            party.IsCamped = false;
            party.IsStealthed = false;
            party.WorldPosition = _settings.ClampToWorld(ob.OpenWorldEntry);

            // The looters stay behind in their killing ground; nothing follows through the rock.
            for (int i = 0; i < _state.Parties.Count; i++)
            {
                PartyState other = _state.Parties[i];
                if (other.IsPlayer || !other.HasHomeZone) continue;
                other.AiState = PartyAiState.Idle;
                other.PursuitTargetId = null;
                other.Destination = null;
            }

            CampaignSeeder.SeedOpenWorld(_state, _settings);

            _log?.Push(CampaignEventKind.Discovery, "Through the passage",
                "The forest opens. The long road home begins", _state.Clock.Now.DayNumber);

            ObjectiveChanged?.Invoke();
            OpenWorldEntered?.Invoke();
        }

        // --- Helpers ------------------------------------------------------------------------------

        private void Narrate(string title, string body, string button)
        {
            _beatFired = true;
            Narrated?.Invoke(new Narration
            {
                Title = title,
                Speaker = $"{DecanusName}, decanus",
                Body = body,
                Button = button
            });
        }

        private bool IsResolved(string poiId)
        {
            PointOfInterest poi = Find(_state, poiId);
            return poi != null && poi.Resolved;
        }

        private void Reveal(string poiId)
        {
            PointOfInterest poi = Find(_state, poiId);
            if (poi == null) return;
            poi.Hidden = false;
            poi.Discovered = true;
            _log?.Push(CampaignEventKind.Discovery, $"Marked: {poi.DisplayName}",
                "The decanus points it out", _state.Clock.Now.DayNumber);
        }

        private static PointOfInterest Find(CampaignState state, string poiId)
        {
            if (state == null || string.IsNullOrEmpty(poiId)) return null;
            for (int i = 0; i < state.PointsOfInterest.Count; i++)
                if (state.PointsOfInterest[i].Id == poiId) return state.PointsOfInterest[i];
            return null;
        }
    }
}
