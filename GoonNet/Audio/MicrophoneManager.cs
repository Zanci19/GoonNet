using System;
using NAudio.Wave;

namespace GoonNet;

/// <summary>
/// Captures microphone input and injects it into the live stream for announcements / talkover.
/// Automatically ducks the main audio output while the microphone is active.
/// </summary>
public sealed class MicrophoneManager : IDisposable
{
    private static readonly Lazy<MicrophoneManager> _instance = new(() => new MicrophoneManager());
    public static MicrophoneManager Instance => _instance.Value;

    private WaveInEvent? _waveIn;
    private bool _disposed;

    // ── Settings ──────────────────────────────────────────────────────────────
    /// <summary>WaveIn device number (0 = default microphone).</summary>
    public int DeviceNumber { get; set; } = 0;

    /// <summary>Fraction (0–1) to reduce main audio volume to while mic is active.</summary>
    public float DuckLevel { get; set; } = 0.25f;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsActive { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<float>? LevelChanged; // fires with RMS level 0–1

    // ── Device enumeration ────────────────────────────────────────────────────
    public static int DeviceCount => WaveInEvent.DeviceCount;
    public static string GetDeviceName(int deviceNumber)
    {
        if (deviceNumber < 0 || deviceNumber >= WaveInEvent.DeviceCount) return "(unknown)";
        return WaveInEvent.GetCapabilities(deviceNumber).ProductName;
    }

    private MicrophoneManager() { }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts capturing from the microphone and injecting samples into the stream.
    /// Also ducks the main audio playback.
    /// </summary>
    public void StartTalkover()
    {
        if (IsActive) return;

        // Use mono 44100 to match common audio format; StreamManager will handle encoding
        var fmt = new WaveFormat(44100, 1);
        _waveIn = new WaveInEvent
        {
            DeviceNumber = DeviceNumber,
            WaveFormat = fmt,
            BufferMilliseconds = 50
        };
        _waveIn.DataAvailable += OnDataAvailable;
        try
        {
            _waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            _waveIn.Dispose();
            _waveIn = null;
            throw new InvalidOperationException($"Could not open microphone (device {DeviceNumber}): {ex.Message}", ex);
        }

        IsActive = true;
        AudioEngine.Instance.Duck(DuckLevel);
    }

    /// <summary>Stops microphone capture and restores the main audio volume.</summary>
    public void StopTalkover()
    {
        if (!IsActive) return;

        IsActive = false;
        try { _waveIn?.StopRecording(); } catch { }
        _waveIn?.Dispose();
        _waveIn = null;
        AudioEngine.Instance.Unduck();
        LevelChanged?.Invoke(this, 0f);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!IsActive || e.BytesRecorded == 0) return;

        int sampleCount = e.BytesRecorded / 2; // 16-bit PCM → 2 bytes per sample
        var floats = new float[sampleCount];
        double sumSq = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            // Little-endian PCM-16: low byte first, high byte second
            short pcm = BitConverter.ToInt16(e.Buffer, i * 2);
            float f = pcm / 32768f;
            floats[i] = f;
            sumSq += f * f;
        }

        // Inject into stream
        if (_waveIn != null)
            StreamManager.Instance.InjectMicSamples(floats, sampleCount, _waveIn.WaveFormat);

        // Report RMS level for VU metering
        float rms = (float)Math.Sqrt(sumSq / sampleCount);
        LevelChanged?.Invoke(this, Math.Clamp(rms, 0f, 1f));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTalkover();
    }
}
