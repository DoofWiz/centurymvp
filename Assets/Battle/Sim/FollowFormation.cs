using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Assigns each following squad to the flank position nearest where it already stands, so "Follow
    /// Me" squads take the closest place beside the Centurion instead of a fixed numbered one — and,
    /// when he turns, hold their world position rather than swapping sides down the whole line. Every
    /// squad runs the same deterministic assignment off shared state, so they agree without a manager.
    /// </summary>
    public static class FollowFormation
    {
        private const int MaxFollowers = 16;

        // A squad keeps its current flank unless another is clearly closer, so a turn doesn't churn.
        private const float Hysteresis = 1.5f;

        private static readonly List<BattleSquad> Followers = new List<BattleSquad>(MaxFollowers);
        private static readonly float[] Offset = new float[MaxFollowers];
        private static readonly Vector3[] World = new Vector3[MaxFollowers];
        private static readonly bool[] SlotTaken = new bool[MaxFollowers];
        private static readonly bool[] SquadDone = new bool[MaxFollowers];

        /// <summary>The lateral offset <paramref name="self"/> should take beside the Centurion this frame.</summary>
        public static float LateralFor(
            BattleSquad self, List<BattleSquad> playerSquads,
            Vector3 playerPos, Vector3 playerFacing, BattleSettings settings)
        {
            Followers.Clear();
            for (int i = 0; i < playerSquads.Count; i++)
            {
                BattleSquad s = playerSquads[i];
                if (s.Order == SquadOrder.FollowMe && s.IsEffective && !s.IsOffField) Followers.Add(s);
            }

            int n = Followers.Count;
            if (n == 0) return self.FollowLateral;
            if (n > MaxFollowers) n = MaxFollowers;

            Vector3 flat = SquadFormationSolver.FlatFacing(playerFacing);
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 ahead = flat * (settings.FollowDistance * 0.9f);

            for (int j = 0; j < n; j++)
            {
                Offset[j] = FlankOffset(j, ScreenSpacing(settings));
                World[j] = playerPos + right * Offset[j] + ahead;
                SlotTaken[j] = false;
                SquadDone[j] = false;
            }

            float selfLateral = Offset[0];

            // Global greedy: bind the closest remaining (squad, slot) pair, biased to keep a squad where
            // it already is so the assignment does not flicker frame to frame.
            for (int a = 0; a < n; a++)
            {
                int bestSquad = -1, bestSlot = -1;
                float bestCost = float.MaxValue;

                for (int s = 0; s < n; s++)
                {
                    if (SquadDone[s]) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (SlotTaken[j]) continue;

                        float cost = Vector3.Distance(Followers[s].AnchorPosition, World[j]);
                        if (Mathf.Approximately(Offset[j], Followers[s].FollowLateral)) cost -= Hysteresis;
                        if (cost < bestCost) { bestCost = cost; bestSquad = s; bestSlot = j; }
                    }
                }

                if (bestSquad < 0) break;

                SquadDone[bestSquad] = true;
                SlotTaken[bestSlot] = true;
                Followers[bestSquad].FollowLateral = Offset[bestSlot];
                if (Followers[bestSquad] == self) selfLateral = Offset[bestSlot];
            }

            return selfLateral;
        }

        /// <summary>
        /// The following line stands a squad's actual fighting width apart, not the looser deployment
        /// frontage — a screen, not a parade — and it now INCLUDES the centre slot: the line forms in
        /// front of the Centurion, who commands from behind it rather than standing in a gap in it.
        /// </summary>
        private static float ScreenSpacing(BattleSettings settings) =>
            settings.LineWidth * settings.LineSpacing.x + 1.4f;

        /// <summary>0, -F, +F, -2F, +2F ... centre first, then flanks outward.</summary>
        private static float FlankOffset(int index, float frontage)
        {
            if (index == 0) return 0f;
            int step = (index + 1) / 2;
            float sign = index % 2 == 1 ? -1f : 1f;
            return step * frontage * sign;
        }
    }
}
