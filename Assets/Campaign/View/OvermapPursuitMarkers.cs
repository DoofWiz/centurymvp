using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// A pulsing red ring under any VISIBLE party that is currently hunting the column. "That
    /// warband is coming for you" was only discoverable by hovering each marker; a predator should
    /// read as a predator from across the map — but only once the column can actually see it, so
    /// the fog of war gives up nothing it should not.
    /// </summary>
    public sealed class OvermapPursuitMarkers : MonoBehaviour
    {
        private const int Segments = 36;
        private const float Radius = 2.6f;

        private static readonly Color RingColour = new Color(0.85f, 0.24f, 0.18f);

        private CampaignState _state;
        private Material _material;
        private readonly List<LineRenderer> _rings = new List<LineRenderer>();

        public void Bind(CampaignState state)
        {
            _state = state;

            _material = Century.Core.World.FxMaterials.VertexTinted();
        }

        private void LateUpdate()
        {
            if (_state?.PlayerParty == null) return;

            string playerId = _state.PlayerParty.Id;
            int used = 0;

            for (int i = 0; i < _state.Parties.Count; i++)
            {
                PartyState party = _state.Parties[i];
                if (party.IsPlayer || party.IsDisbanded) continue;
                if (party.PursuitTargetId != playerId) continue;

                // Beyond the line of sight the hunter stays a secret; half-seen, the ring is faint.
                float visibility = LineOfSight.Visibility01(_state, party.WorldPosition);
                if (visibility < 0.3f) continue;

                LineRenderer ring = GetRing(used++);
                ring.enabled = true;

                // Breathing scale + alpha: dread, not decoration.
                float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 3.2f));
                float radius = Radius * (0.92f + 0.12f * pulse);
                LayCircle(ring, party.WorldPosition, radius);

                Color colour = RingColour;
                colour.a = visibility * (0.45f + 0.5f * pulse);
                ring.startColor = colour;
                ring.endColor = colour;
            }

            for (int i = used; i < _rings.Count; i++) _rings[i].enabled = false;
        }

        private LineRenderer GetRing(int index)
        {
            while (_rings.Count <= index)
            {
                var go = new GameObject("PursuitRing");
                go.transform.SetParent(transform, false);

                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = Segments;
                line.widthMultiplier = 0.35f;
                line.material = _material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _rings.Add(line);
            }

            return _rings[index];
        }

        private static void LayCircle(LineRenderer line, Vector3 centre, float radius)
        {
            for (int p = 0; p < Segments; p++)
            {
                float angle = p / (float)Segments * Mathf.PI * 2f;
                line.SetPosition(p, centre
                                    + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius
                                    + Vector3.up * 0.6f);
            }
        }
    }
}
