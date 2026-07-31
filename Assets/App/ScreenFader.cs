using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Century.App
{
    /// <summary>
    /// The breath between places: scenes no longer pop into each other mid-frame. The screen goes
    /// dark the moment a gameplay scene starts unloading and eases back in once the next one is
    /// standing — a beat that makes overmap → battle → overmap read as travel rather than a glitch.
    /// Entirely code-built (one overlay canvas), owned by the GameDirector.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private const float FadeInSeconds = 0.7f;

        private Image _veil;
        private Coroutine _fade;

        public static ScreenFader Create(Transform parent)
        {
            var fader = new GameObject("ScreenFader").AddComponent<ScreenFader>();
            fader.transform.SetParent(parent, false);
            fader.Build();
            return fader;
        }

        private void Build()
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;   // over every UI document and every camera

            var imageGo = new GameObject("Veil");
            imageGo.transform.SetParent(canvasGo.transform, false);

            _veil = imageGo.AddComponent<Image>();
            _veil.color = new Color(0f, 0f, 0f, 0f);
            _veil.raycastTarget = false;

            RectTransform rect = _veil.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Instant dark — the old scene is leaving; there is nothing worth seeing torn down.</summary>
        public void SnapToBlack()
        {
            if (_fade != null) StopCoroutine(_fade);
            if (_veil != null) _veil.color = Color.black;
        }

        /// <summary>Ease the new scene in.</summary>
        public void FadeIn()
        {
            if (_veil == null) return;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            float t = 0f;
            while (t < FadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                _veil.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, t / FadeInSeconds));
                yield return null;
            }

            _veil.color = new Color(0f, 0f, 0f, 0f);
            _fade = null;
        }
    }
}
