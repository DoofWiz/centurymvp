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

        /// <summary>The camp appointment that fills each office. The offices and the camp's
        /// appointment flow are ONE system; this is the join that keeps them agreeing.</summary>
        public static CampRole? RoleFor(string post)
        {
            switch (post)
            {
                case PostId.Optio: return CampRole.Optio;
                case PostId.Signifer: return CampRole.Signifer;
                case PostId.Tesserarius: return CampRole.Tesserarius;
                case PostId.Medicus: return CampRole.Medicus;
                case PostId.Speculator: return CampRole.Speculator;
                default: return null;
            }
        }

        public static string PostFor(CampRole role)
        {
            switch (role)
            {
                case CampRole.Optio: return PostId.Optio;
                case CampRole.Signifer: return PostId.Signifer;
                case CampRole.Tesserarius: return PostId.Tesserarius;
                case CampRole.Medicus: return PostId.Medicus;
                case CampRole.Speculator: return PostId.Speculator;
                default: return null;
            }
        }

        /// <summary>
        /// Seeds and SYNCS the establishment. The camp appointment is the authority on who holds
        /// an office; a man holding the matching rank is the fallback (and is written back into
        /// the appointment chart, so both surfaces always agree). Idempotent — call before any
        /// read. The speculator has no rank in the ladder, so he is filled by appointment only.
        /// </summary>
        public void EnsureSeeded(Roster roster, int day, CampAppointments appointments = null)
        {
            foreach (string post in PostId.Tier1)
            {
                SoldierRecord holder = ResolveHolder(post, roster, appointments);

                PostRecord record = Find(post);
                if (record == null)
                {
                    Records.Add(new PostRecord
                    {
                        Post = post,
                        HolderSoldierId = holder?.Id,
                        DayAppointed = day
                    });
                    continue;
                }

                if (record.HolderSoldierId != holder?.Id)
                {
                    record.HolderSoldierId = holder?.Id;
                    record.DayAppointed = day;
                }
            }
        }

        private static SoldierRecord ResolveHolder(string post, Roster roster, CampAppointments appointments)
        {
            CampRole? role = RoleFor(post);

            if (appointments != null && role.HasValue)
            {
                string id = appointments.GetHolderId(role.Value);
                SoldierRecord appointed = id == null ? null : roster.Find(id);
                if (appointed != null && appointed.IsAlive) return appointed;
            }

            // Rank fallback: promotion raised a man to the rank but nobody was appointed. He is
            // written into the chart too — unless he already holds a different office, because
            // one man holds one job.
            SoldierRecord ranked = roster.FindByRank(post);
            if (ranked == null) return null;

            if (appointments != null && role.HasValue)
            {
                if (appointments.HoldsAnyRole(ranked.Id, out CampRole other) && other != role.Value)
                    return null;
                appointments.Assign(role.Value, ranked.Id);
            }

            return ranked;
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

        /// <summary>One line of what the office DOES, shown beside the holder. Mechanics in plain
        /// words, not flavour — the player reads this to decide who to appoint and what to invest.</summary>
        public static string Charge(string post)
        {
            switch (post)
            {
                case PostId.Optio:
                    return "Squads break later and steady faster; he takes command if you fall in battle";
                case PostId.Signifer:
                    return "Carries the signum in battle — squads near it hold their nerve";
                case PostId.Tesserarius:
                    return "Your orders reach the squads faster; the camp is harder to surprise";
                case PostId.Medicus:
                    return "More of the fallen are found alive as wounded, and the wounded heal faster";
                case PostId.Speculator:
                    return "Rides out from camp to mark prizes on the map — a better office finds richer prizes and fewer false leads";
                default: return string.Empty;
            }
        }
    }
}
