using System;

namespace Century.Campaign.Model
{
    /// <summary>
    /// One man's standing with one other man. Century is a personal story: seventy men in hostile
    /// country, and what each thinks of the others — and of you — is load-bearing. Ties are
    /// directional (he may love a man who despises him) and carry the last reason they moved,
    /// so the UI can say WHY, not just how much.
    /// </summary>
    [Serializable]
    public sealed class RelationTie
    {
        public string OtherId;

        /// <summary>-1 (blood enemy) .. +1 (brother in arms). 0 is indifference.</summary>
        public float Value;

        /// <summary>The last significant thing that moved this tie, in plain words.</summary>
        public string Why;
    }
}
