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

        /// <summary>Back to the title screen. The running campaign is closed by the App layer.</summary>
        void LoadTitle(Action onComplete = null);

        /// <summary>Loads (or reloads) the battle scene for a request. The opening sequence uses it
        /// to restart itself when the player dies; the campaign uses it through the encounter path.</summary>
        void LoadBattle(BattleRequest request, Action onComplete = null);
    }
}
