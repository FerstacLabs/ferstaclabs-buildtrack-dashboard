using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public static class AttendanceSchedulePolicy
{
    public const string DefaultTimeZoneId = "Asia/Baku";
    public static readonly TimeOnly DefaultPlannedStart = new(8, 0);
    public static readonly TimeOnly DefaultPlannedEnd = new(18, 0);
    public const int DefaultLateGraceMinutes = 10;
    public const int DefaultEarlyExitGraceMinutes = 10;

    private static readonly HashSet<string> ConfirmedCheckoutReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Manual",
        "AutoEndOfDay",
        "ExitDevice",
        "DeviceDirection",
    };

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var candidate = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(candidate);
        }
        catch (TimeZoneNotFoundException) when (candidate.Equals(DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (InvalidTimeZoneException) when (candidate.Equals(DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveTimeZone(DefaultTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveTimeZone(DefaultTimeZoneId);
        }
    }

    public static DateTimeOffset ToUtc(DateOnly workDate, TimeOnly localTime, TimeZoneInfo timeZone)
    {
        var localDateTime = workDate.ToDateTime(localTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone));
    }

    public static int CalculateLateMinutes(DateTimeOffset actualCheckIn, DateOnly workDate, TimeZoneInfo timeZone)
    {
        var graceEnd = ToUtc(workDate, DefaultPlannedStart, timeZone).AddMinutes(DefaultLateGraceMinutes);
        return Math.Max(0, (int)Math.Floor((actualCheckIn - graceEnd).TotalMinutes));
    }

    public static int CalculateEarlyExitMinutes(DateTimeOffset? confirmedCheckOut, DateOnly workDate, TimeZoneInfo timeZone)
    {
        if (confirmedCheckOut is null) return 0;

        var earlyExitThreshold = ToUtc(workDate, DefaultPlannedEnd, timeZone).AddMinutes(-DefaultEarlyExitGraceMinutes);
        return Math.Max(0, (int)Math.Floor((earlyExitThreshold - confirmedCheckOut.Value).TotalMinutes));
    }

    public static bool IsCheckoutConfirmed(AttendanceSession session) =>
        session.CheckOutTime is not null
        && session.CloseReason is not null
        && ConfirmedCheckoutReasons.Contains(session.CloseReason);

    public static double CalculateWorkedHours(AttendanceSession session, DateTimeOffset now)
    {
        var end = session.Status == AttendanceSessionStatus.Open
            ? now
            : session.CheckOutTime ?? session.LastSeenTime ?? session.CheckInTime;
        return Math.Round(Math.Max(0, (end - session.CheckInTime).TotalHours), 2);
    }

    public static string FormatPlannedTime(TimeOnly value) => value.ToString("HH:mm");
}
