using Century.Battle.Model;

namespace Century.Battle.Sim
{
    /// <summary>
    /// How far a man may stray from his formation slot to reach a foe, as a function of his squad's
    /// order and formation. This is the single lever that turns "hold the line" into a line that
    /// actually holds: targeting and movement both measure a man's leash from his <em>slot</em>, not
    /// from wherever the chase has dragged him, so a squad that is told to hold cannot be walked off
    /// the field one lunge at a time.
    /// </summary>
    public static class SquadTactics
    {
        /// <summary>Extra distance beyond weapon reach a man may leave his slot to fight, per order.</summary>
        public static float AggressionLeash(BattleSquad squad)
        {
            if (squad == null) return 1f;

            switch (squad.Order)
            {
                // Loose and independent: a skirmisher roams as far as his open order allows.
                case SquadOrder.Skirmish: return squad.Formation.EngagementLeash();

                // Running for the edge; do not stop to fight.
                case SquadOrder.Retreat: return 0f;

                // A fighting withdrawal: cut down only what is already on top of you.
                case SquadOrder.Fallback: return 0.35f;

                // Root to the spot. Strike what comes to the line; never step out after it.
                case SquadOrder.HoldPosition: return 0.6f * Tightness(squad.Formation);

                // Advancing into contact: lean into the enemy line but keep the shape.
                case SquadOrder.Advance: return 1.2f * Tightness(squad.Formation);

                // Screening the Centurion: engage nearby threats without abandoning his flank.
                case SquadOrder.FollowMe: return 1.0f * Tightness(squad.Formation);

                default: return 1.0f;
            }
        }

        /// <summary>A tight formation keeps a shorter leash; an open one a longer one.</summary>
        private static float Tightness(FormationType formation)
        {
            switch (formation)
            {
                case FormationType.Testudo: return 0.5f;
                case FormationType.Wedge: return 1.2f;
                case FormationType.Loose: return 2.5f;
                default: return 1f;
            }
        }
    }
}
