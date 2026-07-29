using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// The march marker: a ring where the column was told to go — gold on open ground, red and
    /// tracking when the order is to run another party down. Bursts wide on each new order and
    /// breathes while the order stands, so the player always knows where their men are heading.
    /// Entirely code-generated.
    /// </summary>
    public sealed class OvermapMoveMarker : MonoBehaviour
    {
        private const int Segments = 36;
        private const float Radius = 1.4f;

        private static readonly Color GroundColour = new Color(0.94f, 0.79f, 0.41f, 0.9f);
        private static readonly Color PursuitColour = new Color(0.85f, 0.30f, 0.24f, 0.95f);

        private LineRenderer _ring;
        private System.Func<Vector3?> _positionSource;
        private bool _pursuit;
        private float _burstStarted = -10f;

        private void Awake()
        {
            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = false;
            _ring.positionCount = Segments;
            _ring.startWidth = 0.22f;
            _ring.endWidth = 0.22f;
            _ring.material = new Material(
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));

            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0.12f, Mathf.Sin(angle) * Radius));
            }

            _ring.enabled = false;
        }

        /// <summary>
        /// Points the marker at wherever the source reports — a fixed ground point, or a moving
        /// party. A null report hides the marker (order complete or target gone).
        /// </summary>
        public void Show(System.Func<Vector3?> positionSource, bool pursuit)
        {
            _positionSource = positionSource;
            _pursuit = pursuit;
            _burstStarted = Time.time;

            Color colour = pursuit ? PursuitColour : GroundColour;
            _ring.startColor = colour;
            _ring.endColor = colour;
            if (_ring.material.HasProperty("_BaseColor")) _ring.material.SetColor("_BaseColor", colour);
            if (_ring.material.HasProperty("_Color")) _ring.material.SetColor("_Color", colour);
        }

        public void Hide()
        {
            _positionSource = null;
            _ring.enabled = false;
        }

        private void LateUpdate()
        {
            Vector3? position = _positionSource?.Invoke();
            if (position == null)
            {
                _ring.enabled = false;
                return;
            }

            _ring.enabled = true;
            transform.position = position.Value;

            // The juice: a burst that collapses onto the point, then a steady breath — quicker and
            // sharper when the ring is riding a quarry.
            float sinceBurst = Time.time - _burstStarted;
            float burst = Mathf.Lerp(2.4f, 1f, Mathf.SmoothStep(0f, 1f, sinceBurst / 0.3f));
            float breath = 1f + (_pursuit ? 0.12f : 0.07f) * Mathf.Sin(Time.time * (_pursuit ? 7f : 4f));
            transform.localScale = Vector3.one * (burst * breath);
        }
    }
}
