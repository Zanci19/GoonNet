using System;
using System.IO;
using System.Text.Json;

namespace GoonNet;

/// <summary>
/// Persists application settings (including MySQL connection) to a JSON file.
/// </summary>
public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GoonNet", "settings.json");

    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();

    // MySQL connection
    public string MySqlServer { get; set; } = "localhost";
    public int MySqlPort { get; set; } = 3306;
    public string MySqlDatabase { get; set; } = "goonnet";
    public string MySqlUser { get; set; } = "root";
    public string MySqlPassword { get; set; } = string.Empty;

    // Paths
    public string MusicFolder { get; set; } = ProjectPaths.MusicFolder;

    // Audio
    public string MainAudioDevice { get; set; } = string.Empty;
    public string PreviewAudioDevice { get; set; } = string.Empty;

    // Email
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string EmailFrom { get; set; } = string.Empty;

    /// <summary>Builds a MySqlConnector connection string from the stored settings.</summary>
    public string BuildConnectionString()
        => $"Server={MySqlServer};Port={MySqlPort};Database={MySqlDatabase};User ID={MySqlUser};Password={MySqlPassword};AllowZeroDateTime=True;ConvertZeroDateTime=True;";

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
