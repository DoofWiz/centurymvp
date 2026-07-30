using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// The "caught flat" markers: whenever a party is surprised, a "!" pops above it inside a radial
    /// wheel that depletes as its surprise window runs out. The column's own surprise is RED — a
    /// battle now is an ambush against you. A visible enemy's surprise is GOLD — the trap is theirs
    /// to suffer, and the wheel is how long you have to spring it. Entirely code-generated.
    /// </summary>
    /// <remarks>Enemy wheels show only while that party is visible to the column. Later, scout
    /// skill should gate them entirely — knowing the enemy is catchable flat is intelligence.</remarks>
    public sealed class SurpriseIndicator : MonoBehaviour
    {
        private const int Segments = 40;
        private const float Radius = 2.4f;
        private const float Height = 7f;

        private static readonly Color DangerColour = new Color(0.85f, 0.30f, 0.24f, 0.95f);
        private static readonly Color PreyColour = new Color(0.94f, 0.79f, 0.41f, 0.95f);

        private sealed class Marker
        {
            public GameObject Root;
            public LineRenderer Wheel;
            public TextMesh Mark;
            public bool WasActive;
            public float PopStarted;
        }

        private readonly Dictionary<string, Marker> _markers = new Dictionary<string, Marker>();

        private CampaignState _state;
        private CampaignSettings _settings;
        private Camera _camera;
        private Font _font;

        public void Bind(CampaignState state, CampaignSettings settings)
        {
            _state = state;
            _settings = settings;
            _camera = Camera.main;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void LateUpdate()
        {
            if (_state == null || _settings == null) return;

            CampaignTime now = _state.Clock.Now;

            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState party = parties[i];
                if (party.IsDisbanded) continue;

                bool active = party.IsSurprised(now)
                              && (party.IsPlayer || LineOfSight.Visibility01(_state, party) > 0.05f);

                Marker marker = GetMarker(party);
                if (!active)
                {
                    marker.WasActive = false;
                    marker.Root.SetActive(false);
                    continue;
                }

                if (!marker.WasActive)
                {
                    marker.WasActive = true;
                    marker.PopStarted = Time.time;
                    Paint(marker, party.IsPlayer ? DangerColour : PreyColour);
                }

                marker.Root.SetActive(true);
                marker.Root.transform.position = party.WorldPosition + Vector3.up * Height;

                // The wheel is the window: full when the surprise lands, gone when it passes.
                float remaining = Mathf.Clamp01(
                    (float)((party.SurprisedUntil.TotalMinutes - now.TotalMinutes) / _settings.SurpriseWindowMinutes));
                DrawArc(marker.Wheel, remaining);

                // Pop on arrival, then an urgent heartbeat.
                float sincePop = Time.time - marker.PopStarted;
                float pop = Mathf.Lerp(1.8f, 1f, Mathf.SmoothStep(0f, 1f, sincePop / 0.35f));
                float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 9f);
                marker.Root.transform.localScale = Vector3.one * (pop * pulse);

                if (_camera == null) _camera = Camera.main;
                if (_camera != null) marker.Mark.transform.rotation = _camera.transform.rotation;
            }
        }

        private Marker GetMarker(PartyState party)
        {
            if (_markers.TryGetValue(party.Id, out Marker existing)) return existing;

            var marker = new Marker { Root = new GameObject($"Surprise [{party.DisplayName}]") };
            marker.Root.transform.SetParent(transform, false);

            marker.Wheel = marker.Root.AddComponent<LineRenderer>();
            marker.Wheel.loop = false;
            marker.Wheel.useWorldSpace = false;
            marker.Wheel.positionCount = Segments;
            marker.Wheel.startWidth = 0.35f;
            marker.Wheel.endWidth = 0.35f;
            marker.Wheel.material = Century.Core.World.FxMaterials.Unlit();

            var markRoot = new GameObject("Mark");
            markRoot.transform.SetParent(marker.Root.transform, false);
            marker.Mark = markRoot.AddComponent<TextMesh>();
            marker.Mark.font = _font;
            markRoot.GetComponent<MeshRenderer>().material = _font.material;
            marker.Mark.text = "!";
            marker.Mark.fontSize = 110;
            marker.Mark.characterSize = 0.05f;
            marker.Mark.anchor = TextAnchor.MiddleCenter;
            marker.Mark.alignment = TextAlignment.Center;

            marker.Root.SetActive(false);
            _markers[party.Id] = marker;
            return marker;
        }

        private static void Paint(Marker marker, Color colour)
        {
            marker.Wheel.startColor = colour;
            marker.Wheel.endColor = colour;
            if (marker.Wheel.material.HasProperty("_BaseColor")) marker.Wheel.material.SetColor("_BaseColor", colour);
            if (marker.Wheel.material.HasProperty("_Color")) marker.Wheel.material.SetColor("_Color", colour);
            marker.Mark.color = colour;
        }

        /// <summary>Redraws a wheel as an arc covering the remaining fraction of the window.</summary>
        private static void DrawArc(LineRenderer wheel, float fraction01)
        {
            float sweep = Mathf.PI * 2f * fraction01;

            for (int i = 0; i < Segments; i++)
            {
                float angle = Mathf.PI * 0.5f - sweep * (i / (float)(Segments - 1));
                wheel.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius));
            }
        }
    }
}
