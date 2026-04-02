using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NAudio.Lame;
using NAudio.Wave;

namespace GoonNet;

/// <summary>
/// Singleton HTTP audio streaming server. Encodes the live audio feed to MP3 and serves it
/// to any HTTP client (browser, VLC, foobar2000, etc.) at http://host:port/stream.
/// Auto-connects to AudioEngine via SampleAggregator.
/// </summary>
public sealed class StreamManager : IDisposable
{
    private static readonly Lazy<StreamManager> _instance = new(() => new StreamManager());
    public static StreamManager Instance => _instance.Value;

    // ── Settings ──────────────────────────────────────────────────────────────
    public int Port { get; set; } = 8000;
    public int BitRate { get; set; } = 128;
    public string StationName { get; set; } = "GoonNet Radio";

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsStreaming { get; private set; }
    public int ListenerCount => _broadcast.ClientCount;
    /// <summary>Whether the stream is bound to all interfaces (true) or only localhost (false).</summary>
    public bool IsNetworkWide { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<StreamClientEventArgs>? ClientConnected;
    public event EventHandler<StreamClientEventArgs>? ClientDisconnected;
    public event EventHandler<string>? StatusChanged;

    // ── Internal ──────────────────────────────────────────────────────────────
    private HttpListener? _listener;
    private Thread? _listenThread;
    private Thread? _encodeThread;
    private CancellationTokenSource _cts = new();
    private readonly BroadcastBuffer _broadcast = new();
    private LameMP3FileWriter? _encoder;
    private WaveFormat? _encoderFormat;
    private readonly BlockingCollection<(float[] samples, int count, WaveFormat format)> _queue
        = new(boundedCapacity: 160);
    private SampleAggregator? _currentAggregator;
    private bool _disposed;

    // Silence generation: keep encoder alive even when no audio is playing
    private static readonly WaveFormat _defaultFormat = new WaveFormat(44100, 2);
    private WaveFormat? _lastSeenFormat;

    private StreamManager()
    {
        AudioEngine.Instance.MainSampleAggregatorChanged += OnSampleAggregatorChanged;
        // Attach immediately in case playback was already initialized before StreamManager was created
        OnSampleAggregatorChanged(this, EventArgs.Empty);
    }

    // ── AudioEngine hookup ────────────────────────────────────────────────────

    private void OnSampleAggregatorChanged(object? sender, EventArgs e)
    {
        if (_currentAggregator != null)
            _currentAggregator.StreamSamplesAvailable -= OnStreamSamples;

        _currentAggregator = AudioEngine.Instance.MainSampleAggregator;

        if (_currentAggregator != null)
            _currentAggregator.StreamSamplesAvailable += OnStreamSamples;
    }

    private void OnStreamSamples(object? sender, StreamSamplesEventArgs e)
    {
        if (!IsStreaming) return;
        _lastSeenFormat = e.WaveFormat;
        if (_broadcast.ClientCount == 0) return;
        var copy = new float[e.Count];
        Array.Copy(e.Samples, copy, e.Count);
        _queue.TryAdd((copy, e.Count, e.WaveFormat));
    }

    /// <summary>Injects microphone samples directly into the stream (for live announce/talkover).</summary>
    public void InjectMicSamples(float[] samples, int count, WaveFormat format)
    {
        if (!IsStreaming || _broadcast.ClientCount == 0) return;
        var normalized = NormalizeToPreferredFormat(samples, count, format);
        if (normalized == null) return;

        var (normalizedSamples, normalizedCount, normalizedFormat) = normalized.Value;
        _lastSeenFormat = normalizedFormat;
        var copy = new float[normalizedCount];
        Array.Copy(normalizedSamples, copy, normalizedCount);
        _queue.TryAdd((copy, normalizedCount, normalizedFormat));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Start()
    {
        if (IsStreaming) return;
        _cts = new CancellationTokenSource();

        // Try binding to all interfaces first (allows LAN access); then explicit local IP;
        // finally fall back to localhost-only.
        // Note: http://+ may require admin rights on Windows without prior URL reservation.
        bool networkWide = false;
        HttpListener? listener = null;
        string bindMode = "localhost";
        string localIp = GetLocalIpAddress();
        try
        {
            var hl = new HttpListener();
            hl.Prefixes.Add($"http://+:{Port}/");
            hl.Start();
            listener = hl;
            networkWide = true;
            bindMode = "+";
        }
        catch
        {
            try
            {
                if (localIp == "localhost")
                    throw new InvalidOperationException("Could not determine a LAN IPv4 address.");

                var hl = new HttpListener();
                hl.Prefixes.Add($"http://{localIp}:{Port}/");
                hl.Start();
                listener = hl;
                networkWide = true;
                bindMode = "local-ip";
            }
            catch
            {
                try
                {
                    var hl = new HttpListener();
                    hl.Prefixes.Add($"http://localhost:{Port}/");
                    hl.Start();
                    listener = hl;
                    bindMode = "localhost";
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Failed to start stream: {ex.Message}");
                    return;
                }
            }
        }

        _listener = listener;
        IsNetworkWide = networkWide;
        IsStreaming = true;

        _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "StreamListen" };
        _listenThread.Start();

        _encodeThread = new Thread(EncodeLoop) { IsBackground = true, Name = "StreamEncode" };
        _encodeThread.Start();

        if (bindMode == "localhost")
        {
            StatusChanged?.Invoke(this, $"Streaming on http://localhost:{Port}/stream (localhost only)");
            StatusChanged?.Invoke(this,
                $"For LAN access on Windows, run once as admin: netsh http add urlacl url=http://+:{Port}/ user=Everyone");
        }
        else
        {
            StatusChanged?.Invoke(this, $"Streaming on http://{localIp}:{Port}/stream  (also http://localhost:{Port}/stream)");
        }
    }

    public void Stop()
    {
        if (!IsStreaming) return;
        IsStreaming = false;
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
        _listener = null;
        _broadcast.CloseAll();
        _encoder?.Dispose();
        _encoder = null;
        _encoderFormat = null;
        while (_queue.TryTake(out _)) { }
        StatusChanged?.Invoke(this, "Stream stopped");
    }

    /// <summary>
    /// Normalizes microphone/input samples to match the preferred stream format and avoid
    /// encoder reinitialization glitches (for example mono mic over stereo program audio).
    /// </summary>
    private (float[] samples, int count, WaveFormat format)? NormalizeToPreferredFormat(
        float[] samples, int count, WaveFormat incomingFormat)
    {
        var preferred = _lastSeenFormat ?? _encoderFormat ?? _defaultFormat;

        if (incomingFormat.SampleRate == preferred.SampleRate &&
            incomingFormat.Channels == preferred.Channels)
            return (samples, count, incomingFormat);

        // Upmix mono -> stereo (duplicate channel) when sample rate matches.
        if (incomingFormat.SampleRate == preferred.SampleRate &&
            incomingFormat.Channels == 1 &&
            preferred.Channels == 2)
        {
            var stereo = new float[count * 2];
            for (int i = 0; i < count; i++)
            {
                float sVal = samples[i];
                stereo[i * 2] = sVal;
                stereo[i * 2 + 1] = sVal;
            }
            return (stereo, stereo.Length, new WaveFormat(preferred.SampleRate, preferred.Channels));
        }

        // Unsupported conversion (typically sample-rate mismatch): skip to avoid stream corruption.
        return null;
    }

    /// <summary>Returns the machine's best local IPv4 address for LAN access.</summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "localhost";
        }
        catch { return "localhost"; }
    }

    // ── Listener thread ───────────────────────────────────────────────────────

    private void ListenLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try { ctx = _listener!.GetContext(); }
            catch { break; }
            ThreadPool.QueueUserWorkItem(_ => HandleClient(ctx));
        }
    }

    private void HandleClient(HttpListenerContext? ctx)
    {
        if (ctx == null) return;
        var path = ctx.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";

        if (path == "/stream" || path == "/stream.mp3")
        {
            ServeAudioStream(ctx);
        }
        else
        {
            ServeInfoPage(ctx);
        }
    }

    private void ServeAudioStream(HttpListenerContext ctx)
    {
        var ep = ctx.Request.RemoteEndPoint?.ToString() ?? "unknown";
        try
        {
            ctx.Response.ContentType = "audio/mpeg";
            ctx.Response.Headers["icy-name"] = StationName;
            ctx.Response.Headers["icy-br"] = BitRate.ToString();
            ctx.Response.Headers["icy-metaint"] = "0";
            ctx.Response.Headers["Cache-Control"] = "no-cache, no-store";
            ctx.Response.Headers["Connection"] = "keep-alive";
            ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
            ctx.Response.SendChunked = true;

            var client = new StreamClient(ctx.Response.OutputStream);
            _broadcast.AddClient(client);
            int listeners = _broadcast.ClientCount;
            ClientConnected?.Invoke(this, new StreamClientEventArgs(ep, listeners));

            // Block until client disconnects or stream is stopped
            client.Wait(_cts.Token);

            _broadcast.RemoveClient(client);
            ClientDisconnected?.Invoke(this, new StreamClientEventArgs(ep, _broadcast.ClientCount));
        }
        catch { }
        finally
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    private void ServeInfoPage(HttpListenerContext ctx)
    {
        try
        {
            string host = ctx.Request.UserHostName ?? $"localhost:{Port}";
            string streamUrl = $"http://{host}/stream";
            string localIp = GetLocalIpAddress();
            string lanUrl = IsNetworkWide ? $"http://{localIp}:{Port}/stream" : streamUrl;

            var html = $@"<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
  <title>{StationName}</title>
  <style>
    * {{ box-sizing: border-box; margin: 0; padding: 0; }}
    body {{ font-family: 'Segoe UI', sans-serif; background: #1a1a2e; color: #eee; padding: 2em; }}
    h1 {{ color: #00d4ff; margin-bottom: 0.3em; font-size: 2em; }}
    .subtitle {{ color: #aaa; margin-bottom: 1.5em; }}
    .card {{ background: #16213e; border: 1px solid #0f3460; border-radius: 8px; padding: 1.2em; margin-bottom: 1em; }}
    .card h2 {{ color: #e94560; margin-bottom: 0.6em; font-size: 1em; text-transform: uppercase; letter-spacing: 1px; }}
    a {{ color: #00d4ff; }}
    .player {{ width: 100%; margin-top: 1em; border-radius: 4px; }}
    .badge {{ display: inline-block; background: #0f3460; padding: 0.2em 0.6em; border-radius: 3px; font-size: 0.85em; margin: 0.2em; }}
    .tip {{ color: #aaa; font-size: 0.85em; margin-top: 0.6em; }}
    .url {{ font-family: monospace; background: #0a0a1a; padding: 0.3em 0.6em; border-radius: 3px; word-break: break-all; }}
    .green {{ color: #00ff88; }}
    .play-btn {{ display: inline-block; margin-top: 0.8em; background: #e94560; color: #fff; border: none; border-radius: 6px; padding: 0.5em 1.4em; font-size: 1em; cursor: pointer; }}
    .play-btn:hover {{ background: #ff6080; }}
  </style>
</head>
<body>
  <h1>📻 {StationName}</h1>
  <p class='subtitle'>GoonNet Radio Automation &mdash; Live Stream</p>

  <div class='card'>
    <h2>🎧 Listen Now</h2>
    <audio id='player' class='player' controls preload='auto'>
      <source src='/stream' type='audio/mpeg'>
      Your browser does not support audio streaming.
    </audio>
    <br>
    <button class='play-btn' onclick='document.getElementById(""player"").play()'>▶ Play Stream</button>
    <p class='tip'>Click Play above, or open the stream URL directly in VLC or another media player.</p>
  </div>

  <div class='card'>
    <h2>📡 Stream URLs</h2>
    <p>Local: <span class='url'><a href='{streamUrl}'>{streamUrl}</a></span></p>
    {(IsNetworkWide && lanUrl != streamUrl ? $"<p style='margin-top:0.5em'>LAN: <span class='url'><a href='{lanUrl}'>{lanUrl}</a></span></p>" : "")}
    <p class='tip'>Add the stream URL to VLC (Media → Open Network Stream) or any internet radio app.</p>
  </div>

  <div class='card'>
    <h2>ℹ️ Stream Info</h2>
    <span class='badge'>🎵 MP3</span>
    <span class='badge'>📶 {BitRate} kbps</span>
    <span class='badge green'>👥 {_broadcast.ClientCount} listener{(_broadcast.ClientCount == 1 ? "" : "s")}</span>
  </div>
</body>
</html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch { }
        finally
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    // ── Encoding thread ───────────────────────────────────────────────────────

    private void EncodeLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                bool hadItem = _queue.TryTake(out var item, 100, _cts.Token);

                if (!hadItem)
                {
                    // No audio data — send silence to keep connected clients alive and
                    // to stop the browser audio element from stalling.
                    if (_broadcast.ClientCount > 0)
                    {
                        var fmt = _lastSeenFormat ?? _defaultFormat;
                        EnsureEncoder(fmt);
                        if (_encoder != null)
                        {
                            // Generate ~100 ms of PCM-16 silence (all zeros) to keep
                            // connected clients alive and stop browser players from stalling.
                            const int silenceFrameMs = 100;
                            int silenceBytes = (fmt.SampleRate * fmt.Channels * silenceFrameMs / 1000) * 2;
                            var silence = new byte[silenceBytes]; // all zeros = silence
                            try { _encoder.Write(silence, 0, silence.Length); }
                            catch { _encoder?.Dispose(); _encoder = null; }
                        }
                    }
                    continue;
                }

                if (_broadcast.ClientCount == 0) continue;

                var (samples, count, format) = item;
                _lastSeenFormat = format;
                EnsureEncoder(format);
                if (_encoder == null) continue;

                // Convert float samples to PCM-16 bytes
                int byteCount = count * 2;
                var pcm = new byte[byteCount];
                for (int i = 0; i < count; i++)
                {
                    short s = (short)Math.Clamp(samples[i] * 32768f, -32768f, 32767f);
                    pcm[i * 2] = (byte)(s & 0xFF);
                    pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
                try { _encoder.Write(pcm, 0, byteCount); }
                catch (Exception ex)
                {
                    // Encoding failed — dispose encoder so it's recreated on next sample
                    _encoder?.Dispose();
                    _encoder = null;
                    StatusChanged?.Invoke(this, $"Encoding error: {ex.Message}");
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private void EnsureEncoder(WaveFormat format)
    {
        if (_encoder != null && _encoderFormat != null &&
            _encoderFormat.SampleRate == format.SampleRate &&
            _encoderFormat.Channels == format.Channels)
            return;

        _encoder?.Dispose();
        _encoderFormat = new WaveFormat(format.SampleRate, 16, format.Channels);
        try
        {
            _encoder = new LameMP3FileWriter(_broadcast, _encoderFormat, BitRate);
        }
        catch (Exception ex)
        {
            _encoder = null;
            StatusChanged?.Invoke(this, $"MP3 encoder error: {ex.Message}");
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _queue.Dispose();
    }

    // ── BroadcastBuffer: writes MP3 bytes to all connected clients ─────────────

    private sealed class BroadcastBuffer : Stream
    {
        private readonly List<StreamClient> _clients = new();
        private readonly object _lock = new();

        public int ClientCount { get { lock (_lock) return _clients.Count; } }

        public void AddClient(StreamClient c) { lock (_lock) _clients.Add(c); }
        public void RemoveClient(StreamClient c) { lock (_lock) _clients.Remove(c); }

        public void CloseAll()
        {
            lock (_lock)
            {
                foreach (var c in _clients) c.Close();
                _clients.Clear();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            List<StreamClient>? dead = null;
            lock (_lock)
            {
                foreach (var c in _clients)
                {
                    if (!c.TryWrite(buffer, offset, count))
                    {
                        dead ??= new List<StreamClient>();
                        dead.Add(c);
                    }
                }
                if (dead != null)
                    foreach (var d in dead) _clients.Remove(d);
            }
        }

        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // ── StreamClient: wraps an HTTP response stream ────────────────────────────

    private sealed class StreamClient
    {
        private readonly Stream _stream;
        private readonly ManualResetEventSlim _done = new();

        public StreamClient(Stream stream) { _stream = stream; }

        public bool TryWrite(byte[] buffer, int offset, int count)
        {
            try
            {
                _stream.Write(buffer, offset, count);
                _stream.Flush();
                return true;
            }
            catch
            {
                _done.Set();
                return false;
            }
        }

        public void Wait(CancellationToken ct)
        {
            try { _done.Wait(ct); } catch { }
        }

        public void Close()
        {
            _done.Set();
            try { _stream.Close(); } catch { }
        }
    }
}

public class StreamClientEventArgs : EventArgs
{
    public string ClientEndpoint { get; }
    public int ListenerCount { get; }
    public StreamClientEventArgs(string endpoint, int count) { ClientEndpoint = endpoint; ListenerCount = count; }
}
