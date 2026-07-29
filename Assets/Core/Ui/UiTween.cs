using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Core.Ui
{
    /// <summary>
    /// Coroutine-based animation helpers for UI Toolkit elements.
    /// </summary>
    /// <remarks>
    /// Coroutines rather than USS transitions because the summary screen needs *sequencing* — this
    /// number counts up, then that bar fills, then those rows appear one by one. USS transitions can
    /// animate a property but cannot express an ordered script, and doing it in code keeps the whole
    /// reveal readable in one place.
    ///
    /// Everything here uses unscaled time so the interface still animates when the game is paused,
    /// which is exactly when the summary screen is on display.
    /// </remarks>
    public static class UiTween
    {
        /// <summary>Runs a normalised 0..1 progression over <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Over(float duration, Func<float, float> easing, Action<float> apply)
        {
            if (apply == null) yield break;

            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }

            easing = easing ?? Easing.Linear;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                apply(easing(t));
                yield return null;
            }

            apply(1f);
        }

        /// <summary>
        /// Counts a label from one value to another. The format string receives the current value, so
        /// callers control whether it reads "342" or "342 coin" or "+8 rations".
        /// </summary>
        public static IEnumerator CountUp(
            Label label, float from, float to, float duration, string format = "{0:0}")
        {
            if (label == null) yield break;

            yield return Over(duration, Easing.OutCubic, t =>
                label.text = string.Format(format, Mathf.Lerp(from, to, t)));
        }

        public static IEnumerator CountUpInt(
            Label label, int from, int to, float duration, string format = "{0}")
        {
            if (label == null) yield break;

            yield return Over(duration, Easing.OutCubic, t =>
                label.text = string.Format(format, Mathf.RoundToInt(Mathf.Lerp(from, to, t))));
        }

        /// <summary>Fills a bar element's width from one fraction to another.</summary>
        public static IEnumerator FillBar(VisualElement fill, float from01, float to01, float duration)
        {
            if (fill == null) yield break;

            yield return Over(duration, Easing.OutQuad, t =>
                fill.style.width = Length.Percent(Mathf.Lerp(from01, to01, t) * 100f));
        }

        public static IEnumerator Fade(VisualElement element, float from, float to, float duration)
        {
            if (element == null) yield break;

            element.style.opacity = from;
            yield return Over(duration, Easing.OutQuad, t => element.style.opacity = Mathf.Lerp(from, to, t));
        }

        /// <summary>Fades an element in while sliding it up slightly. The default "appear" motion.</summary>
        public static IEnumerator AppearFromBelow(
            VisualElement element, float duration = 0.28f, float riseFrom = 14f)
        {
            if (element == null) yield break;

            element.style.opacity = 0f;
            element.style.translate = new Translate(0f, riseFrom);

            yield return Over(duration, Easing.OutCubic, t =>
            {
                element.style.opacity = t;
                element.style.translate = new Translate(0f, Mathf.Lerp(riseFrom, 0f, t));
            });

            element.style.translate = new Translate(0f, 0f);
        }

        /// <summary>
        /// Reveals a set of elements one after another. This is the whole point of the summary screen:
        /// seeing six things arrive in sequence tells you what you gained, whereas seeing them all at
        /// once tells you only that something happened.
        /// </summary>
        public static IEnumerator StaggerIn(
            IList<VisualElement> elements, float perItemDelay = 0.09f, float itemDuration = 0.26f)
        {
            if (elements == null) yield break;

            for (int i = 0; i < elements.Count; i++)
            {
                VisualElement element = elements[i];
                if (element == null) continue;

                element.style.opacity = 0f;
            }

            for (int i = 0; i < elements.Count; i++)
            {
                VisualElement element = elements[i];
                if (element == null) continue;

                // Fire and forget per item so they overlap rather than queueing end to end.
                element.style.opacity = 0f;
                element.style.translate = new Translate(0f, 10f);

                float captured = itemDuration;
                VisualElement captureElement = element;

                yield return Wait(perItemDelay);

                // Drive the individual item without blocking the stagger.
                RunDetached(captureElement, captured);
            }

            yield return Wait(itemDuration);
        }

        /// <summary>
        /// Applies an appear animation using USS transitions so it can run without its own coroutine.
        /// Used by <see cref="StaggerIn"/> so items overlap.
        /// </summary>
        private static void RunDetached(VisualElement element, float duration)
        {
            element.style.transitionProperty = new List<StylePropertyName>
            {
                new StylePropertyName("opacity"),
                new StylePropertyName("translate")
            };
            element.style.transitionDuration = new List<TimeValue> { new TimeValue(duration, TimeUnit.Second) };
            element.style.transitionTimingFunction =
                new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };

            element.style.opacity = 1f;
            element.style.translate = new Translate(0f, 0f);
        }

        /// <summary>Pulses an element's scale once. For promotions and other moments worth marking.</summary>
        public static IEnumerator Punch(VisualElement element, float strength = 0.18f, float duration = 0.4f)
        {
            if (element == null) yield break;

            yield return Over(duration, Easing.OutElastic, t =>
            {
                float scale = 1f + strength * (1f - t);
                element.style.scale = new Scale(new Vector2(scale, scale));
            });

            element.style.scale = new Scale(Vector2.one);
        }

        public static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
