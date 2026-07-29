using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Produces the position each man should occupy within his squad's formation.
    /// </summary>
    /// <remarks>
    /// Slots are computed in formation-local space — +X to the right of the line, +Z forward — and
    /// transformed by the squad's anchor. Keeping it side-effect free and static means formation
    /// shapes can be unit tested and previewed in the editor without a running battle.
    ///
    /// Slot 0 is always the front-centre position, so the man in slot 0 is the one the enemy meets
    /// first. Squad ordering should therefore put the steadiest man there.
    /// </remarks>
    public static class SquadFormationSolver
    {
        public static Vector3 GetWorldSlot(BattleSquad squad, int slotIndex, BattleSettings settings)
        {
            Vector3 local = GetLocalSlot(squad.Formation, slotIndex, squad.Members.Count, settings);
            Quaternion rotation = Quaternion.LookRotation(FlatFacing(squad.AnchorFacing), Vector3.up);
            return squad.AnchorPosition + rotation * local;
        }

        public static Vector3 GetLocalSlot(
            FormationType formation, int slotIndex, int squadSize, BattleSettings settings)
        {
            if (slotIndex < 0) slotIndex = 0;

            switch (formation)
            {
                case FormationType.Testudo: return Grid(slotIndex, TestudoWidth(squadSize), settings.TestudoSpacing);
                case FormationType.Wedge: return Wedge(slotIndex, settings.WedgeSpacing);
                case FormationType.Loose: return Scattered(slotIndex, settings.LooseSpacing);
                case FormationType.Column: return Grid(slotIndex, 2, settings.ColumnSpacing);
                default: return Grid(slotIndex, settings.LineWidth, settings.LineSpacing);
            }
        }

        /// <summary>Rows of fixed width, centred on the anchor, extending backwards.</summary>
        private static Vector3 Grid(int slotIndex, int width, Vector2 spacing)
        {
            width = Mathf.Max(1, width);
            int row = slotIndex / width;
            int column = slotIndex % width;

            // Centre each row. A partial rear rank centres on its own count so the line looks dressed.
            float centreOffset = (width - 1) * 0.5f;
            float x = (column - centreOffset) * spacing.x;
            float z = -row * spacing.y;

            return new Vector3(x, 0f, z);
        }

        /// <summary>Triangular point-forward: row r holds r+1 men.</summary>
        private static Vector3 Wedge(int slotIndex, Vector2 spacing)
        {
            int row = 0;
            int consumed = 0;

            while (consumed + row + 1 <= slotIndex)
            {
                consumed += row + 1;
                row++;
            }

            int column = slotIndex - consumed;
            float centreOffset = row * 0.5f;

            return new Vector3((column - centreOffset) * spacing.x, 0f, -row * spacing.y);
        }

        /// <summary>
        /// Open order with deterministic jitter. Derived from the slot index rather than Random so a
        /// squad's scatter pattern is stable frame to frame — men should not jiggle in place.
        /// </summary>
        private static Vector3 Scattered(int slotIndex, Vector2 spacing)
        {
            const int width = 4;
            Vector3 basePosition = Grid(slotIndex, width, spacing);

            float jitterX = Hash01(slotIndex * 2 + 1) - 0.5f;
            float jitterZ = Hash01(slotIndex * 2 + 2) - 0.5f;

            basePosition.x += jitterX * spacing.x * 0.6f;
            basePosition.z += jitterZ * spacing.y * 0.6f;
            return basePosition;
        }

        private static int TestudoWidth(int squadSize) =>
            Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, squadSize))));

        private static float Hash01(int value)
        {
            unchecked
            {
                int hash = value * 73856093;
                hash ^= hash >> 13;
                hash *= 19349663;
                hash ^= hash >> 16;
                return (hash & 0xFFFF) / 65535f;
            }
        }

        public static Vector3 FlatFacing(Vector3 facing)
        {
            facing.y = 0f;
            return facing.sqrMagnitude < 0.0001f ? Vector3.forward : facing.normalized;
        }
    }
}
