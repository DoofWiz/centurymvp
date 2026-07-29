namespace Century.Core.Contracts
{
    /// <summary>
    /// The temperament of the man leading a hostile band, chosen when the band is spawned and carried
    /// into any battle it fights. It is the enemy commander's doctrine — how the warband closes,
    /// holds, or keeps its distance — and is what makes one fight feel different from the next.
    /// </summary>
    /// <remarks>
    /// Lives in Core.Contracts so both layers can name it without coupling: the campaign assigns it to
    /// a party at spawn, hands it across on the <see cref="BattleRequest"/>, and the battle assembly
    /// reads it to drive the enemy AI — neither side referencing the other.
    /// </remarks>
    public enum CommanderBehaviour
    {
        /// <summary>Advances in good order, holds a line, and looks to turn a flank. The default.</summary>
        Disciplined = 0,

        /// <summary>Charges headlong at the nearest Roman with no thought for tactics or losses.</summary>
        Fanatic = 1,

        /// <summary>Keeps its distance and harasses with thrown weapons, giving ground when pressed.</summary>
        Skirmisher = 2,

        /// <summary>Holds off and lets the Romans come, committing only when they overextend.</summary>
        Wary = 3
    }
}
