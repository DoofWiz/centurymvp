using Century.Campaign.Model;
using Century.Core;
using Century.Core.World;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// Wires up the scene-local pieces once <see cref="OvermapWorld"/> has built its views.
    /// Execution order matters here, so this runs from Start while OvermapWorld builds in Awake.
    /// </summary>
    public sealed class OvermapBootstrap : MonoBehaviour
    {
        [SerializeField] private OvermapCameraRig _cameraRig;

        private void Start()
        {
            // Germania under an overcast march: mossy dark ground instead of the terrain's bare
            // white fallback, which blew out to sheer white under the noon sun (worst in WebGL).
            TerrainDressing.Apply(
                low: new Color(0.16f, 0.19f, 0.11f),
                high: new Color(0.29f, 0.31f, 0.19f));

            OvermapWorld world = ServiceLocator.Get<OvermapWorld>();

            if (_cameraRig != null && world.PlayerView != null)
                _cameraRig.SetTarget(world.PlayerView.transform);

            // The sun follows the campaign clock, so the march visibly passes through dawn, noon and
            // dark instead of the time being just a number on the HUD. Created in code; no scene setup.
            if (ServiceLocator.TryGet(out CampaignState state))
            {
                var dayNight = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
                dayNight.HourSource = () => state.Clock.Now.TotalHours;

                // The sight veil darkens everything beyond the column's line of sight.
                new GameObject("OvermapFog").AddComponent<OvermapFog>().Bind(state);

                // Pulsing red rings under visible hunters: a predator reads as a predator at a glance.
                new GameObject("PursuitMarkers").AddComponent<OvermapPursuitMarkers>().Bind(state);

                // The "caught flat" wheel: visible whenever a sudden reveal has the column surprised.
                if (ServiceLocator.TryGet(out Sim.CampaignSettings settings))
                    new GameObject("SurpriseIndicator").AddComponent<SurpriseIndicator>().Bind(state, settings);
            }
        }
    }
}
