using System.Collections.Generic;
namespace Century.Core.Items
{
    /// <summary>
    /// The item database: everything that can be looted, carried, traded, gifted or one day hung on
    /// an officer. Authored in code, one line per item, so the designer can retune names, weights,
    /// values and flavour freely — or replace the whole table with an external database later.
    /// </summary>
    /// <remarks>
    /// Since the itemisation sweep, only Food (plus money and kit condition) remains an aggregate on
    /// the campaign Stores: everything else the column carries is items here. Foodstuffs carry a
    /// FoodValue (raw ones need the cooking fire), medical items carry doses (raw materials need the
    /// medical tent), and fuel carries warmth for the night fires.
    /// </remarks>
    public static class ItemCatalog
    {
        /// <summary>What the men carry on their backs before any wagon or beast helps, in loads.</summary>
        public const float BaseCarryCapacity = 80f;

        private static readonly List<ItemDef> Items = new List<ItemDef>();
        private static readonly Dictionary<string, ItemDef> ById = new Dictionary<string, ItemDef>();

        public static IReadOnlyList<ItemDef> All => Items;

        public static ItemDef Find(string id) => ById.TryGetValue(id, out ItemDef def) ? def : null;

        static ItemCatalog()
        {
            const InventoryCategory Res = InventoryCategory.Resources;
            const InventoryCategory Cur = InventoryCategory.Currency;
            const InventoryCategory Equ = InventoryCategory.Equipment;
            const InventoryCategory Doc = InventoryCategory.Documents;
            const InventoryCategory Tro = InventoryCategory.Trophies;
            const InventoryCategory Tra = InventoryCategory.Transport;

            // ---- Resources: Foodstuffs. Raw goods need the cooking fire; the rest are eaten from
            //      the packs the moment the cooked Food runs out. ----
            Def("grain_sack", "Sack of Grain", Res, "Foodstuffs", 3f, 4,
                "Rough-milled spelt. The mill that ground it is ash now.", food: 6f, raw: true);
            Def("flour_sack", "Sack of Flour", Res, "Foodstuffs", 3f, 5,
                "Ground fine and kept dry, a miracle in this country.", food: 7f, raw: true);
            Def("raw_game", "Raw Game", Res, "Foodstuffs", 2f, 3,
                "Hunted and hung. Wants fire and salt before it wants a stomach.", food: 5f, raw: true);
            Def("hardtack", "Hardtack Crate", Res, "Foodstuffs", 2f, 3,
                "Twice-baked and proof against everything except teeth.", food: 4f);
            Def("trail_mix", "Trail Mix", Res, "Foodstuffs", 1f, 4,
                "Nuts, hard fruit and crumbs of biscuit. Eaten on the move, no fire needed.", food: 3f);
            Def("salt_pork", "Salted Pork", Res, "Foodstuffs", 2f, 6,
                "Wrapped in cloth and brine. The men eye it on the march.", food: 5f);
            Def("dried_fish", "Dried Fish", Res, "Foodstuffs", 1f, 3,
                "Strung river fish, hard as bark and twice as salty.", food: 3f);
            Def("cheese_wheels", "Cheese Wheels", Res, "Foodstuffs", 2f, 5,
                "Traded from a farmstead that asked no questions.", food: 3f);
            Def("waterskins", "Waterskins", Res, "Foodstuffs", 1f, 2,
                "Patched hide skins. A dry column is a dead column.", food: 1f);

            // ---- Resources: Materials (fuel lives here now — firewood is a material like any other) ----
            Def("firewood_bundle", "Firewood Bundle", Res, "Materials", 2f, 1,
                "Split and dried. Warmth, cooked food and courage by the armful.", fuel: 1f);
            Def("charcoal_sack", "Sack of Charcoal", Res, "Materials", 2f, 3,
                "Burns hot and quiet — the fire a smith wants, and a hidden camp too.", fuel: 2f);
            Def("leather_hides", "Leather Hides", Res, "Materials", 2f, 5,
                "For straps, soles, slings and the hundred small repairs of a marching column.");
            Def("timber", "Seasoned Timber", Res, "Materials", 4f, 3,
                "Cut lengths fit for palisade stakes, hafts and wagon ribs.");
            Def("iron_ingots", "Iron Ingots", Res, "Materials", 5f, 12,
                "Grey and heavy. Every blade the column will ever mend starts here.");
            Def("bronze_scrap", "Bronze Scrap", Res, "Materials", 3f, 8,
                "Fittings, buckles and broken vessels bound for the melting pot.");
            Def("rope_coils", "Rope Coils", Res, "Materials", 1f, 2,
                "Hemp coils. Nobody has ever had enough rope.");
            Def("pitch_pot", "Pot of Pitch", Res, "Materials", 2f, 4,
                "Seals seams, waterproofs boots, and burns when asked to.");
            Def("wool_cloth", "Wool Cloth", Res, "Materials", 1f, 4,
                "Bolts of undyed wool against the German winter.");

            // ---- Resources: Medical Supplies. Usable medicine carries doses; raw materials must be
            //      worked into dressings and salves at the medical tent. ----
            Def("linen_bandages", "Linen Bandages", Res, "Medical Supplies", 1f, 3,
                "Boiled and rolled by the medicus himself.", doses: 2f);
            Def("field_dressings", "Field Dressings", Res, "Medical Supplies", 1f, 6,
                "Vinegar-washed cloth cut and packed for the line. The medicus' own work.", doses: 3f);
            Def("healing_salve", "Healing Salve", Res, "Medical Supplies", 1f, 8,
                "Herbs in honey, sealed in wax. Smells vile, works wonders.", doses: 3f);
            Def("healing_herbs", "Healing Herbs", Res, "Medical Supplies", 1f, 5,
                "Yarrow and comfrey, gathered where the guides pointed. Raw, until the tent works them.");
            Def("vinegar_jar", "Jar of Vinegar", Res, "Medical Supplies", 2f, 2,
                "Cleans wounds and cuts the taste of doubtful water. A raw material of mercy.");
            Def("honey_pot", "Pot of Honey", Res, "Medical Supplies", 1f, 6,
                "For wounds and for morale, in that order, officially.");

            // ---- Resources: Luxuries ----
            Def("wine_amphora", "Wine Amphora", Res, "Luxuries", 4f, 15,
                "Genuine Campanian, a long way from home. So are we.");
            Def("ale_cask", "Cask of Ale", Res, "Luxuries", 4f, 8,
                "Local brew, cloudy and strong. The men have stopped complaining about it.");
            Def("mead_jar", "Jar of Mead", Res, "Luxuries", 2f, 9,
                "Honey-wine from some chieftain's stores. Sweet enough to forget the rain.");
            Def("olive_oil", "Olive Oil", Res, "Luxuries", 2f, 10,
                "A taste of Italy. Men have wept over less.");
            Def("garum_jar", "Jar of Garum", Res, "Luxuries", 2f, 12,
                "Fermented fish sauce. Home in a jar, if home smells like that.");
            Def("dried_figs", "Dried Figs", Res, "Luxuries", 1f, 6,
                "Sweet, southern, and strictly rationed by the optio.");

            // ---- Currency & Valuables: Trade Goods (coin itself lives on the ledger) ----
            Def("fur_pelts", "Fur Pelts", Cur, "Trade Goods", 1f, 7,
                "Beaver and fox, well cured. Good as coin anywhere cold.");
            Def("amber_lumps", "Baltic Amber", Cur, "Trade Goods", 0.2f, 18,
                "Honey-coloured lumps the tribes prize and Rome prizes more.");
            Def("salt_blocks", "Salt Blocks", Cur, "Trade Goods", 3f, 9,
                "White gold. Every village will treat with a man carrying salt.");
            Def("glass_beads", "Glass Beads", Cur, "Trade Goods", 0.2f, 5,
                "Bright Roman glass, worth more here than it ever was in Italy.");
            Def("pottery_fine", "Fine Pottery", Cur, "Trade Goods", 2f, 8,
                "Red Samian ware, miraculously unbroken so far.");

            // ---- Currency & Valuables: Valuables ----
            Def("silver_torc", "Silver Torc", Cur, "Valuables", 0.5f, 40,
                "A chieftain's neck-ring. A princely gift, or a princely bribe.");
            Def("gold_ring", "Gold Ring", Cur, "Valuables", 0.1f, 55,
                "An equestrian's ring. Its story ended at Teutoburg.");
            Def("bronze_fibulae", "Bronze Fibulae", Cur, "Valuables", 0.1f, 6,
                "Cloak-pins, fine work. Small enough to gift, fine enough to matter.");
            Def("silver_plate", "Silver Plate", Cur, "Valuables", 1.5f, 65,
                "Embossed dining silver from some legate's baggage. He won't miss it.");

            // ---- Equipment: Weapons (spares; every man carries his own kit) ----
            Def("gladius_spare", "Gladii", Equ, "Weapons", 1.5f, 20,
                "Spare short swords, oiled and wrapped. A man who loses his blade takes one here.");
            Def("pugio_spare", "Pugiones", Equ, "Weapons", 0.5f, 8,
                "Legionary daggers. The last argument, and the most portable.");
            Def("framea_captured", "Frameae", Equ, "Weapons", 1f, 6,
                "Captured German spears — light, wicked, and better than empty hands.");
            Def("war_axe", "War Axes", Equ, "Weapons", 2f, 10,
                "Bearded axes off the field. The men swing them for firewood and worse.");

            // ---- Equipment: Armour ----
            Def("scutum_spare", "Scuta", Equ, "Armour", 4f, 18,
                "Spare tower shields, patched and re-gripped. The wall must not thin.");
            Def("mail_shirt", "Mail Shirts", Equ, "Armour", 6f, 45,
                "Lorica hamata off the fallen. Heavy on the wagon, lighter on the conscience.");
            Def("helmet_spare", "Helmets", Equ, "Armour", 2f, 14,
                "Dented but sound. A dent means it worked.");
            Def("round_shield", "Round Shields", Equ, "Armour", 3f, 7,
                "Captured German boards. Lighter than a scutum and wearier in the arm.");

            // ---- Equipment: Tools ----
            Def("dolabra", "Dolabrae", Equ, "Tools", 2f, 6,
                "Pick-mattocks. The legion's truest weapon, ask any veteran.");
            Def("whetstone", "Whetstones", Equ, "Tools", 0.5f, 2,
                "A dull blade is a club. The stones say otherwise, nightly.");
            Def("saw_frame", "Frame Saws", Equ, "Tools", 1.5f, 5,
                "For timber, palisade and pyre alike.");
            Def("smith_hammer", "Smith's Hammers", Equ, "Tools", 2f, 7,
                "With a field anvil and enough oaths, armour comes back to life.");
            Def("cobbler_kit", "Cobbler's Kits", Equ, "Tools", 1f, 4,
                "Awls, waxed thread and lasts. An army marches on mended boots.");

            // ---- Equipment: Ammunition ----
            Def("pila_bundle", "Pila (bundle of 10)", Equ, "Ammunition", 5f, 15,
                "Replacement pila. Thrown once, bent by design, always wanted.");
            Def("javelin_bundle", "Javelins (bundle of 10)", Equ, "Ammunition", 4f, 8,
                "Light javelins, Roman and German mixed. The dead don't mind sharing.");
            Def("sling_stones", "Sling Stones", Equ, "Ammunition", 2f, 1,
                "River-smoothed shot in a sack. Cheap as the riverbed.");

            // ---- Documents ----
            Def("map_weser", "Map of the Weser Valley", Doc, "Maps", 0.1f, 25,
                "A trader's map, annotated in two hands. One of them was afraid.");
            Def("map_marsh_paths", "Chart of the Marsh Paths", Doc, "Maps", 0.1f, 30,
                "Safe ways through the black water, if the marks can be trusted.");
            Def("dispatch_xviii", "Dispatch of the Eighteenth", Doc, "Letters", 0.1f, 0,
                "A muddied dispatch from a legion that no longer exists.");
            Def("letter_tribal", "Sealed Tribal Letter", Doc, "Letters", 0.1f, 15,
                "Bound in bark, sealed in wax. Someone would pay to read it — or to burn it.");
            Def("orders_varus", "Orders under Varus' Seal", Doc, "Orders", 0.1f, 10,
                "Marching orders for an army that never arrived. Proof, and warning.");
            Def("contract_trader", "Trader's Contract", Doc, "Contracts", 0.1f, 20,
                "Safe passage and fair prices with a named river trader, if he still lives.");

            // ---- Trophies ----
            Def("vexillum_xix", "Vexillum of the Nineteenth", Tro, "Banners", 1f, 0,
                "The century's own standard, carried out of the forest. While it stands, so do we.");
            Def("banner_cherusci", "Cherusci War Banner", Tro, "Banners", 1f, 30,
                "Taken on the field. Every man who sees it remembers who ran.");
            Def("blade_chieftain", "Chieftain's Longsword", Tro, "Named Weapons", 2f, 80,
                "Pattern-welded and named in a tongue we don't speak. It came at a price.");
            Def("armour_ancestral", "Ancestral Scale Cuirass", Tro, "Named Armour", 6f, 90,
                "Older than the feud that surrendered it. Fit for an officer.");
            Def("idol_grove", "Idol of the Grove", Tro, "Artefacts", 3f, 50,
                "Oak, older than Rome, and heavier than it looks. The guides won't touch it.");
            Def("carved_horse", "Carved Wooden Horse", Tro, "Mementos", 0.1f, 0,
                "A soldier's carving for a son he means to see again.");

            // ---- Transport ----
            Def("ox_wagon", "Ox Wagon", Tra, "Wagons", 0f, 60,
                "Slow, loud, and worth its weight in everything it carries.", capacity: 120f);
            Def("hand_cart", "Hand Carts", Tra, "Wagons", 0f, 12,
                "Two wheels and a pair of tired men. Better than backs.", capacity: 30f);
            Def("mule", "Mules", Tra, "Pack Animals", 0f, 25,
                "Stubborn, sure-footed, and the best judge of a bad path in the column.", capacity: 25f);
            Def("pack_horse", "Pack Horses", Tra, "Pack Animals", 0f, 30,
                "Carries what a contubernium can't, and eats accordingly.", capacity: 30f);
            Def("riding_horse", "Riding Horses", Tra, "Horses", 0f, 45,
                "For scouts and messages. The Centurion walks with his men.");
        }

