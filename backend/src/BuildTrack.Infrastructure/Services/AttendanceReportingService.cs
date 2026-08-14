using System.Globalization;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Services;

public interface IAttendanceReportingService
{
    Task<AttendanceDailyRosterReport> BuildDailyRosterAsync(Guid? siteId, DateOnly? requestedDate, DateTimeOffset? nowOverride, CancellationToken ct);
    Task<AttendanceDisciplineReport> BuildDisciplineReportAsync(Guid? siteId, DateOnly? requestedDateFrom, DateOnly? requestedDateTo, DateTimeOffset? nowOverride, CancellationToken ct);
}

public sealed record AttendanceDailyRosterReport(
    DateOnly WorkDate,
    string PlannedStart,
    string PlannedEnd,
    int LateGraceMinutes,
    int EarlyExitGraceMinutes,
    int ActiveWorkersCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int EarlyExitCount,
    double TotalWorkedHours,
    double AttendancePercent,
    IReadOnlyList<AttendanceDailyRosterRow> Rows);

public sealed record AttendanceDailyRosterRow(
    string Key,
    Guid WorkerId,
    string WorkerExternalId,
    string WorkerName,
    Guid SiteId,
    string SiteName,
    string? Role,
    string? Brigade,
    string PlannedCheckIn,
    string PlannedCheckOut,
    DateTimeOffset? ActualCheckIn,
    string? ActualCheckInLocal,
    DateTimeOffset? ActualCheckOut,
    string? ActualCheckOutLocal,
    string Status,
    int LateMinutes,
    int EarlyExitMinutes,
    double WorkedHours,
    string EntryMethod,
    int RiskScore,
    string RiskLevel,
    string? Source);

public sealed record AttendanceDisciplineReport(
    DateOnly DateFrom,
    DateOnly DateTo,
    string PlannedStart,
    string PlannedEnd,
    int LateGraceMinutes,
    int EarlyExitGraceMinutes,
    int ScheduledWorkerDays,
    int PresentWorkerDays,
    int AbsentWorkerDays,
    int LateCount,
    int TotalLateMinutes,
    int EarlyExitCount,
    int TotalEarlyExitMinutes,
    int ApprovedPermissionDays,
    double ApprovedPermissionHours,
    double AttendancePercent,
    bool PermissionDomainAvailable,
    IReadOnlyList<AttendanceDisciplineRow> Rows,
    IReadOnlyList<AttendanceDisciplineTrendPoint> Trend);

public sealed record AttendanceDisciplineRow(
    string Key,
    Guid WorkerId,
    string WorkerExternalId,
    string WorkerName,
    Guid SiteId,
    string SiteName,
    string? Role,
    string? Brigade,
    int ScheduledDays,
    int PresentDays,
    int AbsentDays,
    int LateCount,
    int TotalLateMinutes,
    int EarlyExitCount,
    int TotalEarlyExitMinutes,
    int ApprovedPermissionDays,
    double ApprovedPermissionHours,
    double AttendancePercent,
    int RiskScore,
    string RiskLevel,
    string Trend,
    string Note);

public sealed record AttendanceDisciplineTrendPoint(
    string Key,
    DateOnly Date,
    string Label,
    int LateCount,
    int TotalLateMinutes,
    double LateHours,
    int EarlyExitCount);

public sealed class AttendanceReportingService(BuildTrackDbContext db) : IAttendanceReportingService
{
    private const string AbsentStatus = "Gəlməyib";
    private const string PresentStatus = "Gəlib";
    private const string LateStatus = "Gecikib";
    private const string SeedSource = "seed_bakinity_demo";

