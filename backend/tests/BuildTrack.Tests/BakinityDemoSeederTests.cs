using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Tests;

public sealed class BakinityDemoSeederTests
{
    [Fact]
    public async Task BakinityDemoSeedCreatesCompleteIdempotentTenantEnvironment()
    {
        await using var db = CreateDb();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_BAKINITY_DEMO"] = "true",
                ["SEED_BAKINITY_DEMO_PASSWORD"] = CreateTestPassword(),
            })
            .Build();

        await BakinityDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);
        await BakinityDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Code == DbInitializer.BakinityDemoTenantCode);
        Assert.Equal(DbInitializer.BakinityDemoTenantId, tenant.Id);
        Assert.Equal("BAKİNİTY MMC", tenant.CompanyName);

        Assert.Equal(1, await db.Users.CountAsync(x => x.TenantId == tenant.Id && x.Email == "eldar@bakinity.az" && x.Role == BuildTrackUserRole.Owner));
        Assert.Equal(10, await db.Users.CountAsync(x => x.TenantId == tenant.Id && x.Role == BuildTrackUserRole.Supervisor));
        Assert.Equal(3, await db.Users.CountAsync(x => x.TenantId == tenant.Id && x.Role == BuildTrackUserRole.ProcurementAgent));
        Assert.Equal(4, await db.Sites.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(10, await db.SupervisorSiteAssignments.CountAsync(x => x.TenantId == tenant.Id && x.IsActive));
        Assert.Equal(48, await db.Workers.CountAsync(x => x.TenantId == tenant.Id));
        Assert.Equal(48, await db.WorkerSiteAssignments.CountAsync(x => x.TenantId == tenant.Id && x.Status == WorkerSiteAssignmentStatus.Active));
        Assert.True(await db.FieldSmetaItems.CountAsync(x => x.TenantId == tenant.Id) >= 30);
        Assert.True(await db.FieldWarehouseCatalogItems.CountAsync(x => x.TenantId == tenant.Id) >= DbInitializer.SupplyCatalogSeedItems.Count);
        Assert.True(await db.WarehouseStockMovements.CountAsync(x => x.TenantId == tenant.Id && x.ReferenceType == "BakinitySeedOpeningBalance") >= 10);

        var requestStatuses = await db.FieldWarehouseRequests
            .Where(x => x.TenantId == tenant.Id)
            .Select(x => x.Status)
            .Distinct()
            .ToArrayAsync();
        Assert.Contains(FieldWarehouseRequestStatus.PendingApproval, requestStatuses);
        Assert.Contains(FieldWarehouseRequestStatus.NeedsJustification, requestStatuses);
        Assert.Contains(FieldWarehouseRequestStatus.InFulfillment, requestStatuses);
        Assert.Contains(FieldWarehouseRequestStatus.ReadyForPickup, requestStatuses);
        Assert.Contains(FieldWarehouseRequestStatus.Rejected, requestStatuses);
        Assert.Contains(FieldWarehouseRequestStatus.Issued, requestStatuses);
        Assert.Single(await db.ProcurementTasks.Where(x => x.TenantId == tenant.Id && x.Code == "BAK-PO-001").ToArrayAsync());

        var workspace = await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenant.Id);
        using var document = JsonDocument.Parse(workspace.WorkspaceJson);
        Assert.Equal(4, document.RootElement.GetProperty("objects").GetArrayLength());
        Assert.Equal(9, document.RootElement.GetProperty("stages").GetArrayLength());
        Assert.Equal(10, document.RootElement.GetProperty("workItems").GetArrayLength());
        Assert.Equal(6, document.RootElement.GetProperty("crews").GetArrayLength());
        Assert.Equal(48, document.RootElement.GetProperty("workerAssignments").GetArrayLength());
    }

    [Fact]
    public async Task BakinityDemoSeedIsOptInAndDoesNotCreateTenantByDefault()
    {
        await using var db = CreateDb();

        await BakinityDemoSeeder.SeedAsync(db, new ConfigurationBuilder().Build(), CancellationToken.None);

        Assert.False(await db.Tenants.AnyAsync(x => x.Code == DbInitializer.BakinityDemoTenantCode));
    }

    private static BuildTrackDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext());
    }

    private static string CreateTestPassword() => $"{Guid.NewGuid():N}Aa1!";
}
