using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// The single authority on how far the column can see on the overmap. Every consumer — the fog
    /// overlay, warband fading, POI reveal, hover information — asks here, so when sight range later
    /// grows factors (the Centurion's skills, the head scout's ability, weather, terrain, stealth),
    /// they are added in ONE place and the whole map obeys at once.
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>Fraction of the radius at which things become fully visible. Inside this, opaque;
        /// from here to the edge, a gradual reveal.</summary>
        private const float FullVisibilityFraction = 0.72f;

        /// <summary>Current sight radius in world units.</summary>
        public static float Radius(CampaignState state)
        {
            PartyState player = state?.PlayerParty;
            if (player == null) return 90f;

            // The party's detection radius (the designer knob that already reveals POIs and drives
            // the contact warning), multiplied by whatever the commander has learned. Weather,
            // night and terrain belong here too, when they come.
            float radius = player.DetectionRadius;
            if (state.Commander.Has("eyes_and_ears")) radius *= 1.12f;
            // Password: the tesserarius' watch chain passes word of everything it sees.
            if (PostTreeCatalog.Invested(state, "password")) radius *= 1.08f;
            // Far Rider: the speculator ranges the ridgelines ahead of the column.
            if (PostTreeCatalog.Invested(state, "far_rider")) radius *= 1.10f;
            return radius;
        }

        /// <summary>A stealthed party is seen at only this fraction of normal sight range — which is
        /// exactly what lets a stalking raider get close enough to open a surprise window.</summary>
        private const float StealthedSightFraction = 0.55f;

        /// <summary>
        /// How visible a world position is to the column: 1 inside the inner circle, fading to 0 at
        /// the edge of sight. This drives marker opacity and how much a hover reveals.
        /// </summary>
        public static float Visibility01(CampaignState state, Vector3 worldPosition) =>
            Visibility01(state, worldPosition, 1f);

        /// <summary>Party-aware visibility: a stealthed party is spotted far later — unless the
        /// speculator's capstone holds, and stealth hides nothing from the column.</summary>
        public static float Visibility01(CampaignState state, PartyState observed)
        {
            if (observed == null) return 0f;

            bool hidden = observed.IsStealthed
                && !PostTreeCatalog.Invested(state, "nothing_unseen");
            return Visibility01(state, observed.WorldPosition,
                hidden ? StealthedSightFraction : 1f);
        }

        private static float Visibility01(CampaignState state, Vector3 worldPosition, float radiusFraction)
        {
            PartyState player = state?.PlayerParty;
            if (player == null) return 1f;

            float radius = Radius(state) * radiusFraction;
            float full = radius * FullVisibilityFraction;

            Vector3 to = worldPosition - player.WorldPosition;
            to.y = 0f;
            float distance = to.magnitude;

            if (distance <= full) return 1f;
            if (distance >= radius) return 0f;
            return 1f - (distance - full) / (radius - full);
        }
    }
}
