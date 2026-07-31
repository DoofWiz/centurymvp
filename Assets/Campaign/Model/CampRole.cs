namespace Century.Campaign.Model
{
    /// <summary>
    /// The senior appointments (Principalis) the player fills at camp. Distinct from a soldier's
    /// <see cref="SoldierRecord.RankId"/>: a role is a job the player hands out and can take back,
    /// where the rank is the man's standing in the century.
    /// </summary>
    public enum CampRole
    {
        /// <summary>Second in command. The steadying hand the whole century leans on.</summary>
        Optio = 0,
        /// <summary>Watch commander. A good one keeps the camp from being caught asleep.</summary>
        Tesserarius = 1,
        /// <summary>Lead scout. Ranges ahead from camp and brings back Opportunities.</summary>
        Speculator = 2,
        /// <summary>Standard bearer. His presence stiffens morale in the line.</summary>
        Signifer = 3,
        /// <summary>The bandage box. More men come back from the fields they fall on.</summary>
        Medicus = 4
    }

    /// <summary>
    /// Static, designer-facing description of each <see cref="CampRole"/>: what it is called, the
    /// one-line benefit shown in the UI, and who is eligible to hold it. Kept out of the enum so the
    /// model stays plain data and the copy lives in one place.
    /// </summary>
    public readonly struct CampRoleInfo
    {
        public readonly CampRole Role;
        public readonly string Title;
        public readonly string Latin;
        public readonly string Benefit;

        /// <summary>Minimum veterancy a man needs before he can be appointed. Tiro passes everything.</summary>
        public readonly VeterancyTier MinimumTier;

        private CampRoleInfo(CampRole role, string title, string latin, string benefit, VeterancyTier minimumTier)
        {
            Role = role;
            Title = title;
            Latin = latin;
            Benefit = benefit;
            MinimumTier = minimumTier;
        }

        public static CampRoleInfo For(CampRole role)
        {
            switch (role)
            {
                case CampRole.Optio:
                    return new CampRoleInfo(role, "Second", "Optio",
                        "Steadies the men — a little more morale recovered each rest.",
                        VeterancyTier.Veteranus);
                case CampRole.Tesserarius:
                    return new CampRoleInfo(role, "Watch Commander", "Tesserarius",
                        "Keeps the watch — fewer ambushes and lost supplies at camp.",
                        VeterancyTier.Miles);
                case CampRole.Speculator:
                    return new CampRoleInfo(role, "Lead Scout", "Speculator",
                        "Ranges ahead — send him out to uncover Opportunities.",
                        VeterancyTier.Miles);
                case CampRole.Signifer:
                    return new CampRoleInfo(role, "Standard Bearer", "Signifer",
                        "Carries the standard — bolsters morale through the night.",
                        VeterancyTier.Miles);
                case CampRole.Medicus:
                    return new CampRoleInfo(role, "Physician", "Medicus",
                        "Keeps the bandage box — more of the fallen found alive, faster healing.",
                        VeterancyTier.Miles);
                default:
                    return new CampRoleInfo(role, role.ToString(), role.ToString(), string.Empty,
                        VeterancyTier.Tiro);
            }
        }

        /// <summary>Every role, in display order. Allocates a small array; call once and cache.</summary>
        public static readonly CampRole[] All =
            { CampRole.Optio, CampRole.Tesserarius, CampRole.Speculator, CampRole.Signifer, CampRole.Medicus };

        /// <summary>Can this man hold this role? The centurion never steps down to a subordinate post.</summary>
        public bool IsEligible(SoldierRecord soldier)
        {
            if (soldier == null || !soldier.IsAlive) return false;
            if (soldier.RankId == "centurion") return false;
            return soldier.Tier >= MinimumTier;
        }
    }
}
