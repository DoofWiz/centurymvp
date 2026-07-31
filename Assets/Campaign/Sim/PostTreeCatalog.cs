using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core.Contracts;

namespace Century.Campaign.Sim
{
    /// <summary>One node of an office's tree. Code-authored per project convention (the brief
    /// suggests ScriptableObjects; our catalogs live in code and the designer edits them here).</summary>
    public sealed class PostNodeDef
    {
        public string Id, Post, Name, Effect;
        public int Rank;          // 1..3, or 4 for the capstone
        public int Cost;
    }

    /// <summary>
    /// The Tier-1 office trees (army brief §5), phase 2 cut: every node listed is WIRED — either
    /// into the battle effect set or a campaign system. Vacancy penalties live in the resolver,
    /// so an empty office is strictly worse than a bare filled one.
    /// </summary>
    public static class PostTreeCatalog
    {
        public static readonly List<PostNodeDef> Nodes = new List<PostNodeDef>
        {
            new PostNodeDef{ Id="hastile", Post=PostId.Optio, Name="Hastile", Rank=1, Cost=2, Effect="Squads recover cohesion 25% faster out of the press" },
            new PostNodeDef{ Id="stand_fast", Post=PostId.Optio, Name="Stand Fast", Rank=2, Cost=3, Effect="Squads hold to a lower ebb before breaking" },
            new PostNodeDef{ Id="drill", Post=PostId.Optio, Name="Drill", Rank=3, Cost=3, Effect="Camp training earns half again the experience" },
            new PostNodeDef{ Id="centurio_in_waiting", Post=PostId.Optio, Name="Centurio in Waiting", Rank=4, Cost=5, Effect="The succession window runs 25 seconds longer" },

            new PostNodeDef{ Id="follow_the_signum", Post=PostId.Signifer, Name="Follow the Signum", Rank=1, Cost=2, Effect="The standard steadies further and harder" },
            new PostNodeDef{ Id="the_purse", Post=PostId.Signifer, Name="The Purse", Rank=2, Cost=3, Effect="A share of every field's coin is banked and kept" },
            new PostNodeDef{ Id="burial_club", Post=PostId.Signifer, Name="Burial Club", Rank=3, Cost=3, Effect="Proper rites soften the weight of the fallen" },
            new PostNodeDef{ Id="never_fallen", Post=PostId.Signifer, Name="It Has Never Fallen", Rank=4, Cost=5, Effect="While the standard stands, no squad's heart falls below its floor" },

            new PostNodeDef{ Id="passed_down", Post=PostId.Tesserarius, Name="Passed Down", Rank=1, Cost=2, Effect="Orders travel a quarter faster" },
            new PostNodeDef{ Id="night_watch", Post=PostId.Tesserarius, Name="Night Watch", Rank=2, Cost=3, Effect="Hostile parties find the camp harder to surprise" },
            new PostNodeDef{ Id="password", Post=PostId.Tesserarius, Name="Password", Rank=3, Cost=3, Effect="The column sees a little farther" },
            new PostNodeDef{ Id="no_surprises", Post=PostId.Tesserarius, Name="No Surprises", Rank=4, Cost=5, Effect="The century can never be caught without time to form" },

            new PostNodeDef{ Id="field_dressing", Post=PostId.Medicus, Name="Field Dressing", Rank=1, Cost=2, Effect="More of the fallen are found alive" },
            new PostNodeDef{ Id="convalescence", Post=PostId.Medicus, Name="Convalescence", Rank=2, Cost=3, Effect="The wounded mend a third faster in camp" },
            new PostNodeDef{ Id="clean_hands", Post=PostId.Medicus, Name="Clean Hands", Rank=3, Cost=3, Effect="The medicus works faster on the field" },
            new PostNodeDef{ Id="he_lives", Post=PostId.Medicus, Name="He Lives", Rank=4, Cost=5, Effect="Once each battle, a death is refused outright" },
        };

