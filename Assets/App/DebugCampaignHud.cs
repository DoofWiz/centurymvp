using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Core;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// Throwaway IMGUI readout of campaign state, so the simulation can be verified before any real
    /// UI exists. Delete this once the UI layer lands — it is a diagnostic, not a prototype of the HUD.
    /// </summary>
    public sealed class DebugCampaignHud : MonoBehaviour
    {
        // Deliberately NOT a serialized field. Existing scene objects already have "visible = true"
        // written into them from before the real HUD existed, and a serialized default cannot override
        // saved data — so the flag lives only at runtime. F1 still brings it up for diagnostics.
        private bool _visible;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        private CampaignState _state;
        private CampaignSettings _settings;
        private CampaignTicker _ticker;
        private GUIStyle _style;

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Re-fetched every draw: a campaign can end (the title) or be replaced (a load).
            if (!ServiceLocator.TryGet(out _state)) return;
            if (_settings == null && !ServiceLocator.TryGet(out _settings)) return;
            if (_ticker == null && !ServiceLocator.TryGet(out _ticker)) return;

            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            PartyState party = _state.PlayerParty;
            if (party == null) return;

            int men = party.Roster.ActiveCount;
            float foodDays = party.Stores.FoodDaysRemaining(men, _settings.RationsPerManPerDay);

            float kph = PartySpeedCalculator.EvaluateKph(party, _settings);

            GUILayout.BeginArea(new Rect(12f, 12f, 460f, 320f), GUI.skin.box);
            GUILayout.Label($"<b>{party.DisplayName}</b>   {_state.Clock.Now.ToWatchString()}  {_state.Clock.Now.ToClockString()}", _style);
            GUILayout.Label($"Speed control: {_ticker.Current}  (1 pause / 2 normal / 3 fast / 4 fastest)", _style);
            GUILayout.Space(6f);
            GUILayout.Label($"Men {men} / {party.Roster.Capacity}   Wounded {party.Roster.WoundedCount}", _style);
            GUILayout.Label($"Food {foodDays:0.0} days", _style);
            GUILayout.Label($"Equipment {party.Stores.EquipmentCondition01 * 100f:0}%   Coin {party.Stores.Coin}   Denarii {party.Stores.Denarii}", _style);
            GUILayout.Label($"Morale {party.Morale.ToDisplayString()}", _style);
            GUILayout.Label($"March {kph:0.00} km/h   Destination {(party.Destination.HasValue ? "set" : "halted")}", _style);
            GUILayout.Space(6f);
            GUILayout.Label(DescribeNearestHostile(party), _style);
            GUILayout.Label($"Cooldown: {DescribeCooldown(party)}", _style);
            GUILayout.Label("Right-click to march.  H halt.  WASD pan.  Space recentre.  Q/E rotate.", _style);
            GUILayout.EndArea();
        }

        private string DescribeNearestHostile(PartyState player)
        {
            PartyState nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < _state.Parties.Count; i++)
            {
                PartyState other = _state.Parties[i];
                if (other.IsDisbanded || !PartyRelations.IsHostile(player, other)) continue;

                float distance = Vector3.Distance(other.WorldPosition, player.WorldPosition);
                if (distance >= nearestDistance) continue;

                nearest = other;
                nearestDistance = distance;
            }

            if (nearest == null) return "No hostiles remain.";

            return $"Nearest: {nearest.DisplayName} ({nearest.Roster.ActiveCount}) " +
                   $"{nearestDistance:0} units, {nearest.AiState}";
        }

        private string DescribeCooldown(PartyState player)
        {
            double remaining = player.ContactCooldownUntil.TotalMinutes - _state.Clock.Now.TotalMinutes;
            return remaining <= 0d ? "clear" : $"{remaining:0} min";
        }
    }
}
