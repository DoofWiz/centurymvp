using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Century.Campaign.View
{
    /// <summary>
    /// Scene representation of a <see cref="PartyState"/>. Owns no gameplay data of its own: it reads
    /// the model on bind, drives a NavMeshAgent from it, and writes the resulting position back.
    /// Destroying this object must never lose information.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PartyView : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private Transform _bannerRoot;
        [SerializeField] private float _facingTurnSpeed = 540f;

        [Header("Placement")]
        [Tooltip("How far from the stored position to search for a valid NavMesh point on spawn.")]
        [SerializeField] private float _navMeshSampleRadius = 25f;

        private NavMeshAgent _agent;
        private CampaignSettings _settings;
        private ITimeControlSource _timeSource;

        /// <summary>Last destination pushed to the agent, used to detect model-side changes.</summary>
        private Vector3? _appliedDestination;

        /// <summary>False when the party could not be placed on the NavMesh. The view then idles quietly.</summary>
        private bool _isPlaced;

        public PartyState Party { get; private set; }
        public bool IsPlaced => _isPlaced;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            // The agent throws if it is enabled off-mesh, so it stays disabled until Bind has
            // confirmed a valid position. Enabling it in the prefab is what produces the
            // "Failed to create agent" error at Instantiate time.
            _agent.enabled = false;

            // Speed is model-driven, so the agent must not apply its own acceleration curve or
            // rotation. Both are handled here to keep the model and the view in step.
            _agent.acceleration = 9999f;
            _agent.angularSpeed = 0f;
            _agent.updateRotation = false;
            _agent.autoBraking = false;
        }

        public void Bind(PartyState party, CampaignSettings settings, ITimeControlSource timeSource)
        {
            Party = party;
            _settings = settings;
            _timeSource = timeSource;

            name = $"Party [{party.DisplayName}]";

            _isPlaced = TryPlaceOnNavMesh(party.WorldPosition);
            if (!_isPlaced) return;

            SyncDestinationFromModel();
        }

        private bool TryPlaceOnNavMesh(Vector3 desiredPosition)
        {
            if (!TryResolveOnNavMesh(desiredPosition, out Vector3 placement))
            {
                Debug.LogWarning(
                    $"[PartyView] '{Party.DisplayName}' could not be placed: no NavMesh within " +
                    $"{_navMeshSampleRadius} units of {desiredPosition}. " +
                    "Check that the terrain is baked and that it covers this position — a default Unity " +
                    "terrain sits at the origin and extends in +X/+Z only, so world zero is its corner.",
                    this);

                transform.position = desiredPosition;
                return false;
            }

            // Move the transform first, then enable. Enabling an agent that is already on the mesh
            // avoids the placement error entirely rather than recovering from it.
            transform.position = placement;
            _agent.enabled = true;

            if (_agent.isOnNavMesh) return true;

            Debug.LogWarning($"[PartyView] '{Party.DisplayName}' rejected placement at {placement}.", this);
            _agent.enabled = false;
            return false;
        }

        private bool TryResolveOnNavMesh(Vector3 desired, out Vector3 resolved)
        {
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas))
            {
                resolved = hit.position;
                return true;
            }

            resolved = desired;
            return false;
        }

        /// <summary>Issues a march order. Writes to the model first so the order survives a battle.</summary>
        public bool MoveTo(Vector3 worldPosition)
        {
            if (Party == null || !CanDrive()) return false;
            if (!TryResolveOnNavMesh(worldPosition, out Vector3 destination)) return false;

            Party.Destination = destination;
            Party.IsCamped = false;
            _agent.isStopped = false;
            _appliedDestination = destination;
            return _agent.SetDestination(destination);
        }

        public void Halt()
        {
            if (Party == null) return;
            Party.Destination = null;
            _appliedDestination = null;
            if (CanDrive()) _agent.ResetPath();
        }

        /// <summary>Guards every agent call. Off-mesh agents throw on almost every property.</summary>
        private bool CanDrive() => _isPlaced && _agent.enabled && _agent.isOnNavMesh;

        private void Update()
        {
            if (Party == null || _settings == null) return;

            // A party that never found the mesh still keeps its model position current so that the
            // rest of the simulation — supply, morale, time — carries on unaffected.
            if (!CanDrive())
            {
                Party.WorldPosition = transform.position;
                return;
            }

            // AI parties have their destination written by PartyAiDirector, which knows nothing about
            // agents. Pulling it across here keeps the model authoritative for both AI and player.
            SyncDestinationFromModel();

            float clockMultiplier = _timeSource?.CurrentMultiplier ?? 1f;

            // TODO(step 4): sample the terrain layer under the party for the terrain multiplier.
            _agent.speed = PartySpeedCalculator.EvaluateAgentSpeed(Party, _settings, clockMultiplier);
            _agent.isStopped = _agent.speed <= 0.0001f;

            FaceTravelDirection();

            Party.WorldPosition = transform.position;

            bool arrived = !_agent.pathPending
                           && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;
            if (arrived && Party.Destination.HasValue) Halt();
        }

        /// <summary>Pushes a model-side destination change onto the agent.</summary>
        private void SyncDestinationFromModel()
        {
            if (!CanDrive()) return;

            Vector3? desired = Party.Destination;

            if (!desired.HasValue)
            {
                if (!_appliedDestination.HasValue) return;
                _appliedDestination = null;
                _agent.ResetPath();
                return;
            }

            const float repathThreshold = 2f;
            if (_appliedDestination.HasValue &&
                (desired.Value - _appliedDestination.Value).sqrMagnitude < repathThreshold * repathThreshold)
                return;

            if (!TryResolveOnNavMesh(desired.Value, out Vector3 destination)) return;

            _appliedDestination = destination;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void FaceTravelDirection()
        {
            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.0001f) return;

            Transform target = _bannerRoot != null ? _bannerRoot : transform;
            target.rotation = Quaternion.RotateTowards(
                target.rotation,
                Quaternion.LookRotation(velocity.normalized, Vector3.up),
                _facingTurnSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            // Last chance to persist. Cheap insurance against losing a frame of movement on unload.
            if (Party != null) Party.WorldPosition = transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            if (Party == null) return;
            Gizmos.color = new Color(0.9f, 0.75f, 0.35f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Party.DetectionRadius);
        }
    }

    /// <summary>
    /// Lets Campaign-layer code read and change the clock speed without depending on the App layer.
    /// CampaignTicker (in Century.App) implements this; anything in Campaign or its views should
    /// depend on the interface, never on CampaignTicker directly — App references Campaign, not the
    /// other way round, and reaching across that boundary is what breaks the build.
    /// </summary>
    public interface ITimeControlSource
    {
        TimeControl Current { get; }
        float CurrentMultiplier { get; }
        void SetTimeControl(TimeControl control);
    }
}
