using System.Collections.Generic;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Holds recent battle events for display, and tracks the single most urgent one so the HUD can
    /// show the banner from the mock-up: "OUR MEN ARE BREAKING! Rally them, Centurion!"
    /// </summary>
    /// <remarks>
    /// A ring of recent lines plus one prominent alert, rather than a scrolling log. During a fight
    /// the player has no attention to spare for reading history — what matters is the one thing going
    /// wrong right now.
    /// </remarks>
    public sealed class BattleEventFeed : MonoBehaviour
    {
        [SerializeField] private int _capacity = 6;
        [SerializeField] private float _alertHoldSeconds = 4f;

        private readonly List<BattleEvent> _events = new List<BattleEvent>();

        public IReadOnlyList<BattleEvent> Events => _events;

        public bool HasAlert { get; private set; }
        public string AlertMessage { get; private set; }
        public BattleEventKind AlertKind { get; private set; }

        private float _alertExpiry;

        public void Subscribe(BattleSimulation simulation)
        {
            if (simulation != null) simulation.EventRaised += Push;
        }

        public void Push(BattleEvent battleEvent)
        {
            _events.Add(battleEvent);
            if (_events.Count > _capacity) _events.RemoveAt(0);

            if (battleEvent.Kind != BattleEventKind.Warning && battleEvent.Kind != BattleEventKind.Critical)
                return;

            // Critical always overrides; a warning does not shout over a critical still on screen.
            if (HasAlert && AlertKind == BattleEventKind.Critical && battleEvent.Kind == BattleEventKind.Warning)
                return;

            HasAlert = true;
            AlertKind = battleEvent.Kind;
            AlertMessage = battleEvent.Message;
            _alertExpiry = Time.time + _alertHoldSeconds;
        }

        private void Update()
        {
            if (HasAlert && Time.time >= _alertExpiry) HasAlert = false;
        }
    }
}
