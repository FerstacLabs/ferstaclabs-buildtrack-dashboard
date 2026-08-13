using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Tests;

public sealed class AttendanceReportingServiceTests
{
    [Fact]
    public async Task DailyRosterUsesCanonicalSessionsAndIncludesAbsentWorkers()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var date = new DateOnly(2026, 8, 12);
        var siteA = AddSite(db, tenantId, "Blok A");
        var siteB = AddSite(db, tenantId, "Blok B");
        var lateWorker = AddWorker(db, tenantId, siteA.Id, "W-001", "Late Worker");
        AddWorker(db, tenantId, siteA.Id, "W-002", "Absent Worker");
        var normalWorker = AddWorker(db, tenantId, siteB.Id, "W-003", "Normal Worker");
        var deviceA = AddDevice(db, tenantId, siteA.Id, "CAM-A");
        var deviceB = AddDevice(db, tenantId, siteB.Id, "CAM-B");
        AddClosedSession(db, tenantId, siteA.Id, deviceA.Id, lateWorker, date, new TimeOnly(8, 23), new TimeOnly(17, 10));
        AddClosedSession(db, tenantId, siteB.Id, deviceB.Id, normalWorker, date, new TimeOnly(7, 52), new TimeOnly(18, 4));
        await db.SaveChangesAsync();

