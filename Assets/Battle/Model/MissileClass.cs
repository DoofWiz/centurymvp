namespace Century.Battle.Model
{
    /// <summary>
    /// What a man throws before the lines meet. The class decides how hard a landed hit wounds and
    /// how hard a caught one lands on a blocking board — the board itself is a wall, not a
    /// percentage (see <see cref="Sim.MissileResolver"/>): anything arriving inside a raised
    /// shield's cover is stopped outright, whatever was thrown.
    /// </summary>
    public enum MissileClass
    {
        /// <summary>Nothing to throw. The scripted opening's enemies fight blade-only.</summary>
        None = 0,

        /// <summary>The legionary's pilum: heavy, flat, and lethal where it lands.</summary>
        Pila = 1,

        /// <summary>The warrior's framea, thrown: a shade lighter than a pilum.</summary>
        Javelins = 2,

        /// <summary>Stones off the field: the scavenger's arm. A bruise and an insult, not a spear.</summary>
        Rocks = 3
    }

    /// <summary>
    /// The shape of a missile class, relative to the pilum — <see cref="Sim.BattleSettings.PilaDamage"/>
    /// and <see cref="Sim.BattleSettings.PilaLaunchSpeed"/> stay the global knobs, and these scale them.
    /// </summary>
    public readonly struct MissileProfile
    {
        /// <summary>Multiplier on PilaDamage for a hit that actually reaches the body.</summary>
        public readonly float DamageScale;

        /// <summary>Multiplier on the blocking board's own BlockStaminaCost when caught on it.</summary>
        public readonly float BlockStaminaScale;

        /// <summary>Multiplier on PilaLaunchSpeed. A stone is lobbed, not cast.</summary>
        public readonly float LaunchSpeedScale;

        private MissileProfile(float damage, float blockStamina, float launchSpeed)
        {
            DamageScale = damage;
            BlockStaminaScale = blockStamina;
            LaunchSpeedScale = launchSpeed;
        }

        public static MissileProfile For(MissileClass missile)
        {
            switch (missile)
            {
                case MissileClass.Rocks:
                    // Very low damage even on a clean hit, and barely felt on a board: a rock-armed
                    // band harasses a shielded line, it does not shoot one down.
                    return new MissileProfile(damage: 0.16f, blockStamina: 0.5f, launchSpeed: 0.8f);
                case MissileClass.Javelins:
                    return new MissileProfile(damage: 0.85f, blockStamina: 1.2f, launchSpeed: 1f);
                case MissileClass.None:
                    return new MissileProfile(damage: 0f, blockStamina: 0f, launchSpeed: 1f);
                default:   // Pila
                    return new MissileProfile(damage: 1f, blockStamina: 1.5f, launchSpeed: 1f);
            }
        }
    }
}
