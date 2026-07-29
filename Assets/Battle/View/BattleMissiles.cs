using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Owns thrown weapons for the whole battle: the Centurion's aimed pila, each squad's opening
    /// volley as the enemy closes, and the steady per-man throwing of squads under a Skirmish order.
    /// Flight is physics (see <see cref="PilaProjectile"/>); the damage a landed missile does is
    /// deferred to <see cref="MissileResolver"/>.
    /// </summary>
    public sealed class BattleMissiles : MonoBehaviour
    {
        private BattleState _state;
        private BattleSettings _settings;
        private PilaProjectile _prefab;
        private Transform _root;
        private MissileResolver _resolver;
        private Material _shaftMaterial;

        public bool IsReady => _state != null;

        public void Initialise(BattleState state, BattleSettings settings, PilaProjectile prefab, Transform root)
        {
            _state = state;
            _settings = settings;
            _prefab = prefab;   // optional; a plain shaft is generated when none is assigned
            _root = root;
            _resolver = new MissileResolver(state, settings);
        }

        // --- Throwing --------------------------------------------------------------------------

        public void Throw(Vector3 from, Vector3 targetPoint, BattleCombatant attacker, bool attackerIsPlayerSide)
        {
            if (_state == null) return;

            Vector3 velocity = BallisticVelocity(from, targetPoint, _settings.PilaLaunchSpeed);
            Quaternion rotation = Quaternion.LookRotation(
                velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.forward, Vector3.up);

            PilaProjectile projectile = _prefab != null
                ? Instantiate(_prefab, from, rotation, _root)
                : CreateDefaultProjectile(from, rotation);

            if (projectile == null) return;
            if (projectile.TryGetComponent(out Rigidbody body)) body.linearVelocity = velocity;
            projectile.Launch(this, attacker, attackerIsPlayerSide);
        }

        /// <summary>
        /// Builds a plain shaft when no pila prefab is assigned, so throwing works out of the box. A
        /// designer can drop a nicer prefab onto BattleBootstrap later without touching this.
        /// </summary>
        private PilaProjectile CreateDefaultProjectile(Vector3 position, Quaternion rotation)
        {
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Pilum";
            shaft.transform.SetParent(_root, false);
            shaft.transform.SetPositionAndRotation(position, rotation);
            shaft.transform.localScale = new Vector3(0.07f, 0.07f, 1.15f);

            if (shaft.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = ShaftMaterial();

            Rigidbody body = shaft.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.useGravity = true;
            body.isKinematic = false;

            return shaft.AddComponent<PilaProjectile>();
        }

        private Material ShaftMaterial()
        {
            if (_shaftMaterial != null) return _shaftMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");

            _shaftMaterial = new Material(shader);
            var colour = new Color(0.22f, 0.19f, 0.15f);
            if (_shaftMaterial.HasProperty("_BaseColor")) _shaftMaterial.SetColor("_BaseColor", colour);
            if (_shaftMaterial.HasProperty("_Color")) _shaftMaterial.SetColor("_Color", colour);
            return _shaftMaterial;
        }

        /// <summary>The launch velocity a thrown pilum would use for this shot — for the aim preview.</summary>
        public Vector3 SolveLaunchVelocity(Vector3 from, Vector3 target) =>
            BallisticVelocity(from, target, _settings.PilaLaunchSpeed);

        /// <summary>
        /// A launch velocity that lobs a pilum onto <paramref name="target"/>. The angle is fixed at a
        /// clear javelin arc and the *speed* is solved to the distance, so the pilum lands in the
        /// reticle instead of sailing flat over it. Speed is capped at <paramref name="maxSpeed"/>;
        /// beyond that range it falls short rather than launching absurdly fast.
        /// </summary>
        private static Vector3 BallisticVelocity(Vector3 from, Vector3 target, float maxSpeed)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            Vector3 direction = distance > 0.001f ? flat / distance : Vector3.forward;

            const float angle = 42f * Mathf.Deg2Rad; // reads clearly from the top-down camera
            float gravity = Mathf.Max(0.1f, -Physics.gravity.y);

            // v such that range = v^2 * sin(2θ) / g equals the target distance.
            float speed = Mathf.Sqrt(distance * gravity / Mathf.Max(0.01f, Mathf.Sin(2f * angle)));
            speed = Mathf.Clamp(speed, 3f, maxSpeed);

            return (direction * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
        }

        // --- Impact ----------------------------------------------------------------------------

        /// <summary>
        /// Resolves an in-flight missile against any opposing man within impact radius of
        /// <paramref name="position"/>. Returns true if it struck someone.
        /// </summary>
        public bool TryImpact(Vector3 position, BattleCombatant attacker, bool attackerIsPlayerSide)
        {
            if (_state == null) return false;

            float bestSqr = _settings.PilaImpactRadius * _settings.PilaImpactRadius;
            BattleCombatant best = null;

            List<BattleSquad> targets = attackerIsPlayerSide ? _state.EnemySquads : _state.PlayerSquads;
            for (int s = 0; s < targets.Count; s++)
            {
                if (targets[s].IsOffField) continue;
                List<BattleCombatant> members = targets[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (!man.IsAlive || man == attacker) continue;
                    float sqr = (man.WorldPosition - position).sqrMagnitude;
                    if (sqr > bestSqr) continue;
                    best = man;
                    bestSqr = sqr;
                }
            }

            // An enemy javelin can find the Centurion, who stands in no squad.
            if (!attackerIsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive && player != attacker)
                {
                    float sqr = (player.WorldPosition - position).sqrMagnitude;
                    if (sqr <= bestSqr) best = player;
                }
            }

            if (best == null) return false;

            _resolver.ResolveHit(attacker, best);
            return true;
        }

        // --- Squad missiles (both sides) ---------------------------------------------------------

        /// <summary>
        /// Every man on the field carries a few pila. A squad looses ONE opening volley — all throwers
        /// at once — as an enemy closes to volley range; the Skirmish order then spends whatever is
        /// left, each man throwing on his own cooldown at whatever he can reach. Men already in the
        /// press keep their hands on their blades.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsReady) return;

            TickSide(_state.PlayerSquads, deltaSeconds, isPlayerSide: true);
            TickSide(_state.EnemySquads, deltaSeconds, isPlayerSide: false);
        }

        private void TickSide(List<BattleSquad> squads, float deltaSeconds, bool isPlayerSide)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                BattleSquad squad = squads[s];
                if (!squad.IsEffective || squad.IsOffField) continue;

                // Skirmishing squads (or a Skirmisher warband's, by doctrine) throw continuously;
                // everyone else holds their pila for the single volley before contact.
                bool skirmishing = squad.Order == SquadOrder.Skirmish
                                   || (!isPlayerSide && _state.EnemyBehaviour == CommanderBehaviour.Skirmisher);

                if (skirmishing) TickSkirmish(squad, deltaSeconds, isPlayerSide);
                else TickVolley(squad, isPlayerSide);
            }
        }

        private void TickSkirmish(BattleSquad squad, float deltaSeconds, bool isPlayerSide)
        {
            List<BattleCombatant> members = squad.Members;
            for (int m = 0; m < members.Count; m++)
            {
                BattleCombatant man = members[m];
                man.MissileCooldown -= deltaSeconds;

                if (!CanThrow(man) || man.MissileCooldown > 0f) continue;

                BattleCombatant mark = NearestTarget(man.WorldPosition, _settings.MissileRange, isPlayerSide);
                if (mark == null) continue;

                // Point-blank throwing looks absurd; inside this range the blade does the work.
                if ((mark.WorldPosition - man.WorldPosition).sqrMagnitude < 36f) continue;

                LooseAt(man, mark, isPlayerSide);
                man.MissileCooldown = _settings.MissileCooldownSeconds * Random.Range(0.85f, 1.3f);
            }
        }

        /// <summary>The classic beat: the line waits, the enemy closes, and every arm goes back at once.</summary>
        private void TickVolley(BattleSquad squad, bool isPlayerSide)
        {
            if (squad.HasLoosedVolley) return;

            BattleCombatant closest = NearestTarget(squad.CentreOfMass(), _settings.PilaVolleyRange, isPlayerSide);
            if (closest == null) return;

            squad.HasLoosedVolley = true;

            List<BattleCombatant> members = squad.Members;
            for (int m = 0; m < members.Count; m++)
            {
                BattleCombatant man = members[m];
                if (!CanThrow(man)) continue;

                BattleCombatant mark = NearestTarget(man.WorldPosition, _settings.MissileRange, isPlayerSide);
                if (mark == null) continue;

                LooseAt(man, mark, isPlayerSide);
            }
        }

        private static bool CanThrow(BattleCombatant man) =>
            man.IsAlive && man.PilaRemaining > 0 && !man.IsInCombat && !man.IsAttacking;

        private void LooseAt(BattleCombatant man, BattleCombatant mark, bool isPlayerSide)
        {
            man.PilaRemaining--;
            Throw(man.WorldPosition + Vector3.up * 1.6f, mark.WorldPosition, man, isPlayerSide);
        }

        /// <summary>Nearest living opponent within range. The lone Centurion counts, for enemy arms.</summary>
        private BattleCombatant NearestTarget(Vector3 from, float maxRange, bool attackerIsPlayerSide)
        {
            float bestSqr = maxRange * maxRange;
            BattleCombatant best = null;

            List<BattleSquad> targets = attackerIsPlayerSide ? _state.EnemySquads : _state.PlayerSquads;
            for (int s = 0; s < targets.Count; s++)
            {
                if (targets[s].IsOffField) continue;
                List<BattleCombatant> members = targets[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (!man.IsAlive) continue;
                    float sqr = (man.WorldPosition - from).sqrMagnitude;
                    if (sqr > bestSqr) continue;
                    best = man;
                    bestSqr = sqr;
                }
            }

            if (!attackerIsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive)
                {
                    float sqr = (player.WorldPosition - from).sqrMagnitude;
                    if (sqr <= bestSqr) best = player;
                }
            }

            return best;
        }
    }
}
