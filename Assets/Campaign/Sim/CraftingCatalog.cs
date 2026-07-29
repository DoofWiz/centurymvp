using System.Collections.Generic;
using Century.Campaign.Model;

namespace Century.Campaign.Sim
{
    /// <summary>What a recipe takes and what it makes: either Food into the stores, or items.</summary>
    public sealed class CraftingRecipe
    {
        public string Id;
        public string Name;
        public string Flavour;

        /// <summary>Icon name from the game-icons pack (the X of the .gi-X USS class).</summary>
        public string Icon;

        /// <summary>The station that must be built (level 1+) before this can be made.</summary>
        public CampStationId Station;

        /// <summary>Inputs consumed from the inventory: catalog item id and count.</summary>
        public (string ItemId, int Count)[] Inputs;

        /// <summary>Food added to the stores. The cooking fire's level scales this up.</summary>
        public float FoodYield;

        /// <summary>Item produced instead of (or as well as) Food.</summary>
        public string OutputItemId;
        public int OutputCount;
    }

    /// <summary>
    /// Everything the camp can make. Authored in code, one recipe per entry, same contract as
    /// <see cref="Core.Items.ItemCatalog"/>: the designer retunes freely here. Recipes are gated by
    /// camp stations — a cooking fire feeds the men, the medical tent works raw materials into
    /// medicine — so raising the camp is what unlocks the crafts.
    /// </summary>
    public static class CraftingCatalog
    {
        public static readonly List<CraftingRecipe> All = new List<CraftingRecipe>
        {
            // ---- Cooking Fire: raw stores into Food ------------------------------------------------
            new CraftingRecipe
            {
                Id = "bake_bread", Icon = "bread", Name = "Bake Bread", Station = CampStationId.CookingFire,
                Inputs = new[] { ("grain_sack", 1) }, FoodYield = 7f,
                Flavour = "Flat loaves off the fire-stones. The smell alone is worth morale."
            },
            new CraftingRecipe
            {
                Id = "bake_flour", Icon = "bread", Name = "Bake Loaves of Flour", Station = CampStationId.CookingFire,
                Inputs = new[] { ("flour_sack", 1) }, FoodYield = 8f,
                Flavour = "Proper bread, near enough. The men stop calling it porridge duty."
            },
            new CraftingRecipe
            {
                Id = "salt_the_game", Icon = "meat", Name = "Salt the Game", Station = CampStationId.CookingFire,
                Inputs = new[] { ("raw_game", 1), ("salt_blocks", 1) }, FoodYield = 9f,
                Flavour = "Hunted meat cured to last the march. Salt buys time; time buys miles."
            },
            new CraftingRecipe
            {
                Id = "mix_trail", Icon = "shiny-apple", Name = "Pack Trail Mix", Station = CampStationId.CookingFire,
                Inputs = new[] { ("dried_figs", 1), ("hardtack", 1) },
                OutputItemId = "trail_mix", OutputCount = 3,
                Flavour = "Broken biscuit and hard fruit, bagged for the march. Eaten without stopping."
            },

            // ---- Medical Tent: raw materials into medicine ----------------------------------------
            new CraftingRecipe
            {
                Id = "prepare_dressings", Icon = "bandage-roll", Name = "Prepare Dressings", Station = CampStationId.MedicalTent,
                Inputs = new[] { ("wool_cloth", 1), ("vinegar_jar", 1) },
                OutputItemId = "field_dressings", OutputCount = 2,
                Flavour = "Cloth boiled, cut and soaked in vinegar. Ready for the worst of it."
            },
            new CraftingRecipe
            {
                Id = "brew_salve", Icon = "health-potion", Name = "Brew Healing Salve", Station = CampStationId.MedicalTent,
                Inputs = new[] { ("healing_herbs", 2), ("honey_pot", 1) },
                OutputItemId = "healing_salve", OutputCount = 2,
                Flavour = "Yarrow and comfrey steeped in honey. The medicus guards the recipe jealously."
            },
        };
    }
}
