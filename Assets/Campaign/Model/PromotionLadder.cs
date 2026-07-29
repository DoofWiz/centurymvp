using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>A man raised to a new post.</summary>
    public readonly struct Promotion
    {
        public readonly string SoldierId;
        public readonly string DisplayName;
        public readonly string FromRankId;
        public readonly string ToRankId;

        public Promotion(string soldierId, string displayName, string fromRankId, string toRankId)
        {
            SoldierId = soldierId;
            DisplayName = displayName;
            FromRankId = fromRankId;
            ToRankId = toRankId;
        }
    }

    /// <summary>
    /// Fills vacant command posts from the ranks.
    /// </summary>
    /// <remarks>
    /// A century holds exactly one optio, one tesserarius and one medicus. Promotion is therefore
    /// gated on a post falling vacant, not on reaching an experience threshold — which is both
    /// historically right and better play, because it means losing your optio is a real event with a
    /// visible consequence rather than a stat adjustment.
    ///
    /// The immunis post is the exception: it is a specialist grade rather than a command appointment,
    /// so several men can hold it.
    /// </remarks>
    public static class PromotionLadder
    {
        /// <summary>Single-holder posts, in order of seniority.</summary>
        private static readonly string[] UniquePosts = { "optio", "tesserarius", "medicus" };

        /// <summary>Minimum experience to be considered for each post.</summary>
        private const int OptioRequirement = 300;
        private const int TesserariusRequirement = 180;
        private const int MedicusRequirement = 140;
        private const int ImmunisRequirement = 120;

        /// <summary>Number of immunes a century may carry, as a fraction of strength.</summary>
        private const float ImmunisShare = 0.12f;

        public static int RequirementFor(string rankId)
        {
            switch (rankId)
            {
                case "optio": return OptioRequirement;
                case "tesserarius": return TesserariusRequirement;
                case "medicus": return MedicusRequirement;
                case "immunis": return ImmunisRequirement;
                default: return 0;
            }
        }

        /// <summary>
        /// Fills every vacant post it can and returns what changed. Called after a battle, once the
        /// dead have been removed from the roster.
        /// </summary>
        public static List<Promotion> Resolve(Roster roster)
        {
            var promotions = new List<Promotion>();
            if (roster == null) return promotions;

            for (int i = 0; i < UniquePosts.Length; i++)
            {
                string post = UniquePosts[i];
                if (roster.FindByRank(post) != null) continue;

                SoldierRecord candidate = MostExperiencedRanker(roster, RequirementFor(post));
                if (candidate == null) continue;

                promotions.Add(new Promotion(candidate.Id, candidate.DisplayName, candidate.RankId, post));
                candidate.RankId = post;

                // The medicus is a support role, so his battle archetype changes with the post.
                if (post == "medicus") candidate.ArchetypeId = "legionary_support";
            }

            promotions.AddRange(FillImmunes(roster));
            return promotions;
        }

        /// <summary>Specialist grade, awarded up to a share of the century's strength.</summary>
        private static List<Promotion> FillImmunes(Roster roster)
        {
            var promotions = new List<Promotion>();

            int active = roster.ActiveCount;
            int allowed = (int)(active * ImmunisShare);
            if (allowed <= 0) return promotions;

            int current = 0;
            for (int i = 0; i < roster.Soldiers.Count; i++)
                if (roster.Soldiers[i].IsAlive && roster.Soldiers[i].RankId == "immunis") current++;

            int vacancies = allowed - current;
            if (vacancies <= 0) return promotions;

            for (int v = 0; v < vacancies; v++)
            {
                SoldierRecord candidate = MostExperiencedRanker(roster, ImmunisRequirement);
                if (candidate == null) break;

                promotions.Add(new Promotion(candidate.Id, candidate.DisplayName, candidate.RankId, "immunis"));
                candidate.RankId = "immunis";
            }

            return promotions;
        }

        /// <summary>Most experienced plain legionary who clears the requirement.</summary>
        private static SoldierRecord MostExperiencedRanker(Roster roster, int minimumExperience)
        {
            SoldierRecord best = null;

            for (int i = 0; i < roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = roster.Soldiers[i];
                if (!soldier.IsAlive || soldier.RankId != "legionary") continue;
                if (soldier.Experience < minimumExperience) continue;
                if (best != null && soldier.Experience <= best.Experience) continue;

                best = soldier;
            }

            return best;
        }
    }
}
