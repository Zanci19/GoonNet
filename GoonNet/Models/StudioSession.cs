using System;
using System.Xml.Serialization;

namespace GoonNet;

/// <summary>
/// Persists the Studio operator's working state between sessions.
/// Saved to AppData/GoonNet/studio_session.xml on exit and loaded on startup.
/// </summary>
[XmlRoot("StudioSession")]
public class StudioSession
{
    /// <summary>ID of the playlist that was loaded when the session was saved.</summary>
    public Guid? PlaylistId { get; set; }

    /// <summary>Index of the track that was cued/playing.</summary>
    public int CurrentIndex { get; set; } = -1;

    public float MainVolume { get; set; } = 0.85f;
    public float PreviewVolume { get; set; } = 0.8f;

    /// <summary>Pitch offset in semitones (-24 to +24).</summary>
    public double PitchSemiTones { get; set; } = 0.0;

    /// <summary>Tempo change in percent (-50 to +100). 0 = normal speed.</summary>
    public double TempoChange { get; set; } = 0.0;

    /// <summary>Rate change in percent (-50 to +100). 0 = normal rate (pitch+speed).</summary>
    public double RateChange { get; set; } = 0.0;

    public bool AutoPlay { get; set; } = true;
    public string TransitionSoundPath { get; set; } = string.Empty;
    public int FadeSeconds { get; set; } = 5;
    public int MicDeviceIndex { get; set; } = 0;
    public int DuckingPercent { get; set; } = 25;
}
