using System;

namespace GoonNet;

public enum EventType
{
    Music, Jingle, Ad, Block, Speech, Pause, File, Command, Playlist, LiveReport, News
}

public enum TrackType
{
    Music, Jingle, Advertisement, Background, Speech, LiveReport, News
}

[Flags]
public enum DayOfWeekFlags
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Weekdays | Weekend
}

public enum PlaylistAlgorithm
{
    Sequential, Random, WeightedRandom, LeastRecent, DayNightAware
}

public enum AudioDeviceType
{
    Main, Preview
}

public enum UserRole
{
    Admin, Operator, ReadOnly
}

public enum DatabaseState
{
    Idle, Loading, Loaded, Locked, ReadOnly
}

[Flags]
public enum TrackFlag
{
    None = 0,
    New = 1,
    Promoted = 2,
    Duplicate = 4,
    Archived = 8
}

public enum RepeatMode
{
    Once, Daily, Weekly, Loop
}
