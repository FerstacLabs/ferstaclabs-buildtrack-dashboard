using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public static class AttendanceEventOperationalClock
{
    public static DateTimeOffset Resolve(AttendanceEvent attendanceEvent) =>
        string.Equals(attendanceEvent.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)
            ? attendanceEvent.CreatedAt
            : attendanceEvent.EventTime;

    public static DateOnly ResolveWorkDate(AttendanceEvent attendanceEvent, TimeZoneInfo timeZone) =>
        AttendanceSessionPlanner.CalculateWorkDate(Resolve(attendanceEvent), timeZone);
}