        var service = new AttendanceReportingService(db);
        var siteReport = await service.BuildDailyRosterAsync(siteA.Id, date, AttendanceSchedulePolicy.ToUtc(date, new TimeOnly(20, 0), AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku")), CancellationToken.None);
        var allReport = await service.BuildDailyRosterAsync(null, date, AttendanceSchedulePolicy.ToUtc(date, new TimeOnly(20, 0), AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku")), CancellationToken.None);

        Assert.Equal(2, siteReport.ActiveWorkersCount);
        Assert.Equal(1, siteReport.PresentCount);
        Assert.Equal(1, siteReport.AbsentCount);
        Assert.Equal(1, siteReport.LateCount);
        Assert.Equal(1, siteReport.EarlyExitCount);
        Assert.Contains(siteReport.Rows, row => row.WorkerExternalId == "W-002" && row.Status == "Gəlməyib");
        Assert.Contains(siteReport.Rows, row => row.WorkerExternalId == "W-001" && row.LateMinutes == 13 && row.EarlyExitMinutes == 40);
        Assert.Equal(3, allReport.ActiveWorkersCount);
        Assert.Equal(2, allReport.PresentCount);
        Assert.Equal(1, allReport.AbsentCount);
    }

    [Fact]
    public async Task DisciplineReportUsesRealTimeMathAndNotRiskScore()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var date = new DateOnly(2026, 8, 12);
        var site = AddSite(db, tenantId, "Villa");
        var riskyButOnTime = AddWorker(db, tenantId, site.Id, "W-010", "Risky But On Time", riskScore: 95);
        var device = AddDevice(db, tenantId, site.Id, "CAM-010");
        AddClosedSession(db, tenantId, site.Id, device.Id, riskyButOnTime, date, new TimeOnly(8, 2), new TimeOnly(18, 11));
        await db.SaveChangesAsync();

        var service = new AttendanceReportingService(db);
        var report = await service.BuildDisciplineReportAsync(site.Id, date, date, AttendanceSchedulePolicy.ToUtc(date, new TimeOnly(20, 0), AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku")), CancellationToken.None);

        Assert.Equal(1, report.ScheduledWorkerDays);
        Assert.Equal(1, report.PresentWorkerDays);
        Assert.Equal(0, report.LateCount);
        Assert.Equal(0, report.TotalLateMinutes);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task TenantFilterPreventsCrossTenantAttendanceAggregation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        var date = new DateOnly(2026, 8, 12);
        var siteA = AddSite(db, tenantA, "Tenant A Site");
        var siteB = AddSite(db, tenantB, "Tenant B Site");
        var workerA = AddWorker(db, tenantA, siteA.Id, "A-001", "Tenant A Worker");
        var workerB = AddWorker(db, tenantB, siteB.Id, "B-001", "Tenant B Worker");
        var deviceA = AddDevice(db, tenantA, siteA.Id, "CAM-A");
        var deviceB = AddDevice(db, tenantB, siteB.Id, "CAM-B");
        AddClosedSession(db, tenantA, siteA.Id, deviceA.Id, workerA, date, new TimeOnly(8, 2), new TimeOnly(18, 0));
        AddClosedSession(db, tenantB, siteB.Id, deviceB.Id, workerB, date, new TimeOnly(8, 37), new TimeOnly(18, 0));
        await db.SaveChangesAsync();

        var service = new AttendanceReportingService(db);
        var report = await service.BuildDailyRosterAsync(null, date, AttendanceSchedulePolicy.ToUtc(date, new TimeOnly(20, 0), AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku")), CancellationToken.None);

        Assert.Single(report.Rows);
        Assert.Equal("A-001", report.Rows[0].WorkerExternalId);
        Assert.Equal(0, report.LateCount);
    }

    private static BuildTrackDbContext CreateDb(Guid tenantId)
    {
        var context = new TenantContext { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, context);
    }

    private static Site AddSite(BuildTrackDbContext db, Guid tenantId, string name)
    {
        var site = new Site
        {
            TenantId = tenantId,
            Name = name,
            Address = "Test address",
            TimeZone = "Asia/Baku",
        };
        db.Sites.Add(site);
        return site;
    }

    private static Worker AddWorker(BuildTrackDbContext db, Guid tenantId, Guid siteId, string code, string name, int riskScore = 0)
    {
        var worker = new Worker
        {
            TenantId = tenantId,
            SiteId = siteId,
            ExternalWorkerCode = code,
            FullName = name,
            Brigade = "Test briqada",
            Role = "Test usta",
            HourlyRate = 5,
            RiskScore = riskScore,
            Status = WorkerStatus.Active,
            AttendanceSource = "Camera",
        };
        db.Workers.Add(worker);
        db.WorkerSiteAssignments.Add(new WorkerSiteAssignment
        {
            TenantId = tenantId,
            Worker = worker,
            SiteId = siteId,
            IsPrimary = true,
            Status = WorkerSiteAssignmentStatus.Active,
        });
        return worker;
    }

    private static Device AddDevice(BuildTrackDbContext db, Guid tenantId, Guid siteId, string registerId)
    {
        var device = new Device
        {
            TenantId = tenantId,
            SiteId = siteId,
            Name = registerId,
            Vendor = "dahua",
            Model = "DHI-ASI6213J-MW",
            Mode = DeviceMode.Simulator,
            RegisterDeviceId = registerId,
            Username = "admin",
            EncryptedPassword = "test",
        };
        db.Devices.Add(device);
        return device;
    }

    private static void AddClosedSession(
        BuildTrackDbContext db,
        Guid tenantId,
        Guid siteId,
        Guid deviceId,
        Worker worker,
        DateOnly date,
        TimeOnly checkIn,
        TimeOnly checkOut)
    {
        var timeZone = AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku");
        var checkInUtc = AttendanceSchedulePolicy.ToUtc(date, checkIn, timeZone);
        var checkOutUtc = AttendanceSchedulePolicy.ToUtc(date, checkOut, timeZone);
        var entryEvent = new AttendanceEvent
        {
            TenantId = tenantId,
            SiteId = siteId,
            DeviceId = deviceId,
            Worker = worker,
            WorkerExternalId = worker.ExternalWorkerCode,
            WorkerName = worker.FullName,
            EventTime = checkInUtc,
            CreatedAt = checkInUtc,
            Direction = AttendanceDirection.Entry,
            Status = AttendanceEventStatus.Ok,
            Method = AttendanceMethod.Face,
            Source = "unit_test",
        };
        var exitEvent = new AttendanceEvent
        {
            TenantId = tenantId,
            SiteId = siteId,
            DeviceId = deviceId,
            Worker = worker,
            WorkerExternalId = worker.ExternalWorkerCode,
            WorkerName = worker.FullName,
            EventTime = checkOutUtc,
            CreatedAt = checkOutUtc,
            Direction = AttendanceDirection.Exit,
            Status = AttendanceEventStatus.Ok,
            Method = AttendanceMethod.Face,
            Source = "unit_test",
        };
        db.AttendanceEvents.AddRange(entryEvent, exitEvent);
        db.AttendanceSessions.Add(new AttendanceSession
        {
            TenantId = tenantId,
            SiteId = siteId,
            DeviceId = deviceId,
            Worker = worker,
            WorkerExternalId = worker.ExternalWorkerCode,
            WorkerName = worker.FullName,
            WorkDate = date,
            CheckInEvent = entryEvent,
            CheckInTime = checkInUtc,
            CheckOutEvent = exitEvent,
            CheckOutTime = checkOutUtc,
            LastSeenEvent = exitEvent,
            LastSeenTime = checkOutUtc,
            CloseReason = "DeviceDirection",
            PresenceStatus = "Closed",
            Status = AttendanceSessionStatus.Closed,
            Source = "unit_test",
        });
    }
}
