using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Spawns a hostile warband next to the player so a POI "fight" choice turns into a real battle.
    /// It adds nothing new to the battle pipeline — the ordinary <see cref="EncounterDetector"/> sees
    /// the fresh party on its next tick and loads the fight, exactly as a wandering warband would.
    /// </summary>
    public static class PoiFightFactory
    {
        public static PartyState SpawnRaiders(
            CampaignState state, Vector3 near, int strength,
            string archetypeId = null, string displayName = null)
        {
            strength = Mathf.Max(4, strength);

            // Placed just inside contact range so the fight opens almost immediately, but not exactly
            // on top of the column, which reads as an ambush rather than a spawn.
            Vector2 offset = Random.insideUnitCircle.normalized * 9f;
            Vector3 spawn = near + new Vector3(offset.x, 0f, offset.y);

            var party = new PartyState
            {
                Id = state.MintId("party"),
                DisplayName = displayName ?? "Cherusci Raiders",
                Faction = PartyFaction.Germanic,
                WorldPosition = spawn,
                DetectionRadius = 90f,
                SpeedModifier = 1f,
                Behaviour = CommanderBehaviourPool.Pick(),
                AiState = PartyAiState.Pursuing,
                PursuitTargetId = state.PlayerParty != null ? state.PlayerParty.Id : null,
                PursuitExpiryTime = state.Clock.Now.Plus(300d),
                Stores = { Food = 60f }
            };

            party.Roster.Capacity = strength;
            for (int i = 0; i < strength; i++)
            {
                party.Roster.Add(new SoldierRecord
                {
                    Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                    DisplayName = $"Warrior {i + 1}",
                    RankId = "warrior",
                    ArchetypeId = archetypeId ?? "cherusci_warrior",
                    Health01 = Random.Range(0.6f, 1f),
                    Stamina01 = Random.Range(0.6f, 1f),
                    Morale01 = Random.Range(0.6f, 0.9f)
                });
            }

            party.Morale.Value01 = party.Roster.AverageMorale01;
            state.AddParty(party);
            return party;
        }
    }
}
