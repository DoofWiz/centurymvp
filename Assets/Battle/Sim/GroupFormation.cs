using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// The layer the squad system was missing: when the SAME formation order goes to SEVERAL squads,
    /// they dress as a group instead of each executing it alone where it stands. Squads never merge —
    /// each keeps its own block, officers and cohesion — but their anchors are laid out together:
    /// a Line order forms one continuous front; a Double Line forms the duplex acies, a second rank
    /// of squads covering the gaps of the first; a Column strings them out along the line of march.
    /// </summary>
    /// <remarks>
    /// Pure geometry over squad anchors — the per-man solver still owns everything inside a squad,
    /// and a single selected squad is left exactly where it stands (no group to dress with). The
    /// group faces the nearest enemy concentration, because dressing ranks facing the wrong way is
    /// a parade, not a battle line.
    /// </remarks>
    public static class GroupFormation
    {
        /// <summary>Gap left between neighbouring squad blocks, in metres.</summary>
        private const float BlockGap = 1.4f;

        /// <summary>Extra depth between the front line and the covering line of a duplex acies.</summary>
        private const float RankGap = 2.4f;

        private static readonly List<BattleSquad> Sorted = new List<BattleSquad>(16);

        /// <summary>
        /// Anchor target for each squad of <paramref name="group"/> under <paramref name="formation"/>,
        /// written into <paramref name="targets"/> in the same order. No-op for fewer than two squads.
        /// </summary>
        public static void Arrange(
            List<BattleSquad> group, FormationType formation, BattleState state,
            BattleSettings settings, List<Vector3> targets)
        {
            targets.Clear();
            for (int i = 0; i < group.Count; i++) targets.Add(group[i].AnchorPosition);
            if (group.Count < 2) return;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < group.Count; i++) centre += group[i].AnchorPosition;
            centre /= group.Count;

            Vector3 facing = FacingFor(group, centre, state);
            Vector3 right = Vector3.Cross(Vector3.up, facing);

            // Stable ordering: each squad keeps its place in the line relative to its neighbours.
            Sorted.Clear();
            Sorted.AddRange(group);
            Sorted.Sort((a, b) =>
                Vector3.Dot(a.AnchorPosition - centre, right)
                    .CompareTo(Vector3.Dot(b.AnchorPosition - centre, right)));

            float width = settings.LineWidth * SpacingFor(formation, settings).x + BlockGap;
            float depth = 2f * SpacingFor(formation, settings).y + RankGap;

            for (int i = 0; i < Sorted.Count; i++)
            {
                Vector3 local = formation == FormationType.DoubleLine ? DuplexSlot(i, Sorted.Count, width, depth)
                    : formation == FormationType.Column ? new Vector3(0f, 0f, -i * depth)
                    : SingleRankSlot(i, Sorted.Count, width);

                Vector3 world = centre + right * local.x + facing * local.z;

                // Write the target back against the CALLER'S order, not the sorted one.
                int original = group.IndexOf(Sorted[i]);
                if (original >= 0) targets[original] = world;
            }
        }

        /// <summary>One continuous front: blocks abreast, centred on the group.</summary>
        private static Vector3 SingleRankSlot(int index, int count, float width) =>
            new Vector3((index - (count - 1) * 0.5f) * width, 0f, 0f);

        /// <summary>
        /// The duplex acies. Front line takes the even positions of the sorted order, the covering
        /// line the odd ones, stood behind the gaps of the front — so every seam in the first line
        /// has a fresh squad's shields behind it.
        /// </summary>
        private static Vector3 DuplexSlot(int index, int count, float width, float depth)
        {
            int frontCount = (count + 1) / 2;

            if (index % 2 == 0)
            {
                int front = index / 2;
                return new Vector3((front - (frontCount - 1) * 0.5f) * width, 0f, 0f);
            }

            // Rear squad i stands behind the gap between front squads i and i+1.
            int rear = index / 2;
            float left = (rear - (frontCount - 1) * 0.5f) * width;
            return new Vector3(left + width * 0.5f, 0f, -depth);
        }

        /// <summary>Face the nearest effective enemy concentration; failing that, keep the current front.</summary>
        private static Vector3 FacingFor(List<BattleSquad> group, Vector3 centre, BattleState state)
        {
            List<BattleSquad> hostiles = group[0].IsPlayerSide ? state.EnemySquads : state.PlayerSquads;

            Vector3 threat = Vector3.zero;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < hostiles.Count; i++)
            {
                if (!hostiles[i].IsEffective || hostiles[i].IsOffField) continue;
                Vector3 hostileCentre = hostiles[i].CentreOfMass();
                float sqr = (hostileCentre - centre).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                threat = hostileCentre;
            }

            if (bestSqr < float.MaxValue)
            {
                Vector3 to = threat - centre;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) return to.normalized;
            }

            Vector3 average = Vector3.zero;
            for (int i = 0; i < group.Count; i++) average += SquadFormationSolver.FlatFacing(group[i].AnchorFacing);
            return SquadFormationSolver.FlatFacing(average);
        }

        private static Vector2 SpacingFor(FormationType formation, BattleSettings settings)
        {
            switch (formation)
            {
                case FormationType.Testudo: return settings.TestudoSpacing;
                case FormationType.Loose: return settings.LooseSpacing;
                case FormationType.Column: return settings.ColumnSpacing;
                default: return settings.LineSpacing;
            }
        }
    }
}
