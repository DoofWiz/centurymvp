using System;
using System.Collections.Generic;

namespace Century.Campaign.Model
{
    /// <summary>
    /// Who holds each senior post in the century. Lives on the <see cref="PartyState"/> so an
    /// appointment made at camp survives the walk back to the overmap and into the next battle.
    /// </summary>
    /// <remarks>
    /// Stored as a small list of pairs rather than a dictionary so it serialises cleanly through
    /// every path Unity might take (JsonUtility included), matching how <see cref="Roster"/> keeps a
    /// plain <c>List</c>. The list only ever holds one entry per role.
    /// </remarks>
    [Serializable]
    public sealed class CampAppointments
    {
        [Serializable]
        public struct Appointment
        {
            public CampRole Role;
            public string SoldierId;
        }

        public List<Appointment> Held = new List<Appointment>();

        /// <summary>Soldier id filling the role, or null if the post is vacant.</summary>
        public string GetHolderId(CampRole role)
        {
            for (int i = 0; i < Held.Count; i++)
                if (Held[i].Role == role) return Held[i].SoldierId;
            return null;
        }

        public bool IsFilled(CampRole role) => !string.IsNullOrEmpty(GetHolderId(role));

        /// <summary>Whether a given man currently holds any post. Used to keep one man to one job.</summary>
        public bool HoldsAnyRole(string soldierId, out CampRole role)
        {
            for (int i = 0; i < Held.Count; i++)
            {
                if (Held[i].SoldierId != soldierId) continue;
                role = Held[i].Role;
                return true;
            }

            role = default;
            return false;
        }

        /// <summary>
        /// Appoints <paramref name="soldierId"/> to <paramref name="role"/>. A man can only hold one
        /// post, so he is quietly stood down from any other first, and whoever held this post is
        /// displaced. Pass a null id to leave the post vacant.
        /// </summary>
        public void Assign(CampRole role, string soldierId)
        {
            if (!string.IsNullOrEmpty(soldierId))
                RemoveHolder(soldierId);

            for (int i = 0; i < Held.Count; i++)
            {
                if (Held[i].Role != role) continue;
                if (string.IsNullOrEmpty(soldierId)) Held.RemoveAt(i);
                else Held[i] = new Appointment { Role = role, SoldierId = soldierId };
                return;
            }

            if (!string.IsNullOrEmpty(soldierId))
                Held.Add(new Appointment { Role = role, SoldierId = soldierId });
        }

        public void Clear(CampRole role) => Assign(role, null);

        private void RemoveHolder(string soldierId)
        {
            for (int i = Held.Count - 1; i >= 0; i--)
                if (Held[i].SoldierId == soldierId) Held.RemoveAt(i);
        }
    }
}
