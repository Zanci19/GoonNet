using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("PlaylistSequence")]
public class PlaylistSequence
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    [XmlArray("Items")]
    [XmlArrayItem("SequenceItem")]
    public List<SequenceItem> Items { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Daily;
}

public class SequenceItem
{
    public Guid PlaylistId { get; set; }
    public int Order { get; set; }
    public DateTime? ScheduledDate { get; set; }

    [XmlIgnore]
    public TimeSpan? ScheduledTime { get; set; }
    [XmlElement("ScheduledTime")]
    public string? ScheduledTimeXml
    {
        get => ScheduledTime?.ToString();
        set => ScheduledTime = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }
}
