using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Composition root. Lives in the persistent Boot scene, owns the campaign's lifecycle (new,
    /// loaded, ended), registers the long-lived services and owns scene flow. This is the only
    /// place in the project that is allowed to be a singleton.
    /// </summary>
    /// <remarks>
    /// Two layers of service: the CORE services (settings, scene flow, the ticker, the save slots,
    /// the fader) live for the whole run; the CAMPAIGN services (state, simulation, encounters,
    /// result applier, event log) are built when a campaign begins and torn down when it ends, so
    /// the title screen sits over nothing and a loaded save replaces everything cleanly.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameDirector : MonoBehaviour, IBattleResultSink
    {
        /// <summary>Where the run starts. Title is the game; the others are designer shortcuts.</summary>
        public enum StartMode
        {
            /// <summary>The title screen. What the player sees.</summary>
            Title = 0,
            /// <summary>Straight into a new guided campaign: the opening sequence.</summary>
            NewCampaign = 1,
            /// <summary>A new guided campaign, opening skipped: the Aftermath on the overmap.</summary>
            Aftermath = 2,
            /// <summary>The old free start: the full century, everything live, no guidance.</summary>
            Sandbox = 3
        }

        [Header("Configuration")]
        [SerializeField] private CampaignSettings _campaignSettings;
        [SerializeField] private int _campaignSeed = 90409;

        [Tooltip("Title = the game as shipped. NewCampaign / Aftermath / Sandbox jump straight into " +
                 "play for iteration; the title's own buttons are unaffected.")]
        [SerializeField] private StartMode _startMode = StartMode.Title;

        [Header("Runtime")]
        [SerializeField] private CampaignTicker _ticker;

        public static GameDirector Instance { get; private set; }

        public CampaignState Campaign { get; private set; }
        public SceneFlowService SceneFlow { get; private set; }
        public CampaignSimulation Simulation { get; private set; }
        public EncounterCoordinator Encounters { get; private set; }
        public BattleResultApplier ResultApplier { get; private set; }
        public CampaignEventLog EventLog { get; private set; }
        public SaveService Saves { get; private set; }

        public bool HasCampaign => Campaign != null;

        /// <summary>A line for the title screen to show once, e.g. after the century is lost.</summary>
        public string TitleNotice { get; set; }

        private ScreenFader _fader;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_campaignSettings == null)
            {
                Debug.LogError("[GameDirector] CampaignSettings is not assigned. Aborting boot.", this);
                enabled = false;
                return;
            }

            BuildCoreServices();
        }

        private void BuildCoreServices()
        {
            ServiceLocator.Clear();

            SceneFlow = new SceneFlowService(this);
            Saves = new SaveService(this);

            if (_ticker == null) _ticker = gameObject.AddComponent<CampaignTicker>();
            _ticker.Initialise(null, _campaignSettings);
            _ticker.IsRunning = false;

            ServiceLocator.Register(_campaignSettings);
            ServiceLocator.Register(SceneFlow);
            ServiceLocator.Register<ISceneNavigator>(SceneFlow);
            ServiceLocator.Register(_ticker);
            ServiceLocator.Register<ISaveSlots>(Saves);
            ServiceLocator.Register<IBattleResultSink>(this);

            SceneFlow.GameplaySceneLoaded += OnGameplaySceneLoaded;
            SceneFlow.GameplaySceneUnloading += OnGameplaySceneUnloading;

            _fader = ScreenFader.Create(transform);
        }

        private void Start()
        {
            switch (_startMode)
            {
                case StartMode.NewCampaign: StartNewCampaign(); break;
                case StartMode.Aftermath: StartNewCampaign(skipOpening: true); break;
                case StartMode.Sandbox: StartSandbox(); break;
                default: SceneFlow.LoadTitle(); break;
            }
        }

        // --- Campaign lifecycle ---------------------------------------------------------------

        /// <summary>The guided start: the opening sequence, then the Aftermath.</summary>
        public void StartNewCampaign(bool skipOpening = false)
        {
            CampaignState state = NewCampaignFactory.CreateOnboarding(_campaignSeed, _campaignSettings);
            BeginCampaign(state, null);

            if (skipOpening)
            {
                CompleteOpening();
                SceneFlow.LoadOvermap();
                return;
            }

            SceneFlow.LoadBattle(OpeningSequenceForge.Build(state));
        }

        /// <summary>The free start: the full century on the open map, nothing guided.</summary>
        public void StartSandbox()
        {
            CampaignState state = NewCampaignFactory.CreateSandbox(_campaignSeed, _campaignSettings);
            BeginCampaign(state, null);
            SceneFlow.LoadOvermap();
        }

        /// <summary>A campaign read back from a save. Resumes wherever it stood.</summary>
        public void LoadCampaign(CampaignState state, List<SavedEvent> events)
        {
            if (state == null) return;
            BeginCampaign(state, events);

            if (state.Onboarding.Chapter == OnboardingChapter.Opening)
                SceneFlow.LoadBattle(OpeningSequenceForge.Build(state));
            else
                SceneFlow.LoadOvermap();
        }

        private void BeginCampaign(CampaignState state, List<SavedEvent> events)
        {
            EndCampaign();

            Campaign = state;
            EventLog = new CampaignEventLog();
            Simulation = new CampaignSimulation(Campaign, _campaignSettings);
            Encounters = new EncounterCoordinator(Campaign, _campaignSettings, SceneFlow, EventLog);
            ResultApplier = new BattleResultApplier(Campaign, _campaignSettings, EventLog);

            _ticker.SetClock(Campaign.Clock);

            ServiceLocator.Register(Campaign);
            ServiceLocator.Register(Campaign.Clock);
            ServiceLocator.Register(EventLog);

            Simulation.Notified += OnSimulationNotified;

            if (events != null && events.Count > 0)
            {
                for (int i = 0; i < events.Count; i++)
                    EventLog.Push((CampaignEventKind)events[i].Kind, events[i].Headline, events[i].Detail, events[i].Day);
                EventLog.Push(CampaignEventKind.Discovery, "The march resumes", "Loaded from a saved game",
                    Campaign.Clock.Now.DayNumber);
            }
            else
            {
                EventLog.Push(
                    CampaignEventKind.Discovery,
                    Campaign.Onboarding.IsActive ? "The survivors gather" : "The column forms up",
                    $"{Campaign.PlayerParty.Roster.ActiveCount} men answer the muster",
                    Campaign.Clock.Now.DayNumber);
            }
        }

        private void EndCampaign()
        {
            if (Campaign == null) return;

            if (Simulation != null) Simulation.Notified -= OnSimulationNotified;
            Encounters?.Dispose();
            Simulation?.Dispose();

            ServiceLocator.Unregister<CampaignState>();
            ServiceLocator.Unregister<CampaignClock>();
            ServiceLocator.Unregister<CampaignEventLog>();
            ServiceLocator.Unregister<BattleRequest>();

            _ticker.SetClock(null);
            _ticker.IsRunning = false;

            Campaign = null;
            Simulation = null;
            Encounters = null;
            ResultApplier = null;
            EventLog = null;
        }

        /// <summary>The opening sequence is done: the story moves to the Aftermath.</summary>
        private void CompleteOpening()
        {
            if (Campaign == null) return;
            if (Campaign.Onboarding.Chapter == OnboardingChapter.Opening)
                Campaign.Onboarding.Chapter = OnboardingChapter.Aftermath;
        }

        private void OnSimulationNotified(PartyState party, string message)
        {
            if (!party.IsPlayer || EventLog == null || Campaign == null) return;
            EventLog.Push(CampaignEventKind.Supply, message, string.Empty, Campaign.Clock.Now.DayNumber);
        }

        private void Update()
        {
            // Designer tool: F5 on the overmap spins up a TEST battle exactly where the column
            // stands, against a comparable synthetic warband — the fastest way to see what ground
            // the world function births at any spot. The forged party ids match nothing in the
            // campaign, so the result applier ignores the outcome (only the clock pays the visit).
            if (!Input.GetKeyDown(KeyCode.F5)) return;
            if (_ticker == null || !_ticker.IsRunning) return;   // overmap only
            if (Campaign?.PlayerParty == null || Encounters == null || Encounters.Pending.HasValue) return;

            Vector3 here = Campaign.PlayerParty.WorldPosition;
            int romans = Mathf.Clamp(Campaign.PlayerParty.Roster.CombatReadyCount, 9, 96);

            var config = new BattleTestConfig
            {
                RomanCount = romans,
                GermanCount = Mathf.Clamp(Mathf.RoundToInt(romans * 1.1f), 8, 120),
                SkipDeployment = true,
                WorldX = here.x,
                WorldZ = here.z,
                HourOfDay = (float)(Campaign.Clock.Now.TotalHours % 24d)
            };

            EventLog.Push(CampaignEventKind.Discovery, "Test battle",
                $"On this ground ({here.x:0}, {here.z:0})", Campaign.Clock.Now.DayNumber);

            SceneFlow.LoadBattle(BattleTestForge.Build(config));
        }

        /// <summary>Return leg of the battle contract. Called by the battle scene, whatever it is.</summary>
        void IBattleResultSink.Submit(BattleResult result)
        {
            if (Campaign == null)
            {
                SceneFlow.LoadTitle();
                return;
            }

            ResultApplier.Apply(result);
            Encounters.ClearPending();

            // The opening sequence reports as Aborted (there is nothing to apply); its finishing is
            // the story's business, and the story moves to the Aftermath.
            if (Campaign.Onboarding.Chapter == OnboardingChapter.Opening)
            {
                CompleteOpening();
                Debug.Log("[Campaign] The opening is done. The Aftermath begins.");
                SceneFlow.LoadOvermap();
                return;
            }

            // The century is lost: nobody left to march. Back to the title.
            PartyState player = Campaign.PlayerParty;
            if (player == null || player.Roster.ActiveCount <= 0)
            {
                TitleNotice = "THE CENTURY IS LOST";
                Debug.Log("[Campaign] The century is lost. Returning to the title.");
                SceneFlow.LoadTitle();
                return;
            }

            Debug.Log($"[Campaign] Battle ended: {result.Outcome}. Returning to the overmap.");
            SceneFlow.LoadOvermap();
        }

        private void OnGameplaySceneLoaded(string sceneName)
        {
            // The campaign clock only runs on the overmap. Battle uses its own real-time clock and
            // reports elapsed time back through BattleResult.
            _ticker.IsRunning = Campaign != null && sceneName == SceneNames.Overmap;
            _fader?.FadeIn();

            // WebGL only: swap Polytope-shaded decor for stock URP materials (their custom
            // shaders fail on the web target and render pink). No-op everywhere else.
            Century.Core.World.WebGlShaderFallback.Sweep();

            // The title sits over no campaign: whatever was running is closed behind it.
            if (sceneName == SceneNames.Title) EndCampaign();
        }

        private void OnGameplaySceneUnloading(string sceneName)
        {
            _ticker.IsRunning = false;
            _fader?.SnapToBlack();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (SceneFlow != null)
            {
                SceneFlow.GameplaySceneLoaded -= OnGameplaySceneLoaded;
                SceneFlow.GameplaySceneUnloading -= OnGameplaySceneUnloading;
            }

            EndCampaign();
            ServiceLocator.Clear();
            Instance = null;
        }
    }
}
