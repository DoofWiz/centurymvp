using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// Ticks the overmap AI and the encounter detector on a fixed cadence while the overmap is loaded.
    /// </summary>
    /// <remarks>
    /// This exists only to give the plain-C# directors a heartbeat. All decisions live in
    /// <see cref="PartyAiDirector"/> and <see cref="EncounterDetector"/>, which are testable without
    /// Unity. Deliberately not per-frame: at 0.2s the chase still feels responsive and the cost is
    /// invisible even with a few hundred parties.
    /// </remarks>
    public sealed class OvermapDirectorRunner : MonoBehaviour
    {
        private CampaignState _state;
        private CampaignSettings _settings;
        private PartyAiDirector _ai;
        private EncounterDetector _detector;
        private PoiDirector _poi;
        private AmbushDirector _ambush;
        private ITimeControlSource _timeSource;

        private float _accumulator;
        private bool _contactRaised;

        /// <summary>Raised once when the player collides with a hostile. The App layer handles it.</summary>
        public static event System.Action<EncounterContact> ContactDetected;

        /// <summary>Raised when the column reaches a point of interest. The overmap HUD handles it.</summary>
        public static event System.Action<Model.PointOfInterest> PoiTriggered;

        private void Start()
        {
            _state = ServiceLocator.Get<CampaignState>();
            _settings = ServiceLocator.Get<CampaignSettings>();
            ServiceLocator.TryGet(out _timeSource);
            ServiceLocator.TryGet(out CampaignEventLog log);

            _ai = new PartyAiDirector(_state, _settings);
            _detector = new EncounterDetector(_state, _settings);
            _poi = new PoiDirector(_state, log);
            _ambush = new AmbushDirector(_state, _settings);
            _poi.Triggered += p => PoiTriggered?.Invoke(p);
        }

        private void Update()
        {
            if (_state == null || _contactRaised) return;

            // Frozen clock means a paused campaign: no decisions, no encounters.
            if (_timeSource != null && _timeSource.CurrentMultiplier <= 0f) return;

            _accumulator += Time.deltaTime;
            if (_accumulator < _settings.DirectorIntervalSeconds) return;
            _accumulator = 0f;

            _ai.Evaluate();
            _ambush.Tick();

            // A point of interest takes precedence: it pauses time as it opens, which stops us here
            // next tick, and it must not fire in the same frame as a battle contact.
            if (_poi.Evaluate()) return;

            if (!_detector.TryFindContact(out EncounterContact contact)) return;

            // Latch so a contact cannot fire twice while the battle scene is loading.
            _contactRaised = true;
            ContactDetected?.Invoke(contact);
        }
    }
}
