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
        var procurementTask = await db.ProcurementTasks.Include(x => x.Lines).SingleAsync(x => x.TenantId == tenant.Id && x.Code == "BAK-PO-001");
        Assert.Single(procurementTask.Lines);

        var tileAdhesiveRequest = await db.FieldWarehouseRequests.Include(x => x.Lines).SingleAsync(x => x.TenantId == tenant.Id && x.Code == "BAK-WR-003");
        var tileAdhesiveLine = tileAdhesiveRequest.Lines.Single();
        Assert.Equal(FieldWarehouseRequestStatus.InFulfillment, tileAdhesiveRequest.Status);
        Assert.Equal(FieldWarehouseRequestLineStatus.ProcurementInProgress, tileAdhesiveLine.Status);
        Assert.Equal(260m, tileAdhesiveRequest.RequestedQuantity);
        Assert.Equal(260m, tileAdhesiveRequest.ApprovedQuantity);
        Assert.Equal(210m, tileAdhesiveRequest.ReservedQuantity);
        Assert.Equal(0m, tileAdhesiveRequest.IssuedQuantity);
        Assert.Equal(260m, tileAdhesiveLine.RequestedQuantity);
        Assert.Equal(210m, tileAdhesiveLine.ReservedQuantity);
        var tileAdhesiveNeed = await db.ProcurementNeeds.SingleAsync(x => x.TenantId == tenant.Id && x.SourceRequestId == tileAdhesiveRequest.Id);
        Assert.Equal(260m, tileAdhesiveNeed.RequiredQuantity);
        Assert.Equal(210m, tileAdhesiveNeed.AlreadyAvailableQuantity);
        Assert.Equal(50m, tileAdhesiveNeed.ShortfallQuantity);
        Assert.Equal(50m, procurementTask.Lines.Single().RequestedQuantity);

        var vestRequest = await db.FieldWarehouseRequests.Include(x => x.Lines).SingleAsync(x => x.TenantId == tenant.Id && x.Code == "BAK-WR-004");
        Assert.Equal(FieldWarehouseRequestStatus.ReadyForPickup, vestRequest.Status);
        Assert.Equal(15m, vestRequest.RequestedQuantity);
        Assert.Equal(15m, vestRequest.ReservedQuantity);
        Assert.Equal(FieldWarehouseRequestLineStatus.ReadyForIssue, vestRequest.Lines.Single().Status);
        Assert.False(await db.ProcurementNeeds.AnyAsync(x => x.TenantId == tenant.Id && x.SourceRequestId == vestRequest.Id && x.Status != ProcurementNeedStatus.Cancelled));

        var rejectedRequest = await db.FieldWarehouseRequests.Include(x => x.Lines).SingleAsync(x => x.TenantId == tenant.Id && x.Code == "BAK-WR-005");
        Assert.Equal(FieldWarehouseRequestStatus.Rejected, rejectedRequest.Status);
        Assert.Equal(0m, rejectedRequest.ReservedQuantity);
        Assert.Equal(FieldWarehouseRequestLineStatus.Rejected, rejectedRequest.Lines.Single().Status);
        Assert.False(await db.ProcurementNeeds.AnyAsync(x => x.TenantId == tenant.Id && x.SourceRequestId == rejectedRequest.Id));

        var issuedRequest = await db.FieldWarehouseRequests.Include(x => x.Lines).SingleAsync(x => x.TenantId == tenant.Id && x.Code == "BAK-WR-006");
        Assert.Equal(FieldWarehouseRequestStatus.Issued, issuedRequest.Status);
        Assert.Equal(40m, issuedRequest.IssuedQuantity);
        Assert.Equal(FieldWarehouseRequestLineStatus.Issued, issuedRequest.Lines.Single().Status);

        var workspace = await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenant.Id);
        using var document = JsonDocument.Parse(workspace.WorkspaceJson);
        Assert.Equal(4, document.RootElement.GetProperty("objects").GetArrayLength());
        Assert.Equal(9, document.RootElement.GetProperty("stages").GetArrayLength());
        Assert.Equal(10, document.RootElement.GetProperty("workItems").GetArrayLength());
        Assert.Equal(6, document.RootElement.GetProperty("crews").GetArrayLength());
        Assert.Equal(48, document.RootElement.GetProperty("workerAssignments").GetArrayLength());

        var workItems = document.RootElement.GetProperty("workItems").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                item => new
                {
                    ObjectId = item.GetProperty("objectId").GetString(),
                    StageId = item.GetProperty("stageId").GetString(),
                    MaterialUnitPrice = item.GetProperty("materialUnitPrice").GetDecimal(),
                });
        var materials = document.RootElement.GetProperty("materials").EnumerateArray().ToArray();
        Assert.Equal(6, materials.Length);
        var materialCountsByObject = materials
            .GroupBy(material => material.GetProperty("objectId").GetString())
            .ToDictionary(group => group.Key!, group => group.Count());
        Assert.True(materialCountsByObject.Count >= 2);
        Assert.Equal(6, materialCountsByObject.Values.Sum());
        foreach (var material in materials)
        {
            var objectId = material.GetProperty("objectId").GetString();
            var linkedWorkItemId = material.GetProperty("linkedWorkItemId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(objectId));
            Assert.False(string.IsNullOrWhiteSpace(linkedWorkItemId));
            var linkedWorkItem = workItems[linkedWorkItemId!];
            Assert.Equal(linkedWorkItem.ObjectId, objectId);
            Assert.Equal(linkedWorkItem.StageId, material.GetProperty("linkedStageId").GetString());
            Assert.Equal(linkedWorkItem.MaterialUnitPrice, material.GetProperty("unitPrice").GetDecimal());
        }
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
