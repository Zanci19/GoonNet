using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("LogEntry")]
public class LogEntry
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public EventType EventType { get; set; } = EventType.Music;
    public string Notes { get; set; } = string.Empty;
    public string ComputerId { get; set; } = Environment.MachineName;
    public Guid? UserId { get; set; }
}
