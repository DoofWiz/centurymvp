using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Writes a <see cref="BattleResult"/> back into campaign state: casualties, condition, loot,
    /// elapsed time and contact cooldowns.
    /// </summary>
    /// <remarks>
    /// Deliberately the only place that mutates the roster after a fight. When something goes wrong
    /// with "my men came back wrong", there is exactly one file to read.
    /// </remarks>
    public sealed class BattleResultApplier
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;
        private readonly CampaignEventLog _log;

        public BattleResultApplier(CampaignState state, CampaignSettings settings, CampaignEventLog log = null)
        {
            _state = state;
            _settings = settings;
            _log = log;
        }

        public void Apply(BattleResult result)
        {
            if (result == null || result.Outcome == BattleOutcome.Aborted) return;

            PartyState player = _state.FindParty(result.PlayerPartyId);
            PartyState enemy = _state.FindParty(result.EnemyPartyId);

            _state.Clock.Advance(result.MinutesElapsed);

            if (player != null) ApplyToPlayer(player, result);
            if (enemy != null) ApplyToEnemy(enemy, result);

            ApplyCooldowns(player, enemy);
            PruneDisbandedParties();
        }

        /// <summary>
        /// Removes disbanded parties from the world outright rather than leaving inert husks in the
        /// list. The overmap already skips them when spawning views, but leaving them in means every
        /// proximity and AI scan keeps paying for parties that no longer exist.
        /// </summary>
        private void PruneDisbandedParties()
        {
            for (int i = _state.Parties.Count - 1; i >= 0; i--)
            {
                PartyState party = _state.Parties[i];
                if (!party.IsDisbanded || party.IsPlayer) continue;

                _state.Parties.RemoveAt(i);
            }
        }

        private void ApplyToPlayer(PartyState player, BattleResult result)
        {
            int wounded = 0;
            bool anyRouted = false;

            for (int i = 0; i < result.Combatants.Count; i++)
            {
                CombatantOutcome outcome = result.Combatants[i];
                if (outcome.Routed) anyRouted = true;

                SoldierRecord soldier = player.Roster.Find(outcome.SoldierId);
                if (soldier == null) continue;

                // A wounded man was cut down but found alive when the field was cleared: he comes
                // home at a sliver of health — not combat-ready, slowing the column, a job for the
                // medicine chest — instead of into the ground.
                soldier.Health01 = outcome.Survived ? Mathf.Max(0.01f, outcome.Health01)
                    : outcome.Wounded ? 0.06f
                    : 0f;
                soldier.Stamina01 = outcome.Stamina01;
                soldier.Morale01 = outcome.Morale01;
                soldier.Kills += outcome.Kills;

                if (!outcome.Survived)
                {
                    if (outcome.Wounded) wounded++;
                    continue;   // no experience for the part of the battle spent face-down
                }

                soldier.Experience += outcome.ExperienceGained;
                soldier.BattlesSurvived++;

                // Breaking leaves a mark beyond the battle it happened in.
                if (outcome.Routed) soldier.Morale01 = Mathf.Max(0f, soldier.Morale01 - 0.08f);
            }

            if (wounded > 0)
                _log?.Push(
                    CampaignEventKind.Loss,
                    $"{wounded} men wounded",
                    "Found alive among the fallen. They march as baggage until they heal",
                    _state.Clock.Now.DayNumber);

            var fallen = player.Roster.RemoveFallen();
            if (fallen.Count > 0)
            {
                Debug.Log($"[Campaign] {fallen.Count} men lost. {player.Roster.ActiveCount} remain.");

                // Named officers are reported individually. Losing the optio is a different event from
                // losing four legionaries, and the log should say so.
                string officers = DescribeFallenOfficers(fallen);

                _log?.Push(
                    CampaignEventKind.Loss,
                    $"{fallen.Count} men lost",
                    string.IsNullOrEmpty(officers)
                        ? $"{player.Roster.ActiveCount} remain"
                        : officers,
                    _state.Clock.Now.DayNumber);
            }

            ResolvePromotions(player);

            // The offices that stood the field learn from it. After RemoveFallen, so an office
            // whose holder died today has no living holder and earns nothing from the battle
            // that killed him.
            PostTreeCatalog.AwardBattlePoints(
                _state, player.Roster, result.Outcome == BattleOutcome.Victory, wounded, anyRouted);

            LootBundle loot = result.Loot;

            // "Nothing Goes to Waste": the field is stripped to the bone.
            float lootScale = _state.Commander.Has("nothing_goes_to_waste") ? 1.3f : 1f;

            // The Purse: the signifer banks a share of everything the field yields.
            float coinScale = lootScale * (PostTreeCatalog.Invested(_state, "the_purse") ? 1.15f : 1f);

            player.Stores.Food += loot.Food * lootScale;
            player.Stores.Coin += Mathf.RoundToInt(loot.Coin * coinScale);
            player.Stores.Denarii += loot.Denarii;
            player.Stores.EquipmentCondition01 =
                Mathf.Clamp01(player.Stores.EquipmentCondition01 + loot.EquipmentConditionDelta);

            // Itemised spoils were decided by the battle (and shown on the summary); here they
            // simply come home to the inventory.
            for (int i = 0; i < loot.Items.Count; i++)
            {
                LootItem item = loot.Items[i];
                player.Inventory.Add(item.ItemId, item.Count);

                if (item.ItemId == "banner_cherusci")
                {
                    _state.Commander.Note(CommanderPhilosophy.Barbarian, 1f);
                    _log?.Push(
                        CampaignEventKind.Gain,
                        "War banner taken",
                        "Their standard travels with the column now",
                        _state.Clock.Now.DayNumber);
                }
            }

            if (loot.Prisoners > 0)
                _log?.Push(
                    CampaignEventKind.Gain,
                    $"{loot.Prisoners} prisoners taken",
                    "Held under guard with the column",
                    _state.Clock.Now.DayNumber);

            // Surviving a fight steadies a unit; being driven off does the opposite. Doctrine bends
            // both: victory tastes richer to men who take what they kill, and a practised
            // disengagement is a manoeuvre, not a defeat.
            float victoryShift = _state.Commander.Has("take_what_you_kill") ? 0.09f : 0.06f;
            float withdrawalShift = _state.Commander.Has("disengage") ? -0.025f : -0.05f;

            float moraleShift = result.Outcome == BattleOutcome.Victory ? victoryShift
                : result.Outcome == BattleOutcome.Withdrawal ? withdrawalShift
                : -0.12f;

            // Burial Club: the fallen get their rites, and the living carry them lighter.
            if (fallen.Count > 0 && PostTreeCatalog.Invested(_state, "burial_club"))
                moraleShift += 0.04f;

            // The signum's fate outlives the battle. Losing it is a wound to every man in the
            // column; carrying it home after it fell is a story they will tell for years.
            if (result.SignumLost)
            {
                _state.SignumLost = true;
                moraleShift -= 0.12f;
                _log?.Push(
                    CampaignEventKind.Loss,
                    "The signum is lost",
                    "The century's standard did not come home. The men march ashamed",
                    _state.Clock.Now.DayNumber);
            }
            else if (result.SignumFell)
            {
                _log?.Push(
                    CampaignEventKind.Gain,
                    "The signum came home",
                    "It fell in the press, and a man of the century raised it again",
                    _state.Clock.Now.DayNumber);
            }

            // The record of how the commander fights, written after every field.
            NoteBattleStyle(result);

            for (int i = 0; i < player.Roster.Soldiers.Count; i++)
                player.Roster.Soldiers[i].Morale01 =
                    Mathf.Clamp01(player.Roster.Soldiers[i].Morale01 + moraleShift);

            player.Morale.Value01 = player.Roster.AverageMorale01;
            player.Destination = null;

            if (result.Outcome == BattleOutcome.Victory && result.Loot.Food > 0f)
                _log?.Push(
                    CampaignEventKind.Gain,
                    "Field stripped",
                    $"{result.Loot.Food:0} food, {result.Loot.Coin} coin",
                    _state.Clock.Now.DayNumber);
        }

        /// <summary>
        /// Fills any command post left vacant by the fighting. Losing the optio is a real event with a
        /// visible consequence, and this is where the century closes the gap.
        /// </summary>
        private void ResolvePromotions(PartyState player)
        {
            System.Collections.Generic.List<Promotion> promotions = PromotionLadder.Resolve(player.Roster);

            for (int i = 0; i < promotions.Count; i++)
            {
                Promotion promotion = promotions[i];

                _log?.Push(
                    CampaignEventKind.Gain,
                    $"{promotion.DisplayName} promoted",
                    $"{promotion.FromRankId} to {promotion.ToRankId}",
                    _state.Clock.Now.DayNumber);

                Debug.Log($"[Campaign] {promotion.DisplayName}: {promotion.FromRankId} → {promotion.ToRankId}");
            }
        }

        /// <summary>How a battle was fought marks what the commander is becoming.</summary>
        private void NoteBattleStyle(BattleResult result)
        {
            CommanderProgress commander = _state.Commander;

            if (result.Outcome == BattleOutcome.Withdrawal)
            {
                commander.Note(CommanderPhilosophy.Survivor, 1f);
                return;
            }

            if (result.Outcome != BattleOutcome.Victory) return;

            // A victory with no squad broken is Roman work; a victory won at the point of the
            // commander's own blade is something older.
            string centurionId = _state.PlayerParty?.Roster.FindByRank("centurion")?.Id;
            bool anyRouted = false;
            int commanderKills = 0;

            for (int i = 0; i < result.Combatants.Count; i++)
            {
                CombatantOutcome outcome = result.Combatants[i];
                if (outcome.Routed) anyRouted = true;
                if (centurionId != null && outcome.SoldierId == centurionId) commanderKills = outcome.Kills;
            }

            if (!anyRouted) commander.Note(CommanderPhilosophy.TrueRoman, 1f);
            if (commanderKills >= 3) commander.Note(CommanderPhilosophy.Barbarian, 1f + commanderKills * 0.15f);
        }

        private static string DescribeFallenOfficers(System.Collections.Generic.List<SoldierRecord> fallen)
        {
            var names = new System.Collections.Generic.List<string>();

            for (int i = 0; i < fallen.Count; i++)
                if (fallen[i].RankId != "legionary")
                    names.Add($"{fallen[i].DisplayName} ({fallen[i].RankId})");

            return names.Count == 0 ? string.Empty : string.Join(", ", names);
        }

        private void ApplyToEnemy(PartyState enemy, BattleResult result)
        {
            if (result.Outcome == BattleOutcome.Victory)
            {
                enemy.IsDisbanded = true;
                enemy.Destination = null;
                Debug.Log($"[Campaign] {enemy.DisplayName} destroyed.");

                _log?.Push(
                    CampaignEventKind.Gain,
                    $"{enemy.DisplayName} destroyed",
                    $"{result.EnemiesKilled} enemy dead",
                    _state.Clock.Now.DayNumber);
                return;
            }

            // Survived the player: bloodied, and it stops chasing for a while.
            int losses = Mathf.Min(result.EnemiesKilled, enemy.Roster.Soldiers.Count);
            for (int i = 0; i < losses; i++) enemy.Roster.Soldiers[i].Health01 = 0f;
            enemy.Roster.RemoveFallen();

            // Prisoners are gone from the warband as surely as the dead.
            int captured = Mathf.Min(result.Loot.Prisoners, enemy.Roster.Soldiers.Count);
            for (int i = 0; i < captured; i++) enemy.Roster.Soldiers[i].Health01 = 0f;
            enemy.Roster.RemoveFallen();

            enemy.AiState = PartyAiState.Idle;
            enemy.PursuitTargetId = null;
            enemy.Destination = null;
            enemy.Morale.Value01 = enemy.Roster.AverageMorale01;

            // A warband too weak to fight is finished as a force, whatever the nominal outcome was.
            // Without this a two-man remnant keeps existing on the overmap and can re-trigger combat.
            if (enemy.Roster.CombatReadyCount <= 2)
            {
                enemy.IsDisbanded = true;

                _log?.Push(
                    CampaignEventKind.Gain,
                    $"{enemy.DisplayName} scattered",
                    "What remains is no longer a warband",
                    _state.Clock.Now.DayNumber);
            }
        }

        private void ApplyCooldowns(PartyState player, PartyState enemy)
        {
            var until = _state.Clock.Now.Plus(_settings.PostBattleCooldownMinutes);
            if (player != null) player.ContactCooldownUntil = until;
            if (enemy != null && !enemy.IsDisbanded)
            {
                enemy.ContactCooldownUntil = until;
                enemy.NextDecisionTime = until;
            }
        }
    }
}
