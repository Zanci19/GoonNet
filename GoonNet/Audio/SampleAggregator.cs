using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace GoonNet;

/// <summary>
/// ISampleProvider wrapper that captures samples for real-time FFT spectrum analysis,
/// VU meter peak detection, and audio streaming.
/// </summary>
public class SampleAggregator : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _fftLength;
    private readonly int _m; // log2(fftLength)
    private readonly Complex[] _fftBuffer;
    private readonly float[] _hannWindow;
    private int _fftPos;

    private int _sampleCount;
    private float _maxValue;
    private float _minValue;

    private readonly float[] _streamBuffer;
    private int _streamPos;

    /// <summary>Number of left-channel samples between VU meter notifications (~30 fps at 44100 Hz).</summary>
    public int NotificationCount { get; set; } = 1470;

    public event EventHandler<FftEventArgs>? FftDataAvailable;
    public event EventHandler<MaxSampleEventArgs>? MaximumCalculated;
    public event EventHandler<StreamSamplesEventArgs>? StreamSamplesAvailable;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public SampleAggregator(ISampleProvider source, int fftLength = 2048)
    {
        if ((fftLength & (fftLength - 1)) != 0)
            throw new ArgumentException("FFT length must be a power of 2", nameof(fftLength));

        _source = source;
        _fftLength = fftLength;
        _m = (int)Math.Log(fftLength, 2.0);
        _fftBuffer = new Complex[fftLength];
        _hannWindow = CreateHannWindow(fftLength);
        // ~93 ms stream buffer at 44100 Hz stereo
        _streamBuffer = new float[4096 * Math.Max(1, source.WaveFormat.Channels)];
    }

    private static float[] CreateHannWindow(int size)
    {
        var w = new float[size];
        for (int i = 0; i < size; i++)
            w[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (size - 1))));
        return w;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        int channels = WaveFormat.Channels;

        for (int n = 0; n < samplesRead; n++)
        {
            float sample = buffer[offset + n];

            // Process left channel for FFT and VU meter
            if (n % channels == 0)
            {
                // VU peak tracking
                if (sample > _maxValue) _maxValue = sample;
                if (sample < _minValue) _minValue = sample;
                _sampleCount++;
                if (_sampleCount >= NotificationCount)
                {
                    MaximumCalculated?.Invoke(this, new MaxSampleEventArgs(_minValue, _maxValue));
                    _maxValue = 0f;
                    _minValue = 0f;
                    _sampleCount = 0;
                }

                // FFT accumulation with Hann window
                _fftBuffer[_fftPos].X = sample * _hannWindow[_fftPos];
                _fftBuffer[_fftPos].Y = 0;
                _fftPos++;
                if (_fftPos >= _fftLength)
                {
                    _fftPos = 0;
                    var copy = new Complex[_fftLength];
                    Array.Copy(_fftBuffer, copy, _fftLength);
                    FastFourierTransform.FFT(true, _m, copy);
                    FftDataAvailable?.Invoke(this, new FftEventArgs(copy));
                }
            }

            // Accumulate all channels for streaming
            _streamBuffer[_streamPos++] = sample;
            if (_streamPos >= _streamBuffer.Length)
            {
                StreamSamplesAvailable?.Invoke(this, new StreamSamplesEventArgs(_streamBuffer, _streamPos, WaveFormat));
                _streamPos = 0;
            }
        }
        return samplesRead;
    }
}

public class FftEventArgs : EventArgs
{
    public Complex[] Result { get; }
    public FftEventArgs(Complex[] result) { Result = result; }
}

public class MaxSampleEventArgs : EventArgs
{
    public float MinValue { get; }
    public float MaxValue { get; }
    public MaxSampleEventArgs(float minValue, float maxValue) { MinValue = minValue; MaxValue = maxValue; }
}

public class StreamSamplesEventArgs : EventArgs
{
    public float[] Samples { get; }
    public int Count { get; }
    public WaveFormat WaveFormat { get; }

    public StreamSamplesEventArgs(float[] samples, int count, WaveFormat format)
    {
        Samples = samples;
        Count = count;
        WaveFormat = format;
    }
}
