using System;
using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// The signum as a world object (army phase 3). The signifer carries it; when he falls it is
    /// ON THE GROUND, and whoever reaches it first decides what kind of day this is: a man of the
    /// century raises it again, an enemy carries it away. Battles gain a second objective beyond
    /// killing, and the signifer's aura becomes something you can watch fall over.
    /// </summary>
    /// <remarks>
    /// Capture is passive by design: no enemy AI hunts the signum, it changes hands because the
    /// press of melee happens to roll over it. That keeps the drama emergent and the system small.
    /// The morale meaning of every state lives in <see cref="MoraleSystem"/> (aura source) and in
    /// the one-time shocks this system applies at each transition.
    /// </remarks>
    public sealed class SignumSystem
    {
        private const float ScanIntervalSeconds = 0.25f;

        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        private float _scanAccumulator;

        public event Action<BattleEvent> EventRaised;

        public SignumSystem(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;

            // Lost in an earlier battle: there is no signum to carry until it is won back.
            if (state.Signum == SignumStatus.Lost) return;

            // The signum takes the field with the signifer. No signifer, no signum — the office's
            // vacancy is felt as an absence, exactly as the brief wants.
            BattleCombatant bearer = FindPlayerSide(OfficerRole.Signifer);
            if (bearer == null) return;

            _state.Signum = SignumStatus.Carried;
            _state.SignumCarrier = bearer;
            _state.SignumPosition = bearer.WorldPosition;
        }

        public void Tick(float deltaSeconds)
        {
            switch (_state.Signum)
            {
                case SignumStatus.Carried:
                case SignumStatus.EnemyHeld:
                    TickCarried();
                    break;

                case SignumStatus.Fallen:
                    _scanAccumulator += deltaSeconds;
                    if (_scanAccumulator >= ScanIntervalSeconds)
                    {
                        _scanAccumulator = 0f;
                        TickFallen();
                    }
                    break;
            }
        }

        private void TickCarried()
        {
            BattleCombatant carrier = _state.SignumCarrier;
            bool enemyHands = _state.Signum == SignumStatus.EnemyHeld;

            if (carrier == null || !carrier.IsAlive)
            {
                Fall(enemyHands);
                return;
            }

            _state.SignumPosition = carrier.WorldPosition;

            if (enemyHands)
            {
                _state.SignumPlanted = false;

                // A broken enemy who gets far enough from the field is gone, and it with him.
                Vector3 flat = carrier.WorldPosition; flat.y = 0f;
                if (flat.sqrMagnitude > _settings.SignumLostRadius * _settings.SignumLostRadius)
                {
                    _state.Signum = SignumStatus.Lost;
                    _state.SignumCarrier = null;
                    Raise(BattleEventKind.Critical, "The signum is carried from the field!");
                }
                return;
            }

            // Planted: the bearer stands steady out of the press, and the standard reaches farther.
            BattleSquad squad = SquadOf(carrier);
            _state.SignumPlanted = squad != null
                && !squad.IsRouted
                && squad.Order == SquadOrder.HoldPosition
                && !carrier.IsInCombat;
        }

        private void Fall(bool fromEnemyHands)
        {
            _state.Signum = SignumStatus.Fallen;
            _state.SignumCarrier = null;
            _state.SignumPlanted = false;

            if (fromEnemyHands)
            {
                // Its thief is dead. It lies in the open again, and that is GOOD news.
                Raise(BattleEventKind.Good, "The signum's captor is down — it lies in the open!");
                return;
            }

            _state.SignumEverFell = true;
            ShockPlayerSquads(_settings.SignumFallCohesionHit);
            Raise(BattleEventKind.Critical, "The signum has fallen! Raise it before they take it!");
        }

        private void TickFallen()
        {
            float radiusSqr = _settings.SignumPickupRadius * _settings.SignumPickupRadius;

            // A man of the century reaches it first — the Centurion himself counts.
            BattleCombatant rescuer = FindNear(_state.SignumPosition, radiusSqr, playerSide: true);
            if (rescuer != null)
            {
                _state.Signum = SignumStatus.Carried;
                _state.SignumCarrier = rescuer;
                SurgePlayerSquads(_settings.SignumRecoveredCohesionBonus);
                Raise(BattleEventKind.Good, $"{rescuer.DisplayName} raises the signum!");
                return;
            }

            BattleCombatant thief = FindNear(_state.SignumPosition, radiusSqr, playerSide: false);
            if (thief != null)
            {
                _state.Signum = SignumStatus.EnemyHeld;
                _state.SignumCarrier = thief;
                ShockPlayerSquads(_settings.SignumCapturedCohesionHit);
                Raise(BattleEventKind.Critical, "They have taken the signum! Take it back!");
            }
        }

        // --- Shocks ------------------------------------------------------------------------------

        private void ShockPlayerSquads(float amount)
        {
            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                if (squad.IsDestroyed || squad.IsOffField) continue;
                squad.Cohesion01 = Mathf.Clamp01(squad.Cohesion01 - amount);
            }
        }

        private void SurgePlayerSquads(float amount)
        {
            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                if (squad.IsDestroyed || squad.IsOffField || squad.IsRouted) continue;
                squad.Cohesion01 = Mathf.Clamp01(squad.Cohesion01 + amount);
            }
        }

        // --- Lookups -----------------------------------------------------------------------------

        private BattleCombatant FindPlayerSide(OfficerRole role)
        {
            for (int s = 0; s < _state.PlayerSquads.Count; s++)
            {
                List<BattleCombatant> members = _state.PlayerSquads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].IsAlive && members[m].Role == role) return members[m];
            }
            return null;
        }

        private BattleCombatant FindNear(Vector3 point, float radiusSqr, bool playerSide)
        {
            if (playerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive
                    && (player.WorldPosition - point).sqrMagnitude <= radiusSqr)
                    return player;
            }

            List<BattleSquad> squads = playerSide ? _state.PlayerSquads : _state.EnemySquads;
            for (int s = 0; s < squads.Count; s++)
            {
                BattleSquad squad = squads[s];
                if (squad.IsOffField) continue;

                // A routing man does not stop for anything, least of all a flag.
                if (squad.IsRouted) continue;

                List<BattleCombatant> members = squad.Members;
                for (int m = 0; m < members.Count; m++)
                {
                    BattleCombatant man = members[m];
                    if (!man.IsAlive) continue;
                    if ((man.WorldPosition - point).sqrMagnitude <= radiusSqr) return man;
                }
            }
            return null;
        }

        private BattleSquad SquadOf(BattleCombatant man)
        {
            for (int s = 0; s < _state.PlayerSquads.Count; s++)
                if (_state.PlayerSquads[s].Members.Contains(man)) return _state.PlayerSquads[s];
            return null;
        }

        private void Raise(BattleEventKind kind, string message) =>
            EventRaised?.Invoke(new BattleEvent(kind, message, _state.ElapsedSeconds));
    }
}
