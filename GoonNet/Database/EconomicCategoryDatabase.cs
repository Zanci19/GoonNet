using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class EconomicCategoryDatabase : DatabaseBase<EconomicCategory>
{
    protected override Guid GetId(EconomicCategory item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<EconomicCategory>), new XmlRootAttribute("EconomicCategories"));

    public List<EconomicCategory> GetActive() =>
        GetAll().Where(c => c.IsActive).ToList();

    public EconomicCategory? GetByName(string name) =>
        GetAll().FirstOrDefault(c => c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
}
