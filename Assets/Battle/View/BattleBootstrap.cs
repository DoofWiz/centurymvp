using System.Collections;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using Century.Core;
using Century.Core.Contracts;
using Century.Core.Ui;
using Century.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// Entry point of the Battle scene. Runs the phase sequence — deployment, opening, fighting,
    /// aftermath — and submits a <see cref="BattleResult"/> when the aftermath is dismissed.
    /// </summary>
    /// <remarks>
    /// This assembly still references only Century.Core. It has no idea what a ration is, what a POI
    /// is, or how the overmap works — the request comes in, the result goes out, and the campaign
    /// layer owns the consequences.
    ///
    /// Soldiers are spawned under a flat root rather than under their SquadView. The squad is a logical
    /// grouping, not a spatial one: parenting men to a transform that moves every frame means anything
    /// which stops updating its own position — a corpse, most obviously — gets dragged along with it.
    /// </remarks>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BattleSettings _settings;

        [Tooltip("Skip the deployment screen. Useful while iterating on combat.")]
        [SerializeField] private bool _skipDeployment;

        [Header("Prefabs")]
        [SerializeField] private PlayerCharacterController _playerPrefab;
        [SerializeField] private SoldierView _romanSoldierPrefab;
        [SerializeField] private SoldierView _germanicSoldierPrefab;
        [SerializeField] private SquadView _squadPrefab;

        [Tooltip("Rigidbody projectile thrown as a pilum/javelin. Optional: throwing is disabled if unset.")]
        [SerializeField] private PilaProjectile _pilaPrefab;
        [SerializeField] private BattleMissiles _missiles;

        [Header("Scene")]
        [SerializeField] private BattleCameraRig _cameraRig;
        [SerializeField] private SquadCommandInput _commandInput;
        [SerializeField] private BattleEventFeed _eventFeed;
        [SerializeField] private BattleHudController _hud;
        [SerializeField] private BattleDeploymentController _deployment;
        [SerializeField] private BattleSummaryController _summary;
        [SerializeField] private Transform _spawnRoot;

        [Header("Debug")]
        [SerializeField] private DebugBattleHud _debugHud;

        private BattleState _state;
        private BattleSimulation _simulation;
        private PlayerCharacterController _player;
        private BattleAimIndicator _aimIndicator;
        private StageBanner _banner;
        private Transform _corpseRoot;

        private readonly List<SquadView> _playerSquadViews = new List<SquadView>();
        private readonly List<SoldierView> _soldierViews = new List<SoldierView>();

        private bool _submitted;
        private bool _outcomeDecided;
        private BattleOutcome _pendingOutcome = BattleOutcome.Aborted;

        private void Start()
        {
            UiInputBootstrapper.EnsureEventSystem();
            ResolveMissingReferences();

            if (!Validate()) return;

            if (!ServiceLocator.TryGet(out BattleRequest request))
            {
                Debug.LogError("[Battle] No BattleRequest registered. Returning to the campaign.", this);
                Submit(new BattleResult { Outcome = BattleOutcome.Aborted });
                return;
            }

            _state = BattleFactory.Create(request, _settings);
            ServiceLocator.Register(_state);

            BuildRoots();
            SpawnPlayer();
            SpawnSquads(_state.PlayerSquads, _romanSoldierPrefab, collectViews: true);
            SpawnSquads(_state.EnemySquads, _germanicSoldierPrefab, collectViews: false);

            _simulation = new BattleSimulation(_state, _settings);
            if (_eventFeed != null) _eventFeed.Subscribe(_simulation);

            if (_missiles == null) _missiles = gameObject.AddComponent<BattleMissiles>();
            _missiles.Initialise(_state, _settings, _pilaPrefab, _spawnRoot);

            _aimIndicator = new GameObject("AimIndicator").AddComponent<BattleAimIndicator>();
            _aimIndicator.transform.SetParent(_spawnRoot, false);

            if (_commandInput != null)
                _commandInput.Bind(_state, _settings, _cameraRig, _player, _playerSquadViews);

            // The command reticle and squad tags are pure feedback, generated in code — no scene setup.
            var reticle = new GameObject("CommandReticle").AddComponent<BattleReticle>();
            reticle.transform.SetParent(_spawnRoot, false);
            reticle.Initialise(_cameraRig, _commandInput, _player);

            var labels = new GameObject("SquadLabels").AddComponent<SquadLabels>();
            labels.transform.SetParent(_spawnRoot, false);
            labels.Initialise(_state.PlayerSquads, _commandInput);

            // Trampled field-earth for the bare terrain, which otherwise renders as blown-out white
            // (worst in WebGL). Slightly browner than the overmap's moss: ground that armies churn.
            TerrainDressing.Apply(
                low: new Color(0.20f, 0.18f, 0.12f),
                high: new Color(0.32f, 0.29f, 0.19f));

            // The field is lit for the hour the battle began, and the sun creeps as the fight runs
            // (battle seconds pass at the same exchange rate the aftermath charges the clock).
            var dayNight = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
            dayNight.transform.SetParent(_spawnRoot, false);
            dayNight.HourSource = () => _state.TimeOfDay.TotalHours + _state.ElapsedSeconds / 360d;

            // Hides squads waiting off the field and reveals them when summoned; draws the boundary.
            var reinforcements = new GameObject("Reinforcements").AddComponent<ReinforcementsView>();
            reinforcements.transform.SetParent(_spawnRoot, false);
            reinforcements.Initialise(_soldierViews, _settings);

            if (_hud != null) _hud.Bind(_state, _settings, _commandInput, _eventFeed, _player);
            if (_debugHud != null) _debugHud.Bind(_state, _commandInput, _eventFeed, _player);

            CreateBanner();

            Debug.Log($"[Battle] {_state.PlayerSideAliveCount} on the field against {_state.EnemyAliveCount} " +
                      $"({_state.EnemyBehaviour}), {_state.Reserve.Count} in reserve.");

            // An ambushed column gets no deployment: the fight starts where the trap was sprung,
            // in whatever order the march was in. That is what an ambush IS.
            if (_skipDeployment || _deployment == null || _state.PlayerAmbushed)
            {
                _hud?.SetCombatUiVisible(true);
                StartCoroutine(BeginOpening());
            }
            else
            {
                BeginDeployment();
            }
        }

        /// <summary>
        /// Fills in any scene reference left unassigned in the inspector.
        /// </summary>
        /// <remarks>
        /// Not a substitute for wiring the scene properly, but the failures these fields produce are
        /// silent: an unassigned HUD reference means the combat interface never hides behind a modal
        /// and never populates, with nothing in the console to say why. Resolving them here and
        /// logging each one turns a silent misbehaviour into a line of text naming the field.
        /// </remarks>
        private void ResolveMissingReferences()
        {
            _cameraRig = Adopt(_cameraRig, nameof(_cameraRig));
            _commandInput = Adopt(_commandInput, nameof(_commandInput));
            _eventFeed = Adopt(_eventFeed, nameof(_eventFeed));
            _hud = Adopt(_hud, nameof(_hud));
            _deployment = Adopt(_deployment, nameof(_deployment));
            _summary = Adopt(_summary, nameof(_summary));
            _debugHud = Adopt(_debugHud, nameof(_debugHud));

            if (_spawnRoot == null) _spawnRoot = transform;
        }

        private T Adopt<T>(T current, string fieldName) where T : Component
        {
            if (current != null) return current;

#if UNITY_2023_1_OR_NEWER
            T found = FindFirstObjectByType<T>();
#else
            T found = FindObjectOfType<T>();
#endif
            if (found == null) return null;

            Debug.LogWarning(
                $"[Battle] '{fieldName}' was not assigned on BattleBootstrap. Using the " +
                $"{typeof(T).Name} found on '{found.gameObject.name}'. Assign it in the inspector " +
                "to make this explicit.", this);

            return found;
        }

        private bool Validate()
        {
            if (_settings != null && _playerPrefab != null && _romanSoldierPrefab != null
                && _germanicSoldierPrefab != null && _squadPrefab != null) return true;

            Debug.LogError("[Battle] BattleBootstrap is missing prefab or settings references.", this);
            return false;
        }

        private void BuildRoots()
        {
            if (_spawnRoot == null) _spawnRoot = transform;

            // Corpses are re-parented here on death. It must never move.
            var corpses = new GameObject("Fallen");
            corpses.transform.SetParent(_spawnRoot, false);
            _corpseRoot = corpses.transform;
        }

        private void CreateBanner()
        {
            UIDocument document = _hud != null ? _hud.GetComponent<UIDocument>() : null;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null) _banner = new StageBanner(root);
        }

        // --- Spawning -------------------------------------------------------------------------

        private void SpawnPlayer()
        {
            if (_state.PlayerCharacter == null)
            {
                Debug.LogError("[Battle] No player character in the battle state.", this);
                return;
            }

            _player = Instantiate(
                _playerPrefab, _state.PlayerCharacter.WorldPosition, Quaternion.identity, _spawnRoot);
            _player.Bind(_state.PlayerCharacter, _settings, _cameraRig);

            if (_cameraRig != null) _cameraRig.SetTarget(_player.transform);
        }

        private void SpawnSquads(List<BattleSquad> squads, SoldierView soldierPrefab, bool collectViews)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                BattleSquad squad = squads[s];

                SquadView squadView = Instantiate(
                    _squadPrefab, squad.AnchorPosition, Quaternion.identity, _spawnRoot);
                squadView.Bind(squad, _state, _settings, _player);
                if (collectViews) _playerSquadViews.Add(squadView);

                for (int m = 0; m < squad.Members.Count; m++)
                {
                    BattleCombatant man = squad.Members[m];

                    // Parented to the flat spawn root, not to squadView. See the class remarks.
                    SoldierView soldier = Instantiate(
                        soldierPrefab, man.WorldPosition, Quaternion.identity, _spawnRoot);
                    soldier.Bind(man, squad, _settings, _corpseRoot);
                    _soldierViews.Add(soldier);
                }
            }
        }

        // --- Phases ---------------------------------------------------------------------------

        private void BeginDeployment()
        {
            _state.Phase = BattlePhase.Deployment;

            // Deployment is modal: no world control, and no combat HUD underneath the overlay.
            SetWorldInputEnabled(false);
            _hud?.SetCombatUiVisible(false);

            _deployment.Confirmed += OnDeploymentConfirmed;
            _deployment.SnapRequested += SnapSoldiersToFormation;
            _deployment.Begin(_state, _settings);
        }

        private void OnDeploymentConfirmed()
        {
            _deployment.Confirmed -= OnDeploymentConfirmed;
            _deployment.SnapRequested -= SnapSoldiersToFormation;

            _hud?.SetCombatUiVisible(true);
            StartCoroutine(BeginOpening());
        }

        /// <summary>Teleports every man onto his slot after the deployment layout changes.</summary>
        private void SnapSoldiersToFormation()
        {
            for (int i = 0; i < _soldierViews.Count; i++) _soldierViews[i].SnapToSlot();

            if (_player != null && _state.PlayerCharacter != null)
                _player.transform.position = _state.PlayerCharacter.WorldPosition;
        }

        /// <summary>
        /// Single switch for everything that reads input from the world. Gating in one place is what
        /// makes a phase genuinely modal rather than merely covered by a panel.
        /// </summary>
        private void SetWorldInputEnabled(bool enabled)
        {
            if (_player != null) _player.IsControlEnabled = enabled;
            if (_commandInput != null) _commandInput.IsEnabled = enabled;
            if (_cameraRig != null) _cameraRig.IsInputEnabled = enabled;
        }

        /// <summary>
        /// The announcement. A fight that simply starts feels like a simulation; one that is declared
        /// feels like an event, and the second it costs buys most of the sense of occasion.
        /// </summary>
        private IEnumerator BeginOpening()
        {
            _state.Phase = BattlePhase.Opening;

            if (_banner != null)
            {
                if (_state.PlayerAmbushed)
                    yield return _banner.Show("Ambush", "Form up, Centurion!", 1.5f, "stage-banner__frame--danger");
                else
                    yield return _banner.Show("Battle begins", _state.EnemyDisplayName, 1.3f);
            }

            SetWorldInputEnabled(true);
            _state.Phase = BattlePhase.Fighting;
        }

        private void Update()
        {
            if (_state == null || _simulation == null || _submitted) return;
            if (_state.Phase != BattlePhase.Fighting) return;

            bool rallyHeld = _player != null && _player.IsRallying;

            if (_player != null)
            {
                // Melee: LMB slash / charged thrust, L-Ctrl shield, all resolved in MeleeCombat.
                if (_player.ChargeStarted) _simulation.Combat.PlayerBeginCharge();
                if (_player.SlashRequested) _simulation.Combat.PlayerSlash();
                if (_player.ThrustRequested) _simulation.Combat.PlayerThrust(_player.ThrustCharge01);
                _simulation.Combat.SetPlayerShield(_player.ShieldHeld);

                Vector3 from = _player.transform.position + Vector3.up * 1.6f;
                Vector3 aim = _player.AimPoint.sqrMagnitude > 0.001f
                    ? _player.AimPoint
                    : _player.transform.position + _player.Facing * _settings.MissileRange;

                // Hold RMB to aim (arc + reticle preview), release to throw.
                if (_aimIndicator != null)
                {
                    if (_player.IsAiming) _aimIndicator.Show(from, aim, _missiles);
                    else _aimIndicator.Hide();
                }

                if (_player.ThrowRequested && _player.PilaRemaining > 0 && _missiles != null)
                {
                    _missiles.Throw(from, aim, _player.Combatant, attackerIsPlayerSide: true);
                    _player.PilaRemaining--;
                }
            }

            if (_missiles != null) _missiles.Tick(Time.deltaTime);

            _simulation.Tick(Time.deltaTime, rallyHeld);

            // Manual withdrawal remains available: leaving the field with the men you still have is a
            // legitimate strategic choice, not a failure.
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                StartCoroutine(BeginAftermath(BattleOutcome.Withdrawal));
                return;
            }

            if (_simulation.IsConcluded && !_outcomeDecided)
                StartCoroutine(BeginAftermath(_simulation.Outcome));
        }

        private IEnumerator BeginAftermath(BattleOutcome outcome)
        {
            if (_outcomeDecided) yield break;

            _outcomeDecided = true;
            _pendingOutcome = outcome;
            _state.Phase = BattlePhase.Aftermath;

            SetWorldInputEnabled(false);

            if (_banner != null)
            {
                switch (outcome)
                {
                    case BattleOutcome.Victory:
                        yield return _banner.Show("Victory", "The field is ours", 1.5f);
                        break;
                    case BattleOutcome.Defeat:
                        yield return _banner.Show("Defeat", "The century is broken", 1.5f,
                            "stage-banner__frame--danger");
                        break;
                    default:
                        yield return _banner.Show("Withdrawn", "We leave the field", 1.4f);
                        break;
                }
            }

            // Battle seconds to campaign minutes. A fight of a few minutes on screen represents the
            // best part of an hour of manoeuvring, forming up and fighting.
            double minutes = 12d + _state.ElapsedSeconds / 6d;
            BattleResult result = BattleConclusion.Build(_state, outcome, minutes);

            if (_summary == null)
            {
                Submit(result);
                yield break;
            }

            // The summary is modal too, for the same reason.
            _hud?.SetCombatUiVisible(false);

            _summary.Dismissed += () => Submit(result);
            _summary.Show(_state, result);
        }

        private void Submit(BattleResult result)
        {
            if (_submitted) return;
            _submitted = true;

            ServiceLocator.Unregister<BattleRequest>();
            ServiceLocator.Unregister<BattleState>();

            if (ServiceLocator.TryGet(out IBattleResultSink sink)) sink.Submit(result);
            else Debug.LogError("[Battle] No IBattleResultSink registered; the result is lost.", this);
        }
    }
}
