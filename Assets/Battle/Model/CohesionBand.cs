namespace Century.Battle.Model
{
    /// <summary>
    /// A squad's willingness to keep fighting, on the Total War ladder. This, not health, is the
    /// primary fail state of a battle: a century is destroyed long before it is killed.
    /// </summary>
    public enum CohesionBand
    {
        /// <summary>Fled the line. Will not fight or take orders, and runs for the map edge.</summary>
        Broken = 0,
        /// <summary>About to go. Fights badly, will not advance. This is the warning state.</summary>
        Breaking = 1,
        /// <summary>Shaken but holding. Reduced effectiveness.</summary>
        Wavering = 2,
        /// <summary>Fighting as trained.</summary>
        Confident = 3,
        /// <summary>Winning and knows it — unshakable.</summary>
        Fearless = 4
    }

    public static class CohesionBandExtensions
    {
        public static CohesionBand FromValue(float morale01, bool isRouted)
        {
            if (isRouted) return CohesionBand.Broken;
            if (morale01 < 0.22f) return CohesionBand.Breaking;
            if (morale01 < 0.45f) return CohesionBand.Wavering;
            if (morale01 < 0.78f) return CohesionBand.Confident;
            return CohesionBand.Fearless;
        }

        /// <summary>Damage output multiplier. Frightened men fight badly.</summary>
        public static float CombatMultiplier(this CohesionBand band)
        {
            switch (band)
            {
                case CohesionBand.Broken: return 0f;
                case CohesionBand.Breaking: return 0.45f;
                case CohesionBand.Wavering: return 0.75f;
                case CohesionBand.Fearless: return 1.15f;
                default: return 1f;
            }
        }

        /// <summary>Whether the squad will still move forward on an Advance order.</summary>
        public static bool WillAdvance(this CohesionBand band) =>
            band != CohesionBand.Broken && band != CohesionBand.Breaking;
    }
}
