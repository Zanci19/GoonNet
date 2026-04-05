using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;

namespace GoonNet;

/// <summary>
/// MySQL-backed music database. Overrides the XML-based DatabaseBase operations
/// so all CRUD goes directly to the MySQL <c>music</c> table while keeping the
/// in-memory list as a working cache.
/// </summary>
public class MySqlMusicDatabase : MusicDatabase
{
    private string _connectionString = string.Empty;

    public void InitializeMySql(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Load / Save
    // ──────────────────────────────────────────────────────────────────────────

    public override async Task LoadAsync()
    {
        try
        {
            _items = await FetchAllFromDb();
            State = DatabaseState.Loaded;
            InvokeOnLoaded();
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("MySqlMusicDatabase.Load", ex.Message);
            _items = new List<MusicTrack>();
            State = DatabaseState.Idle;
        }
    }

    // No-op: MySQL is written through immediately on Add/Update/Delete.
    public override Task SaveAsync() => Task.CompletedTask;

    // ──────────────────────────────────────────────────────────────────────────
    // CRUD
    // ──────────────────────────────────────────────────────────────────────────

    public new void Add(MusicTrack track)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                INSERT INTO music (author, title, `year`, category, music_path, playlist, added_at, play_count)
                VALUES (@author, @title, @year, @category, @music_path, @playlist, @added_at, 0);
                SELECT LAST_INSERT_ID();";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@author", track.Artist);
            cmd.Parameters.AddWithValue("@title", track.Title);
            cmd.Parameters.AddWithValue("@year", track.Year > 0 ? (object)track.Year : DBNull.Value);
            cmd.Parameters.AddWithValue("@category", track.Genre);
            cmd.Parameters.AddWithValue("@music_path", BuildMusicPath(track));
            cmd.Parameters.AddWithValue("@playlist", track.PlaylistName);
            cmd.Parameters.AddWithValue("@added_at", track.DateAdded);
            track.DbId = Convert.ToInt32(cmd.ExecuteScalar());
            _items.Add(track);
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("MySqlMusicDatabase.Add", ex.Message);
            throw;
        }
    }

    public new bool Update(MusicTrack track)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                UPDATE music SET author=@author, title=@title, `year`=@year,
                    category=@category, music_path=@music_path, playlist=@playlist
                WHERE ID=@id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@author", track.Artist);
            cmd.Parameters.AddWithValue("@title", track.Title);
            cmd.Parameters.AddWithValue("@year", track.Year > 0 ? (object)track.Year : DBNull.Value);
            cmd.Parameters.AddWithValue("@category", track.Genre);
            cmd.Parameters.AddWithValue("@music_path", BuildMusicPath(track));
            cmd.Parameters.AddWithValue("@playlist", track.PlaylistName);
            cmd.Parameters.AddWithValue("@id", track.DbId);
            cmd.ExecuteNonQuery();

            var idx = _items.FindIndex(t => t.Id == track.Id);
            if (idx >= 0) _items[idx] = track;
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("MySqlMusicDatabase.Update", ex.Message);
            return false;
        }
    }

    public new bool Delete(Guid id)
    {
        var track = _items.FirstOrDefault(t => t.Id == id);
        if (track == null) return false;
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlCommand("DELETE FROM music WHERE ID=@id", conn);
            cmd.Parameters.AddWithValue("@id", track.DbId);
            cmd.ExecuteNonQuery();
            _items.Remove(track);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("MySqlMusicDatabase.Delete", ex.Message);
            return false;
        }
    }

    public bool UpdatePlayStats(int dbId)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            const string sql = "UPDATE music SET play_count=play_count+1, last_played=@now WHERE ID=@id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            cmd.Parameters.AddWithValue("@id", dbId);
            cmd.ExecuteNonQuery();
            var track = _items.FirstOrDefault(t => t.DbId == dbId);
            if (track != null) { track.PlayCount++; track.LastPlayed = DateTime.Now; }
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("MySqlMusicDatabase.UpdatePlayStats", ex.Message);
            return false;
        }
    }

    public new bool UpdatePlayStats(Guid id)
    {
        var track = _items.FirstOrDefault(t => t.Id == id);
        if (track == null) return false;
        return UpdatePlayStats(track.DbId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Playlist helpers
    // ──────────────────────────────────────────────────────────────────────────

    public IEnumerable<string> GetAllPlaylistNames()
        => _items.Select(t => t.PlaylistName)
                 .Where(n => !string.IsNullOrWhiteSpace(n))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(n => n);

    public IEnumerable<MusicTrack> GetByPlaylist(string playlistName)
        => _items.Where(t => t.PlaylistName.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

    // ──────────────────────────────────────────────────────────────────────────
    // Import / Export
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Exports all music rows to a CSV file.</summary>
    public void ExportCsv(string path)
    {
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        writer.WriteLine("id,author,title,year,category,music_path,playlist,added_at,last_played,play_count");
        foreach (var t in _items)
        {
            writer.WriteLine(string.Join(",",
                Csv(t.DbId.ToString()),
                Csv(t.Artist),
                Csv(t.Title),
                Csv(t.Year > 0 ? t.Year.ToString() : ""),
                Csv(t.Genre),
                Csv(BuildMusicPath(t)),
                Csv(t.PlaylistName),
                Csv(t.DateAdded.ToString("o")),
                Csv(t.LastPlayed?.ToString("o") ?? ""),
                Csv(t.PlayCount.ToString())));
        }
    }

    /// <summary>Exports all music rows as SQL INSERT statements.</summary>
    public void ExportSql(string path)
    {
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        writer.WriteLine("-- GoonNet music export " + DateTime.Now.ToString("o"));
        writer.WriteLine("-- Run this against your MySQL/MariaDB server");
        writer.WriteLine();
        writer.WriteLine("CREATE TABLE IF NOT EXISTS music (");
        writer.WriteLine("  ID INT AUTO_INCREMENT PRIMARY KEY,");
        writer.WriteLine("  author VARCHAR(255) DEFAULT NULL,");
        writer.WriteLine("  title VARCHAR(255) DEFAULT NULL,");
        writer.WriteLine("  `year` INT(4) DEFAULT NULL,");
        writer.WriteLine("  category VARCHAR(32) DEFAULT NULL,");
        writer.WriteLine("  music_path VARCHAR(512) DEFAULT NULL,");
        writer.WriteLine("  playlist VARCHAR(64) DEFAULT NULL,");
        writer.WriteLine("  added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,");
        writer.WriteLine("  last_played TIMESTAMP NULL,");
        writer.WriteLine("  play_count INT DEFAULT 0");
        writer.WriteLine(");");
        writer.WriteLine();
        foreach (var t in _items)
        {
            writer.WriteLine(
                $"INSERT INTO music (author, title, `year`, category, music_path, playlist, added_at, last_played, play_count) VALUES " +
                $"({SqlStr(t.Artist)}, {SqlStr(t.Title)}, {(t.Year > 0 ? t.Year.ToString() : "NULL")}, " +
                $"{SqlStr(t.Genre)}, {SqlStr(BuildMusicPath(t))}, {SqlStr(t.PlaylistName)}, " +
                $"{SqlStr(t.DateAdded.ToString("yyyy-MM-dd HH:mm:ss"))}, " +
                $"{(t.LastPlayed.HasValue ? SqlStr(t.LastPlayed.Value.ToString("yyyy-MM-dd HH:mm:ss")) : "NULL")}, " +
                $"{t.PlayCount});");
        }
    }

    /// <summary>Imports music rows from a CSV file (skips duplicates by music_path).</summary>
    public (int added, int skipped) ImportCsv(string path)
    {
        int added = 0, skipped = 0;
        var existing = _items.Select(t => BuildMusicPath(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line);
            if (parts.Length < 7) continue;

            // skip header re-occurrences
            if (parts[0].Equals("id", StringComparison.OrdinalIgnoreCase)) continue;

            var musicPath = parts[5].Trim('"');
            if (existing.Contains(musicPath)) { skipped++; continue; }

            var track = new MusicTrack
            {
                Artist = parts[1].Trim('"'),
                Title = parts[2].Trim('"'),
                Year = int.TryParse(parts[3].Trim('"'), out var yr) ? yr : 0,
                Genre = parts[4].Trim('"'),
                PlaylistName = parts.Length > 6 ? parts[6].Trim('"') : "",
                DateAdded = DateTime.TryParse(parts.Length > 7 ? parts[7].Trim('"') : "", out var da) ? da : DateTime.Now,
                LastPlayed = parts.Length > 8 && DateTime.TryParse(parts[8].Trim('"'), out var lp) ? lp : null,
                PlayCount = parts.Length > 9 && int.TryParse(parts[9].Trim('"'), out var pc) ? pc : 0
            };
            track.Location = Path.GetDirectoryName(musicPath)?.Replace('\\', '/') ?? "/music";
            track.FileName = Path.GetFileName(musicPath);

            Add(track);
            existing.Add(musicPath);
            added++;
        }
        return (added, skipped);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Connection test
    // ──────────────────────────────────────────────────────────────────────────

    public bool TestConnection(out string error)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<MusicTrack>> FetchAllFromDb()
    {
        var list = new List<MusicTrack>();
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = "SELECT ID, author, title, `year`, category, music_path, playlist, added_at, last_played, play_count FROM music ORDER BY author, title";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var musicPath = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            var track = new MusicTrack
            {
                DbId = reader.GetInt32(0),
                Artist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Year = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Genre = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                PlaylistName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                DateAdded = reader.IsDBNull(7) ? DateTime.Now : reader.GetDateTime(7),
                LastPlayed = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                PlayCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                Location = !string.IsNullOrEmpty(musicPath)
                    ? (Path.GetDirectoryName(musicPath)?.Replace('\\', '/') ?? "/music")
                    : "/music",
                FileName = !string.IsNullOrEmpty(musicPath) ? Path.GetFileName(musicPath) : string.Empty
            };
            list.Add(track);
        }
        return list;
    }

    private static string BuildMusicPath(MusicTrack t)
    {
        if (string.IsNullOrEmpty(t.Location))
            return "/music/" + t.FileName;
        // Normalise to forward-slash, relative-style path
        var loc = t.Location.Replace('\\', '/').TrimEnd('/');
        return loc + "/" + t.FileName;
    }

    private static string Csv(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
    private static string SqlStr(string v) => "'" + v.Replace("'", "''") + "'";

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    // Check for escaped double-quote ("")
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuote = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"') { inQuote = true; }
                else if (c == ',') { result.Add(current.ToString()); current.Clear(); }
                else { current.Append(c); }
            }
            i++;
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
