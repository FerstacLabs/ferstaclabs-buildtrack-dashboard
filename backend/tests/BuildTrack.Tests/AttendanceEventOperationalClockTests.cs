using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;

namespace BuildTrack.Tests;

public sealed class AttendanceEventOperationalClockTests
{
    [Fact]
    public void ActiveRegisterUsesCreatedAtForBakuWorkDate()
    {
        var attendanceEvent = new AttendanceEvent
        {
            Source = DahuaEventSourceExtensions.ActiveRegisterSource,
            EventTime = DateTimeOffset.Parse("2026-07-26T18:25:49Z"),
            CreatedAt = DateTimeOffset.Parse("2026-07-26T22:25:29Z"),
        };

        var timeZone = ResolveBakuTimeZone();

        Assert.Equal(DateTimeOffset.Parse("2026-07-26T22:25:29Z"), AttendanceEventOperationalClock.Resolve(attendanceEvent));
        Assert.Equal(new DateOnly(2026, 7, 27), AttendanceEventOperationalClock.ResolveWorkDate(attendanceEvent, timeZone));
    }

    [Fact]
    public void CgiPollingKeepsEventTimeForBakuWorkDate()
    {
        var attendanceEvent = new AttendanceEvent
        {
            Source = DahuaEventSourceExtensions.CgiPollingSource,
            EventTime = DateTimeOffset.Parse("2026-07-26T18:25:49Z"),
            CreatedAt = DateTimeOffset.Parse("2026-07-26T22:25:29Z"),
        };

        var timeZone = ResolveBakuTimeZone();

        Assert.Equal(DateTimeOffset.Parse("2026-07-26T18:25:49Z"), AttendanceEventOperationalClock.Resolve(attendanceEvent));
        Assert.Equal(new DateOnly(2026, 7, 26), AttendanceEventOperationalClock.ResolveWorkDate(attendanceEvent, timeZone));
    }

    private static TimeZoneInfo ResolveBakuTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
    }
}
