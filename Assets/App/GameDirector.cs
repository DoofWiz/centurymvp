using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Composition root. Lives in the persistent Boot scene, constructs the campaign, registers the
    /// long-lived services and owns scene flow. This is the only place in the project that is
    /// allowed to be a singleton.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameDirector : MonoBehaviour, IBattleResultSink
    {
        [Header("Configuration")]
        [SerializeField] private CampaignSettings _campaignSettings;
        [SerializeField] private int _campaignSeed = 90409;
        [SerializeField] private bool _loadOvermapOnStart = true;

        [Header("Runtime")]
        [SerializeField] private CampaignTicker _ticker;

        public static GameDirector Instance { get; private set; }

        public CampaignState Campaign { get; private set; }
        public SceneFlowService SceneFlow { get; private set; }
        public CampaignSimulation Simulation { get; private set; }
        public EncounterCoordinator Encounters { get; private set; }
        public BattleResultApplier ResultApplier { get; private set; }
        public CampaignEventLog EventLog { get; private set; }

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

            BuildServices();
        }

        private void BuildServices()
        {
            ServiceLocator.Clear();

            Campaign = NewCampaignFactory.Create(_campaignSeed, _campaignSettings);
            EventLog = new CampaignEventLog();
            Simulation = new CampaignSimulation(Campaign, _campaignSettings);
            SceneFlow = new SceneFlowService(this);
            Encounters = new EncounterCoordinator(Campaign, _campaignSettings, SceneFlow, EventLog);
            ResultApplier = new BattleResultApplier(Campaign, _campaignSettings, EventLog);

            if (_ticker == null) _ticker = gameObject.AddComponent<CampaignTicker>();
            _ticker.Initialise(Campaign.Clock, _campaignSettings);

            ServiceLocator.Register(Campaign);
            ServiceLocator.Register(Campaign.Clock);
            ServiceLocator.Register(_campaignSettings);
            ServiceLocator.Register(SceneFlow);
            ServiceLocator.Register<ISceneNavigator>(SceneFlow);
            ServiceLocator.Register(_ticker);
            ServiceLocator.Register(EventLog);
            ServiceLocator.Register<IBattleResultSink>(this);

            SceneFlow.GameplaySceneLoaded += OnGameplaySceneLoaded;
            SceneFlow.GameplaySceneUnloading += OnGameplaySceneUnloading;

            Simulation.Notified += OnSimulationNotified;

            EventLog.Push(
                CampaignEventKind.Discovery,
                "The column forms up",
                $"{Campaign.PlayerParty.Roster.ActiveCount} men answer the muster",
                Campaign.Clock.Now.DayNumber);
        }

        private void OnSimulationNotified(PartyState party, string message)
        {
            if (!party.IsPlayer) return;
            EventLog.Push(CampaignEventKind.Supply, message, string.Empty, Campaign.Clock.Now.DayNumber);
        }

        private void Start()
        {
            if (_loadOvermapOnStart) SceneFlow.LoadOvermap();
        }

        /// <summary>Return leg of the battle contract. Called by the battle scene, whatever it is.</summary>
        void IBattleResultSink.Submit(BattleResult result)
        {
            ResultApplier.Apply(result);
            Encounters.ClearPending();

            Debug.Log($"[Campaign] Battle ended: {result.Outcome}. Returning to the overmap.");
            SceneFlow.LoadOvermap();
        }

        private void OnGameplaySceneLoaded(string sceneName)
        {
            // The campaign clock only runs on the overmap. Battle uses its own real-time clock and
            // reports elapsed time back through BattleResult.
            _ticker.IsRunning = sceneName == SceneNames.Overmap;
        }

        private void OnGameplaySceneUnloading(string sceneName)
        {
            _ticker.IsRunning = false;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (SceneFlow != null)
            {
                SceneFlow.GameplaySceneLoaded -= OnGameplaySceneLoaded;
                SceneFlow.GameplaySceneUnloading -= OnGameplaySceneUnloading;
            }

            if (Simulation != null) Simulation.Notified -= OnSimulationNotified;

            Encounters?.Dispose();
            Simulation?.Dispose();
            ServiceLocator.Clear();
            Instance = null;
        }
    }
}
