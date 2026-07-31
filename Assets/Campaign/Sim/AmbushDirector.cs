using System.Collections.Generic;
using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Watches for the moment an ambush is born, on BOTH sides of the fog.
    /// </summary>
    /// <remarks>
    /// The player's side: a hostile that was invisible to the column suddenly revealed CLOSE opens a
    /// surprise window on the player (<see cref="PartyState.SurprisedUntil"/>) — battle joined inside
    /// it is an ambush against the column.
    ///
    /// The enemy's side is the mirror: each hostile party has a detection envelope (shrunk hard by
    /// the player's stealth). If the column crosses from undetected to detected already INSIDE the
    /// ambush range — a stealthy approach that got close before it was marked — the surprise window
    /// opens on THAT party instead, and battle joined inside it lets the player spring the trap.
    /// The HUD draws depleting wheels from both.
    /// </remarks>
    public sealed class AmbushDirector
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;

        /// <summary>Party ids that were visible to the column on the previous tick.</summary>
        private readonly HashSet<string> _wasVisibleToPlayer = new HashSet<string>();

        /// <summary>Party ids that had already detected the column on the previous tick.</summary>
        private readonly HashSet<string> _wasAwareOfPlayer = new HashSet<string>();

        public AmbushDirector(CampaignState state, CampaignSettings settings)
        {
            _state = state;
            _settings = settings;
        }

        public void Tick()
        {
            PartyState player = _state.PlayerParty;
            if (player == null || player.IsDisbanded) return;

            float playerVisibility = player.VisibilityRadius(_settings.StealthVisibilityMultiplier);

            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState other = parties[i];
                if (other.IsPlayer || other.IsDisbanded || !other.CanFight) continue;
                if (!PartyRelations.IsHostile(player, other)) continue;

                float distance = Vector3.Distance(other.WorldPosition, player.WorldPosition);

                TickPlayerSurprise(player, other, distance);
                TickEnemySurprise(player, other, distance, playerVisibility);
            }
        }

        /// <summary>A hostile revealed suddenly close catches the COLUMN flat.</summary>
        private void TickPlayerSurprise(PartyState player, PartyState other, float distance)
        {
            bool visible = LineOfSight.Visibility01(_state, other) > 0.05f;
            bool wasVisible = _wasVisibleToPlayer.Contains(other.Id);

            if (visible && !wasVisible && distance <= _settings.AmbushRevealRange)
                player.SurprisedUntil = _state.Clock.Now.Plus(_settings.SurpriseWindowMinutes);

            if (visible) _wasVisibleToPlayer.Add(other.Id);
            else _wasVisibleToPlayer.Remove(other.Id);
        }

        /// <summary>The column detected suddenly close — stealth that worked — catches THEM flat.</summary>
        private void TickEnemySurprise(
            PartyState player, PartyState other, float distance, float playerVisibility)
        {
            // Same biased envelope the pursuit AI uses, so "aware" means the same thing everywhere.
            float envelope = (other.DetectionRadius + playerVisibility) * _settings.EnemyDetectionOfPlayerFactor;
            if (_state.Commander.Has("a_quiet_camp")) envelope *= 0.8f;
            // Night Watch: sentries posted by the book — the column is harder to mark.
            if (PostTreeCatalog.Invested(_state, "night_watch")) envelope *= 0.85f;
            // Cold Trails: the speculator sweeps the column's sign; hunters mark it late.
            if (PostTreeCatalog.Invested(_state, "cold_trails")) envelope *= 0.9f;
            bool aware = distance <= envelope;
            bool wasAware = _wasAwareOfPlayer.Contains(other.Id);

            if (aware && !wasAware && distance <= _settings.AmbushRevealRange)
                other.SurprisedUntil = _state.Clock.Now.Plus(_settings.SurpriseWindowMinutes);

            if (aware) _wasAwareOfPlayer.Add(other.Id);
            else _wasAwareOfPlayer.Remove(other.Id);
        }
    }
}
