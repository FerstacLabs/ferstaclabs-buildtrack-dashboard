using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class AttendanceIngestionServiceTests
{
    [Fact]
    public async Task VerifiedActiveRegisterAttendanceAutoResolvesParserUncertainEventForSameTenantDeviceAndUser()
    {
        await using var db = CreateDbContext();
        var ids = await SeedDeviceWorkerAndParserEventAsync(db);
        var service = CreateService(db);

        var inserted = await service.IngestDahuaRecordAsync(
            ids.DeviceId,
            VerifiedAttendanceRecord(),
            source: DahuaEventSourceExtensions.ActiveRegisterSource,
            requireSuccessfulAttendance: true);

        var securityEvent = await db.SecurityEvents.SingleAsync(x => x.Id == ids.SecurityEventId);
        Assert.NotNull(inserted);
        Assert.Equal(SecurityEventStatus.AutoResolved, securityEvent.Status);
        Assert.NotNull(securityEvent.ReviewedAt);
        Assert.Contains("Avtomatik", securityEvent.ReviewNote);
    }

    [Fact]
    public async Task VerifiedActiveRegisterAttendanceDoesNotAutoResolveUnknownFaceEvent()
    {
        await using var db = CreateDbContext();
        var ids = await SeedDeviceWorkerAndParserEventAsync(db, SecurityEventType.UnknownFace);
        var service = CreateService(db);

        await service.IngestDahuaRecordAsync(
            ids.DeviceId,
            VerifiedAttendanceRecord(),
            source: DahuaEventSourceExtensions.ActiveRegisterSource,
            requireSuccessfulAttendance: true);

        var securityEvent = await db.SecurityEvents.SingleAsync(x => x.Id == ids.SecurityEventId);
        Assert.Equal(SecurityEventStatus.Open, securityEvent.Status);
        Assert.Null(securityEvent.ReviewedAt);
    }

    private static AttendanceIngestionService CreateService(BuildTrackDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAHUA_PARSER_UNCERTAIN_AUTO_RESOLVE_SECONDS"] = "90",
            })
            .Build();

        return new AttendanceIngestionService(
            db,
            new NoopAttendanceSessionService(),
            new WorkerCameraIdentityResolver(
                db,
                new WorkerSiteAssignmentService(db, NullLogger<WorkerSiteAssignmentService>.Instance),
                NullLogger<WorkerCameraIdentityResolver>.Instance),
            new WorkerSiteAssignmentService(db, NullLogger<WorkerSiteAssignmentService>.Instance),
            configuration,
            NullLogger<AttendanceIngestionService>.Instance);
    }

    private static BuildTrackDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext());
    }

    private static async Task<SeedIds> SeedDeviceWorkerAndParserEventAsync(BuildTrackDbContext db, SecurityEventType eventType = SecurityEventType.ParserUncertainSmartEvent)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var securityEventId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "GOLD MMC", Code = "GOLDMMC", Status = TenantStatus.Active });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "GOLD PALACE" });
        db.Devices.Add(new Device
        {
            Id = deviceId,
            TenantId = tenantId,
            SiteId = siteId,
            Name = "GOLD CAM",
            RegisterDeviceId = "BT-GOLDMMC-CAM001",
            RegisterPort = 7000,
            Username = "admin",
            EncryptedPassword = "encrypted",
        });
        db.Workers.Add(new Worker
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TenantId = tenantId,
            SiteId = siteId,
            ExternalWorkerCode = "W-0001",
            FullName = "ilham",
            Status = WorkerStatus.Active,
        });
        db.WorkerCameraIdentities.Add(new WorkerCameraIdentity
        {
            TenantId = tenantId,
            WorkerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DeviceId = deviceId,
            Vendor = "Dahua",
            ExternalUserId = "1",
            CardName = "ilham",
            NormalizedCardName = "ilham",
            IsPrimary = true,
        });
        db.SecurityEvents.Add(new SecurityEvent
        {
            Id = securityEventId,
            TenantId = tenantId,
            SiteId = siteId,
            DeviceId = deviceId,
            EventTime = DateTimeOffset.UtcNow.AddSeconds(-30),
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EventType = eventType,
            Severity = SecurityEventSeverity.Warning,
            Status = SecurityEventStatus.Open,
            RawRecNo = 2139965688,
            Method = "Face",
            Direction = "Entry",
            Source = DahuaEventSourceExtensions.ActiveRegisterSource,
            RawPayloadJson = """{"Classification":"ParserUncertainSmartEvent","UserID":"1","CardName":"8)6i$z","ExpectedWorkerName":"ilham"}""",
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30),
        });
        await db.SaveChangesAsync();
        return new SeedIds(deviceId, securityEventId);
    }

    private static DahuaAccessRecord VerifiedAttendanceRecord() => new()
    {
        RecNo = 41,
        CreateTime = DateTimeOffset.UtcNow,
        UserId = "1",
        CardName = "ilham",
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/app/data/security-snapshots/smart-events/recognized.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Classification"] = "RecognizedAttendance",
            ["IdentityVerified"] = "true",
            ["IdentityRisk"] = "Low",
            ["Status"] = "1",
            ["UserID"] = "1",
            ["CardName"] = "ilham",
            ["Method"] = "15",
            ["Type"] = "Entry",
            ["SnapshotSource"] = "NetSdkSmartEventImageBuffer",
        },
    };

    private sealed record SeedIds(Guid DeviceId, Guid SecurityEventId);

    private sealed class NoopAttendanceSessionService : IAttendanceSessionService
    {
        public Task ProcessEventAsync(AttendanceEvent attendanceEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
