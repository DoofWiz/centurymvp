using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Tuning for the battle layer. Create via Assets > Create > Century > Battle Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Century/Battle Settings", fileName = "BattleSettings")]
    public sealed class BattleSettings : ScriptableObject
    {
        [Header("Squads")]
        [Tooltip("Maximum men in a contubernium.")]
        [Range(4, 12)] public int SquadSize = 10;

        [Tooltip("UNUSED since the reinforcements rework: the player chooses per squad on the " +
                 "deployment screen; nothing is held back by force.")]
        public int DefaultDeploymentCount = 40;

        [Tooltip("Half-size of the battlefield in metres. Reinforcements enter at its edges, and the " +
                 "boundary is drawn so the field visibly ends.")]
        public float FieldHalfExtent = 70f;

        [Header("Formation spacing (metres)")]
        public Vector2 LineSpacing = new Vector2(1.4f, 1.3f);
        public Vector2 TestudoSpacing = new Vector2(0.95f, 0.95f);
        public Vector2 WedgeSpacing = new Vector2(1.5f, 1.35f);
        public Vector2 LooseSpacing = new Vector2(2.6f, 2.4f);
        public Vector2 ColumnSpacing = new Vector2(1.3f, 1.2f);

        [Tooltip("Men per rank in a fighting line.")]
        [Range(2, 10)] public int LineWidth = 5;

        [Header("Movement")]
        public float SoldierBaseSpeed = 4.5f;
        public float PlayerSpeed = 5.2f;
        public float PlayerTurnSpeed = 900f;
        public float SoldierTurnSpeed = 540f;

        [Tooltip("How far a man may drift from his slot before he is considered out of station.")]
        public float SlotTolerance = 0.35f;

        [Tooltip("Distance a following squad keeps behind the Centurion.")]
        public float FollowDistance = 6f;

        [Header("Order propagation")]
        [Tooltip("Base seconds before a squad acts on an order.")]
        public float OrderBaseDelaySeconds = 0.35f;

        [Tooltip("Extra seconds per metre between the Centurion and the squad.")]
        public float OrderDelayPerMetre = 0.035f;

        [Tooltip("Multiplier applied when the squad has a tesserarius. He carries the watchword.")]
        [Range(0.2f, 1f)] public float TesserariusDelayMultiplier = 0.55f;

        public float MaxOrderDelaySeconds = 4f;

        [Header("Deployment")]
        [Tooltip("Distance between the two lines at the start of a stand-up fight.")]
        public float DeploymentSeparation = 55f;

        [Tooltip("Separation when the player is ambushed. Much closer, and no time to form.")]
        public float AmbushSeparation = 22f;

        public float SquadFrontage = 9f;

        // =====================================================================================
        //  Step 3b
        // =====================================================================================

        [Header("Melee — reach")]
        [Tooltip("Distance at which a man can strike. Gladius reach plus a step.")]
        public float EngagementRange = 1.75f;

        [Tooltip("Range within which a man will look for someone to fight.")]
        public float TargetSearchRange = 6f;

        [Tooltip("Seconds between melee evaluations. Combat need not resolve every frame.")]
        public float MeleeTickSeconds = 0.2f;

        [Header("Melee — damage")]
        [Tooltip("Seconds between blows for an average fighter.")]
        public float AttackIntervalSeconds = 1.4f;

        [Tooltip("Health fraction removed by a clean unmodified hit. Lower means longer, grindier fights.")]
        [Range(0.02f, 0.5f)] public float BaseHitDamage = 0.11f;

        [Tooltip("Chance a blow lands at all, before modifiers.")]
        [Range(0.1f, 1f)] public float BaseHitChance = 0.55f;

        [Header("Physical melee")]
        [Tooltip("Global multiplier on all weapon wound severity. Lower = longer fights. Tuned low " +
                 "so lines grind: men hack and block while fatigue and morale decide the day, and a " +
                 "death is an event rather than a metronome.")]
        [Range(0.2f, 2f)] public float MeleeDamageScale = 0.55f;

        [Tooltip("Stamina a man spends throwing a blow.")]
        public float MeleeStaminaPerStrike = 0.06f;

        [Tooltip("Stamina a shield-bearer loses turning a blow aside.")]
        public float ShieldBlockStaminaCost = 0.09f;

        [Tooltip("Body radius a blow must reach to wound a man, in metres.")]
        public float BodyRadius = 0.55f;

        [Tooltip("Player LMB held below this (seconds) is a quick slash; above it charges a thrust.")]
        public float PlayerSlashHoldThreshold = 0.16f;

        [Tooltip("Seconds a man reels after a heavy blow breaks his tired guard: he cannot strike and his " +
                 "shield drops. This is the beat in which a pressured shield wall cracks.")]
        public float StaggerSeconds = 0.6f;

        [Tooltip("Fraction of a blow's damage that gets through when it staggers past a broken guard.")]
        [Range(0f, 1f)] public float StaggerDamageFraction = 0.5f;

        [Tooltip("SUPERSEDED in 2d: the shield's block cone is now per-weapon (MeleeProfile.BlockArcDot), " +
                 "so Roman and German boards differ. This global field is no longer read.")]
        [Range(-0.5f, 0.6f)] public float ShieldBlockArcDot = -0.1f;

        [Header("Shields")]
        [Tooltip("Scales a formation's frontal block chance into how often a frontal blow is turned " +
                 "aside. 1 uses the formation value as-is; higher makes the shield wall even stickier.")]
        [Range(0f, 1.5f)] public float ShieldBlockChanceScale = 1f;

        [Tooltip("The Centurion's own scutum: chance a blow from his front is blocked, scaled by his stamina.")]
        [Range(0f, 0.9f)] public float PlayerShieldBlockChance = 0.45f;

        [Tooltip("Damage multiplier for a blow landing on a man's flank.")]
        public float FlankDamageMultiplier = 1.5f;

        [Tooltip("Damage multiplier for a blow landing from behind. Rout is lethal for this reason.")]
        public float RearDamageMultiplier = 2.2f;

        [Tooltip("Stamina spent per blow thrown.")]
        public float StaminaPerAttack = 0.02f;

        [Tooltip("Stamina recovered per second while out of combat.")]
        public float StaminaRecoveryPerSecond = 0.012f;

        [Header("Morale — decay and pressure")]
        [Tooltip("Seconds between morale evaluations.")]
        public float MoraleTickSeconds = 0.5f;

        [Tooltip("Cohesion lost per man lost from the squad — charged once per death. Ten deaths " +
                 "in a squad of ten costs a full bar; a bad volley shakes a line without breaking it.")]
        public float CohesionLossPerCasualty = 0.10f;

        [Tooltip("How quickly the shock of casualties fades, per second.")]
        public float CasualtyPressureDecay = 0.25f;

        [Tooltip("Cohesion lost per second while locally outnumbered, at maximum disadvantage.")]
        public float OutnumberedCohesionPerSecond = 0.05f;

        [Tooltip("Radius used to count who is winning locally.")]
        public float LocalPressureRadius = 12f;

        [Tooltip("Cohesion lost per second while the formation is broken up.")]
        public float DisorderCohesionPerSecond = 0.02f;

        [Tooltip("Cohesion recovered per second when out of combat and in good order.")]
        public float CohesionRecoveryPerSecond = 0.035f;

        [Tooltip("Cohesion lost per second scaled by how much of the squad has fallen. A gutted squad breaks.")]
        public float DepletionCohesionPerSecond = 0.05f;

        [Tooltip("Cohesion lost per second when the squad is exhausted (scaled by how tired the men " +
                 "are). This is a primary way long fights END: a spent line wavers and breaks.")]
        public float FatigueCohesionPerSecond = 0.05f;

        [Header("Morale — the standard")]
        [Tooltip("Radius within which the standard-bearer's banner steadies a squad.")]
        public float StandardAuraRadius = 22f;

        [Tooltip("Cohesion granted per second to a squad near its living standard.")]
        public float StandardCohesionPerSecond = 0.045f;

        [Header("Morale — command")]
        [Tooltip("Radius within which the Centurion steadies his men.")]
        public float CommandAuraRadius = 14f;

        [Tooltip("Cohesion granted per second by the Centurion's presence.")]
        public float CommandAuraCohesionPerSecond = 0.05f;

        [Tooltip("Cohesion granted per second by an optio in the squad. He stands at the rear to stop men running.")]
        public float OptioCohesionPerSecond = 0.03f;

        [Tooltip("Multiplier on cohesion loss while an optio lives. Below one means he absorbs shock.")]
        [Range(0.3f, 1f)] public float OptioLossMultiplier = 0.7f;

        [Header("Rout and rally")]
        [Tooltip("Cohesion at or below which a squad routs.")]
        [Range(0f, 0.3f)] public float RoutThreshold = 0.08f;

        [Tooltip("How far behind the friendly line a routing squad runs.")]
        public float RoutDistance = 45f;

        [Tooltip("Speed multiplier for men in flight. Fear is faster than discipline.")]
        public float RoutSpeedMultiplier = 1.35f;

        [Tooltip("Radius within which the Centurion can rally a broken squad.")]
        public float RallyRadius = 10f;

        [Tooltip("Seconds of sustained rallying needed to bring a squad back.")]
        public float RallyDurationSeconds = 2.5f;

        [Tooltip("Cohesion a rallied squad returns with.")]
        [Range(0.1f, 0.6f)] public float RalliedCohesion = 0.3f;

        [Header("Medicus")]
        [Tooltip("Health restored per second to a wounded man beside a medicus who is out of combat.")]
        public float MedicusHealPerSecond = 0.03f;

        public float MedicusRadius = 5f;

        [Header("Battle end")]
        [Tooltip("Fraction of the enemy that must be dead or routed for a victory.")]
        [Range(0.5f, 1f)] public float EnemyBrokenFraction = 0.8f;

        [Tooltip("Fraction of your own force lost or routed before the battle is a defeat.")]
        [Range(0.5f, 1f)] public float PlayerBrokenFraction = 0.8f;

        [Tooltip("Seconds the end condition must hold before the battle concludes.")]
        public float OutcomeConfirmSeconds = 2.5f;

        [Header("Enemy AI")]
        [Tooltip("Seconds between enemy squad decisions.")]
        public float EnemyDecisionSeconds = 1.2f;

        [Tooltip("Distance at which an enemy squad commits to a charge.")]
        public float EnemyChargeRange = 30f;

        [Tooltip("Range a Wary commander lets the Romans reach before he commits his squads.")]
        public float WaryCommitRange = 22f;

        [Tooltip("Distance a Skirmisher squad tries to keep from the nearest Roman.")]
        public float SkirmishPreferredRange = 16f;

        [Tooltip("If a Roman comes within this, a Skirmisher squad gives ground.")]
        public float SkirmishMinRange = 10f;

        [Header("Missiles (pila & javelins)")]
        [Tooltip("Pila the Centurion carries into a fight.")]
        public int PilaCount = 4;

        [Tooltip("Launch speed of a thrown pilum, in metres per second. Higher is flatter and faster.")]
        public float PilaLaunchSpeed = 26f;

        [Tooltip("Health fraction a pilum removes on a clean hit. Wounds more than it kills: the " +
                 "volley softens and shakes a line for the charge, it does not delete it.")]
        [Range(0.05f, 0.9f)] public float PilaDamage = 0.38f;

        [Tooltip("Radius within which a flying pilum counts as striking a man (measured at chest height).")]
        public float PilaImpactRadius = 0.85f;

        [Tooltip("Fraction of a pilum's damage a raised shield stops from the front. Pila largely defeat shields.")]
        [Range(0f, 0.8f)] public float PilaShieldBlock = 0.25f;

        [Tooltip("Seconds between javelin throws for a Skirmisher squad.")]
        public float MissileCooldownSeconds = 2.2f;

        [Tooltip("Maximum range a Skirmisher will loose a javelin.")]
        public float MissileRange = 20f;

        [Tooltip("A squad looses its one opening volley when the enemy closes to this range.")]
        public float PilaVolleyRange = 14f;
    }
}
