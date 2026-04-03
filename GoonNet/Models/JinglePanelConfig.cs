using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GoonNet;

public enum JinglePlayMode
{
    MixOver,    // Play over current track without stopping it
    Abort,      // Stop current track then play jingle
    Sequential  // Queue jingle to play after current track ends
}

public enum JingleButtonActionType
{
    Jingle,
    File,
    TimeAnnouncement,
    ExternalCommand,
    RDS
}

[XmlRoot("JingleButtonConfig")]
public class JingleButtonConfig
{
    public string Label { get; set; } = string.Empty;

    [XmlArray("JingleIds")]
    [XmlArrayItem("Id")]
    public List<Guid> JingleIds { get; set; } = new();

    public JinglePlayMode PlayMode { get; set; } = JinglePlayMode.MixOver;
    public JingleButtonActionType ActionType { get; set; } = JingleButtonActionType.Jingle;

    /// <summary>Path used when ActionType is File.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Command used when ActionType is ExternalCommand.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>RDS text used when ActionType is RDS.</summary>
    public string RdsText { get; set; } = string.Empty;

    /// <summary>Background colour for the button (ARGB hex).</summary>
    public string ColorHex { get; set; } = "#E0E0E0";

    public bool IsActive { get; set; } = true;

    /// <summary>Index into JingleIds of the next jingle to play (rotates through the list).</summary>
    [XmlIgnore]
    public int NextJingleIndex { get; set; }
}

[XmlRoot("JingleGroupConfig")]
public class JingleGroupConfig
{
    public string Name { get; set; } = "Group";

    [XmlArray("Buttons")]
    [XmlArrayItem("Button")]
    public List<JingleButtonConfig> Buttons { get; set; } = new();

    public JingleGroupConfig()
    {
        for (int i = 0; i < 12; i++)
            Buttons.Add(new JingleButtonConfig { Label = $"Jingle {i + 1}" });
    }
}

[XmlRoot("JinglePanelConfig")]
public class JinglePanelConfig
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Index 0 is the fixed group; indices 1+ are interchangeable.</summary>
    [XmlArray("Groups")]
    [XmlArrayItem("Group")]
    public List<JingleGroupConfig> Groups { get; set; } = new();

    /// <summary>Which of the interchangeable groups (index ≥1) is currently shown.</summary>
    public int ActiveGroupIndex { get; set; } = 1;

    public JinglePanelConfig()
    {
        // Fixed group
        var fixed1 = new JingleGroupConfig { Name = "Fixed" };
        Groups.Add(fixed1);
        // 5 interchangeable groups
        for (int g = 1; g <= 5; g++)
            Groups.Add(new JingleGroupConfig { Name = $"Panel {g}" });
    }
}
