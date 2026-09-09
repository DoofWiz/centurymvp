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

            MissileClass missile = attacker != null ? attacker.Missile : MissileClass.Pila;
            float launchSpeed = _settings.PilaLaunchSpeed * MissileProfile.For(missile).LaunchSpeedScale;

            Vector3 velocity = BallisticVelocity(from, targetPoint, launchSpeed);
            Quaternion rotation = Quaternion.LookRotation(
                velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.forward, Vector3.up);

            PilaProjectile projectile = missile == MissileClass.Rocks
                ? CreateRock(from, rotation)
                : _prefab != null
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
            // A thin shaft at full casting speed crosses half a metre per physics step — discrete
            // collision happily tunnels it through a man or the ground.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return shaft.AddComponent<PilaProjectile>();
        }

        /// <summary>A looter's stone: a fist-sized lump, not a shaft. Code-gen like the default pilum.</summary>
        private PilaProjectile CreateRock(Vector3 position, Quaternion rotation)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(_root, false);
            rock.transform.SetPositionAndRotation(position, rotation);
            rock.transform.localScale = Vector3.one * 0.2f;

            if (rock.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = RockMaterial();

            Rigidbody body = rock.AddComponent<Rigidbody>();
            body.mass = 0.8f;
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return rock.AddComponent<PilaProjectile>();
        }

        private Material _rockMaterial;

        private Material RockMaterial()
        {
            if (_rockMaterial == null)
                _rockMaterial = Century.Core.World.FxMaterials.Unlit(new Color(0.42f, 0.40f, 0.37f));
            return _rockMaterial;
        }

        private Material ShaftMaterial()
        {
            if (_shaftMaterial == null)
                _shaftMaterial = Century.Core.World.FxMaterials.Unlit(new Color(0.22f, 0.19f, 0.15f));
            return _shaftMaterial;
        }

        /// <summary>The launch velocity a thrown pilum would use for this shot — for the aim preview.</summary>
        public Vector3 SolveLaunchVelocity(Vector3 from, Vector3 target) =>
            BallisticVelocity(from, target, _settings.PilaLaunchSpeed);

        /// <summary>
        /// A launch velocity that puts a pilum onto <paramref name="target"/> with real force. Full
        /// throwing speed, and the LOW ballistic solution for the angle — a hard, flat cast whose
        /// flight time grows with distance, not the old fixed 42° lob that took the same lazy age
        /// to land at every range. Close throws keep a minimum arc (re-solving the speed down) so
        /// they still read as thrown; past maximum range it launches at 45° and falls short.
        /// </summary>
        private static Vector3 BallisticVelocity(Vector3 from, Vector3 target, float maxSpeed)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            Vector3 direction = distance > 0.001f ? flat / distance : Vector3.forward;

            float gravity = Mathf.Max(0.1f, -Physics.gravity.y);
            const float minAngle = 9f * Mathf.Deg2Rad;

            // range = v² sin(2θ) / g  →  the low-arc θ for full speed, floored at the minimum arc.
            float sin2 = Mathf.Clamp01(distance * gravity / Mathf.Max(0.01f, maxSpeed * maxSpeed));
            float angle = Mathf.Max(0.5f * Mathf.Asin(sin2), minAngle);

            // At the floored angle the full speed would overshoot, so the speed is solved back down.
            float speed = Mathf.Clamp(
                Mathf.Sqrt(distance * gravity / Mathf.Max(0.01f, Mathf.Sin(2f * angle))),
                3f, maxSpeed);

            return (direction * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
        }

        // --- Impact ----------------------------------------------------------------------------

        /// <summary>
        /// Resolves an in-flight missile against any opposing man within impact radius of
        /// <paramref name="position"/>. <paramref name="travelDir"/> is its direction of flight, so
        /// the resolver can judge which quarter the throw arrives from. Returns true if it struck.
        /// </summary>
        public bool TryImpact(Vector3 position, Vector3 travelDir, BattleCombatant attacker, bool attackerIsPlayerSide)
        {
            if (_state == null) return false;

            float bestSqr = _settings.PilaImpactRadius * _settings.PilaImpactRadius;
            BattleCombatant best = null;

            // Positions are at a man's FEET; a pilum in flight crosses at body height, so the check
            // is against the chest — measuring to the feet made most of the arc miss men it visibly
            // passed through.
            Vector3 chestOffset = Vector3.up * 1.1f;

            List<BattleSquad> targets = attackerIsPlayerSide ? _state.EnemySquads : _state.PlayerSquads;
            for (int s = 0; s < targets.Count; s++)
            {
                if (targets[s].IsOffField) continue;
                List<BattleCombatant> members = targets[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (!man.IsAlive || man == attacker) continue;
                    float sqr = (man.WorldPosition + chestOffset - position).sqrMagnitude;
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
                    float sqr = (player.WorldPosition + chestOffset - position).sqrMagnitude;
                    if (sqr <= bestSqr) best = player;
                }
            }

            if (best == null) return false;

            _resolver.ResolveHit(attacker, best, travelDir);
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

                // A squad that has not marked an enemy (the opening's passing warband) throws nothing.
                if (squad.Unaware) continue;

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
                    if (!man.IsAlive || man.InExecution) continue;
                    float sqr = (man.WorldPosition - from).sqrMagnitude;
                    if (sqr > bestSqr) continue;
                    best = man;
                    bestSqr = sqr;
                }
            }

            if (!attackerIsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive && !player.InExecution)
                {
                    float sqr = (player.WorldPosition - from).sqrMagnitude;
                    if (sqr <= bestSqr) best = player;
                }
            }

            return best;
        }
    }
}
