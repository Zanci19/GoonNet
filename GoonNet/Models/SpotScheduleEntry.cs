using System;
using System.Xml.Serialization;

namespace GoonNet;

/// <summary>One cell in the media plan: a scheduled spot slot for a given ad on a given hour/day.</summary>
[XmlRoot("SpotScheduleEntry")]
public class SpotScheduleEntry
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The advertisement to air.</summary>
    public Guid AdId { get; set; }

    /// <summary>Ad title cache so the media plan can display without joining.</summary>
    public string AdTitle { get; set; } = string.Empty;
    public string Advertiser { get; set; } = string.Empty;

    /// <summary>0=Sunday … 6=Saturday (DayOfWeek order).</summary>
    public int DayOfWeek { get; set; }

    public int Hour { get; set; }
    public int Minute { get; set; }

    public int DesiredPlays { get; set; } = 1;
    public int MaxPlays { get; set; } = 1;

    public bool IsFixed { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid EconomicCategoryId { get; set; }
    public string Notes { get; set; } = string.Empty;
}
