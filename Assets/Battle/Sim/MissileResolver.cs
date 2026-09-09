using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Resolves a thrown missile that has reached a man. The shield rule is the melee rule: a raised
    /// board is a WALL for the arc it covers — a pilum, a javelin or a stone arriving inside that
    /// cover is stopped outright at a cost in wind, never discounted by a percentage. What beats a
    /// shield is geometry (a throw from a quarter the board does not cover reaches the body first)
    /// or exhaustion (a guard too blown to hold, which staggers open) — exactly as for a sword blow.
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

        /// <summary><paramref name="travelDir"/> is the missile's direction of flight at impact, so
        /// the block is judged against where the throw actually came from, not where the thrower has
        /// wandered to since he loosed it.</summary>
        public void ResolveHit(BattleCombatant attacker, BattleCombatant defender, Vector3 travelDir)
        {
            if (defender == null || !defender.IsAlive) return;

            // A man locked in an execution — either role — is outside the fight; stray shafts miss.
            if (defender.InExecution) return;

            MissileProfile missile = MissileProfile.For(attacker != null ? attacker.Missile : MissileClass.Pila);

            // The quarter the missile came FROM, as the defender stands: flatten the flight line and
            // reverse it. A throw whose line is lost (a spent shaft dropping straight down) falls
            // back to the thrower's position.
            Vector3 from = -travelDir;
            from.y = 0f;
            if (from.sqrMagnitude < 0.0001f && attacker != null)
            {
                from = attacker.WorldPosition - defender.WorldPosition;
                from.y = 0f;
            }
            float rawDot = from.sqrMagnitude < 0.0001f
                ? 1f
                : Vector3.Dot(from.normalized, SquadFormationSolver.FlatFacing(defender.Facing));

            // A killed man falls the way the shaft was travelling: away from the quarter it came from.
            if (from.sqrMagnitude > 0.0001f) defender.LastHitDirection = -from.normalized;

            if (defender.HasShield && defender.ShieldRaised && !defender.IsStaggered
                && defender.Stance != MeleeStance.Striking)
            {
                MeleeProfile board = MeleeProfile.For(defender.Weapon);

                // The Centurion's facing follows a mouse, not a drilled front: his board covers a
                // wider arc than a ranker's, exactly as it does in the melee.
                float blockArc = defender.IsPlayerControlled
                    ? Mathf.Min(board.BlockArcDot, 0.2f)
                    : board.BlockArcDot;

                // Testudo is the formation built for exactly this moment: boards over heads as well
                // as fronts, so the roof covers every quarter.
                bool covered = rawDot > blockArc || FormationOf(defender) == FormationType.Testudo;
                if (covered)
                {
                    if (defender.Stamina01 > board.GuardFloor)
                    {
                        // Caught on the board: no wound, only wind — and no combat-clock reset,
                        // because a volley weathered behind a shield is endured, not fought.
                        defender.Stamina01 = Mathf.Max(
                            0f, defender.Stamina01 - board.BlockStaminaCost * missile.BlockStaminaScale);
                        defender.WasBlockThisTick = true;
                        return;
                    }

                    // Guard-break, mirroring the melee: the blow staggers through a spent guard for
                    // partial damage, empties his wind and drops the board for a beat.
                    defender.Stamina01 = 0f;
                    defender.WasGuardBreakThisTick = true;
                    float staggerScale = defender.IsPlayerSide && _state.HasSkill("no_shield_but_courage") ? 0.6f : 1f;
                    defender.StaggerTimer = _settings.StaggerSeconds * staggerScale
                                            * StaminaProfile.For(defender.Stamina).ActionMultiplier;
                    defender.ShieldRaised = false;
                    ApplyWound(attacker, defender, missile, rawDot, _settings.StaggerDamageFraction);
                    return;
                }
                // Outside the cover: the missile reaches the body first and the board does nothing.
            }

            ApplyWound(attacker, defender, missile, rawDot, 1f);
        }

        private void ApplyWound(
            BattleCombatant attacker, BattleCombatant defender, MissileProfile missile,
            float rawDot, float damageScale)
        {
            float facing = FacingMultiplier(rawDot);
            float variance = (float)(_random.NextDouble() * 0.4d + 0.8d);
            float damage = _settings.PilaDamage * missile.DamageScale * facing * variance * damageScale;

            // The Centurion's death ends the battle; stray volley fire must not decide that alone.
            if (defender.IsPlayerControlled) damage *= _settings.CommanderMissileDamageFactor;

            // The rally's shelter covers the volleys too.
            if (defender.IsPlayerSide && _state.RallyActive) damage *= _settings.RallyDamageTakenFactor;

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

        private FormationType FormationOf(BattleCombatant defender)
        {
            BattleSquad squad = FindSquadOf(defender);
            return squad != null ? squad.Formation : FormationType.Loose;
        }

        private static float FacingMultiplier(float rawDot)
        {
            if (rawDot > 0.45f) return 1f;      // took it on the front
            if (rawDot > -0.45f) return 1.5f;   // flank
            return 2.0f;                        // in the back
        }

        private BattleSquad FindSquadOf(BattleCombatant man)
        {
            if (man.IsPlayerControlled) return null;   // the Centurion stands in no squad
            List<BattleSquad> squads = man.IsPlayerSide ? _state.PlayerSquads : _state.EnemySquads;
            if (man.SquadIndex >= 0 && man.SquadIndex < squads.Count) return squads[man.SquadIndex];
            return null;
        }
    }
}
