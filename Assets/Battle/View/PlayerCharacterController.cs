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
        [SerializeField] private KeyCode _stealthKey = KeyCode.C;

        [Tooltip("Movement speed multiplier while crouched in stealth.")]
        [Range(0.2f, 1f)] [SerializeField] private float _stealthMoveFraction = 0.5f;

        private CombatantGear _gear;
        private bool _lmbCharging;
        private float _lmbHeld;

        private CharacterController _controller;
        private BattleSettings _settings;
        private BattleState _state;
        private BattleCameraRig _cameraRig;
        private BattleCombatant _combatant;
        private float _verticalVelocity;
        private float _rallyCallLeft;
        private float _rallyCallTime;

        public BattleCombatant Combatant => _combatant;
        public Vector3 Facing { get; private set; } = Vector3.forward;

        /// <summary>Set for one frame when R is pressed. The bootstrap asks the simulation to fire
        /// the rally burst; whether it takes depends on the cooldown, not on this.</summary>
        public bool RallyRequested { get; private set; }

        /// <summary>True while the rally CALL animation plays — the couple of seconds of arm over
        /// head in which the Centurion neither moves nor fights. The burst outlives it.</summary>
        public bool IsRallying { get; private set; }

        /// <summary>True while the ExecutionDirector owns the body for the killing scene.</summary>
        public bool IsExecuting { get; private set; }

        /// <summary>For the ExecutionDirector and RallyFx, which pose and read the figure directly.</summary>
        public Transform BodyRoot => _bodyRoot;
        public SoldierRig Rig => _rig;
        public CombatantGear Gear => _gear;

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

        /// <summary>The stealth stance is a thing the opening teaches; until then C does nothing.</summary>
        public bool StealthAllowed { get; set; }

        /// <summary>Crouched and slow: men who have not marked him look past him (opening sequence).</summary>
        public bool IsStealthed { get; private set; }

        /// <summary>Forward pitch of the body, degrees: 82 lies him flat, 0 stands him. Driven by a
        /// script while control is suspended (the opening's waking), zero otherwise.</summary>
        public float PosePitch { get; set; }

        public void SetStealth(bool on) => IsStealthed = on && StealthAllowed;

        /// <summary>While control is suspended, the Centurion walks himself here (the opening's
        /// exit): a scripted march with the ordinary stride, gravity and facing. Null stands him
        /// where the model says.</summary>
        public Vector3? ScriptedDestination { get; set; }

        /// <summary>Metres per second of a scripted walk.</summary>
        public float ScriptedSpeed { get; set; } = 2.4f;

        /// <summary>True when there is no scripted walk left to do.</summary>
        public bool ScriptedArrived
        {
            get
            {
                if (!ScriptedDestination.HasValue) return true;
                Vector3 to = ScriptedDestination.Value - transform.position;
                to.y = 0f;
                return to.sqrMagnitude <= 0.36f;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_bodyRoot == null) _bodyRoot = transform;
        }

        public void Bind(
            BattleCombatant combatant, BattleSettings settings, BattleCameraRig cameraRig,
            BattleState state = null)
        {
            _combatant = combatant;
            _settings = settings;
            _cameraRig = cameraRig;
            _state = state;
            PilaRemaining = settings.PilaCount;

            name = $"Centurion [{combatant.DisplayName}]";
            transform.position = combatant.WorldPosition;

            InstallRig();
            _gear = new CombatantGear(_bodyRoot, combatant.Weapon, combatant.HasShield, emphasise: true);
        }

        /// <summary>Starts the rally-call animation: arm over head, gathering the men. Movement and
        /// weapons are locked while it plays — the burst's cost, paid up front.</summary>
        public void BeginRallyCall(float seconds)
        {
            _rallyCallLeft = Mathf.Max(0.1f, seconds);
            _rallyCallTime = 0f;
            IsRallying = true;
        }

        /// <summary>The ExecutionDirector takes the body: input dead, controller parked, every pose
        /// coming from the director until <see cref="EndExecution"/>.</summary>
        public void BeginExecution()
        {
            IsExecuting = true;
            IsRallying = false;
            _rallyCallLeft = 0f;
            RallyRequested = false;
            ChargeStarted = SlashRequested = ThrustRequested = false;
            ShieldHeld = false;
            ThrowRequested = false;
            IsAiming = false;
            _lmbCharging = false;
            if (_controller != null) _controller.enabled = false;
        }

        public void EndExecution()
        {
            IsExecuting = false;
            if (_controller != null && _combatant != null && _combatant.IsAlive)
                _controller.enabled = true;
            if (_combatant != null) _combatant.WorldPosition = transform.position;
        }

        private void Update()
        {
            if (_settings == null || _combatant == null) return;

            // The ExecutionDirector owns the body for the moment: position, facing and pose all
            // come from it, so nothing here may touch them.
            if (IsExecuting) return;

            if (!IsControlEnabled)
            {
                IsRallying = false;
                RallyRequested = false;
                _rallyCallLeft = 0f;
                ChargeStarted = SlashRequested = ThrustRequested = false;
                ShieldHeld = false;
                ThrowRequested = false;
                IsAiming = false;
                _lmbCharging = false;

                if (ScriptedDestination.HasValue && !ScriptedArrived)
                {
                    ScriptedStep();
                    return;
                }

                // Follow the model while control is suspended, so deployment moves are reflected here.
                transform.position = _combatant.WorldPosition;
                ApplyPosePitch();
                _rig?.Animate(0f, Time.deltaTime);
                _gear?.Pose(_combatant, Time.deltaTime, _rig);
                return;
            }

            // A dead Centurion gives no orders and swings no blade; he goes over like any man and
            // lies where he fell while the Optio's window runs (Centurio in Waiting).
            if (_combatant != null && !_combatant.IsAlive)
            {
                if (!_playerDown)
                {
                    _playerDown = true;
                    HitEffects.SpawnDeath(transform.position + Vector3.up * 0.9f);
                    if (_controller != null) _controller.enabled = false;

                    int seed = name.GetHashCode();
                    _gear?.DropOnDeath(BattleTerrainBuilder.Grounded(transform.position), seed);
                    DeathTopple.Begin(gameObject, transform, _bodyRoot,
                        _combatant.LastHitDirection, _rig, seed);
                }
                IsRallying = false;
                RallyRequested = false;
                ShieldHeld = false;
                ThrowRequested = false;
                IsAiming = false;
                return;
            }

            // R fires the rally burst (the bootstrap asks the simulation; the cooldown answers).
            // The CALL itself — the couple of seconds of gathering arm — is what roots him.
            RallyRequested = Input.GetKeyDown(_rallyKey);
            if (_rallyCallLeft > 0f)
            {
                _rallyCallLeft -= Time.deltaTime;
                _rallyCallTime += Time.deltaTime;
            }
            IsRallying = _rallyCallLeft > 0f;

            // Stealth toggles on C once the opening has taught it; a sprint stands him up.
            if (StealthAllowed && Input.GetKeyDown(_stealthKey)) IsStealthed = !IsStealthed;
            if (IsStealthed && Input.GetKey(KeyCode.LeftShift)) IsStealthed = false;

            HandleMeleeInput();
            ShieldHeld = !IsRallying && Input.GetKey(_shieldKey);

            // Aiming a pilum: hold to raise it and show the arc, release to throw. Running out of pila
            // mid-aim simply ends the aim without a throw.
            bool holdingThrow = !IsRallying && PilaRemaining > 0 && Input.GetMouseButton(_throwMouseButton);
            ThrowRequested = IsAiming && !holdingThrow;
            IsAiming = holdingThrow;

            if (!IsRallying) ApplyMovement();
            ApplyFacing();
            ApplyPosePitch();

            _combatant.WorldPosition = transform.position;
            _combatant.Facing = Facing;

            if (_combatant.WasHitThisTick) _rig?.NotifyHit();
            _rig?.Animate(_controller != null ? _controller.velocity.magnitude : 0f, Time.deltaTime);

            if (IsRallying) _gear?.PoseRallyWave(_combatant, _rallyCallTime, Time.deltaTime, _rig);
            else _gear?.Pose(_combatant, Time.deltaTime, _rig);
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
        private bool _playerDown;

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
            if (IsStealthed) pace *= _stealthMoveFraction;

            // The rally's surge carries the commander too.
            if (_state != null && _state.RallyActive) pace *= _settings.RallyMoveSpeedFactor;
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

        /// <summary>One frame of the scripted walk: the same controller move as the player's own,
        /// pointed by the script instead of the keys.</summary>
        private void ScriptedStep()
        {
            Vector3 to = ScriptedDestination.Value - transform.position;
            to.y = 0f;
            Vector3 dir = to.normalized;

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + _gravity * Time.deltaTime;
            Vector3 velocity = dir * ScriptedSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            Facing = dir;
            _bodyRoot.rotation = Quaternion.RotateTowards(
                _bodyRoot.rotation, Quaternion.LookRotation(dir, Vector3.up), _settings.PlayerTurnSpeed * Time.deltaTime);

            _combatant.WorldPosition = transform.position;
            _combatant.Facing = dir;

            _rig?.Animate(ScriptedSpeed, Time.deltaTime);
            _gear?.Pose(_combatant, Time.deltaTime, _rig);
        }

        private float _crouch;
        private bool _posePitched;

        /// <summary>Body pitch on top of the facing: the scripted lie-down, and the stealth crouch.
        /// Applied after facing so the yaw is the facing's and the pitch is this.</summary>
        private void ApplyPosePitch()
        {
            if (_bodyRoot == null) return;

            float crouchTarget = IsStealthed ? 22f : 0f;
            _crouch = Mathf.MoveTowards(_crouch, crouchTarget, 110f * Time.unscaledDeltaTime);

            float pitch = PosePitch + _crouch;
            bool pitched = Mathf.Abs(pitch) > 0.01f;
            if (!pitched && !_posePitched) return;
            _posePitched = pitched;

            _bodyRoot.rotation = Quaternion.Euler(pitch, _bodyRoot.eulerAngles.y, 0f);
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
