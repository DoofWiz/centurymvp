using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Binds each living man to the formation slot nearest to where he already stands, so a squad that
    /// turns or reforms pivots as a body — nobody sprints across the ranks to a fixed numbered place.
    /// Living men fill the front slots first, so a bloodied squad also closes ranks rather than
    /// fighting around the gaps its dead left.
    /// </summary>
    /// <remarks>
    /// Only run while a squad is manoeuvring (not locked in melee): the slot also serves as a man's
    /// engagement tether, and shuffling tethers mid-fight would make targets flicker. A small
    /// hysteresis keeps a man in his current slot unless another is clearly closer, so equidistant
    /// slots do not make him twitch between them.
    /// </remarks>
    public static class FormationAssignment
    {
        private const int MaxSlots = 32;
        private const float Hysteresis = 0.6f;

        private static readonly List<BattleCombatant> Living = new List<BattleCombatant>(MaxSlots);
        private static readonly Vector3[] SlotPos = new Vector3[MaxSlots];
        private static readonly bool[] SlotTaken = new bool[MaxSlots];
        private static readonly bool[] ManDone = new bool[MaxSlots];

        public static void Reassign(BattleSquad squad, BattleSettings settings)
        {
            if (squad == null || squad.IsRouted || squad.IsWithdrawn) return;

            // Frozen while the line is in contact — see the remarks.
            if (squad.EngagedCount > 0) return;

            Living.Clear();
            List<BattleCombatant> members = squad.Members;
            for (int i = 0; i < members.Count; i++)
                if (members[i].IsAlive) Living.Add(members[i]);

            int k = Living.Count;
            if (k == 0 || k > MaxSlots) return;

            for (int s = 0; s < k; s++)
            {
                SlotPos[s] = SquadFormationSolver.GetWorldSlot(squad, s, settings);
                SlotTaken[s] = false;
                ManDone[s] = false;
            }

            // Global greedy: repeatedly bind the closest remaining (man, slot) pair. A man's current
            // slot is discounted by the hysteresis so he tends to keep it.
            for (int assigned = 0; assigned < k; assigned++)
            {
                int bestMan = -1, bestSlot = -1;
                float bestCost = float.MaxValue;

                for (int m = 0; m < k; m++)
                {
                    if (ManDone[m]) continue;

                    Vector3 pos = Living[m].WorldPosition;
                    int current = Living[m].SlotIndex;

                    for (int s = 0; s < k; s++)
                    {
                        if (SlotTaken[s]) continue;

                        float cost = Vector3.Distance(pos, SlotPos[s]);
                        if (s == current) cost -= Hysteresis;
                        if (cost < bestCost) { bestCost = cost; bestMan = m; bestSlot = s; }
                    }
                }

                if (bestMan < 0) break;

                Living[bestMan].SlotIndex = bestSlot;
                ManDone[bestMan] = true;
                SlotTaken[bestSlot] = true;
            }
        }
    }
}
