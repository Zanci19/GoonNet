using System;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GoonNet;

public sealed class AudioEngine : IDisposable
{
    private static readonly Lazy<AudioEngine> _instance = new(() => new AudioEngine());
    public static AudioEngine Instance => _instance.Value;

    private WaveOutEvent? _mainPlayer;
    private WaveOutEvent? _previewPlayer;
    private AudioFileReader? _mainReader;
    private AudioFileReader? _previewReader;
    private SoundTouchSampleProvider? _mainPitchSpeed;
    private VolumeSampleProvider? _mainVolume;
    private VolumeSampleProvider? _previewVolume;
    private System.Windows.Forms.Timer? _positionTimer;

    private float _mainVolumeLevel = 1.0f;
    private float _previewVolumeLevel = 0.8f;
    private double _pitchSemiTones = 0.0;
    private double _tempoChangePercent = 0.0;
    private double _rateChangePercent = 0.0;
    private bool _mainPaused;
    private bool _previewPaused;
    private bool _disposed;

    // Ducking state (for mic talkover)
    private float _duckSavedVolume = -1f;

    public event EventHandler<TrackEventArgs>? TrackStarted;
    public event EventHandler<TrackEventArgs>? TrackEnded;
    public event EventHandler<PositionEventArgs>? TrackPositionChanged;
    public event EventHandler<AudioErrorEventArgs>? Error;
    public event EventHandler? MainSampleAggregatorChanged;

    public MusicTrack? CurrentTrack { get; private set; }
    public MusicTrack? NextTrack { get; private set; }

    /// <summary>The current SampleAggregator inserted in the main playback pipeline. Null when stopped.</summary>
    public SampleAggregator? MainSampleAggregator { get; private set; }

    public bool IsPlaying => _mainPlayer?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _mainPaused;
    public bool IsPreviewPlaying => _previewPlayer?.PlaybackState == PlaybackState.Playing;

    public float MainVolume
    {
        get => _mainVolumeLevel;
        set
        {
            _mainVolumeLevel = Math.Clamp(value, 0f, 1f);
            if (_mainVolume != null) _mainVolume.Volume = _mainVolumeLevel;
        }
    }

    public float PreviewVolume
    {
        get => _previewVolumeLevel;
        set
        {
            _previewVolumeLevel = Math.Clamp(value, 0f, 1f);
            if (_previewVolume != null) _previewVolume.Volume = _previewVolumeLevel;
        }
    }

    public double MainVolumeDb
    {
        get => _mainVolumeLevel > 0 ? 20.0 * Math.Log10(_mainVolumeLevel) : -96.0;
        set => MainVolume = (float)Math.Pow(10.0, value / 20.0);
    }

    /// <summary>Pitch shift in semitones applied to the main output. Range -24 to +24.</summary>
    public double PitchSemiTones
    {
        get => _pitchSemiTones;
        set
        {
            _pitchSemiTones = Math.Clamp(value, -24.0, 24.0);
            if (_mainPitchSpeed != null) _mainPitchSpeed.PitchSemiTones = _pitchSemiTones;
        }
    }

    /// <summary>Tempo change in percent. 0 = normal, +50 = 50% faster without pitch change.</summary>
    public double TempoChange
    {
        get => _tempoChangePercent;
        set
        {
            _tempoChangePercent = Math.Clamp(value, -70.0, 100.0);
            if (_mainPitchSpeed != null) _mainPitchSpeed.TempoChange = _tempoChangePercent;
        }
    }

    /// <summary>Rate change in percent. 0 = normal. Changes both speed and pitch together.</summary>
    public double RateChange
    {
        get => _rateChangePercent;
        set
        {
            _rateChangePercent = Math.Clamp(value, -70.0, 100.0);
            if (_mainPitchSpeed != null) _mainPitchSpeed.RateChange = _rateChangePercent;
        }
    }

    public TimeSpan MainPosition => _mainReader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan MainDuration => _mainReader?.TotalTime ?? TimeSpan.Zero;

