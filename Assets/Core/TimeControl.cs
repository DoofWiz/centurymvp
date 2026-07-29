namespace Century.Core
{
    /// <summary>Overmap speed setting, mapped to the four transport buttons in the HUD.</summary>
    public enum TimeControl
    {
        Paused = 0,
        Normal = 1,
        Fast = 2,
        Fastest = 3
    }

    public static class TimeControlExtensions
    {
        public static float ToMultiplier(this TimeControl control)
        {
            switch (control)
            {
                case TimeControl.Paused: return 0f;
                case TimeControl.Normal: return 1f;
                case TimeControl.Fast: return 3f;
                case TimeControl.Fastest: return 8f;
                default: return 1f;
            }
        }
    }
}
