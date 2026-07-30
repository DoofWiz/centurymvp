using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Physical melee for both sides. A man winds up, strikes, and recovers; a blow is resolved by
    /// geometry — does the blade reach a body in the frontal arc, and is a raised shield in the way —
    /// not by a hit-chance roll. The Centurion's blows come from input; everyone else's from a small
    /// per-man driver keyed off his weapon (a swordsman holds a line behind his shield; a spearman
    /// keeps his distance and lunges).
    /// </summary>
    /// <remarks>
    /// Replaces the old MeleeResolver but keeps the signals the rest of the sim reads: kills come from
    /// <see cref="BattleCombatant.Health01"/> reaching zero, engagement from
    /// <see cref="BattleCombatant.TimeSinceCombat"/>, and targeting from <see cref="BattleCombatant.Target"/>.
    /// Resolution is vector math against known positions, so it scales to a full battle without a
    /// hundred physics colliders; the *view* poses real weapon and shield objects for the feel.
    /// </remarks>
    public sealed class MeleeCombat
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly System.Random _random;

        private float _dt;

        public MeleeCombat(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;
            _random = new System.Random(state.RandomSeed ^ 0x4A17);
        }

        public void Tick(float deltaSeconds)
        {
            _dt = deltaSeconds;

            ClearHitFlags(_state.PlayerSquads);
            ClearHitFlags(_state.EnemySquads);
            if (_state.PlayerCharacter != null)
            {
                _state.PlayerCharacter.WasHitThisTick = false;
                _state.PlayerCharacter.WasBlockThisTick = false;
                _state.PlayerCharacter.WasGuardBreakThisTick = false;
            }

            MarkHostileProximity();

            TickSide(_state.PlayerSquads);
            TickSide(_state.EnemySquads);
            TickPlayer();
        }

        /// <summary>
        /// One squad-vs-squad sweep marking who has an enemy within shields-up range, so the guard
        /// decision for every idle man is a flag read instead of a field scan.
        /// </summary>
        private void MarkHostileProximity()
        {
            // Half a frontage of slack: the squad centre lags its nearest man by roughly that much.
            float range = _settings.ShieldsUpRange + _settings.SquadFrontage * 0.5f;
            float rangeSqr = range * range;

            for (int p = 0; p < _state.PlayerSquads.Count; p++) _state.PlayerSquads[p].HostileNearby = false;
            for (int e = 0; e < _state.EnemySquads.Count; e++) _state.EnemySquads[e].HostileNearby = false;

            for (int p = 0; p < _state.PlayerSquads.Count; p++)
            {
                BattleSquad player = _state.PlayerSquads[p];
                if (player.IsOffField || !player.IsEffective) continue;

                for (int e = 0; e < _state.EnemySquads.Count; e++)
                {
                    BattleSquad enemy = _state.EnemySquads[e];
                    if (enemy.IsOffField || !enemy.IsEffective) continue;

                    if (FlatSqr(player.CentreOfMass(), enemy.CentreOfMass()) > rangeSqr) continue;
                    player.HostileNearby = true;
                    enemy.HostileNearby = true;
                }
            }
        }

        private static void ClearHitFlags(List<BattleSquad> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    members[m].WasHitThisTick = false;
                    members[m].WasBlockThisTick = false;
                    members[m].WasGuardBreakThisTick = false;
                }
            }
        }

        private void TickSide(List<BattleSquad> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                BattleSquad squad = squads[s];
                if (squad.IsOffField) continue;   // reinforcements are not on the field to fight
                bool routed = squad.IsRouted;

                for (int m = 0; m < squad.Members.Count; m++)
                {
                    BattleCombatant man = squad.Members[m];
                    if (!man.IsAlive) continue;
                    UpdateMan(man, squad, routed);
                }
            }
        }

        private void UpdateMan(BattleCombatant man, BattleSquad squad, bool routed)
        {
            man.TimeSinceCombat += _dt;
            man.AttackCooldown -= _dt;
            if (man.StaggerTimer > 0f) man.StaggerTimer -= _dt;

            if (routed)
            {
                man.Target = null;
                man.Stance = MeleeStance.Idle;
                man.ShieldRaised = false;
                man.StaggerTimer = 0f;
                man.DesiredRange = 0f;
                return;
            }

            if (man.IsStaggered)
            {
                // Reeling from a broken guard: no strike, no shield — only keep a target and let any
                // blow already in flight finish its recovery.
                man.ShieldRaised = false;
                AcquireTarget(man, squad);
                AdvanceStance(man, squad, isPlayer: false);
                return;
            }

            AcquireTarget(man, squad);
            DriveNpc(man, squad);
            AdvanceStance(man, squad, isPlayer: false);
        }

        private void TickPlayer()
        {
            BattleCombatant player = _state.PlayerCharacter;
            if (player == null || !player.IsAlive) return;

            player.TimeSinceCombat += _dt;
            player.AttackCooldown -= _dt;
            if (player.StaggerTimer > 0f) player.StaggerTimer -= _dt;
            if (player.IsStaggered) player.ShieldRaised = false;
            AdvanceStance(player, null, isPlayer: true);
        }

        // --- Player intents (called from input) ------------------------------------------------

        public void PlayerBeginCharge()
        {
            BattleCombatant p = _state.PlayerCharacter;
            if (p == null || !p.IsAlive || p.IsStaggered) return;
            if (p.Stance == MeleeStance.Idle) { p.Stance = MeleeStance.Charging; p.Charge01 = 0f; }
        }

        public void PlayerSlash() => BeginPlayerStrike(AttackForm.Slash, 0f);

        public void PlayerThrust(float charge01) => BeginPlayerStrike(AttackForm.Thrust, Mathf.Clamp01(charge01));

        public void SetPlayerShield(bool raised)
        {
            BattleCombatant p = _state.PlayerCharacter;
            if (p == null || !p.IsAlive) return;
            p.ShieldRaised = raised && p.HasShield && !p.IsStaggered && p.Stance != MeleeStance.Striking;
        }

        private void BeginPlayerStrike(AttackForm form, float charge01)
        {
            BattleCombatant p = _state.PlayerCharacter;
            if (p == null || !p.IsAlive || p.IsStaggered) return;
            if (p.Stance != MeleeStance.Idle && p.Stance != MeleeStance.Charging) return;

            p.Form = form;
            p.Charge01 = charge01;
            EnterStriking(p, MeleeProfile.For(p.Weapon), null);
        }

        // --- NPC driver ------------------------------------------------------------------------

        private void DriveNpc(BattleCombatant man, BattleSquad squad)
        {
            BattleCombatant target = man.Target;
            MeleeProfile profile = MeleeProfile.For(man.Weapon);

            if (target == null || !target.IsAlive)
            {
                // No one to fight yet, but the boards come up when a warband is bearing down — the
                // guard must precede the target, or a line receives its first volley bare-armed.
                man.ShieldRaised = man.HasShield
                                   && man.Stamina01 > profile.GuardFloor
                                   && squad.HostileNearby;
                man.DesiredRange = 0f;
                return;
            }

            float distance = FlatDistance(man.WorldPosition, target.WorldPosition);

            // A lone target — the surrounded Centurion with no shield-mates beside him — is swarmed, not
            // fenced with: the spearmen drop their standoff and close to finish him rather than dancing
            // at spear length around a man they outnumber.
            bool swarm = target.IsPlayerControlled;

            // Footwork: a swordsman closes and holds; a spearman keeps his distance and lunges.
            if (man.Weapon == WeaponClass.Spear)
                man.DesiredRange = (man.IsAttacking || swarm) ? profile.Reach * 0.7f : profile.Standoff;
            else
                man.DesiredRange = profile.Reach * 0.85f;

            // Block first: the shield comes up as soon as a foe is anywhere near — well before blade
            // range, because a volley or a charge is exactly when the board matters — and drops only
            // for the brief stab itself (the Striking frame). A man blown below his board's guard
            // floor cannot hold it up at all: the shield comes down and he gasps, which is what makes
            // a tiring line visibly sag before it cracks.
            man.ShieldRaised = man.HasShield && man.Stance != MeleeStance.Striking
                               && man.Stamina01 > profile.GuardFloor
                               && distance <= _settings.ShieldsUpRange;

            // A spearman commits from his standoff and closes during the wind-up; a swordsman only
            // when already at reach. Either way the strike resolves once he has stepped in.
            float triggerRange = profile.Standoff > 0.1f ? profile.Standoff + 1f : profile.Reach + 0.3f;

            if (man.Stance == MeleeStance.Idle && man.AttackCooldown <= 0f
                && distance <= triggerRange && man.Stamina01 > 0.15f)
                BeginNpcAttack(man, profile);
        }

        private void BeginNpcAttack(BattleCombatant man, MeleeProfile profile)
        {
            // A swordsman stabs for the gap; a spearman hacks at the wall of shields.
            double roll = _random.NextDouble();
            man.Form = man.Weapon == WeaponClass.Spear
                ? roll < 0.75 ? AttackForm.Slash : AttackForm.Thrust
                : roll < 0.65 ? AttackForm.Thrust : AttackForm.Slash;

            man.Charge01 = man.Form == AttackForm.Thrust ? 1f : 0f;
            man.Stance = MeleeStance.Charging;

            // A worn man winds up slower. His class is his campaign condition, so a force-marched
            // squad fights at a visibly duller tempo from the first blow.
            float tempo = StaminaProfile.For(man.Stamina).ActionMultiplier;
            man.StanceTimer = profile.ChargeSeconds * (man.Form == AttackForm.Thrust ? 1f : 0.4f) * tempo;
            man.StrikeResolved = false;
            man.ShieldRaised = false;
        }

        // --- Stance machine --------------------------------------------------------------------

        private void AdvanceStance(BattleCombatant man, BattleSquad squad, bool isPlayer)
        {
            MeleeProfile profile = MeleeProfile.For(man.Weapon);

            switch (man.Stance)
            {
                case MeleeStance.Charging:
                    if (isPlayer) break;   // the player holds the wind-up until he releases
                    man.StanceTimer -= _dt;
                    if (man.StanceTimer <= 0f) EnterStriking(man, profile, squad);
                    break;

                case MeleeStance.Striking:
                    man.StanceTimer -= _dt;
                    if (man.StanceTimer <= 0f)
                    {
                        man.Stance = MeleeStance.Recovering;
                        man.StanceTimer = profile.RecoverSeconds * StaminaProfile.For(man.Stamina).ActionMultiplier;
                    }
                    break;

                case MeleeStance.Recovering:
                    man.StanceTimer -= _dt;
                    if (man.StanceTimer <= 0f)
                    {
                        man.Stance = MeleeStance.Idle;
                        man.AttackCooldown = profile.AttackInterval * Range(0.8f, 1.2f)
                                             * StaminaProfile.For(man.Stamina).ActionMultiplier;
                    }
                    break;
            }
        }

        private void EnterStriking(BattleCombatant man, MeleeProfile profile, BattleSquad squad)
        {
            man.Stance = MeleeStance.Striking;
            man.StanceTimer = profile.StrikeSeconds;
            man.StrikeResolved = false;
            man.StrikeDir = StrikeDirection(man);
            man.Stamina01 = Mathf.Max(0f, man.Stamina01 - _settings.MeleeStaminaPerStrike);

            ResolveStrike(man, profile, squad);
        }

        private Vector3 StrikeDirection(BattleCombatant man)
        {
            if (man.Target != null && man.Target.IsAlive)
            {
                Vector3 to = man.Target.WorldPosition - man.WorldPosition;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) return to.normalized;
            }

            Vector3 facing = man.Facing;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
        }

        // --- Strike resolution -----------------------------------------------------------------

        private void ResolveStrike(BattleCombatant man, MeleeProfile profile, BattleSquad squad)
        {
            if (man.StrikeResolved) return;
            man.StrikeResolved = true;
            man.TimeSinceCombat = 0f;

            Vector3 pos = man.WorldPosition;
            Vector3 dir = man.StrikeDir;
            float reach = profile.Reach + (man.Form == AttackForm.Thrust ? 0.4f : 0f);

            BattleCombatant victim = FindVictim(man, pos, dir, reach, profile.FrontalArcDot);
            if (victim == null) return;

            victim.TimeSinceCombat = 0f;

            // A raised shield turns the blow — but only within the board's own frontal cone, and only
            // while its bearer still has the wind to hold it. Two things beat a shield now: coming at it
            // from outside the cone (a flanker, or a man who has stepped to the side in the scrum), and
            // catching a bearer whose stamina is spent, whose guard breaks and staggers open.
            if (victim.HasShield && victim.ShieldRaised && !victim.IsStaggered
                && victim.Stance != MeleeStance.Striking)
            {
                MeleeProfile board = MeleeProfile.For(victim.Weapon);
                bool inCone = FrontDot(man, victim) > board.BlockArcDot;
                if (inCone)
                {
                    if (victim.Stamina01 > board.GuardFloor)
                    {
                        // The board holds, at a cost in wind.
                        float cost = board.BlockStaminaCost * (man.Form == AttackForm.Thrust ? 1.5f : 1f);
                        victim.Stamina01 = Mathf.Max(0f, victim.Stamina01 - cost);
                        victim.WasBlockThisTick = true;
                        return;
                    }

                    // Guard-break: too tired to hold. The blow staggers through for reduced damage,
                    // empties his wind, and drops his guard for a beat — the wall cracking. A worn
                    // man reels longer; a man of the "No Shield but Courage" doctrine, less.
                    victim.Stamina01 = 0f;
                    victim.WasGuardBreakThisTick = true;
                    float staggerScale = victim.IsPlayerSide && _state.HasSkill("no_shield_but_courage") ? 0.6f : 1f;
                    victim.StaggerTimer = _settings.StaggerSeconds * staggerScale
                                          * StaminaProfile.For(victim.Stamina).ActionMultiplier;
                    victim.ShieldRaised = false;
                    ApplyWound(man, victim, profile, squad, _settings.StaggerDamageFraction);
                    return;
                }
                // Outside the cone: the shield is no help — the blow lands in full below.
            }

            ApplyWound(man, victim, profile, squad, 1f);
        }

        /// <summary>Score a landed blow onto <paramref name="victim"/>, scaled by band, condition, facing
        /// and an optional <paramref name="damageScale"/> (a staggered guard lets only part of it through).</summary>
        private void ApplyWound(BattleCombatant man, BattleCombatant victim, MeleeProfile profile,
            BattleSquad squad, float damageScale)
        {
            float baseDamage = man.Form == AttackForm.Thrust
                ? profile.ThrustDamage * (0.6f + 0.4f * man.Charge01)
                : profile.SlashDamage;

            float band = squad != null ? squad.Band.CombatMultiplier() : 1f;
            float condition = Mathf.Lerp(0.6f, 1f, man.Health01) * Mathf.Lerp(0.7f, 1f, man.Stamina01);
            float facing = FacingMultiplier(man, victim);

            // High ground: blows fall harder downhill and land weaker climbing. This is the first
            // way the sculpted field reaches into the melee itself.
            float ground = 1f + Mathf.Clamp(
                (man.WorldPosition.y - victim.WorldPosition.y) * _settings.HighGroundDamagePerMetre,
                -_settings.HighGroundDamageCap, _settings.HighGroundDamageCap);

            float damage = baseDamage * band * condition * facing * ground * man.DamageMultiplier
                           * _settings.MeleeDamageScale * damageScale * Range(0.85f, 1.15f);

            victim.Health01 = Mathf.Max(0f, victim.Health01 - damage);
            victim.WasHitThisTick = true;
            victim.Morale01 = Mathf.Clamp01(victim.Morale01 - damage * 0.4f);

            if (victim.Health01 <= 0f)
            {
                man.Kills++;
                victim.Target = null;

                // Cut down is not always killed: the field decides now, the burial detail later.
                victim.IsWoundedOut = CasualtyFate.RollWoundedOut(_state, _settings, victim, _random);

                // Blooded: for the commander's own men, a kill answers something.
                if (man.IsPlayerSide && _state.HasSkill("blooded"))
                    man.Morale01 = Mathf.Clamp01(man.Morale01 + 0.05f);
            }
        }

        /// <summary>Nearest opposing man the blade actually reaches, within the frontal arc.</summary>
        private BattleCombatant FindVictim(
            BattleCombatant man, Vector3 pos, Vector3 dir, float reach, float arcDot)
        {
            BattleCombatant best = null;
            float bestSqr = float.MaxValue;
            float bodyRadius = _settings.BodyRadius;

            List<BattleSquad> opponents = OpposingSquads(man);
            for (int s = 0; s < opponents.Count; s++)
            {
                if (opponents[s].IsOffField) continue;
                List<BattleCombatant> members = opponents[s].Members;
                for (int i = 0; i < members.Count; i++)
                {
                    BattleCombatant e = members[i];
                    if (!e.IsAlive) continue;
                    if (Reaches(pos, dir, reach, arcDot, bodyRadius, e.WorldPosition, out float sqr) && sqr < bestSqr)
                    {
                        best = e;
                        bestSqr = sqr;
                    }
                }
            }

            if (!man.IsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive
                    && Reaches(pos, dir, reach, arcDot, bodyRadius, player.WorldPosition, out float sqr) && sqr < bestSqr)
                    best = player;
            }

            return best;
        }

        private static bool Reaches(
            Vector3 pos, Vector3 dir, float reach, float arcDot, float bodyRadius, Vector3 target, out float sqr)
        {
            Vector3 to = target - pos;
            to.y = 0f;
            sqr = to.sqrMagnitude;

            float distance = Mathf.Sqrt(sqr);
            if (distance > reach + bodyRadius) return false;

            Vector3 toDir = distance > 0.001f ? to / distance : dir;
            if (Vector3.Dot(toDir, dir) < arcDot) return false;

            // Distance from the target to the blade segment [pos, pos + dir*reach].
            float along = Mathf.Clamp(Vector3.Dot(to, dir), 0f, reach);
            Vector3 closest = pos + dir * along;
            Vector3 perp = target - closest;
            perp.y = 0f;
            return perp.sqrMagnitude <= bodyRadius * bodyRadius;
        }

        // --- Target acquisition ----------------------------------------------------------------

        private void AcquireTarget(BattleCombatant man, BattleSquad squad)
        {
            MeleeProfile profile = MeleeProfile.For(man.Weapon);

            // The leash is measured from the man's SLOT, not his live position — this is what stops a
            // line being walked off the field one lunge at a time. How far past reach he may engage is
            // set by his squad's order (hold keeps a short tether, skirmish a long one).
            Vector3 tether = SquadFormationSolver.GetWorldSlot(squad, man.SlotIndex, _settings);
            float leash = profile.Reach + SquadTactics.AggressionLeash(squad) + 0.75f;
            float leashSqr = leash * leash;

            if (man.Target != null && man.Target.IsAlive)
            {
                if (FlatSqr(man.Target.WorldPosition, tether) <= leashSqr) return;
                man.Target = null;
            }

            BattleCombatant best = null;
            float bestSqr = leashSqr;

            List<BattleSquad> opponents = OpposingSquads(man);
            for (int s = 0; s < opponents.Count; s++)
            {
                if (opponents[s].IsOffField) continue;
                List<BattleCombatant> members = opponents[s].Members;
                for (int i = 0; i < members.Count; i++)
                {
                    BattleCombatant e = members[i];
                    if (!e.IsAlive) continue;
                    float sqr = FlatSqr(e.WorldPosition, tether);
                    if (sqr > bestSqr) continue;
                    best = e;
                    bestSqr = sqr;
                }
            }

            if (!man.IsPlayerSide)
            {
                BattleCombatant player = _state.PlayerCharacter;
                if (player != null && player.IsAlive)
                {
                    float sqr = FlatSqr(player.WorldPosition, tether);
                    if (sqr <= bestSqr) best = player;
                }
            }

            man.Target = best;
        }

        // --- Helpers ---------------------------------------------------------------------------

        private List<BattleSquad> OpposingSquads(BattleCombatant man) =>
            man.IsPlayerSide ? _state.EnemySquads : _state.PlayerSquads;

        private float FacingMultiplier(BattleCombatant attacker, BattleCombatant defender)
        {
            Vector3 toAttacker = attacker.WorldPosition - defender.WorldPosition;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return 1f;

            float dot = Vector3.Dot(toAttacker.normalized, SquadFormationSolver.FlatFacing(defender.Facing));
            if (dot > 0.45f) return 1f;
            if (dot > -0.45f) return _settings.FlankDamageMultiplier;
            return _settings.RearDamageMultiplier;
        }

        private static float FrontDot(BattleCombatant attacker, BattleCombatant defender)
        {
            Vector3 toAttacker = attacker.WorldPosition - defender.WorldPosition;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return 1f;
            return Vector3.Dot(toAttacker.normalized, SquadFormationSolver.FlatFacing(defender.Facing));
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Mathf.Sqrt(FlatSqr(a, b));

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}
