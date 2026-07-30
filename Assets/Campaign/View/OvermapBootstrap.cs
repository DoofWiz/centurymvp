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

        /// <summary>
        /// The single failure that hides every pawn: a NavMesh baked when the ground was flat.
        /// Agents then walk at y = 0 UNDERNEATH the sculpted terrain — moving normally, rendered
        /// buried. Probing a hill catches it and says so, loudly, instead of leaving an invisible
        /// army to be debugged by eye.
        /// </summary>
        private static void WarnIfNavMeshIsStale()
        {
            // A spot that is reliably high ground on the sculpted world.
            var probe = new Vector3(180f, 0f, 220f);
            float groundHeight = Century.Core.World.WorldTerrainForge.HeightAt(probe.x, probe.z);
            if (groundHeight < 4f) return;   // world not sculpted at all; nothing to compare

            probe.y = groundHeight;
            if (!UnityEngine.AI.NavMesh.SamplePosition(
                    probe, out UnityEngine.AI.NavMeshHit hit, 60f, UnityEngine.AI.NavMesh.AllAreas))
                return;

            if (Mathf.Abs(hit.position.y - groundHeight) > 3f)
                Debug.LogError(
                    $"[Overmap] The NavMesh is STALE: ground at probe ({probe.x:0},{probe.z:0}) is " +
                    $"{groundHeight:0.0}m high but the NavMesh sits at {hit.position.y:0.0}m. Parties are " +
                    "walking under the terrain. Open the Overmap scene, select the terrain's " +
                    "NavMeshSurface and click BAKE, then save the scene.");
        }

        private void Start()
        {
            WarnIfNavMeshIsStale();

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
