using System;
using System.Collections.Generic;

namespace Century.Core.Items
{
    /// <summary>The six shelves of the inventory screen. Order here is tab order on screen.</summary>
    public enum InventoryCategory
    {
        Resources = 0,
        Currency = 1,
        Equipment = 2,
        Documents = 3,
        Trophies = 4,
        Transport = 5
    }

    /// <summary>
    /// One kind of thing the column can carry, as authored in the catalog: what it is, which shelf
    /// and sub-heading it lives under, what it weighs and fetches, and the flavour line that makes a
    /// sack of grain feel looted rather than spreadsheeted.
    /// </summary>
    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public InventoryCategory Category;

        /// <summary>Sub-heading within the tab: "Supplies", "Materials", "Weapons", "Maps", ...</summary>
        public string Subtype;

        public string Flavour;

        /// <summary>Carry weight per unit, in loads. Transport itself weighs nothing.</summary>
        public float Weight;

        /// <summary>Worth per unit in denarii, for trade and for the greed of the men.</summary>
        public int Value;

        /// <summary>Carry capacity this item ADDS when owned (wagons, pack animals).</summary>
        public float CapacityBonus;

        /// <summary>Icon name from the game-icons pack (the X in the .gi-X USS class), or null.</summary>
        public string IconName;

        /// <summary>Food added per unit when eaten or cooked. Zero for anything inedible.</summary>
        public float FoodValue;

        /// <summary>Raw foodstuff: worth its FoodValue only through the cooking fire, never eaten
        /// straight from the sack. Ready-to-eat items (false) are drawn on automatically when the
        /// cooked Food runs out.</summary>
        public bool RawFood;

        /// <summary>Doses of usable medicine per unit. Raw medical materials carry zero and must be
        /// worked into dressings or salves at the medical tent first.</summary>
        public float MedicineDoses;

        /// <summary>Warmth per unit on the night fires. Firewood 1, charcoal burns longer.</summary>
        public float FuelValue;
    }

    /// <summary>A quantity of one item. The inventory is a list of these.</summary>
    [Serializable]
    public sealed class ItemStack
    {
        public string ItemId;
        public int Count;
    }

    /// <summary>
    /// Everything the column carries that is not a man or an aggregate store. Pure state — the
    /// catalog gives stacks their meaning, the inventory screen gives them a face.
    /// </summary>
    [Serializable]
    public sealed class PartyInventory
    {
        public List<ItemStack> Stacks = new List<ItemStack>();

        public int CountOf(string itemId)
        {
            for (int i = 0; i < Stacks.Count; i++)
                if (Stacks[i].ItemId == itemId) return Stacks[i].Count;
            return 0;
        }

        public void Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count == 0) return;

            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].ItemId != itemId) continue;

                Stacks[i].Count += count;
                if (Stacks[i].Count <= 0) Stacks.RemoveAt(i);
                return;
            }

            if (count > 0) Stacks.Add(new ItemStack { ItemId = itemId, Count = count });
        }

        public void Remove(string itemId, int count) => Add(itemId, -count);
    }
}
