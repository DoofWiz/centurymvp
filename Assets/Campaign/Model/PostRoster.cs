using System;
using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>
    /// The century's establishment: its named offices (army brief §4). A post is an office, not a
    /// level — exactly one holder or vacant, and vacancy is visible and felt. Phase 1 of the army
    /// layer: the model and its seeding; trees, appointment flow and Consuetudo retention follow.
    /// </summary>
    public static class PostId
    {
        public const string Optio = "optio";
        public const string Signifer = "signifer";
        public const string Tesserarius = "tesserarius";
        public const string Medicus = "medicus";
        public const string Speculator = "speculator";

        /// <summary>Tier 1 establishment, in display order.</summary>
        public static readonly string[] Tier1 = { Optio, Signifer, Tesserarius, Medicus, Speculator };
    }

    [Serializable]
    public sealed class PostRecord
    {
        public string Post;
        /// <summary>Holder's soldier id, or null while vacant.</summary>
        public string HolderSoldierId;
        public int DayAppointed;

        /// <summary>Lifetime points the office has accrued — the office's standing, never spent down.</summary>
        public float PostExperience;

        /// <summary>Points still available to invest in the office's tree.</summary>
        public float UnspentPoints;

        /// <summary>Tree nodes the office holds. They belong to the OFFICE, not the man — the
        /// holder dying keeps the tradition, subject to Consuetudo retention at chapter turns.</summary>
        public List<string> InvestedNodeIds = new List<string>();

        /// <summary>Chapters the post has been continuously filled — feeds Consuetudo retention.</summary>
        public int ChaptersContinuouslyFilled;
    }

    /// <summary>
    /// Lives on <see cref="CampaignState"/>. Holders are validated against the living roster on
    /// read, so a death vacates the office the moment the campaign learns of it.
    /// </summary>
    [Serializable]
    public sealed class PostRoster
    {
        public List<PostRecord> Records = new List<PostRecord>();

        /// <summary>Seeds Tier 1 from the roster's existing ranks. The speculator deliberately
        /// starts VACANT: he is the cohort's man, and he finds the column later — the player
        /// learns his value by his absence (brief §10.10).</summary>
        public void EnsureSeeded(Roster roster, int day)
        {
            foreach (string post in PostId.Tier1)
            {
                if (Find(post) != null) continue;

                SoldierRecord holder = post == PostId.Speculator ? null : roster.FindByRank(post);
                Records.Add(new PostRecord
                {
                    Post = post,
                    HolderSoldierId = holder?.Id,
                    DayAppointed = day
                });
            }
        }

        public PostRecord Find(string post)
        {
            for (int i = 0; i < Records.Count; i++)
                if (Records[i].Post == post) return Records[i];
            return null;
        }

        /// <summary>The living holder, or null — a dead man's office is vacant whatever the record says.</summary>
        public SoldierRecord HolderOf(string post, Roster roster)
        {
            PostRecord record = Find(post);
            if (record?.HolderSoldierId == null) return null;

            SoldierRecord holder = roster.Find(record.HolderSoldierId);
            return holder != null && holder.IsAlive ? holder : null;
        }

        /// <summary>What the century loses while the office stands empty (brief §4.4). Phase 1
        /// names the penalty; later phases make each one bite mechanically.</summary>
        public static string VacancyPenalty(string post)
        {
            switch (post)
            {
                case PostId.Optio: return "No one holds the rear rank — squads break sooner";
                case PostId.Signifer: return "No standard to rally on; losses weigh heavier";
                case PostId.Tesserarius: return "Orders travel slowly; the night watch is thin";
                case PostId.Medicus: return "The wounded lie where they fall";
                case PostId.Speculator: return "The column marches blind — word arrives late, or not at all";
                default: return "The office stands empty";
            }
        }

        public static string DisplayName(string post)
        {
            switch (post)
            {
                case PostId.Optio: return "Optio";
                case PostId.Signifer: return "Signifer";
                case PostId.Tesserarius: return "Tesserarius";
                case PostId.Medicus: return "Medicus";
                case PostId.Speculator: return "Speculator";
                default: return post;
            }
        }

        /// <summary>One line of what the office is for, shown beside the holder.</summary>
        public static string Charge(string post)
        {
            switch (post)
            {
                case PostId.Optio: return "The man behind — discipline, and the line that does not break";
                case PostId.Signifer: return "The standard and the purse";
                case PostId.Tesserarius: return "The watchword — orders and the night";
                case PostId.Medicus: return "The bandage box";
                case PostId.Speculator: return "The man who is not there — what the column knows";
                default: return string.Empty;
            }
        }
    }
}
