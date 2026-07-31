using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>
    /// Turns party condition into a march speed. Kept static and side-effect free so it can be
    /// called from the view every frame, from AI planning, and from tests, with no surprises.
    /// </summary>
    public static class PartySpeedCalculator
    {
        /// <summary>Kilometres per hour.</summary>
        public static float EvaluateKph(PartyState party, CampaignSettings settings, float terrainMultiplier = 1f)
        {
            if (party == null || settings == null) return 0f;
            if (party.IsCamped || party.IsDisbanded) return 0f;

            int men = party.Roster.ActiveCount;
            if (men <= 0) return 0f;

            // A heavier column is slower. Load is measured against nominal capacity.
            float load = Mathf.Clamp01((float)men / Mathf.Max(1, party.Roster.Capacity));
            float encumbrance = Mathf.Lerp(1.10f, 0.85f, load);

            // Sheer size sets the pace: a dozen raiders slip along at a trot no war host can hold.
            // This is what lets small bands escape big ones, and makes the host a slow-moving storm.
            float size = Mathf.Lerp(1.15f, 0.78f, Mathf.InverseLerp(8f, 90f, men));

            // Tired men drag. March long enough without rest, especially overnight, and the column's
            // stamina falls — and with it the pace, compounding until someone orders a halt.
            float stamina = Mathf.Lerp(0.65f, 1f, AverageStamina01(party));

            // Wounded men set the pace of the whole column.
            float wounded = Mathf.Lerp(1f, 0.65f, party.Roster.WoundedFraction);

            float morale = Mathf.Lerp(0.80f, 1.05f, party.Morale.Value01);
            float hunger = party.Stores.Food > 0f ? 1f : 0.70f;

            // Moving unseen costs pace — but raiders have stalked these woods all their lives.
            float stealth = party.IsStealthed
                ? (party.Kind == PartyKind.Raiders ? 0.85f : 0.6f)
                : 1f;

            // "Know the Land": the commander reads the ground like the natives read the sky.
            // "Read the Ground": the speculator finds the firm path for everyone behind him.
            float doctrine = 1f;
            if (party.IsPlayer && Century.Core.ServiceLocator.TryGet(out CampaignState campaign))
            {
                if (campaign.Commander.Has("know_the_land")) doctrine = 1.08f;
                if (PostTreeCatalog.Invested(campaign, "read_the_ground")) doctrine *= 1.05f;
            }

            return settings.BaseMarchSpeedKph
                   * party.SpeedModifier
                   * encumbrance
                   * size
                   * stamina
                   * wounded
                   * morale
                   * hunger
                   * stealth
                   * doctrine
                   * Mathf.Max(0.05f, terrainMultiplier);
        }

        private static float AverageStamina01(PartyState party)
        {
            float total = 0f;
            int alive = 0;
            for (int i = 0; i < party.Roster.Soldiers.Count; i++)
            {
                SoldierRecord soldier = party.Roster.Soldiers[i];
                if (!soldier.IsAlive) continue;
                total += soldier.Stamina01;
                alive++;
            }

            return alive <= 0 ? 1f : total / alive;
        }

        /// <summary>
        /// Converts march speed into Unity units per real second, which is what a NavMeshAgent wants.
        /// Because the conversion includes the clock rate, pressing fast-forward speeds the column up
        /// for free and pausing stops it dead.
        /// </summary>
        public static float EvaluateAgentSpeed(
            PartyState party, CampaignSettings settings, float clockMultiplier, float terrainMultiplier = 1f)
        {
            float kph = EvaluateKph(party, settings, terrainMultiplier);
            float gameHoursPerRealSecond = settings.MinutesPerRealSecond * clockMultiplier / 60f;

            // OvermapSpeedScale is a designer knob applied only here, not in EvaluateKph, so the HUD
            // still reports the fiction's true km/h while the world can be paced up or down for feel.
            return kph * settings.UnitsPerKilometre * gameHoursPerRealSecond * settings.OvermapSpeedScale;
        }
    }
}
