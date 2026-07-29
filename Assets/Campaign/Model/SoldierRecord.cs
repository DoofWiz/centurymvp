using System;

namespace Century.Campaign.Model
{
    /// <summary>Veterancy tier, derived from accumulated experience.</summary>
    public enum VeterancyTier
    {
        /// <summary>Raw recruit.</summary>
        Tiro = 0,
        /// <summary>Trained soldier.</summary>
        Miles = 1,
        /// <summary>Seasoned. Has survived a campaign.</summary>
        Veteranus = 2,
        /// <summary>Recalled veteran. The steadiest men in the century.</summary>
        Evocatus = 3
    }

    /// <summary>
    /// A named man in the roster. Plain C# rather than a MonoBehaviour so that the roster survives
    /// scene unloads, serialises cleanly into a save, and can be unit tested without Unity.
    /// </summary>
    [Serializable]
    public sealed class SoldierRecord
    {
        public string Id;
        public string DisplayName;

        /// <summary>"centurion", "optio", "tesserarius", "medicus", "immunis", "legionary".</summary>
        public string RankId = "legionary";

        public string ArchetypeId = "legionary_heavy";

        public float Health01 = 1f;
        public float Stamina01 = 1f;
        public float Morale01 = 0.6f;

        /// <summary>How this man regards the centurion, 0..1. Drives his quips and, later, dissent.
        /// Distinct from morale: a man can be in good spirits yet resent your command, or the reverse.</summary>
        public float Loyalty01 = 0.6f;

        /// <summary>Which contubernium this man belongs to (0-based), or -1 if not yet assigned.
        /// The SAME grouping is shown at camp and fought in battle — one structure, one truth.
        /// Assigned by ContuberniumLedger; do not hand-set.</summary>
        public int Contubernium = -1;

        /// <summary>Appointed leader of his contubernium. At most one per squad, kept so by
        /// ContuberniumLedger. Unset means the senior man stands as acting Decanus.</summary>
        public bool IsDecanus;

        public int Kills;
        public int DayJoined;

        /// <summary>Accumulated experience. Drives veterancy and eligibility for promotion.</summary>
        public int Experience;

        /// <summary>Battles this man has come through alive.</summary>
        public int BattlesSurvived;

        public bool IsAlive => Health01 > 0f;

        /// <summary>Wounded men slow the party and are the reason medicine matters.</summary>
        public bool IsWounded => IsAlive && Health01 < 0.5f;

        /// <summary>Can this man fight? Used when building the battle roster.</summary>
        public bool IsCombatReady => IsAlive && Health01 >= 0.25f;

        public VeterancyTier Tier => VeterancyLadder.TierFor(Experience);

        /// <summary>
        /// Combat weighting from experience. Deliberately modest — a veteran is meaningfully better
        /// than a recruit but not twice the man, so numbers still matter more than quality.
        /// </summary>
        public float VeterancyMultiplier => VeterancyLadder.MultiplierFor(Tier);
    }

    /// <summary>Experience thresholds and their effects.</summary>
    public static class VeterancyLadder
    {
        public const int MilesThreshold = 120;
        public const int VeteranusThreshold = 380;
        public const int EvocatusThreshold = 850;

        public static VeterancyTier TierFor(int experience)
        {
            if (experience >= EvocatusThreshold) return VeterancyTier.Evocatus;
            if (experience >= VeteranusThreshold) return VeterancyTier.Veteranus;
            if (experience >= MilesThreshold) return VeterancyTier.Miles;
            return VeterancyTier.Tiro;
        }

        public static float MultiplierFor(VeterancyTier tier)
        {
            switch (tier)
            {
                case VeterancyTier.Miles: return 1.08f;
                case VeterancyTier.Veteranus: return 1.18f;
                case VeterancyTier.Evocatus: return 1.3f;
                default: return 1f;
            }
        }

        /// <summary>Experience needed to reach the next tier, or 0 when already at the top.</summary>
        public static int NextThreshold(VeterancyTier tier)
        {
            switch (tier)
            {
                case VeterancyTier.Tiro: return MilesThreshold;
                case VeterancyTier.Miles: return VeteranusThreshold;
                case VeterancyTier.Veteranus: return EvocatusThreshold;
                default: return 0;
            }
        }

        public static int PreviousThreshold(VeterancyTier tier)
        {
            switch (tier)
            {
                case VeterancyTier.Miles: return MilesThreshold;
                case VeterancyTier.Veteranus: return VeteranusThreshold;
                case VeterancyTier.Evocatus: return EvocatusThreshold;
                default: return 0;
            }
        }

        /// <summary>Progress through the current tier, 0..1. Full when at the top tier.</summary>
        public static float Progress01(int experience)
        {
            VeterancyTier tier = TierFor(experience);
            int next = NextThreshold(tier);
            if (next <= 0) return 1f;

            int previous = PreviousThreshold(tier);
            int span = next - previous;
            return span <= 0 ? 1f : (float)(experience - previous) / span;
        }
    }
}
