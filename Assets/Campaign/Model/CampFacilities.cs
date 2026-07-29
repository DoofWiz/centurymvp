using System;
using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>The upgradable stations a camp can raise. Each is a job someone does for the century.</summary>
    public enum CampStationId
    {
        /// <summary>Valetudinarium — tends the wounded so they return to the line sooner.</summary>
        MedicalTent = 0,
        /// <summary>Fabrica — a field smithy that keeps kit off the "worn" list.</summary>
        Fabrica = 1,
        /// <summary>Campus — a patch of trampled ground to drill the green men on.</summary>
        TrainingGround = 2,
        /// <summary>Vigilia — pickets and a watchfire that make the camp hard to surprise.</summary>
        WatchPost = 3,
        /// <summary>The scout's fire — a base for ranging parties before they set out.</summary>
        ScoutFire = 4,
        /// <summary>Culina — the cook-fire and kettles that turn raw stores into food.</summary>
        CookingFire = 5
    }

    /// <summary>
    /// Per-party record of which stations have been built and to what level. Level 0 means "not yet
    /// built". Lives on <see cref="PartyState"/> so a camp raised in one sitting is still standing
    /// the next night.
    /// </summary>
    [Serializable]
    public sealed class CampFacilities
    {
        [Serializable]
        public struct Station
        {
            public CampStationId Id;
            public int Level;
        }

        public List<Station> Built = new List<Station>();

        public int LevelOf(CampStationId id)
        {
            for (int i = 0; i < Built.Count; i++)
                if (Built[i].Id == id) return Built[i].Level;
            return 0;
        }

        /// <summary>Sets a station's level directly. Used by the factory to stand up a starting camp.</summary>
        public void SetLevel(CampStationId id, int level)
        {
            for (int i = 0; i < Built.Count; i++)
            {
                if (Built[i].Id != id) continue;
                Built[i] = new Station { Id = id, Level = level };
                return;
            }

            Built.Add(new Station { Id = id, Level = level });
        }

        public bool CanUpgrade(CampStationId id) => LevelOf(id) < CampStationCatalog.Info(id).MaxLevel;
    }

    /// <summary>
    /// Static definitions for the camp stations: names, the effect at a given level, and what the
    /// next level costs. Kept apart from <see cref="CampFacilities"/> so the per-party save data stays
    /// small and the designer-facing copy sits in one file.
    /// </summary>
    public readonly struct CampStationCatalog
    {
        public readonly CampStationId Id;
        public readonly string Name;
        public readonly string Latin;
        public readonly string Description;
        public readonly int MaxLevel;

        /// <summary>Icon name from the game-icons pack (the X of the .gi-X USS class).</summary>
        public readonly string Icon;

        /// <summary>Timber charged to reach level 1; each further level scales this up. Stations are
        /// built from real materials in the inventory, not an abstract supply number.</summary>
        private readonly int _baseTimberCost;

        /// <summary>Denarii charged to reach level 1; each further level scales this up.</summary>
        private readonly int _baseDenariiCost;

        private CampStationCatalog(
            CampStationId id, string name, string latin, string description,
            int maxLevel, int baseTimberCost, int baseDenariiCost, string icon)
        {
            Id = id;
            Name = name;
            Latin = latin;
            Description = description;
            MaxLevel = maxLevel;
            _baseTimberCost = baseTimberCost;
            _baseDenariiCost = baseDenariiCost;
            Icon = icon;
        }

        public static readonly CampStationId[] All =
        {
            CampStationId.CookingFire, CampStationId.MedicalTent, CampStationId.Fabrica,
            CampStationId.TrainingGround, CampStationId.WatchPost, CampStationId.ScoutFire
        };

        public static CampStationCatalog Info(CampStationId id)
        {
            switch (id)
            {
                case CampStationId.CookingFire:
                    return new CampStationCatalog(id, "Cooking Fire", "Culina",
                        "Kettles and a proper fire. Turns raw stores into food the men will eat.",
                        3, 1, 20, "cauldron");
                case CampStationId.MedicalTent:
                    return new CampStationCatalog(id, "Medical Tent", "Valetudinarium",
                        "The medicus tends the wounded here, and works raw materials into medicine.",
                        3, 2, 60, "healing");
                case CampStationId.Fabrica:
                    return new CampStationCatalog(id, "Field Smithy", "Fabrica",
                        "Repairs weapons and armour overnight, holding off the slide to 'worn'.",
                        3, 3, 80, "anvil");
                case CampStationId.TrainingGround:
                    return new CampStationCatalog(id, "Training Ground", "Campus",
                        "Ground to drill the ranks. Sharpens what training the men receive.",
                        3, 1, 40, "crossed-swords");
                case CampStationId.WatchPost:
                    return new CampStationCatalog(id, "Watch Post", "Vigilia",
                        "Pickets and a watchfire. A stronger watch is harder to catch unawares.",
                        3, 2, 30, "watchtower");
                case CampStationId.ScoutFire:
                    return new CampStationCatalog(id, "Scout's Fire", "Speculatorium",
                        "A base for ranging parties, letting scouts reach farther afield.",
                        3, 1, 50, "campfire");
                default:
                    return new CampStationCatalog(id, id.ToString(), id.ToString(), string.Empty, 1, 2, 50, "toolbox");
            }
        }

        /// <summary>Seasoned timber needed to raise the station from <paramref name="currentLevel"/>.</summary>
        public int TimberCost(int currentLevel) => _baseTimberCost * (currentLevel + 1);

        /// <summary>Denarii needed to raise the station from <paramref name="currentLevel"/>.</summary>
        public int DenariiCost(int currentLevel) => _baseDenariiCost * (currentLevel + 1);

        /// <summary>Effect copy for a given level. Level 0 reads as "not built".</summary>
        public string EffectAt(int level)
        {
            if (level <= 0) return "Not yet built.";

            switch (Id)
            {
                case CampStationId.CookingFire:
                    return $"Cooking raw stores yields {100 + 15 * (level - 1)}% of their value.";
                case CampStationId.MedicalTent:
                    return $"Wounded recover {25 * level}% faster while camped.";
                case CampStationId.Fabrica:
                    return $"Repairs {2 * level}% kit condition each night.";
                case CampStationId.TrainingGround:
                    return $"Training yields {level}× the experience.";
                case CampStationId.WatchPost:
                    return $"Watch strength +{level}. Fewer camp ambushes.";
                case CampStationId.ScoutFire:
                    return $"Scouting range +{level}. Better Opportunities.";
                default:
                    return $"Level {level}.";
            }
        }
    }
}
