using System;
using System.Collections.Generic;
using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Reveals points of interest as the column comes within sight of them, and raises a trigger when
    /// the column actually arrives at one. Plain C#, ticked by the overmap director alongside the AI
    /// and encounter detectors — the same heartbeat, so it costs nothing extra.
    /// </summary>
    public sealed class PoiDirector
    {
        /// <summary>How close the column must come to a POI to set its event in motion, in world units.</summary>
        private const float TriggerRadius = 12f;

        private readonly CampaignState _state;
        private readonly CampaignEventLog _log;

        /// <summary>Raised when the column reaches a discovered, unresolved POI.</summary>
        public event Action<PointOfInterest> Triggered;

        public PoiDirector(CampaignState state, CampaignEventLog log)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _log = log;
        }

        /// <summary>Returns true if a POI fired this tick, so the caller can skip other work.</summary>
        public bool Evaluate()
        {
            PartyState player = _state.PlayerParty;
            if (player == null || player.IsDisbanded) return false;

            float detectSqr = player.DetectionRadius * player.DetectionRadius;
            const float triggerSqr = TriggerRadius * TriggerRadius;

            List<PointOfInterest> pois = _state.PointsOfInterest;
            for (int i = 0; i < pois.Count; i++)
            {
                PointOfInterest poi = pois[i];
                if (poi.Resolved) continue;

                // FLAT distance: the player stands at terrain height, and a POI authored at y = 0
                // under a 15m hill would otherwise be unreachable — the vertical gap alone can
                // exceed the trigger radius. Proximity on a map is a ground-plan question.
                Vector3 delta = poi.WorldPosition - player.WorldPosition;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;

                if (!poi.Discovered)
                {
                    if (sqr > detectSqr) continue;
                    poi.Discovered = true;
                    _log?.Push(CampaignEventKind.Discovery,
                        $"Sighted: {poi.DisplayName}", "A place worth a look", _state.Clock.Now.DayNumber);
                }

                if (sqr <= triggerSqr)
                {
                    Triggered?.Invoke(poi);
                    return true;
                }
            }

            return false;
        }
    }
}
