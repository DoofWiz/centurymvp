using UnityEngine;
using UnityEngine.Rendering;

namespace Century.Core.World
{
    /// <summary>
    /// Turns an hour-of-day into lighting: the sun wheels around the map, warms and reddens as it
    /// nears the horizon, hands over to a pale moon at night, and the ambient light follows through
    /// dawn, day, dusk and dark. The component is deliberately dumb about WHERE time comes from —
    /// whoever creates it supplies <see cref="HourSource"/> (the campaign clock on the overmap, the
    /// battle's start time plus elapsed fighting in a battle) — so it lives in Core and serves both.
    /// </summary>
    /// <remarks>
    /// It adopts the scene's hand-placed directional light rather than adding a second sun, registers
    /// it as <see cref="RenderSettings.sun"/> so the procedural skybox draws the disc and the horizon
    /// glow itself, and switches ambient to gradient-driven trilight so night actually gets dark
    /// without a per-frame GI rebuild. Created entirely from code; no scene setup.
    /// </remarks>
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Header("Sun")]
        [Tooltip("Peak intensity of the sun at noon. Kept below full: Germania's sun is a witness, " +
                 "not a participant.")]
        [SerializeField] private float _dayIntensity = 0.95f;

        [Tooltip("Yaw of the sun's arc, so shadows fall at a pleasing angle rather than due south.")]
        [SerializeField] private float _arcYaw = -35f;

        [Header("Moon")]
        [Tooltip("Intensity of the moonlight that carries the night watches.")]
        [SerializeField] private float _moonIntensity = 0.14f;

        [SerializeField] private Color _moonColour = new Color(0.58f, 0.66f, 0.85f);

        [Header("Ambient")]
        [Tooltip("Drive RenderSettings ambient light through the cycle. Disable to leave the scene's own.")]
        [SerializeField] private bool _driveAmbient = true;

        /// <summary>Hour of day (0..24, fractional; whole days are fine and get wrapped).</summary>
        public System.Func<double> HourSource;

        private Light _light;
        private Gradient _sunColourByElevation;
        private Gradient _ambientSky;
        private Gradient _ambientEquator;
        private Gradient _ambientGround;

        private void Awake()
        {
            AdoptLight();
            BuildGradients();

            if (_light != null) RenderSettings.sun = _light;
            if (_driveAmbient) RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        private void Update()
        {
            if (HourSource == null || _light == null) return;

            float hour = (float)(HourSource() % 24d);
            if (hour < 0f) hour += 24f;

            Apply(hour);
        }

        /// <summary>Sets the whole lighting state for an hour of day. Public so a scene with a fixed
        /// time (a loading vignette, a screenshot) can pose it directly.</summary>
        public void Apply(float hour)
        {
            float dayFraction = hour / 24f;

            // One full turn per day, pitched so 06:00 rises, 12:00 stands overhead, 18:00 sets.
            Quaternion sunArc = Quaternion.Euler(dayFraction * 360f - 90f, _arcYaw, 0f);
            float sunElevation = -(sunArc * Vector3.forward).y;   // 1 overhead, 0 horizon, <0 below

            if (sunElevation > 0f)
            {
                _light.transform.rotation = sunArc;
                _light.color = _sunColourByElevation.Evaluate(Mathf.Clamp01(sunElevation));
                _light.intensity = _dayIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunElevation / 0.35f));
            }
            else
            {
                // The moon stands roughly opposite the sun, so it rises as the sun sinks. Both lights
                // pass the horizon at zero intensity, which is what hides the handover.
                _light.transform.rotation = Quaternion.Euler(dayFraction * 360f + 90f, _arcYaw, 0f);
                _light.color = _moonColour;
                _light.intensity = _moonIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-sunElevation / 0.3f));
            }

            if (!_driveAmbient) return;

            RenderSettings.ambientSkyColor = _ambientSky.Evaluate(dayFraction);
            RenderSettings.ambientEquatorColor = _ambientEquator.Evaluate(dayFraction);
            RenderSettings.ambientGroundColor = _ambientGround.Evaluate(dayFraction);

            if (RenderSettings.fog)
                RenderSettings.fogColor = _ambientEquator.Evaluate(dayFraction);
        }

        /// <summary>Adopts the scene's directional light, or makes one when a scene has none.</summary>
        private void AdoptLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional) continue;
                _light = lights[i];
                return;
            }

            var sun = new GameObject("Sun (DayNightCycle)");
            sun.transform.SetParent(transform, false);
            _light = sun.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.shadows = LightShadows.Soft;
        }

        // --- Palette -----------------------------------------------------------------------------

        private void BuildGradients()
        {
            // Sun colour by how high it stands: dull ember on the horizon, brief muted amber, then a
            // pewter near-white — an overcast northern light, never golden. Grim without murk: the
            // VALUE range stays wide so the field still reads; only the warmth is denied.
            _sunColourByElevation = Ramp(
                (0.00f, new Color(0.82f, 0.36f, 0.20f)),
                (0.12f, new Color(0.87f, 0.58f, 0.38f)),
                (0.30f, new Color(0.90f, 0.85f, 0.74f)),
                (0.55f, new Color(0.92f, 0.93f, 0.92f)));

            // Ambient over the day (0 = midnight): cold blue-grey night, an ashen blush at dawn and
            // dusk, a low iron-grey sky through the working day.
            _ambientSky = Ramp(
                (0.00f, new Color(0.09f, 0.11f, 0.17f)),
                (0.20f, new Color(0.10f, 0.12f, 0.19f)),
                (0.26f, new Color(0.46f, 0.35f, 0.30f)),
                (0.35f, new Color(0.52f, 0.58f, 0.66f)),
                (0.68f, new Color(0.52f, 0.57f, 0.64f)),
                (0.76f, new Color(0.50f, 0.34f, 0.26f)),
                (0.83f, new Color(0.10f, 0.12f, 0.19f)),
                (1.00f, new Color(0.09f, 0.11f, 0.17f)));

            _ambientEquator = Ramp(
                (0.00f, new Color(0.07f, 0.08f, 0.13f)),
                (0.20f, new Color(0.08f, 0.09f, 0.14f)),
                (0.26f, new Color(0.66f, 0.42f, 0.28f)),
                (0.35f, new Color(0.55f, 0.58f, 0.62f)),
                (0.68f, new Color(0.55f, 0.57f, 0.60f)),
                (0.76f, new Color(0.68f, 0.40f, 0.24f)),
                (0.83f, new Color(0.08f, 0.09f, 0.14f)),
                (1.00f, new Color(0.07f, 0.08f, 0.13f)));

            _ambientGround = Ramp(
                (0.00f, new Color(0.05f, 0.05f, 0.06f)),
                (0.25f, new Color(0.07f, 0.06f, 0.06f)),
                (0.40f, new Color(0.22f, 0.19f, 0.16f)),
                (0.65f, new Color(0.22f, 0.19f, 0.16f)),
                (0.80f, new Color(0.07f, 0.06f, 0.06f)),
                (1.00f, new Color(0.05f, 0.05f, 0.06f)));
        }

        private static Gradient Ramp(params (float time, Color colour)[] keys)
        {
            var colours = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                colours[i] = new GradientColorKey(keys[i].colour, keys[i].time);

            var gradient = new Gradient();
            gradient.SetKeys(colours, new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
            return gradient;
        }
    }
}
