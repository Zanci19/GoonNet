using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("Advertisement")]
public class Advertisement
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string Advertiser { get; set; } = string.Empty;
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

    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);
    public DayOfWeekFlags ValidDays { get; set; } = DayOfWeekFlags.All;
    public int MaxPlaysPerDay { get; set; } = 3;
    public int PlaysToday { get; set; }
    public int TotalPlays { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 5;
    public string ContractNumber { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
}
