using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;
using UnityEngine.AI;

namespace Century.Battle.View
{
    /// <summary>
    /// One man on the field. Seeks the slot his squad assigns him, closes on whatever the melee
    /// resolver has given him to fight, and runs when his squad breaks.
    /// </summary>
    /// <remarks>
    /// The critical detail is <see cref="ApplyAvoidance"/>. NavMeshAgent obstacle avoidance exists to
    /// stop agents overlapping, which is exactly what a testudo requires them to do. Leaving it on
    /// turns every close formation into a milling crowd, so avoidance is switched off in tight order
    /// and the formation solver becomes the sole authority on spacing.
    ///
    /// Movement priority is: rout, then engaged target, then formation slot. A man who has someone to
    /// fight stops dressing the line, which is what makes contact look like contact rather than like
    /// two grids sliding past each other.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SoldierView : MonoBehaviour
    {
        [SerializeField] private Transform _bodyRoot;

        [Header("Feedback")]
        [Tooltip("Optional renderer tinted to show wounds and death.")]
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Color _woundedTint = new Color(0.55f, 0.15f, 0.12f);

        private NavMeshAgent _agent;
        private BattleSettings _settings;
        private BattleCombatant _combatant;
        private BattleSquad _squad;
        private CombatantGear _gear;
        private bool _isPlaced;
        private bool _isDown;

        /// <summary>Where corpses are re-parented to. Must be a transform that never moves.</summary>
        private Transform _corpseRoot;

        private Color _baseColour = Color.white;
        private MaterialPropertyBlock _propertyBlock;

        public BattleCombatant Combatant => _combatant;
        public BattleSquad Squad => _squad;

        /// <summary>True when the man is dressed in his slot rather than still moving up.</summary>
        public bool IsInStation { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.enabled = false;
            _agent.updateRotation = false;
            _agent.autoBraking = true;
            if (_bodyRoot == null) _bodyRoot = transform;

            if (_bodyRenderer == null) _bodyRenderer = GetComponentInChildren<Renderer>();
            if (_bodyRenderer != null && _bodyRenderer.sharedMaterial != null)
                _baseColour = _bodyRenderer.sharedMaterial.color;

            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Bind(
            BattleCombatant combatant, BattleSquad squad, BattleSettings settings, Transform corpseRoot = null)
        {
            _combatant = combatant;
            _squad = squad;
            _settings = settings;
            _corpseRoot = corpseRoot;

            name = $"{combatant.DisplayName} [{squad.DisplayName}]";

            if (NavMesh.SamplePosition(combatant.WorldPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                _agent.enabled = true;
                _isPlaced = _agent.isOnNavMesh;
            }
            else
            {
                transform.position = combatant.WorldPosition;
                Debug.LogWarning($"[SoldierView] {combatant.DisplayName} could not reach the NavMesh.", this);
            }

            _agent.speed = _settings.SoldierBaseSpeed;
            _gear = new CombatantGear(_bodyRoot, combatant.Weapon, combatant.HasShield);
            ApplyTint();
        }

        /// <summary>
        /// Teleports the man onto his formation slot. Used during deployment, where the men should
        /// appear in position rather than marching there — the phase is outside time.
        /// </summary>
        public void SnapToSlot()
        {
            if (_combatant == null || _squad == null || !_combatant.IsAlive) return;

            Vector3 slot = SquadFormationSolver.GetWorldSlot(_squad, _combatant.SlotIndex, _settings);

            if (NavMesh.SamplePosition(slot, out NavMeshHit hit, 6f, NavMesh.AllAreas)) slot = hit.position;

            if (_agent.enabled && _agent.isOnNavMesh) _agent.Warp(slot);
            else transform.position = slot;

            _bodyRoot.rotation = Quaternion.LookRotation(
                SquadFormationSolver.FlatFacing(_squad.AnchorFacing), Vector3.up);

            _combatant.WorldPosition = transform.position;
            IsInStation = true;
        }

        private void Update()
        {
            if (_combatant == null || _squad == null) return;

            if (!_combatant.IsAlive)
            {
                HandleDeath();
                return;
            }

            if (!_isPlaced || !_agent.isOnNavMesh)
            {
                _combatant.WorldPosition = transform.position;
                return;
            }

            if (_squad.IsRouted) UpdateRouting();
            else if (_combatant.IsEngaged) UpdateEngaged();
            else UpdateFormation();

            _combatant.WorldPosition = transform.position;
            _combatant.Facing = _bodyRoot.forward;

            _gear?.Pose(_combatant, Time.deltaTime);

            if (_combatant.WasHitThisTick)
            {
                ApplyTint();
                HitEffects.Spawn(transform.position + Vector3.up * 1.15f);
            }
        }

        // --- Movement modes --------------------------------------------------------------------

        /// <summary>Broken men run for the rear and do not stop to dress ranks.</summary>
        private void UpdateRouting()
        {
            IsInStation = false;

            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.speed = _settings.SoldierBaseSpeed * _settings.RoutSpeedMultiplier
                           * Mathf.Lerp(0.7f, 1f, _combatant.Health01)
                           * StaminaProfile.For(_combatant.Stamina).MoveMultiplier;

            _agent.SetDestination(_squad.RoutDestination);
            FaceDirection(_agent.velocity);
        }

        /// <summary>
        /// Closes to reach on an assigned target. The man still respects his leash, because the
        /// resolver only ever assigns targets within it.
        /// </summary>
        private void UpdateEngaged()
        {
            IsInStation = false;

            Vector3 targetPos = _combatant.Target.WorldPosition;
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // The man keeps the distance his footwork wants: a swordsman closes and holds, a spearman
            // hangs back and only steps in to lunge (DesiredRange drops while he is attacking).
            float desired = _combatant.DesiredRange > 0.05f
                ? _combatant.DesiredRange
                : _settings.EngagementRange * 0.85f;

            ApplyAvoidance();

            float condition = Mathf.Lerp(0.6f, 1f, _combatant.Health01)
                              * Mathf.Lerp(0.7f, 1f, _combatant.Stamina01)
                              * StaminaProfile.For(_combatant.Stamina).MoveMultiplier;

            if (distance > desired + 0.2f)
            {
                _agent.speed = _settings.SoldierBaseSpeed * condition;
                _agent.SetDestination(TetheredDestination(targetPos));
            }
            else if (distance < desired - 0.5f && !_combatant.IsAttacking)
            {
                Vector3 away = transform.position - targetPos;
                away.y = 0f;
                Vector3 dir = away.sqrMagnitude > 0.001f ? away.normalized : -_bodyRoot.forward;
                _agent.speed = _settings.SoldierBaseSpeed * 0.9f;
                _agent.SetDestination(TetheredDestination(targetPos + dir * desired));
            }
            else
            {
                _agent.ResetPath();
            }

            // Face the man you are fighting: this is what stops flanking damage applying by accident.
            FaceDirection(toTarget);
        }

        private void UpdateFormation()
        {
            Vector3 slot = SquadFormationSolver.GetWorldSlot(_squad, _combatant.SlotIndex, _settings);

            float distance = Vector3.Distance(transform.position, slot);
            IsInStation = distance <= _settings.SlotTolerance;

            ApplyAvoidance();
            ApplySpeed(distance);

            if (distance > _settings.SlotTolerance) _agent.SetDestination(slot);
            else _agent.ResetPath();

            // Face the squad's facing (toward the enemy) whenever in station OR with a fight close by —
            // a man stepping back to his slot after a blow backpedals, he does not turn his back on a
            // foe. Only a clear march with no enemy near lets him face the way he is running.
            bool threatNear = _squad.EngagedCount > 0 || _combatant.TimeSinceCombat < 3f;
            FaceDirection(IsInStation || threatNear
                ? SquadFormationSolver.FlatFacing(_squad.AnchorFacing)
                : _agent.velocity);
        }

        /// <summary>
        /// Clamps a chase destination so a man may lean into contact but never leave the leash his
        /// order allows around his formation slot. A held line therefore strikes what comes to it and
        /// snaps back, instead of following a fleeing foe across the map.
        /// </summary>
        private Vector3 TetheredDestination(Vector3 desiredWorld)
        {
            Vector3 slot = SquadFormationSolver.GetWorldSlot(_squad, _combatant.SlotIndex, _settings);
            float reach = MeleeProfile.For(_combatant.Weapon).Reach;
            float leash = reach + SquadTactics.AggressionLeash(_squad);

            Vector3 fromSlot = desiredWorld - slot;
            fromSlot.y = 0f;
            if (fromSlot.magnitude <= leash) return desiredWorld;

            return slot + fromSlot.normalized * leash;
        }

        // --- Helpers --------------------------------------------------------------------------

        private void ApplyAvoidance()
        {
            bool tight = _squad.Formation.IsTight() && !_squad.IsRouted;

            _agent.obstacleAvoidanceType = tight
                ? ObstacleAvoidanceType.NoObstacleAvoidance
                : ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            // Front rank gets priority so the line dresses from the front backwards rather than
            // the rear ranks shoving through it.
            _agent.avoidancePriority = 50 + Mathf.Clamp(_combatant.SlotIndex, 0, 49);
        }

        private void ApplySpeed(float distanceToSlot)
        {
            float formationSpeed = _settings.SoldierBaseSpeed * _squad.Formation.SpeedMultiplier();

            // Men who have fallen behind hurry to catch up, otherwise a moving line never re-dresses
            // and trails stragglers forever.
            float catchUp = Mathf.Clamp(distanceToSlot / 4f, 1f, 1.7f);

            float condition = Mathf.Lerp(0.6f, 1f, _combatant.Health01)
                              * Mathf.Lerp(0.7f, 1f, _combatant.Stamina01)
                              * StaminaProfile.For(_combatant.Stamina).MoveMultiplier;

            _agent.speed = formationSpeed * catchUp * condition;
        }

        private void FaceDirection(Vector3 desired)
        {
            desired.y = 0f;
            if (desired.sqrMagnitude < 0.0004f) return;

            _bodyRoot.rotation = Quaternion.RotateTowards(
                _bodyRoot.rotation,
                Quaternion.LookRotation(desired.normalized, Vector3.up),
                _settings.SoldierTurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// The dead stay on the field. Corpses are the clearest possible read on how a fight went,
        /// and removing them would make a won battle look identical to an untouched one.
        /// </summary>
        /// <remarks>
        /// The detach is essential. A living soldier's NavMeshAgent overwrites his transform every
        /// frame, so he does not care what he is parented to — but once the agent is disabled, a
        /// corpse inherits every movement of its parent. Left attached to anything that moves, bodies
        /// slide across the field as the fight develops.
        /// </remarks>
        private void HandleDeath()
        {
            if (_isDown) return;
            _isDown = true;

            if (_agent.enabled && _agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;

            Vector3 restingPlace = transform.position;
            float facingYaw = _bodyRoot.eulerAngles.y;

            transform.SetParent(_corpseRoot, worldPositionStays: true);
            transform.position = restingPlace;

            _bodyRoot.rotation = Quaternion.Euler(90f, facingYaw, 0f);
            _bodyRoot.localPosition += Vector3.down * 0.35f;

            // Colliders on a corpse serve no purpose and confuse the living.
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            _combatant.WorldPosition = restingPlace;
            ApplyTint();
        }

        private void ApplyTint()
        {
            if (_bodyRenderer == null) return;

            Color colour = !_combatant.IsAlive
                ? _baseColour * 0.35f
                : Color.Lerp(_woundedTint, _baseColour, _combatant.Health01);

            _propertyBlock.SetColor("_BaseColor", colour);
            _propertyBlock.SetColor("_Color", colour);
            _bodyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDestroy()
        {
            if (_combatant != null) _combatant.WorldPosition = transform.position;
        }
    }
}
