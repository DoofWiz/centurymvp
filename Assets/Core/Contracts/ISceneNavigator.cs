using System;

namespace Century.Core.Contracts
{
    /// <summary>
    /// The campaign layer's view onto scene flow. Lets scene-local UI in Century.Campaign ask to
    /// change scenes without a reference back up to Century.App, where the concrete
    /// <c>SceneFlowService</c> lives.
    /// </summary>
    /// <remarks>
    /// Same trick as <see cref="IBattleResultSink"/>: the App layer registers the implementation on
    /// boot, and a scene fetches it from the service locator. This is what keeps the assembly graph
    /// pointing one way — Campaign depends on Core, never on App.
    /// </remarks>
    public interface ISceneNavigator
    {
        void LoadOvermap(Action onComplete = null);
        void LoadCamp(Action onComplete = null);
    }
}
