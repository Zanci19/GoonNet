using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("Playlist")]
public class Playlist
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool IsAutomatic { get; set; }
    public PlaylistAlgorithm Algorithm { get; set; } = PlaylistAlgorithm.Sequential;

    [XmlArray("Items")]
    [XmlArrayItem("PlaylistItem")]
    public List<PlaylistItem> Items { get; set; } = new();

    [XmlIgnore]
    public TimeSpan TotalDuration => Items.Aggregate(TimeSpan.Zero, (acc, i) => acc + (i.Duration ?? TimeSpan.Zero));

    [XmlIgnore]
    public TimeSpan? TargetDuration { get; set; }
    [XmlElement("TargetDuration")]
    public string? TargetDurationXml
    {
        get => TargetDuration?.ToString();
        set => TargetDuration = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }

    public bool IsLocked { get; set; }
}

public class PlaylistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrackId { get; set; }
    public TrackType TrackType { get; set; } = TrackType.Music;
    public int Order { get; set; }

    [XmlIgnore]
    public TimeSpan? ScheduledTime { get; set; }
    [XmlElement("ScheduledTime")]
    public string? ScheduledTimeXml
    {
        get => ScheduledTime?.ToString();
        set => ScheduledTime = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }

    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public bool IsPlayed { get; set; }
    public string Notes { get; set; } = string.Empty;

    [XmlIgnore]
    public TimeSpan? Duration { get; set; }
    [XmlElement("Duration")]
    public string? DurationXml
    {
        get => Duration?.ToString();
        set => Duration = value != null && TimeSpan.TryParse(value, out var ts) ? ts : (TimeSpan?)null;
    }

    // Denormalized display fields
    [XmlIgnore]
    public string Artist { get; set; } = string.Empty;
    [XmlIgnore]
    public string Title { get; set; } = string.Empty;
}
