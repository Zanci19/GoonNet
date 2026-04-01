using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GoonNet;

public class TelnetServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private bool _disposed;

    public int Port { get; set; } = 23;
    public string ServerPassword { get; set; } = "goonnet";
    public bool IsRunning { get; private set; }

    public event EventHandler<TelnetClientArgs>? ClientConnected;
    public event EventHandler<TelnetClientArgs>? ClientDisconnected;
    public event EventHandler<TelnetCommandArgs>? CommandReceived;

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        IsRunning = true;
        Task.Run(AcceptClientsAsync, _cts.Token);
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
        _listener?.Stop();
        foreach (var client in _clients.Values)
            client.Close();
        _clients.Clear();
    }

    private async Task AcceptClientsAsync()
    {
        while (IsRunning && _cts != null && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(_cts.Token);
                var id = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                _clients[id] = client;
                ClientConnected?.Invoke(this, new TelnetClientArgs(id));
                _ = HandleClientAsync(client, id, _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { /* listener stopped */ }
        }
    }

    private async Task HandleClientAsync(TcpClient client, string clientId, CancellationToken ct)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        try
        {
            await writer.WriteLineAsync("GoonNet Radio Automation Server");
            await writer.WriteAsync("Password: ");
            var pwd = await reader.ReadLineAsync(ct);
            if (pwd != ServerPassword)
            {
                await writer.WriteLineAsync("Access denied.");
                return;
            }
            await writer.WriteLineAsync("Welcome! Type HELP for commands.");
            while (!ct.IsCancellationRequested)
            {
                await writer.WriteAsync("> ");
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                var response = ProcessCommand(line.Trim(), clientId);
                await writer.WriteLineAsync(response);
                if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) break;
            }
        }
        catch { }
        finally
        {
            _clients.TryRemove(clientId, out _);
            client.Close();
            ClientDisconnected?.Invoke(this, new TelnetClientArgs(clientId));
        }
    }

    private string ProcessCommand(string line, string clientId)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToUpperInvariant();
        var arg = parts.Length > 1 ? parts[1] : string.Empty;

        CommandReceived?.Invoke(this, new TelnetCommandArgs(clientId, cmd, arg));

        return cmd switch
        {
            "STATUS" => $"Status: {(AudioEngine.Instance.IsPlaying ? "Playing" : "Stopped")} | Track: {AudioEngine.Instance.CurrentTrack?.Title ?? "None"}",
            "PLAY" => HandlePlay(arg),
            "STOP" => HandleStop(),
            "NEXT" => "NEXT: command queued",
            "VOLUME" => HandleVolume(arg),
            "HELP" => "Commands: STATUS, PLAY <file>, STOP, NEXT, VOLUME <0-100>, QUIT",
            "QUIT" => "Goodbye.",
            _ => $"Unknown command: {cmd}"
        };
    }

    private static string HandlePlay(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return "Usage: PLAY <filename>";
        var track = new MusicTrack
        {
            FileName = System.IO.Path.GetFileName(arg),
            Location = System.IO.Path.GetDirectoryName(arg) ?? string.Empty,
            Title = System.IO.Path.GetFileNameWithoutExtension(arg)
        };
        AudioEngine.Instance.PlayTrack(track);
        return $"Playing: {arg}";
    }

    private static string HandleStop()
    {
        AudioEngine.Instance.Stop();
        return "Stopped.";
    }

    private static string HandleVolume(string arg)
    {
        if (int.TryParse(arg, out var vol))
        {
            AudioEngine.Instance.MainVolume = vol / 100f;
            return $"Volume set to {vol}%";
        }
        return "Usage: VOLUME <0-100>";
    }

    public void Dispose()
    {
        if (!_disposed) { _disposed = true; Stop(); }
    }
}

public class TelnetClientArgs : EventArgs
{
    public string ClientId { get; }
    public TelnetClientArgs(string id) { ClientId = id; }
}

public class TelnetCommandArgs : EventArgs
{
    public string ClientId { get; }
    public string Command { get; }
    public string Argument { get; }
    public TelnetCommandArgs(string clientId, string command, string argument) { ClientId = clientId; Command = command; Argument = argument; }
}
