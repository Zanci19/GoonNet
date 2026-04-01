using System;
using System.IO;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("MusicTrack")]
public class MusicTrack
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public TrackType Type { get; set; } = TrackType.Music;
    public string Comments { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public TrackFlag Flags { get; set; } = TrackFlag.None;
    public int Rating { get; set; } = 50;

    [XmlArray("Attributes")]
    [XmlArrayItem("Attribute")]
    public int[] Attributes { get; set; } = new int[5];

    public double BPM { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;

    [XmlIgnore]
    public TimeSpan Duration { get; set; }

    [XmlElement("Duration")]
    public string DurationXml
    {
        get => Duration.ToString();
        set => Duration = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero;
    }

    public int Bitrate { get; set; }

    [XmlIgnore]
    public TimeSpan Start { get; set; }
    [XmlElement("Start")]
    public string StartXml { get => Start.ToString(); set => Start = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan End { get; set; }
    [XmlElement("End")]
    public string EndXml { get => End.ToString(); set => End = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan FadeIn { get; set; }
    [XmlElement("FadeIn")]
    public string FadeInXml { get => FadeIn.ToString(); set => FadeIn = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan FadeOut { get; set; }
    [XmlElement("FadeOut")]
    public string FadeOutXml { get => FadeOut.ToString(); set => FadeOut = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan Intro { get; set; }
    [XmlElement("Intro")]
    public string IntroXml { get => Intro.ToString(); set => Intro = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan VoiceOut { get; set; }
    [XmlElement("VoiceOut")]
    public string VoiceOutXml { get => VoiceOut.ToString(); set => VoiceOut = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan HotStart { get; set; }
    [XmlElement("HotStart")]
    public string HotStartXml { get => HotStart.ToString(); set => HotStart = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan MixIn { get; set; }
    [XmlElement("MixIn")]
    public string MixInXml { get => MixIn.ToString(); set => MixIn = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan LoopStart { get; set; }
    [XmlElement("LoopStart")]
    public string LoopStartXml { get => LoopStart.ToString(); set => LoopStart = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan LoopEnd { get; set; }
    [XmlElement("LoopEnd")]
    public string LoopEndXml { get => LoopEnd.ToString(); set => LoopEnd = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan RefrainStart { get; set; }
    [XmlElement("RefrainStart")]
    public string RefrainStartXml { get => RefrainStart.ToString(); set => RefrainStart = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    [XmlIgnore]
    public TimeSpan RefrainEnd { get; set; }
    [XmlElement("RefrainEnd")]
    public string RefrainEndXml { get => RefrainEnd.ToString(); set => RefrainEnd = TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero; }

    public DateTime DateAdded { get; set; } = DateTime.Now;
    public DateTime? LastPlayed { get; set; }
    public int PlayCount { get; set; }

    [XmlIgnore]
    public string FullPath => Path.Combine(Location, FileName);

    [XmlIgnore]
    public bool IsArchived => (Flags & TrackFlag.Archived) != 0;
    [XmlIgnore]
    public bool IsNew => (Flags & TrackFlag.New) != 0;
    [XmlIgnore]
    public bool IsPromoted => (Flags & TrackFlag.Promoted) != 0;
    [XmlIgnore]
    public bool IsDuplicate => (Flags & TrackFlag.Duplicate) != 0;
}
