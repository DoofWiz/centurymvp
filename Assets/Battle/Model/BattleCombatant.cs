using UnityEngine;

namespace Century.Battle.Model
{
    /// <summary>
    /// One man in the fight. Created from a <see cref="Core.Contracts.CombatantSpec"/> and flattened
    /// back into a <see cref="Core.Contracts.CombatantOutcome"/> when the battle ends.
    /// </summary>
    /// <remarks>
    /// The identity that matters is <see cref="SoldierId"/>: it is what lets a man who dies here be
    /// the same man who is missing from the roster on the overmap afterwards.
    /// </remarks>
    public sealed class BattleCombatant
    {
        public string SoldierId;
        public string DisplayName;
        public string ArchetypeId;
        public OfficerRole Role;

        public float Health01 = 1f;
        public float Stamina01 = 1f;
        public float Morale01 = 0.6f;

        public bool IsPlayerControlled;
        public bool IsPlayerSide;

        /// <summary>Squad this man belongs to, and his position within its formation.</summary>
        public int SquadIndex = -1;
        public int SlotIndex = -1;

        /// <summary>Persistent campaign squad (contubernium) carried in on the spec, or -1.</summary>
        public int GroupIndex = -1;

        /// <summary>Appointed Decanus of his group; leads from the front-centre slot.</summary>
        public bool IsGroupLeader;

        /// <summary>Multiplier on this man's melee damage (veterancy under the right doctrine).</summary>
        public float DamageMultiplier = 1f;

        public int Kills;

        /// <summary>Live world position, written by the view each frame.</summary>
        public Vector3 WorldPosition;

        /// <summary>Direction the man is facing. Needed because a blow from behind is worse.</summary>
        public Vector3 Facing = Vector3.forward;

        // --- Loadout ---------------------------------------------------------------------------

        /// <summary>What he fights with, and whether he carries a shield. Set by the battle factory.</summary>
        public WeaponClass Weapon = WeaponClass.Sword;
        public bool HasShield = true;

        /// <summary>How much wind he has; bounds his stamina bar and its refill. Set by the factory.</summary>
        public StaminaClass Stamina = StaminaClass.Fresh;

        /// <summary>Pila (or framea) left on his back. Seeded by the factory; spent by BattleMissiles.</summary>
        public int PilaRemaining;

        /// <summary>Seconds until this man may throw again, while his squad skirmishes.</summary>
        public float MissileCooldown;

        // --- Melee -----------------------------------------------------------------------------

        /// <summary>Man currently being fought, or null. Set by the melee system, not the view.</summary>
        public BattleCombatant Target;

        /// <summary>Seconds until this man can strike again.</summary>
        public float AttackCooldown;

        /// <summary>Where he is in the rhythm of a blow, and which kind of blow it is.</summary>
        public MeleeStance Stance = MeleeStance.Idle;
        public AttackForm Form = AttackForm.Slash;

        /// <summary>Time left in the current stance.</summary>
        public float StanceTimer;

        /// <summary>Charge behind a thrust, 0..1. A fully charged thrust is lethal.</summary>
        public float Charge01;

        /// <summary>True while the shield is up in front. Blocks frontal blows; bleeds stamina.</summary>
        public bool ShieldRaised;

        /// <summary>Set once a strike has landed or been blocked, so it resolves only once.</summary>
        public bool StrikeResolved;

        /// <summary>Direction the blow is committed along, locked when the strike begins.</summary>
        public Vector3 StrikeDir = Vector3.forward;

        /// <summary>Distance the man's footwork wants to keep from his target. The view honours it.</summary>
        public float DesiredRange;

        /// <summary>Seconds a man is reeling after a heavy blow broke his tired guard. While it lasts he
        /// cannot strike and his shield is forced down — the beat in which a shield wall cracks.</summary>
        public float StaggerTimer;

        /// <summary>True while winding up or striking — when a shield gives no cover.</summary>
        public bool IsAttacking => Stance == MeleeStance.Charging || Stance == MeleeStance.Striking;

        /// <summary>True while reeling from a broken guard.</summary>
        public bool IsStaggered => StaggerTimer > 0f;

        /// <summary>Set for one tick after taking a wound, so the view can react.</summary>
        public bool WasHitThisTick;

        /// <summary>Set for one tick when his shield turned a blow aside — the view puffs dust off
        /// the board, which is how a holding wall reads at a glance.</summary>
        public bool WasBlockThisTick;

        /// <summary>Set for one tick when a blow smashed his tired guard open (the stagger).</summary>
        public bool WasGuardBreakThisTick;

        /// <summary>Cut down but not killed: he is out of this battle exactly like a dead man, and
        /// found alive among the fallen when the field is cleared afterwards.</summary>
        public bool IsWoundedOut;

        /// <summary>Seconds since this man last struck or was struck. Drives "in combat" checks.</summary>
        public float TimeSinceCombat = 999f;

        public bool IsAlive => Health01 > 0f;
        public bool IsOfficer => Role != OfficerRole.None && Role != OfficerRole.Immunis;
        public bool IsEngaged => Target != null && Target.IsAlive;
        public bool IsInCombat => TimeSinceCombat < 2f;
    }
}
