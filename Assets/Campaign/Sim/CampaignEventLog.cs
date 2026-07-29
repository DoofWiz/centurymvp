using System;
using System.Collections.Generic;

namespace Century.Campaign.Sim
{
    public enum CampaignEventKind
    {
        Discovery = 0,
        Supply = 1,
        Threat = 2,
        Loss = 3,
        Gain = 4
    }

    public readonly struct CampaignEvent
    {
        public readonly CampaignEventKind Kind;
        public readonly string Headline;
        public readonly string Detail;
        public readonly int DayNumber;

        public CampaignEvent(CampaignEventKind kind, string headline, string detail, int dayNumber)
        {
            Kind = kind;
            Headline = headline;
            Detail = detail;
            DayNumber = dayNumber;
        }
    }

    /// <summary>
    /// Rolling log of notable campaign events, feeding the Recent Events panel.
    /// </summary>
    /// <remarks>
    /// Lives in the campaign layer rather than the HUD so that events survive the trip into a battle
    /// and back. A log owned by a scene-local UI component would be wiped by every fight, which is
    /// precisely when the most interesting entries are generated.
    /// </remarks>
    public sealed class CampaignEventLog
    {
        private readonly List<CampaignEvent> _events = new List<CampaignEvent>();
        private readonly int _capacity;

        public IReadOnlyList<CampaignEvent> Events => _events;

        /// <summary>Raised on every addition so the HUD can rebuild without polling.</summary>
        public event Action<CampaignEvent> Added;

        public CampaignEventLog(int capacity = 24)
        {
            _capacity = capacity < 4 ? 4 : capacity;
        }

        public void Push(CampaignEventKind kind, string headline, string detail, int dayNumber)
        {
            var entry = new CampaignEvent(kind, headline, detail, dayNumber);

            _events.Add(entry);
            if (_events.Count > _capacity) _events.RemoveAt(0);

            Added?.Invoke(entry);
        }

        /// <summary>Most recent entries first, capped at <paramref name="count"/>.</summary>
        public IEnumerable<CampaignEvent> MostRecent(int count)
        {
            int start = _events.Count - 1;
            int emitted = 0;

            for (int i = start; i >= 0 && emitted < count; i--, emitted++)
                yield return _events[i];
        }
    }
}
