using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("Background")]
public class Background
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;

    [XmlIgnore]
    public TimeSpan Duration { get; set; }
    [XmlElement("Duration")]
    public string DurationXml { get => Duration.ToString(); set => Duration = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    public string Category { get; set; } = string.Empty;
    public double InitialVolume { get; set; } = 50.0;
    public double Decay { get; set; } = 1.0;
    public double Sustain { get; set; } = 50.0;
    public double Release { get; set; } = 1.0;
    public bool IsLooping { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
