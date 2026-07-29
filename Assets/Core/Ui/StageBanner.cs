using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Core.Ui
{
    /// <summary>
    /// The large centre-screen announcement: "BATTLE BEGINS", "THE FIELD IS OURS".
    /// </summary>
    /// <remarks>
    /// Builds its own elements rather than requiring UXML, so a scene needs no extra wiring to use it.
    /// Styling comes from the shared stylesheet via class names, so it inherits the game's look
    /// without duplicating any of it here.
    ///
    /// These exist because a fight that simply starts feels like a simulation, and one that is
    /// announced feels like an event. The cost is a second of the player's time and it buys most of
    /// the sense of occasion.
    /// </remarks>
    public sealed class StageBanner
    {
        private readonly VisualElement _container;
        private readonly VisualElement _frame;
        private readonly Label _headline;
        private readonly Label _sub;

        public StageBanner(VisualElement root)
        {
            _container = new VisualElement { pickingMode = PickingMode.Ignore };
            _container.AddToClassList("stage-banner");
            _container.style.display = DisplayStyle.None;

            _frame = new VisualElement { pickingMode = PickingMode.Ignore };
            _frame.AddToClassList("stage-banner__frame");

            _headline = new Label { pickingMode = PickingMode.Ignore };
            _headline.AddToClassList("stage-banner__headline");

            _sub = new Label { pickingMode = PickingMode.Ignore };
            _sub.AddToClassList("stage-banner__sub");

            _frame.Add(_headline);
            _frame.Add(_sub);
            _container.Add(_frame);

            root?.Add(_container);
        }

        /// <summary>Rises in, holds, fades out. Yield on this from a coroutine to sequence around it.</summary>
        public IEnumerator Show(string headline, string sub, float holdSeconds = 1.4f, string modifier = null)
        {
            _headline.text = headline != null ? headline.ToUpperInvariant() : string.Empty;
            _sub.text = sub ?? string.Empty;
            _sub.style.display = string.IsNullOrEmpty(sub) ? DisplayStyle.None : DisplayStyle.Flex;

            _frame.ClearClassList();
            _frame.AddToClassList("stage-banner__frame");
            if (!string.IsNullOrEmpty(modifier)) _frame.AddToClassList(modifier);

            _container.style.display = DisplayStyle.Flex;

            // Letter-spacing opens out as it appears. Subtle, but it is what makes the title feel
            // like it is being declared rather than switched on.
            yield return UiTween.Over(0.36f, Easing.OutCubic, t =>
            {
                _container.style.opacity = t;
                _headline.style.letterSpacing = new StyleLength(Mathf.Lerp(-4f, 6f, t));
                _frame.style.scale = new Scale(new Vector2(Mathf.Lerp(0.94f, 1f, t), 1f));
            });

            yield return UiTween.Wait(holdSeconds);
            yield return UiTween.Fade(_container, 1f, 0f, 0.3f);

            _container.style.display = DisplayStyle.None;
        }

        public void HideImmediate()
        {
            _container.style.display = DisplayStyle.None;
            _container.style.opacity = 0f;
        }
    }
}
