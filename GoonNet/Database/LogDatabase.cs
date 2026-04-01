using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class LogDatabase : DatabaseBase<LogEntry>
{
    protected override Guid GetId(LogEntry item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<LogEntry>), new XmlRootAttribute("BroadcastLog"));

    public IEnumerable<LogEntry> GetByDateRange(DateTime from, DateTime to)
        => _items.Where(e => e.Timestamp >= from && e.Timestamp <= to).OrderBy(e => e.Timestamp);

    public IEnumerable<LogEntry> GetByTrack(string title)
        => _items.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

    public LogEntry AddEntry(string artist, string title, string fileName, EventType type, string notes = "")
    {
        var entry = new LogEntry
        {
            Artist = artist,
            Title = title,
            FileName = fileName,
            EventType = type,
            Notes = notes,
            Timestamp = DateTime.Now
        };
        Add(entry);
        return entry;
    }

    public IEnumerable<LogEntry> GetRecent(int count)
        => _items.OrderByDescending(e => e.Timestamp).Take(count);
}
