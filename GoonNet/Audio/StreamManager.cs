using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
        = new(boundedCapacity: 80);
    private SampleAggregator? _currentAggregator;
    private bool _disposed;

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
        if (!IsStreaming || _broadcast.ClientCount == 0) return;
        var copy = new float[e.Count];
        Array.Copy(e.Samples, copy, e.Count);
        _queue.TryAdd((copy, e.Count, e.WaveFormat));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Start()
    {
        if (IsStreaming) return;
        _cts = new CancellationTokenSource();

        _listener = CreateListener();
        try { _listener.Start(); }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Failed to start stream: {ex.Message}");
            return;
        }

        IsStreaming = true;

        _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "StreamListen" };
        _listenThread.Start();

        _encodeThread = new Thread(EncodeLoop) { IsBackground = true, Name = "StreamEncode" };
        _encodeThread.Start();

        StatusChanged?.Invoke(this, $"Streaming on http://localhost:{Port}/stream");
    }

    public void Stop()
    {
        if (!IsStreaming) return;
        IsStreaming = false;
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        _broadcast.CloseAll();
        _encoder?.Dispose();
        _encoder = null;
        _encoderFormat = null;
        StatusChanged?.Invoke(this, "Stream stopped");
    }

    // ── Listener thread ───────────────────────────────────────────────────────

    private HttpListener CreateListener()
    {
        // Try network-wide prefix first (requires admin on Windows); fall back to localhost only
        try
        {
            var hl = new HttpListener();
            hl.Prefixes.Add($"http://+:{Port}/");
            return hl;
        }
        catch (HttpListenerException)
        {
            var hl = new HttpListener();
            hl.Prefixes.Add($"http://localhost:{Port}/");
            return hl;
        }
    }

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
            ctx.Response.Headers["Connection"] = "close";
            ctx.Response.SendChunked = false;

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
            var html = $@"<!DOCTYPE html><html>
<head><meta charset='utf-8'><title>{StationName}</title>
<style>body{{font-family:sans-serif;background:#111;color:#eee;padding:2em;}}
a{{color:#4cf;}} h1{{color:#4f4;}}</style></head>
<body>
<h1>📻 {StationName}</h1>
<p><strong>Stream URL:</strong>
<a href='http://{ctx.Request.UserHostName}:{Port}/stream'>
http://{ctx.Request.UserHostName}:{Port}/stream</a></p>
<p><strong>Listeners:</strong> {_broadcast.ClientCount}</p>
<p><strong>Bitrate:</strong> {BitRate} kbps | <strong>Format:</strong> MP3</p>
<p>Open the stream URL in your browser, VLC, foobar2000, or any internet radio player.</p>
<audio controls autoplay src='/stream' style='width:100%;margin-top:1em'></audio>
</body></html>";
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
                if (!_queue.TryTake(out var item, 100, _cts.Token)) continue;
                if (_broadcast.ClientCount == 0) continue;

                var (samples, count, format) = item;
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
