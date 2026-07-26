using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class AttendanceSessionService(
    BuildTrackDbContext db,
    IConfiguration configuration,
    ILogger<AttendanceSessionService> logger) : IAttendanceSessionService
{
    public async Task ProcessEventAsync(AttendanceEvent attendanceEvent, CancellationToken cancellationToken = default)
    {
        if (!IsValidSessionEvent(attendanceEvent))
        {
            logger.LogInformation(
                "Skipped session processing because event invalid. Event {EventId}, Source {Source}, Status {Status}, WorkerExternalId {WorkerExternalId}",
                attendanceEvent.Id,
                attendanceEvent.Source,
                attendanceEvent.Status,
                attendanceEvent.WorkerExternalId);
            return;
        }

        var alreadyUsed = await db.AttendanceSessions.AnyAsync(
            x => x.CheckInEventId == attendanceEvent.Id || x.CheckOutEventId == attendanceEvent.Id,
            cancellationToken);
        if (alreadyUsed)
        {
            logger.LogInformation("Skipped session processing because event {EventId} was already attached to a session", attendanceEvent.Id);
            return;
        }

        var timeZone = ResolveTimeZone(configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
        var operationalEventTime = AttendanceEventOperationalClock.Resolve(attendanceEvent);
        var workDate = AttendanceEventOperationalClock.ResolveWorkDate(attendanceEvent, timeZone);
        var minCheckoutGap = TimeSpan.FromMinutes(ParsePositiveInt(configuration["DAHUA_ATTENDANCE_MIN_CHECKOUT_AFTER_MINUTES"], 15));
        var mode = configuration["DAHUA_ATTENDANCE_MODE"] ?? "SingleDeviceToggle";
        var singleSessionPerDay = ParseBool(configuration["DAHUA_ATTENDANCE_SINGLE_SESSION_PER_DAY"], defaultValue: true);
        var updateCheckoutToLastSeen = ParseBool(configuration["DAHUA_ATTENDANCE_UPDATE_CHECKOUT_TO_LAST_SEEN"], defaultValue: true);
        var oneCameraPresenceMode = ParseBool(configuration["DAHUA_ATTENDANCE_ONE_CAMERA_PRESENCE_MODE"], defaultValue: true);
        var workerExternalId = attendanceEvent.WorkerExternalId!.Trim();

        if (mode.Equals("SingleDeviceToggle", StringComparison.OrdinalIgnoreCase) && singleSessionPerDay && oneCameraPresenceMode)
        {
            await ProcessOneCameraPresenceSessionAsync(attendanceEvent, workerExternalId, workDate, cancellationToken);
            return;
        }

        if (mode.Equals("SingleDeviceToggle", StringComparison.OrdinalIgnoreCase) && singleSessionPerDay)
        {
            await ProcessSingleDeviceDailySessionAsync(attendanceEvent, workerExternalId, workDate, minCheckoutGap, updateCheckoutToLastSeen, cancellationToken);
            return;
        }

        var openSession = await FindOpenSessionAsync(attendanceEvent.DeviceId, workerExternalId, workDate, cancellationToken);
        var decision = mode.Equals("DeviceDirection", StringComparison.OrdinalIgnoreCase)
            ? AttendanceSessionPlanner.DecideDeviceDirection(openSession, attendanceEvent.Direction, operationalEventTime, minCheckoutGap)
            : AttendanceSessionPlanner.DecideSingleDeviceToggle(openSession, operationalEventTime, minCheckoutGap);

        switch (decision.Type)
        {
            case AttendanceSessionDecisionType.CreateCheckIn:
                await CreateSessionAsync(attendanceEvent, workerExternalId, workDate, "Created attendance session check-in", cancellationToken);
                break;
            case AttendanceSessionDecisionType.CloseCheckOut when openSession is not null:
                await CloseSessionAsync(openSession, attendanceEvent, "Closed session only by confirmed exit/manual/auto end day", "DeviceDirection", cancellationToken);
                break;
            default:
                logger.LogInformation(
                    "Ignored event because checkout gap not reached or session action not needed. Event {EventId}, WorkerExternalId {WorkerExternalId}, Reason {Reason}",
                    attendanceEvent.Id,
                    workerExternalId,
                    decision.Reason);
                break;
        }
    }


    private async Task ProcessOneCameraPresenceSessionAsync(
        AttendanceEvent attendanceEvent,
        string workerExternalId,
        DateOnly workDate,
        CancellationToken cancellationToken)
    {
        var existingSession = await db.AttendanceSessions
            .Where(x => x.SiteId == attendanceEvent.SiteId
                        && x.DeviceId == attendanceEvent.DeviceId
                        && x.WorkerExternalId == workerExternalId
                        && x.WorkDate == workDate)
            .OrderByDescending(x => x.Status == AttendanceSessionStatus.Open)
            .ThenBy(x => x.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);

        var decision = AttendanceSessionPlanner.DecideOneCameraPresence(existingSession, AttendanceEventOperationalClock.Resolve(attendanceEvent));
        switch (decision.Type)
        {
            case AttendanceSessionDecisionType.CreateCheckIn:
                await CreateSessionAsync(attendanceEvent, workerExternalId, workDate, "Created daily presence session", cancellationToken);
                logger.LogInformation("Created daily presence session. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, CheckInTime {CheckInTime}", attendanceEvent.Id, workerExternalId, workDate, AttendanceEventOperationalClock.Resolve(attendanceEvent));
                break;
            case AttendanceSessionDecisionType.UpdateLastSeen when existingSession is not null:
                await UpdateLastSeenAsync(existingSession, attendanceEvent, cancellationToken);
                logger.LogInformation("Ignored checkout in one-camera presence mode. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}", attendanceEvent.Id, workerExternalId, workDate);
                break;
            default:
                logger.LogInformation("Ignored one-camera presence event. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, Reason {Reason}", attendanceEvent.Id, workerExternalId, workDate, decision.Reason);
                break;
        }
    }
    private async Task ProcessSingleDeviceDailySessionAsync(
        AttendanceEvent attendanceEvent,
        string workerExternalId,
        DateOnly workDate,
        TimeSpan minCheckoutGap,
        bool updateCheckoutToLastSeen,
        CancellationToken cancellationToken)
    {
        var existingSession = await db.AttendanceSessions
            .Where(x => x.SiteId == attendanceEvent.SiteId
                        && x.DeviceId == attendanceEvent.DeviceId
                        && x.WorkerExternalId == workerExternalId
                        && x.WorkDate == workDate)
            .OrderByDescending(x => x.Status == AttendanceSessionStatus.Open)
            .ThenBy(x => x.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);

        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(existingSession, AttendanceEventOperationalClock.Resolve(attendanceEvent), minCheckoutGap, updateCheckoutToLastSeen);
        switch (decision.Type)
        {
            case AttendanceSessionDecisionType.CreateCheckIn:
                await CreateSessionAsync(attendanceEvent, workerExternalId, workDate, "Created daily check-in session", cancellationToken);
                break;
            case AttendanceSessionDecisionType.CloseCheckOut when existingSession is not null:
                await CloseSessionAsync(existingSession, attendanceEvent, "Closed daily session as checkout", "DeviceDirection", cancellationToken);
                break;
            case AttendanceSessionDecisionType.UpdateCheckOut when existingSession is not null:
                await UpdateCheckOutToLastSeenAsync(existingSession, attendanceEvent, cancellationToken);
                break;
            default:
                if (decision.Reason.Contains("checkout gap", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Ignored event because checkout gap not reached. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}", attendanceEvent.Id, workerExternalId, workDate);
                }
                else if (decision.Reason.Contains("update disabled", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Ignored event because daily session already closed and update disabled. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}", attendanceEvent.Id, workerExternalId, workDate);
                }
                else
                {
                    logger.LogInformation("Ignored daily attendance event. Event {EventId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, Reason {Reason}", attendanceEvent.Id, workerExternalId, workDate, decision.Reason);
                }
                break;
        }
    }

    private Task<AttendanceSession?> FindOpenSessionAsync(Guid deviceId, string workerExternalId, DateOnly workDate, CancellationToken cancellationToken) =>
        db.AttendanceSessions
            .Where(x => x.DeviceId == deviceId
                        && x.WorkerExternalId == workerExternalId
                        && x.WorkDate == workDate
                        && x.Status == AttendanceSessionStatus.Open)
            .OrderByDescending(x => x.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task CreateSessionAsync(AttendanceEvent attendanceEvent, string workerExternalId, DateOnly workDate, string logMessage, CancellationToken cancellationToken)
    {
        var session = new AttendanceSession
        {
            SiteId = attendanceEvent.SiteId,
            DeviceId = attendanceEvent.DeviceId,
            WorkerId = attendanceEvent.WorkerId,
            WorkerExternalId = workerExternalId,
            WorkerName = attendanceEvent.WorkerName,
            WorkDate = workDate,
            CheckInEventId = attendanceEvent.Id,
            CheckInTime = AttendanceEventOperationalClock.Resolve(attendanceEvent),
            LastSeenEventId = attendanceEvent.Id,
            LastSeenTime = AttendanceEventOperationalClock.Resolve(attendanceEvent),
            PresenceStatus = "RegisteredToday",
            Status = AttendanceSessionStatus.Open,
            Source = attendanceEvent.Source,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.AttendanceSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "{LogMessage}. Session {SessionId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, CheckInTime {CheckInTime}",
                logMessage,
                session.Id,
                session.WorkerExternalId,
                session.WorkDate,
                session.CheckInTime);
        }
        catch (DbUpdateException ex) when (IsDuplicateException(ex))
        {
            db.Entry(session).State = EntityState.Detached;
            logger.LogInformation(ex, "Duplicate daily attendance session ignored. WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}", workerExternalId, workDate);
        }
    }

    private async Task CloseSessionAsync(AttendanceSession session, AttendanceEvent attendanceEvent, string logMessage, string closeReason, CancellationToken cancellationToken)
    {
        session.CheckOutEventId = attendanceEvent.Id;
        session.CheckOutTime = AttendanceEventOperationalClock.Resolve(attendanceEvent);
        session.LastSeenEventId = attendanceEvent.Id;
        session.LastSeenTime = AttendanceEventOperationalClock.Resolve(attendanceEvent);
        session.CloseReason = closeReason;
        session.PresenceStatus = "Closed";
        session.Status = AttendanceSessionStatus.Closed;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(attendanceEvent.WorkerName)) session.WorkerName = attendanceEvent.WorkerName;
        if (session.WorkerId is null && attendanceEvent.WorkerId is not null) session.WorkerId = attendanceEvent.WorkerId;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "{LogMessage}. Session {SessionId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, CheckOutTime {CheckOutTime}",
            logMessage,
            session.Id,
            session.WorkerExternalId,
            session.WorkDate,
            session.CheckOutTime);
    }

    private async Task UpdateLastSeenAsync(AttendanceSession session, AttendanceEvent attendanceEvent, CancellationToken cancellationToken)
    {
        session.LastSeenEventId = attendanceEvent.Id;
        var operationalEventTime = AttendanceEventOperationalClock.Resolve(attendanceEvent);
        session.LastSeenTime = operationalEventTime;
        session.PresenceStatus = DateTimeOffset.UtcNow - operationalEventTime <= TimeSpan.FromMinutes(15) ? "RecentlySeen" : "RegisteredToday";
        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(attendanceEvent.WorkerName)) session.WorkerName = attendanceEvent.WorkerName;
        if (session.WorkerId is null && attendanceEvent.WorkerId is not null) session.WorkerId = attendanceEvent.WorkerId;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updated last seen for worker. Session {SessionId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, LastSeenTime {LastSeenTime}",
            session.Id,
            session.WorkerExternalId,
            session.WorkDate,
            session.LastSeenTime);
    }

    private async Task UpdateCheckOutToLastSeenAsync(AttendanceSession session, AttendanceEvent attendanceEvent, CancellationToken cancellationToken)
    {
        session.CheckOutEventId = attendanceEvent.Id;
        session.CheckOutTime = AttendanceEventOperationalClock.Resolve(attendanceEvent);
        session.Status = AttendanceSessionStatus.Closed;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(attendanceEvent.WorkerName)) session.WorkerName = attendanceEvent.WorkerName;
        if (session.WorkerId is null && attendanceEvent.WorkerId is not null) session.WorkerId = attendanceEvent.WorkerId;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updated checkout to last seen. Session {SessionId}, WorkerExternalId {WorkerExternalId}, WorkDate {WorkDate}, CheckOutTime {CheckOutTime}",
            session.Id,
            session.WorkerExternalId,
            session.WorkDate,
            session.CheckOutTime);
    }

    private static bool IsValidSessionEvent(AttendanceEvent attendanceEvent) =>
        attendanceEvent.Status == AttendanceEventStatus.Ok
        && !string.IsNullOrWhiteSpace(attendanceEvent.WorkerExternalId)
        && attendanceEvent.Source.StartsWith("dahua_", StringComparison.OrdinalIgnoreCase);

    private static bool IsDuplicateException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (InvalidTimeZoneException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
    }
}

