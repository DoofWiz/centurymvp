using System;
using System.Collections;
using Century.Core.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Century.App
{
    /// <summary>
    /// Owns scene transitions. Title, Overmap, Camp and Battle are mutually exclusive additive scenes
    /// on top of the persistent Boot scene; only one is ever loaded at a time.
    /// </summary>
    /// <remarks>
    /// Coroutine based rather than async/await so the project does not need UniTask yet. The public
    /// surface is task-shaped (start, callback on completion) so it can be moved later without
    /// touching call sites.
    /// </remarks>
    public sealed class SceneFlowService : ISceneNavigator
    {
        private readonly MonoBehaviour _coroutineHost;
        private string _activeGameplayScene;

        public bool IsTransitioning { get; private set; }

        /// <summary>The scene currently standing on top of Boot, or null before the first load.</summary>
        public string ActiveScene => _activeGameplayScene;

        public event Action<string> GameplaySceneLoaded;
        public event Action<string> GameplaySceneUnloading;

        public SceneFlowService(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
        }

        public void LoadTitle(Action onComplete = null) =>
            _coroutineHost.StartCoroutine(SwapTo(SceneNames.Title, onComplete));

        public void LoadOvermap(Action onComplete = null) =>
            _coroutineHost.StartCoroutine(SwapTo(SceneNames.Overmap, onComplete));

        public void LoadCamp(Action onComplete = null) =>
            _coroutineHost.StartCoroutine(SwapTo(SceneNames.Camp, onComplete));

        public void LoadBattle(BattleRequest request, Action onComplete = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Core.ServiceLocator.Register(request);
            _coroutineHost.StartCoroutine(SwapTo(SceneNames.Battle, onComplete));
        }

        private IEnumerator SwapTo(string sceneName, Action onComplete)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning($"[SceneFlow] Ignored request to load '{sceneName}' during a transition.");
                yield break;
            }

            IsTransitioning = true;

            if (!string.IsNullOrEmpty(_activeGameplayScene))
            {
                GameplaySceneUnloading?.Invoke(_activeGameplayScene);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_activeGameplayScene);
                while (unload != null && !unload.isDone) yield return null;

                // Views created by the unloaded scene are gone; release their managed memory before
                // the next scene allocates its own. Cheap here, painful if left to a random frame.
                AsyncOperation cleanup = Resources.UnloadUnusedAssets();
                while (!cleanup.isDone) yield return null;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[SceneFlow] Scene '{sceneName}' is not in Build Settings. Add it (File > Build Profiles).");
                IsTransitioning = false;
                yield break;
            }
            while (!load.isDone) yield return null;

            Scene loaded = SceneManager.GetSceneByName(sceneName);
            if (loaded.IsValid()) SceneManager.SetActiveScene(loaded);

            _activeGameplayScene = sceneName;
            IsTransitioning = false;

            GameplaySceneLoaded?.Invoke(sceneName);
            onComplete?.Invoke();
        }
    }
}
