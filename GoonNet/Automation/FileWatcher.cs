using System;
using System.IO;

namespace GoonNet;

public class FileWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public string MonitorPath { get; set; } = string.Empty;
    public string BaseFolder { get; set; } = string.Empty;

    private static readonly string[] SupportedExtensions =
        { ".mp3", ".wav", ".ogg", ".flac", ".aac", ".m4a", ".wma" };

    public event EventHandler<AudioFileDetectedArgs>? AudioFileDetected;

    public void StartWatching(string path)
    {
        MonitorPath = path;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnFileCreated;
        _watcher.Renamed += OnFileRenamed;
    }

    public void StopWatching()
    {
        if (_watcher != null)
            _watcher.EnableRaisingEvents = false;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (IsSupportedExtension(e.FullPath))
        {
            var trackType = DetectFileType(e.FullPath);
            AudioFileDetected?.Invoke(this, new AudioFileDetectedArgs(e.FullPath, trackType));
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsSupportedExtension(e.FullPath))
        {
            var trackType = DetectFileType(e.FullPath);
            AudioFileDetected?.Invoke(this, new AudioFileDetectedArgs(e.FullPath, trackType));
        }
    }

    private static bool IsSupportedExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(SupportedExtensions, ext) >= 0;
    }

    public TrackType DetectFileType(string path)
    {
        var lower = path.ToLowerInvariant();
        if (lower.Contains(Path.DirectorySeparatorChar + "jingle")) return TrackType.Jingle;
        if (lower.Contains(Path.DirectorySeparatorChar + "ad") ||
            lower.Contains(Path.DirectorySeparatorChar + "advertisement")) return TrackType.Advertisement;
        if (lower.Contains(Path.DirectorySeparatorChar + "background")) return TrackType.Background;
        if (lower.Contains(Path.DirectorySeparatorChar + "speech")) return TrackType.Speech;
        if (lower.Contains(Path.DirectorySeparatorChar + "report") ||
            lower.Contains(Path.DirectorySeparatorChar + "live")) return TrackType.LiveReport;
        if (lower.Contains(Path.DirectorySeparatorChar + "news")) return TrackType.News;
        return TrackType.Music;
    }

    public void MoveToCorrectFolder(string sourcePath, TrackType type, string baseFolder)
    {
        var subfolder = type switch
        {
            TrackType.Music => "Music",
            TrackType.Jingle => "Jingles",
            TrackType.Advertisement => "Ads",
            TrackType.Background => "Backgrounds",
            TrackType.Speech => "Speech",
            TrackType.LiveReport => "Reports",
            TrackType.News => "News",
            _ => "Music"
        };

        var targetDir = Path.Combine(baseFolder, subfolder);
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
        if (!File.Exists(targetPath))
            File.Move(sourcePath, targetPath);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _watcher?.Dispose();
        }
    }
}

public class AudioFileDetectedArgs : EventArgs
{
    public string FilePath { get; }
    public TrackType DetectedType { get; }
    public AudioFileDetectedArgs(string path, TrackType type) { FilePath = path; DetectedType = type; }
}
