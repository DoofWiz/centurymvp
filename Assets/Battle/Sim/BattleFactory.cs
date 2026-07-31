using System.Collections.Generic;
using Century.Battle.Model;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Turns a <see cref="BattleRequest"/> into a deployed <see cref="BattleState"/>: squads formed,
    /// officers distributed, both sides placed on the field.
    /// </summary>
    /// <remarks>
    /// Nobody on the player's side is held back by force: the whole century marches on, and which
    /// contubernia wait as reinforcements is the player's call on the deployment screen. The enemy
    /// commander makes the same call by doctrine here, unseen.
    /// </remarks>
    public static class BattleFactory
    {
        public static BattleState Create(BattleRequest request, BattleSettings settings)
        {
            var state = new BattleState
            {
                PlayerPartyId = request.PlayerPartyId,
                EnemyPartyId = request.EnemyPartyId,
                EnemyDisplayName = request.EnemyDisplayName,
                TerrainId = request.TerrainId,
                TimeOfDay = request.TimeOfDay,
                PlayerAmbushed = request.PlayerAmbushed,
                EnemyAmbushed = request.EnemyAmbushed,
                CommanderSkills = new HashSet<string>(request.CommanderSkills),
                CapturedBanners = request.CapturedBanners,
                RandomSeed = request.RandomSeed,
                EnemyBehaviour = request.EnemyBehaviour
            };

            List<BattleCombatant> playerMen = Convert(request.PlayerCombatants, settings, isPlayerSide: true);
            List<BattleCombatant> enemyMen = Convert(request.EnemyCombatants, settings, isPlayerSide: false);

            state.PlayerCharacter = ExtractPlayerCharacter(playerMen);

            BuildSquads(state.PlayerSquads, playerMen, settings, isPlayerSide: true, namePrefix: "Contubernium");
            BuildSquads(state.EnemySquads, enemyMen, settings, isPlayerSide: false, namePrefix: "Warband");

            // A squad arrives with the heart its men brought from the campaign: a Fearless column
            // starts with a full cohesion bar, a Broken one is a push from shattering. Squads used
            // to open at a flat 0.6 no matter what the roster said.
            SeedCohesion(state.PlayerSquads);
            SeedCohesion(state.EnemySquads);

            state.Effects = request.PostEffects ?? new Century.Core.Contracts.PostEffectSet();

            // A century that lost its signum fields no standard at all until it is won back:
            // Lost from the first tick means no carrier, no aura, no floor — the absence bites.
            if (request.SignumAlreadyLost)
            {
                state.Signum = SignumStatus.Lost;
                state.SignumLostBeforeBattle = true;
            }

            Deploy(state, settings);
            HoldBackEnemyReserve(state, settings);
            ApplyCommanderDoctrines(state);
            return state;
        }

        /// <summary>Doctrines that shape the opening of a battle, applied once at build.</summary>
        private static void ApplyCommanderDoctrines(BattleState state)
        {
            // War Cry: blood up in a fight the commander CHOSE — never in one sprung on them.
            float playerBonus = 0f;
            if (state.HasSkill("war_cry") && !state.PlayerAmbushed) playerBonus += 0.08f;

            // The Old Ways: captured banners in the baggage embolden the line.
            if (state.HasSkill("the_old_ways")) playerBonus += 0.05f * state.CapturedBanners;

            if (playerBonus > 0f)
                for (int i = 0; i < state.PlayerSquads.Count; i++)
                    state.PlayerSquads[i].Cohesion01 = Mathf.Clamp01(state.PlayerSquads[i].Cohesion01 + playerBonus);

            // Never Fight Fair: a sprung trap opens with the enemy already shaken.
            if (state.HasSkill("never_fight_fair") && state.EnemyAmbushed)
                for (int i = 0; i < state.EnemySquads.Count; i++)
                    state.EnemySquads[i].Cohesion01 = Mathf.Clamp01(state.EnemySquads[i].Cohesion01 - 0.15f);
        }

        /// <summary>
        /// The enemy commander holds a share of his warbands off the field by doctrine: a fanatic
        /// commits everything; a wary man keeps half in hand. Held squads park at his own edge of
        /// the field, invisible until his moment comes.
        /// </summary>
        private static void HoldBackEnemyReserve(BattleState state, BattleSettings settings)
        {
            float fraction;
            switch (state.EnemyBehaviour)
            {
                case CommanderBehaviour.Fanatic: fraction = 0f; break;
                case CommanderBehaviour.Skirmisher: fraction = 0.25f; break;
                case CommanderBehaviour.Wary: fraction = 0.5f; break;
                default: fraction = 0.34f; break;   // Disciplined
            }

            int holdCount = Mathf.FloorToInt(state.EnemySquads.Count * fraction);
            if (holdCount <= 0) return;

            // The rearmost squads are held; the line that deployed in front stays in front.
            for (int i = 0; i < holdCount; i++)
            {
                BattleSquad squad = state.EnemySquads[state.EnemySquads.Count - 1 - i];
                squad.IsOffField = true;
                squad.Order = SquadOrder.HoldPosition;

                Vector3 park = new Vector3(
                    (i - (holdCount - 1) * 0.5f) * settings.SquadFrontage,
                    0f,
                    settings.FieldHalfExtent);

                squad.AnchorPosition = park;
                squad.AnchorFacing = Vector3.back;
                squad.OrderedPosition = park;

                for (int m = 0; m < squad.Members.Count; m++)
                    squad.Members[m].WorldPosition = SquadFormationSolver.GetWorldSlot(squad, m, settings);
            }
        }

        private static List<BattleCombatant> Convert(
            List<CombatantSpec> specs, BattleSettings settings, bool isPlayerSide)
        {
            var men = new List<BattleCombatant>(specs.Count);
            for (int i = 0; i < specs.Count; i++)
            {
                CombatantSpec spec = specs[i];
                bool isLegionary = IsLegionary(spec.ArchetypeId);

                // How much wind he brings from the campaign; caps his bar and how fast it refills.
                StaminaClass staminaClass = StaminaProfile.FromStamina(spec.Stamina01);
                float staminaCap = StaminaProfile.For(staminaClass).MaxStamina;

                men.Add(new BattleCombatant
                {
                    SoldierId = spec.SoldierId,
                    DisplayName = spec.DisplayName,
                    VeterancyLabel = spec.VeterancyLabel,
                    ArchetypeId = spec.ArchetypeId,
                    Role = OfficerRoleParser.FromRankId(spec.RankId),
                    Health01 = spec.Health01,
                    Stamina01 = Mathf.Min(spec.Stamina01, staminaCap),
                    Morale01 = spec.Morale01,
                    IsPlayerControlled = spec.IsPlayerControlled,
                    IsPlayerSide = isPlayerSide,
                    Weapon = isLegionary ? WeaponClass.Sword : WeaponClass.Spear,
                    HasShield = true,
                    Stamina = staminaClass,
                    PilaRemaining = settings.PilaCount,
                    GroupIndex = spec.GroupIndex,
                    IsGroupLeader = spec.IsGroupLeader,
                    DamageMultiplier = spec.DamageMultiplier
                });
            }

            return men;
        }

        /// <summary>Both sides carry a shield now; the weapon marks the side — Romans the gladius behind a
        /// scutum, Germanic fighters the spear behind a lighter, wider board.</summary>
        private static bool IsLegionary(string archetypeId) =>
            string.IsNullOrEmpty(archetypeId) || archetypeId.StartsWith("legionary");

        /// <summary>The Centurion fights as himself, outside the squad structure.</summary>
        private static BattleCombatant ExtractPlayerCharacter(List<BattleCombatant> men)
        {
            for (int i = 0; i < men.Count; i++)
            {
                if (!men[i].IsPlayerControlled && men[i].Role != OfficerRole.Centurion) continue;

                BattleCombatant commander = men[i];
                commander.IsPlayerControlled = true;
                commander.Stamina = StaminaClass.Fresh;   // the Centurion is never gated by fatigue class
                men.RemoveAt(i);
                return commander;
            }

            // No centurion survived to lead: promote the first man rather than leaving the player bodiless.
            if (men.Count == 0) return null;

            BattleCombatant substitute = men[0];
            substitute.IsPlayerControlled = true;
            men.RemoveAt(0);
            return substitute;
        }

        private static bool HasGroups(List<BattleCombatant> men)
        {
            for (int i = 0; i < men.Count; i++)
                if (men[i].GroupIndex >= 0) return true;
            return false;
        }

        /// <summary>
        /// Forms squads. Men who carry a persistent contubernium fight in it — the exact tent groups
        /// the camp screen shows, Decanus at the front-centre slot. Men without one (warbands) are
        /// dealt into squads of <see cref="BattleSettings.SquadSize"/> with officers spread across
        /// them, one apiece, so that losing one squad does not decapitate the whole force.
        /// </summary>
        private static void BuildSquads(
            List<BattleSquad> squads, List<BattleCombatant> men, BattleSettings settings,
            bool isPlayerSide, string namePrefix)
        {
            if (men.Count == 0) return;

            if (HasGroups(men))
            {
                BuildSquadsFromGroups(squads, men, isPlayerSide, namePrefix);
                return;
            }

            int squadCount = Mathf.CeilToInt(men.Count / (float)settings.SquadSize);

            for (int i = 0; i < squadCount; i++)
            {
                squads.Add(new BattleSquad
                {
                    Index = i,
                    DisplayName = $"{namePrefix} {ToRoman(i + 1)}",
                    IsPlayerSide = isPlayerSide,
                    Formation = isPlayerSide ? FormationType.Line : FormationType.Loose,
                    Order = isPlayerSide ? SquadOrder.FollowMe : SquadOrder.Advance
                });
            }

            var officers = new List<BattleCombatant>();
            var rankers = new List<BattleCombatant>();

            for (int i = 0; i < men.Count; i++)
            {
                if (men[i].IsOfficer) officers.Add(men[i]);
                else rankers.Add(men[i]);
            }

            for (int i = 0; i < officers.Count; i++) Assign(squads[i % squadCount], officers[i]);

            int cursor = 0;
            for (int i = 0; i < rankers.Count; i++)
            {
                // Skip squads that are already full rather than overloading the first one.
                int guard = 0;
                while (squads[cursor % squadCount].Members.Count >= settings.SquadSize && guard++ < squadCount)
                    cursor++;

                Assign(squads[cursor % squadCount], rankers[i]);
                cursor++;
            }

            for (int i = squads.Count - 1; i >= 0; i--)
                if (squads[i].Members.Count == 0) squads.RemoveAt(i);

            for (int i = 0; i < squads.Count; i++)
            {
                squads[i].Index = i;
                SortForBattleOrder(squads[i]);
            }
        }

        /// <summary>One battle squad per contubernium, named for it, exactly as the camp shows.</summary>
        private static void BuildSquadsFromGroups(
            List<BattleSquad> squads, List<BattleCombatant> men, bool isPlayerSide, string namePrefix)
        {
            var indices = new List<int>();
            for (int i = 0; i < men.Count; i++)
                if (men[i].GroupIndex >= 0 && !indices.Contains(men[i].GroupIndex)) indices.Add(men[i].GroupIndex);
            indices.Sort();

            for (int g = 0; g < indices.Count; g++)
            {
                var squad = new BattleSquad
                {
                    Index = squads.Count,
                    DisplayName = $"{namePrefix} {ToRoman(indices[g] + 1)}",
                    IsPlayerSide = isPlayerSide,
                    Formation = isPlayerSide ? FormationType.Line : FormationType.Loose,
                    Order = isPlayerSide ? SquadOrder.FollowMe : SquadOrder.Advance
                };
                squads.Add(squad);

                for (int i = 0; i < men.Count; i++)
                    if (men[i].GroupIndex == indices[g]) Assign(squad, men[i]);
            }

            // Anyone without a group (should not happen for the player side, but stay safe) falls
            // into one final squad rather than vanishing from the fight.
            BattleSquad strays = null;
            for (int i = 0; i < men.Count; i++)
            {
                if (men[i].GroupIndex >= 0) continue;
                if (strays == null)
                {
                    strays = new BattleSquad
                    {
                        Index = squads.Count,
                        DisplayName = $"{namePrefix} {ToRoman(squads.Count + 1)}",
                        IsPlayerSide = isPlayerSide,
                        Formation = isPlayerSide ? FormationType.Line : FormationType.Loose,
                        Order = isPlayerSide ? SquadOrder.FollowMe : SquadOrder.Advance
                    };
                    squads.Add(strays);
                }

                Assign(strays, men[i]);
            }

            for (int i = 0; i < squads.Count; i++) SortForBattleOrder(squads[i]);
        }

        private static void Assign(BattleSquad squad, BattleCombatant man)
        {
            man.SquadIndex = squad.Index;
            man.SlotIndex = squad.Members.Count;
            squad.Members.Add(man);
        }

        /// <summary>Squad cohesion opens at the men's average personal morale (floored so no squad
        /// starts already broken — a shaken century still forms a line before it cracks).</summary>
        private static void SeedCohesion(List<BattleSquad> squads)
        {
            for (int i = 0; i < squads.Count; i++)
                squads[i].Cohesion01 = Mathf.Clamp(squads[i].AverageMorale01, 0.35f, 1f);
        }

        /// <summary>
        /// The front rank takes the first blow, so the steadiest men stand in it — and the Decanus,
        /// where one is appointed, takes slot 0 at the front-centre and leads his tent group. The
        /// optio belongs at the back, where his job is to stop the line dissolving.
        /// </summary>
        private static void SortForBattleOrder(BattleSquad squad)
        {
            squad.Members.Sort((a, b) =>
            {
                if (a.IsGroupLeader != b.IsGroupLeader)
                    return a.IsGroupLeader ? -1 : 1;

                if (a.Role == OfficerRole.Optio != (b.Role == OfficerRole.Optio))
                    return a.Role == OfficerRole.Optio ? 1 : -1;

                float aScore = a.Health01 * 0.6f + a.Morale01 * 0.4f;
                float bScore = b.Health01 * 0.6f + b.Morale01 * 0.4f;
                return bScore.CompareTo(aScore);
            });

            for (int i = 0; i < squad.Members.Count; i++) squad.Members[i].SlotIndex = i;
        }

        private static void Deploy(BattleState state, BattleSettings settings)
        {
            float separation = state.PlayerAmbushed ? settings.AmbushSeparation : settings.DeploymentSeparation;

            PlaceLine(state.PlayerSquads, -separation * 0.5f, Vector3.forward, settings);
            PlaceLine(state.EnemySquads, separation * 0.5f, Vector3.back, settings);

            if (state.PlayerCharacter != null)
                state.PlayerCharacter.WorldPosition = new Vector3(0f, 0f, -separation * 0.5f - settings.FollowDistance);
        }

        private static void PlaceLine(
            List<BattleSquad> squads, float z, Vector3 facing, BattleSettings settings)
        {
            float totalWidth = (squads.Count - 1) * settings.SquadFrontage;

            for (int i = 0; i < squads.Count; i++)
            {
                float x = i * settings.SquadFrontage - totalWidth * 0.5f;
                squads[i].AnchorPosition = new Vector3(x, 0f, z);
                squads[i].AnchorFacing = facing;
                squads[i].OrderedPosition = squads[i].AnchorPosition;

                for (int m = 0; m < squads[i].Members.Count; m++)
                    squads[i].Members[m].WorldPosition =
                        SquadFormationSolver.GetWorldSlot(squads[i], m, settings);
            }
        }

        private static string ToRoman(int value)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return value >= 1 && value <= numerals.Length ? numerals[value - 1] : value.ToString();
        }
    }
}
