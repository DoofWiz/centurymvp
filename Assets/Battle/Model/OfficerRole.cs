namespace Century.Battle.Model
{
    /// <summary>
    /// Command roles that carry a mechanical benefit in battle.
    /// </summary>
    /// <remarks>
    /// Historically accurate placements matter here: the optio stood at the rear of the formation
    /// specifically to stop men running, which is why his effect is cohesion rather than combat.
    /// </remarks>
    public enum OfficerRole
    {
        None = 0,
        /// <summary>The player. Commands in person; his presence steadies nearby squads.</summary>
        Centurion = 1,
        /// <summary>Rear-rank officer. Resists rout and holds the line together.</summary>
        Optio = 2,
        /// <summary>Carries the watchword. Orders reach his squad faster.</summary>
        Tesserarius = 3,
        /// <summary>Treats the wounded during and after the fight.</summary>
        Medicus = 4,
        /// <summary>Specialist ranker, exempt from fatigues. Steadier than a common legionary.</summary>
        Immunis = 5,
        /// <summary>Standard bearer. His banner steadies the men around it; losing it shakes the army.</summary>
        Signifer = 6
    }

    public static class OfficerRoleParser
    {
        /// <summary>Maps the campaign layer's rank string onto a battle role.</summary>
        public static OfficerRole FromRankId(string rankId)
        {
            switch (rankId)
            {
                case "centurion": return OfficerRole.Centurion;
                case "optio": return OfficerRole.Optio;
                case "tesserarius": return OfficerRole.Tesserarius;
                case "medicus": return OfficerRole.Medicus;
                case "signifer": return OfficerRole.Signifer;
                case "immunis": return OfficerRole.Immunis;
                default: return OfficerRole.None;
            }
        }
    }
}
