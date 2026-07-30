using System;
using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Squad cohesion, rout, and rally. This is the system that decides battles.
    /// </summary>
    /// <remarks>
    /// A century is destroyed long before it is killed. Cohesion is therefore the real health bar,
    /// and everything else — casualties, being flanked, fatigue, the commander's presence — feeds it.
    ///
    /// Rout is deliberately punishing and deliberately recoverable. A routing squad turns its back,
    /// and blows from behind do more than twice damage, so letting a squad break is expensive.
    /// But the Centurion can rally it by standing there and holding the line, which is the moment
    /// the game is built around.
    /// </remarks>
    public sealed class MoraleSystem
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        /// <summary>Bands from the previous tick, so transitions can be reported once each.</summary>
        private readonly Dictionary<BattleSquad, CohesionBand> _lastBand =
            new Dictionary<BattleSquad, CohesionBand>();

        private readonly Dictionary<BattleSquad, int> _lastAliveCount = new Dictionary<BattleSquad, int>();

        /// <summary>Strength each squad started the battle at, for the depletion penalty.</summary>
        private readonly Dictionary<BattleSquad, int> _initialCount = new Dictionary<BattleSquad, int>();

        private Vector3? _playerStandard;
        private Vector3? _enemyStandard;

        public event Action<BattleEvent> EventRaised;

        public MoraleSystem(BattleState state, BattleSettings settings)
        {
            _state = state;
            _settings = settings;

            Register(state.PlayerSquads);
            Register(state.EnemySquads);
        }

        private void Register(List<BattleSquad> squads)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                _lastBand[squads[i]] = squads[i].Band;
                _lastAliveCount[squads[i]] = squads[i].AliveCount;
                _initialCount[squads[i]] = squads[i].AliveCount;
            }
        }

        public void Tick(float deltaSeconds)
        {
            _playerStandard = FindStandard(_state.PlayerSquads);
            _enemyStandard = FindStandard(_state.EnemySquads);

            TickSide(_state.PlayerSquads, _state.EnemySquads, isPlayerSide: true, deltaSeconds);
            TickSide(_state.EnemySquads, _state.PlayerSquads, isPlayerSide: false, deltaSeconds);
        }

        private void TickSide(
            List<BattleSquad> squads, List<BattleSquad> hostile, bool isPlayerSide, float deltaSeconds)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                BattleSquad squad = squads[i];
                if (squad.IsDestroyed) continue;

                // Reinforcements wait beyond the field: no fear reaches them, none leaves them.
                if (squad.IsOffField) continue;

                int lostThisTick = AccumulateCasualtyPressure(squad, deltaSeconds);
                UpdateCommandAura(squad, isPlayerSide);

                float delta = 0f;
                float shock = CasualtyShock(squad, lostThisTick);

                // "No Man Breaks Rank": the commander's own squads grieve later.
                if (isPlayerSide && _state.HasSkill("no_man_breaks_rank")) shock *= 0.75f;

                delta -= shock;
                delta -= LocalDisadvantage(squad, hostile, deltaSeconds);
                delta -= Disorder(squad, deltaSeconds);
                delta -= Depletion(squad, deltaSeconds);
                delta -= Fatigue(squad, deltaSeconds);
                delta += Support(squad, deltaSeconds);
                delta += StandardSupport(squad, isPlayerSide, deltaSeconds);
                delta += Recovery(squad, deltaSeconds);

                // An optio absorbs shock rather than adding heart. Historically his post was the rear
                // rank, and his job was stopping men leaving it.
                if (delta < 0f && squad.HasOfficer(OfficerRole.Optio)) delta *= _settings.OptioLossMultiplier;

                // "Iron Discipline": under the commander's own eye, fear moves slower.
                if (delta < 0f && isPlayerSide && squad.UnderCommandAura && _state.HasSkill("iron_discipline"))
                    delta *= 0.8f;

                squad.Cohesion01 = Mathf.Clamp01(squad.Cohesion01 + delta);

                UpdateRout(squad, isPlayerSide);
                ReportTransitions(squad);
            }
        }

        /// <summary>Tracks fresh losses. The pressure value itself only marks "men just died here"
        /// (it gates recovery); the cohesion COST of a death is charged once, in CasualtyShock.</summary>
        private int AccumulateCasualtyPressure(BattleSquad squad, float deltaSeconds)
        {
            int alive = squad.AliveCount;
            if (!_lastAliveCount.TryGetValue(squad, out int previous)) previous = alive;

            int lost = previous - alive;
            if (lost > 0) squad.RecentCasualtyPressure += lost;

            squad.RecentCasualtyPressure = Mathf.Max(
                0f, squad.RecentCasualtyPressure - _settings.CasualtyPressureDecay * deltaSeconds);

            _lastAliveCount[squad] = alive;
            return Mathf.Max(0, lost);
        }

        /// <summary>
        /// A ONE-TIME cohesion cost per man lost — exactly what the tuning field promises. The old
        /// form charged the field's value per second for as long as the fear of a death lingered,
        /// which integrated to several times the stated cost and grew superlinearly when deaths
        /// clustered: a single pila volley could erase a squad's entire cohesion bar twice over,
        /// which is why lines routed on contact.
        /// </summary>
        private float CasualtyShock(BattleSquad squad, int lostThisTick)
        {
            if (lostThisTick <= 0) return 0f;
            return lostThisTick * _settings.CohesionLossPerCasualty;
        }

        /// <summary>Being locally outnumbered is what actually frightens men, not overall army size.</summary>
        private float LocalDisadvantage(BattleSquad squad, List<BattleSquad> hostile, float deltaSeconds)
        {
            if (squad.EngagedCount == 0) return 0f;

            Vector3 centre = squad.CentreOfMass();
            float radiusSqr = _settings.LocalPressureRadius * _settings.LocalPressureRadius;

            int friends = CountNear(squad.IsPlayerSide ? _state.PlayerSquads : _state.EnemySquads, centre, radiusSqr);
            int foes = CountNear(hostile, centre, radiusSqr);

            if (_state.PlayerCharacter != null && _state.PlayerCharacter.IsAlive && squad.IsPlayerSide
                && (_state.PlayerCharacter.WorldPosition - centre).sqrMagnitude <= radiusSqr)
                friends++;

            if (foes <= friends) return 0f;

            float ratio = Mathf.Clamp01((foes - friends) / (float)Mathf.Max(1, friends));
            return ratio * _settings.OutnumberedCohesionPerSecond * deltaSeconds;
        }

        private static int CountNear(List<BattleSquad> squads, Vector3 point, float radiusSqr)
        {
            int count = 0;
            for (int s = 0; s < squads.Count; s++)
            {
                if (squads[s].IsRouted) continue;
                List<BattleCombatant> members = squads[s].Members;

                for (int m = 0; m < members.Count; m++)
                {
                    if (!members[m].IsAlive) continue;
                    if ((members[m].WorldPosition - point).sqrMagnitude <= radiusSqr) count++;
                }
            }

            return count;
        }

        /// <summary>A squad that has lost its shape loses its nerve with it.</summary>
        private float Disorder(BattleSquad squad, float deltaSeconds)
        {
            if (squad.Dressed01 >= 0.65f) return 0f;
            return (0.65f - squad.Dressed01) * _settings.DisorderCohesionPerSecond * deltaSeconds;
        }

        /// <summary>A squad that has been gutted loses heart; the near-last survivor most of all.</summary>
        private float Depletion(BattleSquad squad, float deltaSeconds)
        {
            int initial = _initialCount.TryGetValue(squad, out int start) ? start : squad.Members.Count;
            if (initial <= 0) return 0f;

            int alive = squad.AliveCount;
            float lostFraction = 1f - alive / (float)initial;
            float drain = lostFraction * _settings.DepletionCohesionPerSecond * deltaSeconds;

            if (alive > 0 && alive <= 2) drain += _settings.DepletionCohesionPerSecond * 1.5f * deltaSeconds;

            return drain;
        }

        /// <summary>Exhausted men lose their nerve as their arms tire.</summary>
        private float Fatigue(BattleSquad squad, float deltaSeconds)
        {
            float tired = Mathf.Max(0f, 0.55f - squad.AverageStamina01) / 0.55f;
            return tired * _settings.FatigueCohesionPerSecond * deltaSeconds;
        }

        /// <summary>The standard steadies the men who can see it.</summary>
        private float StandardSupport(BattleSquad squad, bool isPlayerSide, float deltaSeconds)
        {
            Vector3? standard = isPlayerSide ? _playerStandard : _enemyStandard;
            if (!standard.HasValue) return 0f;

            // "By the Eagle": the standard reaches farther and grips harder for the player's side.
            bool eagle = isPlayerSide && _state.HasSkill("by_the_eagle");
            float radius = _settings.StandardAuraRadius * (eagle ? 1.35f : 1f);

            if ((standard.Value - squad.CentreOfMass()).sqrMagnitude > radius * radius) return 0f;

            return _settings.StandardCohesionPerSecond * (eagle ? 1.25f : 1f) * deltaSeconds;
        }

        /// <summary>
        /// The living standard-bearer of a side: a Signifer if there is one, else an Optio, else the
        /// first man still standing. Null once the last of them falls (or all near squads have broken).
        /// </summary>
        private static Vector3? FindStandard(List<BattleSquad> squads)
        {
            BattleCombatant bearer = FirstAliveWithRole(squads, OfficerRole.Signifer)
                                     ?? FirstAliveWithRole(squads, OfficerRole.Optio)
                                     ?? FirstAlive(squads);

            return bearer != null ? bearer.WorldPosition : (Vector3?)null;
        }

        private static BattleCombatant FirstAliveWithRole(List<BattleSquad> squads, OfficerRole role)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                if (squads[s].IsRouted) continue;
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].IsAlive && members[m].Role == role) return members[m];
            }
            return null;
        }

        private static BattleCombatant FirstAlive(List<BattleSquad> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                if (squads[s].IsRouted) continue;
                List<BattleCombatant> members = squads[s].Members;
                for (int m = 0; m < members.Count; m++)
                    if (members[m].IsAlive) return members[m];
            }
            return null;
        }

        private float Support(BattleSquad squad, float deltaSeconds)
        {
            float support = 0f;

            if (squad.UnderCommandAura)
            {
                // "Follow Me!": the commander's presence in the press counts half again as much.
                float presence = squad.IsPlayerSide && _state.HasSkill("follow_me") ? 1.5f : 1f;
                support += _settings.CommandAuraCohesionPerSecond * presence * deltaSeconds;
            }

            if (squad.HasOfficer(OfficerRole.Optio)) support += _settings.OptioCohesionPerSecond * deltaSeconds;

            return support;
        }

        private float Recovery(BattleSquad squad, float deltaSeconds)
        {
            if (squad.EngagedCount > 0 || squad.RecentCasualtyPressure > 0.5f) return 0f;
            return _settings.CohesionRecoveryPerSecond * deltaSeconds * squad.Dressed01;
        }

        private void UpdateCommandAura(BattleSquad squad, bool isPlayerSide)
        {
            squad.UnderCommandAura = false;
            if (!isPlayerSide) return;

            BattleCombatant player = _state.PlayerCharacter;
            if (player == null || !player.IsAlive) return;

            // "The Centurion's Voice" carries the commander's reach 30% farther.
            float radius = _settings.CommandAuraRadius
                           * (_state.HasSkill("centurions_voice") ? 1.3f : 1f);
            squad.UnderCommandAura =
                (player.WorldPosition - squad.CentreOfMass()).sqrMagnitude <= radius * radius;
        }

        private void UpdateRout(BattleSquad squad, bool isPlayerSide)
        {
            if (squad.IsRouted) return;
            if (squad.Cohesion01 > _settings.RoutThreshold) return;

            squad.IsRouted = true;
            squad.RallyProgress01 = 0f;
            squad.Order = SquadOrder.Fallback;
            squad.Formation = FormationType.Loose;
            squad.PendingOrder = null;
            squad.PendingFormation = null;
            squad.RoutDestination = ComputeRoutDestination(squad, isPlayerSide);

            for (int i = 0; i < squad.Members.Count; i++) squad.Members[i].Target = null;
        }

        /// <summary>
        /// Broken men run for the map edge and off it — directly AWAY from the nearest enemy
        /// concentration, not down a compass axis. A hard-coded axis sent men who broke on the far
        /// side of a melee fleeing straight through the enemy, which no frightened man ever did.
        /// </summary>
        private Vector3 ComputeRoutDestination(BattleSquad squad, bool isPlayerSide)
        {
            Vector3 centre = squad.CentreOfMass();
            List<BattleSquad> hostiles = isPlayerSide ? _state.EnemySquads : _state.PlayerSquads;

            Vector3 threat = Vector3.zero;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < hostiles.Count; i++)
            {
                if (!hostiles[i].IsEffective || hostiles[i].IsOffField) continue;

                Vector3 hostileCentre = hostiles[i].CentreOfMass();
                float sqr = (hostileCentre - centre).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                threat = hostileCentre;
            }

            Vector3 away = bestSqr < float.MaxValue ? centre - threat : (isPlayerSide ? Vector3.back : Vector3.forward);
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = isPlayerSide ? Vector3.back : Vector3.forward;

            return centre + away.normalized * Mathf.Max(_settings.RoutDistance, 160f);
        }

        /// <summary>
        /// Called while the player holds the rally input. Progress accumulates only while he is close
        /// and stops the moment he leaves, so rallying costs him his own position in the line.
        /// </summary>
        public void TickRally(float deltaSeconds, bool rallyHeld)
        {
            BattleCombatant player = _state.PlayerCharacter;
            if (player == null || !player.IsAlive) return;

            float radiusSqr = _settings.RallyRadius * _settings.RallyRadius;

            for (int i = 0; i < _state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = _state.PlayerSquads[i];
                if (!squad.IsRouted || squad.IsDestroyed) continue;

                bool inRange = (player.WorldPosition - squad.CentreOfMass()).sqrMagnitude <= radiusSqr;

                if (!rallyHeld || !inRange)
                {
                    squad.RallyProgress01 = Mathf.Max(0f, squad.RallyProgress01 - deltaSeconds * 0.5f);
                    continue;
                }

                squad.RallyProgress01 += deltaSeconds / Mathf.Max(0.1f, _settings.RallyDurationSeconds);
                if (squad.RallyProgress01 < 1f) continue;

                Rally(squad);
            }
        }

        private void Rally(BattleSquad squad)
        {
            squad.IsRouted = false;
            squad.RallyProgress01 = 0f;
            squad.Cohesion01 = _settings.RalliedCohesion;
            squad.RecentCasualtyPressure = 0f;
            squad.Order = SquadOrder.FollowMe;
            squad.Formation = FormationType.Line;

            for (int i = 0; i < squad.Members.Count; i++)
                if (squad.Members[i].IsAlive)
                    squad.Members[i].Morale01 = Mathf.Max(squad.Members[i].Morale01, _settings.RalliedCohesion);

            Raise(BattleEventKind.Good, $"{squad.DisplayName} rallied to the standard.");
        }

        private void ReportTransitions(BattleSquad squad)
        {
            CohesionBand band = squad.Band;
            if (!_lastBand.TryGetValue(squad, out CohesionBand previous)) previous = band;
            _lastBand[squad] = band;

            if (band == previous || !squad.IsPlayerSide) return;

            switch (band)
            {
                case CohesionBand.Broken:
                    Raise(BattleEventKind.Critical, $"{squad.DisplayName} has broken!");
                    break;
                case CohesionBand.Breaking when previous > CohesionBand.Breaking:
                    Raise(BattleEventKind.Warning, $"{squad.DisplayName} is breaking! Rally them, Centurion!");
                    break;
                case CohesionBand.Wavering when previous > CohesionBand.Wavering:
                    Raise(BattleEventKind.Warning, $"{squad.DisplayName} is wavering.");
                    break;
                case CohesionBand.Confident when previous < CohesionBand.Confident:
                    Raise(BattleEventKind.Good, $"{squad.DisplayName} has steadied.");
                    break;
            }
        }

        private void Raise(BattleEventKind kind, string message) =>
            EventRaised?.Invoke(new BattleEvent(kind, message, _state.ElapsedSeconds));
    }
}
