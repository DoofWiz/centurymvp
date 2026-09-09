using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Diagnostic readout of squad state, plus a rough version of the alert banner from the mock-up.
    /// Replaced by the real HUD later; for now it is the fastest way to see whether cohesion, rout
    /// and rally are behaving.
    /// </summary>
    public sealed class DebugBattleHud : MonoBehaviour
    {
        // Deliberately NOT a serialized field. Existing scene objects already have "visible = true"
        // written into them from before the real HUD existed, and a serialized default cannot override
        // saved data — so the flag lives only at runtime. F1 still brings it up for diagnostics.
        private bool _visible;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        private BattleState _state;
        private SquadCommandInput _commands;
        private BattleEventFeed _feed;
        private PlayerCharacterController _player;

        private GUIStyle _style;
        private GUIStyle _alertStyle;

        public void Bind(
            BattleState state, SquadCommandInput commands, BattleEventFeed feed, PlayerCharacterController player)
        {
            _state = state;
            _commands = commands;
            _feed = feed;
            _player = player;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _state == null) return;
            EnsureStyles();

            DrawAlert();
            DrawPanel();
        }

        private void EnsureStyles()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            if (_alertStyle == null)
                _alertStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    richText = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
        }

        private void DrawAlert()
        {
            if (_feed == null || !_feed.HasAlert) return;

            string colour = _feed.AlertKind == BattleEventKind.Critical ? "#d94f3d" : "#e0b355";
            var rect = new Rect(Screen.width * 0.5f - 320f, 28f, 640f, 44f);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, $"<color={colour}>{_feed.AlertMessage.ToUpperInvariant()}</color>", _alertStyle);
        }

        private void DrawPanel()
        {
            string selection = _commands == null || _commands.AllSelected
                ? "ALL"
                : _commands.SelectedCount.ToString();

            GUILayout.BeginArea(new Rect(12f, 12f, 560f, 520f), GUI.skin.box);

            GUILayout.Label($"<b>{_state.EnemyDisplayName}</b>   {_state.TimeOfDay.ToClockString()}   " +
                            $"{(_state.PlayerAmbushed ? "AMBUSHED" : "engaged")}   " +
                            $"t+{_state.ElapsedSeconds:0}s", _style);

            GUILayout.Label($"Ours {_state.PlayerSideAliveCount}   Theirs {_state.EnemyAliveCount}   " +
                            $"Reserve {_state.Reserve.Count}   Selected {selection}", _style);

            if (_state.PlayerCharacter != null)
                GUILayout.Label($"Centurion  health {_state.PlayerCharacter.Health01 * 100f:0}%   " +
                                $"stamina {_state.PlayerCharacter.Stamina01 * 100f:0}%   " +
                                $"kills {_state.PlayerCharacter.Kills}" +
                                $"{(_player != null && _player.IsRallying ? "   <color=#e0b355>RALLYING</color>" : "")}",
                    _style);

            GUILayout.Space(6f);

            for (int i = 0; i < _state.PlayerSquads.Count; i++) DrawSquadLine(_state.PlayerSquads[i], i);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Enemy</b>", _style);
            for (int i = 0; i < _state.EnemySquads.Count; i++) DrawEnemyLine(_state.EnemySquads[i]);

            GUILayout.Space(8f);
            GUILayout.Label("WASD move.  Mouse faces.  LMB strike.  R fires the rally burst.", _style);
            GUILayout.Label("Hover+Tab select squad.  0 all.  1 formation menu.  2 advance.  3 hold.  4 follow.  5 skirmish.  6 fall back.  7 retreat.", _style);
            GUILayout.EndArea();
        }

        private void DrawSquadLine(BattleSquad squad, int index)
        {
            if (squad.IsDestroyed)
            {
                GUILayout.Label($"  <color=#666666>{squad.DisplayName} — wiped out</color>", _style);
                return;
            }

            string marker = _commands != null && _commands.IsSelected(index) ? "▸ " : "  ";
            string band = ColourForBand(squad.Band);

            string pending = squad.HasPendingOrder
                ? $"  → {(squad.PendingOrder.HasValue ? squad.PendingOrder.Value.ToString() : "")}" +
                  $"{(squad.PendingFormation.HasValue ? " / " + squad.PendingFormation.Value : "")}" +
                  $" in {Mathf.Max(0f, squad.PendingReadyAt - _state.ElapsedSeconds):0.0}s"
                : "";

            string rally = squad.IsRouted && squad.RallyProgress01 > 0f
                ? $"  rally {squad.RallyProgress01 * 100f:0}%"
                : "";

            string aura = squad.UnderCommandAura ? " ✦" : "";

            GUILayout.Label(
                $"{marker}{squad.DisplayName}{aura}  {squad.AliveCount}m  {squad.Order}/{squad.Formation}  " +
                $"<color={band}>{squad.Band}</color> {squad.Cohesion01 * 100f:0}%  " +
                $"dressed {squad.Dressed01 * 100f:0}%  fighting {squad.EngagedCount}{pending}{rally}",
                _style);
        }

        private void DrawEnemyLine(BattleSquad squad)
        {
            if (squad.IsDestroyed)
            {
                GUILayout.Label($"  <color=#666666>{squad.DisplayName} — destroyed</color>", _style);
                return;
            }

            GUILayout.Label($"  {squad.DisplayName}  {squad.AliveCount}m  {squad.Formation}  " +
                            $"<color={ColourForBand(squad.Band)}>{squad.Band}</color>", _style);
        }

        private static string ColourForBand(CohesionBand band)
        {
            switch (band)
            {
                case CohesionBand.Broken: return "#8a8a8a";
                case CohesionBand.Breaking: return "#d94f3d";
                case CohesionBand.Wavering: return "#e0b355";
                case CohesionBand.Fearless: return "#7fc97f";
                default: return "#cfcfcf";
            }
        }
    }
}
