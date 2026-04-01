using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class AdDatabase : DatabaseBase<Advertisement>
{
    protected override Guid GetId(Advertisement item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<Advertisement>), new XmlRootAttribute("AdLibrary"));

    public IEnumerable<Advertisement> GetActiveAds()
    {
        var today = DateTime.Today;
        return _items.Where(a => a.IsActive && a.StartDate <= today && a.EndDate >= today && a.PlaysToday < a.MaxPlaysPerDay);
    }

    public IEnumerable<Advertisement> GetAdsForDay(DayOfWeekFlags day)
        => GetActiveAds().Where(a => (a.ValidDays & day) != 0);

    public bool IncrementPlayCount(Guid id)
    {
        var ad = GetById(id);
        if (ad == null) return false;
        ad.PlaysToday++;
        ad.TotalPlays++;
        ad.LastPlayed = DateTime.Now;
        return true;
    }

    public void ResetDailyCounts()
    {
        foreach (var ad in _items)
            ad.PlaysToday = 0;
    }
}
