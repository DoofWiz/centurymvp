namespace Century.Campaign.Model
{
    /// <summary>
    /// Experience grade of a ranker. Distinct from <c>RankId</c>, which is an appointed post —
    /// a man becomes a veteranus by surviving, but he becomes an optio because the centurion says so.
    /// </summary>
    public enum SoldierGrade
    {
        /// <summary>Raw recruit. Fresh from the levy or a civilian picked up on the road.</summary>
        Tiro = 0,
        /// <summary>Trained soldier of the line.</summary>
        Miles = 1,
        /// <summary>Seasoned. Has stood in a shield wall and held.</summary>
        Veteranus = 2,
        /// <summary>Exempt from fatigues through proven skill.</summary>
        Immunis = 3,
        /// <summary>Recalled veteran of exceptional standing.</summary>
        Evocatus = 4
    }

    public static class SoldierGradeTable
    {
        /// <summary>Cumulative experience required to reach each grade.</summary>
        private static readonly int[] Thresholds = { 0, 60, 180, 420, 900 };

        public static SoldierGrade FromExperience(int experience)
        {
            for (int i = Thresholds.Length - 1; i >= 0; i--)
                if (experience >= Thresholds[i]) return (SoldierGrade)i;
            return SoldierGrade.Tiro;
        }

        public static int ThresholdFor(SoldierGrade grade)
        {
            int index = (int)grade;
            return index >= 0 && index < Thresholds.Length ? Thresholds[index] : 0;
        }

        /// <summary>Experience needed for the next grade, or -1 at the ceiling.</summary>
        public static int NextThreshold(SoldierGrade grade)
        {
            int next = (int)grade + 1;
            return next < Thresholds.Length ? Thresholds[next] : -1;
        }

        /// <summary>Progress toward the next grade, 0..1. Returns 1 at the ceiling.</summary>
        public static float Progress01(int experience)
        {
            SoldierGrade grade = FromExperience(experience);
            int floor = ThresholdFor(grade);
            int ceiling = NextThreshold(grade);

            if (ceiling < 0) return 1f;
            int span = ceiling - floor;
            return span <= 0 ? 1f : (float)(experience - floor) / span;
        }

        /// <summary>Combat effectiveness multiplier conferred by grade.</summary>
        public static float CombatMultiplier(SoldierGrade grade)
        {
            switch (grade)
            {
                case SoldierGrade.Tiro: return 0.82f;
                case SoldierGrade.Veteranus: return 1.12f;
                case SoldierGrade.Immunis: return 1.22f;
                case SoldierGrade.Evocatus: return 1.32f;
                default: return 1f;
            }
        }

        /// <summary>Resistance to losing heart, applied as a multiplier on morale loss.</summary>
        public static float SteadinessMultiplier(SoldierGrade grade)
        {
            switch (grade)
            {
                case SoldierGrade.Tiro: return 1.3f;
                case SoldierGrade.Veteranus: return 0.85f;
                case SoldierGrade.Immunis: return 0.75f;
                case SoldierGrade.Evocatus: return 0.65f;
                default: return 1f;
            }
        }
    }
}
