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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_SKYSNAP_DEMO"] = "true",
                ["SEED_SKYSNAP_DEMO_PASSWORD"] = "SkySnapDemo!2026",
            })
            .Build();

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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_SKYSNAP_DEMO"] = "true",
                ["SEED_SKYSNAP_DEMO_PASSWORD"] = "SkySnapDemo!2026",
            })
            .Build();

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
}
