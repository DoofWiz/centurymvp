using System;
using System.Collections.Generic;
using Century.Core;

namespace Century.Campaign.Model
{
    /// <summary>
    /// The root of everything that persists across scene loads. If a piece of state is not reachable
    /// from here, it will not survive a trip into the battle scene and back — that is the rule the
    /// whole architecture hangs on.
    /// </summary>
    public sealed class CampaignState
    {
        public CampaignClock Clock { get; }

        public PartyState PlayerParty { get; set; }

        /// <summary>All parties including the player's.</summary>
        public List<PartyState> Parties { get; } = new List<PartyState>();

        /// <summary>Places worth stopping for on the overmap: shrines, camps, scouted Opportunities.</summary>
        public List<PointOfInterest> PointsOfInterest { get; } = new List<PointOfInterest>();

        /// <summary>The commander's evolving identity: owned doctrines and the hidden record of how
        /// they have led, which weights every future offer.</summary>
        public CommanderProgress Commander { get; } = new CommanderProgress();

        /// <summary>Master seed. Derived seeds are produced per encounter so fights are reproducible.</summary>
        public int RandomSeed;

        /// <summary>The player's own standing as a commander, earned across engagements.</summary>
        public int CommandExperience;

        /// <summary>Engagements fought. Shown on the post-battle summary.</summary>
        public int BattlesFought;

        /// <summary>Monotonic counter used to mint unique ids without a GUID dependency.</summary>
        private int _nextEntityIndex;

        public CampaignState(CampaignClock clock)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public string MintId(string prefix) => $"{prefix}_{_nextEntityIndex++:000}";

        public PartyState FindParty(string partyId)
        {
            for (int i = 0; i < Parties.Count; i++)
                if (Parties[i].Id == partyId) return Parties[i];
            return null;
        }

        public void AddParty(PartyState party)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            Parties.Add(party);
            if (party.IsPlayer) PlayerParty = party;
        }
    }
}