        private static void Def(
            string id, string name, InventoryCategory category, string subtype,
            float weight, int value, string flavour, float capacity = 0f,
            float food = 0f, bool raw = false, float doses = 0f, float fuel = 0f)
        {
            var def = new ItemDef
            {
                Id = id,
                Name = name,
                Category = category,
                Subtype = subtype,
                Weight = weight,
                Value = value,
                Flavour = flavour,
                CapacityBonus = capacity,
                FoodValue = food,
                RawFood = raw,
                MedicineDoses = doses,
                FuelValue = fuel,
                IconName = IconNames.TryGetValue(id, out string icon) ? icon : null
            };

            Items.Add(def);
            ById[id] = def;
        }

        /// <summary>
        /// Item id → icon name in the game-icons pack (rendered via the generated .gi-* classes in
        /// century.uss). Kept as one table so swapping an item's art is a one-line change; an
        /// unmapped item falls back to its subtype glyph on the tile.
        /// </summary>
        private static readonly Dictionary<string, string> IconNames = new Dictionary<string, string>
        {
            ["grain_sack"] = "grain-bundle",
            ["hardtack"] = "sliced-bread",
            ["salt_pork"] = "ham-shank",
            ["dried_fish"] = "fish-cooked",
            ["cheese_wheels"] = "cheese-wedge",
            ["waterskins"] = "water-flask",
            ["firewood_bundle"] = "log",
            ["charcoal_sack"] = "campfire",
            ["leather_hides"] = "animal-hide",
            ["timber"] = "wood-beam",
            ["iron_ingots"] = "metal-bar",
            ["bronze_scrap"] = "anvil-impact",
            ["rope_coils"] = "rope-coil",
            ["pitch_pot"] = "cauldron",
            ["wool_cloth"] = "wool",
            ["flour_sack"] = "grain",
            ["raw_game"] = "meat",
            ["trail_mix"] = "shiny-apple",
            ["linen_bandages"] = "bandaged",
            ["field_dressings"] = "bandage-roll",
            ["healing_salve"] = "health-potion",
            ["healing_herbs"] = "herbs-bundle",
            ["vinegar_jar"] = "water-bottle",
            ["honey_pot"] = "honeycomb",
            ["wine_amphora"] = "amphora",
            ["ale_cask"] = "barrel",
            ["mead_jar"] = "beer-horn",
            ["olive_oil"] = "olive",
            ["garum_jar"] = "fish-bucket",
            ["dried_figs"] = "fruit-bowl",
            ["fur_pelts"] = "fur-shirt",
            ["amber_lumps"] = "amber-mosquito",
            ["salt_blocks"] = "stone-block",
            ["glass_beads"] = "prayer-beads",
            ["pottery_fine"] = "painted-pottery",
            ["silver_torc"] = "torc",
            ["gold_ring"] = "ring",
            ["bronze_fibulae"] = "safety-pin",
            ["silver_plate"] = "trophy",
            ["gladius_spare"] = "gladius",
            ["pugio_spare"] = "daggers",
            ["framea_captured"] = "barbed-spear",
            ["war_axe"] = "war-axe",
            ["scutum_spare"] = "roman-shield",
            ["mail_shirt"] = "mail-shirt",
            ["helmet_spare"] = "galea",
            ["round_shield"] = "round-shield",
            ["dolabra"] = "war-pick",
            ["whetstone"] = "stone-stack",
            ["saw_frame"] = "crosscut-saw",
            ["smith_hammer"] = "claw-hammer",
            ["cobbler_kit"] = "sewing-needle",
            ["pila_bundle"] = "spears",
            ["javelin_bundle"] = "thrown-spear",
            ["sling_stones"] = "stone-pile",
            ["map_weser"] = "treasure-map",
            ["map_marsh_paths"] = "compass",
            ["dispatch_xviii"] = "scroll-unfurled",
            ["letter_tribal"] = "envelope",
            ["orders_varus"] = "tied-scroll",
            ["contract_trader"] = "contract",
            ["vexillum_xix"] = "vertical-banner",
            ["banner_cherusci"] = "flying-flag",
            ["blade_chieftain"] = "broadsword",
            ["armour_ancestral"] = "scale-mail",
            ["idol_grove"] = "totem",
            ["carved_horse"] = "horse-head",
            ["ox_wagon"] = "old-wagon",
            ["hand_cart"] = "wheelbarrow",
            ["mule"] = "donkey",
            ["pack_horse"] = "horseshoe",
            ["riding_horse"] = "cavalry",
        };

