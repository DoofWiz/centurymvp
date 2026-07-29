using System;
using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>The men of a party. "73 / 120" in the HUD is <see cref="ActiveCount"/> / <see cref="Capacity"/>.</summary>
    [Serializable]
    public sealed class Roster
    {
        public List<SoldierRecord> Soldiers = new List<SoldierRecord>();

        /// <summary>Nominal paper strength of the unit. A century is 80 men; 120 allows attachments.</summary>
        public int Capacity = 120;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Soldiers.Count; i++)
                    if (Soldiers[i].IsAlive) count++;
                return count;
            }
        }

        public int CombatReadyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Soldiers.Count; i++)
                    if (Soldiers[i].IsCombatReady) count++;
                return count;
            }
        }

        public int WoundedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Soldiers.Count; i++)
                    if (Soldiers[i].IsWounded) count++;
                return count;
            }
        }

        public float WoundedFraction
        {
            get
            {
                int active = ActiveCount;
                return active <= 0 ? 0f : (float)WoundedCount / active;
            }
        }

        /// <summary>Average morale across living men. Feeds the party morale band in the HUD.</summary>
        public float AverageMorale01
        {
            get
            {
                float total = 0f;
                int active = 0;
                for (int i = 0; i < Soldiers.Count; i++)
                {
                    if (!Soldiers[i].IsAlive) continue;
                    total += Soldiers[i].Morale01;
                    active++;
                }

                return active <= 0 ? 0f : total / active;
            }
        }

        public SoldierRecord Find(string soldierId)
        {
            for (int i = 0; i < Soldiers.Count; i++)
                if (Soldiers[i].Id == soldierId) return Soldiers[i];
            return null;
        }

        public SoldierRecord FindByRank(string rankId)
        {
            for (int i = 0; i < Soldiers.Count; i++)
                if (Soldiers[i].IsAlive && Soldiers[i].RankId == rankId) return Soldiers[i];
            return null;
        }

        public void Add(SoldierRecord soldier)
        {
            if (soldier == null) throw new ArgumentNullException(nameof(soldier));
            Soldiers.Add(soldier);
        }

        /// <summary>Removes the dead from the active list. Call after applying a battle result.</summary>
        public List<SoldierRecord> RemoveFallen()
        {
            var fallen = new List<SoldierRecord>();
            for (int i = Soldiers.Count - 1; i >= 0; i--)
            {
                if (Soldiers[i].IsAlive) continue;
                fallen.Add(Soldiers[i]);
                Soldiers.RemoveAt(i);
            }

            return fallen;
        }
    }
}
