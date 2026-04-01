using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("Block")]
public class Block
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public bool IsTimedBlock { get; set; }

    [XmlIgnore]
    public TimeSpan? ScheduledTime { get; set; }
    [XmlElement("ScheduledTime")]
    public string? ScheduledTimeXml
    {
        get => ScheduledTime?.ToString();
        set => ScheduledTime = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }

    public Guid? PreBlockJingleId { get; set; }
    public Guid? PostBlockJingleId { get; set; }

    [XmlArray("Items")]
    [XmlArrayItem("BlockItem")]
    public List<BlockItem> Items { get; set; } = new();

    [XmlIgnore]
    public TimeSpan SoftFadeDuration { get; set; } = TimeSpan.FromSeconds(3);
    [XmlElement("SoftFadeDuration")]
    public string SoftFadeDurationXml { get => SoftFadeDuration.ToString(); set => SoftFadeDuration = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.FromSeconds(3); }

    public string Comments { get; set; } = string.Empty;
}

public class BlockItem
{
    public Guid ItemId { get; set; } = Guid.NewGuid();
    public EventType ItemType { get; set; } = EventType.Music;
    public int Order { get; set; }

    [XmlIgnore]
    public TimeSpan? Duration { get; set; }
    [XmlElement("Duration")]
    public string? DurationXml
    {
        get => Duration?.ToString();
        set => Duration = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }
}
