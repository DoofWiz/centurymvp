using System;

namespace Century.Core
{
    /// <summary>
    /// An immutable point in campaign time, stored as minutes elapsed since the campaign began.
    /// </summary>
    /// <remarks>
    /// The campaign day is split into eight three-hour watches, the first beginning at 06:00.
    /// That makes "Day 23 / 3rd Watch" read as the middle of the twenty-third day, which matches
    /// the daylight shown in the overmap mock-up. The split is a design choice, not a historical
    /// one (Roman vigiliae covered the night only) and lives in one place so it can be changed.
    /// </remarks>
    public readonly struct CampaignTime : IEquatable<CampaignTime>, IComparable<CampaignTime>
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;
        public const int WatchesPerDay = 8;
        public const int MinutesPerWatch = MinutesPerDay / WatchesPerDay;
        public const int FirstWatchStartHour = 6;

        public static readonly CampaignTime Zero = new CampaignTime(0d);

        public double TotalMinutes { get; }

        public CampaignTime(double totalMinutes)
        {
            TotalMinutes = totalMinutes < 0d ? 0d : totalMinutes;
        }

        public static CampaignTime FromDays(double days) => new CampaignTime(days * MinutesPerDay);
        public static CampaignTime FromHours(double hours) => new CampaignTime(hours * MinutesPerHour);

        /// <summary>Zero-based day since campaign start.</summary>
        public int DayIndex => (int)(TotalMinutes / MinutesPerDay);

        /// <summary>One-based day, as shown in the HUD.</summary>
        public int DayNumber => DayIndex + 1;

        public int MinuteOfDay => (int)(TotalMinutes - (double)DayIndex * MinutesPerDay);
        public int Hour => MinuteOfDay / MinutesPerHour;
        public int Minute => MinuteOfDay % MinutesPerHour;
        public double TotalHours => TotalMinutes / MinutesPerHour;
        public double TotalDays => TotalMinutes / MinutesPerDay;

        /// <summary>Zero-based watch within the day.</summary>
        public int WatchIndex
        {
            get
            {
                int offset = MinuteOfDay - FirstWatchStartHour * MinutesPerHour;
                if (offset < 0) offset += MinutesPerDay;
                return offset / MinutesPerWatch;
            }
        }

        public int WatchNumber => WatchIndex + 1;

        /// <summary>True between dusk and dawn. Drives lighting, detection range and camp events.</summary>
        public bool IsNight => Hour < 5 || Hour >= 21;

        /// <summary>Overmap HUD format, e.g. "Day 23 / 3rd Watch".</summary>
        public string ToWatchString() => $"Day {DayNumber} / {Ordinal(WatchNumber)} Watch";

        /// <summary>Battle HUD format, e.g. "15:42".</summary>
        public string ToClockString() => $"{Hour:00}:{Minute:00}";

        public static string Ordinal(int value)
        {
            int lastTwo = value % 100;
            if (lastTwo >= 11 && lastTwo <= 13) return value + "th";
            switch (value % 10)
            {
                case 1: return value + "st";
                case 2: return value + "nd";
                case 3: return value + "rd";
                default: return value + "th";
            }
        }

        public CampaignTime Plus(double minutes) => new CampaignTime(TotalMinutes + minutes);
        public static double operator -(CampaignTime a, CampaignTime b) => a.TotalMinutes - b.TotalMinutes;

        public bool Equals(CampaignTime other) => TotalMinutes.Equals(other.TotalMinutes);
        public override bool Equals(object obj) => obj is CampaignTime other && Equals(other);
        public override int GetHashCode() => TotalMinutes.GetHashCode();
        public int CompareTo(CampaignTime other) => TotalMinutes.CompareTo(other.TotalMinutes);
        public override string ToString() => ToWatchString() + " " + ToClockString();
    }
}
