using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class WorkerSiteAssignmentServiceTests
{
    [Fact]
    public async Task SyncAssignmentsCreatesPrimaryActiveSiteAssignment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var ids = await SeedWorkerWithSitesAsync(db, tenantId);
        var service = CreateService(db);

        await service.SyncAssignmentsAsync(ids.WorkerId, [ids.SiteAId], ids.SiteAId);

        var assignment = await db.WorkerSiteAssignments.SingleAsync(x => x.WorkerId == ids.WorkerId);
        var worker = await db.Workers.SingleAsync(x => x.Id == ids.WorkerId);
        Assert.Equal(ids.SiteAId, assignment.SiteId);
        Assert.True(assignment.IsPrimary);
        Assert.Equal(WorkerSiteAssignmentStatus.Active, assignment.Status);
        Assert.Equal(ids.SiteAId, worker.SiteId);
    }

    [Fact]
    public async Task SyncAssignmentsRejectsAnotherTenantSite()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDbContext(tenantA);
        var idsA = await SeedWorkerWithSitesAsync(db, tenantA);
        var idsB = await SeedWorkerWithSitesAsync(db, tenantB);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SyncAssignmentsAsync(idsA.WorkerId, [idsB.SiteAId], idsB.SiteAId));
    }

    [Fact]
    public async Task EnsureAssignmentAutoAssignsWorkerFromCameraAttendanceSite()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var ids = await SeedWorkerWithSitesAsync(db, tenantId);
        var service = CreateService(db);

        await service.EnsureAssignmentAsync(ids.WorkerId, ids.SiteBId, "Worker auto-assigned to site from camera attendance");

        Assert.Contains(await db.WorkerSiteAssignments.ToListAsync(), assignment =>
            assignment.WorkerId == ids.WorkerId
            && assignment.SiteId == ids.SiteBId
            && assignment.Status == WorkerSiteAssignmentStatus.Active);
    }

    private static WorkerSiteAssignmentService CreateService(BuildTrackDbContext db) =>
        new(db, NullLogger<WorkerSiteAssignmentService>.Instance);

    private static BuildTrackDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }

    private static async Task<SeedIds> SeedWorkerWithSitesAsync(BuildTrackDbContext db, Guid tenantId)
    {
        var siteAId = Guid.NewGuid();
        var siteBId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = $"Tenant {tenantId:N}", Code = tenantId.ToString("N")[..8], Status = TenantStatus.Active });
        db.Sites.AddRange(
            new Site { Id = siteAId, TenantId = tenantId, Name = "GOLD PALACE" },
            new Site { Id = siteBId, TenantId = tenantId, Name = "OTHER SITE" });
        db.Workers.Add(new Worker
        {
            Id = workerId,
            TenantId = tenantId,
            SiteId = siteAId,
            ExternalWorkerCode = "W-0001",
            FullName = "ilham",
            HourlyRate = 5,
            Status = WorkerStatus.Active,
        });
        await db.SaveChangesAsync();
        return new SeedIds(workerId, siteAId, siteBId);
    }

    private sealed record SeedIds(Guid WorkerId, Guid SiteAId, Guid SiteBId);
}
