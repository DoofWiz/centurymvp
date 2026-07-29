using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Core.Ui
{
    /// <summary>
    /// Short-lived text that rises and fades at a screen position: "+8 RATIONS", "PROMOTED".
    /// </summary>
    /// <remarks>
    /// Used by the summary screen to mark individual gains as they land. Each call creates and then
    /// removes its own element, so there is no pool to manage and nothing to reset between battles.
    /// At the volumes involved — a few dozen over several seconds — the allocation is irrelevant.
    /// </remarks>
    public static class FloatingText
    {
        public static IEnumerator Rise(
            VisualElement host, string text, string extraClass = null,
            float rise = 28f, float duration = 0.9f)
        {
            if (host == null) yield break;

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("floater");
            if (!string.IsNullOrEmpty(extraClass)) label.AddToClassList(extraClass);

            host.Add(label);

            yield return UiTween.Over(duration, Easing.OutCubic, t =>
            {
                label.style.translate = new Translate(0f, -rise * t);
                label.style.opacity = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
            });

            host.Remove(label);
        }
    }
}
