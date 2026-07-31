using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// The one gate through which relationships move. Total War does not care what your men think
    /// of you; Century is seventy men in hostile country, and it does. Every system that pleases
    /// one man and slights another — battle outcomes, promotions, appointments, choices at a
    /// crossroads shrine — writes it here, and the soldier panel reads it back with reasons.
    /// </summary>
    public static class RelationshipLedger
    {
        /// <summary>Ties per man. Enough for a social circle; the weakest is evicted past this.</summary>
        private const int MaxTies = 12;

        /// <summary>Moves how <paramref name="man"/> regards <paramref name="other"/>. Directional:
        /// call <see cref="Bond"/> for the mutual version. The reason sticks when the move is big
        /// enough to be worth remembering.</summary>
        public static void Adjust(SoldierRecord man, SoldierRecord other, float delta, string why = null)
        {
            if (man == null || other == null || ReferenceEquals(man, other)) return;
            if (string.IsNullOrEmpty(other.Id)) return;

            RelationTie tie = null;
            for (int i = 0; i < man.Ties.Count; i++)
                if (man.Ties[i].OtherId == other.Id) { tie = man.Ties[i]; break; }

            if (tie == null)
            {
                if (man.Ties.Count >= MaxTies) EvictWeakest(man);
                tie = new RelationTie { OtherId = other.Id };
                man.Ties.Add(tie);
            }

            tie.Value = Mathf.Clamp(tie.Value + delta, -1f, 1f);
            if (!string.IsNullOrEmpty(why) && Mathf.Abs(delta) >= 0.05f) tie.Why = why;
        }

        /// <summary>The mutual form: both men move the same way (marching, fighting, surviving).</summary>
        public static void Bond(SoldierRecord a, SoldierRecord b, float delta, string why = null)
        {
            Adjust(a, b, delta, why);
            Adjust(b, a, delta, why);
        }

        /// <summary>How a man regards the CENTURION. Loyalty already existed; this is its one gate.</summary>
        public static void AdjustLoyalty(SoldierRecord man, float delta)
        {
            if (man == null) return;
            man.Loyalty01 = Mathf.Clamp01(man.Loyalty01 + delta);
        }

        public static float TieValue(SoldierRecord man, string otherId)
        {
            if (man == null) return 0f;
            for (int i = 0; i < man.Ties.Count; i++)
                if (man.Ties[i].OtherId == otherId) return man.Ties[i].Value;
            return 0f;
        }

        /// <summary>The band word the UI shows. Words, never percentages.</summary>
        public static string Band(float value)
        {
            if (value >= 0.6f) return "Brother in arms";
            if (value >= 0.3f) return "Fast friend";
            if (value >= 0.1f) return "Comrade";
            if (value > -0.1f) return "Indifferent";
            if (value > -0.3f) return "Cool";
            if (value > -0.6f) return "A grudge";
            return "Bad blood";
        }

        /// <summary>How the man's regard for the Centurion reads, in words.</summary>
        public static string AttitudeWord(float loyalty01)
        {
            if (loyalty01 >= 0.8f) return "Devoted to you";
            if (loyalty01 >= 0.6f) return "Loyal";
            if (loyalty01 >= 0.4f) return "Dutiful";
            if (loyalty01 >= 0.2f) return "Resentful";
            return "Near mutiny";
        }

        public static string AttitudeColorClass(float loyalty01) =>
            loyalty01 >= 0.6f ? "text-good" : loyalty01 >= 0.4f ? "text-dim" : "text-danger";

        /// <summary>USS class for the band, or null for the plain default.</summary>
        public static string BandColorClass(float value)
        {
            if (value >= 0.3f) return "text-good";
            if (value >= 0.1f) return null;
            if (value > -0.3f) return "text-faint";
            return "text-danger";
        }

        private static void EvictWeakest(SoldierRecord man)
        {
            int weakest = 0;
            for (int i = 1; i < man.Ties.Count; i++)
                if (Mathf.Abs(man.Ties[i].Value) < Mathf.Abs(man.Ties[weakest].Value)) weakest = i;
            man.Ties.RemoveAt(weakest);
        }
    }
}
