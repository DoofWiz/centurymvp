using System;

namespace Century.Core
{
    /// <summary>
    /// Authoritative campaign clock. Everything time-driven in the campaign layer subscribes here
    /// rather than reading Unity's <c>Time</c>, so the simulation can be stepped, fast-forwarded,
    /// paused or replayed from a save without any of its consumers knowing the difference.
    /// </summary>
    public sealed class CampaignClock
    {
        /// <summary>Safety valve so a very large single advance cannot lock the main thread.</summary>
        private const int MaxBoundaryEventsPerAdvance = 512;

        public CampaignTime Now { get; private set; }

        /// <summary>Raised once per <see cref="Advance"/> call, after the clock has settled.</summary>
        public event Action<CampaignTime> Ticked;

        /// <summary>Raised on each whole hour crossed. Supply consumption hangs off this.</summary>
        public event Action<CampaignTime> HourElapsed;

        public event Action<CampaignTime> WatchChanged;
        public event Action<CampaignTime> DayChanged;

        public CampaignClock(CampaignTime start = default)
        {
            Now = start;
        }

        public void Advance(double minutes)
        {
            if (minutes <= 0d) return;

            double target = Now.TotalMinutes + minutes;

            int guard = 0;
            while (guard++ < MaxBoundaryEventsPerAdvance)
            {
                double nextHourBoundary =
                    Math.Floor(Now.TotalMinutes / CampaignTime.MinutesPerHour + 1d) * CampaignTime.MinutesPerHour;

                if (nextHourBoundary > target) break;
                SetTime(new CampaignTime(nextHourBoundary));
            }

            SetTime(new CampaignTime(target));
            Ticked?.Invoke(Now);
        }

        private void SetTime(CampaignTime next)
        {
            CampaignTime previous = Now;
            Now = next;

            bool hourChanged = next.Hour != previous.Hour || next.DayIndex != previous.DayIndex;
            if (hourChanged) HourElapsed?.Invoke(next);
            if (next.WatchIndex != previous.WatchIndex) WatchChanged?.Invoke(next);
            if (next.DayIndex != previous.DayIndex) DayChanged?.Invoke(next);
        }
    }
}
