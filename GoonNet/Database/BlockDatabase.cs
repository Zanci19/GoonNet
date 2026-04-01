using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class BlockDatabase : DatabaseBase<Block>
{
    protected override Guid GetId(Block item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<Block>), new XmlRootAttribute("BlockLibrary"));

    public IEnumerable<Block> GetTimedBlocks() => _items.Where(b => b.IsTimedBlock);

    public IEnumerable<Block> GetSpecialBlocks() => _items.Where(b => !b.IsTimedBlock);
}
