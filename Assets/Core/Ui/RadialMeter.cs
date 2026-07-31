using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Core.Ui
{
    /// <summary>
    /// A circular 0..1 gauge drawn with Painter2D — a ring track with an arc that fills clockwise from
    /// twelve o'clock. Built for the camp screen's equipment and morale readouts, where a dial reads
    /// faster than a bare percentage.
    /// </summary>
    /// <remarks>
    /// Constructed in code rather than exposed to UXML (no UxmlFactory) because the camp controller is
    /// the only thing that makes one; keeping it out of the UXML schema avoids a registration step and
    /// the versioned attribute plumbing that comes with it. Colours are plain fields so a caller can
    /// tint a meter to its resource without a stylesheet round-trip.
    /// </remarks>
    public sealed class RadialMeter : VisualElement
    {
        private float _value01;
        private Color _trackColor = new Color(0f, 0f, 0f, 0.55f);
        private Color _fillColor = new Color(0.79f, 0.64f, 0.15f); // --gold
        private float _thickness = 5f;

        public RadialMeter()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public float Value01
        {
            get => _value01;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, _value01)) return;
                _value01 = clamped;
                MarkDirtyRepaint();
            }
        }

        public Color TrackColor { set { _trackColor = value; MarkDirtyRepaint(); } }
        public Color FillColor { set { _fillColor = value; MarkDirtyRepaint(); } }
        public float Thickness { set { _thickness = value; MarkDirtyRepaint(); } }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width < 2f || rect.height < 2f) return;

            Painter2D painter = context.painter2D;
            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - _thickness * 0.5f;
            if (radius <= 0f) return;

            painter.lineWidth = _thickness;
            painter.lineCap = LineCap.Butt;

            // Track: the full ring at low opacity so an empty meter still reads as a dial.
            painter.strokeColor = _trackColor;
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();

            if (_value01 <= 0f) return;

            // Fill: clockwise from twelve o'clock (-90°). Butt caps — round caps at this radius
            // read as blobs, not a dial. A full meter draws a plain circle: an arc whose start
            // and end coincide (mod 360) is degenerate and renders wrong.
            painter.strokeColor = _fillColor;
            painter.BeginPath();
            if (_value01 >= 0.999f)
                painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            else
                painter.Arc(centre, radius,
                    new Angle(-90f, AngleUnit.Degree),
                    new Angle(-90f + 360f * _value01, AngleUnit.Degree));
            painter.Stroke();
        }
    }
}
