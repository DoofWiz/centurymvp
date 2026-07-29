namespace Century.Battle.Model
{
    /// <summary>
    /// How much wind a man has in him. Set from his campaign stamina when a battle is built: a man
    /// force-marched all day comes to the field already tired, and tires further as he fights.
    /// Bounds the top of his stamina bar and how fast it refills. The player is unclassed (always fresh).
    /// </summary>
    public enum StaminaClass
    {
        Fresh = 0,
        Steady = 1,
        Winded = 2,
        Tired = 3,
        Exhausted = 4
    }

    public readonly struct StaminaProfile
    {
        public readonly float MaxStamina;
        public readonly float RecoveryMultiplier;

        /// <summary>Multiplier on movement speed. Below one, a worn man visibly drags.</summary>
        public readonly float MoveMultiplier;

        /// <summary>
        /// Multiplier on the TIMINGS of a blow — wind-up, recovery, the pause between strikes, and how
        /// long a broken guard staggers. Above one means slower: a tired man swings less often and is
        /// slower to gather himself. Fresh through Winded fight at full tempo; Tired and Exhausted drag.
        /// </summary>
        public readonly float ActionMultiplier;

        private StaminaProfile(float max, float recovery, float move, float action)
        {
            MaxStamina = max;
            RecoveryMultiplier = recovery;
            MoveMultiplier = move;
            ActionMultiplier = action;
        }

        public static StaminaProfile For(StaminaClass staminaClass)
        {
            switch (staminaClass)
            {
                case StaminaClass.Steady: return new StaminaProfile(0.90f, 1.0f, 1f, 1f);
                case StaminaClass.Winded: return new StaminaProfile(0.72f, 0.75f, 1f, 1f);
                case StaminaClass.Tired: return new StaminaProfile(0.52f, 0.55f, 0.88f, 1.2f);
                case StaminaClass.Exhausted: return new StaminaProfile(0.34f, 0.38f, 0.75f, 1.4f);
                default: return new StaminaProfile(1.0f, 1.2f, 1f, 1f); // Fresh
            }
        }

        /// <summary>Reads a man's condition off his campaign stamina as he arrives on the field.</summary>
        public static StaminaClass FromStamina(float stamina01)
        {
            if (stamina01 >= 0.85f) return StaminaClass.Fresh;
            if (stamina01 >= 0.60f) return StaminaClass.Steady;
            if (stamina01 >= 0.40f) return StaminaClass.Winded;
            if (stamina01 >= 0.20f) return StaminaClass.Tired;
            return StaminaClass.Exhausted;
        }
    }
}
