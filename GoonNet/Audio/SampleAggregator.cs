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
    private float _maxLeft;
    private float _minLeft;
    private float _maxRight;
    private float _minRight;

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
                if (sample > _maxLeft) _maxLeft = sample;
                if (sample < _minLeft) _minLeft = sample;
                if (channels == 1)
                {
                    if (sample > _maxRight) _maxRight = sample;
                    if (sample < _minRight) _minRight = sample;
                }
                _sampleCount++;
                if (_sampleCount >= NotificationCount)
                {
                    MaximumCalculated?.Invoke(this, new MaxSampleEventArgs(_minLeft, _maxLeft, _minRight, _maxRight));
                    _maxLeft = _maxRight = 0f;
                    _minLeft = _minRight = 0f;
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
            else if (channels > 1 && n % channels == 1)
            {
                if (sample > _maxRight) _maxRight = sample;
                if (sample < _minRight) _minRight = sample;
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
    public float MinLeft { get; }
    public float MaxLeft { get; }
    public float MinRight { get; }
    public float MaxRight { get; }
    public float LeftPeak => Math.Max(Math.Abs(MinLeft), Math.Abs(MaxLeft));
    public float RightPeak => Math.Max(Math.Abs(MinRight), Math.Abs(MaxRight));

    public MaxSampleEventArgs(float minLeft, float maxLeft, float minRight, float maxRight)
    {
        MinLeft = minLeft;
        MaxLeft = maxLeft;
        MinRight = minRight;
        MaxRight = maxRight;
    }
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
