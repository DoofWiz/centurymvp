using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// Translates player input into march orders. A click on open ground marches there; a click on a
    /// visible hostile party locks pursuit — the column tracks the quarry to intercept until it is
    /// caught, lost, or a new order overrides it. Both orders raise the animated move marker: gold
    /// for ground, red and following for a hunt.
    /// </summary>
    public sealed class PlayerPartyController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private int _moveMouseButton = 1;   // right mouse, Bannerlord style
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _maxRayDistance = 5000f;

        [Tooltip("How close a click must land to a visible party to read as 'attack them'.")]
        [SerializeField] private float _partyPickRadius = 6f;

        [Tooltip("Seconds between pursuit re-paths. The quarry moves; the intercept course follows.")]
        [SerializeField] private float _pursuitRepathSeconds = 0.25f;

        private OvermapWorld _world;
        private CampaignState _state;
        private Camera _camera;
        private OvermapMoveMarker _marker;

        private string _pursuitTargetId;
        private float _nextRepathAt;
        private Vector3 _lastQuarryPosition;
        private bool _hasLastQuarryPosition;

        private void Start()
        {
            _world = ServiceLocator.Get<OvermapWorld>();
            ServiceLocator.TryGet(out _state);
            _camera = Camera.main;

            _marker = new GameObject("MoveMarker").AddComponent<OvermapMoveMarker>();
        }

        private void Update()
        {
            if (_world == null || _world.PlayerView == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (Input.GetMouseButtonDown(_moveMouseButton)) TryIssueMoveOrder();
            if (Input.GetKeyDown(KeyCode.H)) HaltEverything();

            TickPursuit();
        }

        private void TryIssueMoveOrder()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _groundMask, QueryTriggerInteraction.Ignore))
                return;

            // A click near a VISIBLE hostile is an order to run them down, not to stand where
            // they were standing.
            PartyState quarry = FindClickedParty(hit.point);
            if (quarry != null)
            {
                _pursuitTargetId = quarry.Id;
                _nextRepathAt = 0f;
                _hasLastQuarryPosition = false;

                string capturedId = quarry.Id;
                _marker.Show(() =>
                {
                    PartyState target = _state?.FindParty(capturedId);
                    return target != null && !target.IsDisbanded && _pursuitTargetId == capturedId
                        ? target.WorldPosition
                        : (Vector3?)null;
                }, pursuit: true);
                return;
            }

            _pursuitTargetId = null;
            if (!_world.PlayerView.MoveTo(hit.point)) return;

            _marker.Show(() =>
                _state?.PlayerParty != null && _state.PlayerParty.Destination.HasValue
                    ? _state.PlayerParty.Destination.Value
                    : (Vector3?)null, pursuit: false);
        }

        /// <summary>Nearest hostile party within the pick radius that the column can actually see.</summary>
        private PartyState FindClickedParty(Vector3 point)
        {
            if (_state?.PlayerParty == null) return null;

            PartyState best = null;
            float bestSqr = _partyPickRadius * _partyPickRadius;

            for (int i = 0; i < _state.Parties.Count; i++)
            {
                PartyState party = _state.Parties[i];
                if (party.IsPlayer || party.IsDisbanded || !party.CanFight) continue;
                if (!PartyRelations.IsHostile(_state.PlayerParty, party)) continue;
                if (LineOfSight.Visibility01(_state, party) <= 0.05f) continue;

                Vector3 to = party.WorldPosition - point;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                best = party;
                bestSqr = sqr;
            }

            return best;
        }

        /// <summary>Keeps the intercept course current while a hunt stands.</summary>
        private void TickPursuit()
        {
            if (string.IsNullOrEmpty(_pursuitTargetId)) return;

            PartyState target = _state?.FindParty(_pursuitTargetId);
            if (target == null || target.IsDisbanded || !target.CanFight)
            {
                _pursuitTargetId = null;
                return;
            }

            if (Time.time < _nextRepathAt) return;
            _nextRepathAt = Time.time + _pursuitRepathSeconds;

            // March for where the quarry is HEADED, not its heels: project its recent travel out,
            // further the farther away it is. A standing quarry is simply marched at.
            Vector3 current = target.WorldPosition;
            Vector3 aim = current;

            if (_hasLastQuarryPosition)
            {
                Vector3 travel = current - _lastQuarryPosition;
                travel.y = 0f;

                if (travel.sqrMagnitude > 0.0025f)
                {
                    float distance = Vector3.Distance(_world.PlayerView.transform.position, current);
                    aim = current + travel.normalized * Mathf.Min(distance * 0.45f, 60f);
                }
            }

            _lastQuarryPosition = current;
            _hasLastQuarryPosition = true;

            _world.PlayerView.MoveTo(aim);
        }

        private void HaltEverything()
        {
            _pursuitTargetId = null;
            _world.PlayerView.Halt();
            _marker.Hide();
        }
    }
}
