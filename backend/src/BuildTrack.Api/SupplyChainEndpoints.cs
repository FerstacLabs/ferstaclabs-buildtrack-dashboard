using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Api;

public static class SupplyChainEndpoints
{
    public static WebApplication MapSupplyChainEndpoints(this WebApplication app)
    {
        app.MapGet("/api/catalog/items/search", SearchCatalogAsync);
        app.MapGet("/api/catalog/units", GetUnitsAsync);
        app.MapPost("/api/field/warehouse/cart-requests", CreateFieldCartRequestAsync);

        app.MapGet("/api/procurement/warehouse/stock", GetWarehouseStockAsync);
        app.MapGet("/api/procurement/warehouse/requests", GetManagementWarehouseRequestsAsync);
        app.MapPost("/api/procurement/warehouse/requests/{id:guid}/approve", ApproveWarehouseRequestAsync);
        app.MapPost("/api/procurement/warehouse/requests/{id:guid}/issue", IssueWarehouseRequestAsync);
        app.MapGet("/api/procurement/needs", GetProcurementNeedsAsync);
        app.MapPost("/api/procurement/tasks", CreateProcurementTaskAsync);
        app.MapGet("/api/procurement/tasks", GetProcurementTasksAsync);
        app.MapGet("/api/procurement/tasks/{id:guid}", GetProcurementTaskAsync);
        app.MapPost("/api/procurement/tasks/{id:guid}/verify", VerifyProcurementTaskAsync);
        app.MapPost("/api/procurement/goods-receipts", CreateGoodsReceiptAsync);
        app.MapGet("/api/procurement/suppliers", GetSuppliersAsync);
        app.MapPost("/api/procurement/suppliers", SaveSupplierAsync);
        app.MapGet("/api/procurement/agents", GetProcurementAgentsAsync);
        app.MapPost("/api/procurement/agents", CreateProcurementAgentAsync);
        app.MapPut("/api/procurement/agents/{id:guid}", UpdateProcurementAgentAsync);
        app.MapPost("/api/procurement/agents/{id:guid}/reset-password", ResetProcurementAgentPasswordAsync);
        app.MapGet("/api/procurement/trace/{fieldRequestId:guid}", GetTraceAsync);

        app.MapGet("/api/supply/me", GetSupplyMeAsync);
        app.MapGet("/api/supply/dashboard", GetSupplyDashboardAsync);
        app.MapGet("/api/supply/tasks", GetSupplyTasksAsync);
        app.MapGet("/api/supply/tasks/{id:guid}", GetSupplyTaskAsync);
        app.MapPost("/api/supply/tasks/{id:guid}/accept", AcceptSupplyTaskAsync);
        app.MapPost("/api/supply/tasks/{id:guid}/start", StartSupplyTaskAsync);
        app.MapPost("/api/supply/tasks/{taskId:guid}/lines/{lineId:guid}/purchase", UpdateSupplyTaskLinePurchaseAsync);
        app.MapPost("/api/supply/tasks/{id:guid}/attachments", UploadSupplyAttachmentAsync);
        app.MapPost("/api/supply/tasks/{id:guid}/submit", SubmitSupplyTaskAsync);
        app.MapGet("/api/supply/notifications", GetSupplyNotificationsAsync);
        app.MapGet("/api/supply/settings", GetSupplySettings);

        return app;
    }

