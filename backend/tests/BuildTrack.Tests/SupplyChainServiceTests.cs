using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Tests;

public sealed class SupplyChainServiceTests
{
    private static readonly DateOnly Tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

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
            Tomorrow,
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
        Assert.Equal(Tomorrow, request.NeededBy);
        Assert.Equal(Tomorrow, need.RequiredBy);
        var task = await service.CreateProcurementTaskAsync(tenantId, [need.Id], seed.AgentId, seed.UserId, "Test", CancellationToken.None);
        Assert.Equal(Tomorrow, task.RequiredBy);
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
    public async Task RejectedWarehouseRequestCannotBeApprovedOrReserved()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-REJECT-HELMET", "Kaska", 30);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Critical,
            "Rejected request must stay terminal",
            [new SupplyRequestLineInput(seed.ItemId, 10, "Kaska")]), CancellationToken.None);

        var stored = await db.FieldWarehouseRequests.Include(x => x.Lines).SingleAsync(x => x.Id == request.Id);
        stored.Status = FieldWarehouseRequestStatus.Rejected;
        foreach (var line in stored.Lines)
        {
            line.Status = FieldWarehouseRequestLineStatus.Rejected;
        }
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None));

        Assert.Equal("Rejected warehouse requests cannot be processed.", ex.Message);
        Assert.Empty(await db.WarehouseReservations.ToListAsync());
        Assert.Empty(await db.ProcurementNeeds.ToListAsync());
    }

    [Fact]
    public async Task NeedsJustificationWarehouseRequestCannotBeApprovedOrReserved()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-JUSTIFY-HELMET", "Kaska", 30);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Normal,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 10, "Kaska")]), CancellationToken.None);

        var stored = await db.FieldWarehouseRequests.SingleAsync(x => x.Id == request.Id);
        stored.Status = FieldWarehouseRequestStatus.NeedsJustification;
        stored.JustificationRequestNote = "Explain the quantity.";
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None));

        Assert.Equal("Warehouse request must be pending approval before stock processing.", ex.Message);
        Assert.Empty(await db.WarehouseReservations.ToListAsync());
        Assert.Empty(await db.ProcurementNeeds.ToListAsync());
    }

    [Fact]
    public async Task RejectedWarehouseRequestCannotCreateProcurementTaskFromExistingNeed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "TOOL-REJECT-DRILL", "Sverlo", 0);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Urgent,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 5, "Sverlo")]), CancellationToken.None);
        await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);

        var needId = await db.ProcurementNeeds.Where(x => x.SourceRequestId == request.Id).Select(x => x.Id).SingleAsync();
        var stored = await db.FieldWarehouseRequests.SingleAsync(x => x.Id == request.Id);
        stored.Status = FieldWarehouseRequestStatus.Rejected;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateProcurementTaskAsync(tenantId, [needId], seed.AgentId, seed.UserId, null, CancellationToken.None));

        Assert.Equal("Rejected warehouse requests cannot be processed.", ex.Message);
        Assert.Empty(await db.ProcurementTasks.ToListAsync());
    }

    [Fact]
    public async Task PartiallyApprovedWarehouseRequestCannotBeIssuedUntilFullyReady()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-PARTIAL-HELMET", "Kaska", 7);
        var service = CreateService(db);

        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Normal,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 20, "Kaska")]), CancellationToken.None);
        var review = await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);

        Assert.Equal(FieldWarehouseRequestStatus.PartiallyApproved, review.Status);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IssueFieldRequestAsync(tenantId, request.Id, seed.WarehouseId, seed.UserId, "Prorab", null, CancellationToken.None));

        Assert.Equal("Warehouse request is not ready for issue.", ex.Message);
        Assert.Empty(await db.WarehouseIssues.ToListAsync());
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
    public async Task PurchaseQuantityCannotExceedRequestedQuantity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "TOOL-DRILL-OVER", "Sverlo", 0);
        var service = CreateService(db);
        var task = await CreateProcurementTaskScenarioAsync(db, service, tenantId, seed, 8);

        var lineId = task.Lines.Single().Id;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateTaskLinePurchaseAsync(tenantId, seed.AgentId, new SupplyTaskLinePurchaseInput(lineId, 9, 2, null, null), CancellationToken.None));

        Assert.Equal("Alınan miqdar tələb olunan miqdarı keçə bilməz", ex.Message);
    }

    [Fact]
    public async Task SubmitRequiresProductPhotoForEveryPurchasedLineAndReceipt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "TOOL-DRILL-PHOTO", "Sverlo", 0);
        var service = CreateService(db);
        var task = await CreateProcurementTaskScenarioAsync(db, service, tenantId, seed, 6);
        var line = task.Lines.Single();

        await service.UpdateTaskLinePurchaseAsync(tenantId, seed.AgentId, new SupplyTaskLinePurchaseInput(line.Id, 6, 3, null, "Alındı"), CancellationToken.None);
        db.ProcurementAttachments.Add(CreateAttachment(tenantId, task.Id, null, seed.AgentId, ProcurementAttachmentType.Receipt));
        await db.SaveChangesAsync();

        var missingPhoto = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitTaskForVerificationAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None));
        Assert.Contains("məhsul şəkli", missingPhoto.Message, StringComparison.OrdinalIgnoreCase);

        db.ProcurementAttachments.Add(CreateAttachment(tenantId, task.Id, line.Id, seed.AgentId, ProcurementAttachmentType.ProductPhoto));
        await db.SaveChangesAsync();

        var submitted = await service.SubmitTaskForVerificationAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None);
        var submittedAgain = await service.SubmitTaskForVerificationAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None);

        Assert.Equal(ProcurementTaskStatus.SubmittedForVerification, submitted.Status);
        Assert.Equal(ProcurementTaskStatus.SubmittedForVerification, submittedAgain.Status);
    }

    [Fact]
    public async Task VerifyDoesNotIncreaseStockButGoodsReceiptDoes()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "TOOL-DRILL-RECEIPT", "Sverlo", 0);
        var service = CreateService(db);
        var task = await CreateProcurementTaskScenarioAsync(db, service, tenantId, seed, 4);
        var line = task.Lines.Single();

        await service.UpdateTaskLinePurchaseAsync(tenantId, seed.AgentId, new SupplyTaskLinePurchaseInput(line.Id, 4, 5, null, null), CancellationToken.None);
        db.ProcurementAttachments.Add(CreateAttachment(tenantId, task.Id, null, seed.AgentId, ProcurementAttachmentType.Invoice));
        db.ProcurementAttachments.Add(CreateAttachment(tenantId, task.Id, line.Id, seed.AgentId, ProcurementAttachmentType.ProductPhoto));
        await db.SaveChangesAsync();
        await service.SubmitTaskForVerificationAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None);

        await service.VerifyTaskAsync(tenantId, task.Id, seed.UserId, "OK", CancellationToken.None);
        Assert.Equal(0, (await new WarehouseAvailabilityService(db).GetAvailabilityAsync(tenantId, seed.WarehouseId, seed.ItemId, CancellationToken.None)).OnHand);

        await service.ReceiveGoodsAsync(tenantId, task.Id, seed.WarehouseId, seed.UserId, "Anbara qəbul", CancellationToken.None);
        Assert.Equal(4, (await new WarehouseAvailabilityService(db).GetAvailabilityAsync(tenantId, seed.WarehouseId, seed.ItemId, CancellationToken.None)).OnHand);
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

    [Fact]
    public async Task IssueFieldRequestRejectsWhenIssueWouldMakeStockNegative()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var seed = await SeedAsync(db, tenantId, "PPE-NEGATIVE-GLOVE", "İş əlcəyi", 5);
        var service = CreateService(db);
        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            null,
            FieldWarehouseUrgency.Normal,
            null,
            [new SupplyRequestLineInput(seed.ItemId, 5, "Əlcək")]), CancellationToken.None);
        await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);

        var movement = await db.WarehouseStockMovements.SingleAsync(x => x.CatalogItemId == seed.ItemId && x.MovementType == WarehouseStockMovementType.OpeningBalance);
        movement.Quantity = 2;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IssueFieldRequestAsync(tenantId, request.Id, seed.WarehouseId, seed.UserId, "Prorab", null, CancellationToken.None));

        Assert.Equal("Warehouse issue would make stock negative.", ex.Message);
        Assert.Empty(await db.WarehouseIssues.ToListAsync());
        Assert.Equal(2, (await new WarehouseAvailabilityService(db).GetAvailabilityAsync(tenantId, seed.WarehouseId, seed.ItemId, CancellationToken.None)).OnHand);
    }

    private static async Task<ProcurementTask> CreateProcurementTaskScenarioAsync(BuildTrackDbContext db, ISupplyChainService service, Guid tenantId, SeedIds seed, decimal quantity)
    {
        var request = await service.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            tenantId,
            seed.SiteId,
            seed.UserId,
            Tomorrow,
            FieldWarehouseUrgency.Critical,
            null,
            [new SupplyRequestLineInput(seed.ItemId, quantity, "Test")]), CancellationToken.None);
        await service.ApproveFieldRequestAsync(tenantId, request.Id, seed.UserId, null, CancellationToken.None);
        var needId = await db.ProcurementNeeds.Where(x => x.SourceRequestId == request.Id).Select(x => x.Id).SingleAsync();
        var task = await service.CreateProcurementTaskAsync(tenantId, [needId], seed.AgentId, seed.UserId, null, CancellationToken.None);
        return await service.StartShoppingAsync(tenantId, task.Id, seed.AgentId, CancellationToken.None);
    }

    private static ProcurementAttachment CreateAttachment(Guid tenantId, Guid taskId, Guid? taskLineId, Guid userId, ProcurementAttachmentType type) =>
        new()
        {
            TenantId = tenantId,
            TaskId = taskId,
            TaskLineId = taskLineId,
            UploadedByUserId = userId,
            AttachmentType = type,
            StoragePath = $"/tmp/{Guid.NewGuid():N}",
            OriginalFileName = type == ProcurementAttachmentType.ProductPhoto ? "photo.jpg" : "receipt.pdf",
            MimeType = type == ProcurementAttachmentType.ProductPhoto ? "image/jpeg" : "application/pdf",
            Size = 2048,
        };

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
            new WarehouseUsagePolicyService(db));

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
