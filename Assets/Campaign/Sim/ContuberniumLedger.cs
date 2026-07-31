using System.Collections.Generic;
using Century.Campaign.Model;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// The single authority on which contubernium each man belongs to. The camp's organisation
    /// chart and the battle's squads are both built from this one assignment, so the century the
    /// player manages at camp is exactly the century that forms up on the field.
    /// </summary>
    /// <remarks>
    /// Assignment is lazy and stable: men keep their squad for as long as they live, and anyone new
    /// (recruits, rescued prisoners) is folded into the thinnest existing squad — the way a real
    /// century patched its tent groups — or opens a new one when all are full. The Centurion stands
    /// outside the structure.
    /// </remarks>
    public static class ContuberniumLedger
    {
        /// <summary>Eight men to a tent. The historical contubernium, and now the tactical one.</summary>
        public const int Size = 8;

        /// <summary>Gives every unassigned living man a contubernium. Safe to call often.</summary>
        public static void EnsureAssigned(Roster roster)
        {
            if (roster == null) return;

            List<SoldierRecord> soldiers = roster.Soldiers;
            for (int i = 0; i < soldiers.Count; i++)
            {
                SoldierRecord soldier = soldiers[i];
                if (!soldier.IsAlive || soldier.Contubernium >= 0 || IsCenturion(soldier)) continue;

                soldier.Contubernium = ThinnestSquad(soldiers);
            }
        }

        /// <summary>The squad with the fewest living members under strength, or a fresh index.</summary>
        private static int ThinnestSquad(List<SoldierRecord> soldiers)
        {
            int bestIndex = -1;
            int bestCount = Size;
            int highest = -1;

            for (int index = 0; ; index++)
            {
                int count = 0;
                bool exists = false;

                for (int i = 0; i < soldiers.Count; i++)
                {
                    if (soldiers[i].Contubernium != index) continue;
                    exists = true;
                    if (soldiers[i].IsAlive) count++;
                }

                if (exists)
                {
                    highest = index;
                    if (count < bestCount)
                    {
                        bestCount = count;
                        bestIndex = index;
                    }
                    continue;
                }

                // Ran past the last occupied index: either join the thinnest, or open a new squad.
                return bestIndex >= 0 ? bestIndex : highest + 1;
            }
        }

        /// <summary>Distinct occupied squad indices, sorted, for the living.</summary>
        public static void CollectIndices(Roster roster, List<int> result)
        {
            result.Clear();
            List<SoldierRecord> soldiers = roster.Soldiers;

            for (int i = 0; i < soldiers.Count; i++)
            {
                SoldierRecord soldier = soldiers[i];
                if (!soldier.IsAlive || soldier.Contubernium < 0) continue;
                if (!result.Contains(soldier.Contubernium)) result.Add(soldier.Contubernium);
            }

            result.Sort();
        }

        /// <summary>The appointed Decanus of a squad, or null when the post is unfilled.</summary>
        public static SoldierRecord DecanusOf(Roster roster, int contubernium)
        {
            List<SoldierRecord> soldiers = roster.Soldiers;
            for (int i = 0; i < soldiers.Count; i++)
            {
                SoldierRecord soldier = soldiers[i];
                if (soldier.IsAlive && soldier.Contubernium == contubernium && soldier.IsDecanus)
                    return soldier;
            }

            return null;
        }

        /// <summary>Appoints a man Decanus of his own contubernium, standing down any previous holder.</summary>
        public static void SetDecanus(Roster roster, string soldierId)
        {
            SoldierRecord appointee = roster.Find(soldierId);
            if (appointee == null || appointee.Contubernium < 0) return;

            SoldierRecord previous = null;
            List<SoldierRecord> soldiers = roster.Soldiers;
            for (int i = 0; i < soldiers.Count; i++)
            {
                if (soldiers[i].Contubernium != appointee.Contubernium) continue;
                if (soldiers[i].IsDecanus && !ReferenceEquals(soldiers[i], appointee)) previous = soldiers[i];
                soldiers[i].IsDecanus = false;
            }

            appointee.IsDecanus = true;

            // Tent politics: being replaced as Decanus of your OWN tent, in front of your own
            // men, is a small public humiliation — and the new man wears it.
            if (previous != null && previous.IsAlive)
            {
                RelationshipLedger.Adjust(previous, appointee, -0.2f,
                    "replaced as Decanus of his own tent");
                RelationshipLedger.AdjustLoyalty(previous, -0.05f);
            }

            RelationshipLedger.AdjustLoyalty(appointee, 0.06f);
        }

        private static bool IsCenturion(SoldierRecord soldier) => soldier.RankId == "centurion";
    }
}
