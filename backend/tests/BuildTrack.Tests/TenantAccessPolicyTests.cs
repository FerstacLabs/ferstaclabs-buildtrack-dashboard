using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Tests;

public sealed class TenantAccessPolicyTests
{
    [Fact]
    public void MatchingTenantCanAccessEntity()
    {
        var tenantId = Guid.NewGuid();

        Assert.True(TenantAccessPolicy.CanAccessTenant(tenantId, tenantId));
    }

    [Fact]
    public void DifferentTenantCannotAccessEntity()
    {
        Assert.False(TenantAccessPolicy.CanAccessTenant(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void EmptyTenantContextIsAllowedForWorkerProcesses()
    {
        Assert.True(TenantAccessPolicy.CanAccessTenant(null, Guid.NewGuid()));
    }

    [Fact]
    public async Task TenantQueryFilters_HideOtherTenantSitesDevicesAttendanceAndSecurityEvents()
    {
        var tenantContext = new TenantContext();
        await using var db = CreateDbContext(tenantContext);
        var ids = await SeedTenantIsolationDataAsync(db);

        tenantContext.TenantId = ids.GoldTenantId;

        Assert.Equal(["GOLD PALACE"], await db.Sites.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(["GOLD PALACE TERMINAL"], await db.Devices.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(["ilham"], await db.AttendanceEvents.OrderBy(x => x.WorkerName).Select(x => x.WorkerName!).ToArrayAsync());
        Assert.Equal(["ilham"], await db.AttendanceSessions.OrderBy(x => x.WorkerName).Select(x => x.WorkerName!).ToArrayAsync());
        Assert.Equal(["GOLD security"], await db.SecurityEvents.OrderBy(x => x.Message).Select(x => x.Message!).ToArrayAsync());
        Assert.Equal(["BT-GOLDMMC-CAM001"], await db.DahuaActiveRegisterRawEvents.OrderBy(x => x.RegisterDeviceId).Select(x => x.RegisterDeviceId!).ToArrayAsync());
        Assert.Equal(["GOLD report"], await db.SupervisorDailyReports.OrderBy(x => x.GeneralNote).Select(x => x.GeneralNote!).ToArrayAsync());
        Assert.Equal(["GOLD line"], await db.SupervisorDailyReportLines.OrderBy(x => x.Note).Select(x => x.Note!).ToArrayAsync());

        tenantContext.TenantId = ids.DemoTenantId;

        Assert.Equal(["API Test Layihəsi"], await db.Sites.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(["API TEST TERMINAL"], await db.Devices.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(["demo worker"], await db.AttendanceEvents.OrderBy(x => x.WorkerName).Select(x => x.WorkerName!).ToArrayAsync());
        Assert.Equal(["demo worker"], await db.AttendanceSessions.OrderBy(x => x.WorkerName).Select(x => x.WorkerName!).ToArrayAsync());
        Assert.Equal(["DEMO security"], await db.SecurityEvents.OrderBy(x => x.Message).Select(x => x.Message!).ToArrayAsync());
        Assert.Equal(["BT-API-TEST-001"], await db.DahuaActiveRegisterRawEvents.OrderBy(x => x.RegisterDeviceId).Select(x => x.RegisterDeviceId!).ToArrayAsync());
        Assert.Equal(["DEMO report"], await db.SupervisorDailyReports.OrderBy(x => x.GeneralNote).Select(x => x.GeneralNote!).ToArrayAsync());
        Assert.Equal(["DEMO line"], await db.SupervisorDailyReportLines.OrderBy(x => x.Note).Select(x => x.Note!).ToArrayAsync());
    }

    private static BuildTrackDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, tenantContext);
    }

    private static async Task<TenantIsolationIds> SeedTenantIsolationDataAsync(BuildTrackDbContext db)
    {
        var demoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var goldTenantId = Guid.Parse("ad023503-a986-4b64-b7d1-565817a431fc");
        var demoSiteId = Guid.NewGuid();
        var goldSiteId = Guid.Parse("07749990-3211-42f8-b978-296a6490c5fd");
        var demoDeviceId = Guid.NewGuid();
        var goldDeviceId = Guid.Parse("40cdf585-f528-42d7-ada0-d5e5823356a6");
        var demoEventId = Guid.NewGuid();
        var goldEventId = Guid.NewGuid();
        var demoSupervisorId = Guid.NewGuid();
        var goldSupervisorId = Guid.NewGuid();
        var demoSmetaItemId = Guid.NewGuid();
        var goldSmetaItemId = Guid.NewGuid();
        var demoReportId = Guid.NewGuid();
        var goldReportId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var workDate = DateOnly.FromDateTime(now.UtcDateTime);

        db.Tenants.AddRange(
            new Tenant { Id = demoTenantId, CompanyName = "FerstacLabs Demo", Code = "DEMO", Status = TenantStatus.Active },
            new Tenant { Id = goldTenantId, CompanyName = "GOLD MMC", Code = "GOLDMMC", Status = TenantStatus.Active });

        db.Sites.AddRange(
            new Site { Id = demoSiteId, TenantId = demoTenantId, Name = "API Test Layihəsi" },
            new Site { Id = goldSiteId, TenantId = goldTenantId, Name = "GOLD PALACE" });

        db.Users.AddRange(
            new AppUser
            {
                Id = demoSupervisorId,
                TenantId = demoTenantId,
                FullName = "Demo Supervisor",
                Email = "demo.supervisor@example.test",
                PasswordHash = "hash",
                Role = BuildTrackUserRole.Supervisor,
                Status = BuildTrackUserStatus.Active,
            },
            new AppUser
            {
                Id = goldSupervisorId,
                TenantId = goldTenantId,
                FullName = "Gold Supervisor",
                Email = "gold.supervisor@example.test",
                PasswordHash = "hash",
                Role = BuildTrackUserRole.Supervisor,
                Status = BuildTrackUserStatus.Active,
            });

        db.Devices.AddRange(
            new Device
            {
                Id = demoDeviceId,
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                Name = "API TEST TERMINAL",
                RegisterDeviceId = "BT-API-TEST-001",
                RegisterPort = 7000,
                Username = "admin",
                EncryptedPassword = "encrypted",
            },
            new Device
            {
                Id = goldDeviceId,
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                Name = "GOLD PALACE TERMINAL",
                RegisterDeviceId = "BT-GOLDMMC-CAM001",
                RegisterPort = 7000,
                Username = "admin",
                EncryptedPassword = "encrypted",
            });

        db.AttendanceEvents.AddRange(
            new AttendanceEvent
            {
                Id = demoEventId,
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                DeviceId = demoDeviceId,
                WorkerExternalId = "1",
                WorkerName = "demo worker",
                EventTime = now,
                Direction = AttendanceDirection.Entry,
                Status = AttendanceEventStatus.Ok,
                Method = AttendanceMethod.Face,
                Source = "dahua_active_register",
            },
            new AttendanceEvent
            {
                Id = goldEventId,
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                DeviceId = goldDeviceId,
                WorkerExternalId = "1",
                WorkerName = "ilham",
                EventTime = now,
                Direction = AttendanceDirection.Entry,
                Status = AttendanceEventStatus.Ok,
                Method = AttendanceMethod.Face,
                Source = "dahua_active_register",
            });

        db.AttendanceSessions.AddRange(
            new AttendanceSession
            {
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                DeviceId = demoDeviceId,
                WorkerExternalId = "1",
                WorkerName = "demo worker",
                WorkDate = workDate,
                CheckInEventId = demoEventId,
                CheckInTime = now,
                LastSeenEventId = demoEventId,
                LastSeenTime = now,
                Status = AttendanceSessionStatus.Open,
                Source = "dahua_active_register",
            },
            new AttendanceSession
            {
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                DeviceId = goldDeviceId,
                WorkerExternalId = "1",
                WorkerName = "ilham",
                WorkDate = workDate,
                CheckInEventId = goldEventId,
                CheckInTime = now,
                LastSeenEventId = goldEventId,
                LastSeenTime = now,
                Status = AttendanceSessionStatus.Open,
                Source = "dahua_active_register",
            });

        db.SecurityEvents.AddRange(
            new SecurityEvent
            {
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                DeviceId = demoDeviceId,
                EventTime = now,
                EventDate = workDate,
                EventType = SecurityEventType.UnknownFace,
                Severity = SecurityEventSeverity.Warning,
                Status = SecurityEventStatus.Open,
                Message = "DEMO security",
                Source = "dahua_active_register",
            },
            new SecurityEvent
            {
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                DeviceId = goldDeviceId,
                EventTime = now,
                EventDate = workDate,
                EventType = SecurityEventType.UnknownFace,
                Severity = SecurityEventSeverity.Warning,
                Status = SecurityEventStatus.Open,
                Message = "GOLD security",
                Source = "dahua_active_register",
            });

        db.DahuaActiveRegisterRawEvents.AddRange(
            new DahuaActiveRegisterRawEvent
            {
                TenantId = demoTenantId,
                DeviceId = demoDeviceId,
                RegisterDeviceId = "BT-API-TEST-001",
                ListenerPort = 7000,
                CallbackCommand = 5,
                PayloadBytes = 10,
            },
            new DahuaActiveRegisterRawEvent
            {
                TenantId = goldTenantId,
                DeviceId = goldDeviceId,
                RegisterDeviceId = "BT-GOLDMMC-CAM001",
                ListenerPort = 7000,
                CallbackCommand = 5,
                PayloadBytes = 10,
            });

        db.FieldSmetaItems.AddRange(
            new FieldSmetaItem
            {
                Id = demoSmetaItemId,
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                StageName = "Demo stage",
                WorkName = "Demo work",
                Unit = "m2",
            },
            new FieldSmetaItem
            {
                Id = goldSmetaItemId,
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                StageName = "Gold stage",
                WorkName = "Gold work",
                Unit = "m2",
            });

        db.SupervisorDailyReports.AddRange(
            new SupervisorDailyReport
            {
                Id = demoReportId,
                TenantId = demoTenantId,
                SiteId = demoSiteId,
                SupervisorUserId = demoSupervisorId,
                ReportDate = workDate,
                Status = FieldDailyReportStatus.Submitted,
                GeneralNote = "DEMO report",
            },
            new SupervisorDailyReport
            {
                Id = goldReportId,
                TenantId = goldTenantId,
                SiteId = goldSiteId,
                SupervisorUserId = goldSupervisorId,
                ReportDate = workDate,
                Status = FieldDailyReportStatus.Submitted,
                GeneralNote = "GOLD report",
            });

        db.SupervisorDailyReportLines.AddRange(
            new SupervisorDailyReportLine
            {
                TenantId = demoTenantId,
                ReportId = demoReportId,
                SmetaItemId = demoSmetaItemId,
                ReportedQuantity = 1,
                Unit = "m2",
                Note = "DEMO line",
            },
            new SupervisorDailyReportLine
            {
                TenantId = goldTenantId,
                ReportId = goldReportId,
                SmetaItemId = goldSmetaItemId,
                ReportedQuantity = 1,
                Unit = "m2",
                Note = "GOLD line",
            });

        await db.SaveChangesAsync();
        return new TenantIsolationIds(demoTenantId, goldTenantId);
    }

    private sealed record TenantIsolationIds(Guid DemoTenantId, Guid GoldTenantId);
}
