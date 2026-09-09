using System.Collections;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// The first battle's guided command tutorial: a linear flow of pop-ups, each holding the clock
    /// until the player performs the thing it teaches — select with TAB, HOLD the line, ADVANCE
    /// when the enemy tires, RALLY — and a closing line when the enemy breaks. Runs post-deployment
    /// in the first campaign battle (BattleRequest.ShowIntro), replacing the old wall-of-text
    /// explainer with doing instead of reading.
    /// </summary>
    /// <remarks>
    /// The same machinery as the opening's pop-ups: the shared "opening-tutorial" sheet in
    /// BattleHud.uxml (never in use here, because a tutorial battle is never the opening) and
    /// <see cref="BattleTimeControls.ScriptHold"/> for the frozen clock. Input and the UI run
    /// unscaled, so orders can be given while the world stands still. Every wait also watches for
    /// the battle ending under it — a withdrawal mid-lesson must never leave the clock held.
    /// </remarks>
    public sealed class BattleTutorial : MonoBehaviour
    {
        private BattleState _state;
        private SquadCommandInput _commands;
        private BattleTimeControls _time;

        private VisualElement _sheet;
        private Label _title, _body, _hint;
        private float _hideAt = float.MaxValue;

        private SquadOrder? _lastOrder;

        public void Initialise(
            BattleState state, VisualElement hudRoot, SquadCommandInput commands,
            BattleTimeControls time)
        {
            _state = state;
            _commands = commands;
            _time = time;

            if (hudRoot != null)
            {
                _sheet = hudRoot.Q<VisualElement>("opening-tutorial");
                _title = hudRoot.Q<Label>("opening-tutorial-title");
                _body = hudRoot.Q<Label>("opening-tutorial-body");
                _hint = hudRoot.Q<Label>("opening-tutorial-hint");
            }

            if (_commands != null) _commands.OrderIssued += OnOrderIssued;

            StartCoroutine(Run());
        }

        private void OnOrderIssued(SquadOrder? order)
        {
            if (order.HasValue) _lastOrder = order;
        }

        private void Update()
        {
            if (_sheet == null || Time.unscaledTime < _hideAt) return;
            _sheet.style.display = DisplayStyle.None;
            _hideAt = float.MaxValue;
        }

        private IEnumerator Run()
        {
            // Post-deployment: the fighting phase, with a breath for the opening banner to clear.
            yield return new WaitUntil(() => _state.Phase == BattlePhase.Fighting || BattleOver);
            if (BattleOver) { Finish(); yield break; }
            yield return WaitRealtime(0.8f);

            // 1 — what this is.
            yield return Step("THE CENTURION",
                "In Century, you fight on the front with your men: command them in the fight, " +
                "keep their morale up, and lead them to victory.",
                "CLICK TO CONTINUE",
                () => Input.GetMouseButtonDown(0));
            if (BattleOver) { Finish(); yield break; }

            // 2 — selection.
            yield return Step("YOUR MEN",
                "This is you and a squad of your men. Move the cursor over them and press TAB " +
                "to select them.",
                "TAB · SELECT",
                () => _commands != null && _commands.SelectedCount > 0);
            if (BattleOver) { Finish(); yield break; }

            // 3 — hold the line.
            _lastOrder = null;
            yield return Step("HOLD THE LINE",
                "The enemy is advancing. Give them the HOLD command.",
                "PRESS 3 · HOLD",
                () => _lastOrder == SquadOrder.HoldPosition);
            if (BattleOver) { Finish(); yield break; }

            // The fight runs: the enemy spends itself on the shields. The lesson waits for their
            // arms to grow heavy — or stops waiting if a strange battle never tires them.
            float tiredThreshold = Mathf.Max(0.3f, EnemyAverageStamina() - 0.22f);
            float earliest = _state.ElapsedSeconds + 8f;
            float latest = _state.ElapsedSeconds + 50f;
            yield return new WaitUntil(() =>
                BattleOver || EnemiesBroken()
                || (_state.ElapsedSeconds >= earliest && EnemyAverageStamina() <= tiredThreshold)
                || _state.ElapsedSeconds >= latest);
            if (BattleOver) { Finish(); yield break; }

            if (!EnemiesBroken())
            {
                // 4 — press them.
                _lastOrder = null;
                yield return Step("PRESS THEM",
                    "They are getting tired. Now is the time to press against them. Select your " +
                    "squad and give them an ADVANCE order.",
                    "PRESS 2 · ADVANCE",
                    () => _lastOrder == SquadOrder.Advance);
                if (BattleOver) { Finish(); yield break; }

                // A moment for the line to step off before the horn.
                yield return WaitScaled(1.2f);
                if (BattleOver) { Finish(); yield break; }

                // 5 — the rally. A player who already spent it has learnt the lesson himself.
                if (!EnemiesBroken() && !(_state.RallyActive || _state.RallyCooldownLeft > 0f))
                {
                    yield return Step("RALLY THEM!",
                        "Press R to buff your men, increasing their stamina and reducing " +
                        "incoming damage!",
                        "PRESS R · RALLY",
                        () => _state.RallyActive || _state.RallyCooldownLeft > 0f);
                    if (BattleOver) { Finish(); yield break; }
                }
            }

            // 6 — the rout.
            yield return new WaitUntil(() => EnemiesBroken() || BattleOver);
            if (!BattleOver)
            {
                Show("THEY ARE RETREATING!", "The day is yours!", null);
                _hideAt = Time.unscaledTime + 3.5f;
            }

            Finish();
        }

        /// <summary>One lesson: the pop-up goes up, the clock stops, and both stay until the player
        /// does the thing — then the world moves and the words linger three seconds while he does it
        /// again for real. A battle ending mid-lesson releases everything.</summary>
        private IEnumerator Step(string title, string body, string hint, System.Func<bool> done)
        {
            Show(title, body, hint);
            _time?.Hold();

            yield return null;   // never satisfied by the click that closed the last pop-up
            yield return new WaitUntil(() => done() || BattleOver);

            _time?.ReleaseHold();
            _hideAt = Time.unscaledTime + 3f;
        }

        private void Show(string title, string body, string hint)
        {
            if (_sheet == null) return;

            if (_title != null) _title.text = title;
            if (_body != null)
            {
                _body.text = body ?? string.Empty;
                _body.style.display = string.IsNullOrEmpty(body) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_hint != null)
            {
                _hint.text = hint ?? string.Empty;
                _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            _sheet.style.display = DisplayStyle.Flex;
            _hideAt = float.MaxValue;
        }

        private void Finish()
        {
            _time?.ReleaseHold();
            if (_commands != null) _commands.OrderIssued -= OnOrderIssued;

            // A battle that ended under a lesson takes the words with it; a finished tutorial
            // lets its last line linger on the ordinary hide clock.
            if (BattleOver && _sheet != null)
            {
                _sheet.style.display = DisplayStyle.None;
                _hideAt = float.MaxValue;
            }
        }

        private bool BattleOver => _state == null || _state.Phase == BattlePhase.Aftermath;

        /// <summary>Every enemy squad routed, withdrawn or destroyed: the moment step 6 names.</summary>
        private bool EnemiesBroken()
        {
            for (int i = 0; i < _state.EnemySquads.Count; i++)
                if (_state.EnemySquads[i].IsEffective) return false;
            return true;
        }

        /// <summary>Average wind of the enemy still in the fight; 1 when none are.</summary>
        private float EnemyAverageStamina()
        {
            float total = 0f;
            int squads = 0;
            for (int i = 0; i < _state.EnemySquads.Count; i++)
            {
                BattleSquad squad = _state.EnemySquads[i];
                if (!squad.IsEffective || squad.IsOffField) continue;
                total += squad.AverageStamina01;
                squads++;
            }
            return squads <= 0 ? 1f : total / squads;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        /// <summary>Battle-clock seconds: honours pause and tactical time.</summary>
        private IEnumerator WaitScaled(float seconds)
        {
            float until = _state.ElapsedSeconds + seconds;
            while (_state.ElapsedSeconds < until && !BattleOver) yield return null;
        }

        private void OnDestroy() => Finish();
    }
}
