using System;
using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>One line of the event log, flattened for the save file.</summary>
    [Serializable]
    public sealed class SavedEvent
    {
        public int Kind;
        public string Headline;
        public string Detail;
        public int Day;
    }

    /// <summary>
    /// The save file's shape. A flat root over the real model objects: the parties, places,
    /// commander and offices are the campaign's own classes, which are already plain
    /// <c>[Serializable]</c> data, so nothing is copied field by field and nothing is forgotten
    /// when a model grows. Only <see cref="CampaignState"/> itself is unpacked, because its
    /// members are read-only properties and its clock is a service.
    /// </summary>
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int Version;
        public string SavedAtUtc;

        // --- The slot card -------------------------------------------------------------------
        public string Title;
        public string Detail;

        // --- CampaignState, unpacked ----------------------------------------------------------
        public double ClockMinutes;
        public int RandomSeed;
        public int CommandExperience;
        public int BattlesFought;
        public int NextEntityIndex;
        public bool SignumLost;
        public bool OvermapIntroSeen;
        public bool BattleIntroSeen;
        public bool OpenWorldSeeded;

        public List<PartyState> Parties = new List<PartyState>();
        public List<PointOfInterest> PointsOfInterest = new List<PointOfInterest>();
        public CommanderProgress Commander = new CommanderProgress();
        public PostRoster Posts = new PostRoster();
        public OnboardingState Onboarding = new OnboardingState();

        public List<SavedEvent> Events = new List<SavedEvent>();
    }

    /// <summary>
    /// Campaign state to JSON and back, through JsonUtility (the project carries no other JSON
    /// library, and the models were shaped for it: lists of structs, no dictionaries, public
    /// fields). Where a save is written is the App layer's business.
    /// </summary>
    public static class CampaignSerializer
    {
        public const int CurrentVersion = 1;

        public static string ToJson(CampaignState state, CampaignEventLog log, bool prettyPrint = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var data = new CampaignSaveData
            {
                Version = CurrentVersion,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Title = TitleFor(state),
                Detail = DetailFor(state),
                ClockMinutes = state.Clock.Now.TotalMinutes,
                RandomSeed = state.RandomSeed,
                CommandExperience = state.CommandExperience,
                BattlesFought = state.BattlesFought,
                NextEntityIndex = state.NextEntityIndex,
                SignumLost = state.SignumLost,
                OvermapIntroSeen = state.OvermapIntroSeen,
                BattleIntroSeen = state.BattleIntroSeen,
                OpenWorldSeeded = state.OpenWorldSeeded,
                Parties = state.Parties,
                PointsOfInterest = state.PointsOfInterest,
                Commander = state.Commander,
                Posts = state.Posts,
                Onboarding = state.Onboarding
            };

            if (log != null)
            {
                IReadOnlyList<CampaignEvent> events = log.Events;
                for (int i = 0; i < events.Count; i++)
                    data.Events.Add(new SavedEvent
                    {
                        Kind = (int)events[i].Kind,
                        Headline = events[i].Headline,
                        Detail = events[i].Detail,
                        Day = events[i].DayNumber
                    });
            }

            return JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>Reads the slot card without rebuilding a campaign. Null if unreadable.</summary>
        public static CampaignSaveData ReadHeader(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                CampaignSaveData data = JsonUtility.FromJson<CampaignSaveData>(json);
                return data != null && data.Version > 0 ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Unreadable save header: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Rebuilds a campaign from a save. Returns null when the JSON is not a save this build can
        /// read. The event log entries come back separately so the caller can push them into a
        /// fresh log.
        /// </summary>
        public static CampaignState FromJson(string json, out List<SavedEvent> events)
        {
            events = new List<SavedEvent>();
            CampaignSaveData data = ReadHeader(json);
            if (data == null) return null;

            var clock = new CampaignClock(new CampaignTime(data.ClockMinutes));
            var state = new CampaignState(clock)
            {
                RandomSeed = data.RandomSeed,
                CommandExperience = data.CommandExperience,
                BattlesFought = data.BattlesFought,
                SignumLost = data.SignumLost,
                OvermapIntroSeen = data.OvermapIntroSeen,
                BattleIntroSeen = data.BattleIntroSeen,
                OpenWorldSeeded = data.OpenWorldSeeded
            };

            for (int i = 0; i < data.Parties.Count; i++)
            {
                PartyState party = data.Parties[i];
                if (party == null || string.IsNullOrEmpty(party.Id)) continue;

                // A march order does not survive the file (nullable fields do not serialise);
                // the column stands where it was saved. Empty strings come back as null so the
                // "is anyone there" checks read as they did before the save.
                party.Destination = null;
                if (string.IsNullOrEmpty(party.PursuitTargetId)) party.PursuitTargetId = null;
                state.AddParty(party);
            }

            for (int i = 0; i < data.PointsOfInterest.Count; i++)
                if (data.PointsOfInterest[i] != null) state.PointsOfInterest.Add(data.PointsOfInterest[i]);

            state.Restore(data.Commander, data.Posts, data.Onboarding);
            state.NextEntityIndex = data.NextEntityIndex;

            if (data.Events != null) events = data.Events;

            if (state.PlayerParty == null)
            {
                Debug.LogWarning("[Save] The save holds no player party; it cannot be resumed.");
                return null;
            }

            return state;
        }

        public static string TitleFor(CampaignState state)
        {
            PartyState player = state.PlayerParty;
            string unit = player != null ? player.DisplayName : "Centuria";
            return $"{unit} · Day {state.Clock.Now.DayNumber}";
        }

        public static string DetailFor(CampaignState state)
        {
            PartyState player = state.PlayerParty;
            int men = player != null ? player.Roster.ActiveCount : 0;
            string chapter;
            switch (state.Onboarding.Chapter)
            {
                case OnboardingChapter.Opening: chapter = "The opening"; break;
                case OnboardingChapter.Aftermath: chapter = "The Aftermath"; break;
                case OnboardingChapter.OpenWorld: chapter = "The long road home"; break;
                default: chapter = "Sandbox"; break;
            }
            return $"{men} men · {chapter} · {state.BattlesFought} battles";
        }
    }
}
