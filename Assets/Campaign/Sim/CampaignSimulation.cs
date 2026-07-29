using System;
using System.Collections.Generic;
using Century.Campaign.Model;
using Century.Core;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Advances party condition as campaign time passes: rations burned, kit worn, men tiring,
    /// morale sagging when the food runs out.
    /// </summary>
    /// <remarks>
    /// Driven by <see cref="CampaignClock.HourElapsed"/> rather than by Update. That means one
    /// second of real time and eight seconds of fast-forward consume exactly the same rations for
    /// the same game hours, which is the behaviour players expect and the thing most prototypes
    /// get wrong.
    /// </remarks>
    public sealed class CampaignSimulation : IDisposable
    {
        private readonly CampaignState _state;
        private readonly CampaignSettings _settings;
        private bool _disposed;

        /// <summary>Raised when something worth a line in the Recent Events panel happens.</summary>
        public event Action<PartyState, string> Notified;

        public CampaignSimulation(CampaignState state, CampaignSettings settings)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _state.Clock.HourElapsed += OnHourElapsed;
        }

        private void OnHourElapsed(CampaignTime now)
        {
            List<PartyState> parties = _state.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                PartyState party = parties[i];
                if (party.IsDisbanded) continue;
                StepParty(party, now);
            }
        }

        private void StepParty(PartyState party, CampaignTime now)
        {
            int mouths = party.Roster.ActiveCount;
            if (mouths <= 0)
            {
                party.IsDisbanded = true;
                return;
            }

            const float hoursPerStep = 1f;
            float dayFraction = hoursPerStep / CampaignTime.HoursPerDay;

            // --- Food ----------------------------------------------------------------------
            // The men eat cooked Food first; when it runs out they eat ready-to-eat items straight
            // from the packs (never raw stores — those need the cooking fire). Only when both are
            // gone do they go hungry. Doctrine thins the burn: logistics or plain hard living.
            float rationScale = 1f;
            if (party.IsPlayer)
            {
                if (_state.Commander.Has("roman_logistics")) rationScale *= 0.9f;
                if (_state.Commander.Has("one_more_day")) rationScale *= 0.85f;
            }

            float foodNeeded = mouths * _settings.RationsPerManPerDay * dayFraction * rationScale;

            if (party.Stores.Food < foodNeeded)
                party.Stores.Food += Core.Items.ItemCatalog.EatReadyFood(
                    party.Inventory, foodNeeded - party.Stores.Food);

            bool wentHungry = party.Stores.Food < foodNeeded;
            party.Stores.Food = Mathf.Max(0f, party.Stores.Food - foodNeeded);

            // --- Men -----------------------------------------------------------------------
            bool marching = !party.IsCamped && party.Destination.HasValue;

            for (int i = 0; i < party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = party.Roster.Soldiers[i];
                if (!soldier.IsAlive) continue;

                if (wentHungry)
                {
                    soldier.Morale01 = Mathf.Clamp01(soldier.Morale01 - _settings.StarvationMoralePerHour);
                    soldier.Health01 = Mathf.Clamp01(soldier.Health01 - _settings.StarvationHealthPerHour);
                }

                if (party.IsCamped)
                {
                    soldier.Stamina01 = Mathf.Clamp01(soldier.Stamina01 + _settings.CampedStaminaRecoveryPerHour);
                }
                else if (marching)
                {
                    // Marching through the night wears men far faster than the daylight road — the
                    // cost of outrunning a pursuer is a slower, wearier column tomorrow.
                    float nightMultiplier = now.IsNight ? _settings.NightMarchStaminaMultiplier : 1f;
                    soldier.Stamina01 = Mathf.Clamp01(
                        soldier.Stamina01 - _settings.MarchStaminaCostPerHour * nightMultiplier);
                }
            }

            if (marching)
            {
                party.Stores.EquipmentCondition01 =
                    Mathf.Clamp01(party.Stores.EquipmentCondition01 - _settings.MarchEquipmentWearPerHour);

                // Hours moved unseen are the quiet school of the Survivor.
                if (party.IsPlayer && party.IsStealthed)
                    _state.Commander.Note(CommanderPhilosophy.Survivor, 0.02f);
            }

            party.Morale.Value01 = party.Roster.AverageMorale01;

            if (wentHungry && party.IsPlayer && now.Hour == 6)
                Notified?.Invoke(party, "The men went without rations.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _state.Clock.HourElapsed -= OnHourElapsed;
            _disposed = true;
        }
    }
}
