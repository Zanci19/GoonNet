using System;
using System.Collections.Generic;

namespace GoonNet;

public class ErrorLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ErrorLog
{
    private static readonly Lazy<ErrorLog> _instance = new(() => new ErrorLog());
    public static ErrorLog Instance => _instance.Value;

    private readonly List<ErrorLogEntry> _entries = new();
    private readonly object _lock = new();

    public event EventHandler<ErrorLogEntry>? ErrorAdded;
    public event EventHandler? Cleared;

    public IReadOnlyList<ErrorLogEntry> Entries
    {
        get { lock (_lock) { return _entries.ToArray(); } }
    }

    public void Add(string source, string message)
    {
        var entry = new ErrorLogEntry { Source = source, Message = message };
        lock (_lock) { _entries.Add(entry); }
        ErrorAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (_lock) { _entries.Clear(); }
        Cleared?.Invoke(this, EventArgs.Empty);
    }
}
