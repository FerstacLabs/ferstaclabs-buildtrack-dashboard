using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed record WarehouseAvailability(Guid WarehouseId, Guid CatalogItemId, decimal OnHand, decimal Reserved, decimal Available);

public sealed record SupplyRequestLineInput(Guid CatalogItemId, decimal Quantity, string? Reason, string? SpecificationJson = null);

public sealed record CreateSupplyRequestInput(
    Guid TenantId,
    Guid SiteId,
    Guid SupervisorUserId,
    DateOnly? NeededBy,
    FieldWarehouseUrgency Urgency,
    string? GeneralNote,
    IReadOnlyList<SupplyRequestLineInput> Lines);

public sealed record SupplyReviewResult(Guid RequestId, decimal TotalRequested, decimal TotalReserved, decimal TotalShortfall, FieldWarehouseRequestStatus Status);

public sealed record SupplyTaskLinePurchaseInput(Guid TaskLineId, decimal PurchasedQuantity, decimal? UnitPrice, Guid? SupplierId, string? Note);

public interface IWarehouseAvailabilityService
{
    Task<Warehouse> GetOrCreateDefaultWarehouseAsync(Guid tenantId, CancellationToken ct);
    Task<WarehouseAvailability> GetAvailabilityAsync(Guid tenantId, Guid warehouseId, Guid catalogItemId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, WarehouseAvailability>> GetAvailabilityMapAsync(Guid tenantId, Guid warehouseId, IReadOnlyCollection<Guid> catalogItemIds, CancellationToken ct);
}

public interface IWarehouseUsagePolicyService
{
    Task<bool> IsAbnormalRequestAsync(Guid tenantId, FieldWarehouseCatalogItem item, decimal requestedQuantity, CancellationToken ct);
}

public interface ISupplyAttachmentStorage
{
    Task<ProcurementAttachment> SaveAsync(Guid tenantId, Guid taskId, Guid? taskLineId, Guid uploadedByUserId, ProcurementAttachmentType type, Stream content, string fileName, string? contentType, long size, CancellationToken ct);
}

public interface ISupplyChainService
{
    Task<FieldWarehouseRequest> CreateFieldRequestAsync(CreateSupplyRequestInput input, CancellationToken ct);
    Task<SupplyReviewResult> ApproveFieldRequestAsync(Guid tenantId, Guid requestId, Guid managerUserId, string? managerComment, CancellationToken ct);
    Task<ProcurementTask> CreateProcurementTaskAsync(Guid tenantId, IReadOnlyCollection<Guid> needIds, Guid? assignedProcurementUserId, Guid managerUserId, string? instruction, CancellationToken ct);
    Task<ProcurementTask> AcceptTaskAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct);
    Task<ProcurementTask> StartShoppingAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct);
    Task<ProcurementTaskLine> UpdateTaskLinePurchaseAsync(Guid tenantId, Guid procurementUserId, SupplyTaskLinePurchaseInput input, CancellationToken ct);
    Task<ProcurementTask> SubmitTaskForVerificationAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct);
    Task<ProcurementTask> VerifyTaskAsync(Guid tenantId, Guid taskId, Guid verifierUserId, string? verificationNote, CancellationToken ct);
    Task<WarehouseGoodsReceipt> ReceiveGoodsAsync(Guid tenantId, Guid taskId, Guid warehouseId, Guid receivedByUserId, string? note, CancellationToken ct);
    Task<WarehouseIssue> IssueFieldRequestAsync(Guid tenantId, Guid fieldRequestId, Guid warehouseId, Guid issuedByUserId, string? recipientName, string? handoverNote, CancellationToken ct);
}

public sealed class WarehouseAvailabilityService(BuildTrackDbContext db) : IWarehouseAvailabilityService
{
    public async Task<Warehouse> GetOrCreateDefaultWarehouseAsync(Guid tenantId, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault && x.IsActive, ct);
        if (warehouse is not null) return warehouse;

