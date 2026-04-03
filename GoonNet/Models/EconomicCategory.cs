using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("EconomicCategory")]
public class EconomicCategory
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>ARGB hex color used to highlight this category in the UI.</summary>
    public string ColorHex { get; set; } = "#FFFFFF";

    public decimal RatePerSecond { get; set; }
    public decimal RatePerSpot { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = string.Empty;
}
