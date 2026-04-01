using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("BroadcastEvent")]
public class BroadcastEvent
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public EventType Type { get; set; } = EventType.Music;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DayOfWeekFlags ValidDays { get; set; } = DayOfWeekFlags.All;

    [XmlIgnore]
    public TimeSpan ScheduledTime { get; set; }
    [XmlElement("ScheduledTime")]
    public string ScheduledTimeXml { get => ScheduledTime.ToString(); set => ScheduledTime = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    public bool IsPreEvent { get; set; }
    public bool IsMidEvent { get; set; }
    public bool NeverRemove { get; set; }
    public Guid? LinkedItemId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public string Comments { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
