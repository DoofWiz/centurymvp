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

        [Header("Points of interest")]
        [Tooltip("Optional marker prefab. If empty, a coloured primitive is generated per POI.")]
        [SerializeField] private GameObject _poiMarkerPrefab;

        private sealed class PoiMarker
        {
            public GameObject Root;
            public Material Material;
            public Color BaseColour;
            public PoiKind Kind;
        }

        private readonly Dictionary<string, PartyView> _views = new Dictionary<string, PartyView>();
        private readonly Dictionary<string, PoiMarker> _poiMarkers = new Dictionary<string, PoiMarker>();
        private readonly Dictionary<PoiKind, Material> _poiMaterials = new Dictionary<PoiKind, Material>();

        public CampaignState State { get; private set; }
        public PartyView PlayerView { get; private set; }
        public IReadOnlyDictionary<string, PartyView> Views => _views;

        private void Awake()
        {
            State = ServiceLocator.Get<CampaignState>();
            CampaignSettings settings = ServiceLocator.Get<CampaignSettings>();
            ServiceLocator.TryGet(out ITimeControlSource timeSource);

            if (_partyRoot == null) _partyRoot = transform;

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
        /// Keeps a marker on the map for every discovered, unresolved POI. Cheap to run each frame for
        /// the handful of POIs a campaign holds; markers appear as they are sighted and vanish as they
        /// are dealt with, with no explicit event plumbing.
        /// </summary>
        private void Update()
        {
            if (State == null) return;

            List<PointOfInterest> pois = State.PointsOfInterest;
            for (int i = 0; i < pois.Count; i++)
            {
                PointOfInterest poi = pois[i];
                bool shouldShow = poi.Discovered && !poi.Resolved;
                bool has = _poiMarkers.TryGetValue(poi.Id, out PoiMarker marker);

                if (shouldShow && !has)
                {
                    _poiMarkers[poi.Id] = CreateMarker(poi);
                }
                else if (!shouldShow && has)
                {
                    if (marker.Root != null) Destroy(marker.Root);
                    _poiMarkers.Remove(poi.Id);
                }
                else if (shouldShow && has)
                {
                    FadeMarker(marker, poi);
                }
            }
        }

        /// <summary>
        /// A discovered place is remembered, so its marker never vanishes — it dims beyond the line of
        /// sight, the way explored ground does in an RTS. An Opportunity the scouts brought back is
        /// the exception: the whole point of the report is that you know exactly where it is.
        /// </summary>
        private void FadeMarker(PoiMarker marker, PointOfInterest poi)
        {
            if (marker.Material == null) return;

            float alpha = marker.Kind == PoiKind.Opportunity
                ? 1f
                : Mathf.Lerp(0.35f, 1f, LineOfSight.Visibility01(State, poi.WorldPosition));

            PartyVisibility.SetAlpha(marker.Material, marker.BaseColour, alpha);
        }

        private PoiMarker CreateMarker(PointOfInterest poi)
        {
            GameObject root;
            if (_poiMarkerPrefab != null)
            {
                root = Instantiate(_poiMarkerPrefab, poi.WorldPosition, Quaternion.identity, _partyRoot);
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                root.transform.SetParent(_partyRoot, false);
                root.transform.position = poi.WorldPosition + Vector3.up * 3f;
                root.transform.localScale = new Vector3(3.5f, 3f, 3.5f);

                // A marker must never eat the click-to-move raycast, so it carries no collider.
                Collider collider = root.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            root.name = $"POI [{poi.DisplayName}]";

            // Per-marker material instance so each can fade with the line of sight independently.
            var marker = new PoiMarker { Root = root, Kind = poi.Kind };
            Renderer renderer = root.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                marker.Material = new Material(MarkerMaterial(poi.Kind));
                PartyVisibility.MakeTransparentCapable(marker.Material);
                marker.BaseColour = PartyVisibility.BaseColour(marker.Material);
                renderer.material = marker.Material;
            }

            return marker;
        }

        private Material MarkerMaterial(PoiKind kind)
        {
            if (_poiMaterials.TryGetValue(kind, out Material cached) && cached != null) return cached;

            Material material = Century.Core.World.FxMaterials.Unlit(ColourFor(kind));
            _poiMaterials[kind] = material;
            return material;
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
