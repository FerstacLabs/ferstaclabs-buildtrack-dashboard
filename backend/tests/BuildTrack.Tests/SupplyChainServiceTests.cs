using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class SupplyChainServiceTests
{
    [Fact]
    public async Task ApproveRequestReservesAvailableStockAndCreatesNeedOnlyForShortfall()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-HELMET", "Kaska", 7);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Urgent,
            "20 kaska lazımdır",
            [new SupplyRequestLineInput(seed.ItemId, 20, "Yeni briqada")]), CancellationToken.None);

        var review = await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, "Yoxlandı", CancellationToken.None);

        Assert.Equal(20, review.TotalRequested);
        Assert.Equal(7, review.TotalReserved);
        Assert.Equal(13, review.TotalShortfall);
        var need = await db.ProcurementNeeds.SingleAsync(x => x.SourceRequestId == request.Id);
        Assert.Equal(13, need.ShortfallQuantity);
        Assert.Equal(7, need.AlreadyAvailableQuantity);
        Assert.Equal(7, await db.WarehouseReservations.SumAsync(x => x.Quantity));
    }

    [Fact]
    public async Task ApproveRequestWithEnoughStockCreatesReservationWithoutProcurementNeed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-GLOVE", "İş əlcəyi", 100);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Normal,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 20, "Əlcək")]), CancellationToken.None);

        var review = await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);

        Assert.Equal(FieldWarehouseRequestStatus.ReadyForPickup, review.Status);
        Assert.Empty(await db.ProcurementNeeds.ToListAsync());
        Assert.Equal(20, await db.WarehouseReservations.SumAsync(x => x.Quantity));
    }

    [Fact]
    public async Task SupplyTaskSubmitRequiresReceiptAndProductPhotoEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "TOOL-DRILL-12", "Sverlo 12mm", 0);
        var service = CreateService(db);
        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Critical,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 12, "Sverlo")]), CancellationToken.None);
        await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);
        var needId = await db.ProcurementNeeds.Select(x => x.Id).SingleAsync();
        var task = await service.CreateProcurementTaskAsync(tenantId, [needId], seed.AgentId, seed.UserId, null, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitTaskForVerificationAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None));
    }

    [Fact]
    public async Task IssueFieldRequestIsIdempotentAndDoesNotDuplicateStockMovement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-VEST", "Reflektor jilet", 25);
        var service = CreateService(db);
        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Normal,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 5, "Jilet")]), CancellationToken.None);
        await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);

        var first = await service.IssueFieldRequestAsync(tenantId, request.Id, seed.WarehouseId, seed.UserId, "Prorab", null, CancellationToken.None);
        var second = await service.IssueFieldRequestAsync(tenantId, request.Id, seed.WarehouseId, seed.UserId, "Prorab", null, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.WarehouseStockMovements.CountAsync(x => x.MovementType == WarehouseStockMovementType.Issue));
        Assert.Equal(20, (await new WarehouseAvailabilityService(db).GetAvailabilityAsync(tenantId, seed.WarehouseId, seed.ItemId, CancellationToken.None)).OnHand);
    }

    private static BuildTrackDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }

    private static ISupplyChainService CreateService(BuildTrackDbContext db) =>
        new SupplyChainService(
            db,
            new WarehouseAvailabilityService(db),
            new WarehouseUsagePolicyService(db),
            NullLogger<SupplyChainService>.Instance);

    private static async Task<SeedIds> SeedAsync(BuildTrackDbContext db, Guid tenantId, string itemCode, string itemName, decimal openingStock)
    {
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant", Code = tenantId.ToString("N")[..8], Status = TenantStatus.Active });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "GOLD PALACE" });
        db.Users.AddRange(
            new AppUser { Id = userId, TenantId = tenantId, FullName = "Manager", Email = $"{userId:N}@test.local", PasswordHash = "hash", Role = BuildTrackUserRole.Manager, Status = BuildTrackUserStatus.Active },
            new AppUser { Id = agentId, TenantId = tenantId, FullName = "Supply Agent", Email = $"{agentId:N}@test.local", PasswordHash = "hash", Role = BuildTrackUserRole.ProcurementAgent, Status = BuildTrackUserStatus.Active });
        db.FieldWarehouseCatalogItems.Add(new FieldWarehouseCatalogItem
        {
            Id = itemId,
            TenantId = tenantId,
            Name = itemName,
            NameAz = itemName,
            Category = itemCode.StartsWith("PPE", StringComparison.OrdinalIgnoreCase) ? "PPE" : "Alət",
            Unit = itemCode == "PPE-GLOVE" ? "cüt" : "ədəd",
            Code = itemCode,
            IsActive = true,
        });
        db.Warehouses.Add(new Warehouse { Id = warehouseId, TenantId = tenantId, Name = "Mərkəzi anbar", IsDefault = true, IsActive = true });
        db.WarehouseStockMovements.Add(new WarehouseStockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            CatalogItemId = itemId,
            MovementType = WarehouseStockMovementType.OpeningBalance,
            Quantity = openingStock,
            ReferenceType = "TestOpeningBalance",
            ReferenceId = itemId,
        });
        await db.SaveChangesAsync();
        return new SeedIds(siteId, userId, agentId, itemId, warehouseId);
    }

    private sealed record SeedIds(Guid SiteId, Guid UserId, Guid AgentId, Guid ItemId, Guid WarehouseId);
}
