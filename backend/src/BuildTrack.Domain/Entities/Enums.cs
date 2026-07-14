namespace BuildTrack.Domain.Entities;

public enum WorkerStatus
{
    Active,
    Inactive,
    Blocked
}

public enum DeviceMode
{
    ActiveRegister,
    CgiPollingFallback,
    Simulator
}

public enum DeviceStatus
{
    Pending,
    Online,
    Offline,
    Error
}

public enum AttendanceDirection
{
    Entry,
    Exit,
    Unknown
}

public enum AttendanceEventStatus
{
    Ok,
    Failed,
    Stranger
}

public enum AttendanceMethod
{
    Face,
    Card,
    Fingerprint,
    Password,
    Manual,
    Unknown
}
