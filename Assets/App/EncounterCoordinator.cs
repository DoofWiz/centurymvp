using System;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Campaign.View;
using Century.Core;
using Century.Core.Contracts;

namespace Century.App
{
    /// <summary>
    /// Turns an overmap contact into a battle. This is the translation layer: it is the only class
    /// that sees both a <see cref="PartyState"/> and a <see cref="BattleRequest"/>.
    /// </summary>
    public sealed class EncounterCoordinator : IDisposable
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;
        private readonly SceneFlowService _sceneFlow;
        private readonly CampaignEventLog _log;
        private bool _disposed;

        /// <summary>The encounter awaiting resolution, held so the result can be attributed on return.</summary>
        public EncounterContact? Pending { get; private set; }

        public EncounterCoordinator(
            CampaignState state, CampaignSettings settings, SceneFlowService sceneFlow, CampaignEventLog log = null)
        {
            _state = state;
            _settings = settings;
            _sceneFlow = sceneFlow;
            _log = log;

            OvermapDirectorRunner.ContactDetected += OnContactDetected;
        }

        private void OnContactDetected(EncounterContact contact)
        {
            if (_sceneFlow.IsTransitioning || Pending.HasValue) return;

            Pending = contact;

            _log?.Push(
                CampaignEventKind.Threat,
                contact.Player.IsSurprised(_state.Clock.Now)
                    ? "Ambushed!"
                    : $"Engaged {contact.Enemy.DisplayName}",
                $"{contact.Enemy.Roster.ActiveCount} warriors",
                _state.Clock.Now.DayNumber);

            _sceneFlow.LoadBattle(BuildRequest(contact));
        }

        private BattleRequest BuildRequest(EncounterContact contact)
        {
            // Ambush is a state of surprise, not a question of who was walking toward whom. Both
            // sides carry surprise windows opened by the ambush director — the column's by a sudden
            // close reveal, the enemy's by a stealthy approach they marked far too late. Battle
            // joined inside a window is an ambush; anything else is a fair meeting, deployment
            // and all. When both are flat-footed, the one who blundered INTO the other suffers.
            bool playerAmbushed = contact.Player.IsSurprised(_state.Clock.Now);
            bool enemyAmbushed = !playerAmbushed && contact.Enemy.IsSurprised(_state.Clock.Now);

            // The tesserarius capstone: the watch is so tight the century is never caught unformed.
            if (playerAmbushed && PostTreeCatalog.Invested(_state, "no_surprises"))
            {
                playerAmbushed = false;
                _log?.Push(CampaignEventKind.Discovery, "No surprises",
                    "The watch had them marked — the century forms in time", _state.Clock.Now.DayNumber);
            }

            // Surprise is spent the moment it matters, whichever way the fight goes.
            contact.Player.SurprisedUntil = default;
            contact.Enemy.SurprisedUntil = default;

            // How a commander comes to battle says what kind of commander they are becoming.
            if (enemyAmbushed) _state.Commander.Note(CommanderPhilosophy.Survivor, 1.5f);

            var request = new BattleRequest
            {
                PlayerPartyId = contact.Player.Id,
                EnemyPartyId = contact.Enemy.Id,
                EnemyDisplayName = contact.Enemy.DisplayName,
                TimeOfDay = _state.Clock.Now,
                PlayerAmbushed = playerAmbushed,
                EnemyAmbushed = enemyAmbushed,
                EnemyBehaviour = contact.Enemy.Behaviour,

                // The battlefield is sculpted from the world where the armies actually met.
                WorldX = contact.Player.WorldPosition.x,
                WorldZ = contact.Player.WorldPosition.z,

                // Derived rather than random so the same encounter always plays out the same way.
                RandomSeed = _state.RandomSeed
                             ^ contact.Enemy.Id.GetHashCode()
                             ^ (int)_state.Clock.Now.TotalMinutes,

                CommanderSkills = new System.Collections.Generic.List<string>(
                    _state.Commander.OwnedSkills),
                CapturedBanners = System.Math.Min(2, contact.Player.Inventory.CountOf("banner_cherusci")),

                // The establishment goes to war: offices and their traditions, flattened for battle.
                PostEffects = PostTreeCatalog.Resolve(_state, contact.Player.Roster),

                // TODO(step 4): sample the terrain under the contact point.
                TerrainId = "open"
            };

            // "The Strongest Lead": with the doctrine taken, a man's hard-won experience finally
            // tells in the exchange of blows.
            bool veterancyTells = _state.Commander.Has("the_strongest_lead");

            AppendCombatants(request.PlayerCombatants, contact.Player, markPlayerControlled: true, veterancyTells);
            AppendCombatants(request.EnemyCombatants, contact.Enemy, markPlayerControlled: false, false);

            return request;
        }

        private static void AppendCombatants(
            System.Collections.Generic.List<CombatantSpec> target, PartyState party,
            bool markPlayerControlled, bool veterancyTells)
        {
            SoldierRecord commander = markPlayerControlled ? party.Roster.FindByRank("centurion") : null;

            // The century fights in its tent groups: make sure every man has one, then carry the
            // assignment across the contract so the battle forms the same squads the camp shows.
            // Warbands stay ungrouped (-1) and are dealt into squads by the battle factory as before.
            if (markPlayerControlled) ContuberniumLedger.EnsureAssigned(party.Roster);

            for (int i = 0; i < party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = party.Roster.Soldiers[i];
                if (!soldier.IsCombatReady) continue;   // the badly wounded stay with the baggage

                // An office held by appointment IS the man's battle role: the appointed signifer
                // carries the standard whatever rank he was raised from.
                string rankId = soldier.RankId;
                if (markPlayerControlled
                    && party.Appointments.HoldsAnyRole(soldier.Id, out CampRole heldRole))
                    rankId = PostRoster.PostFor(heldRole) ?? rankId;

                target.Add(new CombatantSpec
                {
                    SoldierId = soldier.Id,
                    DisplayName = soldier.DisplayName,
                    RankId = rankId,
                    ArchetypeId = soldier.ArchetypeId,
                    Health01 = soldier.Health01,
                    Stamina01 = soldier.Stamina01,
                    Morale01 = soldier.Morale01,
                    IsPlayerControlled = commander != null && ReferenceEquals(soldier, commander),
                    GroupIndex = markPlayerControlled ? soldier.Contubernium : -1,
                    IsGroupLeader = markPlayerControlled && soldier.IsDecanus,
                    DamageMultiplier = veterancyTells ? soldier.VeterancyMultiplier : 1f
                });
            }
        }

        public void ClearPending() => Pending = null;

        public void Dispose()
        {
            if (_disposed) return;
            OvermapDirectorRunner.ContactDetected -= OnContactDetected;
            _disposed = true;
        }
    }
}
