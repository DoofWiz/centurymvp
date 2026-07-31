using System.Collections.Generic;

namespace Century.Core.Contracts
{
    /// <summary>
    /// Everything the battle layer needs in order to build a fight. This is the only thing the
    /// campaign layer hands across, which is what keeps the two assemblies independent: the
    /// battle scene has no idea what a party, a store of rations or a POI is.
    /// </summary>
    public sealed class BattleRequest
    {
        public string PlayerPartyId;
        public string EnemyPartyId;
        public string EnemyDisplayName;

        public List<CombatantSpec> PlayerCombatants = new List<CombatantSpec>();
        public List<CombatantSpec> EnemyCombatants = new List<CombatantSpec>();

        /// <summary>Id of the terrain the encounter occurred on, e.g. "dense_forest".</summary>
        public string TerrainId = "open";

        /// <summary>Overmap world position of the encounter. The battlefield is sculpted from the
        /// shared world function around this point, so the fight happens on the ground it happened on.</summary>
        public float WorldX;
        public float WorldZ;

        public CampaignTime TimeOfDay;

        /// <summary>The COLUMN was caught by surprise: no deployment screen, tight starting ground,
        /// the enemy already on top of them. Set only when battle is joined inside the surprise
        /// window a sudden close reveal opened.</summary>
        public bool PlayerAmbushed;

        /// <summary>The ENEMY was caught: a stealthed column striking hunters who never saw it.
        /// The player deploys with a free hand — a wider zone, closer than the rules of a fair
        /// meeting would ever allow.</summary>
        public bool EnemyAmbushed;

        /// <summary>The enemy commander's doctrine, chosen at spawn. Drives the enemy AI in the fight.</summary>
        public CommanderBehaviour EnemyBehaviour = CommanderBehaviour.Disciplined;

        /// <summary>Seed for the battle's own RNG so a fight can be replayed deterministically.</summary>
        public int RandomSeed;

        /// <summary>Commander doctrine ids in effect, for the battle systems that honour them.</summary>
        public List<string> CommanderSkills = new List<string>();

        /// <summary>The establishment's resolved effects (army layer). Never null.</summary>
        public PostEffectSet PostEffects = new PostEffectSet();

        /// <summary>The century's signum was lost in an earlier battle and has not been won back:
        /// no standard takes this field, and no aura steadies the line.</summary>
        public bool SignumAlreadyLost;

        /// <summary>First battle of the campaign: show the how-to-fight explainer before it.</summary>
        public bool ShowIntro;

        /// <summary>Captured war banners travelling with the column (relevant to certain doctrines).</summary>
        public int CapturedBanners;
    }

    /// <summary>A single fighter, flattened out of the campaign roster.</summary>
    public sealed class CombatantSpec
    {
        public string SoldierId;
        public string DisplayName;

        /// <summary>"centurion", "optio", "tesserarius", "medicus", "immunis", "legionary", ...</summary>
        public string RankId;

        /// <summary>Drives loadout and battle behaviour, e.g. "legionary_heavy", "cherusci_warrior".</summary>
        public string ArchetypeId;

        public float Health01 = 1f;
        public float Stamina01 = 1f;
        public float Morale01 = 0.6f;

        /// <summary>Exactly one combatant per battle should have this set.</summary>
        public bool IsPlayerControlled;

        /// <summary>Persistent squad this man belongs to (his contubernium), or -1 for none. When
        /// set, the battle builds its squads from these groups instead of dealing men into chunks —
        /// the century fights in the same tent groups the camp screen shows.</summary>
        public int GroupIndex = -1;

        /// <summary>Appointed leader of his group (the Decanus). Takes the front-centre slot.</summary>
        public bool IsGroupLeader;

        /// <summary>Multiplier on this man's melee damage. Carries veterancy when doctrine says
        /// experience should tell; 1 otherwise.</summary>
        public float DamageMultiplier = 1f;

        /// <summary>Display-only veterancy line for the battle HUD, e.g. "Miles (Trained)".
        /// A string because the tier enum lives campaign-side and battle only shows it.</summary>
        public string VeterancyLabel;
    }
}
