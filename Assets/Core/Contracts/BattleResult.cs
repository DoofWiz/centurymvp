using System.Collections.Generic;

namespace Century.Core.Contracts
{
    public enum BattleOutcome
    {
        /// <summary>Battle never resolved (scene aborted, load failure). Campaign state is untouched.</summary>
        Aborted = 0,
        Victory = 1,
        Defeat = 2,
        /// <summary>Player disengaged deliberately. Usually costs stragglers but preserves the party.</summary>
        Withdrawal = 3
    }

    /// <summary>Return leg of the contract. Applied to campaign state by the App layer.</summary>
    public sealed class BattleResult
    {
        public BattleOutcome Outcome = BattleOutcome.Aborted;
        public string PlayerPartyId;
        public string EnemyPartyId;

        public List<CombatantOutcome> Combatants = new List<CombatantOutcome>();

        public int EnemiesKilled;

        /// <summary>Enemy who broke and were run down rather than killed outright.</summary>
        public int EnemiesRouted;

        public LootBundle Loot = new LootBundle();

        /// <summary>Experience for the commander himself, tracked separately from the roster.</summary>
        public int CommanderExperience;

        /// <summary>How much campaign time the fight consumed.</summary>
        public double MinutesElapsed;

        /// <summary>True when the enemy force was destroyed outright rather than merely beaten.</summary>
        public bool EnemyAnnihilated;
    }

    public sealed class CombatantOutcome
    {
        public string SoldierId;
        public bool Survived = true;

        /// <summary>Cut down but found alive when the field was cleared: he comes home, barely,
        /// and fights in nothing until he has healed. Only meaningful when Survived is false.</summary>
        public bool Wounded;

        public float Health01 = 1f;
        public float Stamina01 = 1f;
        public float Morale01 = 0.6f;
        public int Kills;

        /// <summary>Experience earned in this battle. Applied to the roster on return.</summary>
        public int ExperienceGained;

        /// <summary>True when the man broke and ran. Carried back so it can affect his morale.</summary>
        public bool Routed;
    }

    public sealed class LootBundle
    {
        /// <summary>Cooked, edible food stripped from the enemy camp — goes straight to the stores.</summary>
        public float Food;

        public int Coin;
        public int Denarii;

        /// <summary>Additive change to party equipment condition, in the range -1..1.</summary>
        public float EquipmentConditionDelta;

        /// <summary>Captured enemy. What can be done with them is a campaign-layer decision.</summary>
        public int Prisoners;

        /// <summary>Itemised spoils: catalog item id and count. The battle decides them so the
        /// summary can show them; the campaign layer adds them to the party inventory.</summary>
        public List<LootItem> Items = new List<LootItem>();

        public bool IsEmpty =>
            Food <= 0f && Coin <= 0 && Denarii <= 0 && Prisoners <= 0 && Items.Count == 0;
    }

    /// <summary>A quantity of one catalog item taken as spoils.</summary>
    public struct LootItem
    {
        public string ItemId;
        public int Count;

        public LootItem(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
