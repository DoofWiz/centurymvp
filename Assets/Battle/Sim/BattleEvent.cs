namespace Century.Battle.Sim
{
    public enum BattleEventKind
    {
        Info = 0,
        /// <summary>A squad has entered Breaking. This is the "our men are breaking" moment.</summary>
        Warning = 1,
        /// <summary>A squad has routed, or an officer has fallen.</summary>
        Critical = 2,
        Good = 3
    }

    /// <summary>
    /// A line for the battle log. The simulation raises these rather than writing to a HUD directly,
    /// so the same events can later drive audio, the officer roster panel, or a replay.
    /// </summary>
    public readonly struct BattleEvent
    {
        public readonly BattleEventKind Kind;
        public readonly string Message;
        public readonly float AtSeconds;

        public BattleEvent(BattleEventKind kind, string message, float atSeconds)
        {
            Kind = kind;
            Message = message;
            AtSeconds = atSeconds;
        }
    }
}
