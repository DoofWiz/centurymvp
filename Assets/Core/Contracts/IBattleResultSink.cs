namespace Century.Core.Contracts
{
    /// <summary>
    /// The return path from a battle. The battle assembly resolves a fight and submits the result
    /// here without knowing who is listening or what they will do with it.
    /// </summary>
    /// <remarks>
    /// This is what lets Century.Battle reference only Century.Core. The App layer registers an
    /// implementation on boot; the battle scene fetches it from the service locator and submits once.
    /// </remarks>
    public interface IBattleResultSink
    {
        void Submit(BattleResult result);
    }
}