        // --- Derived ------------------------------------------------------------------------------

        /// <summary>Total loads the inventory currently weighs. Transport weighs nothing.</summary>
        public static float TotalWeight(PartyInventory inventory)
        {
            float total = 0f;
            for (int i = 0; i < inventory.Stacks.Count; i++)
            {
                ItemDef def = Find(inventory.Stacks[i].ItemId);
                if (def != null) total += def.Weight * inventory.Stacks[i].Count;
            }

            return total;
        }

        /// <summary>Carry capacity: what the men manage plus every wagon and beast in the train.</summary>
        public static float Capacity(PartyInventory inventory)
        {
            float total = BaseCarryCapacity;
            for (int i = 0; i < inventory.Stacks.Count; i++)
            {
                ItemDef def = Find(inventory.Stacks[i].ItemId);
                if (def != null) total += def.CapacityBonus * inventory.Stacks[i].Count;
            }

            return total;
        }

        // --- Consumption ---------------------------------------------------------------------------

        /// <summary>Total doses of USABLE medicine carried (dressings, salves, bandages).</summary>
        public static float TotalMedicineDoses(PartyInventory inventory) =>
            Total(inventory, def => def.MedicineDoses);

        /// <summary>Total warmth on hand for the night fires, in firewood-bundle equivalents.</summary>
        public static float TotalFuel(PartyInventory inventory) =>
            Total(inventory, def => def.FuelValue);

