using System.Collections.Generic;
using UnityEngine;

namespace Century.Core.Contracts
{
    /// <summary>
    /// The player's answer to the pre-battle question: who fights, where they form up, and where the
    /// rest will come in from.
    /// </summary>
    /// <remarks>
    /// Kept in Core alongside the request and result because it is part of the campaign/battle
    /// contract: eventually the campaign layer will want to pre-fill a default plan (a standing
    /// order, or the last plan used) rather than the battle scene always asking from scratch.
    /// </remarks>
    public sealed class DeploymentPlan
    {
        /// <summary>Soldier ids committed to the opening line. Everyone else is held back.</summary>
        public List<string> DeployedSoldierIds = new List<string>();

        /// <summary>Centre of the vanguard's formation, within the permitted deployment zone.</summary>
        public Vector3 VanguardCentre;

        /// <summary>Facing of the opening line.</summary>
        public Vector3 VanguardFacing = Vector3.forward;

        /// <summary>Point on the map edge where held-back men will arrive when called.</summary>
        public Vector3 ReinforcementPoint;

        public bool HasReinforcementPoint;
    }
}
