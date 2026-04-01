using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class MusicDatabase : DatabaseBase<MusicTrack>
{
    protected override Guid GetId(MusicTrack item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<MusicTrack>), new XmlRootAttribute("MusicLibrary"));

    public IEnumerable<MusicTrack> SearchByArtist(string artist)
        => _items.Where(t => t.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<MusicTrack> SearchByTitle(string title)
        => _items.Where(t => t.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<MusicTrack> GetByGenre(string genre)
        => _items.Where(t => t.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<MusicTrack> GetByFlags(TrackFlag flags)
        => _items.Where(t => (t.Flags & flags) != 0);

    public IEnumerable<MusicTrack> GetNeverPlayed()
        => _items.Where(t => t.PlayCount == 0);

    public IEnumerable<MusicTrack> GetLeastPlayed(int count)
        => _items.OrderBy(t => t.PlayCount).Take(count);

    public IEnumerable<MusicTrack> GetByBPMRange(double min, double max)
        => _items.Where(t => t.BPM >= min && t.BPM <= max);

    public bool UpdatePlayStats(Guid id)
    {
        var track = GetById(id);
        if (track == null) return false;
        track.PlayCount++;
        track.LastPlayed = DateTime.Now;
        return true;
    }

    public IEnumerable<MusicTrack> Search(string? artist, string? title, string? genre, TrackFlag flags)
    {
        var query = _items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(artist))
            query = query.Where(t => t.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(title))
            query = query.Where(t => t.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(t => t.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));
        if (flags != TrackFlag.None)
            query = query.Where(t => (t.Flags & flags) != 0);
        return query;
    }

    public IEnumerable<string> GetAllGenres()
        => _items.Select(t => t.Genre).Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g);
}
