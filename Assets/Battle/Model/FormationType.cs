namespace Century.Battle.Model
{
    /// <summary>
    /// How a squad arranges itself in space. Orthogonal to <see cref="SquadOrder"/>: a squad can
    /// advance in testudo, or hold position in loose order.
    /// </summary>
    public enum FormationType
    {
        /// <summary>Standard fighting line, shields overlapping, five wide.</summary>
        Line = 0,
        /// <summary>Shields locked overhead and to the flanks. Slow, dense, missile-proof.</summary>
        Testudo = 1,
        /// <summary>Point forward. For breaking through a line rather than holding one. No longer
        /// offered to the player (Double Line took its menu slot); warband AIs still charge in it.</summary>
        Wedge = 2,
        /// <summary>Scattered. For broken ground, missiles, and pursuit.</summary>
        Loose = 3,
        /// <summary>Two abreast. For marching down a track, not for fighting.</summary>
        Column = 4,
        /// <summary>
        /// The duplex acies: ordered as a GROUP, squads dress into two mutually supporting ranks —
        /// a front line of squads with a second line covering its gaps. One squad alone fights it
        /// as an ordinary line; the shape only exists between squads (see GroupFormation).
        /// </summary>
        DoubleLine = 5
    }

    public static class FormationTypeExtensions
    {
        /// <summary>
        /// Tight formations must disable agent avoidance or the men shove each other out of rank.
        /// This is the single most important number in the formation system.
        /// </summary>
        public static bool IsTight(this FormationType formation) =>
            formation == FormationType.Testudo || formation == FormationType.Line
            || formation == FormationType.Wedge || formation == FormationType.DoubleLine;

        /// <summary>Movement speed multiplier. A testudo is slow because it must be.</summary>
        public static float SpeedMultiplier(this FormationType formation)
        {
            switch (formation)
            {
                case FormationType.Testudo: return 0.45f;
                case FormationType.Line: return 0.8f;
                case FormationType.DoubleLine: return 0.8f;
                case FormationType.Wedge: return 0.9f;
                case FormationType.Column: return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>
        /// How far a man may leave his slot to reach an enemy. This is the aggression setting: a
        /// testudo will not break ranks to chase, a skirmish line will. Choosing a formation is
        /// therefore also choosing how much autonomy the squad has.
        /// </summary>
        public static float EngagementLeash(this FormationType formation)
        {
            switch (formation)
            {
                case FormationType.Testudo: return 0.4f;
                case FormationType.Line: return 1.5f;
                case FormationType.DoubleLine: return 1.5f;
                case FormationType.Wedge: return 2.2f;
                case FormationType.Loose: return 6f;
                case FormationType.Column: return 0f;   // marching, not fighting
                default: return 1.5f;
            }
        }

        /// <summary>
        /// Chance a frontal blow is turned aside by the shield wall, before facing and stamina scale
        /// it. This is what makes a Roman line hard to *hit*, not merely hard to hurt: a scutum stops
        /// the blow landing at all. Worthless from the flank, like the damage reduction it sits beside.
        /// </summary>
        public static float FrontalBlockChance(this FormationType formation)
        {
            switch (formation)
            {
                case FormationType.Testudo: return 0.60f;
                case FormationType.Line: return 0.40f;
                case FormationType.DoubleLine: return 0.40f;
                case FormationType.Wedge: return 0.28f;
                case FormationType.Loose: return 0.10f;
                default: return 0.15f;
            }
        }

        /// <summary>Display name — enum ToString would render "DOUBLELINE" on the HUD.</summary>
        public static string Word(this FormationType formation) =>
            formation == FormationType.DoubleLine ? "DOUBLE LINE" : formation.ToString().ToUpperInvariant();
    }
}
