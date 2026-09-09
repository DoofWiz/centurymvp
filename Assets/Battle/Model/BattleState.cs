using System.Collections.Generic;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Battle.Model
{
    /// <summary>Which stage of the battle we are in. Drives which UI is up and whether combat ticks.</summary>
    public enum BattlePhase
    {
        /// <summary>Choosing who deploys and where. Nothing fights.</summary>
        Deployment = 0,
        /// <summary>Announcement banner. Still nothing fights.</summary>
        Opening = 1,
        Fighting = 2,
        /// <summary>Outcome decided, summary on screen.</summary>
        Aftermath = 3
    }

    /// <summary>Where the century's standard is (army phase 3: the signum is a world object).</summary>
    public enum SignumStatus
    {
        /// <summary>No signifer took the field; there is no signum in this battle.</summary>
        Absent = 0,
        /// <summary>In the hands of a man of the century (the signifer, or whoever raised it).</summary>
        Carried = 1,
        /// <summary>On the ground where its bearer fell. Anyone can reach it first.</summary>
        Fallen = 2,
        /// <summary>An enemy has it. Kill him and it falls again.</summary>
        EnemyHeld = 3,
        /// <summary>Carried off the field. It is not coming home today.</summary>
        Lost = 4
    }

    /// <summary>
    /// Root of everything in a single battle. Built from a BattleRequest on scene load and flattened
    /// into a BattleResult when the fight ends.
    /// </summary>
    public sealed class BattleState
    {
        public string PlayerPartyId;
        public string EnemyPartyId;
        public string EnemyDisplayName;
        public string TerrainId;
        public CampaignTime TimeOfDay;
        public bool PlayerAmbushed;

        /// <summary>The player sprang the trap: deployment gets a free hand (wider, closer zone).</summary>
        public bool EnemyAmbushed;

        /// <summary>Commander doctrines in effect this battle, by catalog id.</summary>
        public HashSet<string> CommanderSkills = new HashSet<string>();

        /// <summary>Captured war banners with the column, for the doctrines that care.</summary>
        public int CapturedBanners;

        public bool HasSkill(string id) => CommanderSkills.Contains(id);
        public int RandomSeed;

        /// <summary>The enemy commander's doctrine. Drives <see cref="Sim.EnemySquadAi"/>.</summary>
        public CommanderBehaviour EnemyBehaviour = CommanderBehaviour.Disciplined;

        public List<BattleSquad> PlayerSquads = new List<BattleSquad>();
        public List<BattleSquad> EnemySquads = new List<BattleSquad>();

        /// <summary>Men held out of the fight. They return to the roster untouched.</summary>
        public List<BattleCombatant> Reserve = new List<BattleCombatant>();

        /// <summary>The Centurion. Directly controlled, and not a member of any squad.</summary>
        public BattleCombatant PlayerCharacter;

        /// <summary>Seconds since the battle began. Drives order propagation, not campaign time.</summary>
        public float ElapsedSeconds;

        // --- The rally burst ---------------------------------------------------------------------

        /// <summary>Seconds left of the active rally burst. Zero when no burst is running.</summary>
        public float RallySecondsLeft;

        /// <summary>Seconds until the rally can be called again. Starts when a burst ends.</summary>
        public float RallyCooldownLeft;

        /// <summary>True while the Centurion's rally burst is running: the whole side takes less
        /// damage, moves faster and recovers wind. Ticked by the simulation.</summary>
        public bool RallyActive => RallySecondsLeft > 0f;

        /// <summary>The Centurion is down and the Optio holds command: a degraded-control window
        /// with a closing clock, instead of an instant defeat (army brief, Centurio in Waiting).</summary>
        /// <summary>Resolved office effects from the campaign. Never null.</summary>
        public Century.Core.Contracts.PostEffectSet Effects = new Century.Core.Contracts.PostEffectSet();

        /// <summary>Deaths already negated by the medicus this battle.</summary>
        public int DeathsNegated;

        public bool CommandDevolved;
        public float SuccessionSecondsLeft;

        // --- The signum -------------------------------------------------------------------------

        public SignumStatus Signum = SignumStatus.Absent;

        /// <summary>Whoever holds it right now — a man of the century, or an enemy. Null while
        /// fallen, absent or lost.</summary>
        public BattleCombatant SignumCarrier;

        /// <summary>Authoritative world position: the carrier's while held, the fall point while down.</summary>
        public Vector3 SignumPosition;

        /// <summary>Bearer standing steady out of the press: the signum is planted and reaches farther.</summary>
        public bool SignumPlanted;

        /// <summary>It touched the ground at least once this battle — the campaign hears of it.</summary>
        public bool SignumEverFell;

        /// <summary>It was already gone before this battle began; its absence is old news.</summary>
        public bool SignumLostBeforeBattle;

        public BattlePhase Phase = BattlePhase.Deployment;

        // --- Deployment ------------------------------------------------------------------------

        /// <summary>Centre of the zone the vanguard may deploy into.</summary>
        public Vector3 DeploymentZoneCentre;

        /// <summary>Half-extents of the deployment zone on the ground plane.</summary>
        public Vector2 DeploymentZoneExtents = new Vector2(26f, 10f);

        /// <summary>Where the player has chosen to form his vanguard.</summary>
        public Vector3 VanguardAnchor;

        /// <summary>Map-edge point reinforcements will march in from.</summary>
        public Vector3 ReinforcementPoint;

        public bool HasChosenVanguard;
        public bool HasChosenReinforcementPoint;

        /// <summary>Squad indices the player has held back. Populated during deployment.</summary>
        public HashSet<int> ReservedSquadIndices = new HashSet<int>();

        public bool IsPointInDeploymentZone(Vector3 point)
        {
            Vector3 local = point - DeploymentZoneCentre;
            return Mathf.Abs(local.x) <= DeploymentZoneExtents.x
                   && Mathf.Abs(local.z) <= DeploymentZoneExtents.y;
        }

        public IEnumerable<BattleCombatant> AllPlayerSideCombatants()
        {
            if (PlayerCharacter != null) yield return PlayerCharacter;

            for (int s = 0; s < PlayerSquads.Count; s++)
            {
                List<BattleCombatant> members = PlayerSquads[s].Members;
                for (int i = 0; i < members.Count; i++) yield return members[i];
            }

            for (int i = 0; i < Reserve.Count; i++) yield return Reserve[i];
        }

        public int PlayerSideAliveCount
        {
            get
            {
                int count = 0;
                if (PlayerCharacter != null && PlayerCharacter.IsAlive) count++;
                for (int s = 0; s < PlayerSquads.Count; s++) count += PlayerSquads[s].AliveCount;
                return count;
            }
        }

        public int EnemyAliveCount
        {
            get
            {
                int count = 0;
                for (int s = 0; s < EnemySquads.Count; s++) count += EnemySquads[s].AliveCount;
                return count;
            }
        }
    }
}
