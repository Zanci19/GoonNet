using System;
using NAudio.Wave;
using SoundTouch;

namespace GoonNet;

/// <summary>
/// NAudio ISampleProvider that applies real-time pitch, tempo and rate adjustments
/// using the SoundTouch DSP library.
///
/// Pitch:       change in semitones (0 = no change, positive = higher, negative = lower).
/// TempoChange: tempo speed change in percent (0 = normal, +50 = 50% faster, same pitch).
/// RateChange:  playback rate change in percent (0 = normal, like tape-speed: affects both
///              pitch and tempo proportionally).
/// </summary>
public sealed class SoundTouchSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SoundTouchProcessor _processor;
    private readonly float[] _readBuffer;

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Pitch shift in semitones. Range: -24 to +24.</summary>
    public double PitchSemiTones
    {
        get => _processor.PitchSemiTones;
        set => _processor.PitchSemiTones = Math.Clamp(value, -24.0, 24.0);
    }

    /// <summary>Tempo change in percent. 0 = normal, +50 = 50% faster (pitch unchanged).</summary>
    public double TempoChange
    {
        get => _processor.TempoChange;
        set => _processor.TempoChange = Math.Clamp(value, -50.0, 100.0);
    }

    /// <summary>Rate change in percent. 0 = normal. Changes both speed and pitch together.</summary>
    public double RateChange
    {
        get => _processor.RateChange;
        set => _processor.RateChange = Math.Clamp(value, -50.0, 100.0);
    }

    public SoundTouchSampleProvider(ISampleProvider source)
    {
        _source = source;
        _processor = new SoundTouchProcessor();
        _processor.SampleRate = source.WaveFormat.SampleRate;
        _processor.Channels = source.WaveFormat.Channels;

        // ~200 ms read buffer
        _readBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels / 5];
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        int framesNeeded = count / channels;

        // Keep feeding SoundTouch until it has enough output ready
        while (_processor.AvailableSamples < framesNeeded)
        {
            int read = _source.Read(_readBuffer, 0, _readBuffer.Length);
            if (read == 0) break;
            int frames = read / channels;
            var inputSpan = new ReadOnlySpan<float>(_readBuffer, 0, read);
            _processor.PutSamples(in inputSpan, frames);
        }

        int available = _processor.AvailableSamples;
        int outputFrames = Math.Min(available, framesNeeded);
        if (outputFrames == 0) return 0;

        var outputSpan = new Span<float>(buffer, offset, outputFrames * channels);
        int received = _processor.ReceiveSamples(in outputSpan, outputFrames);
        return received * channels;
    }
}
