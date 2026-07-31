using System;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Orchestrates the battle systems on their own cadences and reports when the fight is over.
    /// </summary>
    /// <remarks>
    /// Each subsystem has a different natural tick rate: melee at 5Hz, morale at 2Hz, enemy decisions
    /// slower still. Running them on fixed accumulators rather than per frame keeps behaviour
    /// framerate-independent and makes the cost predictable with a hundred men on the field.
    ///
    /// Note that the views are not driven from here. They read model state each frame and interpolate
    /// toward it, which is what stops a 5Hz combat tick from looking like a 5Hz game.
    /// </remarks>
    public sealed class BattleSimulation
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        private readonly MeleeCombat _combat;
        private readonly MoraleSystem _morale;
        private readonly MedicusSystem _medicus;
        private readonly EnemySquadAi _enemyAi;
        private readonly BattleOutcomeEvaluator _outcome;

        private float _moraleAccumulator;

        public event Action<BattleEvent> EventRaised;

        public bool IsConcluded { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Aborted;

        /// <summary>Why the battle ended, for the outcome banner.</summary>
        public string OutcomeReason => _outcome.Reason;

        /// <summary>Physical melee. The bootstrap feeds the Centurion's slash/thrust/shield into it.</summary>
        public MeleeCombat Combat => _combat;

        public BattleSimulation(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;

            _combat = new MeleeCombat(state, settings);
            _morale = new MoraleSystem(state, settings);
            _medicus = new MedicusSystem(state, settings);
            _enemyAi = new EnemySquadAi(state, settings);
            _outcome = new BattleOutcomeEvaluator(state, settings);

            _morale.EventRaised += e => EventRaised?.Invoke(e);
        }

        /// <summary>
        /// Advances the battle. <paramref name="rallyHeld"/> comes from input and is passed through
        /// rather than read here, so the simulation stays free of any dependency on Unity's Input.
        /// </summary>
        public void Tick(float deltaSeconds, bool rallyHeld)
        {
            if (IsConcluded) return;

            _state.ElapsedSeconds += deltaSeconds;

            _enemyAi.Tick(deltaSeconds);

            // Melee runs every frame now, so swings and shield blocks are smooth rather than stepped.
            _combat.Tick(deltaSeconds);

            _moraleAccumulator += deltaSeconds;
            if (_moraleAccumulator >= _settings.MoraleTickSeconds)
            {
                UpdateDressedFractions();
                _morale.Tick(_moraleAccumulator);
                _medicus.Tick(_moraleAccumulator);
                _moraleAccumulator = 0f;
            }

            TickSuccession(deltaSeconds);
            _morale.TickRally(deltaSeconds, rallyHeld);
            RecoverStamina(deltaSeconds);
            ClearFallen();

            if (!_outcome.TryEvaluate(deltaSeconds, out BattleOutcome outcome)) return;

            IsConcluded = true;
            Outcome = outcome;
        }

        /// <summary>
        /// Recomputes how well each squad is holding its shape. Fed by the views, which know where
        /// the men actually are, and consumed by the morale system.
        /// </summary>
        private void UpdateDressedFractions()
        {
            UpdateDressed(_state.PlayerSquads);
            UpdateDressed(_state.EnemySquads);
        }

        private void UpdateDressed(List<BattleSquad> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                BattleSquad squad = squads[s];
                int alive = 0;
                int inStation = 0;

                for (int m = 0; m < squad.Members.Count; m++)
                {
                    BattleCombatant man = squad.Members[m];
                    if (!man.IsAlive) continue;
                    alive++;

                    Vector3 slot = SquadFormationSolver.GetWorldSlot(squad, man.SlotIndex, _settings);
                    if ((man.WorldPosition - slot).sqrMagnitude <= 4f) inStation++;
                }

                squad.Dressed01 = alive <= 0 ? 0f : inStation / (float)alive;
            }
        }

        private void RecoverStamina(float deltaSeconds)
        {
            float recovery = _settings.StaminaRecoveryPerSecond * deltaSeconds;

            RecoverSide(_state.PlayerSquads, recovery);
            RecoverSide(_state.EnemySquads, recovery);

            BattleCombatant player = _state.PlayerCharacter;
            if (player != null && player.IsAlive && CanRecover(player)) Recover(player, recovery);
        }

        private static void RecoverSide(List<BattleSquad> squads, float recovery)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (!man.IsAlive || !CanRecover(man)) continue;
                    Recover(man, recovery);
                }
            }
        }

        /// <summary>A tired man catches his breath slowly and never back to full — his class caps both.
        /// Behind a raised shield he recovers at half pace rather than not at all: a hard block on
        /// regen while guarding let two exhausted, shielded lines freeze facing each other forever,
        /// since neither could ever regain the wind to swing again.</summary>
        private static void Recover(BattleCombatant man, float baseRecovery)
        {
            StaminaProfile profile = StaminaProfile.For(man.Stamina);
            float guarded = man.ShieldRaised ? 0.5f : 1f;
            man.Stamina01 = Mathf.Min(
                profile.MaxStamina,
                man.Stamina01 + baseRecovery * profile.RecoveryMultiplier * guarded);
        }

        /// <summary>Swinging a blade is work, and no one recovers mid-fight — but any two-second lull
        /// lets men breathe, which is what turns mutual exhaustion into an ebb rather than a freeze.</summary>
        private static bool CanRecover(BattleCombatant man) =>
            !man.IsInCombat && !man.IsAttacking;

        /// <summary>Clears dangling references to the dead so nobody keeps swinging at a corpse.</summary>
        private void ClearFallen()
        {
            ClearFallenSide(_state.PlayerSquads);
            ClearFallenSide(_state.EnemySquads);

            BattleCombatant player = _state.PlayerCharacter;
            if (player != null && player.Target != null && !player.Target.IsAlive) player.Target = null;
        }

        private static void ClearFallenSide(List<BattleSquad> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (man.Target != null && !man.Target.IsAlive) man.Target = null;
                }
            }
        }

        /// <summary>Centurio in Waiting: the moment the Centurion goes down with an Optio still
        /// standing, command devolves and the window opens. The evaluator reads the clock.</summary>
        private void TickSuccession(float deltaSeconds)
        {
            BattleCombatant player = _state.PlayerCharacter;

            if (!_state.CommandDevolved)
            {
                if (player != null && !player.IsAlive)
                {
                    _state.CommandDevolved = true;
                    _state.SuccessionSecondsLeft = _settings.SuccessionWindowSeconds
                                                   + _state.Effects.SuccessionWindowBonusSeconds;
                }
                return;
            }

            if (_state.SuccessionSecondsLeft > 0f)
                _state.SuccessionSecondsLeft = Mathf.Max(0f, _state.SuccessionSecondsLeft - deltaSeconds);
        }
    }
}
