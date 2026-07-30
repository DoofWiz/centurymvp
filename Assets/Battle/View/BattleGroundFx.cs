using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Ground-level feedback rings: the expanding gold ping that confirms WHERE an order was aimed
    /// the moment it is given, and the filling rally arc around a broken squad the Centurion is
    /// bringing back — the two moments a commander most needs the world, not the HUD, to answer.
    /// </summary>
    /// <remarks>
    /// All LineRenderer circles from a small pool, unlit, code-generated. Order pings arrive via
    /// <see cref="SquadCommandInput.DestinationPinged"/>; rally arcs are polled off squad state
    /// (IsRouted + RallyProgress01), so no rally code had to change to be seen.
    /// </remarks>
    public sealed class BattleGroundFx : MonoBehaviour
    {
        private const int Segments = 40;
        private const float PingSeconds = 0.7f;
        private const float PingStartRadius = 0.6f;
        private const float PingEndRadius = 3.4f;

        private static readonly Color PingColour = new Color(0.94f, 0.79f, 0.41f);
        private static readonly Color RallyColour = new Color(1f, 0.85f, 0.35f);

        private sealed class Ping
        {
            public LineRenderer Line;
            public Vector3 Centre;
            public float Age;
            public bool Active;
        }

        private readonly List<Ping> _pings = new List<Ping>();
        private readonly List<LineRenderer> _rallyArcs = new List<LineRenderer>();

        private BattleState _state;
        private Material _material;

        public void Initialise(BattleState state, SquadCommandInput commands)
        {
            _state = state;
            _material = MakeMaterial();

            if (commands != null) commands.DestinationPinged += SpawnPing;
        }

        // --- Order pings -----------------------------------------------------------------------

        private void SpawnPing(Vector3 position)
        {
            Ping ping = null;
            for (int i = 0; i < _pings.Count; i++)
                if (!_pings[i].Active) { ping = _pings[i]; break; }

            if (ping == null)
            {
                if (_pings.Count >= 12) return;   // spam guard; the oldest are still animating
                ping = new Ping { Line = MakeCircle("OrderPing") };
                _pings.Add(ping);
            }

            ping.Centre = position;
            ping.Age = 0f;
            ping.Active = true;
            ping.Line.enabled = true;
        }

        private void Update()
        {
            TickPings();
            TickRallyArcs();
        }

        private void TickPings()
        {
            // Unscaled: an order given during a tactical pause must still visibly land.
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _pings.Count; i++)
            {
                Ping ping = _pings[i];
                if (!ping.Active) continue;

                ping.Age += dt;
                float t = ping.Age / PingSeconds;
                if (t >= 1f)
                {
                    ping.Active = false;
                    ping.Line.enabled = false;
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);   // fast out, settling
                float radius = Mathf.Lerp(PingStartRadius, PingEndRadius, eased);
                Color colour = PingColour;
                colour.a = 1f - t;

                LayCircle(ping.Line, ping.Centre, radius, 1f);
                ping.Line.startColor = colour;
                ping.Line.endColor = colour;
            }
        }

        // --- Rally arcs ------------------------------------------------------------------------

        private void TickRallyArcs()
        {
            if (_state == null) return;

            int used = 0;
            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                if (!squad.IsRouted || squad.RallyProgress01 <= 0.01f || squad.IsDestroyed) continue;

                LineRenderer arc = GetRallyArc(used++);
                arc.enabled = true;

                // The arc closes as the rally takes: a circle completing is a promise completing.
                LayCircle(arc, squad.CentreOfMass(), 3f, squad.RallyProgress01);

                Color colour = RallyColour;
                colour.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                arc.startColor = colour;
                arc.endColor = colour;
            }

            for (int i = used; i < _rallyArcs.Count; i++) _rallyArcs[i].enabled = false;
        }

        private LineRenderer GetRallyArc(int index)
        {
            while (_rallyArcs.Count <= index) _rallyArcs.Add(MakeCircle("RallyArc"));
            return _rallyArcs[index];
        }

        // --- Plumbing --------------------------------------------------------------------------

        /// <summary>Writes an arc of <paramref name="fraction"/> of a full circle at ground level.</summary>
        private static void LayCircle(LineRenderer line, Vector3 centre, float radius, float fraction)
        {
            int points = Mathf.Max(2, Mathf.CeilToInt(Segments * Mathf.Clamp01(fraction)) + 1);
            line.positionCount = points;
            line.loop = fraction >= 0.999f;

            for (int p = 0; p < points; p++)
            {
                float angle = (p / (float)(points - 1)) * Mathf.PI * 2f * Mathf.Clamp01(fraction);
                line.SetPosition(p, centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius
                                          + Vector3.up * 0.12f);
            }
        }

        private LineRenderer MakeCircle(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = _material;
            line.widthMultiplier = 0.16f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static Material MakeMaterial()
        {
            // Sprites/Default on purpose: always in builds, transparent, and it honours the
            // LineRenderer's vertex colours — which is what lets one shared material carry every
            // ring's own colour and fade.
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }
    }
}