        /// <summary>
        /// The men eat straight from the packs: consumes ready-to-eat items (never raw ones — those
        /// need the cooking fire) until <paramref name="foodNeeded"/> is met or the packs are empty.
        /// Returns the Food actually produced. Whole items are eaten; hungry men do not save halves.
        /// </summary>
        public static float EatReadyFood(PartyInventory inventory, float foodNeeded) =>
            Consume(inventory, foodNeeded, def => def.RawFood ? 0f : def.FoodValue);

        /// <summary>Burns fuel items for the night fires. Returns the warmth actually produced.</summary>
        public static float BurnFuel(PartyInventory inventory, float warmthNeeded) =>
            Consume(inventory, warmthNeeded, def => def.FuelValue);

        /// <summary>Spends doses of usable medicine. Returns the doses actually supplied.</summary>
        public static float SpendMedicine(PartyInventory inventory, float dosesNeeded) =>
            Consume(inventory, dosesNeeded, def => def.MedicineDoses);

        private static float Total(PartyInventory inventory, System.Func<ItemDef, float> valueOf)
        {
            float total = 0f;
            for (int i = 0; i < inventory.Stacks.Count; i++)
            {
                ItemDef def = Find(inventory.Stacks[i].ItemId);
                if (def != null) total += valueOf(def) * inventory.Stacks[i].Count;
            }

            return total;
        }

        /// <summary>Consumes items with the smallest per-unit value first, so a two-dose bandage is
        /// not wasted where one dose of salve would do. Whole units only.</summary>
        private static float Consume(
            PartyInventory inventory, float needed, System.Func<ItemDef, float> valueOf)
        {
            float produced = 0f;

            while (produced < needed)
            {
                ItemDef best = null;
                float bestValue = float.MaxValue;

                for (int i = 0; i < inventory.Stacks.Count; i++)
                {
                    ItemDef def = Find(inventory.Stacks[i].ItemId);
                    if (def == null) continue;

                    float value = valueOf(def);
                    if (value <= 0f || value >= bestValue) continue;
                    best = def;
                    bestValue = value;
                }

                if (best == null) break;

                inventory.Remove(best.Id, 1);
                produced += bestValue;
            }

            return produced;
        }
    }
}
