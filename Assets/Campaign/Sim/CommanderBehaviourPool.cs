using Century.Core.Contracts;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Draws a commander doctrine for a newly spawned hostile band. Every path that creates a band a
    /// player can fight — wandering warbands, POI raiders, event ambushers — goes through here, so no
    /// fight ever opens without a temperament behind it.
    /// </summary>
    /// <remarks>
    /// Weighted rather than uniform: most bands fight in the ordinary reckless or disciplined way, and
    /// the rarer skirmishing and wary commanders are the ones that make the player change their plan.
    /// </remarks>
    public static class CommanderBehaviourPool
    {
        public static CommanderBehaviour Pick()
        {
            // Roughly: Fanatic 40%, Disciplined 30%, Skirmisher 20%, Wary 10%.
            float roll = Random.value;
            if (roll < 0.40f) return CommanderBehaviour.Fanatic;
            if (roll < 0.70f) return CommanderBehaviour.Disciplined;
            if (roll < 0.90f) return CommanderBehaviour.Skirmisher;
            return CommanderBehaviour.Wary;
        }
    }
}