    private static async Task<IResult> SearchCatalogAsync(string? q, string? category, string? subcategory, int? limit, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var query = db.FieldWarehouseCatalogItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(subcategory)) query = query.Where(x => x.Subcategory == subcategory);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(text)
                || (x.NameAz != null && x.NameAz.ToLower().Contains(text))
                || (x.NameRu != null && x.NameRu.ToLower().Contains(text))
                || (x.NameEn != null && x.NameEn.ToLower().Contains(text))
                || (x.Code != null && x.Code.ToLower().Contains(text))
                || (x.SearchAliases != null && x.SearchAliases.ToLower().Contains(text)));
        }

        var take = Math.Clamp(limit ?? 50, 1, 150);
        var rows = await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Take(take)
            .Select(x => new CatalogSearchItemDto(x.Id, x.Name, x.NameAz, x.NameRu, x.NameEn, x.Category, x.Subcategory, x.Unit, x.Code, x.ItemType))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetUnitsAsync(BuildTrackDbContext db, CancellationToken ct)
    {
        var units = await db.SupplyUnits.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SupplyUnitDto(x.Id, x.Code, x.NameAz, x.NameEn, x.NameRu))
            .ToListAsync(ct);
        return Results.Ok(units);
    }

    private static async Task<IResult> CreateFieldCartRequestAsync(
        CreateFieldWarehouseCartRequest request,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IFieldAccessService fieldAccess,
        ISupplyChainService supplyChain,
        CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(request.SiteId, ct);
        if (request.Lines.Count == 0) return Results.BadRequest(new { error = "Ən azı bir material sətri lazımdır" });
        var created = await supplyChain.CreateFieldRequestAsync(new CreateSupplyRequestInput(
            RequireTenantId(tenantContext),
            request.SiteId,
            RequireUserId(tenantContext),
            request.NeededBy,
            request.Urgency,
            request.GeneralNote,
            request.Lines.Select(x => new SupplyRequestLineInput(x.CatalogItemId, x.RequestedQuantity, x.Reason, x.SpecificationJson)).ToArray()), ct);
        return Results.Ok(ToFieldWarehouseRequestDto(created));
    }

    private static async Task<IResult> GetWarehouseStockAsync(BuildTrackDbContext db, ITenantContext tenantContext, IWarehouseAvailabilityService availability, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var warehouse = await availability.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var items = await db.FieldWarehouseCatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
        var availabilityMap = await availability.GetAvailabilityMapAsync(tenantId, warehouse.Id, items.Select(x => x.Id).ToArray(), ct);
        return Results.Ok(items.Select(item =>
        {
            var row = availabilityMap.GetValueOrDefault(item.Id, new WarehouseAvailability(warehouse.Id, item.Id, 0, 0, 0));
            return new WarehouseStockItemDto(item.Id, item.Name, item.Category, item.Subcategory, item.Unit, item.Code, row.OnHand, row.Reserved, row.Available, ResolveStockStatus(row.Available));
        }));
    }

    private static async Task<IResult> GetManagementWarehouseRequestsAsync(Guid? siteId, BuildTrackDbContext db, ITenantContext tenantContext, IWarehouseAvailabilityService availability, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var warehouse = await availability.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var query = db.FieldWarehouseRequests.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .Where(x => x.TenantId == tenantId);
        if (siteId is not null) query = query.Where(x => x.SiteId == siteId.Value);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var itemIds = rows.SelectMany(x => x.Lines.Select(l => l.CatalogItemId)).Distinct().ToArray();
        var availabilityMap = await availability.GetAvailabilityMapAsync(tenantId, warehouse.Id, itemIds, ct);
        return Results.Ok(rows.Select(x => ToManagementRequestDto(x, warehouse.Id, availabilityMap)));
    }

    private static async Task<IResult> ApproveWarehouseRequestAsync(Guid id, ApproveProcurementNeedRequest request, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var result = await supplyChain.ApproveFieldRequestAsync(RequireTenantId(tenantContext), id, RequireUserId(tenantContext), request.ManagerComment, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> IssueWarehouseRequestAsync(Guid id, IssueWarehouseRequest request, ITenantContext tenantContext, IWarehouseAvailabilityService availability, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var warehouse = request.WarehouseId is Guid warehouseId
            ? new Warehouse { Id = warehouseId, TenantId = tenantId }
            : await availability.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var issue = await supplyChain.IssueFieldRequestAsync(tenantId, id, warehouse.Id, RequireUserId(tenantContext), request.RecipientName, request.HandoverNote, ct);
        return Results.Ok(new { issue.Id, issue.Status, issue.IssuedAt });
    }

    private static async Task<IResult> GetProcurementNeedsAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var rows = await db.ProcurementNeeds.AsNoTracking()
            .Include(x => x.CatalogItem)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .ToListAsync(ct);
        return Results.Ok(rows.Select(ToNeedDto));
    }

    private static async Task<IResult> CreateProcurementTaskAsync(AssignProcurementTaskRequest request, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var task = await supplyChain.CreateProcurementTaskAsync(RequireTenantId(tenantContext), request.NeedIds, request.AssignedProcurementUserId, RequireUserId(tenantContext), request.ManagerInstruction, ct);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> GetProcurementTasksAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var rows = await LoadTaskQuery(db, RequireTenantId(tenantContext)).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        return Results.Ok(rows.Select(ToTaskDto));
    }

    private static async Task<IResult> GetProcurementTaskAsync(Guid id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role) && !IsProcurementAgentRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var query = LoadTaskQuery(db, tenantId).Where(x => x.Id == id);
        if (IsProcurementAgentRole(tenantContext.Role)) query = query.Where(x => x.AssignedProcurementUserId == RequireUserId(tenantContext));
        var task = await query.FirstOrDefaultAsync(ct);
        return task is null ? Results.NotFound() : Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> VerifyProcurementTaskAsync(Guid id, VerifyProcurementTaskRequest request, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var task = await supplyChain.VerifyTaskAsync(RequireTenantId(tenantContext), id, RequireUserId(tenantContext), request.VerificationNote, ct);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> CreateGoodsReceiptAsync(CreateGoodsReceiptRequest request, ITenantContext tenantContext, IWarehouseAvailabilityService availability, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var warehouse = request.WarehouseId is Guid id ? new Warehouse { Id = id } : await availability.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var receipt = await supplyChain.ReceiveGoodsAsync(tenantId, request.TaskId, warehouse.Id, RequireUserId(tenantContext), request.Note, ct);
        return Results.Ok(new { receipt.Id, receipt.Status, receipt.ReceivedAt });
    }

    private static async Task<IResult> GetSuppliersAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role) && !IsProcurementAgentRole(tenantContext.Role)) return Results.Forbid();
        var rows = await db.Suppliers.AsNoTracking()
            .Where(x => x.TenantId == RequireTenantId(tenantContext))
            .OrderBy(x => x.Name)
            .Select(x => new SupplierDto(x.Id, x.Name, x.TaxId, x.Phone, x.Email, x.Address, x.ContactPerson, x.Categories, x.Status, x.Notes))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> SaveSupplierAsync(SaveSupplierRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Təchizatçı adı lazımdır" });
        var tenantId = RequireTenantId(tenantContext);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == request.Name.Trim(), ct);
        if (supplier is null)
        {
            supplier = new Supplier { TenantId = tenantId, Name = request.Name.Trim() };
            db.Suppliers.Add(supplier);
        }
        supplier.TaxId = Clean(request.TaxId);
        supplier.Phone = Clean(request.Phone);
        supplier.Email = Clean(request.Email);
        supplier.Address = Clean(request.Address);
        supplier.ContactPerson = Clean(request.ContactPerson);
        supplier.Categories = Clean(request.Categories);
        supplier.Status = request.Status;
        supplier.Notes = Clean(request.Notes);
        supplier.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new SupplierDto(supplier.Id, supplier.Name, supplier.TaxId, supplier.Phone, supplier.Email, supplier.Address, supplier.ContactPerson, supplier.Categories, supplier.Status, supplier.Notes));
    }

    private static async Task<IResult> GetProcurementAgentsAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var users = await db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Role == BuildTrackUserRole.ProcurementAgent)
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);
        var ids = users.Select(x => x.Id).ToArray();
        var openTasks = await db.ProcurementTasks.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.AssignedProcurementUserId != null && ids.Contains(x.AssignedProcurementUserId.Value) && x.Status != ProcurementTaskStatus.Completed && x.Status != ProcurementTaskStatus.Cancelled && x.Status != ProcurementTaskStatus.Verified)
            .GroupBy(x => x.AssignedProcurementUserId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return Results.Ok(users.Select(x => new ProcurementAgentDto(x.Id, x.FullName, x.Email, x.Phone, x.Status, openTasks.GetValueOrDefault(x.Id), x.LastLoginAt)));
    }

    private static async Task<IResult> CreateProcurementAgentAsync(CreateProcurementAgentRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.TemporaryPassword)) return Results.BadRequest(new { error = "Ad, email və müvəqqəti şifrə lazımdır" });
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { error = "Bu email artıq istifadə olunur" });
        var user = new AppUser
        {
            TenantId = RequireTenantId(tenantContext),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            PasswordHash = BuildTrackPasswordHasher.HashPassword(request.TemporaryPassword),
            Role = BuildTrackUserRole.ProcurementAgent,
            Status = BuildTrackUserStatus.Active,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/agents/{user.Id}", new ProcurementAgentDto(user.Id, user.FullName, user.Email, user.Phone, user.Status, 0, user.LastLoginAt));
    }

    private static async Task<IResult> UpdateProcurementAgentAsync(Guid id, UpdateProcurementAgentRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var user = await db.Users.FirstOrDefaultAsync(x => x.TenantId == RequireTenantId(tenantContext) && x.Id == id && x.Role == BuildTrackUserRole.ProcurementAgent, ct);
        if (user is null) return Results.NotFound();
        user.FullName = request.FullName.Trim();
        user.Phone = Clean(request.Phone);
        user.Status = request.Status;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetProcurementAgentPasswordAsync(Guid id, ResetSupervisorPasswordRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8) return Results.BadRequest(new { error = "Müvəqqəti şifrə ən azı 8 simvol olmalıdır" });
        var user = await db.Users.FirstOrDefaultAsync(x => x.TenantId == RequireTenantId(tenantContext) && x.Id == id && x.Role == BuildTrackUserRole.ProcurementAgent, ct);
        if (user is null) return Results.NotFound();
        user.PasswordHash = BuildTrackPasswordHasher.HashPassword(request.TemporaryPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSupplyMeAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == RequireUserId(tenantContext), ct);
        return user is null ? Results.Unauthorized() : Results.Ok(new { user.Id, user.FullName, user.Email, user.Role, user.Status });
    }

    private static async Task<IResult> GetSupplyDashboardAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var userId = RequireUserId(tenantContext);
        var query = LoadTaskQuery(db, tenantId);
        if (IsProcurementAgentRole(tenantContext.Role)) query = query.Where(x => x.AssignedProcurementUserId == userId);
        var tasks = await query.OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
        var unread = await db.SupplyNotifications.AsNoTracking().CountAsync(x => x.TenantId == tenantId && (x.UserId == userId || x.Audience == SupplyNotificationAudience.Procurement) && x.Status == SupplyNotificationStatus.Unread, ct);
        return Results.Ok(new SupplyDashboardDto(
            tasks.Count(x => x.Status == ProcurementTaskStatus.Assigned || x.Status == ProcurementTaskStatus.Accepted),
            tasks.Count(x => x.Status == ProcurementTaskStatus.Shopping),
            tasks.Count(x => x.Status == ProcurementTaskStatus.SubmittedForVerification),
            unread,
            tasks.Select(ToTaskDto).ToArray()));
    }

    private static async Task<IResult> GetSupplyTasksAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var query = LoadTaskQuery(db, tenantId).OrderByDescending(x => x.CreatedAt);
        if (IsProcurementAgentRole(tenantContext.Role))
        {
            var userId = RequireUserId(tenantContext);
            query = query.Where(x => x.AssignedProcurementUserId == userId).OrderByDescending(x => x.CreatedAt);
        }
        var tasks = await query.Take(200).ToListAsync(ct);
        return Results.Ok(tasks.Select(ToTaskDto));
    }

    private static async Task<IResult> GetSupplyTaskAsync(Guid id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
        await GetProcurementTaskAsync(id, db, tenantContext, ct);

    private static async Task<IResult> AcceptSupplyTaskAsync(Guid id, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var task = await supplyChain.AcceptTaskAsync(RequireTenantId(tenantContext), id, RequireUserId(tenantContext), ct);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> StartSupplyTaskAsync(Guid id, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var task = await supplyChain.StartShoppingAsync(RequireTenantId(tenantContext), id, RequireUserId(tenantContext), ct);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> UpdateSupplyTaskLinePurchaseAsync(Guid taskId, Guid lineId, UpdateProcurementTaskLinePurchaseRequest request, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var line = await supplyChain.UpdateTaskLinePurchaseAsync(RequireTenantId(tenantContext), RequireUserId(tenantContext), new SupplyTaskLinePurchaseInput(lineId, request.PurchasedQuantity, request.UnitPrice, request.SupplierId, request.Note), ct);
        return Results.Ok(new { line.Id, line.Status, line.PurchasedQuantity, line.UnitPrice, line.SupplierId });
    }

    private static async Task<IResult> UploadSupplyAttachmentAsync(Guid id, HttpRequest request, BuildTrackDbContext db, ITenantContext tenantContext, ISupplyAttachmentStorage storage, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var userId = RequireUserId(tenantContext);
        var task = await db.ProcurementTasks.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (task is null) return Results.NotFound();
        if (IsProcurementAgentRole(tenantContext.Role) && task.AssignedProcurementUserId != userId) return Results.Forbid();
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null) return Results.BadRequest(new { error = "Fayl seçilməyib" });
        var type = Enum.TryParse<ProcurementAttachmentType>(form["attachmentType"].FirstOrDefault(), true, out var parsed)
            ? parsed
            : ProcurementAttachmentType.Other;
        var lineId = Guid.TryParse(form["taskLineId"].FirstOrDefault(), out var parsedLineId) ? parsedLineId : (Guid?)null;
        await using var stream = file.OpenReadStream();
        var attachment = await storage.SaveAsync(tenantId, id, lineId, userId, type, stream, file.FileName, file.ContentType, file.Length, ct);
        db.ProcurementAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { attachment.Id, attachment.AttachmentType, attachment.OriginalFileName, attachment.Size, attachment.CreatedAt });
    }

    private static async Task<IResult> SubmitSupplyTaskAsync(Guid id, ITenantContext tenantContext, ISupplyChainService supplyChain, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var task = await supplyChain.SubmitTaskForVerificationAsync(RequireTenantId(tenantContext), id, RequireUserId(tenantContext), ct);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> GetSupplyNotificationsAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var userId = RequireUserId(tenantContext);
        var rows = await db.SupplyNotifications.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (x.UserId == userId || x.Audience == SupplyNotificationAudience.Procurement))
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new SupplyNotificationDto(x.Id, x.Audience, x.Title, x.Message, x.ReferenceType, x.ReferenceId, x.Status, x.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static IResult GetSupplySettings(ITenantContext tenantContext)
    {
        if (!IsSupplyPortalRole(tenantContext.Role)) return Results.Forbid();
        return Results.Ok(new { evidenceRequired = true, requiredAttachments = new[] { "ProductPhoto", "Receipt" }, currency = "AZN" });
    }

    private static async Task<IResult> GetTraceAsync(Guid fieldRequestId, BuildTrackDbContext db, ITenantContext tenantContext, IWarehouseAvailabilityService availability, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var request = await db.FieldWarehouseRequests.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == fieldRequestId, ct);
        if (request is null) return Results.NotFound();
        var warehouse = await availability.GetOrCreateDefaultWarehouseAsync(tenantId, ct);
        var availabilityMap = await availability.GetAvailabilityMapAsync(tenantId, warehouse.Id, request.Lines.Select(x => x.CatalogItemId).ToArray(), ct);
        var needs = await db.ProcurementNeeds.AsNoTracking().Include(x => x.CatalogItem).Where(x => x.TenantId == tenantId && x.SourceRequestId == fieldRequestId).ToListAsync(ct);
        var needIds = needs.Select(x => x.Id).ToArray();
        var tasks = await LoadTaskQuery(db, tenantId).Where(x => x.Lines.Any(l => needIds.Contains(l.ProcurementNeedId))).ToListAsync(ct);
        var audit = await db.SupervisorAuditEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EntityId == fieldRequestId)
            .OrderBy(x => x.Timestamp)
            .Select(x => $"{x.Timestamp:O} - {x.Action}: {x.Description}")
            .ToListAsync(ct);
        return Results.Ok(new ProcurementTraceDto(request.Id, request.Code, request.Status, ToManagementRequestDto(request, warehouse.Id, availabilityMap).Lines, needs.Select(ToNeedDto).ToArray(), tasks.Select(ToTaskDto).ToArray(), audit));
    }

    private static IQueryable<ProcurementTask> LoadTaskQuery(BuildTrackDbContext db, Guid tenantId) =>
        db.ProcurementTasks.AsNoTracking()
            .Include(x => x.AssignedProcurementUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Supplier)
            .Where(x => x.TenantId == tenantId);

    private static FieldWarehouseRequestDto ToFieldWarehouseRequestDto(FieldWarehouseRequest request) =>
        new(
            request.Id,
            request.SiteId,
            request.Site?.Name,
            request.CatalogItemId,
            request.CatalogItem?.Name ?? request.Lines.FirstOrDefault()?.CatalogItem?.Name ?? string.Empty,
            request.CatalogItem?.Category ?? request.Lines.FirstOrDefault()?.CatalogItem?.Category ?? string.Empty,
            request.RequestedQuantity,
            request.Unit,
            request.NeededBy,
            request.Urgency,
            request.Reason,
            request.Justification,
            request.ManagerComment,
            request.Status,
            request.SupervisorUserId,
            request.SupervisorUser?.FullName,
            request.CreatedAt,
            request.UpdatedAt,
            request.Code,
            request.GeneralNote,
            request.AbnormalRequest,
            request.Lines.Select(x => new FieldWarehouseRequestLineDto(x.Id, x.CatalogItemId, x.CatalogItem?.Name ?? string.Empty, x.CatalogItem?.Category ?? string.Empty, x.RequestedQuantity, x.Unit, x.Reason, x.Status)).ToArray());

    private static ManagementWarehouseRequestDto ToManagementRequestDto(FieldWarehouseRequest request, Guid warehouseId, IReadOnlyDictionary<Guid, WarehouseAvailability> availability)
    {
        var lines = request.Lines.Select(line =>
        {
            var available = availability.GetValueOrDefault(line.CatalogItemId, new WarehouseAvailability(warehouseId, line.CatalogItemId, 0, 0, 0));
            var shortfall = Math.Max(0, line.RequestedQuantity - line.ReservedQuantity);
            return new ManagementWarehouseLineDto(
                line.Id,
                line.CatalogItemId,
                line.CatalogItem?.Name ?? string.Empty,
                line.CatalogItem?.Category ?? string.Empty,
                line.RequestedQuantity,
                line.ApprovedQuantity,
                line.ReservedQuantity,
                line.IssuedQuantity,
                available.OnHand,
                available.Available,
                shortfall,
                line.Unit,
                line.Reason,
                line.Status);
        }).ToArray();

        return new ManagementWarehouseRequestDto(
            request.Id,
            request.Code,
            request.SiteId,
            request.Site?.Name,
            request.SupervisorUserId,
            request.SupervisorUser?.FullName,
            request.NeededBy,
            request.Urgency,
            request.Status,
            request.GeneralNote,
            request.Justification,
            request.ManagerComment,
            request.AbnormalRequest,
            lines.Sum(x => x.RequestedQuantity),
            lines.Sum(x => x.ReservedQuantity),
            lines.Sum(x => x.ShortfallQuantity),
            request.CreatedAt,
            request.UpdatedAt,
            lines);
    }

    private static ProcurementNeedDto ToNeedDto(ProcurementNeed need) =>
        new(need.Id, need.SourceRequestId, need.SourceRequestLineId, need.CatalogItemId, need.CatalogItem?.Name ?? string.Empty, need.CatalogItem?.Category ?? string.Empty, need.RequiredQuantity, need.AlreadyAvailableQuantity, need.ShortfallQuantity, need.PurchasedQuantity, need.ReceivedQuantity, need.Unit, need.Priority, need.RequiredBy, need.Status, need.Reason, need.CreatedAt);

    private static ProcurementTaskDto ToTaskDto(ProcurementTask task) =>
        new(
            task.Id,
            task.Code,
            task.AssignedProcurementUserId,
            task.AssignedProcurementUser?.FullName,
            task.Status,
            task.Priority,
            task.RequiredBy,
            task.ManagerInstruction,
            task.CreatedAt,
            task.AssignedAt,
            task.StartedAt,
            task.SubmittedAt,
            task.VerifiedAt,
            task.VerificationNote,
            task.Lines.OrderBy(x => x.CatalogItem?.Name).Select(line => new ProcurementTaskLineDto(line.Id, line.ProcurementNeedId, line.CatalogItemId, line.CatalogItem?.Name ?? string.Empty, line.CatalogItem?.Category ?? string.Empty, line.RequestedQuantity, line.PurchasedQuantity, line.AcceptedQuantity, line.Unit, line.Status, line.Note, line.UnitPrice, line.SupplierId, line.Supplier?.Name)).ToArray());

    private static string ResolveStockStatus(decimal available)
    {
        if (available <= 0) return "Bitib";
        if (available <= 10) return "Kritik";
        if (available <= 30) return "Azalır";
        return "Normal";
    }

    private static bool IsManagementRole(string? role) =>
        string.Equals(role, BuildTrackUserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsProcurementAgentRole(string? role) =>
        string.Equals(role, BuildTrackUserRole.ProcurementAgent.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsSupplyPortalRole(string? role) => IsProcurementAgentRole(role) || IsManagementRole(role);

    private static Guid RequireTenantId(ITenantContext tenantContext) =>
        tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context is missing");

    private static Guid RequireUserId(ITenantContext tenantContext) =>
        tenantContext.UserId ?? throw new UnauthorizedAccessException("User context is missing");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
