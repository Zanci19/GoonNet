using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class PlaylistDatabase : DatabaseBase<Playlist>
{
    protected override Guid GetId(Playlist item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<Playlist>), new XmlRootAttribute("Playlists"));

    public IEnumerable<Playlist> GetForDate(DateTime date)
        => _items.Where(p => p.CreatedDate.Date == date.Date);

    public IEnumerable<Playlist> GetAutomatic() => _items.Where(p => p.IsAutomatic);

    public IEnumerable<Playlist> GetManual() => _items.Where(p => !p.IsAutomatic);

    public Playlist? GetCurrentPlaylist()
        => _items.Where(p => !p.IsLocked)
                 .OrderByDescending(p => p.CreatedDate)
                 .FirstOrDefault();
}
