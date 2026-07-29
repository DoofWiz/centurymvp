namespace Century.Battle.Model
{
    /// <summary>Where a man is in the rhythm of a blow.</summary>
    public enum MeleeStance
    {
        /// <summary>Guarding, not committed to a strike.</summary>
        Idle = 0,
        /// <summary>Winding up — the weapon is drawn back (or the thrust is charging).</summary>
        Charging = 1,
        /// <summary>The blow is going in. This is the window a strike is resolved in.</summary>
        Striking = 2,
        /// <summary>Following through and recovering guard. Briefly exposed.</summary>
        Recovering = 3
    }

    /// <summary>A quick cut or a committed thrust. A thrust is slower but far more lethal.</summary>
    public enum AttackForm
    {
        Slash = 0,
        Thrust = 1
    }

    /// <summary>
    /// What a man fights with. Sword implies the Roman scutum-and-gladius drill (holds a line behind a
    /// shield); Spear implies the loose German fighter (no shield, longer reach, hit and run).
    /// </summary>
    public enum WeaponClass
    {
        Sword = 0,
        Spear = 1
    }

    /// <summary>
    /// The shape of a weapon's melee: reach, the bite of a cut versus a thrust, the timings of the
    /// swing, and how far its wielder likes to stand. Constants live here so the feel of each weapon is
    /// in one place; a few global knobs (stamina costs, damage scale) sit on <see cref="Sim.BattleSettings"/>.
    /// </summary>
    public readonly struct MeleeProfile
    {
        public readonly float Reach;
        public readonly float SlashDamage;
        public readonly float ThrustDamage;
        public readonly float ChargeSeconds;   // NPC wind-up before a strike; the player charges by feel
        public readonly float StrikeSeconds;
        public readonly float RecoverSeconds;
        public readonly float FrontalArcDot;   // how far off-centre a blow can still connect
        public readonly float Standoff;        // preferred distance from a foe (0 = close and hold)
        public readonly float AttackInterval;  // seconds between blows for an NPC

        // --- Shield (the board this weapon's wielder carries) ---------------------------------------
        public readonly float BlockArcDot;     // frontal cone the shield covers; a blow whose facing dot
                                               // exceeds this is turned aside. Higher = narrower cover.
        public readonly float BlockStaminaCost;// wind spent turning a blow aside
        public readonly float GuardFloor;      // below this stamina the guard breaks and a blow staggers through

        private MeleeProfile(float reach, float slash, float thrust, float charge, float strike,
            float recover, float arc, float standoff, float interval,
            float blockArc, float blockStamina, float guardFloor)
        {
            Reach = reach;
            SlashDamage = slash;
            ThrustDamage = thrust;
            ChargeSeconds = charge;
            StrikeSeconds = strike;
            RecoverSeconds = recover;
            FrontalArcDot = arc;
            Standoff = standoff;
            AttackInterval = interval;
            BlockArcDot = blockArc;
            BlockStaminaCost = blockStamina;
            GuardFloor = guardFloor;
        }

        public static MeleeProfile For(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Spear:
                    // Long reach; hacks at the line often and skips back out of range between blows.
                    // German board: a wide but tiring guard — covers more angle, costs more wind, and
                    // its holder breaks sooner than a drilled legionary.
                    // Damage is deliberately modest: a frontal fight is a grind of hacks and blocks,
                    // and the kills come from flanks, broken guards and spent lines — not a metronome.
                    return new MeleeProfile(
                        reach: 2.5f, slash: 0.11f, thrust: 0.26f, charge: 0.35f, strike: 0.2f,
                        recover: 0.75f, arc: 0.4f, standoff: 3.4f, interval: 1.9f,
                        blockArc: 0.25f, blockStamina: 0.11f, guardFloor: 0.28f);
                default:
                    // Gladius behind a scutum: holds guard, stabs only now and then when a gap opens.
                    // Roman board: a narrow but iron guard — a determined foe can step around it, but
                    // head-on it holds cheaply and almost never tires out.
                    return new MeleeProfile(
                        reach: 1.9f, slash: 0.12f, thrust: 0.26f, charge: 0.4f, strike: 0.22f,
                        recover: 0.45f, arc: 0.25f, standoff: 0f, interval: 3.2f,
                        blockArc: 0.45f, blockStamina: 0.05f, guardFloor: 0.10f);
            }
        }
    }
}
