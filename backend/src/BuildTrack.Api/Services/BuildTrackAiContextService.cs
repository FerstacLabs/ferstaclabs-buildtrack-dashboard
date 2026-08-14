using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Api.Services;

public interface IBuildTrackAiContextService
{
    Task<BuildTrackAiContextResult> BuildContextAsync(
        string message,
        string? selectedProjectId,
        Guid? selectedSiteId,
        CancellationToken cancellationToken);
}

public sealed record BuildTrackAiContextResult(
    bool Success,
    int StatusCode,
    string? Error,
    JsonObject Context,
    IReadOnlyList<string> SourceModules,
    TimeSpan BuildDuration);

public sealed class BuildTrackAiContextService(
    BuildTrackDbContext db,
    ITenantContext tenantContext,
    ILogger<BuildTrackAiContextService> logger) : IBuildTrackAiContextService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int DetailLimit = 16;

    public async Task<BuildTrackAiContextResult> BuildContextAsync(
        string message,
        string? selectedProjectId,
        Guid? selectedSiteId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required for AI context.");
        var userId = tenantContext.UserId;
        var role = tenantContext.Role ?? "Unknown";
        var requestedModules = ResolveRequestedModules(message);
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tenant",
            "sites",
            "project-progress",
            "workers",
            "attendance",
            "payroll",
            "daily-reports",
            "warehouse",
            "procurement",
            "supply",
            "audit",
            "camera",
        };
        foreach (var module in requestedModules) modules.Add(module);

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var currentUser = userId is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == userId)
                .Select(x => new { x.Id, x.FullName, x.Role, x.Status })
                .FirstOrDefaultAsync(cancellationToken);

        var sites = await db.Sites.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Address, x.TimeZone })
            .ToListAsync(cancellationToken);

        await BuildTrack.Api.ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, cancellationToken);
        var normalizedSelectedProjectId = string.IsNullOrWhiteSpace(selectedProjectId) ? null : selectedProjectId.Trim();
        var projects = await db.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var selectedProject = normalizedSelectedProjectId is null
            ? null
            : projects.FirstOrDefault(x => string.Equals(x.Id, normalizedSelectedProjectId, StringComparison.OrdinalIgnoreCase));
        if (normalizedSelectedProjectId is not null && selectedProject is null)
        {
            stopwatch.Stop();
            return new BuildTrackAiContextResult(
                false,
                StatusCodes.Status404NotFound,
                "Seçilmiş layihə bu tenant üçün tapılmadı.",
                new JsonObject(),
                modules.ToArray(),
                stopwatch.Elapsed);
        }

        var selectedSite = selectedSiteId is null ? null : sites.FirstOrDefault(x => x.Id == selectedSiteId.Value);
        if (selectedSiteId is not null && selectedSite is null)
        {
            stopwatch.Stop();
            return new BuildTrackAiContextResult(
                false,
                StatusCodes.Status404NotFound,
                "Seçilmiş layihə bu tenant üçün tapılmadı.",
                new JsonObject(),
                modules.ToArray(),
                stopwatch.Elapsed);
        }

        var projectSiteIds = selectedProject is null
            ? null
            : await db.ProjectSites.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ProjectId == selectedProject.Id)
                .Select(x => x.SiteId)
                .ToArrayAsync(cancellationToken);
        if (selectedProject is not null && selectedSite is not null && !projectSiteIds!.Contains(selectedSite.Id))
        {
            stopwatch.Stop();
            return new BuildTrackAiContextResult(
                false,
                StatusCodes.Status400BadRequest,
                "Seçilmiş layihə bu daxili layihə qrupuna aid deyil.",
                new JsonObject(),
                modules.ToArray(),
                stopwatch.Elapsed);
        }

        var siteIds = selectedSite is not null
            ? new[] { selectedSite.Id }
            : selectedProject is not null
                ? projectSiteIds!
                : sites.Select(x => x.Id).ToArray();
        var siteIdSet = siteIds.ToHashSet();
        var selectedSiteFilterId = selectedSite?.Id;
        var timeZone = AttendanceSchedulePolicy.ResolveTimeZone(selectedSite?.TimeZone ?? "Asia/Baku");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var workers = await db.Workers.AsNoTracking()
            .Include(x => x.CameraIdentities)
            .Include(x => x.SiteAssignments)
            .Where(x => x.TenantId == tenantId
                && (selectedSiteFilterId == null
                    || x.SiteId == selectedSiteFilterId.Value
                    || x.SiteAssignments.Any(a => a.SiteId == selectedSiteFilterId.Value && a.Status == WorkerSiteAssignmentStatus.Active)))
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
        var workerIds = workers.Select(x => x.Id).ToHashSet();

        var attendanceSessions = await db.AttendanceSessions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId) && x.WorkDate == today)
            .OrderByDescending(x => x.LastSeenTime ?? x.CheckInTime)
            .ToListAsync(cancellationToken);
        var monthAttendanceSessions = await db.AttendanceSessions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId) && x.WorkDate >= monthStart && x.WorkDate <= today)
            .ToListAsync(cancellationToken);
        var recentAttendanceEvents = await db.AttendanceEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(DetailLimit)
            .Select(x => new
            {
                x.Id,
                x.SiteId,
                x.WorkerExternalId,
                x.WorkerName,
                x.Direction,
                x.Status,
                x.Method,
                x.Source,
                x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var projectProgress = await BuildProjectProgressContextAsync(
            db,
            tenantId,
            selectedProject?.Id,
            selectedSite?.Id,
            message,
            cancellationToken);

        var dailyReports = await db.SupervisorDailyReports.AsNoTracking()
            .Include(x => x.SupervisorUser)
            .Include(x => x.Site)
            .Include(x => x.Lines)
                .ThenInclude(line => line.SmetaItem)
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.ReportDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);

        var fieldRequests = await db.FieldWarehouseRequests.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.CatalogItem)
            .Include(x => x.Lines)
                .ThenInclude(line => line.CatalogItem)
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);

        var catalogItems = await db.FieldWarehouseCatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var catalogIds = catalogItems.Select(x => x.Id).ToArray();
        var stockMovements = await db.WarehouseStockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && catalogIds.Contains(x.CatalogItemId))
            .GroupBy(x => x.CatalogItemId)
            .Select(group => new
            {
                CatalogItemId = group.Key,
                Quantity = group.Sum(x =>
                    x.MovementType == WarehouseStockMovementType.OpeningBalance
                    || x.MovementType == WarehouseStockMovementType.PurchaseReceipt
                    || x.MovementType == WarehouseStockMovementType.Return
                    || x.MovementType == WarehouseStockMovementType.TransferIn
                    || x.MovementType == WarehouseStockMovementType.AdjustmentIncrease
                        ? x.Quantity
                        : -x.Quantity),
            })
            .ToListAsync(cancellationToken);
        var reservations = await db.WarehouseReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == WarehouseReservationStatus.Active && catalogIds.Contains(x.CatalogItemId))
            .GroupBy(x => x.CatalogItemId)
            .Select(group => new { CatalogItemId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var procurementNeeds = await db.ProcurementNeeds.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.CatalogItem)
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);
        var procurementTasks = await db.ProcurementTasks.AsNoTracking()
            .Include(x => x.AssignedProcurementUser)
            .Include(x => x.Lines)
                .ThenInclude(line => line.CatalogItem)
            .Include(x => x.Lines)
                .ThenInclude(line => line.ProcurementNeed)
            .Where(x => x.TenantId == tenantId)
            .Where(x => selectedSiteFilterId == null
                || x.Lines.Any(line => line.ProcurementNeed != null && line.ProcurementNeed.SiteId == selectedSiteFilterId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);
        var goodsReceipts = await db.WarehouseGoodsReceipts.AsNoTracking()
            .Include(x => x.Lines)
                .ThenInclude(line => line.CatalogItem)
            .Include(x => x.ProcurementTask)
                .ThenInclude(task => task!.Lines)
                .ThenInclude(line => line.ProcurementNeed)
            .Where(x => x.TenantId == tenantId)
            .Where(x => selectedSiteFilterId == null
                || (x.ProcurementTask != null && x.ProcurementTask.Lines.Any(line => line.ProcurementNeed != null && line.ProcurementNeed.SiteId == selectedSiteFilterId.Value)))
            .OrderByDescending(x => x.ReceivedAt)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);

        var supervisorAssignments = await db.SupervisorSiteAssignments.AsNoTracking()
            .Include(x => x.SupervisorUser)
            .Include(x => x.Site)
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId) && x.IsActive)
            .OrderBy(x => x.SupervisorUser!.FullName)
            .ToListAsync(cancellationToken);
        var auditEvents = await db.SupervisorAuditEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (x.SiteId == null || siteIds.Contains(x.SiteId.Value)))
            .OrderByDescending(x => x.Timestamp)
            .Take(DetailLimit)
            .ToListAsync(cancellationToken);

        var devices = await db.Devices.AsNoTracking()
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.SiteId, x.Name, x.Vendor, x.Model, x.Mode, x.Status, x.LastSeenAt })
            .ToListAsync(cancellationToken);
        var securityEvents = await db.SecurityEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(DetailLimit)
            .Select(x => new { x.Id, x.SiteId, x.EventTime, x.EventType, x.Severity, x.Status, x.Method, x.Message, x.CreatedAt })
            .ToListAsync(cancellationToken);

        var workerDetails = ShouldIncludeDetails(requestedModules, "workers", message)
            ? workers.Take(DetailLimit).Select(worker => new JsonObject
            {
                ["id"] = worker.Id,
                ["siteId"] = worker.SiteId,
                ["workerCode"] = worker.ExternalWorkerCode,
                ["fullName"] = worker.FullName,
                ["brigade"] = worker.Brigade,
                ["role"] = worker.Role,
                ["status"] = worker.Status.ToString(),
                ["hourlyRate"] = worker.HourlyRate,
                ["riskScore"] = worker.RiskScore,
                ["cameraLinked"] = worker.CameraIdentities.Any(),
            }).ToArray()
            : [];

        var stockByItem = stockMovements.ToDictionary(x => x.CatalogItemId, x => x.Quantity);
        var reservedByItem = reservations.ToDictionary(x => x.CatalogItemId, x => x.Quantity);
        var warehouseRows = catalogItems.Select(item =>
        {
            var onHand = stockByItem.GetValueOrDefault(item.Id);
            var reserved = reservedByItem.GetValueOrDefault(item.Id);
            var available = onHand - reserved;
            return new WarehouseAiRow(
                item,
                onHand,
                reserved,
                available,
                item.MinimumStockLevel is decimal minimum && available <= minimum);
        }).ToArray();

        var context = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["generatedAt"] = DateTimeOffset.UtcNow,
                ["timezone"] = selectedSite?.TimeZone ?? "Asia/Baku",
                ["workDate"] = today.ToString("yyyy-MM-dd"),
                ["selectedProjectId"] = selectedProject?.Id,
                ["selectedProjectName"] = selectedProject?.Name ?? "Bütün layihələr",
                ["selectedSiteId"] = selectedSite?.Id,
                ["selectedSiteName"] = selectedSite?.Name ?? "Bütün layihələr",
                ["role"] = role,
                ["sourceModules"] = new JsonArray(modules.OrderBy(x => x).Select(x => JsonValue.Create(x)).ToArray()),
            },
            ["tenant"] = new JsonObject
            {
                ["companyName"] = tenant?.CompanyName ?? "BuildTrack",
                ["tenantCode"] = tenant?.Code,
                ["currentUserName"] = currentUser?.FullName,
                ["currentUserRole"] = currentUser?.Role.ToString() ?? role,
            },
            ["sites"] = new JsonArray(sites.Select(site => new JsonObject
            {
                ["id"] = site.Id,
                ["name"] = site.Name,
                ["address"] = site.Address,
                ["timeZone"] = site.TimeZone,
            }).ToArray<JsonNode?>()),
            ["executiveSummary"] = new JsonObject
            {
                ["siteScope"] = selectedSite?.Name ?? "Bütün layihələr",
                ["projectProgressPercent"] = projectProgress.ProgressPercent,
                ["projectStageCount"] = projectProgress.StageCount,
                ["projectWorkItemCount"] = projectProgress.WorkItemCount,
                ["workerCount"] = workers.Count,
                ["activeWorkerCount"] = workers.Count(x => x.Status == WorkerStatus.Active),
                ["todayPresentCount"] = attendanceSessions.Select(x => x.WorkerExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ["todayOpenSessionCount"] = attendanceSessions.Count(x => x.Status == AttendanceSessionStatus.Open),
                ["todayWorkedHours"] = Math.Round(attendanceSessions.Sum(GetSessionHours), 2),
                ["monthWorkedHours"] = Math.Round(monthAttendanceSessions.Sum(GetSessionHours), 2),
                ["estimatedPayrollToday"] = Math.Round(attendanceSessions.Sum(session => EstimateSessionPay(session, workers)), 2),
                ["pendingDailyReports"] = dailyReports.Count(x => x.Status is FieldDailyReportStatus.Submitted or FieldDailyReportStatus.NeedsCorrection),
                ["criticalWarehouseItems"] = warehouseRows.Count(x => x.IsCritical),
                ["openWarehouseRequests"] = fieldRequests.Count(x => x.Status is not FieldWarehouseRequestStatus.Closed and not FieldWarehouseRequestStatus.Cancelled and not FieldWarehouseRequestStatus.Issued),
                ["openProcurementNeeds"] = procurementNeeds.Count(x => x.Status is not ProcurementNeedStatus.Received and not ProcurementNeedStatus.Cancelled),
                ["activeProcurementTasks"] = procurementTasks.Count(x => x.Status is not ProcurementTaskStatus.Verified and not ProcurementTaskStatus.Cancelled),
                ["unreviewedSecurityEvents"] = securityEvents.Count(x => x.Status == SecurityEventStatus.Open),
            },
            ["projectProgress"] = projectProgress.Context,
            ["workers"] = new JsonObject
            {
                ["summary"] = new JsonObject
                {
                    ["total"] = workers.Count,
                    ["active"] = workers.Count(x => x.Status == WorkerStatus.Active),
                    ["inactive"] = workers.Count(x => x.Status != WorkerStatus.Active),
                    ["cameraLinked"] = workers.Count(x => x.CameraIdentities.Any()),
                    ["highRisk"] = workers.Count(x => x.RiskScore >= 60),
                    ["brigades"] = new JsonArray(workers
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.Brigade) ? "Təyin edilməyib" : x.Brigade!)
                        .OrderByDescending(x => x.Count())
                        .Take(12)
                        .Select(x => new JsonObject { ["name"] = x.Key, ["workerCount"] = x.Count() })
                        .ToArray<JsonNode?>()),
                },
                ["details"] = new JsonArray(workerDetails.Cast<JsonNode?>().ToArray()),
            },
            ["attendance"] = new JsonObject
            {
                ["summary"] = new JsonObject
                {
                    ["date"] = today.ToString("yyyy-MM-dd"),
                    ["presentWorkers"] = attendanceSessions.Select(x => x.WorkerExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    ["openSessions"] = attendanceSessions.Count(x => x.Status == AttendanceSessionStatus.Open),
                    ["closedSessions"] = attendanceSessions.Count(x => x.Status == AttendanceSessionStatus.Closed),
                    ["workedHours"] = Math.Round(attendanceSessions.Sum(GetSessionHours), 2),
                    ["recentEventCount"] = recentAttendanceEvents.Count,
                },
                ["todaySessions"] = new JsonArray(attendanceSessions.Take(DetailLimit).Select(session => new JsonObject
                {
                    ["workerExternalId"] = session.WorkerExternalId,
                    ["workerName"] = session.WorkerName,
                    ["status"] = session.Status.ToString(),
                    ["checkInTime"] = session.CheckInTime,
                    ["lastSeenTime"] = session.LastSeenTime,
                    ["checkOutTime"] = session.CheckOutTime,
                    ["workedHours"] = Math.Round(GetSessionHours(session), 2),
                    ["source"] = session.Source,
                }).ToArray<JsonNode?>()),
                ["recentEvents"] = new JsonArray(recentAttendanceEvents.Select(evt => new JsonObject
                {
                    ["workerExternalId"] = evt.WorkerExternalId,
                    ["workerName"] = evt.WorkerName,
                    ["status"] = evt.Status.ToString(),
                    ["method"] = evt.Method.ToString(),
                    ["direction"] = evt.Direction.ToString(),
                    ["source"] = evt.Source,
                    ["createdAt"] = evt.CreatedAt,
                }).ToArray<JsonNode?>()),
            },
            ["payroll"] = new JsonObject
            {
                ["summary"] = new JsonObject
                {
                    ["basis"] = "AttendanceSessions + worker hourly rates",
                    ["todayEstimatedAmount"] = Math.Round(attendanceSessions.Sum(session => EstimateSessionPay(session, workers)), 2),
                    ["monthEstimatedAmount"] = Math.Round(monthAttendanceSessions.Sum(session => EstimateSessionPay(session, workers)), 2),
                    ["workerTariffCount"] = workers.Count(x => x.HourlyRate > 0),
                },
            },
            ["dailyReports"] = BuildDailyReportContext(dailyReports),
            ["warehouse"] = BuildWarehouseContext(warehouseRows, fieldRequests, requestedModules, message),
            ["procurement"] = BuildProcurementContext(procurementNeeds, procurementTasks, goodsReceipts),
            ["supply"] = new JsonObject
            {
                ["catalogItemCount"] = catalogItems.Count,
                ["supplierCount"] = await db.Suppliers.AsNoTracking().CountAsync(x => x.TenantId == tenantId, cancellationToken),
                ["notificationsUnread"] = await db.SupplyNotifications.AsNoTracking().CountAsync(x => x.TenantId == tenantId && x.Status == SupplyNotificationStatus.Unread, cancellationToken),
            },
            ["supervisors"] = new JsonObject
            {
                ["activeAssignments"] = supervisorAssignments.Count,
                ["assignments"] = new JsonArray(supervisorAssignments.Take(DetailLimit).Select(assignment => new JsonObject
                {
                    ["supervisorName"] = assignment.SupervisorUser?.FullName,
                    ["siteName"] = assignment.Site?.Name,
                    ["notes"] = assignment.Notes,
                }).ToArray<JsonNode?>()),
            },
            ["audit"] = new JsonObject
            {
                ["recentRiskFlags"] = auditEvents.Count(x => x.RiskFlag),
                ["recentEvents"] = new JsonArray(auditEvents.Take(DetailLimit).Select(evt => new JsonObject
                {
                    ["timestamp"] = evt.Timestamp,
                    ["supervisor"] = evt.SupervisorNameSnapshot,
                    ["action"] = evt.Action,
                    ["entityType"] = evt.EntityType,
                    ["riskFlag"] = evt.RiskFlag,
                    ["description"] = evt.Description,
                }).ToArray<JsonNode?>()),
            },
            ["camera"] = new JsonObject
            {
                ["deviceCount"] = devices.Count,
                ["onlineDevices"] = devices.Count(x => x.Status == DeviceStatus.Online),
                ["devices"] = new JsonArray(devices.Select(device => new JsonObject
                {
                    ["id"] = device.Id,
                    ["siteId"] = device.SiteId,
                    ["name"] = device.Name,
                    ["vendor"] = device.Vendor,
                    ["model"] = device.Model,
                    ["mode"] = device.Mode.ToString(),
                    ["status"] = device.Status.ToString(),
                    ["lastSeenAt"] = device.LastSeenAt,
                }).ToArray<JsonNode?>()),
                ["securityEvents"] = new JsonArray(securityEvents.Select(evt => new JsonObject
                {
                    ["eventTime"] = evt.EventTime,
                    ["eventType"] = evt.EventType.ToString(),
                    ["severity"] = evt.Severity.ToString(),
                    ["status"] = evt.Status.ToString(),
                    ["method"] = evt.Method,
                    ["message"] = evt.Message,
                }).ToArray<JsonNode?>()),
            },
            ["safety"] = new JsonObject
            {
                ["secretPolicy"] = "Context excludes passwords, hashes, API keys, JWTs, Dahua credentials, private storage paths and binary images.",
            },
        };

        stopwatch.Stop();
        logger.LogInformation(
            "AI context built. TenantId={TenantId}; UserId={UserId}; SelectedProjectId={SelectedProjectId}; SelectedSiteId={SelectedSiteId}; Modules={Modules}; DurationMs={DurationMs}; ContextChars={ContextChars}",
            tenantId,
            userId,
            selectedProject?.Id,
            selectedSite?.Id,
            string.Join(',', modules.OrderBy(x => x)),
            stopwatch.ElapsedMilliseconds,
            context.ToJsonString(JsonOptions).Length);

        return new BuildTrackAiContextResult(true, StatusCodes.Status200OK, null, context, modules.OrderBy(x => x).ToArray(), stopwatch.Elapsed);
    }

    private static HashSet<string> ResolveRequestedModules(string message)
    {
        var normalized = NormalizeText(message);
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ContainsAny(normalized, "anbar", "warehouse", "stok", "stock", "material", "kaska", "elcek", "əlcək", "beton", "armatur", "kabel", "svirlo")) modules.Add("warehouse");
        if (ContainsAny(normalized, "satinalma", "satınalma", "procurement", "supplier", "techizat", "təchizat", "catishmaz", "çatışmaz")) modules.Add("procurement");
        if (ContainsAny(normalized, "isci", "işçi", "worker", "briqada", "heyet", "heyət")) modules.Add("workers");
        if (ContainsAny(normalized, "davamiyyet", "davamiyyət", "geldi", "gəldi", "isde", "işdə", "saat", "kamera")) modules.Add("attendance");
        if (ContainsAny(normalized, "maas", "maaş", "payroll", "emekhaqqi", "əməkhaqqı", "tarif")) modules.Add("payroll");
        if (ContainsAny(normalized, "smeta", "gedisat", "gedişat", "progress", "etap", "isler", "işlər", "gecikir")) modules.Add("project-progress");
        if (ContainsAny(normalized, "prorab", "supervisor", "hesabat", "gundelik", "gündəlik")) modules.Add("daily-reports");
        if (ContainsAny(normalized, "audit", "risk", "tehlukesizlik", "təhlükəsizlik", "tanimayan", "tanınmayan")) modules.Add("audit");
        if (modules.Count == 0 || ContainsAny(normalized, "umumi", "ümumi", "veziyyet", "vəziyyət", "brief", "xulase", "xülasə")) modules.Add("all");
        return modules;
    }

    private static bool ShouldIncludeDetails(HashSet<string> requestedModules, string module, string message) =>
        requestedModules.Contains("all") || requestedModules.Contains(module) || NormalizeText(message).Length < 80;

    private static bool ContainsAny(string value, params string[] tokens) => tokens.Any(value.Contains);

    private static string NormalizeText(string value) => value.Trim().ToLowerInvariant();

    private static async Task<ProjectProgressAiContext> BuildProjectProgressContextAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        string? selectedProjectId,
        Guid? selectedSiteId,
        string message,
        CancellationToken cancellationToken)
    {
        var projectQuery = db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(selectedProjectId))
        {
            projectQuery = projectQuery.Where(x => x.Id == selectedProjectId);
        }

        var projects = await projectQuery.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var projectIds = projects.Select(x => x.Id).ToArray();
        var projectSiteQuery = db.ProjectSites.AsNoTracking()
            .Include(x => x.Site)
            .Where(x => x.TenantId == tenantId && projectIds.Contains(x.ProjectId));
        if (selectedSiteId is not null)
        {
            projectSiteQuery = projectSiteQuery.Where(x => x.SiteId == selectedSiteId.Value);
        }

        var projectSites = await projectSiteQuery.OrderBy(x => x.Site!.Name).ToListAsync(cancellationToken);
        var scopedSiteIds = selectedSiteId is not null
            ? new[] { selectedSiteId.Value }
            : projectSites.Select(x => x.SiteId).Distinct().ToArray();

        var stageQuery = db.ProjectStages.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && projectIds.Contains(x.ProjectId));
        var workItemQuery = db.ProjectWorkItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && projectIds.Contains(x.ProjectId));
        var crewQuery = db.ProjectCrews.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && projectIds.Contains(x.ProjectId));
        var materialQuery = db.ProjectWorkItemMaterials.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && projectIds.Contains(x.ProjectId));
        if (selectedSiteId is not null)
        {
            stageQuery = stageQuery.Where(x => x.SiteId == selectedSiteId.Value);
            workItemQuery = workItemQuery.Where(x => x.SiteId == selectedSiteId.Value);
            crewQuery = crewQuery.Where(x => x.SiteId == selectedSiteId.Value);
            materialQuery = materialQuery.Where(x => x.SiteId == selectedSiteId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(selectedProjectId))
        {
            stageQuery = stageQuery.Where(x => x.SiteId == null || scopedSiteIds.Contains(x.SiteId.Value));
            workItemQuery = workItemQuery.Where(x => scopedSiteIds.Contains(x.SiteId));
            crewQuery = crewQuery.Where(x => x.SiteId == null || scopedSiteIds.Contains(x.SiteId.Value));
            materialQuery = materialQuery.Where(x => x.SiteId == null || scopedSiteIds.Contains(x.SiteId.Value));
        }

        var stages = await stageQuery.OrderBy(x => x.Order).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var workItems = await workItemQuery.OrderByDescending(x => x.TotalCost).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var crews = await crewQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var materials = await materialQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new ProjectProgressAiContext(new JsonObject
            {
                ["summary"] = new JsonObject { ["available"] = false },
            }, 0, 0, 0);
        }

        var workItemTotal = workItems.Sum(x => x.TotalCost);
        var stageTotal = stages.Sum(x => x.TotalCost);
        var totalCost = workItemTotal > 0 ? workItemTotal : stageTotal;
        var weightedProgress = totalCost > 0
            ? Math.Round(workItems.Count > 0
                ? workItems.Sum(x => x.TotalCost * x.ProgressPercent) / totalCost
                : stages.Sum(x => x.TotalCost * x.ProgressPercent) / totalCost, 1)
            : Math.Round(stages.Select(x => x.ProgressPercent).DefaultIfEmpty(0).Average(), 1);
        var relevantWorkItems = SelectRelevantWorkItems(workItems, message, DetailLimit);

        var context = new JsonObject
        {
            ["summary"] = new JsonObject
            {
                ["available"] = true,
                ["source"] = "normalized-project-tables",
                ["projectCount"] = projects.Count,
                ["objectCount"] = projectSites.Count,
                ["stageCount"] = stages.Count,
                ["workItemCount"] = workItems.Count,
                ["crewCount"] = crews.Count,
                ["materialPlanCount"] = materials.Count,
                ["totalCost"] = Math.Round(totalCost, 2),
                ["laborCost"] = Math.Round(workItems.Sum(x => x.LaborTotal), 2),
                ["materialCost"] = Math.Round(workItems.Sum(x => x.MaterialTotal), 2),
                ["plannedHours"] = Math.Round(workItems.Sum(x => x.PlannedHours), 2),
                ["actualHours"] = Math.Round(workItems.Sum(x => x.ActualHours), 2),
                ["progressPercent"] = weightedProgress,
                ["delayedStages"] = stages.Count(x => x.Status == ProjectEntityStatus.Delayed),
            },
            ["projects"] = new JsonArray(projects.Take(DetailLimit).Select(project => new JsonObject
            {
                ["id"] = project.Id,
                ["name"] = project.Name,
                ["status"] = project.Status.ToString(),
                ["activeEstimateVersionId"] = project.ActiveEstimateVersionId,
            }).ToArray<JsonNode?>()),
            ["objects"] = new JsonArray(projectSites.Take(DetailLimit).Select(projectSite => new JsonObject
            {
                ["id"] = projectSite.SiteId,
                ["projectId"] = projectSite.ProjectId,
                ["name"] = projectSite.Site?.Name,
                ["status"] = projectSite.Status.ToString(),
            }).ToArray<JsonNode?>()),
            ["stages"] = new JsonArray(stages.OrderByDescending(x => x.TotalCost).Take(DetailLimit).Select(stage => new JsonObject
            {
                ["id"] = stage.Id,
                ["objectId"] = stage.SiteId,
                ["name"] = stage.Name,
                ["status"] = stage.Status.ToString(),
                ["progressPercent"] = stage.ProgressPercent,
                ["totalCost"] = stage.TotalCost,
                ["plannedHours"] = stage.PlannedHours,
                ["actualHours"] = stage.ActualHours,
            }).ToArray<JsonNode?>()),
            ["relevantWorkItems"] = new JsonArray(relevantWorkItems.Select(item => new JsonObject
            {
                ["id"] = item.Id,
                ["objectId"] = item.SiteId,
                ["stageId"] = item.StageId,
                ["name"] = item.Name,
                ["unit"] = item.Unit,
                ["quantity"] = item.Quantity,
                ["completedQuantity"] = item.CompletedQuantity,
                ["status"] = item.Status.ToString(),
                ["progressPercent"] = item.ProgressPercent,
                ["totalCost"] = item.TotalCost,
            }).ToArray<JsonNode?>()),
            ["crews"] = new JsonArray(crews.Take(DetailLimit).Select(crew => new JsonObject
            {
                ["id"] = crew.Id,
                ["objectId"] = crew.SiteId,
                ["name"] = crew.Name,
                ["type"] = crew.Type,
                ["foremanName"] = crew.ForemanName,
                ["workerCount"] = crew.WorkerCount,
                ["activeWorkStageId"] = crew.ActiveWorkStageId,
                ["activeWorkItemId"] = crew.ActiveWorkItemId,
            }).ToArray<JsonNode?>()),
        };

        return new ProjectProgressAiContext(context, stages.Count, workItems.Count, weightedProgress);
    }

    private static ProjectProgressAiContext BuildProjectProgressContext(string? workspaceJson, Guid? selectedSiteId, string message)
    {
        if (string.IsNullOrWhiteSpace(workspaceJson))
        {
            return new ProjectProgressAiContext(new JsonObject
            {
                ["summary"] = new JsonObject { ["available"] = false },
            }, 0, 0, 0);
        }

        var root = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
        var stages = FilterByObject(root["stages"] as JsonArray, selectedSiteId).OfType<JsonObject>().ToArray();
        var workItems = FilterByObject(root["workItems"] as JsonArray, selectedSiteId).OfType<JsonObject>().ToArray();
        var objects = FilterObjects(root["objects"] as JsonArray, selectedSiteId).OfType<JsonObject>().ToArray();
        var stageTotal = stages.Sum(x => GetDecimal(x["totalCost"]));
        var weightedProgress = stageTotal > 0
            ? Math.Round(stages.Sum(x => GetDecimal(x["totalCost"]) * GetDecimal(x["progressPercent"])) / stageTotal, 1)
            : Math.Round(stages.Select(x => GetDecimal(x["progressPercent"])).DefaultIfEmpty(0).Average(), 1);
        var relevantWorkItems = SelectRelevantRows(workItems, message, "name", DetailLimit);

        var context = new JsonObject
        {
            ["summary"] = new JsonObject
            {
                ["available"] = true,
                ["objectCount"] = objects.Length,
                ["stageCount"] = stages.Length,
                ["workItemCount"] = workItems.Length,
                ["totalCost"] = Math.Round(stageTotal, 2),
                ["laborCost"] = Math.Round(stages.Sum(x => GetDecimal(x["laborCost"])), 2),
                ["materialCost"] = Math.Round(stages.Sum(x => GetDecimal(x["materialCost"])), 2),
                ["plannedHours"] = Math.Round(workItems.Sum(x => GetDecimal(x["plannedHours"])), 2),
                ["actualHours"] = Math.Round(workItems.Sum(x => GetDecimal(x["actualHours"])), 2),
                ["progressPercent"] = weightedProgress,
                ["delayedStages"] = stages.Count(x => string.Equals(GetString(x["status"]), "Delayed", StringComparison.OrdinalIgnoreCase)),
            },
            ["stages"] = new JsonArray(stages.OrderByDescending(x => GetDecimal(x["totalCost"])).Take(DetailLimit).Select(stage => new JsonObject
            {
                ["id"] = GetString(stage["id"]),
                ["objectId"] = GetString(stage["objectId"]),
                ["name"] = GetString(stage["name"]),
                ["status"] = GetString(stage["status"]),
                ["progressPercent"] = GetDecimal(stage["progressPercent"]),
                ["totalCost"] = GetDecimal(stage["totalCost"]),
                ["plannedHours"] = GetDecimal(stage["plannedHours"]),
                ["actualHours"] = GetDecimal(stage["actualHours"]),
            }).ToArray<JsonNode?>()),
            ["relevantWorkItems"] = new JsonArray(relevantWorkItems.Select(item => new JsonObject
            {
                ["id"] = GetString(item["id"]),
                ["objectId"] = GetString(item["objectId"]),
                ["stageId"] = GetString(item["stageId"]),
                ["name"] = GetString(item["name"]),
                ["unit"] = GetString(item["unit"]),
                ["quantity"] = GetDecimal(item["quantity"]),
                ["completedQuantity"] = GetDecimal(item["completedQuantity"]),
                ["status"] = GetString(item["status"]),
                ["progressPercent"] = GetDecimal(item["progressPercent"]),
                ["totalCost"] = GetDecimal(item["totalCost"]),
            }).ToArray<JsonNode?>()),
        };

        return new ProjectProgressAiContext(context, stages.Length, workItems.Length, weightedProgress);
    }

    private static JsonObject BuildDailyReportContext(IReadOnlyList<SupervisorDailyReport> reports) => new()
    {
        ["summary"] = new JsonObject
        {
            ["totalLoaded"] = reports.Count,
            ["submitted"] = reports.Count(x => x.Status == FieldDailyReportStatus.Submitted),
            ["approved"] = reports.Count(x => x.Status == FieldDailyReportStatus.Approved),
            ["needsCorrection"] = reports.Count(x => x.Status == FieldDailyReportStatus.NeedsCorrection),
            ["rejected"] = reports.Count(x => x.Status == FieldDailyReportStatus.Rejected),
        },
        ["recentReports"] = new JsonArray(reports.Select(report => new JsonObject
        {
            ["id"] = report.Id,
            ["siteName"] = report.Site?.Name,
            ["reportDate"] = report.ReportDate.ToString("yyyy-MM-dd"),
            ["supervisorName"] = report.SupervisorUser?.FullName,
            ["status"] = report.Status.ToString(),
            ["lineCount"] = report.Lines.Count,
            ["reportedQuantity"] = report.Lines.Sum(x => x.ReportedQuantity),
            ["workHours"] = report.Lines.Sum(x => x.WorkHours ?? 0),
            ["workerCount"] = report.Lines.Sum(x => x.WorkerCount ?? 0),
            ["reviewNote"] = report.ReviewNote,
        }).ToArray<JsonNode?>()),
    };

    private static JsonObject BuildWarehouseContext(
        IReadOnlyList<dynamic> warehouseRows,
        IReadOnlyList<FieldWarehouseRequest> requests,
        HashSet<string> requestedModules,
        string message)
    {
        var critical = warehouseRows.Where(x => x.IsCritical).Take(DetailLimit).ToArray();
        var details = ShouldIncludeDetails(requestedModules, "warehouse", message)
            ? warehouseRows.OrderBy(x => x.Available).Take(DetailLimit).ToArray()
            : critical;

        return new JsonObject
        {
            ["summary"] = new JsonObject
            {
                ["catalogItems"] = warehouseRows.Count,
                ["criticalItems"] = critical.Length,
                ["openRequests"] = requests.Count(x => x.Status is not FieldWarehouseRequestStatus.Closed and not FieldWarehouseRequestStatus.Cancelled and not FieldWarehouseRequestStatus.Issued),
                ["readyForPickup"] = requests.Count(x => x.Status == FieldWarehouseRequestStatus.ReadyForPickup),
                ["issued"] = requests.Count(x => x.Status == FieldWarehouseRequestStatus.Issued),
            },
            ["criticalOrRelevantStock"] = new JsonArray(details.Select(row => new JsonObject
            {
                ["catalogItemId"] = row.Item.Id,
                ["code"] = row.Item.Code,
                ["name"] = row.Item.Name,
                ["category"] = row.Item.Category,
                ["unit"] = row.Item.Unit,
                ["minimumStockLevel"] = row.Item.MinimumStockLevel,
                ["onHand"] = row.OnHand,
                ["reserved"] = row.Reserved,
                ["available"] = row.Available,
                ["isCritical"] = row.IsCritical,
            }).ToArray<JsonNode?>()),
            ["requests"] = new JsonArray(requests.Select(request => new JsonObject
            {
                ["id"] = request.Id,
                ["code"] = request.Code,
                ["siteName"] = request.Site?.Name,
                ["catalogItem"] = request.CatalogItem?.Name,
                ["requestedQuantity"] = request.RequestedQuantity,
                ["approvedQuantity"] = request.ApprovedQuantity,
                ["reservedQuantity"] = request.ReservedQuantity,
                ["issuedQuantity"] = request.IssuedQuantity,
                ["unit"] = request.Unit,
                ["urgency"] = request.Urgency.ToString(),
                ["status"] = request.Status.ToString(),
                ["reason"] = request.Reason,
                ["shortfall"] = request.Lines.Sum(line => Math.Max(0, line.RequestedQuantity - line.ReservedQuantity - line.IssuedQuantity)),
            }).ToArray<JsonNode?>()),
        };
    }

    private static JsonObject BuildProcurementContext(
        IReadOnlyList<ProcurementNeed> needs,
        IReadOnlyList<ProcurementTask> tasks,
        IReadOnlyList<WarehouseGoodsReceipt> goodsReceipts) => new()
    {
        ["summary"] = new JsonObject
        {
            ["openNeeds"] = needs.Count(x => x.Status is not ProcurementNeedStatus.Received and not ProcurementNeedStatus.Cancelled),
            ["totalShortfallQuantity"] = needs.Sum(x => x.ShortfallQuantity),
            ["purchasedQuantity"] = needs.Sum(x => x.PurchasedQuantity),
            ["receivedQuantity"] = needs.Sum(x => x.ReceivedQuantity),
            ["activeTasks"] = tasks.Count(x => x.Status is not ProcurementTaskStatus.Verified and not ProcurementTaskStatus.Cancelled),
            ["verifiedTasks"] = tasks.Count(x => x.Status == ProcurementTaskStatus.Verified),
            ["goodsReceipts"] = goodsReceipts.Count,
        },
        ["needs"] = new JsonArray(needs.Select(need => new JsonObject
        {
            ["id"] = need.Id,
            ["siteName"] = need.Site?.Name,
            ["catalogItem"] = need.CatalogItem?.Name,
            ["requiredQuantity"] = need.RequiredQuantity,
            ["alreadyAvailableQuantity"] = need.AlreadyAvailableQuantity,
            ["shortfallQuantity"] = need.ShortfallQuantity,
            ["purchasedQuantity"] = need.PurchasedQuantity,
            ["receivedQuantity"] = need.ReceivedQuantity,
            ["unit"] = need.Unit,
            ["priority"] = need.Priority.ToString(),
            ["status"] = need.Status.ToString(),
            ["requiredBy"] = need.RequiredBy?.ToString("yyyy-MM-dd"),
            ["reason"] = need.Reason,
        }).ToArray<JsonNode?>()),
        ["tasks"] = new JsonArray(tasks.Select(task => new JsonObject
        {
            ["id"] = task.Id,
            ["code"] = task.Code,
            ["assignedTo"] = task.AssignedProcurementUser?.FullName,
            ["status"] = task.Status.ToString(),
            ["priority"] = task.Priority.ToString(),
            ["requiredBy"] = task.RequiredBy?.ToString("yyyy-MM-dd"),
            ["lineCount"] = task.Lines.Count,
            ["requestedQuantity"] = task.Lines.Sum(x => x.RequestedQuantity),
            ["purchasedQuantity"] = task.Lines.Sum(x => x.PurchasedQuantity),
            ["acceptedQuantity"] = task.Lines.Sum(x => x.AcceptedQuantity),
        }).ToArray<JsonNode?>()),
        ["goodsReceipts"] = new JsonArray(goodsReceipts.Select(receipt => new JsonObject
        {
            ["id"] = receipt.Id,
            ["status"] = receipt.Status.ToString(),
            ["receivedAt"] = receipt.ReceivedAt,
            ["lineCount"] = receipt.Lines.Count,
            ["receivedQuantity"] = receipt.Lines.Sum(x => x.ReceivedQuantity),
            ["rejectedQuantity"] = receipt.Lines.Sum(x => x.RejectedQuantity),
        }).ToArray<JsonNode?>()),
    };

    private static IEnumerable<JsonNode?> FilterByObject(JsonArray? rows, Guid? selectedSiteId)
    {
        if (rows is null) yield break;
        var selected = selectedSiteId?.ToString();
        foreach (var row in rows)
        {
            if (row is not JsonObject obj) continue;
            if (selected is null || string.Equals(GetString(obj["objectId"]), selected, StringComparison.OrdinalIgnoreCase)) yield return obj.DeepClone();
        }
    }

    private static IEnumerable<JsonNode?> FilterObjects(JsonArray? rows, Guid? selectedSiteId)
    {
        if (rows is null) yield break;
        var selected = selectedSiteId?.ToString();
        foreach (var row in rows)
        {
            if (row is not JsonObject obj) continue;
            if (selected is null || string.Equals(GetString(obj["id"]), selected, StringComparison.OrdinalIgnoreCase)) yield return obj.DeepClone();
        }
    }

    private static IReadOnlyList<ProjectWorkItemRecord> SelectRelevantWorkItems(IReadOnlyList<ProjectWorkItemRecord> rows, string message, int limit)
    {
        var normalizedMessage = NormalizeText(message);
        var matching = rows
            .Where(row => NormalizeText(row.Name)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(token => token.Length > 2 && normalizedMessage.Contains(token)))
            .Take(limit)
            .ToArray();
        return matching.Length > 0 ? matching : rows.Take(limit).ToArray();
    }

    private static IReadOnlyList<JsonObject> SelectRelevantRows(IReadOnlyList<JsonObject> rows, string message, string propertyName, int limit)
    {
        var normalizedMessage = NormalizeText(message);
        var matching = rows
            .Where(row => NormalizeText(GetString(row[propertyName]) ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(token => token.Length > 2 && normalizedMessage.Contains(token)))
            .Take(limit)
            .ToArray();
        return matching.Length > 0 ? matching : rows.Take(limit).ToArray();
    }

    private static decimal GetDecimal(JsonNode? node)
    {
        if (node is null) return 0;
        try
        {
            return node.GetValue<decimal>();
        }
        catch
        {
            return 0;
        }
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static double GetSessionHours(AttendanceSession session)
    {
        var end = session.CheckOutTime ?? session.LastSeenTime ?? DateTimeOffset.UtcNow;
        return Math.Max(0, (end - session.CheckInTime).TotalHours);
    }

    private static double EstimateSessionPay(AttendanceSession session, IReadOnlyList<Worker> workers)
    {
        var worker = session.WorkerId is Guid workerId
            ? workers.FirstOrDefault(x => x.Id == workerId)
            : workers.FirstOrDefault(x => string.Equals(x.ExternalWorkerCode, session.WorkerExternalId, StringComparison.OrdinalIgnoreCase));
        return (double)(worker?.HourlyRate ?? 0) * GetSessionHours(session);
    }

    private sealed record ProjectProgressAiContext(JsonObject Context, int StageCount, int WorkItemCount, decimal ProgressPercent);

    private sealed record WarehouseAiRow(
        FieldWarehouseCatalogItem Item,
        decimal OnHand,
        decimal Reserved,
        decimal Available,
        bool IsCritical);
}
