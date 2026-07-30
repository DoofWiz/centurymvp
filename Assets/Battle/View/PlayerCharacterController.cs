using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Direct control of the Centurion. Movement is camera-relative on the ground plane; facing
    /// follows the cursor independently of movement, so you can back away while still facing front.
    /// </summary>
    /// <remarks>
    /// Uses CharacterController rather than a NavMeshAgent deliberately. The player should be able
    /// to go anywhere physically reachable, including places the AI would never path to, and should
    /// never have movement smoothed or arbitrated by a pathfinder.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerCharacterController : MonoBehaviour
    {
        [SerializeField] private Transform _bodyRoot;
        [SerializeField] private float _gravity = -20f;

        [Tooltip("Movement speed multiplier while aiming a pilum. You cannot throw at a full run.")]
        [Range(0.05f, 1f)] [SerializeField] private float _aimMoveMultiplier = 0.18f;

        [Header("Input")]
        [SerializeField] private KeyCode _rallyKey = KeyCode.R;
        [SerializeField] private int _attackMouseButton;      // left mouse: slash / charge a thrust
        [SerializeField] private int _throwMouseButton = 1;   // right mouse, hurl a pilum
        [SerializeField] private KeyCode _shieldKey = KeyCode.LeftControl;

        private CombatantGear _gear;
        private bool _lmbCharging;
        private float _lmbHeld;

        private CharacterController _controller;
        private BattleSettings _settings;
        private BattleCameraRig _cameraRig;
        private BattleCombatant _combatant;
        private float _verticalVelocity;

        public BattleCombatant Combatant => _combatant;
        public Vector3 Facing { get; private set; } = Vector3.forward;

        /// <summary>True while the player is calling men back to the standard.</summary>
        public bool IsRallying { get; private set; }

        /// <summary>One-frame melee intents, consumed by the bootstrap and fed to MeleeCombat.</summary>
        public bool ChargeStarted { get; private set; }   // LMB pressed — begin the wind-up
        public bool SlashRequested { get; private set; }   // quick tap released
        public bool ThrustRequested { get; private set; }  // charged release
        public float ThrustCharge01 { get; private set; }  // how far the thrust was charged, 0..1

        /// <summary>True while the shield key is held (and the player is not mid-attack).</summary>
        public bool ShieldHeld { get; private set; }

        /// <summary>Set for one frame when the player releases an aimed pilum. Consumed by the bootstrap.</summary>
        public bool ThrowRequested { get; private set; }

        /// <summary>True while the throw button is held and pila remain — the aim reticle shows.</summary>
        public bool IsAiming { get; private set; }

        /// <summary>Pila the Centurion still has to throw. Seeded from BattleSettings on bind.</summary>
        public int PilaRemaining { get; set; }

        /// <summary>Ground point under the cursor, where a thrown pilum is aimed.</summary>
        public Vector3 AimPoint { get; private set; }

        /// <summary>
        /// False during deployment and the aftermath. Gating here rather than disabling the component
        /// keeps the transform where the model says it is, so the tactical map and the world agree.
        /// </summary>
        public bool IsControlEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_bodyRoot == null) _bodyRoot = transform;
        }

        public void Bind(BattleCombatant combatant, BattleSettings settings, BattleCameraRig cameraRig)
        {
            _combatant = combatant;
            _settings = settings;
            _cameraRig = cameraRig;
            PilaRemaining = settings.PilaCount;

            name = $"Centurion [{combatant.DisplayName}]";
            transform.position = combatant.WorldPosition;

            InstallRig();
            _gear = new CombatantGear(_bodyRoot, combatant.Weapon, combatant.HasShield, emphasise: true);
        }

        private void Update()
        {
            if (_settings == null || _combatant == null) return;

            if (!_combatant.IsAlive) return;

            if (!IsControlEnabled)
            {
                IsRallying = false;
                ChargeStarted = SlashRequested = ThrustRequested = false;
                ShieldHeld = false;
                ThrowRequested = false;
                IsAiming = false;
                _lmbCharging = false;

                // Follow the model while control is suspended, so deployment moves are reflected here.
                transform.position = _combatant.WorldPosition;
                _gear?.Pose(_combatant, Time.deltaTime);
                return;
            }

            // Rallying is a commitment: you stand and shout instead of moving or fighting.
            IsRallying = Input.GetKey(_rallyKey);

            HandleMeleeInput();
            ShieldHeld = !IsRallying && Input.GetKey(_shieldKey);

            // Aiming a pilum: hold to raise it and show the arc, release to throw. Running out of pila
            // mid-aim simply ends the aim without a throw.
            bool holdingThrow = !IsRallying && PilaRemaining > 0 && Input.GetMouseButton(_throwMouseButton);
            ThrowRequested = IsAiming && !holdingThrow;
            IsAiming = holdingThrow;

            if (!IsRallying) ApplyMovement();
            ApplyFacing();

            _combatant.WorldPosition = transform.position;
            _combatant.Facing = _bodyRoot.forward;

            if (_combatant.WasHitThisTick) _rig?.NotifyHit();
            _rig?.Animate(_controller != null ? _controller.velocity.magnitude : 0f, Time.deltaTime);

            _gear?.Pose(_combatant, Time.deltaTime, _rig);
        }

        /// <summary>
        /// Left mouse is the blade: a tap is a quick slash, holding winds up a thrust (charge caps at
        /// one second), and the release sends it. The bootstrap turns these into MeleeCombat calls.
        /// </summary>
        private void HandleMeleeInput()
        {
            ChargeStarted = SlashRequested = ThrustRequested = false;

            if (IsRallying)
            {
                _lmbCharging = false;
                return;
            }

            if (Input.GetMouseButtonDown(_attackMouseButton))
            {
                _lmbCharging = true;
                _lmbHeld = 0f;
                ChargeStarted = true;
            }

            if (_lmbCharging && Input.GetMouseButton(_attackMouseButton))
                _lmbHeld += Time.deltaTime;

            if (_lmbCharging && Input.GetMouseButtonUp(_attackMouseButton))
            {
                _lmbCharging = false;
                if (_lmbHeld < _settings.PlayerSlashHoldThreshold)
                {
                    SlashRequested = true;
                }
                else
                {
                    ThrustRequested = true;
                    ThrustCharge01 = Mathf.Clamp01(_lmbHeld);   // 1s hold = full charge
                }
            }
        }

        private SoldierRig _rig;

        /// <summary>The Centurion in the flesh: transverse crest, bronze, a head above the ranks.
        /// Replaces whatever placeholder the prefab carried; its colliders stay untouched.</summary>
        private void InstallRig()
        {
            float chest = 1.0f;
            MeshRenderer[] placeholders = _bodyRoot.GetComponentsInChildren<MeshRenderer>();
            if (placeholders.Length > 0)
            {
                chest = placeholders[0].bounds.center.y - transform.position.y;
                for (int i = 0; i < placeholders.Length; i++) placeholders[i].enabled = false;
            }

            _rig = SoldierRig.Build(_bodyRoot, chest, roman: true,
                Century.Battle.Model.OfficerRole.Centurion, leader: false, variant: 0);
            _rig.transform.localScale = Vector3.one * 1.05f;
        }

        private void ApplyMovement()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Camera-relative on the ground plane. Yaw only: pitch must not shorten forward movement.
            Quaternion cameraYaw = Quaternion.Euler(0f, _cameraRig != null
                ? _cameraRig.transform.eulerAngles.y
                : 0f, 0f);

            Vector3 move = cameraYaw * new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            // Fatigue and wounds slow the commander like anyone else.
            float condition = Mathf.Lerp(0.65f, 1f, _combatant.Health01)
                              * Mathf.Lerp(0.75f, 1f, _combatant.Stamina01);

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + _gravity * Time.deltaTime;

            // Shift is a SPRINT: full speed, paid for in wind. Ordinary movement is the walk-jog
            // of a man saving himself for the fight; a spent man cannot sprint at all.
            bool sprinting = Input.GetKey(KeyCode.LeftShift)
                             && move.sqrMagnitude > 0.01f
                             && _combatant.Stamina01 > 0.1f
                             && !IsAiming;
            float pace = sprinting ? 1f : _settings.NormalMoveFraction;
            if (sprinting)
                _combatant.Stamina01 =
                    Mathf.Max(0f, _combatant.Stamina01 - _settings.SprintStaminaPerSecond * Time.deltaTime);

            // The ground resists the commander like anyone else: wading slows and drains, slopes tax.
            if (BattleTerrainBuilder.IsInWater(transform.position))
            {
                pace *= _settings.WaterMoveFactor;
                if (move.sqrMagnitude > 0.01f)
                    _combatant.Stamina01 = Mathf.Max(
                        0f, _combatant.Stamina01 - _settings.WaterStaminaPerSecond * Time.deltaTime);
            }
            pace *= 1f - BattleTerrainBuilder.Steepness01(transform.position) * _settings.SteepSlopeMovePenalty;

            float aimSlow = IsAiming ? _aimMoveMultiplier : 1f;
            Vector3 velocity = move * (_settings.PlayerSpeed * pace * condition * aimSlow);
            velocity.y = _verticalVelocity;

            _controller.Move(velocity * Time.deltaTime);
        }

        private void ApplyFacing()
        {
            if (_cameraRig == null || !_cameraRig.TryGetCursorGroundPoint(out Vector3 cursor)) return;

            AimPoint = cursor;

            Vector3 toCursor = cursor - transform.position;
            toCursor.y = 0f;
            if (toCursor.sqrMagnitude < 0.04f) return;

            Facing = toCursor.normalized;

            _bodyRoot.rotation = Quaternion.RotateTowards(
                _bodyRoot.rotation,
                Quaternion.LookRotation(Facing, Vector3.up),
                _settings.PlayerTurnSpeed * Time.deltaTime);
        }
    }
}
