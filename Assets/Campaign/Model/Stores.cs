using System;
using UnityEngine;

namespace Century.Campaign.Model
{
    /// <summary>
    /// The party's aggregate stores. After the itemisation sweep only three things remain here:
    /// Food (what the men eat, produced by cooking and foraging), money, and the aggregate kit
    /// condition. Everything else the column carries — firewood, medicine, materials, spares — is
    /// real items in <see cref="PartyState.Inventory"/>.
    /// </summary>
    [Serializable]
    public sealed class Stores
    {
        /// <summary>One unit of Food feeds one man for one day. Raw foodstuffs in the inventory
        /// become Food at the cooking fire; ready-to-eat items are eaten straight from the packs.</summary>
        public float Food;

        /// <summary>Local barter tokens and trade goods.</summary>
        public int Coin;

        /// <summary>Roman currency. Kept separate because it buys different things.</summary>
        public int Denarii;

        /// <summary>Aggregate kit condition, 0..1. Degrades with marching and combat.</summary>
        [Range(0f, 1f)] public float EquipmentCondition01 = 1f;

        /// <summary>Captives. Worth ransom or labour, but they eat.</summary>
        public int Prisoners;

        public float FoodDaysRemaining(int mouths, float rationsPerManPerDay)
        {
            float dailyBurn = Mathf.Max(0.0001f, mouths * rationsPerManPerDay);
            return Food / dailyBurn;
        }
    }
}
