using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class WorkerCameraIdentityResolverTests
{
    [Fact]
    public async Task ResolvesByTenantDeviceAndCardNameToInternalWorkerCode()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var ids = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantId, "W-0001", "Ilham Sadixov", "ilham", "1");
        var resolver = CreateResolver(db);

        var device = await db.Devices.SingleAsync(x => x.Id == ids.DeviceId);
        var result = await resolver.ResolveAsync(device, KnownRecord("1", "ilham"));

        Assert.True(result.Resolved);
        Assert.Equal("W-0001", result.Worker!.ExternalWorkerCode);
        Assert.Equal("Ilham Sadixov", result.Worker.FullName);
        Assert.Equal("TenantDeviceCardName", result.ResolvedBy);
    }

    [Fact]
    public async Task TenantCannotResolveAnotherTenantWorkerCameraIdentity()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDbContext(tenantA);
        await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantB, "W-9001", "Tenant B Worker", "ilham", "1");
        var idsA = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantA, "W-0001", "Tenant A Worker", "ali", "2");
        var resolver = CreateResolver(db);

        var deviceA = await db.Devices.SingleAsync(x => x.Id == idsA.DeviceId);
        var result = await resolver.ResolveAsync(deviceA, KnownRecord("1", "ilham"));

        Assert.False(result.Resolved);
        Assert.Null(result.Worker);
        Assert.Equal("UnmappedCameraIdentity", result.Status);
    }

    [Fact]
    public async Task TenantCannotMapWorkerToAnotherTenantDevice()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDbContext(tenantA);
        var idsA = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantA, "W-0001", "Tenant A Worker", "ali", "2");
        var idsB = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantB, "W-9001", "Tenant B Worker", "ilham", "1");
        var resolver = CreateResolver(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.UpsertAsync(idsA.WorkerId, idsB.DeviceId, "1", "ilham", true));
        Assert.Contains("Device", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemapRecentUpdatesOnlySameTenantAttendanceAndSessions()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDbContext(tenantA);
        var idsA = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantA, "W-0001", "Ilham Sadixov", "ilham", "1");
        var idsB = await SeedTenantWithWorkerDeviceAndIdentityAsync(db, tenantB, "W-9001", "Tenant B Ilham", "ilham", "1");
        db.AttendanceEvents.Add(RawEvent(tenantA, idsA.SiteId, idsA.DeviceId, "1", "ilham"));
        db.AttendanceEvents.Add(RawEvent(tenantB, idsB.SiteId, idsB.DeviceId, "1", "ilham"));
        db.AttendanceSessions.Add(RawSession(tenantA, idsA.SiteId, idsA.DeviceId, "1", "ilham"));
        db.AttendanceSessions.Add(RawSession(tenantB, idsB.SiteId, idsB.DeviceId, "1", "ilham"));
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db);

        var result = await resolver.RemapRecentAsync(idsA.WorkerId, idsA.IdentityId);

        Assert.Equal(1, result.AttendanceEventsUpdated);
        Assert.Equal(1, result.AttendanceSessionsUpdated);
        Assert.Contains(db.AttendanceEvents.IgnoreQueryFilters().Where(x => x.TenantId == tenantA), x => x.WorkerExternalId == "W-0001" && x.WorkerName == "Ilham Sadixov");
        Assert.Contains(db.AttendanceEvents.IgnoreQueryFilters().Where(x => x.TenantId == tenantB), x => x.WorkerExternalId == "1" && x.WorkerName == "ilham");
        Assert.Contains(db.WorkerSiteAssignments.IgnoreQueryFilters().Where(x => x.TenantId == tenantA), x => x.WorkerId == idsA.WorkerId && x.SiteId == idsA.SiteId && x.Status == WorkerSiteAssignmentStatus.Active);
    }

    private static WorkerCameraIdentityResolver CreateResolver(BuildTrackDbContext db) =>
        new(
            db,
            new WorkerSiteAssignmentService(db, NullLogger<WorkerSiteAssignmentService>.Instance),
            NullLogger<WorkerCameraIdentityResolver>.Instance);

    private static BuildTrackDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }

    private static async Task<SeedIds> SeedTenantWithWorkerDeviceAndIdentityAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        string workerCode,
        string workerName,
        string cardName,
        string userId)
    {
        var siteId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = $"Tenant {tenantId:N}", Code = tenantId.ToString("N")[..8], Status = TenantStatus.Active });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "Site" });
        db.Workers.Add(new Worker { Id = workerId, TenantId = tenantId, SiteId = siteId, ExternalWorkerCode = workerCode, FullName = workerName, Status = WorkerStatus.Active });
        db.Devices.Add(new Device
        {
            Id = deviceId,
            TenantId = tenantId,
            SiteId = siteId,
            Name = "Dahua",
            RegisterDeviceId = $"BT-{tenantId:N}",
            RegisterPort = 7000,
            Username = "admin",
            EncryptedPassword = "encrypted",
        });
        db.WorkerCameraIdentities.Add(new WorkerCameraIdentity
        {
            Id = identityId,
            TenantId = tenantId,
            WorkerId = workerId,
            DeviceId = deviceId,
            Vendor = "Dahua",
            ExternalUserId = userId,
            CardName = cardName,
            NormalizedCardName = cardName,
            IsPrimary = true,
        });
        await db.SaveChangesAsync();
        return new SeedIds(siteId, workerId, deviceId, identityId);
    }

    private static DahuaAccessRecord KnownRecord(string userId, string cardName) => new()
    {
        UserId = userId,
        CardName = cardName,
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        RawFields = new Dictionary<string, string?>
        {
            ["UserID"] = userId,
            ["CardName"] = cardName,
            ["ReceivedCardName"] = cardName,
            ["Status"] = "1",
            ["Method"] = "15",
            ["Type"] = "Entry",
        },
    };

    private static AttendanceEvent RawEvent(Guid tenantId, Guid siteId, Guid deviceId, string userId, string cardName) => new()
    {
        TenantId = tenantId,
        SiteId = siteId,
        DeviceId = deviceId,
        WorkerExternalId = userId,
        WorkerName = cardName,
        EventTime = DateTimeOffset.UtcNow,
        Status = AttendanceEventStatus.Ok,
        Method = AttendanceMethod.Face,
        Direction = AttendanceDirection.Entry,
        Source = DahuaEventSourceExtensions.ActiveRegisterSource,
        RawPayloadJson = $$"""{"UserID":"{{userId}}","CardName":"{{cardName}}","ReceivedCardName":"{{cardName}}"}""",
    };

    private static AttendanceSession RawSession(Guid tenantId, Guid siteId, Guid deviceId, string userId, string cardName) => new()
    {
        TenantId = tenantId,
        SiteId = siteId,
        DeviceId = deviceId,
        WorkerExternalId = userId,
        WorkerName = cardName,
        WorkDate = DateOnly.FromDateTime(DateTime.UtcNow),
        CheckInEventId = Guid.NewGuid(),
        CheckInTime = DateTimeOffset.UtcNow.AddHours(-1),
        LastSeenTime = DateTimeOffset.UtcNow,
        Status = AttendanceSessionStatus.Open,
        Source = DahuaEventSourceExtensions.ActiveRegisterSource,
    };

    private sealed record SeedIds(Guid SiteId, Guid WorkerId, Guid DeviceId, Guid IdentityId);
}
