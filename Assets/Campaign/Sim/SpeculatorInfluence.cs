using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>What kind of thing the speculator actually found. Rolled at scout time; the
    /// player only learns the truth on arrival — unless his intel is good enough to say.</summary>
    public enum OpportunityTier
    {
        /// <summary>The report was wrong: an empty steading, a cold camp. It happens.</summary>
        FalseLead = 0,
        /// <summary>Real, but poor. Barely worth the march.</summary>
        Meagre = 1,
        Solid = 2,
        Rich = 3,
        /// <summary>He found where the century's lost standard is held (army phase 3 tie-in).</summary>
        Signum = 4
    }

    /// <summary>
    /// The seam between the Speculator's OFFICE and the Opportunities system (army brief §9.6).
    /// The office's quality shapes what scouting brings back: how often the report is wrong, how
    /// rich the finds run, and how precisely the risk can be named. Everything the Opportunities
    /// code wants to know about the speculator comes through here and nowhere else.
    /// </summary>
    public interface ISpeculatorInfluence
    {
        /// <summary>The office's overall quality, 0..1: the man, and what the office has learned.</summary>
        float Quality01(CampaignState state);

        /// <summary>Rolls what a fresh ride actually turns up.</summary>
        OpportunityTier RollFind(CampaignState state);

        /// <summary>Whether the report can name the prize and count the spears, or only promise.</summary>
        bool ExactIntel(CampaignState state);
    }

    /// <summary>The seam's access point. Swap <see cref="Current"/> in tests.</summary>
    public static class SpeculatorInfluence
    {
        public static ISpeculatorInfluence Current = new OfficeSpeculatorInfluence();
    }

    /// <summary>The null object (brief: null-object first): scouting behaves exactly as it did
    /// before the seam existed — every find real and solid, every report vague.</summary>
    public sealed class NoSpeculatorInfluence : ISpeculatorInfluence
    {
        public float Quality01(CampaignState state) => 0.5f;
        public OpportunityTier RollFind(CampaignState state) => OpportunityTier.Solid;
        public bool ExactIntel(CampaignState state) => false;
    }

    /// <summary>
    /// The real influence: reads the establishment. Quality comes from the holder's veterancy and
    /// the office's accumulated experience (rides teach the OFFICE — a new man inherits the maps
    /// and the informants). Far Rider halves false leads on top: he checks before he reports.
    /// </summary>
    public sealed class OfficeSpeculatorInfluence : ISpeculatorInfluence
    {
        public float Quality01(CampaignState state)
        {
            Roster roster = state.PlayerParty?.Roster;
            if (roster == null) return 0f;

            SoldierRecord holder = state.Posts.HolderOf(PostId.Speculator, roster);
            if (holder == null) return 0f;

            PostRecord record = state.Posts.Find(PostId.Speculator);
            float experience = record != null ? record.PostExperience : 0f;

            return Mathf.Clamp01(0.2f + (int)holder.Tier * 0.12f + experience * 0.015f);
        }

        public OpportunityTier RollFind(CampaignState state)
        {
            // The lost signum comes first: while it is out there and no lead is already marked,
            // every ride is partly a search for it — and a better office finds it sooner.
            if (state.SignumLost && !SignumLeadOutstanding(state)
                && Random.value < 0.35f + Quality01(state) * 0.35f)
                return OpportunityTier.Signum;

            float quality = Quality01(state);

            float falseLead = Mathf.Lerp(0.30f, 0.05f, quality);
            if (PostTreeCatalog.Invested(state, "far_rider")) falseLead *= 0.5f;

            int scoutFire = state.PlayerParty != null
                ? state.PlayerParty.Facilities.LevelOf(CampStationId.ScoutFire) : 0;
            float rich = Mathf.Lerp(0.08f, 0.35f, quality) + scoutFire * 0.12f;
            float meagre = Mathf.Lerp(0.30f, 0.08f, quality);

            float roll = Random.value;
            if (roll < falseLead) return OpportunityTier.FalseLead;
            roll -= falseLead;
            if (roll < rich) return OpportunityTier.Rich;
            roll -= rich;
            if (roll < meagre) return OpportunityTier.Meagre;
            return OpportunityTier.Solid;
        }

        /// <summary>An office that has learned enough names the prize and counts the spears.
        /// Note the deliberate blind spot: a false lead reads EXACTLY like a solid find even
        /// with perfect intel — he cannot know the report was wrong until someone marches.</summary>
        public bool ExactIntel(CampaignState state) => Quality01(state) >= 0.55f;

        private static bool SignumLeadOutstanding(CampaignState state)
        {
            for (int i = 0; i < state.PointsOfInterest.Count; i++)
            {
                PointOfInterest poi = state.PointsOfInterest[i];
                if (!poi.Resolved && poi.EventId == "signum_held") return true;
            }
            return false;
        }
    }
}
