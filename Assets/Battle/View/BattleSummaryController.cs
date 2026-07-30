using System;
using System.Collections;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Core.Contracts;
using Century.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// The post-battle summary, revealed in sequence: casualties, then spoils, then experience and
    /// promotions, then the commander's own advance.
    /// </summary>
    /// <remarks>
    /// The sequencing is the feature. A single screen showing eight numbers tells the player that
    /// something happened; the same eight numbers arriving one at a time, counting up, tells them what
    /// they gained and what it cost. Everything here is driven by one coroutine so the order and the
    /// pacing are readable in a single method.
    ///
    /// The screen never blocks: a Continue button is live from the first frame, and pressing it skips
    /// straight to the end state rather than cancelling mid-reveal.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleSummaryController : MonoBehaviour
    {
        [Header("Pacing")]
        [SerializeField] private float _sectionGap = 0.35f;
        [SerializeField] private float _countDuration = 0.7f;
        [SerializeField] private float _rowStagger = 0.08f;

        private UIDocument _document;
        private BattleState _state;
        private BattleResult _result;

        private VisualElement _panel, _casualtyList, _spoilsList, _experienceList, _floaterHost;
        private Label _title, _subtitle, _commanderXp;
        private VisualElement _commanderXpBar;
        private Button _continueButton;

        private Coroutine _reveal;
        private bool _finished;

        public event Action Dismissed;

        private void Awake() => _document = GetComponent<UIDocument>();

        public void Show(BattleState state, BattleResult result)
        {
            _state = state;
            _result = result;

            VisualElement root = _document.rootVisualElement;
            if (root == null) return;

            UiInputBootstrapper.EnsureEventSystem();
            BattleHudPanels.SetCombatVisible(root, false);

            CacheElements(root);

            if (_panel == null)
            {
                Debug.LogWarning("[BattleSummary] 'summary-panel' missing from BattleHud.uxml.", this);
                Dismissed?.Invoke();
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            _reveal = StartCoroutine(Reveal());
        }

        private void CacheElements(VisualElement root)
        {
            _panel = root.Q<VisualElement>("summary-panel");
            _title = root.Q<Label>("summary-title");
            _subtitle = root.Q<Label>("summary-subtitle");
            _casualtyList = root.Q<VisualElement>("summary-casualties");
            _spoilsList = root.Q<VisualElement>("summary-spoils");
            _experienceList = root.Q<VisualElement>("summary-experience");
            _commanderXp = root.Q<Label>("summary-commander-xp");
            _commanderXpBar = root.Q<VisualElement>("summary-commander-bar");
            _floaterHost = root.Q<VisualElement>("summary-floaters");

            _continueButton = root.Q<Button>("summary-continue");
            if (_continueButton != null) _continueButton.clicked += Dismiss;
        }

        // --- The reveal -----------------------------------------------------------------------

        private IEnumerator Reveal()
        {
            SetHeadline();

            yield return UiTween.Wait(0.4f);

            yield return RevealCasualties();
            yield return UiTween.Wait(_sectionGap);

            yield return RevealSpoils();
            yield return UiTween.Wait(_sectionGap);

            yield return RevealExperience();
            yield return UiTween.Wait(_sectionGap);

            yield return RevealCommander();

            _finished = true;
        }

        private void SetHeadline()
        {
            string headline;
            string sub;

            switch (_result.Outcome)
            {
                case BattleOutcome.Victory:
                    headline = _result.EnemyAnnihilated ? "ANNIHILATED" : "THE FIELD IS OURS";
                    sub = $"{_state.EnemyDisplayName} is broken";
                    break;
                case BattleOutcome.Defeat:
                    headline = "THE CENTURY IS BROKEN";
                    sub = "What remains must withdraw";
                    break;
                case BattleOutcome.Withdrawal:
                    headline = "WITHDRAWN";
                    sub = "The field is abandoned, the century intact";
                    break;
                default:
                    headline = "THE BATTLE ENDS";
                    sub = string.Empty;
                    break;
            }

            if (_title != null) _title.text = headline;
            if (_subtitle != null) _subtitle.text = sub;

            _title?.EnableInClassList("text-danger", _result.Outcome == BattleOutcome.Defeat);
            _title?.EnableInClassList("text-gold", _result.Outcome == BattleOutcome.Victory);
        }

        /// <summary>
        /// The fallen, named, one at a time. This is the section that makes the roster mean something —
        /// a number would not.
        /// </summary>
        private IEnumerator RevealCasualties()
        {
            if (_casualtyList == null) yield break;
            _casualtyList.Clear();

            var fallen = new List<CombatantOutcome>();
            for (int i = 0; i < _result.Combatants.Count; i++)
                if (!_result.Combatants[i].Survived) fallen.Add(_result.Combatants[i]);

            if (fallen.Count == 0)
            {
                _casualtyList.Add(MakeLine("Not a man lost.", "text-good"));
                yield break;
            }

            for (int i = 0; i < fallen.Count && i < 12; i++)
            {
                BattleCombatant man = FindCombatant(fallen[i].SoldierId);
                string label = man != null ? man.DisplayName : fallen[i].SoldierId;
                string rank = man != null && man.Role != OfficerRole.None
                    ? $"  ({man.Role.ToString().ToUpperInvariant()})"
                    : string.Empty;

                // The clearing of the field: some of the fallen are found still breathing.
                VisualElement line = fallen[i].Wounded
                    ? MakeLine($"{label}{rank} — wounded, carried off", "text-gold")
                    : MakeLine($"✖  {label}{rank}", "text-danger");
                _casualtyList.Add(line);

                yield return UiTween.AppearFromBelow(line, 0.2f, 8f);
                yield return UiTween.Wait(_rowStagger);
            }

            if (fallen.Count > 12)
                _casualtyList.Add(MakeLine($"…and {fallen.Count - 12} more", "text-faint"));
        }

        /// <summary>Spoils count up, each item arriving separately with a rising marker.</summary>
        private IEnumerator RevealSpoils()
        {
            if (_spoilsList == null) yield break;
            _spoilsList.Clear();

            LootBundle loot = _result.Loot;

            if (loot.IsEmpty)
            {
                _spoilsList.Add(MakeLine("Nothing worth carrying.", "text-faint"));
                yield break;
            }

            yield return Spoil("FOOD", loot.Food, "{0:0}");
            yield return Spoil("COIN", loot.Coin, "{0:0}");
            yield return Spoil("PRISONERS", loot.Prisoners, "{0:0}");

            // Itemised spoils, named from the catalog — the same things that land in the inventory.
            for (int i = 0; i < loot.Items.Count; i++)
            {
                LootItem item = loot.Items[i];
                Century.Core.Items.ItemDef def = Century.Core.Items.ItemCatalog.Find(item.ItemId);
                yield return Spoil(def != null ? def.Name.ToUpperInvariant() : item.ItemId, item.Count, "× {0:0}");
            }
        }

        private IEnumerator Spoil(string label, float amount, string format)
        {
            if (amount <= 0f) yield break;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("spoil");

            var name = new Label(label) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("spoil__label");
            row.Add(name);

            var value = new Label("0") { pickingMode = PickingMode.Ignore };
            value.AddToClassList("spoil__value");
            row.Add(value);

            _spoilsList.Add(row);

            yield return UiTween.AppearFromBelow(row, 0.18f, 6f);

            if (_floaterHost != null)
                StartCoroutine(FloatingText.Rise(_floaterHost, $"+{amount:0} {label}", "floater--good"));

            yield return UiTween.CountUp(value, 0f, amount, _countDuration, format);
            yield return UiTween.Punch(value, 0.12f, 0.25f);
        }

        /// <summary>
        /// Experience per man, with promotions called out. Only men who actually gained something are
        /// listed, and officers first, because those are the names the player knows.
        /// </summary>
        private IEnumerator RevealExperience()
        {
            if (_experienceList == null) yield break;
            _experienceList.Clear();

            var earners = new List<CombatantOutcome>();
            for (int i = 0; i < _result.Combatants.Count; i++)
                if (_result.Combatants[i].Survived && _result.Combatants[i].ExperienceGained > 0)
                    earners.Add(_result.Combatants[i]);

            if (earners.Count == 0)
            {
                _experienceList.Add(MakeLine("No experience earned.", "text-faint"));
                yield break;
            }

            // Officers and top performers first; the tail is summarised.
            earners.Sort((a, b) =>
            {
                int roleCompare = RoleWeight(b.SoldierId).CompareTo(RoleWeight(a.SoldierId));
                return roleCompare != 0 ? roleCompare : b.ExperienceGained.CompareTo(a.ExperienceGained);
            });

            int shown = Mathf.Min(earners.Count, 8);
            int remainder = 0;

            for (int i = shown; i < earners.Count; i++) remainder += earners[i].ExperienceGained;

            for (int i = 0; i < shown; i++)
            {
                yield return ExperienceRow(earners[i]);
                yield return UiTween.Wait(_rowStagger);
            }

            if (remainder <= 0) yield break;

            VisualElement tail = MakeLine(
                $"…and {earners.Count - shown} others, {remainder} experience between them", "text-faint");
            _experienceList.Add(tail);
            yield return UiTween.AppearFromBelow(tail, 0.2f, 6f);
        }

        private IEnumerator ExperienceRow(CombatantOutcome outcome)
        {
            BattleCombatant man = FindCombatant(outcome.SoldierId);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("xp-row");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("xp-row__head");

            string displayName = man != null ? man.DisplayName : outcome.SoldierId;
            var name = new Label(displayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("xp-row__name");
            head.Add(name);

            var gained = new Label("+0") { pickingMode = PickingMode.Ignore };
            gained.AddToClassList("xp-row__gain");
            head.Add(gained);

            row.Add(head);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("bar");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("bar__fill");
            fill.AddToClassList("bar__fill--gold");
            fill.style.width = Length.Percent(0f);
            track.Add(fill);
            row.Add(track);

            _experienceList.Add(row);

            yield return UiTween.AppearFromBelow(row, 0.18f, 6f);

            // The bar is a proportional read on the battle's largest single award, so the biggest
            // contributor fills it and everyone else is measured against him.
            float best = Mathf.Max(1f, LargestAward());
            float share = Mathf.Clamp01(outcome.ExperienceGained / best);

            StartCoroutine(UiTween.CountUpInt(gained, 0, outcome.ExperienceGained, _countDuration, "+{0}"));
            yield return UiTween.FillBar(fill, 0f, share, _countDuration);

            if (outcome.Kills <= 0) yield break;

            var kills = new Label($"{outcome.Kills} slain") { pickingMode = PickingMode.Ignore };
            kills.AddToClassList("xp-row__note");
            row.Add(kills);
            yield return UiTween.Fade(kills, 0f, 1f, 0.2f);
        }

        private IEnumerator RevealCommander()
        {
            if (_commanderXp == null) yield break;

            int gained = _result.CommanderExperience;

            yield return UiTween.CountUpInt(_commanderXp, 0, gained, _countDuration, "+{0} EXPERIENCE");

            if (_commanderXpBar != null)
                yield return UiTween.FillBar(_commanderXpBar, 0f, Mathf.Clamp01(gained / 200f), _countDuration);

            if (_floaterHost != null && gained > 0)
                yield return FloatingText.Rise(_floaterHost, "THE CENTURION LEARNS", "floater--gold");
        }

        // --- Helpers --------------------------------------------------------------------------

        private float LargestAward()
        {
            float best = 0f;
            for (int i = 0; i < _result.Combatants.Count; i++)
                best = Mathf.Max(best, _result.Combatants[i].ExperienceGained);
            return best;
        }

        private int RoleWeight(string soldierId)
        {
            BattleCombatant man = FindCombatant(soldierId);
            if (man == null) return 0;

            switch (man.Role)
            {
                case OfficerRole.Optio: return 4;
                case OfficerRole.Tesserarius: return 3;
                case OfficerRole.Medicus: return 2;
                case OfficerRole.Immunis: return 1;
                default: return 0;
            }
        }

        private BattleCombatant FindCombatant(string soldierId)
        {
            foreach (BattleCombatant man in _state.AllPlayerSideCombatants())
                if (man.SoldierId == soldierId) return man;
            return null;
        }

        private static VisualElement MakeLine(string text, string extraClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("summary-line");
            if (!string.IsNullOrEmpty(extraClass)) label.AddToClassList(extraClass);
            return label;
        }

        /// <summary>
        /// Continue is live immediately. Pressing it before the reveal finishes jumps to the end rather
        /// than cancelling — a player who has seen enough should not have to sit through the rest, and
        /// should not lose the information either.
        /// </summary>
        private void Dismiss()
        {
            if (!_finished && _reveal != null)
            {
                StopCoroutine(_reveal);
                _reveal = null;
                StartCoroutine(FinishImmediately());
                return;
            }

            if (_panel != null) _panel.style.display = DisplayStyle.None;
            Dismissed?.Invoke();
        }

        private IEnumerator FinishImmediately()
        {
            float previousGap = _sectionGap;
            float previousCount = _countDuration;
            float previousStagger = _rowStagger;

            _sectionGap = 0f;
            _countDuration = 0f;
            _rowStagger = 0f;

            yield return Reveal();

            _sectionGap = previousGap;
            _countDuration = previousCount;
            _rowStagger = previousStagger;
            _finished = true;
        }

        private void Update()
        {
            if (_panel == null || _panel.style.display == DisplayStyle.None) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Dismiss();
        }
    }
}
