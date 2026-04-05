using System;
using System.Collections.Generic;
using System.Linq;

namespace GoonNet;

public class PlaylistGenerator
{
    private readonly Random _random = new();
    private const int ArtistRepeatMinutes = 30;

    public Playlist GeneratePlaylist(MusicDatabase db, TimeSpan duration, PlaylistAlgorithm algorithm, string genre = "")
    {
        var playlist = new Playlist
        {
            Name = $"Auto {DateTime.Now:yyyy-MM-dd HH:mm}",
            IsAutomatic = true,
            Algorithm = algorithm,
            TargetDuration = duration,
            CreatedDate = DateTime.Now
        };

        var allTracks = db.GetAll().ToList();
        if (!string.IsNullOrWhiteSpace(genre))
            allTracks = allTracks.Where(t => t.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase)).ToList();

        if (allTracks.Count == 0)
            return playlist;

        var selected = algorithm switch
        {
            PlaylistAlgorithm.Sequential => SelectSequential(allTracks),
            PlaylistAlgorithm.Random => SelectRandom(allTracks),
            PlaylistAlgorithm.WeightedRandom => SelectWeightedRandom(allTracks),
            PlaylistAlgorithm.LeastRecent => SelectLeastRecent(allTracks),
            PlaylistAlgorithm.DayNightAware => SelectDayNightAware(allTracks),
            _ => SelectSequential(allTracks)
        };

        FillToTarget(playlist, selected, duration, db);
        return playlist;
    }

    private List<MusicTrack> SelectSequential(List<MusicTrack> tracks)
        => tracks.OrderBy(t => t.Artist).ThenBy(t => t.Title).ToList();

    private List<MusicTrack> SelectRandom(List<MusicTrack> tracks)
    {
        var list = tracks.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private List<MusicTrack> SelectWeightedRandom(List<MusicTrack> tracks)
    {
        var result = new List<MusicTrack>();
        var pool = tracks.ToList();
        // Weight = inverse of play count (less played = more likely to be picked)
        while (pool.Count > 0)
        {
            int totalWeight = pool.Sum(t => Math.Max(1, 100 - t.PlayCount));
            int pick = _random.Next(Math.Max(1, totalWeight));
            int cumulative = 0;
            MusicTrack? chosen = null;
            foreach (var t in pool)
            {
                cumulative += Math.Max(1, 100 - t.PlayCount);
                if (pick < cumulative) { chosen = t; break; }
            }
            if (chosen != null)
            {
                result.Add(chosen);
                pool.Remove(chosen);
            }
        }
        return result;
    }

    private List<MusicTrack> SelectLeastRecent(List<MusicTrack> tracks)
        => tracks.OrderBy(t => t.LastPlayed ?? DateTime.MinValue).ToList();

    private List<MusicTrack> SelectDayNightAware(List<MusicTrack> tracks)
    {
        var now = DateTime.Now.TimeOfDay;
        bool isDaytime = now >= TimeSpan.FromHours(6) && now < TimeSpan.FromHours(22);
        if (isDaytime)
            return tracks.OrderBy(t => t.LastPlayed ?? DateTime.MinValue).ToList();
        else
            return SelectRandom(tracks);
    }

    private void FillToTarget(Playlist playlist, List<MusicTrack> orderedTracks, TimeSpan target, MusicDatabase db)
    {
        var recentArtists = new Queue<(string artist, DateTime addedAt)>();
        var usedIds = new HashSet<Guid>();
        var order = 0;

        foreach (var track in orderedTracks)
        {
            if (playlist.TotalDuration >= target)
                break;

            if (usedIds.Contains(track.Id))
                continue;

            // Remove old artists from recent queue
            var cutoff = DateTime.Now.AddMinutes(-ArtistRepeatMinutes);
            while (recentArtists.Count > 0 && recentArtists.Peek().addedAt < cutoff)
                recentArtists.Dequeue();

            // Check artist repeat
            if (recentArtists.Any(x => x.artist.Equals(track.Artist, StringComparison.OrdinalIgnoreCase)))
                continue;

            playlist.Items.Add(new PlaylistItem
            {
                TrackId = track.Id,
                TrackType = TrackType.Music,
                Order = order++,
                Duration = track.Duration,
                Artist = track.Artist,
                Title = track.Title
            });

            usedIds.Add(track.Id);
            recentArtists.Enqueue((track.Artist, DateTime.Now));
        }
    }
}
