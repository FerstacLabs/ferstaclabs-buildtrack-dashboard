using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public enum AttendanceSessionDecisionType
{
    CreateCheckIn,
    CloseCheckOut,
    Ignore,
    UpdateCheckOut,
    UpdateLastSeen
}

public sealed record AttendanceSessionDecision(AttendanceSessionDecisionType Type, string Reason)
{
    public static AttendanceSessionDecision CreateCheckIn(string reason = "No open session") => new(AttendanceSessionDecisionType.CreateCheckIn, reason);
    public static AttendanceSessionDecision CloseCheckOut(string reason = "Open session can be closed") => new(AttendanceSessionDecisionType.CloseCheckOut, reason);
    public static AttendanceSessionDecision Ignore(string reason) => new(AttendanceSessionDecisionType.Ignore, reason);
    public static AttendanceSessionDecision UpdateCheckOut(string reason = "Closed session checkout can be updated") => new(AttendanceSessionDecisionType.UpdateCheckOut, reason);
    public static AttendanceSessionDecision UpdateLastSeen(string reason = "Presence session last seen can be updated") => new(AttendanceSessionDecisionType.UpdateLastSeen, reason);
}

public static class AttendanceSessionPlanner
{
    public static AttendanceSessionDecision DecideSingleDeviceToggle(
        AttendanceSession? openSession,
        DateTimeOffset eventTime,
        TimeSpan minCheckoutGap)
    {
        if (openSession is null) return AttendanceSessionDecision.CreateCheckIn();

        if (eventTime < openSession.CheckInTime.Add(minCheckoutGap))
        {
            return AttendanceSessionDecision.Ignore("Minimum checkout gap was not reached");
        }

        return AttendanceSessionDecision.CloseCheckOut();
    }

    public static AttendanceSessionDecision DecideSingleDeviceDailySession(
        AttendanceSession? existingSession,
        DateTimeOffset eventTime,
        TimeSpan minCheckoutGap,
        bool updateCheckoutToLastSeen)
    {
        if (existingSession is null) return AttendanceSessionDecision.CreateCheckIn("No daily session");

        if (existingSession.Status == AttendanceSessionStatus.Open)
        {
            if (eventTime < existingSession.CheckInTime.Add(minCheckoutGap))
            {
                return AttendanceSessionDecision.Ignore("checkout gap not reached");
            }

            return AttendanceSessionDecision.CloseCheckOut("Open daily session can be closed");
        }

        if (!updateCheckoutToLastSeen)
        {
            return AttendanceSessionDecision.Ignore("daily session already closed and update disabled");
        }

        if (existingSession.CheckOutTime is null || eventTime > existingSession.CheckOutTime)
        {
            return AttendanceSessionDecision.UpdateCheckOut("closed daily session checkout can be updated to last seen");
        }

        return AttendanceSessionDecision.Ignore("daily session already closed and event is not newer");
    }


    public static AttendanceSessionDecision DecideOneCameraPresence(
        AttendanceSession? existingSession,
        DateTimeOffset eventTime)
    {
        if (existingSession is null) return AttendanceSessionDecision.CreateCheckIn("No daily presence session");
        if (existingSession.Status == AttendanceSessionStatus.Open) return AttendanceSessionDecision.UpdateLastSeen("same camera recognition updates last seen");
        return AttendanceSessionDecision.Ignore("presence session is already closed by confirmed source");
    }

    public static string BuildDisplayStatus(AttendanceSessionStatus status, string? closeReason, DateTimeOffset? lastSeenTime, DateTimeOffset now)
    {
        if (status == AttendanceSessionStatus.Closed)
        {
            return closeReason switch
            {
                "Manual" => "Manual bağlandı",
                "AutoEndOfDay" => "Gün sonu bağlandı",
                "ExitDevice" or "DeviceDirection" => "Təsdiqli çıxış",
                _ => "Bağlandı",
            };
        }

        if (lastSeenTime is not null && now - lastSeenTime <= TimeSpan.FromMinutes(15)) return "Az əvvəl göründü";
        if (lastSeenTime is not null) return "Bugün görünüb";
        return "İşdə qeydiyyatda";
    }
    public static AttendanceSessionDecision DecideDeviceDirection(
        AttendanceSession? openSession,
        AttendanceDirection direction,
        DateTimeOffset eventTime,
        TimeSpan minCheckoutGap)
    {
        if (direction == AttendanceDirection.Exit)
        {
            if (openSession is null) return AttendanceSessionDecision.Ignore("Exit event has no open session");
            if (eventTime < openSession.CheckInTime.Add(minCheckoutGap))
            {
                return AttendanceSessionDecision.Ignore("Minimum checkout gap was not reached");
            }

            return AttendanceSessionDecision.CloseCheckOut();
        }

        if (openSession is not null) return AttendanceSessionDecision.Ignore("Entry event already has open session");
        return AttendanceSessionDecision.CreateCheckIn();
    }

    public static DateOnly CalculateWorkDate(DateTimeOffset eventTime, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(eventTime, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static IReadOnlyList<AttendanceSession> SelectCurrentOpenSessions(IEnumerable<AttendanceSession> sessions, DateOnly currentWorkDate) => sessions
        .Where(session => session.WorkDate == currentWorkDate && session.Status == AttendanceSessionStatus.Open)
        .OrderBy(session => session.CheckInTime)
        .ToList();
}


