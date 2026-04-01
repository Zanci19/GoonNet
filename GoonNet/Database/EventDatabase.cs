using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class EventDatabase : DatabaseBase<BroadcastEvent>
{
    protected override Guid GetId(BroadcastEvent item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<BroadcastEvent>), new XmlRootAttribute("EventSchedule"));

    public IEnumerable<BroadcastEvent> GetEventsForTime(TimeSpan time, DayOfWeekFlags day)
    {
        return _items.Where(e =>
            e.IsActive &&
            (e.ValidDays & day) != 0 &&
            Math.Abs((e.ScheduledTime - time).TotalMinutes) <= 1);
    }

    public IEnumerable<BroadcastEvent> GetActive() => _items.Where(e => e.IsActive);

    public IEnumerable<BroadcastEvent> GetUpcoming(TimeSpan from, TimeSpan to)
        => _items.Where(e => e.IsActive && e.ScheduledTime >= from && e.ScheduledTime <= to)
                 .OrderBy(e => e.ScheduledTime);
}
