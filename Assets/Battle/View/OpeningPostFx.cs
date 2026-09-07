using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Century.Battle.View
{
    /// <summary>
    /// The eye coming into focus: a global URP volume built in code, depth of field at full blur
    /// eased out to nothing over a few seconds, under a soft vignette that stays for the sequence.
    /// Post-processing is switched on for the camera here and only here; nothing else in the game
    /// uses it, and the volume dies with the opening.
    /// </summary>
    public sealed class OpeningPostFx : MonoBehaviour
    {
        private Volume _volume;
        private DepthOfField _dof;
        private Vignette _vignette;

        public static OpeningPostFx Create(Transform parent)
        {
            var fx = new GameObject("OpeningPostFx").AddComponent<OpeningPostFx>();
            fx.transform.SetParent(parent, false);
            fx.Build();
            return fx;
        }

        private void Build()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Opening (runtime)";

            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(0.5f);
            _dof.gaussianEnd.Override(2.5f);
            _dof.gaussianMaxRadius.Override(1.5f);
            _dof.highQualitySampling.Override(true);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.42f);
            _vignette.smoothness.Override(0.6f);
            _vignette.color.Override(Color.black);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.sharedProfile = profile;
        }

        /// <summary>Blur to sharp over <paramref name="seconds"/> of real time (the clock may be held).</summary>
        public void BeginBlurToFocus(float seconds) => StartCoroutine(BlurToFocus(seconds));

        private IEnumerator BlurToFocus(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float eased = 1f - Mathf.Pow(1f - k, 2.2f);

                _dof.gaussianStart.Override(Mathf.Lerp(0.5f, 260f, eased));
                _dof.gaussianEnd.Override(Mathf.Lerp(2.5f, 420f, eased));
                _dof.gaussianMaxRadius.Override(Mathf.Lerp(1.5f, 0.2f, eased));
                _vignette.intensity.Override(Mathf.Lerp(0.42f, 0.28f, eased));
                yield return null;
            }

            _dof.active = false;
        }

        private void OnDestroy()
        {
            if (_volume != null && _volume.sharedProfile != null) Destroy(_volume.sharedProfile);
        }
    }
}
