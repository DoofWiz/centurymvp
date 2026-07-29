using System.Collections.Generic;
using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.Sim
{
    /// <summary>
    /// Repositions the player's force during the deployment phase, and moves reserved squads out to
    /// the reinforcement point so they march on rather than starting in the line.
    /// </summary>
    /// <remarks>
    /// Kept out of the view layer because deployment is a change to battle state, not a change to what
    /// is drawn: the views follow the squad anchors wherever they are put. That also means the same
    /// code can lay out an AI-versus-AI fight nobody is watching.
    /// </remarks>
    public static class BattleDeployment
    {
        /// <summary>
        /// Establishes the deployment zone on the player's side of the field, and a default vanguard
        /// position at its centre so the player can confirm without placing anything.
        /// </summary>
        public static void PrepareZone(BattleState state, BattleSettings settings, float fieldHalfDepth)
        {
            float separation = state.PlayerAmbushed
                ? settings.AmbushSeparation
                : settings.DeploymentSeparation;

            // The zone sits behind where the line would naturally form, so the player is choosing
            // ground rather than choosing how close to stand.
            float zoneZ = -separation * 0.5f - 4f;

            state.DeploymentZoneCentre = new Vector3(0f, 0f, zoneZ);
            state.DeploymentZoneExtents = new Vector2(
                Mathf.Max(20f, settings.SquadFrontage * 3.5f),
                10f);

            // Springing the trap breaks the rules of a fair meeting: the ambusher deploys with a
            // free hand — a far wider zone, pushed up close to prey that never saw them coming.
            if (state.EnemyAmbushed)
            {
                state.DeploymentZoneCentre = new Vector3(0f, 0f, -separation * 0.18f);
                state.DeploymentZoneExtents = new Vector2(
                    Mathf.Max(45f, settings.SquadFrontage * 6f),
                    separation * 0.4f);
                state.VanguardAnchor = state.DeploymentZoneCentre;
            }

            state.VanguardAnchor = state.DeploymentZoneCentre;
            state.ReinforcementPoint = new Vector3(0f, 0f, -Mathf.Abs(fieldHalfDepth));

            state.HasChosenVanguard = false;
            state.HasChosenReinforcementPoint = false;
        }

        /// <summary>
        /// Lays the deployed squads out in line abreast around the vanguard anchor. Held-back squads
        /// become reinforcements: parked OFF the field at the marked entry point, invisible and out
        /// of the fight until the Centurion summons them.
        /// </summary>
        public static void ApplyDeployment(BattleState state, BattleSettings settings)
        {
            var deployed = new List<BattleSquad>();
            var reserved = new List<BattleSquad>();

            for (int i = 0; i < state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = state.PlayerSquads[i];
                if (state.ReservedSquadIndices.Contains(squad.Index)) reserved.Add(squad);
                else deployed.Add(squad);
            }

            // Everything held back would mean no battle. Commit the first squad regardless so the
            // player always has something on the field.
            if (deployed.Count == 0 && reserved.Count > 0)
            {
                deployed.Add(reserved[0]);
                state.ReservedSquadIndices.Remove(reserved[0].Index);
                reserved.RemoveAt(0);
            }

            PlaceAbreast(deployed, state.VanguardAnchor, Vector3.forward, settings, SquadOrder.HoldPosition);
            PlaceAbreast(reserved, state.ReinforcementPoint, Vector3.forward, settings, SquadOrder.HoldPosition);

            for (int i = 0; i < deployed.Count; i++) deployed[i].IsOffField = false;
            for (int i = 0; i < reserved.Count; i++) reserved[i].IsOffField = true;

            if (state.PlayerCharacter != null)
                state.PlayerCharacter.WorldPosition =
                    state.VanguardAnchor - Vector3.forward * settings.FollowDistance;
        }

        /// <summary>
        /// Brings a side's reinforcements onto the field at their entry point, under orders to join
        /// the fight. Idempotent; returns how many squads marched on.
        /// </summary>
        public static int Summon(List<BattleSquad> squads, SquadOrder order)
        {
            int summoned = 0;

            for (int i = 0; i < squads.Count; i++)
            {
                BattleSquad squad = squads[i];
                if (!squad.IsOffField || squad.IsDestroyed) continue;

                squad.IsOffField = false;
                squad.Order = order;
                squad.Formation = FormationType.Line;
                squad.OrderedPosition = squad.AnchorPosition;
                summoned++;
            }

            return summoned;
        }

        private static void PlaceAbreast(
            List<BattleSquad> squads, Vector3 centre, Vector3 facing,
            BattleSettings settings, SquadOrder order)
        {
            if (squads.Count == 0) return;

            float totalWidth = (squads.Count - 1) * settings.SquadFrontage;

            for (int i = 0; i < squads.Count; i++)
            {
                BattleSquad squad = squads[i];

                Vector3 offset = Vector3.right * (i * settings.SquadFrontage - totalWidth * 0.5f);
                squad.AnchorPosition = centre + offset;
                squad.AnchorFacing = facing;
                squad.OrderedPosition = squad.AnchorPosition;
                squad.Order = order;

                for (int m = 0; m < squad.Members.Count; m++)
                    squad.Members[m].WorldPosition =
                        SquadFormationSolver.GetWorldSlot(squad, m, settings);
            }
        }

        /// <summary>Men currently marked for deployment, for the readout on the deployment screen.</summary>
        public static int CountDeployed(BattleState state)
        {
            int count = 0;

            for (int i = 0; i < state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = state.PlayerSquads[i];
                if (state.ReservedSquadIndices.Contains(squad.Index)) continue;
                count += squad.AliveCount;
            }

            return count;
        }

        public static int CountReserved(BattleState state)
        {
            int count = state.Reserve.Count;

            for (int i = 0; i < state.PlayerSquads.Count; i++)
            {
                BattleSquad squad = state.PlayerSquads[i];
                if (state.ReservedSquadIndices.Contains(squad.Index)) count += squad.AliveCount;
            }

            return count;
        }

        /// <summary>Clamps a chosen point to the map edge nearest the player's side.</summary>
        public static Vector3 SnapToFieldEdge(Vector3 point, float halfWidth, float halfDepth)
        {
            float distanceToBack = Mathf.Abs(point.z + halfDepth);
            float distanceToLeft = Mathf.Abs(point.x + halfWidth);
            float distanceToRight = Mathf.Abs(point.x - halfWidth);

            if (distanceToBack <= distanceToLeft && distanceToBack <= distanceToRight)
                return new Vector3(Mathf.Clamp(point.x, -halfWidth, halfWidth), 0f, -halfDepth);

            return distanceToLeft <= distanceToRight
                ? new Vector3(-halfWidth, 0f, Mathf.Clamp(point.z, -halfDepth, 0f))
                : new Vector3(halfWidth, 0f, Mathf.Clamp(point.z, -halfDepth, 0f));
        }
    }
}