    public async Task<AttendanceDailyRosterReport> BuildDailyRosterAsync(Guid? siteId, DateOnly? requestedDate, DateTimeOffset? nowOverride, CancellationToken ct)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var (sites, timeZone) = await ResolveSitesAsync(siteId, ct);
        var siteIds = sites.Select(x => x.Id).ToArray();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var workDate = requestedDate ?? await ResolveDefaultWorkDateAsync(siteIds, today, ct);
        var workers = await LoadActiveWorkersAsync(siteId, ct);
        var siteNames = sites.ToDictionary(x => x.Id, x => x.Name);
        var sessions = await LoadSessionsAsync(siteIds, workDate, ct);
        sessions = await FilterVerifiedSessionsAsync(sessions, ct);
        var eventIds = sessions
            .SelectMany(x => new[] { (Guid?)x.CheckInEventId, x.CheckOutEventId, x.LastSeenEventId })
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var eventsById = eventIds.Length == 0
            ? new Dictionary<Guid, AttendanceEvent>()
            : await db.AttendanceEvents.AsNoTracking().Where(x => eventIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var fallbackEventsByWorkerId = await LoadFallbackEventsByWorkerAsync(siteIds, workDate, timeZone, workers, sessions, ct);
        var sessionsByWorkerId = sessions
            .Select(session => new { Session = session, Worker = ResolveSessionWorker(session, workers) })
            .Where(x => x.Worker is not null)
            .GroupBy(x => x.Worker!.Id)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Session).ToList());

        var rows = workers
            .OrderBy(x => siteNames.GetValueOrDefault(x.SiteId, string.Empty))
            .ThenBy(x => x.ExternalWorkerCode)
            .Select(worker =>
            {
                sessionsByWorkerId.TryGetValue(worker.Id, out var workerSessions);
                fallbackEventsByWorkerId.TryGetValue(worker.Id, out var workerEvents);
                return BuildDailyRow(worker, siteNames, workerSessions ?? [], workerEvents ?? [], eventsById, workDate, timeZone, now);
            })
            .ToArray();

        var activeWorkersCount = rows.Length;
        var presentCount = rows.Count(x => x.Status != AbsentStatus);
        var absentCount = activeWorkersCount - presentCount;
        var lateCount = rows.Count(x => x.LateMinutes > 0);
        var earlyExitCount = rows.Count(x => x.EarlyExitMinutes > 0);
        var totalWorkedHours = Math.Round(rows.Sum(x => x.WorkedHours), 2);
        var attendancePercent = activeWorkersCount == 0 ? 100 : Math.Round((double)presentCount / activeWorkersCount * 100, 1);

        return new AttendanceDailyRosterReport(
            workDate,
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedStart),
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedEnd),
            AttendanceSchedulePolicy.DefaultLateGraceMinutes,
            AttendanceSchedulePolicy.DefaultEarlyExitGraceMinutes,
            activeWorkersCount,
            presentCount,
            absentCount,
            lateCount,
            earlyExitCount,
            totalWorkedHours,
            attendancePercent,
            rows);
    }

    public async Task<AttendanceDisciplineReport> BuildDisciplineReportAsync(Guid? siteId, DateOnly? requestedDateFrom, DateOnly? requestedDateTo, DateTimeOffset? nowOverride, CancellationToken ct)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var (sites, timeZone) = await ResolveSitesAsync(siteId, ct);
        var siteIds = sites.Select(x => x.Id).ToArray();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var (dateFrom, dateTo) = await ResolveDefaultDisciplineRangeAsync(siteIds, today, requestedDateFrom, requestedDateTo, ct);
        if (dateFrom > dateTo)
        {
            (dateFrom, dateTo) = (dateTo, dateFrom);
        }

        var workerRows = new Dictionary<Guid, DisciplineAccumulator>();
        var trend = new List<AttendanceDisciplineTrendPoint>();
        var totalDays = Math.Min(366, dateTo.DayNumber - dateFrom.DayNumber + 1);

        for (var offset = 0; offset < totalDays; offset++)
        {
            var date = dateFrom.AddDays(offset);
            var daily = await BuildDailyRosterAsync(siteId, date, now, ct);
            var lateCount = 0;
            var totalLateMinutes = 0;
            var earlyExitCount = 0;

            foreach (var row in daily.Rows)
            {
                if (!workerRows.TryGetValue(row.WorkerId, out var accumulator))
                {
                    accumulator = new DisciplineAccumulator(row);
                    workerRows[row.WorkerId] = accumulator;
                }

                accumulator.ScheduledDays++;
                if (row.Status == AbsentStatus)
                {
                    accumulator.AbsentDays++;
                }
                else
                {
                    accumulator.PresentDays++;
                }

                if (row.LateMinutes > 0)
                {
                    accumulator.LateCount++;
                    accumulator.TotalLateMinutes += row.LateMinutes;
                    lateCount++;
                    totalLateMinutes += row.LateMinutes;
                }

                if (row.EarlyExitMinutes > 0)
                {
                    accumulator.EarlyExitCount++;
                    accumulator.TotalEarlyExitMinutes += row.EarlyExitMinutes;
                    earlyExitCount++;
                }
            }

            trend.Add(new AttendanceDisciplineTrendPoint(
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                date,
                date.ToString("dd.MM", CultureInfo.InvariantCulture),
                lateCount,
                totalLateMinutes,
                Math.Round(totalLateMinutes / 60d, 2),
                earlyExitCount));
        }

        var rows = workerRows.Values
            .Where(x => x.LateCount > 0 || x.EarlyExitCount > 0 || x.AbsentDays > 0)
            .OrderByDescending(x => x.LateCount + x.EarlyExitCount + x.AbsentDays)
            .ThenByDescending(x => x.TotalLateMinutes)
            .ThenBy(x => x.WorkerName)
            .Select(x => x.ToRow())
            .ToArray();

        var scheduledWorkerDays = workerRows.Values.Sum(x => x.ScheduledDays);
        var presentWorkerDays = workerRows.Values.Sum(x => x.PresentDays);
        var attendancePercent = scheduledWorkerDays == 0 ? 100 : Math.Round((double)presentWorkerDays / scheduledWorkerDays * 100, 1);

        return new AttendanceDisciplineReport(
            dateFrom,
            dateTo,
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedStart),
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedEnd),
            AttendanceSchedulePolicy.DefaultLateGraceMinutes,
            AttendanceSchedulePolicy.DefaultEarlyExitGraceMinutes,
            scheduledWorkerDays,
            presentWorkerDays,
            workerRows.Values.Sum(x => x.AbsentDays),
            workerRows.Values.Sum(x => x.LateCount),
            workerRows.Values.Sum(x => x.TotalLateMinutes),
            workerRows.Values.Sum(x => x.EarlyExitCount),
            workerRows.Values.Sum(x => x.TotalEarlyExitMinutes),
            0,
            0,
            attendancePercent,
            false,
            rows,
            trend);
    }

    private async Task<(IReadOnlyList<Site> Sites, TimeZoneInfo TimeZone)> ResolveSitesAsync(Guid? siteId, CancellationToken ct)
    {
        var query = db.Sites.AsNoTracking().OrderBy(x => x.Name);
        var sites = siteId is null
            ? await query.ToListAsync(ct)
            : await query.Where(x => x.Id == siteId.Value).ToListAsync(ct);

        if (siteId is not null && sites.Count == 0)
        {
            throw new KeyNotFoundException("Site was not found for the current tenant.");
        }

        var timeZone = AttendanceSchedulePolicy.ResolveTimeZone(sites.FirstOrDefault()?.TimeZone);
        return (sites, timeZone);
    }

    private async Task<DateOnly> ResolveDefaultWorkDateAsync(IReadOnlyCollection<Guid> siteIds, DateOnly today, CancellationToken ct)
    {
        var todayHasSessions = await db.AttendanceSessions.AsNoTracking()
            .AnyAsync(x => siteIds.Contains(x.SiteId) && x.WorkDate == today, ct);
        if (todayHasSessions) return today;

        var latestDemoSeedDate = await db.AttendanceSessions.AsNoTracking()
            .Where(x => siteIds.Contains(x.SiteId) && x.Source == SeedSource)
            .OrderByDescending(x => x.WorkDate)
            .Select(x => (DateOnly?)x.WorkDate)
            .FirstOrDefaultAsync(ct);

        return latestDemoSeedDate ?? today;
    }

    private async Task<(DateOnly DateFrom, DateOnly DateTo)> ResolveDefaultDisciplineRangeAsync(
        IReadOnlyCollection<Guid> siteIds,
        DateOnly today,
        DateOnly? requestedDateFrom,
        DateOnly? requestedDateTo,
        CancellationToken ct)
    {
        if (requestedDateFrom is not null || requestedDateTo is not null)
        {
            return (requestedDateFrom ?? requestedDateTo!.Value, requestedDateTo ?? requestedDateFrom!.Value);
        }

        var latestDemoSeedDate = await db.AttendanceSessions.AsNoTracking()
            .Where(x => siteIds.Contains(x.SiteId) && x.Source == SeedSource)
            .OrderByDescending(x => x.WorkDate)
            .Select(x => (DateOnly?)x.WorkDate)
            .FirstOrDefaultAsync(ct);

        if (latestDemoSeedDate is not null)
        {
            return (latestDemoSeedDate.Value.AddDays(-13), latestDemoSeedDate.Value);
        }

        return (new DateOnly(today.Year, today.Month, 1), today);
    }

    private async Task<List<Worker>> LoadActiveWorkersAsync(Guid? siteId, CancellationToken ct)
    {
        var query = db.Workers
            .AsNoTracking()
            .Include(x => x.CameraIdentities)
            .Include(x => x.SiteAssignments)
            .Where(x => x.Status == WorkerStatus.Active);
        if (siteId is not null)
        {
            query = query.Where(x => x.SiteId == siteId.Value
                                     || x.SiteAssignments.Any(assignment =>
                                         assignment.SiteId == siteId.Value
                                         && assignment.Status == WorkerSiteAssignmentStatus.Active));
        }

        return await query
            .OrderBy(x => x.ExternalWorkerCode)
            .ToListAsync(ct);
    }

    private async Task<List<AttendanceSession>> LoadSessionsAsync(IReadOnlyCollection<Guid> siteIds, DateOnly workDate, CancellationToken ct) =>
        await db.AttendanceSessions.AsNoTracking()
            .Where(x => siteIds.Contains(x.SiteId) && x.WorkDate == workDate)
            .OrderBy(x => x.CheckInTime)
            .ToListAsync(ct);

    private async Task<List<AttendanceSession>> FilterVerifiedSessionsAsync(IReadOnlyCollection<AttendanceSession> sessions, CancellationToken ct)
    {
        if (sessions.Count == 0) return [];

        var activeRegisterEventIds = sessions
            .Where(x => string.Equals(x.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => new[] { (Guid?)x.CheckInEventId, x.CheckOutEventId, x.LastSeenEventId })
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (activeRegisterEventIds.Length == 0) return sessions.ToList();

        var events = await db.AttendanceEvents.AsNoTracking()
            .Where(x => activeRegisterEventIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        return sessions
            .Where(session =>
            {
                if (!string.Equals(session.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)) return true;

                var linkedEventIds = new[] { (Guid?)session.CheckInEventId, session.CheckOutEventId, session.LastSeenEventId }
                    .Where(x => x is not null)
                    .Select(x => x!.Value)
                    .ToArray();
                return linkedEventIds.Length > 0
                       && linkedEventIds.All(id => events.TryGetValue(id, out var attendanceEvent)
                                                    && DahuaVerifiedAttendancePayload.IsVerifiedAttendance(attendanceEvent));
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, List<AttendanceEvent>>> LoadFallbackEventsByWorkerAsync(
        IReadOnlyCollection<Guid> siteIds,
        DateOnly workDate,
        TimeZoneInfo timeZone,
        IReadOnlyList<Worker> workers,
        IReadOnlyCollection<AttendanceSession> sessions,
        CancellationToken ct)
    {
        var (dayStartUtc, dayEndUtc) = GetUtcRangeForWorkDate(workDate, timeZone);
        var events = await db.AttendanceEvents.AsNoTracking()
            .Where(x => siteIds.Contains(x.SiteId)
                        && x.Status == AttendanceEventStatus.Ok
                        && x.WorkerExternalId != null
                        && ((x.Source == DahuaEventSourceExtensions.ActiveRegisterSource
                             && x.CreatedAt >= dayStartUtc
                             && x.CreatedAt < dayEndUtc)
                            || (x.Source != DahuaEventSourceExtensions.ActiveRegisterSource
                                && x.EventTime >= dayStartUtc
                                && x.EventTime < dayEndUtc)))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var workerIdsWithSessions = sessions
            .Select(session => ResolveSessionWorker(session, workers)?.Id)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToHashSet();

        var result = new Dictionary<Guid, List<AttendanceEvent>>();
        foreach (var attendanceEvent in events)
        {
            if (!DahuaVerifiedAttendancePayload.IsVerifiedAttendance(attendanceEvent)) continue;

            var worker = ResolveEventWorker(attendanceEvent, workers);
            if (worker is null || workerIdsWithSessions.Contains(worker.Id)) continue;
            if (!result.TryGetValue(worker.Id, out var workerEvents))
            {
                workerEvents = [];
                result[worker.Id] = workerEvents;
            }

            workerEvents.Add(attendanceEvent);
        }

        return result;
    }

    private static AttendanceDailyRosterRow BuildDailyRow(
        Worker worker,
        IReadOnlyDictionary<Guid, string> siteNames,
        IReadOnlyList<AttendanceSession> sessions,
        IReadOnlyList<AttendanceEvent> fallbackEvents,
        IReadOnlyDictionary<Guid, AttendanceEvent> eventsById,
        DateOnly workDate,
        TimeZoneInfo timeZone,
        DateTimeOffset now)
    {
        if (sessions.Count == 0 && fallbackEvents.Count == 0)
        {
            return new AttendanceDailyRosterRow(
                worker.Id.ToString("N"),
                worker.Id,
                worker.ExternalWorkerCode,
                worker.FullName,
                worker.SiteId,
                siteNames.GetValueOrDefault(worker.SiteId, "Naməlum layihə"),
                worker.Role,
                worker.Brigade,
                AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedStart),
                AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedEnd),
                null,
                null,
                null,
                null,
                AbsentStatus,
                0,
                0,
                0,
                "—",
                worker.RiskScore,
                RiskLevel(worker.RiskScore),
                null);
        }

        if (sessions.Count == 0)
        {
            var orderedEvents = fallbackEvents.OrderBy(AttendanceEventOperationalClock.Resolve).ToArray();
            var first = orderedEvents[0];
            var last = orderedEvents[^1];
            var firstSeen = AttendanceEventOperationalClock.Resolve(first);
            var fallbackLastSeen = AttendanceEventOperationalClock.Resolve(last);
            var lateMinutes = AttendanceSchedulePolicy.CalculateLateMinutes(firstSeen, workDate, timeZone);
            return new AttendanceDailyRosterRow(
                worker.Id.ToString("N"),
                worker.Id,
                worker.ExternalWorkerCode,
                worker.FullName,
                worker.SiteId,
                siteNames.GetValueOrDefault(worker.SiteId, "Naməlum layihə"),
                worker.Role,
                worker.Brigade,
                AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedStart),
                AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedEnd),
                firstSeen,
                FormatTime(firstSeen, timeZone),
                null,
                null,
                lateMinutes > 0 ? LateStatus : PresentStatus,
                lateMinutes,
                0,
                Math.Round(Math.Max(0, (fallbackLastSeen - firstSeen).TotalHours), 2),
                MethodLabel(first.Method),
                worker.RiskScore,
                RiskLevel(worker.RiskScore),
                first.Source);
        }

        var orderedSessions = sessions.OrderBy(x => x.CheckInTime).ToArray();
        var firstSession = orderedSessions[0];
        var checkIn = orderedSessions.Min(x => x.CheckInTime);
        var lastSeen = orderedSessions.Select(x => x.LastSeenTime ?? x.CheckOutTime ?? x.CheckInTime).Max();
        var checkoutSession = orderedSessions
            .Where(AttendanceSchedulePolicy.IsCheckoutConfirmed)
            .OrderByDescending(x => x.CheckOutTime)
            .FirstOrDefault();
        var confirmedCheckout = checkoutSession?.CheckOutTime;
        var isOpen = orderedSessions.Any(x => x.Status == AttendanceSessionStatus.Open);
        var effectiveEnd = confirmedCheckout ?? (isOpen ? now : lastSeen);
        var late = AttendanceSchedulePolicy.CalculateLateMinutes(checkIn, workDate, timeZone);
        var earlyExit = AttendanceSchedulePolicy.CalculateEarlyExitMinutes(confirmedCheckout, workDate, timeZone);
        var checkInEvent = eventsById.GetValueOrDefault(firstSession.CheckInEventId);

        return new AttendanceDailyRosterRow(
            worker.Id.ToString("N"),
            worker.Id,
            worker.ExternalWorkerCode,
            worker.FullName,
            worker.SiteId,
            siteNames.GetValueOrDefault(worker.SiteId, "Naməlum layihə"),
            worker.Role,
            worker.Brigade,
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedStart),
            AttendanceSchedulePolicy.FormatPlannedTime(AttendanceSchedulePolicy.DefaultPlannedEnd),
            checkIn,
            FormatTime(checkIn, timeZone),
            confirmedCheckout,
            confirmedCheckout is null ? null : FormatTime(confirmedCheckout.Value, timeZone),
            late > 0 ? LateStatus : PresentStatus,
            late,
            earlyExit,
            Math.Round(Math.Max(0, (effectiveEnd - checkIn).TotalHours), 2),
            checkInEvent is null ? SourceLabel(firstSession.Source) : MethodLabel(checkInEvent.Method),
            worker.RiskScore,
            RiskLevel(worker.RiskScore),
            firstSession.Source);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetUtcRangeForWorkDate(DateOnly workDate, TimeZoneInfo timeZone)
    {
        var startLocal = workDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = workDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone)));
    }

    private static string FormatTime(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string MethodLabel(AttendanceMethod method) => method switch
    {
        AttendanceMethod.Face => "Üz tanıma",
        AttendanceMethod.Card => "Kart",
        AttendanceMethod.Fingerprint => "Barmaq izi",
        AttendanceMethod.Password => "Şifrə",
        AttendanceMethod.Manual => "Manual",
        _ => "Naməlum",
    };

    private static string SourceLabel(string source) =>
        string.Equals(source, SeedSource, StringComparison.OrdinalIgnoreCase) ? "Demo kamera" : source;

    private static string RiskLevel(int score) => score switch
    {
        >= 80 => "Kritik",
        >= 60 => "Yüksək",
        >= 40 => "Orta",
        _ => "Aşağı",
    };

    private static Worker? ResolveSessionWorker(AttendanceSession session, IReadOnlyList<Worker> workers)
    {
        if (session.WorkerId is not null)
        {
            var byId = workers.FirstOrDefault(x => x.Id == session.WorkerId.Value);
            if (byId is not null) return byId;
        }

        return workers.FirstOrDefault(worker =>
            worker.SiteId == session.SiteId
            && WorkerSessionKeys(worker).Contains(session.WorkerExternalId, StringComparer.OrdinalIgnoreCase));
    }

    private static Worker? ResolveEventWorker(AttendanceEvent attendanceEvent, IReadOnlyList<Worker> workers)
    {
        if (attendanceEvent.WorkerId is not null)
        {
            var byId = workers.FirstOrDefault(x => x.Id == attendanceEvent.WorkerId.Value);
            if (byId is not null) return byId;
        }

        if (string.IsNullOrWhiteSpace(attendanceEvent.WorkerExternalId)) return null;
        return workers.FirstOrDefault(worker =>
            worker.SiteId == attendanceEvent.SiteId
            && WorkerSessionKeys(worker).Contains(attendanceEvent.WorkerExternalId, StringComparer.OrdinalIgnoreCase));
    }

    private static string[] WorkerSessionKeys(Worker worker) =>
        new[] { worker.ExternalWorkerCode }
            .Concat(worker.CameraIdentities.SelectMany(identity => new[] { identity.ExternalUserId, identity.CardName, identity.NormalizedCardName }))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed class DisciplineAccumulator
    {
        public DisciplineAccumulator(AttendanceDailyRosterRow firstRow)
        {
            WorkerId = firstRow.WorkerId;
            WorkerExternalId = firstRow.WorkerExternalId;
            WorkerName = firstRow.WorkerName;
            SiteId = firstRow.SiteId;
            SiteName = firstRow.SiteName;
            Role = firstRow.Role;
            Brigade = firstRow.Brigade;
            RiskScore = firstRow.RiskScore;
            RiskLevel = firstRow.RiskLevel;
        }

        public Guid WorkerId { get; }
        public string WorkerExternalId { get; }
        public string WorkerName { get; }
        public Guid SiteId { get; }
        public string SiteName { get; }
        public string? Role { get; }
        public string? Brigade { get; }
        public int ScheduledDays { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LateCount { get; set; }
        public int TotalLateMinutes { get; set; }
        public int EarlyExitCount { get; set; }
        public int TotalEarlyExitMinutes { get; set; }
        public int RiskScore { get; }
        public string RiskLevel { get; }

        public AttendanceDisciplineRow ToRow()
        {
            var attendancePercent = ScheduledDays == 0 ? 100 : Math.Round((double)PresentDays / ScheduledDays * 100, 1);
            var notes = new List<string>();
            if (AbsentDays > 0) notes.Add($"{AbsentDays} gün gəlməyib");
            if (LateCount > 0) notes.Add($"{LateCount} gecikmə");
            if (EarlyExitCount > 0) notes.Add($"{EarlyExitCount} erkən çıxış");
            var trend = LateCount + EarlyExitCount + AbsentDays == 0 ? "Sabit" : "Diqqət";

            return new AttendanceDisciplineRow(
                WorkerId.ToString("N"),
                WorkerId,
                WorkerExternalId,
                WorkerName,
                SiteId,
                SiteName,
                Role,
                Brigade,
                ScheduledDays,
                PresentDays,
                AbsentDays,
                LateCount,
                TotalLateMinutes,
                EarlyExitCount,
                TotalEarlyExitMinutes,
                0,
                0,
                attendancePercent,
                RiskScore,
                RiskLevel,
                trend,
                notes.Count == 0 ? "Qeyd yoxdur" : string.Join(", ", notes));
        }
    }
}
