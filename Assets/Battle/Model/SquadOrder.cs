namespace Century.Battle.Model
{
    /// <summary>
    /// What a squad is trying to do. Orthogonal to <see cref="FormationType"/>.
    /// </summary>
    public enum SquadOrder
    {
        /// <summary>Stay in station on the Centurion. The default.</summary>
        FollowMe = 0,
        /// <summary>Move to the ordered point and hold there.</summary>
        Advance = 1,
        /// <summary>Root to the spot where the order was given.</summary>
        HoldPosition = 2,
        /// <summary>Spread out and act independently. Autonomy is highest here.</summary>
        Skirmish = 3,
        /// <summary>Withdraw away from the enemy in formation, shields still to the front.</summary>
        Fallback = 4,
        /// <summary>Break ranks and run clear of the battlefield entirely. The squad leaves the fight.</summary>
        Retreat = 5
    }
}