        warehouse = new Warehouse
        {
            TenantId = tenantId,
            Name = "Mərkəzi anbar",
            Address = "BuildTrack demo anbarı",
            IsDefault = true,
            IsActive = true,
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return warehouse;
    }

    public async Task<WarehouseAvailability> GetAvailabilityAsync(Guid tenantId, Guid warehouseId, Guid catalogItemId, CancellationToken ct)
    {
        var map = await GetAvailabilityMapAsync(tenantId, warehouseId, [catalogItemId], ct);
        return map.GetValueOrDefault(catalogItemId, new WarehouseAvailability(warehouseId, catalogItemId, 0, 0, 0));
    }

    public async Task<IReadOnlyDictionary<Guid, WarehouseAvailability>> GetAvailabilityMapAsync(Guid tenantId, Guid warehouseId, IReadOnlyCollection<Guid> catalogItemIds, CancellationToken ct)
    {
        var ids = catalogItemIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, WarehouseAvailability>();

        var movements = await db.WarehouseStockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && ids.Contains(x.CatalogItemId))
            .GroupBy(x => x.CatalogItemId)
            .Select(x => new
            {
                CatalogItemId = x.Key,
                OnHand = x.Sum(m => m.MovementType == WarehouseStockMovementType.Issue
                                    || m.MovementType == WarehouseStockMovementType.TransferOut
                                    || m.MovementType == WarehouseStockMovementType.AdjustmentDecrease
                                    || m.MovementType == WarehouseStockMovementType.WriteOff
                    ? -m.Quantity
                    : m.Quantity),
            })
            .ToListAsync(ct);

        var reserved = await db.WarehouseReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && ids.Contains(x.CatalogItemId) && x.Status == WarehouseReservationStatus.Active)
            .GroupBy(x => x.CatalogItemId)
            .Select(x => new { CatalogItemId = x.Key, Reserved = x.Sum(r => r.Quantity) })
            .ToListAsync(ct);

        var movementMap = movements.ToDictionary(x => x.CatalogItemId, x => x.OnHand);
        var reservedMap = reserved.ToDictionary(x => x.CatalogItemId, x => x.Reserved);
        return ids.ToDictionary(
            id => id,
            id =>
            {
                var onHand = movementMap.GetValueOrDefault(id);
                var activeReserved = reservedMap.GetValueOrDefault(id);
                return new WarehouseAvailability(warehouseId, id, onHand, activeReserved, Math.Max(0, onHand - activeReserved));
            });
    }
}

public sealed class WarehouseUsagePolicyService(BuildTrackDbContext db) : IWarehouseUsagePolicyService
{
    public async Task<bool> IsAbnormalRequestAsync(Guid tenantId, FieldWarehouseCatalogItem item, decimal requestedQuantity, CancellationToken ct)
    {
        var itemPolicy = await db.WarehouseUsagePolicies.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CatalogItemId == item.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var categoryPolicy = itemPolicy is null
            ? await db.WarehouseUsagePolicies.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.CatalogItemId == null && x.Category == item.Category)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct)
            : null;
        var limit = itemPolicy?.DefaultMaximumPerRequest ?? categoryPolicy?.DefaultMaximumPerRequest;
        return limit is not null && requestedQuantity > limit.Value;
    }
}

public sealed class SupplyAttachmentStorage(IConfiguration configuration) : ISupplyAttachmentStorage
{
    private readonly string rootPath = configuration["SUPPLY_ATTACHMENT_STORAGE_PATH"] ?? "/app/data/supply-attachments";

    public async Task<ProcurementAttachment> SaveAsync(Guid tenantId, Guid taskId, Guid? taskLineId, Guid uploadedByUserId, ProcurementAttachmentType type, Stream content, string fileName, string? contentType, long size, CancellationToken ct)
    {
        if (size <= 0) throw new InvalidOperationException("Fayl boşdur");
        if (size > 15 * 1024 * 1024) throw new InvalidOperationException("Fayl limiti 15 MB-dır");

        var tenantFolder = Path.Combine(rootPath, tenantId.ToString("N"), taskId.ToString("N"));
        Directory.CreateDirectory(tenantFolder);
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 12) extension = ".bin";
        var storagePath = Path.Combine(tenantFolder, $"{Guid.NewGuid():N}{extension}");
        await using (var stream = File.Create(storagePath))
        {
            await content.CopyToAsync(stream, ct);
        }

