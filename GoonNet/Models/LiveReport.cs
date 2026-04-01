using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("LiveReport")]
public class LiveReport
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime? ScheduledTime { get; set; }
    public Guid? JingleId { get; set; }
    public Guid? BackgroundId { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime DetectedDate { get; set; } = DateTime.Now;

    [XmlIgnore]
    public TimeSpan Duration { get; set; }
    [XmlElement("Duration")]
    public string DurationXml { get => Duration.ToString(); set => Duration = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    public bool IsActive { get; set; } = true;
}
