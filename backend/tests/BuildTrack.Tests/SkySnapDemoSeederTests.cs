using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Tests;

public sealed class SkySnapDemoSeederTests
{
    [Fact]
    public async Task SkySnapDemoSeedCreatesEnglishIsolatedIdempotentTenant()
    {
        await using var db = CreateDb();
        var configuration = SkySnapConfiguration();

        await SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);
        await SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Code == DbInitializer.SkySnapDemoTenantCode);
        Assert.Equal(DbInitializer.SkySnapDemoTenantId, tenant.Id);
        Assert.Equal("SkySnap Construction Demo", tenant.CompanyName);
        Assert.Equal(4, await db.Sites.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(10, await db.Users.CountAsync(x => x.TenantId == tenant.Id && x.Role == BuildTrackUserRole.Supervisor));
        Assert.Equal(2, await db.Users.CountAsync(x => x.TenantId == tenant.Id && x.Role == BuildTrackUserRole.ProcurementAgent));
        Assert.Equal(48, await db.Workers.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(6, await db.ProjectStages.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(12, await db.ProjectWorkItems.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(6, await db.ProjectCrews.CountAsync(x => x.TenantId == tenant.Id));
        Assert.True(await db.FieldWarehouseCatalogItems.CountAsync(x => x.TenantId == tenant.Id) >= DbInitializer.SupplyCatalogSeedItems.Count);
        Assert.True(await db.WarehouseStockMovements.CountAsync(x => x.TenantId == tenant.Id && x.ReferenceType == "SkySnapSeedOpeningBalance") >= 10);
        Assert.Equal(8, await db.SupervisorDailyReports.CountAsync(x => x.TenantId == tenant.Id));
        Assert.True(await db.Devices.CountAsync(x => x.TenantId == tenant.Id && x.RegisterDeviceId.StartsWith("SKYSNAP-DEMO")) >= 4);
        await AssertSkySnapSiteIntegrityAsync(db, tenant.Id);

        var owner = await db.Users.SingleAsync(x => x.TenantId == tenant.Id && x.Email == SkySnapDemoSeeder.DefaultOwnerEmail);
        Assert.Equal("Tomasz Odrobiński", owner.FullName);
        Assert.Equal(BuildTrackUserRole.Owner, owner.Role);

        var workspace = await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenant.Id);
        using var document = JsonDocument.Parse(workspace.WorkspaceJson);
        Assert.Equal("SkySnap Construction Demo Portfolio", document.RootElement.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal(4, document.RootElement.GetProperty("objects").GetArrayLength());
        Assert.Equal(48, document.RootElement.GetProperty("workerAssignments").GetArrayLength());
        Assert.Contains("Drone", document.RootElement.GetRawText());
    }

    [Fact]
    public void ProjectWorkItemModelIncludesProductionSiteForeignKey()
    {
        using var db = CreateDb();

        var entity = db.Model.FindEntityType(typeof(ProjectWorkItemRecord));
        var fk = entity?.GetForeignKeys().SingleOrDefault(candidate =>
            candidate.PrincipalEntityType.ClrType == typeof(Site) &&
            candidate.Properties.Count == 1 &&
            candidate.Properties[0].Name == nameof(ProjectWorkItemRecord.SiteId));

        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Cascade, fk!.DeleteBehavior);
    }

    [Fact]
    public async Task SkySnapDemoSeedCompletesWhenTenantAndSitesAlreadyExist()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant { Id = SkySnapDemoSeeder.TenantId, Code = SkySnapDemoSeeder.TenantCode, CompanyName = "Partial SkySnap", Status = TenantStatus.Active });
        for (var i = 1; i <= 4; i++)
        {
            db.Sites.Add(new Site
            {
                Id = StableGuidForTest($"SKY-SITE-{i:000}"),
                TenantId = SkySnapDemoSeeder.TenantId,
                Name = $"Partial site {i}",
                Address = "Partial",
                TimeZone = "Europe/Warsaw",
            });
        }
        await db.SaveChangesAsync();

        await SkySnapDemoSeeder.SeedAsync(db, SkySnapConfiguration(), CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Code == SkySnapDemoSeeder.TenantCode);
        Assert.Equal(4, await db.Sites.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(48, await db.Workers.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(12, await db.ProjectWorkItems.CountAsync(x => x.TenantId == tenant.Id));
        await AssertSkySnapSiteIntegrityAsync(db, tenant.Id);
    }

    [Fact]
    public async Task SkySnapDemoSeedCompletesWhenTenantSitesAndSomeWorkersAlreadyExist()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant { Id = SkySnapDemoSeeder.TenantId, Code = SkySnapDemoSeeder.TenantCode, CompanyName = "Partial SkySnap", Status = TenantStatus.Active });
        var siteId = StableGuidForTest("SKY-SITE-001");
        db.Sites.Add(new Site { Id = siteId, TenantId = SkySnapDemoSeeder.TenantId, Name = "Partial site 1", Address = "Partial", TimeZone = "Europe/Warsaw" });
        db.Workers.Add(new Worker
        {
            TenantId = SkySnapDemoSeeder.TenantId,
            SiteId = siteId,
            ExternalWorkerCode = "SKY-W-001",
            FullName = "Existing Worker",
            Role = "Existing",
            Brigade = "Concrete Crew",
            Status = WorkerStatus.Active,
        });
        await db.SaveChangesAsync();

        await SkySnapDemoSeeder.SeedAsync(db, SkySnapConfiguration(), CancellationToken.None);
        await SkySnapDemoSeeder.SeedAsync(db, SkySnapConfiguration(), CancellationToken.None);

        Assert.Equal(4, await db.Sites.CountAsync(x => x.TenantId == SkySnapDemoSeeder.TenantId));
        Assert.Equal(48, await db.Workers.CountAsync(x => x.TenantId == SkySnapDemoSeeder.TenantId));
        Assert.Equal(48, await db.Workers.Select(x => new { x.TenantId, x.ExternalWorkerCode }).Distinct().CountAsync(x => x.TenantId == SkySnapDemoSeeder.TenantId));
        Assert.Equal(12, await db.ProjectWorkItems.CountAsync(x => x.TenantId == SkySnapDemoSeeder.TenantId));
        await AssertSkySnapSiteIntegrityAsync(db, SkySnapDemoSeeder.TenantId);
    }

    [Fact]
    public async Task SkySnapDemoSeedIsOptIn()
    {
        await using var db = CreateDb();

        await SkySnapDemoSeeder.SeedAsync(db, new ConfigurationBuilder().Build(), CancellationToken.None);

        Assert.False(await db.Tenants.AnyAsync(x => x.Code == DbInitializer.SkySnapDemoTenantCode));
    }

    [Fact]
    public async Task SkySnapDemoSeedDoesNotStealTomaszUserFromAnotherTenant()
    {
        await using var db = CreateDb();
        var otherTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = otherTenantId, CompanyName = "Other Tenant", Code = "OTHER", Status = TenantStatus.Active });
        db.Users.Add(new AppUser
        {
            TenantId = otherTenantId,
            FullName = "Existing Tomasz",
            Email = SkySnapDemoSeeder.DefaultOwnerEmail,
            PasswordHash = "hash",
            Role = BuildTrackUserRole.Owner,
            Status = BuildTrackUserStatus.Active,
        });
        await db.SaveChangesAsync();
        var configuration = SkySnapConfiguration();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None));

        Assert.Contains("already belongs to another tenant", error.Message);
        var existingUser = await db.Users.SingleAsync(x => x.Email == SkySnapDemoSeeder.DefaultOwnerEmail);
        Assert.Equal(otherTenantId, existingUser.TenantId);
    }

    private static BuildTrackDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext());
    }

    private static IConfiguration SkySnapConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SEED_SKYSNAP_DEMO"] = "true",
            ["SEED_SKYSNAP_DEMO_PASSWORD"] = "SkySnapDemo!2026",
        })
        .Build();

    private static Guid StableGuidForTest(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private static async Task AssertSkySnapSiteIntegrityAsync(BuildTrackDbContext db, Guid tenantId)
    {
        var siteIds = await db.Sites.Where(x => x.TenantId == tenantId).Select(x => x.Id).ToListAsync();
        Assert.NotEmpty(siteIds);

        Assert.False(await db.ProjectSites.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));
        Assert.False(await db.ProjectStages.AnyAsync(x => x.TenantId == tenantId && x.SiteId.HasValue && !siteIds.Contains(x.SiteId.Value)));
        Assert.False(await db.ProjectCrews.AnyAsync(x => x.TenantId == tenantId && x.SiteId.HasValue && !siteIds.Contains(x.SiteId.Value)));
        Assert.False(await db.ProjectWorkItems.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));
        Assert.False(await db.ProjectWorkItemMaterials.AnyAsync(x => x.TenantId == tenantId && x.SiteId.HasValue && !siteIds.Contains(x.SiteId.Value)));
        Assert.False(await db.Workers.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));
        Assert.False(await db.Devices.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));
        Assert.False(await db.SupervisorDailyReports.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));
        Assert.False(await db.FieldSmetaItems.AnyAsync(x => x.TenantId == tenantId && !siteIds.Contains(x.SiteId)));

        var orphanWorkItemCount = await (
            from item in db.ProjectWorkItems
            where item.TenantId == tenantId
            join site in db.Sites on item.SiteId equals site.Id into siteJoin
            from site in siteJoin.DefaultIfEmpty()
            where site == null
            select item.Id).CountAsync();
        Assert.Equal(0, orphanWorkItemCount);
    }
}