        return new ProcurementAttachment
        {
            TenantId = tenantId,
            TaskId = taskId,
            TaskLineId = taskLineId,
            UploadedByUserId = uploadedByUserId,
            AttachmentType = type,
            StoragePath = storagePath,
            OriginalFileName = Path.GetFileName(fileName),
            MimeType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Size = size,
        };
    }
}

public sealed class SupplyChainService(
    BuildTrackDbContext db,
    IWarehouseAvailabilityService availabilityService,
    IWarehouseUsagePolicyService usagePolicy) : ISupplyChainService
{
    public async Task<FieldWarehouseRequest> CreateFieldRequestAsync(CreateSupplyRequestInput input, CancellationToken ct)
    {
        if (input.Lines.Count == 0) throw new InvalidOperationException("Ən azı bir material sətri lazımdır");
        if (input.Lines.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("Miqdar sıfırdan böyük olmalıdır");

        var itemIds = input.Lines.Select(x => x.CatalogItemId).Distinct().ToArray();
        var items = await db.FieldWarehouseCatalogItems
            .Where(x => x.TenantId == input.TenantId && x.IsActive && itemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != itemIds.Length) throw new InvalidOperationException("Kataloq elementi tapılmadı");

        var firstLine = input.Lines[0];
        var firstItem = items[firstLine.CatalogItemId];
        var abnormal = false;
        foreach (var line in input.Lines)
        {
            abnormal |= await usagePolicy.IsAbnormalRequestAsync(input.TenantId, items[line.CatalogItemId], line.Quantity, ct);
        }

        var request = new FieldWarehouseRequest
        {
            TenantId = input.TenantId,
            SiteId = input.SiteId,
            SupervisorUserId = input.SupervisorUserId,
            CatalogItemId = firstItem.Id,
            Code = await GenerateRequestCodeAsync(input.TenantId, ct),
            RequestedQuantity = firstLine.Quantity,
            Unit = firstItem.Unit,
            NeededBy = input.NeededBy,
            Urgency = input.Urgency,
            Reason = Clean(firstLine.Reason) ?? Clean(input.GeneralNote) ?? "Sahə material sorğusu",
            GeneralNote = Clean(input.GeneralNote),
            AbnormalRequest = abnormal,
            Status = abnormal ? FieldWarehouseRequestStatus.NeedsJustification : FieldWarehouseRequestStatus.PendingApproval,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        foreach (var line in input.Lines)
        {
            var item = items[line.CatalogItemId];
            request.Lines.Add(new FieldWarehouseRequestLine
            {
                TenantId = input.TenantId,
                CatalogItemId = item.Id,
                RequestedQuantity = line.Quantity,
                ApprovedQuantity = 0,
                ReservedQuantity = 0,
                IssuedQuantity = 0,
                Unit = item.Unit,
                Reason = Clean(line.Reason),
                SpecificationJson = Clean(line.SpecificationJson),
                Status = FieldWarehouseRequestLineStatus.Pending,
            });
        }

        db.FieldWarehouseRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return await LoadRequestAsync(input.TenantId, request.Id, ct);
    }

    public async Task<SupplyReviewResult> ApproveFieldRequestAsync(Guid tenantId, Guid requestId, Guid managerUserId, string? managerComment, CancellationToken ct)
    {
        var warehouse = await availabilityService.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var request = await db.FieldWarehouseRequests
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Sorğu tapılmadı");

        if (request.Lines.Count == 0)
        {
            request.Lines.Add(new FieldWarehouseRequestLine
            {
                TenantId = tenantId,
                CatalogItemId = request.CatalogItemId,
                RequestedQuantity = request.RequestedQuantity,
                Unit = request.Unit,
                Reason = request.Reason,
                Status = FieldWarehouseRequestLineStatus.Pending,
            });
        }

        var itemIds = request.Lines.Select(x => x.CatalogItemId).Distinct().ToArray();
        var availability = await availabilityService.GetAvailabilityMapAsync(tenantId, warehouse.Id, itemIds, ct);
        decimal totalRequested = 0;
        decimal totalReserved = 0;
        decimal totalShortfall = 0;

        foreach (var line in request.Lines)
        {
            totalRequested += line.RequestedQuantity;
            var available = availability.GetValueOrDefault(line.CatalogItemId, new WarehouseAvailability(warehouse.Id, line.CatalogItemId, 0, 0, 0));
            var reserveQty = Math.Min(line.RequestedQuantity, available.Available);
            var shortfall = line.RequestedQuantity - reserveQty;
            line.ApprovedQuantity = line.RequestedQuantity;
            line.ReservedQuantity = reserveQty;
            line.Status = shortfall > 0
                ? reserveQty > 0 ? FieldWarehouseRequestLineStatus.NeedsProcurement : FieldWarehouseRequestLineStatus.NeedsProcurement
                : FieldWarehouseRequestLineStatus.Reserved;
            line.UpdatedAt = DateTimeOffset.UtcNow;
            totalReserved += reserveQty;
            totalShortfall += shortfall;

            if (reserveQty > 0 && !await db.WarehouseReservations.AnyAsync(x => x.TenantId == tenantId && x.RequestLineId == line.Id && x.Status == WarehouseReservationStatus.Active, ct))
            {
                db.WarehouseReservations.Add(new WarehouseReservation
                {
                    TenantId = tenantId,
                    WarehouseId = warehouse.Id,
                    CatalogItemId = line.CatalogItemId,
                    RequestLineId = line.Id,
                    Quantity = reserveQty,
                    Status = WarehouseReservationStatus.Active,
                });
            }

            if (shortfall > 0 && !await db.ProcurementNeeds.AnyAsync(x => x.TenantId == tenantId && x.SourceRequestLineId == line.Id && x.Status != ProcurementNeedStatus.Cancelled, ct))
            {
                db.ProcurementNeeds.Add(new ProcurementNeed
                {
                    TenantId = tenantId,
                    ProjectId = request.ProjectId,
                    SiteId = request.SiteId,
                    WarehouseId = warehouse.Id,
                    SourceRequestId = request.Id,
                    SourceRequestLineId = line.Id,
                    CatalogItemId = line.CatalogItemId,
                    RequiredQuantity = line.RequestedQuantity,
                    AlreadyAvailableQuantity = reserveQty,
                    ShortfallQuantity = shortfall,
                    Unit = line.Unit,
                    Priority = request.Urgency,
                    RequiredBy = request.NeededBy,
                    Reason = $"Anbarda {reserveQty:0.###} {line.Unit} var, {shortfall:0.###} {line.Unit} əlavə ehtiyacdır.",
                    Status = ProcurementNeedStatus.PendingApproval,
                    CreatedByUserId = managerUserId,
                });
            }
        }

        request.ApprovedQuantity = totalRequested;
        request.ReservedQuantity = totalReserved;
        request.Status = totalShortfall > 0
            ? totalReserved > 0 ? FieldWarehouseRequestStatus.PartiallyApproved : FieldWarehouseRequestStatus.InFulfillment
            : FieldWarehouseRequestStatus.ReadyForPickup;
        request.ManagerComment = Clean(managerComment);
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedByUserId = managerUserId;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return new SupplyReviewResult(request.Id, totalRequested, totalReserved, totalShortfall, request.Status);
    }

    public async Task<ProcurementTask> CreateProcurementTaskAsync(Guid tenantId, IReadOnlyCollection<Guid> needIds, Guid? assignedProcurementUserId, Guid managerUserId, string? instruction, CancellationToken ct)
    {
        var distinctNeedIds = needIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (distinctNeedIds.Length == 0) throw new InvalidOperationException("Satınalma ehtiyacı seçilməyib");
        var needs = await db.ProcurementNeeds
            .Include(x => x.CatalogItem)
            .Where(x => x.TenantId == tenantId && distinctNeedIds.Contains(x.Id))
            .ToListAsync(ct);
        if (needs.Count != distinctNeedIds.Length) throw new InvalidOperationException("Ehtiyaclardan biri tapılmadı");
        if (needs.Any(x => x.Status is ProcurementNeedStatus.Assigned or ProcurementNeedStatus.InPurchase or ProcurementNeedStatus.Purchased or ProcurementNeedStatus.Received))
            throw new InvalidOperationException("Seçilmiş ehtiyaclardan biri artıq task-a bağlanıb");

        var task = new ProcurementTask
        {
            TenantId = tenantId,
            Code = await GenerateTaskCodeAsync(tenantId, ct),
            AssignedProcurementUserId = assignedProcurementUserId,
            Status = assignedProcurementUserId is null ? ProcurementTaskStatus.Draft : ProcurementTaskStatus.Assigned,
            Priority = needs.OrderByDescending(x => x.Priority).First().Priority,
            RequiredBy = needs.Where(x => x.RequiredBy is not null).MinBy(x => x.RequiredBy)?.RequiredBy,
            ManagerInstruction = Clean(instruction),
            AssignedAt = assignedProcurementUserId is null ? null : DateTimeOffset.UtcNow,
        };
        foreach (var need in needs)
        {
            need.Status = ProcurementNeedStatus.Assigned;
            need.UpdatedAt = DateTimeOffset.UtcNow;
            task.Lines.Add(new ProcurementTaskLine
            {
                TenantId = tenantId,
                ProcurementNeedId = need.Id,
                CatalogItemId = need.CatalogItemId,
                RequestedQuantity = need.ShortfallQuantity,
                Unit = need.Unit,
                SpecificationJson = need.SourceRequestLine?.SpecificationJson,
                Status = ProcurementTaskLineStatus.Pending,
            });
        }

        db.ProcurementTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return await LoadTaskAsync(tenantId, task.Id, ct);
    }

    public async Task<ProcurementTask> AcceptTaskAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct)
    {
        var task = await RequireTaskForAgentAsync(tenantId, taskId, procurementUserId, ct);
        if (task.Status == ProcurementTaskStatus.Assigned) task.Status = ProcurementTaskStatus.Accepted;
        await db.SaveChangesAsync(ct);
        return await LoadTaskAsync(tenantId, task.Id, ct);
    }

    public async Task<ProcurementTask> StartShoppingAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct)
    {
        var task = await RequireTaskForAgentAsync(tenantId, taskId, procurementUserId, ct);
        task.Status = ProcurementTaskStatus.Shopping;
        task.StartedAt ??= DateTimeOffset.UtcNow;
        foreach (var line in task.Lines.Where(x => x.Status == ProcurementTaskLineStatus.Pending))
        {
            line.Status = ProcurementTaskLineStatus.Searching;
        }
        await db.SaveChangesAsync(ct);
        return await LoadTaskAsync(tenantId, task.Id, ct);
    }

    public async Task<ProcurementTaskLine> UpdateTaskLinePurchaseAsync(Guid tenantId, Guid procurementUserId, SupplyTaskLinePurchaseInput input, CancellationToken ct)
    {
        var line = await db.ProcurementTaskLines
            .Include(x => x.Task)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == input.TaskLineId, ct)
            ?? throw new InvalidOperationException("Task sətri tapılmadı");
        if (line.Task?.AssignedProcurementUserId != procurementUserId) throw new UnauthorizedAccessException("Bu task sizə təyin edilməyib");
        if (input.PurchasedQuantity < 0) throw new InvalidOperationException("Alınan miqdar mənfi ola bilməz");
        line.PurchasedQuantity = input.PurchasedQuantity;
        line.UnitPrice = input.UnitPrice;
        line.SupplierId = input.SupplierId;
        line.Note = Clean(input.Note);
        line.Status = input.PurchasedQuantity >= line.RequestedQuantity
            ? ProcurementTaskLineStatus.Purchased
            : input.PurchasedQuantity > 0 ? ProcurementTaskLineStatus.PartiallyPurchased : ProcurementTaskLineStatus.Searching;
        line.UpdatedAt = DateTimeOffset.UtcNow;
        if (line.Task.Status != ProcurementTaskStatus.SubmittedForVerification && line.Task.Status != ProcurementTaskStatus.Verified)
        {
            line.Task.Status = line.Task.Lines.Any(x => x.Status == ProcurementTaskLineStatus.PartiallyPurchased) ? ProcurementTaskStatus.PartiallyCompleted : ProcurementTaskStatus.Shopping;
        }
        await db.SaveChangesAsync(ct);
        return line;
    }

    public async Task<ProcurementTask> SubmitTaskForVerificationAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct)
    {
        var task = await RequireTaskForAgentAsync(tenantId, taskId, procurementUserId, ct);
        var hasReceipt = await db.ProcurementAttachments.AnyAsync(x => x.TenantId == tenantId && x.TaskId == taskId && x.AttachmentType == ProcurementAttachmentType.Receipt, ct);
        var hasProductPhoto = await db.ProcurementAttachments.AnyAsync(x => x.TenantId == tenantId && x.TaskId == taskId && x.AttachmentType == ProcurementAttachmentType.ProductPhoto, ct);
        if (!hasReceipt || !hasProductPhoto) throw new InvalidOperationException("Təhvil üçün çek və məhsul şəkli yüklənməlidir");
        task.Status = ProcurementTaskStatus.SubmittedForVerification;
        task.SubmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await LoadTaskAsync(tenantId, task.Id, ct);
    }

    public async Task<ProcurementTask> VerifyTaskAsync(Guid tenantId, Guid taskId, Guid verifierUserId, string? verificationNote, CancellationToken ct)
    {
        var task = await db.ProcurementTasks.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == taskId, ct)
            ?? throw new InvalidOperationException("Task tapılmadı");
        task.Status = ProcurementTaskStatus.Verified;
        task.VerifiedAt = DateTimeOffset.UtcNow;
        task.VerifiedByUserId = verifierUserId;
        task.VerificationNote = Clean(verificationNote);
        foreach (var line in task.Lines.Where(x => x.PurchasedQuantity > 0))
        {
            line.Status = ProcurementTaskLineStatus.Purchased;
            var need = await db.ProcurementNeeds.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == line.ProcurementNeedId, ct);
            if (need is not null)
            {
                need.PurchasedQuantity = Math.Max(need.PurchasedQuantity, line.PurchasedQuantity);
                need.Status = line.PurchasedQuantity >= need.ShortfallQuantity ? ProcurementNeedStatus.Purchased : ProcurementNeedStatus.PartiallyPurchased;
                need.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return await LoadTaskAsync(tenantId, task.Id, ct);
    }

    public async Task<WarehouseGoodsReceipt> ReceiveGoodsAsync(Guid tenantId, Guid taskId, Guid warehouseId, Guid receivedByUserId, string? note, CancellationToken ct)
    {
        var task = await db.ProcurementTasks
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == taskId, ct)
            ?? throw new InvalidOperationException("Task tapılmadı");
        if (task.Status != ProcurementTaskStatus.Verified) throw new InvalidOperationException("Yalnız təsdiqlənmiş satınalma anbara qəbul edilə bilər");

        var existingReceipt = await db.WarehouseGoodsReceipts
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProcurementTaskId == taskId && x.Status == WarehouseGoodsReceiptStatus.Verified, ct);
        if (existingReceipt is not null) return existingReceipt;

        var receipt = new WarehouseGoodsReceipt
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProcurementTaskId = taskId,
            ReceivedByUserId = receivedByUserId,
            Note = Clean(note),
            Status = WarehouseGoodsReceiptStatus.Verified,
        };
        foreach (var line in task.Lines.Where(x => x.PurchasedQuantity > 0))
        {
            receipt.Lines.Add(new WarehouseGoodsReceiptLine
            {
                TenantId = tenantId,
                ProcurementTaskLineId = line.Id,
                CatalogItemId = line.CatalogItemId,
                ExpectedQuantity = line.RequestedQuantity,
                ReceivedQuantity = line.PurchasedQuantity,
                RejectedQuantity = 0,
                Unit = line.Unit,
                Condition = WarehouseGoodsReceiptLineCondition.Accepted,
            });
            db.WarehouseStockMovements.Add(new WarehouseStockMovement
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                CatalogItemId = line.CatalogItemId,
                MovementType = WarehouseStockMovementType.PurchaseReceipt,
                Quantity = line.PurchasedQuantity,
                ReferenceType = "WarehouseGoodsReceiptLine",
                ReferenceId = line.Id,
                PerformedByUserId = receivedByUserId,
                Note = $"Satınalma task {task.Code}",
            });
            line.AcceptedQuantity = line.PurchasedQuantity;
            line.Status = ProcurementTaskLineStatus.Received;
            var need = await db.ProcurementNeeds.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == line.ProcurementNeedId, ct);
            if (need is not null)
            {
                need.ReceivedQuantity = Math.Max(need.ReceivedQuantity, line.PurchasedQuantity);
                need.Status = line.PurchasedQuantity >= need.ShortfallQuantity ? ProcurementNeedStatus.Received : ProcurementNeedStatus.AwaitingReceipt;
                need.UpdatedAt = DateTimeOffset.UtcNow;
                var requestLine = await db.FieldWarehouseRequestLines.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == need.SourceRequestLineId, ct);
                if (requestLine is not null)
                {
                    var reservationQty = Math.Min(line.PurchasedQuantity, Math.Max(0, requestLine.RequestedQuantity - requestLine.ReservedQuantity));
                    if (reservationQty > 0)
                    {
                        db.WarehouseReservations.Add(new WarehouseReservation
                        {
                            TenantId = tenantId,
                            WarehouseId = warehouseId,
                            CatalogItemId = line.CatalogItemId,
                            RequestLineId = requestLine.Id,
                            Quantity = reservationQty,
                        });
                        requestLine.ReservedQuantity += reservationQty;
                        requestLine.Status = requestLine.ReservedQuantity >= requestLine.RequestedQuantity ? FieldWarehouseRequestLineStatus.ReadyForIssue : FieldWarehouseRequestLineStatus.ProcurementInProgress;
                    }
                }
            }
        }

        task.Status = ProcurementTaskStatus.Completed;
        db.WarehouseGoodsReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        await UpdateRequestStatusesAfterReceiptAsync(tenantId, ct);
        return receipt;
    }

    public async Task<WarehouseIssue> IssueFieldRequestAsync(Guid tenantId, Guid fieldRequestId, Guid warehouseId, Guid issuedByUserId, string? recipientName, string? handoverNote, CancellationToken ct)
    {
        var request = await db.FieldWarehouseRequests
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == fieldRequestId, ct)
            ?? throw new InvalidOperationException("Sorğu tapılmadı");

        var existingIssue = await db.WarehouseIssues.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FieldRequestId == fieldRequestId && x.Status == WarehouseIssueStatus.Issued, ct);
        if (existingIssue is not null) return existingIssue;

        var reservations = await db.WarehouseReservations
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.Status == WarehouseReservationStatus.Active && request.Lines.Select(l => l.Id).Contains(x.RequestLineId))
            .ToListAsync(ct);
        if (reservations.Count == 0) throw new InvalidOperationException("Verilməyə hazır rezerv tapılmadı");

        var issue = new WarehouseIssue
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProjectId = request.ProjectId,
            SiteId = request.SiteId,
            FieldRequestId = request.Id,
            IssuedByUserId = issuedByUserId,
            ReceivedBySupervisorUserId = request.SupervisorUserId,
            RecipientName = Clean(recipientName),
            HandoverNote = Clean(handoverNote),
            Status = WarehouseIssueStatus.Issued,
        };
        foreach (var reservation in reservations)
        {
            var line = request.Lines.First(x => x.Id == reservation.RequestLineId);
            reservation.Status = WarehouseReservationStatus.Consumed;
            reservation.ConsumedAt = DateTimeOffset.UtcNow;
            line.IssuedQuantity += reservation.Quantity;
            line.Status = line.IssuedQuantity >= line.RequestedQuantity ? FieldWarehouseRequestLineStatus.Issued : FieldWarehouseRequestLineStatus.ReadyForIssue;
            issue.Lines.Add(new WarehouseIssueLine
            {
                TenantId = tenantId,
                CatalogItemId = reservation.CatalogItemId,
                Quantity = reservation.Quantity,
                Unit = line.Unit,
                ReservationId = reservation.Id,
            });
            db.WarehouseStockMovements.Add(new WarehouseStockMovement
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                CatalogItemId = reservation.CatalogItemId,
                MovementType = WarehouseStockMovementType.Issue,
                Quantity = reservation.Quantity,
                ReferenceType = "WarehouseIssueLine",
                ReferenceId = reservation.Id,
                PerformedByUserId = issuedByUserId,
                Note = $"Sahəyə verildi: {request.Code}",
            });
        }

        request.IssuedQuantity = request.Lines.Sum(x => x.IssuedQuantity);
        request.Status = request.Lines.All(x => x.Status == FieldWarehouseRequestLineStatus.Issued) ? FieldWarehouseRequestStatus.Issued : FieldWarehouseRequestStatus.ReadyForPickup;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        db.WarehouseIssues.Add(issue);
        await db.SaveChangesAsync(ct);
        return issue;
    }

    private async Task UpdateRequestStatusesAfterReceiptAsync(Guid tenantId, CancellationToken ct)
    {
        var requestIds = await db.FieldWarehouseRequestLines
            .Where(x => x.TenantId == tenantId && x.Status != FieldWarehouseRequestLineStatus.Issued)
            .Select(x => x.RequestId)
            .Distinct()
            .ToListAsync(ct);
        foreach (var requestId in requestIds)
        {
            var request = await db.FieldWarehouseRequests.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == requestId, ct);
            if (request is null) continue;
            if (request.Lines.All(x => x.ReservedQuantity >= x.RequestedQuantity))
            {
                request.Status = FieldWarehouseRequestStatus.ReadyForPickup;
                request.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<FieldWarehouseRequest> LoadRequestAsync(Guid tenantId, Guid requestId, CancellationToken ct) =>
        await db.FieldWarehouseRequests
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.CatalogItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .FirstAsync(x => x.TenantId == tenantId && x.Id == requestId, ct);

    private async Task<ProcurementTask> LoadTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct) =>
        await db.ProcurementTasks
            .Include(x => x.AssignedProcurementUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .FirstAsync(x => x.TenantId == tenantId && x.Id == taskId, ct);

    private async Task<ProcurementTask> RequireTaskForAgentAsync(Guid tenantId, Guid taskId, Guid procurementUserId, CancellationToken ct) =>
        await db.ProcurementTasks
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == taskId && x.AssignedProcurementUserId == procurementUserId, ct)
        ?? throw new UnauthorizedAccessException("Bu satınalma task-ı sizə təyin edilməyib");

    private async Task<string> GenerateRequestCodeAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await db.FieldWarehouseRequests.CountAsync(x => x.TenantId == tenantId, ct) + 1;
        return $"FR-{DateTime.UtcNow:yyMMdd}-{count:0000}";
    }

    private async Task<string> GenerateTaskCodeAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await db.ProcurementTasks.CountAsync(x => x.TenantId == tenantId, ct) + 1;
        return $"PO-{DateTime.UtcNow:yyMMdd}-{count:0000}";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
