using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GoonNet;

public class Scheduler
{
    private System.Threading.Timer? _timer;
    private bool _running;

    public EventDatabase? EventDb { get; set; }
    public PlaylistDatabase? PlaylistDb { get; set; }
    public string LiveReportMonitorPath { get; set; } = string.Empty;

    public event EventHandler<SchedulerEventArgs>? EventTriggered;
    public event EventHandler<PlaylistInsertedArgs>? PlaylistInserted;
    public event EventHandler<string>? SchedulerError;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _timer = new System.Threading.Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
    }

    public void Stop()
    {
        _running = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = null;
    }

    private void Tick(object? state)
    {
        if (!_running) return;
        try
        {
            var now = DateTime.Now;
            CheckAndInsertPlaylists(now);
            if (!string.IsNullOrWhiteSpace(LiveReportMonitorPath) && Directory.Exists(LiveReportMonitorPath))
                CheckLiveReports(LiveReportMonitorPath);
        }
        catch (Exception ex)
        {
            SchedulerError?.Invoke(this, ex.Message);
        }
    }

    public void CheckAndInsertPlaylists(DateTime now)
    {
        if (PlaylistDb == null) return;
        var playlists = PlaylistDb.GetAll()
            .Where(p => !p.IsLocked && p.CreatedDate.Date == now.Date)
            .ToList();
        foreach (var pl in playlists)
        {
            PlaylistInserted?.Invoke(this, new PlaylistInsertedArgs(pl, now));
        }
    }

    public IEnumerable<BroadcastEvent> GetCurrentEvents(DateTime now)
    {
        if (EventDb == null) return Enumerable.Empty<BroadcastEvent>();
        var day = DateTimeToFlag(now.DayOfWeek);
        return EventDb.GetEventsForTime(now.TimeOfDay, day);
    }

    public void CheckLiveReports(string monitorPath)
    {
        if (!Directory.Exists(monitorPath)) return;
        var extensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".aac", ".m4a", ".wma" };
        var files = Directory.GetFiles(monitorPath)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-10));

        foreach (var file in files)
        {
            EventTriggered?.Invoke(this, new SchedulerEventArgs(
                new BroadcastEvent { Name = Path.GetFileName(file), Type = EventType.LiveReport, FilePath = file },
                DateTime.Now));
        }
    }

    private static DayOfWeekFlags DateTimeToFlag(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => DayOfWeekFlags.Monday,
        DayOfWeek.Tuesday => DayOfWeekFlags.Tuesday,
        DayOfWeek.Wednesday => DayOfWeekFlags.Wednesday,
        DayOfWeek.Thursday => DayOfWeekFlags.Thursday,
        DayOfWeek.Friday => DayOfWeekFlags.Friday,
        DayOfWeek.Saturday => DayOfWeekFlags.Saturday,
        DayOfWeek.Sunday => DayOfWeekFlags.Sunday,
        _ => DayOfWeekFlags.None
    };
}

public class SchedulerEventArgs : EventArgs
{
    public BroadcastEvent Event { get; }
    public DateTime TriggeredAt { get; }
    public SchedulerEventArgs(BroadcastEvent e, DateTime at) { Event = e; TriggeredAt = at; }
}

public class PlaylistInsertedArgs : EventArgs
{
    public Playlist Playlist { get; }
    public DateTime InsertedAt { get; }
    public PlaylistInsertedArgs(Playlist pl, DateTime at) { Playlist = pl; InsertedAt = at; }
}
