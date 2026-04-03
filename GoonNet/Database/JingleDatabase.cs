using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class JingleDatabase : DatabaseBase<Jingle>
{
    protected override Guid GetId(Jingle item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<Jingle>), new XmlRootAttribute("JingleLibrary"));

    public IEnumerable<Jingle> GetActive() => _items.Where(j => j.IsActive);

    public IEnumerable<Jingle> GetByCategory(string category)
        => _items.Where(j => j.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Jingle> GetByPriority(int priority)
        => _items.Where(j => j.Priority == priority);

    public bool IncrementPlayCount(Guid id)
    {
        var j = GetById(id);
        if (j == null) return false;
        j.PlayCount++;
        j.LastPlayed = DateTime.Now;
        Update(j);
        return true;
    }
}
