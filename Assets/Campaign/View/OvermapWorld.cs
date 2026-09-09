using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// Builds the overmap scene from campaign state on load, and tears it down on unload.
    /// </summary>
    /// <remarks>
    /// This is the piece that makes the battle transition safe. The overmap scene is fully unloaded
    /// while a battle runs, so nothing here may hold state that is not also in the model. On return
    /// this class rebuilds every party from scratch and they land exactly where they left off.
    /// </remarks>
    public sealed class OvermapWorld : MonoBehaviour
    {
        [System.Serializable]
        public struct FactionPrefab
        {
            public PartyFaction Faction;
            public PartyView Prefab;
        }

        [Header("Prefabs")]
        [SerializeField] private List<FactionPrefab> _partyPrefabs = new List<FactionPrefab>();

        [Header("Scene")]
        [SerializeField] private Transform _partyRoot;

        private readonly Dictionary<string, PartyView> _views = new Dictionary<string, PartyView>();
        private readonly Dictionary<string, PoiMarkerView> _poiMarkers = new Dictionary<string, PoiMarkerView>();
        private Font _worldFont;

        public CampaignState State { get; private set; }
        public PartyView PlayerView { get; private set; }
        public IReadOnlyDictionary<string, PartyView> Views => _views;

        private void Awake()
        {
            State = ServiceLocator.Get<CampaignState>();
            CampaignSettings settings = ServiceLocator.Get<CampaignSettings>();
            ServiceLocator.TryGet(out ITimeControlSource timeSource);

            if (_partyRoot == null) _partyRoot = transform;

            // The world font for place names; the engine's face until the designer's is in.
            _worldFont = Resources.Load<Font>("Fonts/CenturyWorld")
                         ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < State.Parties.Count; i++)
            {
                PartyState party = State.Parties[i];
                if (party.IsDisbanded) continue;

                PartyView view = SpawnView(party, settings, timeSource);
                if (view == null) continue;

                _views.Add(party.Id, view);
                if (party.IsPlayer) PlayerView = view;
            }

            ServiceLocator.Register(this);
        }

        private PartyView SpawnView(PartyState party, CampaignSettings settings, ITimeControlSource timeSource)
        {
            PartyView prefab = ResolvePrefab(party.Faction);
            if (prefab == null)
            {
                Debug.LogError($"[OvermapWorld] No prefab registered for faction {party.Faction}.", this);
                return null;
            }

            PartyView view = Instantiate(prefab, party.WorldPosition, Quaternion.identity, _partyRoot);
            view.Bind(party, settings, timeSource);

            // Other columns ghost in and out of the line of sight; your own never does.
            if (!party.IsPlayer)
            {
                ServiceLocator.TryGet(out CampaignEventLog log);
                view.gameObject.AddComponent<PartyVisibility>().Bind(State, party, log);
            }

            return view;
        }

        private PartyView ResolvePrefab(PartyFaction faction)
        {
            for (int i = 0; i < _partyPrefabs.Count; i++)
                if (_partyPrefabs[i].Faction == faction) return _partyPrefabs[i].Prefab;
            return null;
        }

        public PartyView FindView(string partyId) =>
            _views.TryGetValue(partyId, out PartyView view) ? view : null;

        // --- Points of interest ----------------------------------------------------------------

        /// <summary>
        /// Keeps a marker on the map for every discovered, unresolved place. Cheap to run each frame
        /// for the handful of places a campaign holds; markers appear as they are sighted and vanish
        /// as they are dealt with, with no explicit event plumbing. The story's current target is
        /// told apart every frame, so the marker turns gold the moment the objective moves on.
        /// </summary>
        private void Update()
        {
            if (State == null) return;

            List<PointOfInterest> pois = State.PointsOfInterest;
            for (int i = 0; i < pois.Count; i++)
            {
                PointOfInterest poi = pois[i];
                bool shouldShow = poi.Discovered && !poi.Resolved && !poi.Hidden;
                bool has = _poiMarkers.TryGetValue(poi.Id, out PoiMarkerView marker);

                if (shouldShow && !has)
                {
                    marker = PoiMarkerView.Create(_partyRoot, poi, ColourFor(poi.Kind), _worldFont);
                    _poiMarkers[poi.Id] = marker;
                }
                else if (!shouldShow && has)
                {
                    if (marker != null) Destroy(marker.gameObject);
                    _poiMarkers.Remove(poi.Id);
                    continue;
                }

                if (marker == null) continue;

                marker.SetObjective(OnboardingDirector.IsObjectivePoi(State, poi));
                marker.SetVisibility(VisibilityFor(poi));
            }
        }

        /// <summary>
        /// A discovered place is remembered, so its marker never vanishes: it dims beyond the line of
        /// sight, the way explored ground does in an RTS. A scouted prize and the story's objective
        /// are the exceptions: the whole point of the report is that you know exactly where it is.
        /// </summary>
        private float VisibilityFor(PointOfInterest poi)
        {
            if (poi.Kind == PoiKind.Opportunity || OnboardingDirector.IsObjectivePoi(State, poi)) return 1f;
            return Mathf.Lerp(0.35f, 1f, LineOfSight.Visibility01(State, poi.WorldPosition));
        }

        private static Color ColourFor(PoiKind kind)
        {
            switch (kind)
            {
                case PoiKind.Grove: return new Color(0.40f, 0.70f, 0.35f);
                case PoiKind.Ruin: return new Color(0.62f, 0.62f, 0.64f);
                case PoiKind.Refugees: return new Color(0.55f, 0.70f, 0.85f);
                case PoiKind.RaiderCamp: return new Color(0.80f, 0.30f, 0.25f);
                case PoiKind.Battlefield: return new Color(0.52f, 0.42f, 0.30f);
                case PoiKind.Opportunity: return new Color(1.00f, 0.85f, 0.20f);
                case PoiKind.Corpses: return new Color(0.60f, 0.48f, 0.38f);
                case PoiKind.Hounds: return new Color(0.68f, 0.54f, 0.30f);
                case PoiKind.Looters: return new Color(0.85f, 0.32f, 0.22f);
                case PoiKind.SafePlace: return new Color(0.45f, 0.78f, 0.58f);
                case PoiKind.Passage: return new Color(1.00f, 0.85f, 0.20f);
                case PoiKind.SlavePens: return new Color(0.82f, 0.30f, 0.38f);
                default: return new Color(0.85f, 0.70f, 0.32f); // Settlement / fallback
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet(out OvermapWorld registered) && registered == this)
                ServiceLocator.Unregister<OvermapWorld>();
        }
    }
}