        public static IEnumerable<PostNodeDef> For(string post)
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Post == post) yield return Nodes[i];
        }

        public static bool Invested(CampaignState state, string nodeId)
        {
            for (int i = 0; i < state.Posts.Records.Count; i++)
                if (state.Posts.Records[i].InvestedNodeIds.Contains(nodeId)) return true;
            return false;
        }

        /// <summary>A node is buyable when the office is filled, points cover it, and the rank
        /// below it is already held (capstone needs all three ranks).</summary>
        public static bool CanInvest(CampaignState state, Roster roster, PostNodeDef node)
        {
            PostRecord record = state.Posts.Find(node.Post);
            if (record == null || state.Posts.HolderOf(node.Post, roster) == null) return false;
            if (record.InvestedNodeIds.Contains(node.Id)) return false;
            if (record.UnspentPoints < node.Cost) return false;

            int ranksHeld = 0;
            foreach (PostNodeDef other in For(node.Post))
                if (other.Rank < node.Rank && record.InvestedNodeIds.Contains(other.Id)) ranksHeld++;

            return node.Rank == 1 || (node.Rank == 4 ? ranksHeld >= 3 : ranksHeld >= node.Rank - 1);
        }

        /// <summary>Flattens the establishment into what battle reads. Call at request build.</summary>
        public static PostEffectSet Resolve(CampaignState state, Roster roster)
        {
            // Idempotent; guards the fresh campaign where no screen has seeded the roster yet.
            state.Posts.EnsureSeeded(roster, state.Clock.Now.DayNumber);

            var effects = new PostEffectSet();
            PostRoster posts = state.Posts;

            bool optio = posts.HolderOf(PostId.Optio, roster) != null;
            bool signifer = posts.HolderOf(PostId.Signifer, roster) != null;
            bool tesserarius = posts.HolderOf(PostId.Tesserarius, roster) != null;
            bool medicus = posts.HolderOf(PostId.Medicus, roster) != null;

            // Vacancies are FELT (brief §4.4).
            if (!optio) effects.RoutThresholdShift += 0.05f;
            if (!tesserarius) effects.OrderPropagationMultiplier *= 1.5f;
            if (!medicus) effects.WoundedOutBonus -= 0.15f;
            if (!signifer) effects.StandardAuraMultiplier *= 0.5f;

            if (Invested(state, "hastile")) effects.CohesionRecoveryMultiplier *= 1.25f;
            if (Invested(state, "stand_fast")) effects.RoutThresholdShift -= 0.03f;
            if (Invested(state, "centurio_in_waiting")) effects.SuccessionWindowBonusSeconds += 25f;
            if (Invested(state, "follow_the_signum")) effects.StandardAuraMultiplier *= 1.3f;
            if (Invested(state, "never_fallen")) effects.CohesionFloor = 0.12f;
            if (Invested(state, "passed_down")) effects.OrderPropagationMultiplier *= 0.75f;
            if (Invested(state, "field_dressing")) effects.WoundedOutBonus += 0.1f;
            if (Invested(state, "clean_hands")) effects.MedicusSpeedMultiplier *= 1.3f;
            if (Invested(state, "he_lives")) effects.DeathNegations += 1;

            return effects;
        }

        /// <summary>Post experience earned from a battle's shape — the office is used, it learns.</summary>
        public static void AwardBattlePoints(CampaignState state, Roster roster, bool victory, int wounded, bool anyRouted)
        {
            state.Posts.EnsureSeeded(roster, state.Clock.Now.DayNumber);

            Award(state, roster, PostId.Optio, 1f + (anyRouted ? 0f : 1f));
            Award(state, roster, PostId.Signifer, victory ? 2f : 1f);
            Award(state, roster, PostId.Tesserarius, 1.5f);
            Award(state, roster, PostId.Medicus, 1f + wounded * 0.5f);
        }

        private static void Award(CampaignState state, Roster roster, string post, float points)
        {
            if (state.Posts.HolderOf(post, roster) == null) return;   // an empty office learns nothing
            PostRecord record = state.Posts.Find(post);
            record.PostExperience += points;
            record.UnspentPoints += points;
        }
    }
}
