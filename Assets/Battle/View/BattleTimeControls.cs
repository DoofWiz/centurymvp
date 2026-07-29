using Century.Battle.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// Command of the clock: pause, normal and double speed from the HUD buttons, and — the one that
    /// matters in the press — TACTICAL TIME on held Space, which slows the world to a third while the
    /// Centurion reads the field and gives orders without the fight running away from him.
    /// </summary>
    /// <remarks>
    /// Drives <see cref="Time.timeScale"/>, which the whole battle already respects: the simulation
    /// ticks on deltaTime, pila fly on physics, and soldiers move on scaled time. Orders CAN be
    /// issued while paused or slowed — input and UI run unscaled — they simply propagate when time
    /// moves. The scale is forced back to 1 the moment the fighting phase ends and when this
    /// component dies, so no other scene ever inherits a slowed clock.
    /// </remarks>
    public sealed class BattleTimeControls : MonoBehaviour
    {
        [Tooltip("Time scale while Space is held: slow enough to think, alive enough to read.")]
        [Range(0.05f, 0.8f)] [SerializeField] private float _tacticalScale = 0.3f;

        [Tooltip("Time scale of the fast setting.")]
        [Range(1.5f, 4f)] [SerializeField] private float _fastScale = 2f;

        private BattleState _state;
        private Button _pause, _normal, _fast;
        private VisualElement _tacticalHint;

        private enum Mode { Paused, Normal, Fast }
        private Mode _mode = Mode.Normal;

        /// <summary>True while held Space has the world slowed.</summary>
        public bool TacticalHeld { get; private set; }

        public void Initialise(BattleState state, VisualElement root)
        {
            _state = state;
            if (root == null) return;

            _pause = root.Q<Button>("time-pause");
            _normal = root.Q<Button>("time-normal");
            _fast = root.Q<Button>("time-fast");
            _tacticalHint = root.Q<VisualElement>("tactical-hint");

            if (_pause != null) _pause.clicked += () => SetMode(Mode.Paused);
            if (_normal != null) _normal.clicked += () => SetMode(Mode.Normal);
            if (_fast != null) _fast.clicked += () => SetMode(Mode.Fast);

            RefreshButtons();
        }

        private void SetMode(Mode mode)
        {
            // The pause button is a toggle: pressing it again lets time run.
            _mode = mode == Mode.Paused && _mode == Mode.Paused ? Mode.Normal : mode;
            RefreshButtons();
        }

        private void Update()
        {
            if (_state == null) return;

            if (_state.Phase != BattlePhase.Fighting)
            {
                Time.timeScale = 1f;
                TacticalHeld = false;
                return;
            }

            TacticalHeld = _mode != Mode.Paused && Input.GetKey(KeyCode.Space);

            float scale = _mode == Mode.Paused ? 0f
                : TacticalHeld ? _tacticalScale
                : _mode == Mode.Fast ? _fastScale
                : 1f;

            Time.timeScale = scale;

            if (_tacticalHint != null)
                _tacticalHint.style.opacity = TacticalHeld ? 1f : 0.55f;
        }

        private void RefreshButtons()
        {
            Style(_pause, _mode == Mode.Paused);
            Style(_normal, _mode == Mode.Normal);
            Style(_fast, _mode == Mode.Fast);
        }

        private static void Style(Button button, bool active)
        {
            if (button == null) return;
            if (active) button.AddToClassList("time-button--active");
            else button.RemoveFromClassList("time-button--active");
        }

        private void OnDisable() => Time.timeScale = 1f;

        private void OnDestroy() => Time.timeScale = 1f;
    }
}
