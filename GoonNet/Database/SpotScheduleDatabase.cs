using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GoonNet;

public class SpotScheduleDatabase : DatabaseBase<SpotScheduleEntry>
{
    protected override Guid GetId(SpotScheduleEntry item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<SpotScheduleEntry>), new XmlRootAttribute("SpotSchedule"));

    public List<SpotScheduleEntry> GetForDay(DayOfWeek day) =>
        GetAll().Where(e => e.IsActive && e.DayOfWeek == (int)day).ToList();

    public List<SpotScheduleEntry> GetForDayAndHour(DayOfWeek day, int hour) =>
        GetAll().Where(e => e.IsActive && e.DayOfWeek == (int)day && e.Hour == hour).ToList();

    public List<SpotScheduleEntry> GetForAd(Guid adId) =>
        GetAll().Where(e => e.AdId == adId).ToList();
}
