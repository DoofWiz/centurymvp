using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Resolves a thrown pilum or javelin that has reached a man. Kept as plain sim code — the flight
    /// is physics in the view, but what the blow *does* lives here, shield- and facing-aware like the
    /// melee resolver, so it stays deterministic and testable.
    /// </summary>
    public sealed class MissileResolver
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly System.Random _random;

        public MissileResolver(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;
            _random = new System.Random(state.RandomSeed ^ 0x7A1C);
        }

        public void ResolveHit(BattleCombatant attacker, BattleCombatant defender)
        {
            if (defender == null || !defender.IsAlive) return;

            float rawDot = FrontDot(attacker, defender);
            float frontal = Mathf.Clamp01(rawDot);

            // A pilum is built to punch through a shield, so the wall helps far less than it does in
            // melee — but a testudo, made to be missile-proof, still turns some aside.
            float shieldStop = ShieldStopFor(defender);
            float block = shieldStop * frontal * _settings.PilaShieldBlock;

            float facing = FacingMultiplier(rawDot);
            float variance = (float)(_random.NextDouble() * 0.4d + 0.8d);
            float damage = _settings.PilaDamage * facing * (1f - Mathf.Clamp01(block)) * variance;

            defender.Health01 = Mathf.Max(0f, defender.Health01 - damage);
            defender.WasHitThisTick = true;
            defender.Morale01 = Mathf.Clamp01(defender.Morale01 - damage * 0.5f);
            defender.TimeSinceCombat = 0f;

            if (defender.Health01 <= 0f)
            {
                if (attacker != null) attacker.Kills++;
                defender.Target = null;
                defender.IsWoundedOut = CasualtyFate.RollWoundedOut(_state, _settings, defender, _random);
            }
        }

        private float ShieldStopFor(BattleCombatant defender)
        {
            if (defender.IsPlayerControlled) return _settings.PlayerShieldBlockChance;
            BattleSquad squad = FindSquadOf(defender);
            return squad != null ? squad.Formation.FrontalBlockChance() : 0f;
        }

        private static float FacingMultiplier(float rawDot)
        {
            if (rawDot > 0.45f) return 1f;      // took it on the front
            if (rawDot > -0.45f) return 1.5f;   // flank
            return 2.0f;                        // in the back
        }

        private static float FrontDot(BattleCombatant attacker, BattleCombatant defender)
        {
            if (attacker == null) return 1f;
            Vector3 toAttacker = attacker.WorldPosition - defender.WorldPosition;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return 1f;
            return Vector3.Dot(toAttacker.normalized, SquadFormationSolver.FlatFacing(defender.Facing));
        }

        private BattleSquad FindSquadOf(BattleCombatant man)
        {
            List<BattleSquad> squads = man.IsPlayerSide ? _state.PlayerSquads : _state.EnemySquads;
            if (man.SquadIndex >= 0 && man.SquadIndex < squads.Count) return squads[man.SquadIndex];
            return null;
        }
    }
}
