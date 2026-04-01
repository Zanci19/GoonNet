using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("Jingle")]
public class Jingle
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

    [XmlIgnore]
    public TimeSpan FadeIn { get; set; }
    [XmlElement("FadeIn")]
    public string FadeInXml { get => FadeIn.ToString(); set => FadeIn = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan FadeOut { get; set; }
    [XmlElement("FadeOut")]
    public string FadeOutXml { get => FadeOut.ToString(); set => FadeOut = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public int PlayCount { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;
}
