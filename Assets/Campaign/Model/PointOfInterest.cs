using System;
using UnityEngine;

namespace Century.Campaign.Model
{
    /// <summary>The flavour of a point of interest. Drives its marker colour and its framing.</summary>
    public enum PoiKind
    {
        Settlement = 0,
        Grove = 1,
        Ruin = 2,
        Refugees = 3,
        RaiderCamp = 4,
        Battlefield = 5,
        /// <summary>A dynamic prize revealed by the Speculator's scouting. Richer, and one-off.</summary>
        Opportunity = 6,

        // --- The Aftermath (guided start) ---------------------------------------------------
        /// <summary>A field of the massacre's dead. Survivors hide among them.</summary>
        Corpses = 7,
        /// <summary>A pack of hounds worrying the bodies.</summary>
        Hounds = 8,
        /// <summary>Looters at a fire, with chained Romans.</summary>
        Looters = 9,
        /// <summary>Ground fit to camp on.</summary>
        SafePlace = 10,
        /// <summary>The ravine out of the Aftermath: the way into the open world.</summary>
        Passage = 11
    }

    /// <summary>
    /// A place on the overmap worth stopping for: a shrine, a grove, a raider camp, or an Opportunity
    /// the Speculator turned up. Pure state — the marker that shows it and the event that plays out
    /// when the column arrives both live elsewhere and are keyed off this by <see cref="EventId"/>.
    /// </summary>
    [Serializable]
    public sealed class PointOfInterest
    {
        public string Id;
        public PoiKind Kind;
        public string DisplayName;

        /// <summary>Key into the event catalog. The narrative that plays when the column arrives.</summary>
        public string EventId;

        public Vector3 WorldPosition;

        /// <summary>Revealed once the column comes within detection range. Marker shows only then.</summary>
        public bool Discovered;

        /// <summary>Set once the player has dealt with it. Resolved POIs keep no marker and never re-fire.</summary>
        public bool Resolved;

        /// <summary>Not in the world yet: never sighted, never triggered, no marker. The onboarding
        /// director lifts this when the story reaches it (the looters' camp waits on the first two
        /// battle sites). Distinct from Discovered so a hidden place can be authored in advance.</summary>
        public bool Hidden;
    }
}
