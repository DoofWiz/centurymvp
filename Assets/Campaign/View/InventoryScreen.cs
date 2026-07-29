using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The inventory screen: the column's stuff as a grid of tiles — count badge in the corner, a
    /// glyph for the kind of thing, name beneath — under an ALL tab plus one per category. Clicking
    /// a tile inspects it in the footer, beside the STORAGE readout. Aggregates the simulation still
    /// consumes (rations, firewood, medicine, coin) appear as gold quartermaster's-ledger tiles.
    /// </summary>
    /// <remarks>
    /// A plain class over elements inside OvermapHud.uxml, owned by <see cref="OvermapHudController"/>
    /// (which also pauses the clock while it is open). No scene setup. Glyphs are font characters
    /// standing in for item art until real icons exist.
    /// </remarks>
    public sealed class InventoryScreen
    {
        private const int Columns = 8;

        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;

        private readonly VisualElement _modal;
        private readonly ScrollView _list;
        private readonly Label _detailName, _detailInfo, _storage;
        private readonly Button[] _tabs = new Button[7];

        /// <summary>Selected tab: 0 = ALL, otherwise (InventoryCategory)(index - 1).</summary>
        private int _tab;

        private Button _selectedTile;

        public bool IsOpen => _modal != null && _modal.style.display == DisplayStyle.Flex;

        public InventoryScreen(VisualElement root, CampaignState state, CampaignSettings settings)
        {
            _state = state;
            _settings = settings;

            _modal = root.Q<VisualElement>("inventory-modal");
            _list = root.Q<ScrollView>("inv-list");
            _detailName = root.Q<Label>("inv-detail-name");
            _detailInfo = root.Q<Label>("inv-detail-info");
            _storage = root.Q<Label>("inv-storage");

            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i] = root.Q<Button>($"inv-tab-{i}");
                if (_tabs[i] == null) continue;

                int index = i;
                _tabs[i].clicked += () => SelectTab(index);
            }
        }

        public void Open()
        {
            if (_modal == null) return;
            _modal.style.display = DisplayStyle.Flex;
            SelectTab(_tab);
        }

        public void Close()
        {
            if (_modal != null) _modal.style.display = DisplayStyle.None;
        }

        private void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i]?.EnableInClassList("pill--active", i == index);

            ClearInspection();
            Render();
        }

        // --- Rendering ---------------------------------------------------------------------------

        private void Render()
        {
            if (_list == null || _state?.PlayerParty == null) return;
            _list.Clear();
            _selectedTile = null;

            PartyState party = _state.PlayerParty;
            RefreshStorage(party);

            int tiles = 0;

            if (_tab == 0)
            {
                for (int c = 0; c < 6; c++) tiles += RenderCategory(party, (InventoryCategory)c);
            }
            else
            {
                tiles += RenderCategory(party, (InventoryCategory)(_tab - 1));
            }

            if (tiles == 0)
            {
                var grid = NewGrid();
                for (int i = 0; i < Columns; i++) grid.Add(EmptyTile());
                _list.Add(grid);

                var empty = new Label(EmptyText()) { pickingMode = PickingMode.Ignore };
                empty.AddToClassList("inv-empty");
                _list.Add(empty);
            }
        }

        /// <summary>One section per subtype that has anything in it, each a grid padded to full rows.</summary>
        private int RenderCategory(PartyState party, InventoryCategory category)
        {
            int total = 0;
            string openSection = null;
            VisualElement grid = null;
            int inRow = 0;

            void CloseGrid()
            {
                if (grid == null) return;
                while (inRow % Columns != 0) { grid.Add(EmptyTile()); inRow++; }
                _list.Add(grid);
                grid = null;
                inRow = 0;
            }

            void OpenSection(string title)
            {
                CloseGrid();
                AddSectionHeader(title);
                grid = NewGrid();
            }

            // Ledger tiles head their category. For Resources the Food tile opens the Foodstuffs
            // section and the edible items continue in the same grid beside it.
            foreach ((string name, string info, string amount, string glyph) in LedgerRows(party, category))
            {
                string ledgerSection = category == InventoryCategory.Resources
                    ? "Foodstuffs"
                    : category == InventoryCategory.Currency ? "Money" : "The Men's Own Kit";

                if (openSection != ledgerSection)
                {
                    openSection = ledgerSection;
                    OpenSection(ledgerSection);
                }

                grid.Add(LedgerTile(name, info, amount, glyph));
                inRow++;
                total++;
            }

            IReadOnlyList<ItemDef> all = ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                ItemDef def = all[i];
                if (def.Category != category) continue;

                int count = party.Inventory.CountOf(def.Id);
                if (count <= 0) continue;

                if (def.Subtype != openSection)
                {
                    openSection = def.Subtype;
                    OpenSection(def.Subtype);
                }

                grid.Add(ItemTile(def, count));
                inRow++;
                total++;
            }

            CloseGrid();
            return total;
        }

        private IEnumerable<(string, string, string, string)> LedgerRows(PartyState party, InventoryCategory category)
        {
            Stores s = party.Stores;
            int men = Mathf.Max(1, party.Roster.ActiveCount);

            switch (category)
            {
                case InventoryCategory.Resources:
                    yield return ("Food",
                        $"{s.FoodDaysRemaining(men, _settings.RationsPerManPerDay):0.0} days at current strength — " +
                        "cooked and ready; the fire turns raw stores into more",
                        $"{s.Food:0}", "grain");
                    break;

                case InventoryCategory.Currency:
                    yield return ("Denarii", "Silver — the argument every tribe understands", $"{s.Denarii:n0}", "two-coins");
                    yield return ("Coin", "Bronze and small change", $"{s.Coin:n0}", "coins");
                    break;

                case InventoryCategory.Equipment:
                    yield return ("The Men's Own Kit", "Maintained nightly with tools and materials",
                        ConditionWord(s.EquipmentCondition01), "anvil");
                    break;
            }
        }

        // --- Tiles -------------------------------------------------------------------------------

        private static VisualElement NewGrid()
        {
            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.AddToClassList("inv-grid");
            return grid;
        }

        private Button ItemTile(ItemDef def, int count)
        {
            Button tile = Tile(def.IconName, GlyphFor(def.Subtype), def.Name,
                count > 1 ? count.ToString() : null);
            tile.clicked += () => Inspect(tile,
                def.Name,
                $"{def.Flavour}   ·   × {count}  ·  {def.Weight * count:0.#} loads  ·  {def.Value * count:n0} den");
            return tile;
        }

        private Button LedgerTile(string name, string info, string amount, string icon)
        {
            Button tile = Tile(icon, "▣", name, amount);
            tile.AddToClassList("inv-tile--ledger");
            tile.clicked += () => Inspect(tile, name, info);
            return tile;
        }

        private static Button Tile(string icon, string fallbackGlyph, string name, string badge)
        {
            var tile = new Button { text = string.Empty };
            tile.AddToClassList("inv-tile");

            if (!string.IsNullOrEmpty(icon))
            {
                // Real art from the icon pack, via the generated .gi-* classes.
                var image = new VisualElement { pickingMode = PickingMode.Ignore };
                image.AddToClassList("inv-tile__icon");
                image.AddToClassList($"gi-{icon}");
                tile.Add(image);
            }
            else
            {
                var glyphLabel = new Label(fallbackGlyph) { pickingMode = PickingMode.Ignore };
                glyphLabel.AddToClassList("inv-tile__glyph");
                tile.Add(glyphLabel);
            }

            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("inv-tile__name");
            tile.Add(nameLabel);

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeLabel = new Label(badge) { pickingMode = PickingMode.Ignore };
                badgeLabel.AddToClassList("inv-tile__badge");
                tile.Add(badgeLabel);
            }

            return tile;
        }

        private static VisualElement EmptyTile()
        {
            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("inv-tile");
            tile.AddToClassList("inv-tile--empty");
            return tile;
        }

        private void AddSectionHeader(string title)
        {
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("inv-section");

            var label = new Label(title.ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("heading");
            head.Add(label);

            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.AddToClassList("rule");
            rule.AddToClassList("rule--short");
            head.Add(rule);

            _list.Add(head);
        }

        // --- Inspection & footer -------------------------------------------------------------------

        private void Inspect(Button tile, string name, string info)
        {
            _selectedTile?.RemoveFromClassList("inv-tile--selected");
            _selectedTile = tile;
            tile.AddToClassList("inv-tile--selected");

            if (_detailName != null) _detailName.text = name;
            if (_detailInfo != null) _detailInfo.text = info;
        }

        private void ClearInspection()
        {
            if (_detailName != null) _detailName.text = "Select an item to inspect it.";
            if (_detailInfo != null) _detailInfo.text = string.Empty;
        }

        private void RefreshStorage(PartyState party)
        {
            if (_storage == null) return;

            float weight = ItemCatalog.TotalWeight(party.Inventory);
            float capacity = ItemCatalog.Capacity(party.Inventory);
            _storage.text = $"STORAGE {weight:0} / {capacity:0}";
            _storage.EnableInClassList("text-danger", weight > capacity);
        }

        // --- Flavour helpers -----------------------------------------------------------------------

        /// <summary>Stand-in art: one font glyph per subtype, tinted by the shared gold.</summary>
        private static string GlyphFor(string subtype)
        {
            switch (subtype)
            {
                case "Supplies": return "▣";          // ▣
                case "Firewood": return "▲";          // ▲
                case "Materials": return "◈";         // ◈
                case "Medical Supplies": return "✚";  // ✚
                case "Luxuries": return "✦";          // ✦
                case "Trade Goods": return "◆";       // ◆
                case "Valuables": return "✧";         // ✧
                case "Weapons": return "†";           // †
                case "Armour": return "⛊";            // ⛊
                case "Tools": return "▤";             // ▤
                case "Ammunition": return "»";        // »
                case "Maps": return "◊";              // ◊
                case "Letters": return "≡";           // ≡
                case "Orders": return "§";            // §
                case "Contracts": return "¶";         // ¶
                case "Banners": return "⚑";           // ⚑
                case "Named Weapons": return "†";     // †
                case "Named Armour": return "⛊";      // ⛊
                case "Artefacts": return "❖";         // ❖
                case "Mementos": return "♥";          // ♥
                case "Wagons": return "⊞";            // ⊞
                case "Pack Animals": return "∩";      // ∩
                case "Horses": return "♞";            // ♞
                default: return "◇";                  // ◇
            }
        }

        private static string ConditionWord(float condition01)
        {
            if (condition01 >= 0.85f) return "Pristine";
            if (condition01 >= 0.65f) return "Serviceable";
            if (condition01 >= 0.45f) return "Worn";
            if (condition01 >= 0.25f) return "Failing";
            return "Ruinous";
        }

        private string EmptyText()
        {
            InventoryCategory category = _tab == 0 ? InventoryCategory.Resources : (InventoryCategory)(_tab - 1);
            switch (category)
            {
                case InventoryCategory.Documents:
                    return "No papers worth the name. Maps, letters and orders taken on the march will gather here.";
                case InventoryCategory.Trophies:
                    return "No trophies yet. Banners, named arms and stranger things will hang here when they are won.";
                case InventoryCategory.Transport:
                    return "The column carries everything on its backs. Wagons and beasts would ease the load.";
                default:
                    return "Nothing here yet. What the column finds on the march will gather here.";
            }
        }
    }
}
