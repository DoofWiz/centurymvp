using System.Collections.Generic;
using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>One doctrine the commander can take. Every entry's effect is real and wired.</summary>
    public sealed class CommanderSkillDef
    {
        public string Id;
        public string Name;
        public CommanderPhilosophy Philosophy;

        /// <summary>Foundations now; Doctrines and Ideologies are the next tiers of this system.</summary>
        public int Tier = 1;

        public string Flavour;

        /// <summary>What it actually does, in the player's language. Must match the wired effect.</summary>
        public string Effect;

        /// <summary>Icon name from the game-icons pack (the X of the .gi-X USS class).</summary>
        public string Icon;
    }

    /// <summary>
    /// The commander skill deck: three philosophies whose doctrines are OFFERED, three at a time,
    /// weighted by how the player has actually led — the build emerges from the campaign rather
    /// than a tree browsed in advance. Authored in code; the designer retunes freely.
    /// </summary>
    public static class CommanderSkillCatalog
    {
        // --- Levels --------------------------------------------------------------------------------

        /// <summary>Commander level for a given experience total. Level 1 at zero; each level's
        /// step grows, so early doctrine comes fast and late doctrine is earned.</summary>
        public static int LevelFor(int experience)
        {
            int level = 1;
            int threshold = 0;
            int step = 120;

            while (experience >= threshold + step)
            {
                threshold += step;
                step += 40;
                level++;
            }

            return level;
        }

        /// <summary>Experience needed to reach the NEXT level, and progress toward it (0..1).</summary>
        public static float ProgressToNext(int experience, out int intoLevel, out int levelCost)
        {
            int threshold = 0;
            int step = 120;

            while (experience >= threshold + step)
            {
                threshold += step;
                step += 40;
            }

            intoLevel = experience - threshold;
            levelCost = step;
            return Mathf.Clamp01(intoLevel / (float)levelCost);
        }

        /// <summary>Doctrine picks earned and not yet spent.</summary>
        public static int SkillPoints(CommanderProgress progress, int experience) =>
            Mathf.Max(0, LevelFor(experience) - 1 - progress.OwnedSkills.Count);

        // --- The deck ------------------------------------------------------------------------------

        private static readonly List<CommanderSkillDef> Skills = new List<CommanderSkillDef>();
        private static readonly Dictionary<string, CommanderSkillDef> ById =
            new Dictionary<string, CommanderSkillDef>();

        public static IReadOnlyList<CommanderSkillDef> All => Skills;

        public static CommanderSkillDef Find(string id) =>
            ById.TryGetValue(id, out CommanderSkillDef def) ? def : null;

        public static CommanderPhilosophy? PhilosophyOf(string id) => Find(id)?.Philosophy;

        static CommanderSkillCatalog()
        {
            const CommanderPhilosophy Roman = CommanderPhilosophy.TrueRoman;
            const CommanderPhilosophy Survivor = CommanderPhilosophy.Survivor;
            const CommanderPhilosophy Barbarian = CommanderPhilosophy.Barbarian;

            // ---- TRUE ROMAN: the system, trusted harder --------------------------------------------
            Def("iron_discipline", "Iron Discipline", Roman, "checked-shield",
                "We do not become animals simply because we are surrounded by them.",
                "Squads under your command aura lose cohesion 20% slower.");
            Def("centurions_voice", "The Centurion's Voice", Roman, "centurion-helmet",
                "A century is commanded by a voice that expects to be obeyed.",
                "Your command aura reaches 30% farther.");
            Def("roman_order", "Roman Order", Roman, "scroll-quill",
                "Drill until the order and the act are the same thing.",
                "Orders reach your squads 30% faster.");
            Def("by_the_eagle", "By the Eagle", Roman, "vertical-banner",
                "Men do not follow you. They follow what you carry.",
                "The standard steadies squads 35% farther and 25% harder.");
            Def("no_man_breaks_rank", "No Man Breaks Rank", Roman, "backup",
                "The line grieves later.",
                "Losses shake your squads 25% less.");
            Def("roman_logistics", "Roman Logistics", Roman, "wooden-crate",
                "Amateurs talk tactics. Rome talks grain.",
                "The column eats 10% less food.");
            Def("fortify_the_camp", "Fortify the Camp", Roman, "watchtower",
                "Every night, a little Rome.",
                "Camp healing and morale recovery improved by 25%.");

            // ---- SURVIVOR: tomorrow, by any means --------------------------------------------------
            Def("never_fight_fair", "Never Fight Fair", Survivor, "bow-arrow",
                "A fair fight means you have already made a mistake.",
                "When you spring an ambush, enemy squads begin shaken (-15 cohesion).");
            Def("eyes_and_ears", "Eyes and Ears", Survivor, "compass",
                "The column that sees first chooses the day.",
                "Line of sight extended 12%.");
            Def("know_the_land", "Know the Land", Survivor, "trail",
                "Read the ground like the natives read the sky.",
                "The column marches 8% faster.");
            Def("one_more_day", "One More Day", Survivor, "grain",
                "Hunger is a problem for men who plan to die old.",
                "The column eats 15% less food.");
            Def("disengage", "Disengage", Survivor, "return-arrow",
                "Living to fight again is a victory the dead cannot claim.",
                "Withdrawing from battle costs half the morale.");
            Def("a_quiet_camp", "A Quiet Column", Survivor, "campfire",
                "Cold fires, wrapped harness, low voices.",
                "The column is 20% harder for enemies to mark.");
            Def("nothing_goes_to_waste", "Nothing Goes to Waste", Survivor, "coins",
                "The dead have no use for good iron.",
                "30% more food and coin stripped from won fields.");

            // ---- BARBARIAN: ferocity, freedom, belonging -------------------------------------------
            Def("war_cry", "War Cry", Barbarian, "flying-flag",
                "Let them hear what is coming for them.",
                "Squads begin battles you sought with their blood up (+8 cohesion).");
            Def("blooded", "Blooded", Barbarian, "crossed-swords",
                "The first kill answers a question every soldier carries.",
                "A man's morale surges when he takes an enemy down.");
            Def("the_strongest_lead", "The Strongest Lead", Barbarian, "laurels",
                "Rank is given. Standing is taken.",
                "Veteran soldiers strike up to 30% harder.");
            Def("follow_me", "Follow Me!", Barbarian, "boot-prints",
                "Not a voice from behind the line. A blade in front of it.",
                "Your presence steadies nearby squads 50% harder.");
            Def("take_what_you_kill", "Take What You Kill", Barbarian, "trophy",
                "Victory should weigh something in the hand.",
                "Winning lifts the men's spirits half again as much.");
            Def("no_shield_but_courage", "No Shield but Courage", Barbarian, "round-shield",
                "A broken guard is not a broken man.",
                "Your men recover from guard-breaks 40% faster.");
            Def("the_old_ways", "The Old Ways", Barbarian, "totem",
                "Their banners know this land. Now they march under yours.",
                "Each captured war banner emboldens your squads at battle's start.");
        }

        private static void Def(
            string id, string name, CommanderPhilosophy philosophy, string icon,
            string flavour, string effect)
        {
            var def = new CommanderSkillDef
            {
                Id = id,
                Name = name,
                Philosophy = philosophy,
                Icon = icon,
                Flavour = flavour,
                Effect = effect
            };

            Skills.Add(def);
            ById[id] = def;
        }

        // --- The offer -----------------------------------------------------------------------------

        /// <summary>
        /// Deals three doctrines, weighted by how the commander has actually led (behaviour scores)
        /// and by where they have already invested. Deterministic per level, so closing the screen
        /// never re-rolls the hand.
        /// </summary>
        public static List<string> GenerateOffer(CommanderProgress progress, int seed)
        {
            var pool = new List<CommanderSkillDef>();
            for (int i = 0; i < Skills.Count; i++)
                if (!progress.Has(Skills[i].Id)) pool.Add(Skills[i]);

            var offer = new List<string>();
            var random = new System.Random(seed);

            for (int pick = 0; pick < 3 && pool.Count > 0; pick++)
            {
                float total = 0f;
                for (int i = 0; i < pool.Count; i++) total += WeightOf(pool[i], progress);

                float roll = (float)(random.NextDouble() * total);
                int chosen = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= WeightOf(pool[i], progress);
                    if (roll > 0f) continue;
                    chosen = i;
                    break;
                }

                offer.Add(pool[chosen].Id);
                pool.RemoveAt(chosen);
            }

            return offer;
        }

        private static float WeightOf(CommanderSkillDef skill, CommanderProgress progress)
        {
            float behaviour = progress.ScoreFor(skill.Philosophy);
            int invested = progress.OwnedIn(skill.Philosophy, PhilosophyOf);
            return 1f + behaviour * 0.2f + invested * 0.6f;
        }

        // --- Identity ------------------------------------------------------------------------------

        /// <summary>What the men see in their commander — the identity line, never the numbers.</summary>
        public static string IdentityLine(CommanderProgress progress)
        {
            float roman = progress.RomanScore + progress.OwnedIn(CommanderPhilosophy.TrueRoman, PhilosophyOf) * 2f;
            float survivor = progress.SurvivorScore + progress.OwnedIn(CommanderPhilosophy.Survivor, PhilosophyOf) * 2f;
            float barbarian = progress.BarbarianScore + progress.OwnedIn(CommanderPhilosophy.Barbarian, PhilosophyOf) * 2f;

            float top = Mathf.Max(roman, Mathf.Max(survivor, barbarian));
            if (top < 3f) return "Too early to say what kind of commander this march is making of you.";

            bool r = roman >= top * 0.72f;
            bool s = survivor >= top * 0.72f;
            bool b = barbarian >= top * 0.72f;

            if (r && s && b) return "No two of your men agree on what you are becoming.";
            if (r && s) return "A frontier legion: Roman discipline, bent to a land Rome never ruled.";
            if (s && b) return "A wild warband: the Romans would no longer recognise what follows you.";
            if (r && b) return "A new legion: Roman order carried on Germanic ferocity.";
            if (r) return "The men see a true Roman: the answer to every hardship is more discipline.";
            if (s) return "The men see a survivor: no ideology, no glory — tomorrow.";
            return "The men see something the tribes would recognise: ferocity, freedom, belonging.";
        }
    }
}
