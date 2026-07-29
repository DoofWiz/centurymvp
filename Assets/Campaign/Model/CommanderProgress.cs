using System;
using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>The three philosophies a commander can grow into. Order matters nowhere; identity
    /// emerges from behaviour, not from picking a lane.</summary>
    public enum CommanderPhilosophy
    {
        /// <summary>Order, discipline, empire: the answer to hardship is more Rome.</summary>
        TrueRoman = 0,
        /// <summary>Adaptation, cunning, endurance: ideology is a luxury; tomorrow is not.</summary>
        Survivor = 1,
        /// <summary>Ferocity, freedom, belonging: perhaps Rome is not where we belong anymore.</summary>
        Barbarian = 2
    }

    /// <summary>
    /// The commander's evolution: which doctrines they hold, and the running record of HOW they have
    /// led — every retreat, drill, ambush and trophy nudges the philosophy scores that weight what
    /// the game offers them next. The scores are never shown to the player; they read only the
    /// identity the men see in them.
    /// </summary>
    [Serializable]
    public sealed class CommanderProgress
    {
        /// <summary>Doctrines the commander has taken, by catalog id.</summary>
        public List<string> OwnedSkills = new List<string>();

        /// <summary>The standing three-choice offer, persisted so it cannot be re-rolled by
        /// closing the screen. Empty means "generate on next look".</summary>
        public List<string> CurrentOffer = new List<string>();

        /// <summary>Behaviour scores per philosophy. Hidden telemetry, not a stat.</summary>
        public float RomanScore;
        public float SurvivorScore;
        public float BarbarianScore;

        public bool Has(string skillId) => OwnedSkills.Contains(skillId);

        /// <summary>Records an act of leadership in the given style.</summary>
        public void Note(CommanderPhilosophy philosophy, float weight)
        {
            switch (philosophy)
            {
                case CommanderPhilosophy.TrueRoman: RomanScore += weight; break;
                case CommanderPhilosophy.Survivor: SurvivorScore += weight; break;
                case CommanderPhilosophy.Barbarian: BarbarianScore += weight; break;
            }
        }

        public float ScoreFor(CommanderPhilosophy philosophy)
        {
            switch (philosophy)
            {
                case CommanderPhilosophy.TrueRoman: return RomanScore;
                case CommanderPhilosophy.Survivor: return SurvivorScore;
                default: return BarbarianScore;
            }
        }

        /// <summary>Owned doctrines of one philosophy — investment begets recognition.</summary>
        public int OwnedIn(CommanderPhilosophy philosophy, Func<string, CommanderPhilosophy?> philosophyOf)
        {
            int count = 0;
            for (int i = 0; i < OwnedSkills.Count; i++)
                if (philosophyOf(OwnedSkills[i]) == philosophy) count++;
            return count;
        }
    }
}
