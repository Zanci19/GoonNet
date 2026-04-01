using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class BackgroundDatabase : DatabaseBase<Background>
{
    protected override Guid GetId(Background item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<Background>), new XmlRootAttribute("BackgroundLibrary"));

    public IEnumerable<Background> GetByCategory(string category)
        => _items.Where(b => b.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Background> GetActive() => _items.Where(b => b.IsActive);
}
