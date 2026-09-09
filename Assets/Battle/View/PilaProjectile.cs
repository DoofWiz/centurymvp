using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// A thrown pilum or javelin. Its flight is real Unity physics — a Rigidbody under gravity — which
    /// is what gives the arc its weight; the raycast a lesser prototype would use is nowhere in sight.
    /// It finds the man it strikes by a cheap position overlap (not a collider event), so it works the
    /// same whether it hits a soldier's capsule or the Centurion's CharacterController.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PilaProjectile : MonoBehaviour
    {
        [SerializeField] private float _maxFlightSeconds = 5f;
        [SerializeField] private float _despawnAfterStickSeconds = 6f;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private BattleMissiles _missiles;
        private BattleCombatant _attacker;
        private bool _attackerIsPlayerSide;

        private bool _stuck;
        private float _flightTime;

        /// <summary>Direction of flight, kept from the last physics step — collision callbacks see a
        /// post-impact velocity, so the pre-impact line is remembered for the shield-facing check.</summary>
        private Vector3 _travelDir = Vector3.forward;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        /// <summary>Called by <see cref="BattleMissiles"/> straight after the throw sets the velocity.</summary>
        public void Launch(BattleMissiles missiles, BattleCombatant attacker, bool attackerIsPlayerSide)
        {
            _missiles = missiles;
            _attacker = attacker;
            _attackerIsPlayerSide = attackerIsPlayerSide;

            EnsureTrail();
        }

        /// <summary>
        /// A thin fading trail: a lone shaft is a few pixels at strategy height and simply was not
        /// being SEEN — the trail is what makes a volley read as a volley. Added here so a designer
        /// prefab gets it too.
        /// </summary>
        private static Material _trailMaterial;

        private void EnsureTrail()
        {
            if (GetComponent<TrailRenderer>() != null) return;

            if (_trailMaterial == null)
                _trailMaterial = Century.Core.World.FxMaterials.VertexTinted();

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.15f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = _trailMaterial;
            trail.startColor = new Color(0.92f, 0.88f, 0.75f, 0.55f);
            trail.endColor = new Color(0.92f, 0.88f, 0.75f, 0f);
        }

        private void FixedUpdate()
        {
            if (_stuck) return;

            _flightTime += Time.fixedDeltaTime;

            // Point the shaft along its travel so it looks thrown rather than tumbling.
            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude > 0.01f)
            {
                _travelDir = velocity.normalized;
                transform.rotation = Quaternion.LookRotation(_travelDir, Vector3.up);
            }

            if (_missiles != null && _missiles.TryImpact(transform.position, _travelDir, _attacker, _attackerIsPlayerSide))
            {
                Stick();
                return;
            }

            // A shaft that somehow never came down (thrown off the field, resting on scenery) is
            // cleaned up — but never frozen mid-flight, which used to leave volleys hanging in the
            // sky whenever two shafts clipped each other on the way up.
            if (_flightTime >= _maxFlightSeconds)
            {
                if (_rigidbody.linearVelocity.sqrMagnitude < 1f) Stick();
                else Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_stuck) return;

            // Shafts of a volley fly as a cloud and clip one another constantly. A pilum striking a
            // pilum midair froze BOTH in the sky; they pass through each other instead.
            if (collision.collider.GetComponentInParent<PilaProjectile>() != null)
            {
                if (_collider != null) Physics.IgnoreCollision(collision.collider, _collider);
                return;
            }

            // A pilum passes over its own side rather than planting in a friend — friendly hits are
            // never resolved anyway, so sticking in one would just eat the throw.
            if (IsSameSide(collision.collider))
            {
                // Let it fly on through a friend rather than planting in him.
                if (_collider != null) Physics.IgnoreCollision(collision.collider, _collider);
                return;
            }

            // Give whoever we ran into a last chance to be counted as struck (a man the overlap just
            // missed), then plant in whatever we hit — ground, cover, or corpse.
            _missiles?.TryImpact(transform.position, _travelDir, _attacker, _attackerIsPlayerSide);
            Stick();
        }

        private bool IsSameSide(Collider other)
        {
            SoldierView soldier = other.GetComponentInParent<SoldierView>();
            if (soldier != null && soldier.Combatant != null)
                return soldier.Combatant.IsPlayerSide == _attackerIsPlayerSide;

            // The Centurion is on the player side; his own pila should not plant in him.
            if (other.GetComponentInParent<PlayerCharacterController>() != null)
                return _attackerIsPlayerSide;

            return false;
        }

        private void Stick()
        {
            _stuck = true;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            Destroy(gameObject, _despawnAfterStickSeconds);
        }
    }
}
