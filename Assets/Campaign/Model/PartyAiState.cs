namespace Century.Campaign.Model
{
    public enum PartyAiState
    {
        /// <summary>Halted, waiting for the next decision tick.</summary>
        Idle = 0,
        /// <summary>Marching to a self-chosen point.</summary>
        Wandering = 1,
        /// <summary>Actively closing on a detected hostile.</summary>
        Pursuing = 2,
        /// <summary>Running from a stronger hostile. Raiders do this the moment the odds turn.</summary>
        Fleeing = 3
    }

    /// <summary>What manner of force walks the overmap. Drives AI temperament and spawn sizing.</summary>
    public enum PartyKind
    {
        /// <summary>A tribal warband: middling strength, hunts what it can see.</summary>
        Warband = 0,
        /// <summary>Opportunists: small, quick, chase the weak and flee the strong.</summary>
        Raiders = 1,
        /// <summary>A mustered war host: big, slow, and afraid of nothing.</summary>
        WarHost = 2
    }
}
