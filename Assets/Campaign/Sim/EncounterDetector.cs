using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core;

namespace Century.Campaign.Sim
{
    /// <summary>A pair of parties that have collided on the overmap.</summary>
    public readonly struct EncounterContact
    {
        public readonly PartyState Player;
        public readonly PartyState Enemy;

        /// <summary>True when the enemy closed the distance, which starts the battle as an ambush.</summary>
        public readonly bool EnemyInitiated;

        public EncounterContact(PartyState player, PartyState enemy, bool enemyInitiated)
        {
            Player = player;
            Enemy = enemy;
            EnemyInitiated = enemyInitiated;
        }
    }

    /// <summary>
    /// Finds parties that have come within contact range of one another.
    /// </summary>
    /// <remarks>
    /// Only player-involving contacts are reported. AI-versus-AI fights are a later feature and
    /// should be resolved abstractly rather than by loading a battle scene nobody is watching.
    /// The scan is O(n) against the player; when the world grows past a few hundred parties this
    /// wants a spatial hash, but the interface will not change when it does.
    /// </remarks>
    public sealed class EncounterDetector
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;

        public EncounterDetector(CampaignState state, CampaignSettings settings)
        {
            _state = state;
            _settings = settings;
        }

        public bool TryFindContact(out EncounterContact contact)
        {
            contact = default;

            PartyState player = _state.PlayerParty;
            if (player == null || player.IsDisbanded || !player.CanFight) return false;

            CampaignTime now = _state.Clock.Now;
            if (now.TotalMinutes < player.ContactCooldownUntil.TotalMinutes) return false;

            float contactSqr = _settings.ContactRadius * _settings.ContactRadius;

            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState other = parties[i];
                if (other.IsDisbanded || !other.CanFight) continue;
                if (!PartyRelations.IsHostile(player, other)) continue;
                if (now.TotalMinutes < other.ContactCooldownUntil.TotalMinutes) continue;

                if ((other.WorldPosition - player.WorldPosition).sqrMagnitude > contactSqr) continue;

                bool enemyInitiated = other.AiState == PartyAiState.Pursuing
                                      && other.PursuitTargetId == player.Id;

                contact = new EncounterContact(player, other, enemyInitiated);
                return true;
            }

            return false;
        }
    }
}
