namespace Century.Campaign.Model
{
    /// <summary>
    /// Faction hostility. Trivial today, but isolated here because it will need to grow: tribes that
    /// turn hostile after a raid, neutral traders, allied auxiliaries.
    /// </summary>
    public static class PartyRelations
    {
        public static bool IsHostile(PartyState a, PartyState b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return false;
            if (a.Faction == PartyFaction.Neutral || b.Faction == PartyFaction.Neutral) return false;
            return a.Faction != b.Faction;
        }
    }
}