    private AudioEngine()
    {
        _positionTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _positionTimer.Tick += PositionTimer_Tick;
        _positionTimer.Start();
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_mainReader != null && _mainPlayer?.PlaybackState == PlaybackState.Playing)
        {
            TrackPositionChanged?.Invoke(this, new PositionEventArgs(
                _mainReader.CurrentTime, _mainReader.TotalTime));
        }
    }

    public void PlayTrack(MusicTrack track, AudioDeviceType device = AudioDeviceType.Main)
    {
        try
        {
            if (device == AudioDeviceType.Main)
            {
                StopMain();
                CurrentTrack = track;
                _mainReader = new AudioFileReader(track.FullPath);
                if (track.Start > TimeSpan.Zero && track.Start < _mainReader.TotalTime)
                    _mainReader.CurrentTime = track.Start;
                _mainPitchSpeed = new SoundTouchSampleProvider(_mainReader.ToSampleProvider())
                {
                    PitchSemiTones = _pitchSemiTones,
                    TempoChange = _tempoChangePercent,
                    RateChange = _rateChangePercent
                };
                MainSampleAggregator = new SampleAggregator(_mainPitchSpeed);
                _mainVolume = new VolumeSampleProvider(MainSampleAggregator) { Volume = _mainVolumeLevel };
                _mainPlayer = new WaveOutEvent { DesiredLatency = 200 };
                _mainPlayer.Init(_mainVolume);
                _mainPlayer.PlaybackStopped += MainPlayer_PlaybackStopped;
                _mainPlayer.Play();
                MainSampleAggregatorChanged?.Invoke(this, EventArgs.Empty);
                _mainPaused = false;
                TrackStarted?.Invoke(this, new TrackEventArgs(track, device));
            }
            else
            {
                StopPreview();
                _previewReader = new AudioFileReader(track.FullPath);
                _previewVolume = new VolumeSampleProvider(_previewReader.ToSampleProvider()) { Volume = _previewVolumeLevel };
                _previewPlayer = new WaveOutEvent { DesiredLatency = 200 };
                _previewPlayer.Init(_previewVolume);
                _previewPlayer.PlaybackStopped += PreviewPlayer_PlaybackStopped;
                _previewPlayer.Play();
                TrackStarted?.Invoke(this, new TrackEventArgs(track, device));
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new AudioErrorEventArgs(ex.Message, device));
        }
    }

    public void Stop(AudioDeviceType device = AudioDeviceType.Main)
    {
        if (device == AudioDeviceType.Main) StopMain();
        else StopPreview();
    }

    private void StopMain()
    {
        _mainPlayer?.Stop();
        _mainPlayer?.Dispose();
        _mainPlayer = null;
        _mainReader?.Dispose();
        _mainReader = null;
        _mainPitchSpeed = null;
        _mainVolume = null;
        MainSampleAggregator = null;
        _mainPaused = false;
        MainSampleAggregatorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopPreview()
    {
        _previewPlayer?.Stop();
        _previewPlayer?.Dispose();
        _previewPlayer = null;
        _previewReader?.Dispose();
        _previewReader = null;
        _previewVolume = null;
        _previewPaused = false;
    }

    public void Pause(AudioDeviceType device = AudioDeviceType.Main)
    {
        if (device == AudioDeviceType.Main && _mainPlayer?.PlaybackState == PlaybackState.Playing)
        {
            _mainPlayer.Pause();
            _mainPaused = true;
        }
        else if (device == AudioDeviceType.Preview && _previewPlayer?.PlaybackState == PlaybackState.Playing)
        {
            _previewPlayer.Pause();
            _previewPaused = true;
        }
    }

    public void Resume(AudioDeviceType device = AudioDeviceType.Main)
    {
        if (device == AudioDeviceType.Main && _mainPaused)
        {
            _mainPlayer?.Play();
            _mainPaused = false;
        }
        else if (device == AudioDeviceType.Preview && _previewPaused)
        {
            _previewPlayer?.Play();
            _previewPaused = false;
        }
    }

    public void FadeOut(AudioDeviceType device, TimeSpan duration, Action? onComplete = null)
    {
        var vol = device == AudioDeviceType.Main ? _mainVolume : _previewVolume;
        if (vol == null) return;
        var steps = (int)(duration.TotalMilliseconds / 50);
        if (steps <= 0) { Stop(device); onComplete?.Invoke(); return; }
        var startVol = vol.Volume;
        var decrement = startVol / steps;
        var stepCount = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (s, e) =>
        {
            stepCount++;
            var newVol = startVol - (decrement * stepCount);
            if (newVol <= 0 || stepCount >= steps)
            {
                timer.Stop();
                timer.Dispose();
                Stop(device);
                if (device == AudioDeviceType.Main) MainVolume = _mainVolumeLevel;
                else PreviewVolume = _previewVolumeLevel;
                onComplete?.Invoke();
            }
            else
            {
                vol.Volume = newVol;
            }
        };
        timer.Start();
    }

    public void FadeIn(AudioDeviceType device, TimeSpan duration)
    {
        var vol = device == AudioDeviceType.Main ? _mainVolume : _previewVolume;
        if (vol == null) return;
        var targetVol = device == AudioDeviceType.Main ? _mainVolumeLevel : _previewVolumeLevel;
        vol.Volume = 0f;
        var steps = (int)(duration.TotalMilliseconds / 50);
        if (steps <= 0) { vol.Volume = targetVol; return; }
        var increment = targetVol / steps;
        var stepCount = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (s, e) =>
        {
            stepCount++;
            var newVol = increment * stepCount;
            if (newVol >= targetVol || stepCount >= steps)
            {
                vol.Volume = targetVol;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                vol.Volume = newVol;
            }
        };
        timer.Start();
    }

    public void SetPosition(TimeSpan position)
    {
        if (_mainReader != null && position < _mainReader.TotalTime)
            _mainReader.CurrentTime = position;
    }

    /// <summary>Temporarily reduces main volume to <paramref name="level"/> (0–1 fraction of current volume).
    /// Call <see cref="Unduck"/> to restore.</summary>
    public void Duck(float level = 0.25f)
    {
        if (_duckSavedVolume < 0) _duckSavedVolume = _mainVolumeLevel;
        MainVolume = _duckSavedVolume * Math.Clamp(level, 0f, 1f);
    }

    /// <summary>Restores the main volume to the level it was before <see cref="Duck"/> was called.</summary>
    public void Unduck()
    {
        if (_duckSavedVolume >= 0)
        {
            _mainVolumeLevel = _duckSavedVolume;
            MainVolume = _duckSavedVolume;
            _duckSavedVolume = -1f;
        }
    }

    public void QueueNext(MusicTrack track)
    {
        NextTrack = track;
    }

    private void MainPlayer_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        var track = CurrentTrack;
        CurrentTrack = null;
        if (track != null)
            TrackEnded?.Invoke(this, new TrackEventArgs(track, AudioDeviceType.Main));
        if (NextTrack != null)
        {
            var next = NextTrack;
            NextTrack = null;
            PlayTrack(next, AudioDeviceType.Main);
        }
    }

    private void PreviewPlayer_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        TrackEnded?.Invoke(this, new TrackEventArgs(null, AudioDeviceType.Preview));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        StopMain();
        StopPreview();
    }
}

public class TrackEventArgs : EventArgs
{
    public MusicTrack? Track { get; }
    public AudioDeviceType Device { get; }
    public TrackEventArgs(MusicTrack? track, AudioDeviceType device) { Track = track; Device = device; }
}

public class PositionEventArgs : EventArgs
{
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }
    public double Fraction => Duration.TotalSeconds > 0 ? Position.TotalSeconds / Duration.TotalSeconds : 0;
    public PositionEventArgs(TimeSpan position, TimeSpan duration) { Position = position; Duration = duration; }
}

public class AudioErrorEventArgs : EventArgs
{
    public string Message { get; }
    public AudioDeviceType Device { get; }
    public AudioErrorEventArgs(string message, AudioDeviceType device) { Message = message; Device = device; }
}
