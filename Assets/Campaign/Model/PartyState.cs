using System;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;

namespace Century.Campaign.Model
{
    /// <summary>
    /// A band on the overmap: the player's century, a Cherusci warband, a column of refugees.
    /// This is the model. <see cref="View.PartyView"/> is a disposable representation of it that
    /// only exists while the overmap scene is loaded.
    /// </summary>
    [Serializable]
    public sealed class PartyState
    {
        public string Id;
        public string DisplayName;
        public PartyFaction Faction = PartyFaction.Germanic;

        /// <summary>What manner of force this is; drives overmap AI and spawn sizing.</summary>
        public PartyKind Kind = PartyKind.Warband;

        /// <summary>The commander's doctrine, chosen at spawn. Carried into any battle this band fights.</summary>
        public CommanderBehaviour Behaviour = CommanderBehaviour.Disciplined;

        public Roster Roster = new Roster();
        public Stores Stores = new Stores();
        public MoraleState Morale = new MoraleState();

        /// <summary>Everything looted, traded and carried — the stuff of the inventory screen.</summary>
        public Core.Items.PartyInventory Inventory = new Core.Items.PartyInventory();

        /// <summary>Senior appointments (Optio, Tesserarius, ...) handed out at camp. Player only in practice.</summary>
        public CampAppointments Appointments = new CampAppointments();

        /// <summary>Camp stations this party has raised and their levels. Player only in practice.</summary>
        public CampFacilities Facilities = new CampFacilities();

        /// <summary>Authoritative world position. The view reads this on spawn and writes it back as it moves.</summary>
        public Vector3 WorldPosition;

        /// <summary>Current destination, or null when halted. Survives a battle so marches resume.</summary>
        public Vector3? Destination;

        /// <summary>Per-party speed tuning, 1 = baseline. Cavalry scouts sit above 1, baggage below.</summary>
        public float SpeedModifier = 1f;

        /// <summary>How far this party notices others, in world units. Not the battle trigger — see contact radius.</summary>
        public float DetectionRadius = 60f;

        /// <summary>Party is camped: does not move, recovers stamina, burns fewer supplies.</summary>
        public bool IsCamped;

        /// <summary>Party is trying to avoid contact. Shrinks the radius at which others detect it.</summary>
        public bool IsStealthed;

        public bool IsPlayer;

        /// <summary>Set once destroyed or absorbed; the overmap skips these without reallocating the list.</summary>
        public bool IsDisbanded;

        // --- Overmap AI ---------------------------------------------------------------------

        /// <summary>Ignored for the player's party, which is driven by input.</summary>
        public PartyAiState AiState = PartyAiState.Idle;

        /// <summary>Party currently being chased, if any.</summary>
        public string PursuitTargetId;

        /// <summary>When the AI may next choose a new course of action.</summary>
        public CampaignTime NextDecisionTime;

        /// <summary>When a pursuit should be abandoned as hopeless.</summary>
        public CampaignTime PursuitExpiryTime;

        /// <summary>
        /// Contact is suppressed until this time. Set after a battle so that a withdrawal is not
        /// immediately punished by re-triggering the same fight on the next frame.
        /// </summary>
        public CampaignTime ContactCooldownUntil;

        /// <summary>
        /// While the clock stands before this, the party is SURPRISED — caught flat by something
        /// revealed suddenly out of the dark. A battle joined inside this window is an ambush: the
        /// surprised side gets no deployment. Set by the ambush director; read at contact.
        /// </summary>
        public CampaignTime SurprisedUntil;

        public bool IsSurprised(CampaignTime now) => now.TotalMinutes < SurprisedUntil.TotalMinutes;

        public bool CanFight => !IsDisbanded && Roster.CombatReadyCount > 0;

        /// <summary>Effective radius at which this party is spotted by others.</summary>
        public float VisibilityRadius(float stealthMultiplier)
        {
            float baseVisibility = Mathf.Max(20f, Roster.ActiveCount * 1.2f);
            return IsStealthed ? baseVisibility * stealthMultiplier : baseVisibility;
        }
    }
}
