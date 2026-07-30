using System.Collections.Generic;
using UnityEngine;

namespace Century.Battle.Model
{
    /// <summary>
    /// A contubernium: up to ten men who move, hold formation and receive orders as one.
    /// This is the tactical atom of a battle — nothing below squad level takes an order.
    /// </summary>
    public sealed class BattleSquad
    {
        public int Index;
        public string DisplayName;
        public bool IsPlayerSide;

        public List<BattleCombatant> Members = new List<BattleCombatant>();

        public SquadOrder Order = SquadOrder.FollowMe;
        public FormationType Formation = FormationType.Line;

        /// <summary>Point the formation is built around, and the direction it faces.</summary>
        public Vector3 AnchorPosition;
        public Vector3 AnchorFacing = Vector3.forward;

        /// <summary>Target for an Advance order, in world space.</summary>
        public Vector3 OrderedPosition;

        /// <summary>Assigned flank offset beside the Centurion while following. Set by the follow-formation
        /// assignment so a squad takes the nearest place beside him rather than a fixed numbered one.</summary>
        public float FollowLateral;

        // --- Cohesion --------------------------------------------------------------------------

        /// <summary>Squad morale, 0..1. Distinct from the average of its members' personal morale.</summary>
        public float Cohesion01 = 0.6f;

        /// <summary>Fled the line. Will not fight or accept orders until rallied.</summary>
        public bool IsRouted;

        /// <summary>Retreated clear of the field under orders. Out of the fight, and not coming back.</summary>
        public bool IsWithdrawn;

        /// <summary>True once the squad has loosed its opening volley of pila at a closing enemy.</summary>
        public bool HasLoosedVolley;

        /// <summary>
        /// Held back as reinforcements: parked at its side's entry point, invisible and untouchable,
        /// taking no part in the fight until summoned. Both sides use this — the player by choice on
        /// the deployment screen, the enemy commander by doctrine.
        /// </summary>
        public bool IsOffField;

        /// <summary>True for a squad that marched on as summoned reinforcements. The enemy AI sends
        /// these at a flank rather than piling them into the back of its own line.</summary>
        public bool ArrivedAsReserve;

        /// <summary>Where a routed squad is running to.</summary>
        public Vector3 RoutDestination;

        /// <summary>Decaying count of men lost recently. Fresh casualties frighten more than old ones.</summary>
        public float RecentCasualtyPressure;

        /// <summary>Fraction of living men standing in their assigned slot. A broken line loses heart.</summary>
        public float Dressed01 = 1f;

        /// <summary>True while the Centurion is close enough to steady this squad.</summary>
        public bool UnderCommandAura;

        /// <summary>True while an effective enemy squad is within shields-up range. Computed once
        /// per melee tick (see MeleeCombat) so a hundred idle men don't each rescan the field.</summary>
        public bool HostileNearby;

        /// <summary>Accumulated rally effort from the Centurion, 0..1.</summary>
        public float RallyProgress01;

        public CohesionBand Band => CohesionBandExtensions.FromValue(Cohesion01, IsRouted);

        // --- Order propagation ---------------------------------------------------------------

        /// <summary>
        /// Orders are not instantaneous. A shouted command takes time to reach a squad and be
        /// understood, which is what makes standing near your men matter and what the tesserarius
        /// exists to shorten.
        /// </summary>
        public SquadOrder? PendingOrder;
        public FormationType? PendingFormation;
        public Vector3 PendingOrderedPosition;
        public float PendingReadyAt;

        public bool HasPendingOrder => PendingOrder.HasValue || PendingFormation.HasValue;

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Members.Count; i++)
                    if (Members[i].IsAlive) count++;
                return count;
            }
        }

        public bool IsDestroyed => AliveCount <= 0;

        /// <summary>A routed or withdrawn squad is out of the fight without being dead.</summary>
        public bool IsEffective => !IsDestroyed && !IsRouted && !IsWithdrawn;

        public int EngagedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Members.Count; i++)
                    if (Members[i].IsAlive && Members[i].IsInCombat) count++;
                return count;
            }
        }

        public float AverageMorale01
        {
            get
            {
                float total = 0f;
                int alive = 0;
                for (int i = 0; i < Members.Count; i++)
                {
                    if (!Members[i].IsAlive) continue;
                    total += Members[i].Morale01;
                    alive++;
                }

                return alive <= 0 ? 0f : total / alive;
            }
        }

        public float AverageStamina01
        {
            get
            {
                float total = 0f;
                int alive = 0;
                for (int i = 0; i < Members.Count; i++)
                {
                    if (!Members[i].IsAlive) continue;
                    total += Members[i].Stamina01;
                    alive++;
                }

                return alive <= 0 ? 1f : total / alive;
            }
        }

        public bool HasOfficer(OfficerRole role)
        {
            for (int i = 0; i < Members.Count; i++)
                if (Members[i].IsAlive && Members[i].Role == role) return true;
            return false;
        }

        private int _centreFrame = -1;
        private Vector3 _centreCache;

        /// <summary>
        /// Centre of mass of the living. Practically every system asks for this — targeting, morale,
        /// squad AI, anchors, labels, the hover panel — several of them for every squad every frame,
        /// so the sum is memoised per frame rather than re-walked ten times.
        /// </summary>
        public Vector3 CentreOfMass()
        {
            if (Time.frameCount == _centreFrame) return _centreCache;

            Vector3 total = Vector3.zero;
            int alive = 0;

            for (int i = 0; i < Members.Count; i++)
            {
                if (!Members[i].IsAlive) continue;
                total += Members[i].WorldPosition;
                alive++;
            }

            _centreFrame = Time.frameCount;
            _centreCache = alive <= 0 ? AnchorPosition : total / alive;
            return _centreCache;
        }

        /// <summary>Applies a pending order once its propagation delay has elapsed.</summary>
        public void ResolvePendingOrder(float battleTime)
        {
            if (!HasPendingOrder || battleTime < PendingReadyAt) return;

            // Routed or withdrawn men do not take orders. The order is discarded, not queued —
            // shouting at a fleeing squad achieves nothing, and it must be rallied first.
            if (IsRouted || IsWithdrawn)
            {
                PendingOrder = null;
                PendingFormation = null;
                return;
            }

            if (PendingOrder.HasValue)
            {
                Order = PendingOrder.Value;
                OrderedPosition = PendingOrderedPosition;
                PendingOrder = null;
            }

            if (PendingFormation.HasValue)
            {
                Formation = PendingFormation.Value;
                PendingFormation = null;
            }
        }
    }
}
