namespace Century.Core.Contracts
{
    /// <summary>
    /// The army layer's offices, flattened into the numbers battle actually reads (army brief
    /// §9.2). Battle knows nothing of posts or trees — the campaign resolves the establishment
    /// and its invested nodes into this, once, when the request is built. Named fields over a
    /// dictionary: debuggable, cheap to read per tick. Defaults are neutral; vacancies make
    /// them WORSE than neutral, which is how an empty office is felt.
    /// </summary>
    public sealed class PostEffectSet
    {
        /// <summary>Order delay multiplier. Tesserarius invested: below 1. Vacant: above 1.</summary>
        public float OrderPropagationMultiplier = 1f;

        /// <summary>Added to the cohesion level at which squads rout. Optio invested: negative
        /// (they hold longer). Optio vacant: positive (they break sooner).</summary>
        public float RoutThresholdShift;

        /// <summary>Multiplier on out-of-combat cohesion recovery (the Optio's hastile).</summary>
        public float CohesionRecoveryMultiplier = 1f;

        /// <summary>Squad cohesion cannot fall below this while the standard stands (signifer capstone).</summary>
        public float CohesionFloor;

        /// <summary>Multiplier on the standard-bearer's steadying radius and rate.</summary>
        public float StandardAuraMultiplier = 1f;

        /// <summary>Added to the wounded-instead-of-dead chance (medicus). Vacant: negative.</summary>
        public float WoundedOutBonus;

        /// <summary>Extra seconds on the Optio's succession window (his capstone).</summary>
        public float SuccessionWindowBonusSeconds;

        /// <summary>Deaths the medicus can negate outright this battle ("He Lives").</summary>
        public int DeathNegations;

        /// <summary>Multiplier on the field medicus' treatment speed ("Clean Hands").</summary>
        public float MedicusSpeedMultiplier = 1f;
    }
}
