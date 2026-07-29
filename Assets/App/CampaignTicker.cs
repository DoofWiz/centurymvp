using Century.Campaign.Model;
using Century.Campaign.Sim;
using Century.Campaign.View;
using Century.Core;
using UnityEngine;

namespace Century.App
{
    /// <summary>
    /// The single place where real time is converted into campaign time. Nothing else in the
    /// campaign layer is allowed to read <c>Time.deltaTime</c> for simulation purposes.
    /// </summary>
    public sealed class CampaignTicker : MonoBehaviour, ITimeControlSource
    {
        private CampaignClock _clock;
        private CampaignSettings _settings;

        public TimeControl Current { get; private set; } = TimeControl.Normal;
        public float CurrentMultiplier => Current.ToMultiplier();

        /// <summary>False while a battle is loaded or the game is in a menu.</summary>
        public bool IsRunning { get; set; }

        public void Initialise(CampaignClock clock, CampaignSettings settings)
        {
            _clock = clock;
            _settings = settings;
            ServiceLocator.Register<ITimeControlSource>(this);
        }

        public void SetTimeControl(TimeControl control) => Current = control;

        public void TogglePause() =>
            Current = Current == TimeControl.Paused ? TimeControl.Normal : TimeControl.Paused;

        private void Update()
        {
            ReadHotkeys();

            if (!IsRunning || _clock == null || _settings == null) return;

            float multiplier = CurrentMultiplier;
            if (multiplier <= 0f) return;

            _clock.Advance(Time.deltaTime * _settings.MinutesPerRealSecond * multiplier);
        }

        private void ReadHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeControl(TimeControl.Paused);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeControl(TimeControl.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeControl(TimeControl.Fast);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetTimeControl(TimeControl.Fastest);
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        }
    }
}
