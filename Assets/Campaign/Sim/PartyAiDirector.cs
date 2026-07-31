using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Decides what non-player parties do on the overmap: wander, or run something down.
    /// </summary>
    /// <remarks>
    /// The director only ever writes <see cref="PartyState.Destination"/>. It never touches a
    /// NavMeshAgent or a transform. That keeps AI decisions identical whether the overmap scene is
    /// loaded or not, which matters as soon as we want the world to keep turning during a battle.
    /// </remarks>
    public sealed class PartyAiDirector
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;
        private readonly System.Random _random;

        /// <summary>Last seen position of each hunter's quarry, for leading the chase.</summary>
        private readonly Dictionary<string, Vector3> _lastQuarryPosition = new Dictionary<string, Vector3>();

        public PartyAiDirector(CampaignState state, CampaignSettings settings)
        {
            _state = state;
            _settings = settings;
            _random = new System.Random(state.RandomSeed ^ 0x5EED);
        }

        public void Evaluate()
        {
            CampaignTime now = _state.Clock.Now;
            List<PartyState> parties = _state.Parties;

            for (int i = 0; i < parties.Count; i++)
            {
                PartyState party = parties[i];
                if (party.IsPlayer || party.IsDisbanded || !party.CanFight) continue;
                EvaluateParty(party, now);
            }
        }

        private void EvaluateParty(PartyState party, CampaignTime now)
        {
            if (party.AiState == PartyAiState.Pursuing && !ContinuePursuit(party, now)) BreakOff(party, now);

            if (party.AiState != PartyAiState.Pursuing)
            {
                PartyState quarry = FindQuarry(party);

                // Raiders are opportunists: they weigh the odds before they weigh anchor. A weak
                // quarry is chased; a strong one is fled from; near-equal numbers are left alone.
                if (quarry != null && party.Kind == PartyKind.Raiders)
                {
                    float own = party.Roster.CombatReadyCount;
                    float theirs = quarry.Roster.CombatReadyCount;

                    if (theirs > own * 1.15f)
                    {
                        Flee(party, quarry, now);
                        return;
                    }

                    if (theirs > own * 0.85f) quarry = null;   // even odds are bad business
                }

                if (quarry != null)
                {
                    BeginPursuit(party, quarry, now);
                    return;
                }
            }

            if (party.AiState == PartyAiState.Pursuing)
            {
                PartyState target = _state.FindParty(party.PursuitTargetId);
                if (target == null) return;

                // Raiders keep weighing the odds mid-chase: a quarry that rallies to strength — or
                // was misjudged — is dropped and fled from the moment the sums turn sour.
                if (party.Kind == PartyKind.Raiders
                    && target.Roster.CombatReadyCount > party.Roster.CombatReadyCount * 1.15f)
                {
                    Flee(party, target, now);
                    return;
                }

                party.Destination = LeadPursuit(party, target);
                return;
            }

            if (now.TotalMinutes < party.NextDecisionTime.TotalMinutes) return;
            ChooseWander(party, now);
        }

        /// <summary>
        /// Aims ahead of a moving quarry instead of at its heels: the hunter marches for where the
        /// quarry is HEADED, projected further the farther away it is. A standing quarry is simply
        /// marched at.
        /// </summary>
        private Vector3 LeadPursuit(PartyState hunter, PartyState quarry)
        {
            Vector3 current = quarry.WorldPosition;
            Vector3 lead = current;

            if (_lastQuarryPosition.TryGetValue(hunter.Id, out Vector3 previous))
            {
                Vector3 travel = current - previous;
                travel.y = 0f;

                if (travel.sqrMagnitude > 0.01f)
                {
                    float distance = Vector3.Distance(hunter.WorldPosition, current);
                    float reach = Mathf.Min(distance * 0.45f, 60f);
                    lead = _settings.ClampToWorld(current + travel.normalized * reach);
                }
            }

            _lastQuarryPosition[hunter.Id] = current;
            return lead;
        }

        /// <summary>Runs directly away from the threat, well past its detection envelope.</summary>
        private void Flee(PartyState party, PartyState threat, CampaignTime now)
        {
            Vector3 away = party.WorldPosition - threat.WorldPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;

            party.AiState = PartyAiState.Fleeing;
            party.PursuitTargetId = null;
            party.IsCamped = false;
            party.IsStealthed = false;   // fleeing is done openly, at the double
            party.Destination = _settings.ClampToWorld(
                party.WorldPosition + away.normalized * (threat.DetectionRadius * 2f));
            party.NextDecisionTime = now.Plus(_settings.AiDecisionIntervalMinutes * 0.5d);
        }

        /// <summary>Nearest hostile whose visibility exceeds this party's detection range.</summary>
        private PartyState FindQuarry(PartyState hunter)
        {
            PartyState best = null;
            float bestSqr = float.MaxValue;

            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState candidate = parties[i];
                if (candidate.IsDisbanded || !candidate.CanFight) continue;
                if (!PartyRelations.IsHostile(hunter, candidate)) continue;

                float sqr = (candidate.WorldPosition - hunter.WorldPosition).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                float visibility = candidate.VisibilityRadius(_settings.StealthVisibilityMultiplier);
                float range = hunter.DetectionRadius + visibility;

                // The fog is biased in the player's favour: a big Roman column is conspicuous, but
                // the game is better when you SEE the hunt turn toward you than when it starts in
                // the dark. Stealthy stalkers close the gap the honest way.
                if (candidate.IsPlayer)
                {
                    range *= _settings.EnemyDetectionOfPlayerFactor;
                    if (_state.Commander.Has("a_quiet_camp")) range *= 0.8f;
                if (PostTreeCatalog.Invested(_state, "night_watch")) range *= 0.85f;
                if (PostTreeCatalog.Invested(_state, "cold_trails")) range *= 0.9f;
                }

                if (sqr > range * range) continue;

                best = candidate;
                bestSqr = sqr;
            }

            return best;
        }

        private void BeginPursuit(PartyState hunter, PartyState quarry, CampaignTime now)
        {
            hunter.AiState = PartyAiState.Pursuing;
            hunter.PursuitTargetId = quarry.Id;
            hunter.PursuitExpiryTime = now.Plus(_settings.PursuitDurationMinutes);
            hunter.IsCamped = false;
            hunter.Destination = quarry.WorldPosition;
            _lastQuarryPosition.Remove(hunter.Id);

            // Raiders stalk: the chase is made in stealth, so the quarry sees them far too late —
            // which is precisely how a sudden close reveal becomes an ambush.
            hunter.IsStealthed = hunter.Kind == PartyKind.Raiders;
        }

        private bool ContinuePursuit(PartyState hunter, CampaignTime now)
        {
            if (now.TotalMinutes >= hunter.PursuitExpiryTime.TotalMinutes) return false;

            PartyState target = _state.FindParty(hunter.PursuitTargetId);
            if (target == null || target.IsDisbanded || !target.CanFight) return false;

            // Lost contact: the quarry outran the hunter's detection envelope.
            float visibility = target.VisibilityRadius(_settings.StealthVisibilityMultiplier);
            float range = hunter.DetectionRadius + visibility;
            if (target.IsPlayer)
            {
                range *= _settings.EnemyDetectionOfPlayerFactor;
                if (_state.Commander.Has("a_quiet_camp")) range *= 0.8f;
                if (PostTreeCatalog.Invested(_state, "night_watch")) range *= 0.85f;
                if (PostTreeCatalog.Invested(_state, "cold_trails")) range *= 0.9f;
            }

            float leash = range * 1.75f;
            return (target.WorldPosition - hunter.WorldPosition).sqrMagnitude <= leash * leash;
        }

        private void BreakOff(PartyState party, CampaignTime now)
        {
            party.AiState = PartyAiState.Idle;
            party.PursuitTargetId = null;
            party.Destination = null;
            party.IsStealthed = false;
            party.NextDecisionTime = now.Plus(NextInterval());
            _lastQuarryPosition.Remove(party.Id);
        }

        private void ChooseWander(PartyState party, CampaignTime now)
        {
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(
                _settings.WanderMinDistance, _settings.WanderMaxDistance, (float)_random.NextDouble());

            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            party.AiState = PartyAiState.Wandering;
            party.Destination = _settings.ClampToWorld(party.WorldPosition + offset);
            party.NextDecisionTime = now.Plus(NextInterval());
        }

        private double NextInterval()
        {
            double jitter = 0.6d + _random.NextDouble() * 0.8d;
            return _settings.AiDecisionIntervalMinutes * jitter;
        }
    }
}
