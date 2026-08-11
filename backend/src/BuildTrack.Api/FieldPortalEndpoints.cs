using System.Text.Json;
using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Api;

public static class FieldPortalEndpoints
{
    public static WebApplication MapFieldPortalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/field/me", GetFieldMeAsync);
        app.MapGet("/api/field/assignments", GetFieldAssignmentsAsync);
        app.MapGet("/api/field/dashboard", GetFieldDashboardAsync);
        app.MapGet("/api/field/smeta-items", GetFieldSmetaItemsAsync);
        app.MapGet("/api/field/workers", GetFieldWorkersAsync);
        app.MapGet("/api/field/daily-reports", GetFieldDailyReportsAsync);
        app.MapGet("/api/field/daily-reports/{id:guid}", GetFieldDailyReportAsync);
        app.MapPost("/api/field/daily-reports", SaveFieldDailyReportAsync);
        app.MapPut("/api/field/daily-reports/{id:guid}", UpdateFieldDailyReportAsync);
        app.MapPost("/api/field/daily-reports/{id:guid}/submit", SubmitFieldDailyReportAsync);
        app.MapGet("/api/field/site-notes", GetFieldSiteNotesAsync);
        app.MapPost("/api/field/site-notes", CreateFieldSiteNoteAsync);
        app.MapGet("/api/field/worker-events", GetFieldWorkerEventsAsync);
        app.MapPost("/api/field/worker-events", CreateFieldWorkerEventAsync);
        app.MapGet("/api/field/warehouse/catalog", GetFieldWarehouseCatalogAsync);
        app.MapGet("/api/field/warehouse/requests", GetFieldWarehouseRequestsAsync);
        app.MapPost("/api/field/warehouse/requests", CreateFieldWarehouseRequestAsync);

        app.MapGet("/api/supervisors", GetSupervisorsAsync);
        app.MapPost("/api/supervisors", CreateSupervisorAsync);
        app.MapPut("/api/supervisors/{id:guid}", UpdateSupervisorAsync);
        app.MapPost("/api/supervisors/{id:guid}/reset-password", ResetSupervisorPasswordAsync);
        app.MapPost("/api/supervisors/{id:guid}/suspend", SetSupervisorSuspendedAsync);
        app.MapPost("/api/supervisors/{id:guid}/reactivate", SetSupervisorActiveAsync);
        app.MapGet("/api/management/field-reports", GetManagementFieldReportsAsync);
        app.MapPost("/api/management/field-reports/{id:guid}/review", ReviewManagementFieldReportAsync);
        app.MapGet("/api/management/field-warehouse-requests", GetManagementWarehouseRequestsAsync);
        app.MapPost("/api/management/field-warehouse-requests/{id:guid}/review", ReviewManagementWarehouseRequestAsync);
        app.MapGet("/api/supervisor-audit/events", GetSupervisorAuditEventsAsync);

        return app;
    }

    private static async Task<IResult> GetFieldMeAsync(
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IFieldAccessService fieldAccess,
        CancellationToken ct)
    {
        if (!await fieldAccess.CanUseFieldPortalAsync(ct)) return Results.Forbid();
        var userId = RequireUserId(tenantContext);
        var user = await db.Users.AsNoTracking().Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(new FieldMeResponse(
            ToAuthUserResponse(user),
            ToTenantResponse(user.Tenant!),
            MapAssignments(await fieldAccess.GetActiveAssignmentsAsync(ct))));
    }

    private static async Task<IResult> GetFieldAssignmentsAsync(IFieldAccessService fieldAccess, CancellationToken ct)
    {
        if (!await fieldAccess.CanUseFieldPortalAsync(ct)) return Results.Forbid();
        return Results.Ok(MapAssignments(await fieldAccess.GetActiveAssignmentsAsync(ct)));
    }

    private static async Task<IResult> GetFieldDashboardAsync(
        Guid? siteId,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IFieldAccessService fieldAccess,
        CancellationToken ct)
    {
        var site = await ResolveFieldSiteAsync(db, fieldAccess, siteId, ct);
        if (site is null) return Results.NotFound(new { error = "Assigned site was not found" });

        var timeZone = ResolveTimeZone(site.TimeZone);
        var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var todaySeen = await db.AttendanceSessions.AsNoTracking()
            .CountAsync(x => x.SiteId == site.Id && x.WorkDate == workDate, ct);
        var submittedReports = await db.SupervisorDailyReports.AsNoTracking()
            .CountAsync(x => x.SiteId == site.Id
                             && x.ReportDate == workDate
                             && x.Status != FieldDailyReportStatus.Draft,
                ct);
        var openRequests = await db.FieldWarehouseRequests.AsNoTracking()
            .CountAsync(x => x.SiteId == site.Id
                             && x.Status != FieldWarehouseRequestStatus.Closed
                             && x.Status != FieldWarehouseRequestStatus.Cancelled
                             && x.Status != FieldWarehouseRequestStatus.Issued,
                ct);
        var workerNotes = await db.SupervisorWorkerEvents.AsNoTracking()
            .CountAsync(x => x.SiteId == site.Id && x.Status == SupervisorWorkerEventStatus.Submitted, ct);
        var recent = await db.SupervisorAuditEvents.AsNoTracking()
            .Where(x => x.SiteId == site.Id)
            .OrderByDescending(x => x.Timestamp)
            .Take(8)
            .Select(x => new FieldActivityDto(x.Timestamp, x.Action, x.Description, x.RiskFlag ? "Risk" : null))
            .ToListAsync(ct);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == RequireUserId(tenantContext), ct);
        return Results.Ok(new FieldDashboardResponse(site.Id, site.Name, workDate, user?.FullName ?? "Prorab", todaySeen, workerNotes, submittedReports, openRequests, recent));
    }

    private static async Task<IResult> GetFieldSmetaItemsAsync(Guid siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(siteId, ct);
        await EnsureDefaultSmetaItemsAsync(db, siteId, ct);
        var rows = await db.FieldSmetaItems.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.IsActive)
            .OrderBy(x => x.StageName)
            .ThenBy(x => x.WorkName)
            .Select(x => new FieldSmetaItemDto(x.Id, x.SiteId, x.StageName, x.WorkName, x.Unit, x.WorkCategory))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetFieldWorkersAsync(Guid siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(siteId, ct);
        var timeZone = ResolveTimeZone(await db.Sites.AsNoTracking().Where(x => x.Id == siteId).Select(x => x.TimeZone).FirstOrDefaultAsync(ct));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var sessions = await db.AttendanceSessions.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.WorkDate == today)
            .ToListAsync(ct);
        var sessionsByWorker = sessions
            .Where(x => x.WorkerId is not null)
            .GroupBy(x => x.WorkerId!.Value)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(s => s.LastSeenTime ?? s.CheckInTime).First());
        var workers = await db.Workers.AsNoTracking()
            .Where(x => x.SiteId == siteId || x.SiteAssignments.Any(a => a.SiteId == siteId && a.Status == WorkerSiteAssignmentStatus.Active))
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);

        return Results.Ok(workers.Select(worker =>
        {
            sessionsByWorker.TryGetValue(worker.Id, out var session);
            DateTimeOffset? end = session is null ? null : session.LastSeenTime ?? session.CheckOutTime ?? session.CheckInTime;
            var worked = session is null ? 0 : Math.Max(0, (int)Math.Round(((end ?? DateTimeOffset.UtcNow) - session.CheckInTime).TotalMinutes));
            return new FieldWorkerDto(
                worker.Id,
                siteId,
                worker.ExternalWorkerCode,
                worker.FullName,
                worker.Brigade,
                worker.Role,
                session is null ? "Gorunmeyib" : session.Status == AttendanceSessionStatus.Open ? "Isde qeydiyyatda" : "Bagli",
                session?.CheckInTime,
                end,
                worked,
                worker.RiskScore);
        }));
    }

    private static async Task<IResult> GetFieldDailyReportsAsync(Guid? siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var assignments = await fieldAccess.GetActiveAssignmentsAsync(ct);
        var allowedSiteIds = assignments.Select(x => x.SiteId).ToArray();
        if (siteId is not null)
        {
            await fieldAccess.RequireSiteAccessAsync(siteId.Value, ct);
            allowedSiteIds = [siteId.Value];
        }

        var reports = await db.SupervisorDailyReports.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.SmetaItem)
            .Where(x => allowedSiteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.ReportDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return Results.Ok(reports.Select(MapReport));
    }

    private static async Task<IResult> GetFieldDailyReportAsync(Guid id, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var report = await db.SupervisorDailyReports.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.SmetaItem)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return Results.NotFound();
        await fieldAccess.RequireSiteAccessAsync(report.SiteId, ct);
        return Results.Ok(MapReport(report));
    }

    private static async Task<IResult> SaveFieldDailyReportAsync(
        SaveFieldDailyReportRequest request,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IFieldAccessService fieldAccess,
        CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(request.SiteId, ct);
        if (request.Lines.Count == 0) return Results.BadRequest(new { error = "At least one report line is required" });
        if (request.Lines.Any(x => x.ReportedQuantity <= 0)) return Results.BadRequest(new { error = "Reported quantity must be greater than zero" });

        var tenantId = RequireTenantId(tenantContext);
        var userId = RequireUserId(tenantContext);
        var itemIds = request.Lines.Select(x => x.SmetaItemId).Distinct().ToArray();
        var items = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId && x.SiteId == request.SiteId && itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != itemIds.Length) return Results.BadRequest(new { error = "Smeta item does not belong to the assigned site" });

        var report = await db.SupervisorDailyReports
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == request.SiteId && x.SupervisorUserId == userId && x.ReportDate == request.ReportDate, ct);
        if (report is not null)
        {
            return DailyReportDuplicateConflict(report);
        }

        report = new SupervisorDailyReport
        {
            TenantId = tenantId,
            SiteId = request.SiteId,
            SupervisorUserId = userId,
            ReportDate = request.ReportDate,
            Status = FieldDailyReportStatus.Draft,
        };
        db.SupervisorDailyReports.Add(report);

        report.Shift = Clean(request.Shift);
        report.GeneralNote = Clean(request.GeneralNote);
        report.WeatherCondition = Clean(request.WeatherCondition);
        foreach (var line in request.Lines)
        {
            report.Lines.Add(new SupervisorDailyReportLine
            {
                TenantId = tenantId,
                SmetaItemId = line.SmetaItemId,
                ReportedQuantity = line.ReportedQuantity,
                WorkerCount = line.WorkerCount,
                WorkHours = line.WorkHours,
                Unit = items[line.SmetaItemId].Unit,
                Note = Clean(line.Note),
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueDailyReportConflict(ex))
        {
            return Results.Conflict(new { error = "A daily report already exists for this site and date." });
        }

        await WriteAuditAsync(db, tenantId, request.SiteId, userId, "DailyReportCreated", "SupervisorDailyReport", report.Id, false, $"{report.ReportDate:yyyy-MM-dd} tarixli gündəlik hesabat yaradıldı.", ct);
        return Results.Ok(await LoadReportDtoAsync(db, report.Id, ct));
    }

    private static async Task<IResult> UpdateFieldDailyReportAsync(
        Guid id,
        SaveFieldDailyReportRequest request,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IFieldAccessService fieldAccess,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var userId = RequireUserId(tenantContext);
        var report = await db.SupervisorDailyReports
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (report is null) return Results.NotFound();
        await fieldAccess.RequireSiteAccessAsync(report.SiteId, ct);
        if (report.SupervisorUserId != userId) return Results.Forbid();
        if (!CanSupervisorEditDailyReport(report.Status)) return Results.BadRequest(new { error = "Only draft or correction reports can be edited" });
        if (request.SiteId != report.SiteId) return Results.BadRequest(new { error = "Report site cannot be changed" });
        if (request.ReportDate != report.ReportDate) return Results.Conflict(new { error = "A daily report already exists for this site and date." });
        if (request.Lines.Count == 0) return Results.BadRequest(new { error = "At least one report line is required" });
        if (request.Lines.Any(x => x.ReportedQuantity <= 0)) return Results.BadRequest(new { error = "Reported quantity must be greater than zero" });

        var itemIds = request.Lines.Select(x => x.SmetaItemId).Distinct().ToArray();
        var items = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId && x.SiteId == report.SiteId && itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != itemIds.Length) return Results.BadRequest(new { error = "Smeta item does not belong to the assigned site" });

        report.Shift = Clean(request.Shift);
        report.GeneralNote = Clean(request.GeneralNote);
        report.WeatherCondition = Clean(request.WeatherCondition);

        var lineSync = SyncDailyReportLines(report, request.Lines, items, tenantId);
        if (lineSync is not null) return lineSync;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Report was changed by another operation. Refresh and try again." });
        }
        catch (DbUpdateException ex) when (IsUniqueDailyReportConflict(ex))
        {
            return Results.Conflict(new { error = "A daily report already exists for this site and date." });
        }

        var auditAction = report.Status == FieldDailyReportStatus.NeedsCorrection ? "DailyReportCorrectionUpdated" : "DailyReportUpdated";
        var auditDescription = report.Status == FieldDailyReportStatus.NeedsCorrection
            ? $"{report.ReportDate:yyyy-MM-dd} tarixli gündəlik hesabatda tələb olunan düzəlişlər edildi."
            : $"{report.ReportDate:yyyy-MM-dd} tarixli gündəlik hesabat yeniləndi.";
        await WriteAuditAsync(db, tenantId, report.SiteId, userId, auditAction, "SupervisorDailyReport", report.Id, false, auditDescription, ct);
        return Results.Ok(await LoadReportDtoAsync(db, report.Id, ct));
    }

    internal static IResult? SyncDailyReportLines(
        SupervisorDailyReport report,
        IReadOnlyList<SaveFieldDailyReportLineRequest> requestLines,
        IReadOnlyDictionary<Guid, FieldSmetaItem> items,
        Guid tenantId)
    {
        var existingById = report.Lines.ToDictionary(x => x.Id);
        var matchedExistingIds = new HashSet<Guid>();
        var usedFallbackSmetaItemIds = new HashSet<Guid>();

        foreach (var requestLine in requestLines)
        {
            SupervisorDailyReportLine? line = null;
            if (requestLine.Id is Guid lineId)
            {
                if (!existingById.TryGetValue(lineId, out line))
                {
                    return Results.BadRequest(new { error = "Report line does not belong to this daily report" });
                }
            }
            else
            {
                line = report.Lines.FirstOrDefault(x =>
                    !matchedExistingIds.Contains(x.Id)
                    && !usedFallbackSmetaItemIds.Contains(x.SmetaItemId)
                    && x.SmetaItemId == requestLine.SmetaItemId);
                if (line is not null)
                {
                    usedFallbackSmetaItemIds.Add(requestLine.SmetaItemId);
                }
            }

            if (line is null)
            {
                line = new SupervisorDailyReportLine
                {
                    TenantId = tenantId,
                };
                report.Lines.Add(line);
            }

            matchedExistingIds.Add(line.Id);
            line.SmetaItemId = requestLine.SmetaItemId;
            line.ReportedQuantity = requestLine.ReportedQuantity;
            line.WorkerCount = requestLine.WorkerCount;
            line.WorkHours = requestLine.WorkHours;
            line.Unit = items[requestLine.SmetaItemId].Unit;
            line.Note = Clean(requestLine.Note);
        }

        foreach (var existing in report.Lines.Where(x => !matchedExistingIds.Contains(x.Id)).ToArray())
        {
            report.Lines.Remove(existing);
        }

        return null;
    }

    private static bool IsUniqueDailyReportConflict(DbUpdateException ex)
    {
        var current = ex.InnerException;
        while (current is not null)
        {
            if (current.GetType().FullName == "Npgsql.PostgresException"
                && string.Equals(current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString(), "23505", StringComparison.Ordinal)
                && string.Equals(current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString(), "UX_supervisor_daily_reports_daily", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    internal static bool CanSupervisorEditDailyReport(FieldDailyReportStatus status) =>
        status is FieldDailyReportStatus.Draft or FieldDailyReportStatus.NeedsCorrection;

    internal static bool CanSubmitDailyReport(FieldDailyReportStatus status) =>
        status is FieldDailyReportStatus.Draft or FieldDailyReportStatus.NeedsCorrection;

    internal static IResult DailyReportDuplicateConflict(SupervisorDailyReport report) =>
        Results.Conflict(new
        {
            error = "A daily report already exists for this site and date.",
            existingReportId = report.Id,
            existingStatus = report.Status.ToString(),
        });

    private static async Task<IResult> SubmitFieldDailyReportAsync(Guid id, BuildTrackDbContext db, ITenantContext tenantContext, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var report = await db.SupervisorDailyReports.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return Results.NotFound();
        await fieldAccess.RequireSiteAccessAsync(report.SiteId, ct);
        if (report.SupervisorUserId != RequireUserId(tenantContext) && !IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (!CanSubmitDailyReport(report.Status)) return Results.BadRequest(new { error = "Only draft or correction reports can be submitted" });
        if (report.Lines.Count == 0) return Results.BadRequest(new { error = "Report lines are required before submit" });
        var wasCorrection = report.Status == FieldDailyReportStatus.NeedsCorrection;
        report.Status = FieldDailyReportStatus.Submitted;
        report.SubmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, report.TenantId, report.SiteId, report.SupervisorUserId, wasCorrection ? "DailyReportResubmitted" : "DailyReportSubmitted", "SupervisorDailyReport", report.Id, false, wasCorrection ? $"{report.ReportDate:yyyy-MM-dd} tarixli gündəlik hesabat yenidən təsdiq üçün göndərildi." : $"{report.ReportDate:yyyy-MM-dd} tarixli gündəlik hesabat təsdiq üçün göndərildi.", ct);
        return Results.Ok(await LoadReportDtoAsync(db, id, ct));
    }

    private static async Task<IResult> GetFieldSiteNotesAsync(Guid? siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var allowed = await ResolveAllowedSitesAsync(fieldAccess, siteId, ct);
        var notes = await db.SupervisorSiteNotes.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Where(x => allowed.Contains(x.SiteId))
            .OrderByDescending(x => x.EventDateTime)
            .Take(100)
            .Select(x => new FieldSiteNoteDto(x.Id, x.SiteId, x.Site!.Name, x.SupervisorUserId, x.SupervisorUser!.FullName, x.EventDateTime, x.Category, x.Text, x.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateFieldSiteNoteAsync(CreateFieldSiteNoteRequest request, BuildTrackDbContext db, ITenantContext tenantContext, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(request.SiteId, ct);
        if (string.IsNullOrWhiteSpace(request.Text)) return Results.BadRequest(new { error = "Note text is required" });
        var note = new SupervisorSiteNote
        {
            TenantId = RequireTenantId(tenantContext),
            SiteId = request.SiteId,
            SupervisorUserId = RequireUserId(tenantContext),
            EventDateTime = request.EventDateTime ?? DateTimeOffset.UtcNow,
            Category = request.Category,
            Text = request.Text.Trim(),
        };
        db.SupervisorSiteNotes.Add(note);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, note.TenantId, note.SiteId, note.SupervisorUserId, "SiteNoteCreated", "SupervisorSiteNote", note.Id, false, $"Sahə qeydi yaradıldı: {note.Category}", ct);
        var siteName = await db.Sites.AsNoTracking().Where(x => x.Id == note.SiteId).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        var supervisorName = await db.Users.AsNoTracking().Where(x => x.Id == note.SupervisorUserId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return Results.Ok(new FieldSiteNoteDto(note.Id, note.SiteId, siteName, note.SupervisorUserId, supervisorName, note.EventDateTime, note.Category, note.Text, note.CreatedAt));
    }

    private static async Task<IResult> GetFieldWorkerEventsAsync(Guid? siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var allowed = await ResolveAllowedSitesAsync(fieldAccess, siteId, ct);
        var rows = await db.SupervisorWorkerEvents.AsNoTracking()
            .Include(x => x.Worker)
            .Include(x => x.SupervisorUser)
            .Where(x => allowed.Contains(x.SiteId))
            .OrderByDescending(x => x.EventDateTime)
            .Take(100)
            .Select(x => new FieldWorkerEventDto(x.Id, x.SiteId, x.WorkerId, x.Worker!.FullName, x.SupervisorUserId, x.SupervisorUser!.FullName, x.EventType, x.EventDateTime, x.Reason, x.RiskDelta, x.Status, x.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateFieldWorkerEventAsync(CreateFieldWorkerEventRequest request, BuildTrackDbContext db, ITenantContext tenantContext, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(request.SiteId, ct);
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == request.WorkerId && (x.SiteId == request.SiteId || x.SiteAssignments.Any(a => a.SiteId == request.SiteId && a.Status == WorkerSiteAssignmentStatus.Active)), ct);
        if (worker is null) return Results.BadRequest(new { error = "Worker does not belong to this site" });
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { error = "Reason is required" });

        var riskDelta = FieldRiskPolicy.CalculateRiskDelta(request.EventType);
        var fieldEvent = new SupervisorWorkerEvent
        {
            TenantId = RequireTenantId(tenantContext),
            SiteId = request.SiteId,
            WorkerId = request.WorkerId,
            SupervisorUserId = RequireUserId(tenantContext),
            EventType = request.EventType,
            EventDateTime = request.EventDateTime ?? DateTimeOffset.UtcNow,
            Reason = request.Reason.Trim(),
            RiskDelta = riskDelta,
        };
        worker.RiskScore = Math.Clamp(worker.RiskScore + riskDelta, 0, 100);
        db.SupervisorWorkerEvents.Add(fieldEvent);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, fieldEvent.TenantId, fieldEvent.SiteId, fieldEvent.SupervisorUserId, $"Worker{fieldEvent.EventType}Reported", "SupervisorWorkerEvent", fieldEvent.Id, riskDelta >= 3, $"{worker.FullName}: {fieldEvent.EventType} qeydi", ct);
        return Results.Ok(new FieldWorkerEventDto(fieldEvent.Id, fieldEvent.SiteId, worker.Id, worker.FullName, fieldEvent.SupervisorUserId, null, fieldEvent.EventType, fieldEvent.EventDateTime, fieldEvent.Reason, fieldEvent.RiskDelta, fieldEvent.Status, fieldEvent.CreatedAt));
    }

    private static async Task<IResult> GetFieldWarehouseCatalogAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        await EnsureDefaultCatalogAsync(db, RequireTenantId(tenantContext), ct);
        var tenantId = RequireTenantId(tenantContext);
        var rows = await db.FieldWarehouseCatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new FieldWarehouseCatalogItemDto(x.Id, x.Name, x.Category, x.Unit, x.Code))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetFieldWarehouseRequestsAsync(Guid? siteId, BuildTrackDbContext db, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        var allowed = await ResolveAllowedSitesAsync(fieldAccess, siteId, ct);
        return Results.Ok(await LoadWarehouseRequestsAsync(db, allowed, ct));
    }

    private static async Task<IResult> CreateFieldWarehouseRequestAsync(CreateFieldWarehouseRequest request, BuildTrackDbContext db, ITenantContext tenantContext, IFieldAccessService fieldAccess, CancellationToken ct)
    {
        await fieldAccess.RequireSiteAccessAsync(request.SiteId, ct);
        if (request.RequestedQuantity <= 0) return Results.BadRequest(new { error = "Requested quantity must be greater than zero" });
        var catalog = await db.FieldWarehouseCatalogItems.FirstOrDefaultAsync(x => x.Id == request.CatalogItemId && x.IsActive, ct);
        if (catalog is null) return Results.BadRequest(new { error = "Catalog item was not found" });
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { error = "Reason is required" });

        var unusuallyHigh = request.RequestedQuantity > 30 && string.IsNullOrWhiteSpace(request.Justification);
        var warehouseRequest = new FieldWarehouseRequest
        {
            TenantId = RequireTenantId(tenantContext),
            SiteId = request.SiteId,
            SupervisorUserId = RequireUserId(tenantContext),
            CatalogItemId = catalog.Id,
            Code = $"FR-{DateTime.UtcNow:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
            RequestedQuantity = request.RequestedQuantity,
            Unit = catalog.Unit,
            NeededBy = request.NeededBy,
            Urgency = request.Urgency,
            Reason = request.Reason.Trim(),
            Justification = Clean(request.Justification),
            Status = unusuallyHigh ? FieldWarehouseRequestStatus.NeedsJustification : FieldWarehouseRequestStatus.PendingApproval,
        };
        warehouseRequest.Lines.Add(new FieldWarehouseRequestLine
        {
            TenantId = warehouseRequest.TenantId,
            CatalogItemId = catalog.Id,
            RequestedQuantity = request.RequestedQuantity,
            Unit = catalog.Unit,
            Reason = request.Reason.Trim(),
            Status = FieldWarehouseRequestLineStatus.Pending,
        });
        db.FieldWarehouseRequests.Add(warehouseRequest);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, warehouseRequest.TenantId, warehouseRequest.SiteId, warehouseRequest.SupervisorUserId, "WarehouseRequestCreated", "FieldWarehouseRequest", warehouseRequest.Id, unusuallyHigh, $"{catalog.Name}: {warehouseRequest.RequestedQuantity} {catalog.Unit} sorğusu", ct);
        return Results.Ok((await LoadWarehouseRequestsAsync(db, [warehouseRequest.SiteId], ct)).First(x => x.Id == warehouseRequest.Id));
    }

    private static async Task<IResult> GetSupervisorsAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var supervisors = await db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Role == BuildTrackUserRole.Supervisor)
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);
        var supervisorIds = supervisors.Select(x => x.Id).ToArray();
        var assignments = await db.SupervisorSiteAssignments.AsNoTracking().Include(x => x.Site).Where(x => supervisorIds.Contains(x.SupervisorUserId) && x.IsActive).ToListAsync(ct);
        var pendingReports = await db.SupervisorDailyReports.AsNoTracking().Where(x => supervisorIds.Contains(x.SupervisorUserId) && x.Status == FieldDailyReportStatus.Submitted).GroupBy(x => x.SupervisorUserId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var openRequests = await db.FieldWarehouseRequests.AsNoTracking().Where(x => supervisorIds.Contains(x.SupervisorUserId) && x.Status != FieldWarehouseRequestStatus.Closed && x.Status != FieldWarehouseRequestStatus.Issued && x.Status != FieldWarehouseRequestStatus.Cancelled).GroupBy(x => x.SupervisorUserId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var recentEvents = await db.SupervisorAuditEvents.AsNoTracking().Where(x => x.SupervisorUserId != null && supervisorIds.Contains(x.SupervisorUserId.Value) && x.Timestamp >= DateTimeOffset.UtcNow.AddDays(-7)).GroupBy(x => x.SupervisorUserId!.Value).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return Results.Ok(supervisors.Select(user => new SupervisorSummaryDto(user.Id, user.FullName, user.Email, user.Phone, user.Status, user.LastLoginAt, MapAssignments(assignments.Where(x => x.SupervisorUserId == user.Id).ToList()), pendingReports.GetValueOrDefault(user.Id), openRequests.GetValueOrDefault(user.Id), recentEvents.GetValueOrDefault(user.Id))));
    }

    private static async Task<IResult> CreateSupervisorAsync(CreateSupervisorRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.TemporaryPassword)) return Results.BadRequest(new { error = "Full name, email and temporary password are required" });
        if (request.TemporaryPassword.Length < 8) return Results.BadRequest(new { error = "Temporary password must be at least 8 characters" });
        var tenantId = RequireTenantId(tenantContext);
        var activeLicense = await db.Licenses.AsNoTracking().Where(x => x.TenantId == tenantId && x.Status == LicenseStatus.Active && (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow)).OrderByDescending(x => x.ActivatedAt ?? x.CreatedAt).FirstOrDefaultAsync(ct);
        if (activeLicense?.MaxUsers is int maxUsers)
        {
            var usedUsers = await db.Users.CountAsync(x => x.TenantId == tenantId && x.Status == BuildTrackUserStatus.Active, ct);
            if (usedUsers >= maxUsers) return Results.BadRequest(new { error = "License user seat limit has been reached" });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { error = "Email is already registered" });
        var validSiteIds = await ValidateTenantSitesAsync(db, tenantId, request.SiteIds, ct);
        var user = new AppUser
        {
            TenantId = tenantId,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            PasswordHash = BuildTrackPasswordHasher.HashPassword(request.TemporaryPassword),
            Role = BuildTrackUserRole.Supervisor,
            Status = BuildTrackUserStatus.Active,
        };
        db.Users.Add(user);
        AddAssignments(db, tenantId, user.Id, validSiteIds, tenantContext.UserId, request.Notes, request.ValidFrom, request.ValidUntil);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, tenantId, validSiteIds.FirstOrDefault(), user.Id, "AssignmentChanged", "AppUser", user.Id, false, $"Supervisor yaradıldı: {user.FullName}", ct);
        return Results.Created($"/api/supervisors/{user.Id}", new { user.Id });
    }

    private static async Task<IResult> UpdateSupervisorAsync(Guid id, UpdateSupervisorRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var user = await db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && x.Role == BuildTrackUserRole.Supervisor, ct);
        if (user is null) return Results.NotFound();
        var validSiteIds = await ValidateTenantSitesAsync(db, tenantId, request.SiteIds, ct);
        user.FullName = request.FullName.Trim();
        user.Phone = Clean(request.Phone);
        user.Status = request.Status;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var existing = await db.SupervisorSiteAssignments.Where(x => x.TenantId == tenantId && x.SupervisorUserId == id).ToListAsync(ct);
        foreach (var assignment in existing) assignment.IsActive = validSiteIds.Contains(assignment.SiteId);
        var existingSiteIds = existing.Select(x => x.SiteId).ToHashSet();
        AddAssignments(db, tenantId, id, validSiteIds.Where(site => !existingSiteIds.Contains(site)).ToArray(), tenantContext.UserId, request.Notes, request.ValidFrom, request.ValidUntil);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, tenantId, validSiteIds.FirstOrDefault(), id, "AssignmentChanged", "AppUser", id, false, $"Supervisor assignment yeniləndi: {user.FullName}", ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetSupervisorPasswordAsync(Guid id, ResetSupervisorPasswordRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8) return Results.BadRequest(new { error = "Temporary password must be at least 8 characters" });
        var tenantId = RequireTenantId(tenantContext);
        var user = await db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && x.Role == BuildTrackUserRole.Supervisor, ct);
        if (user is null) return Results.NotFound();
        user.PasswordHash = BuildTrackPasswordHasher.HashPassword(request.TemporaryPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, tenantId, null, id, "SupervisorPasswordReset", "AppUser", id, false, $"Supervisor şifrəsi yeniləndi: {user.FullName}", ct);
        return Results.NoContent();
    }

    private static Task<IResult> SetSupervisorSuspendedAsync(Guid id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
        SetSupervisorStatusAsync(id, BuildTrackUserStatus.Disabled, db, tenantContext, ct);

    private static Task<IResult> SetSupervisorActiveAsync(Guid id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
        SetSupervisorStatusAsync(id, BuildTrackUserStatus.Active, db, tenantContext, ct);

    private static async Task<IResult> SetSupervisorStatusAsync(Guid id, BuildTrackUserStatus status, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var user = await db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && x.Role == BuildTrackUserRole.Supervisor, ct);
        if (user is null) return Results.NotFound();
        user.Status = status;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetManagementFieldReportsAsync(Guid? siteId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var query = db.SupervisorDailyReports.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines).ThenInclude(x => x.SmetaItem)
            .AsQueryable();
        if (siteId is not null) query = query.Where(x => x.SiteId == siteId.Value);
        var reports = await query.OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt).Take(150).ToListAsync(ct);
        var reviewerIds = reports.Select(x => x.ReviewedByUserId).OfType<Guid>().Distinct().ToArray();
        var reviewerNames = reviewerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(x => reviewerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return Results.Ok(reports.Select(report => MapReport(report, report.ReviewedByUserId is Guid reviewerId ? reviewerNames.GetValueOrDefault(reviewerId) : null)));
    }

    private static async Task<IResult> ReviewManagementFieldReportAsync(Guid id, ReviewFieldDailyReportRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        if (request.Status is not (FieldDailyReportStatus.Approved or FieldDailyReportStatus.NeedsCorrection or FieldDailyReportStatus.Rejected)) return Results.BadRequest(new { error = "Invalid review status" });
        if (request.Status is FieldDailyReportStatus.NeedsCorrection or FieldDailyReportStatus.Rejected && string.IsNullOrWhiteSpace(request.ReviewNote))
        {
            return Results.BadRequest(new { error = "Review note is required" });
        }

        var tenantId = RequireTenantId(tenantContext);
        var report = await db.SupervisorDailyReports.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (report is null) return Results.NotFound();
        report.Status = request.Status;
        report.ReviewedAt = DateTimeOffset.UtcNow;
        report.ReviewedByUserId = tenantContext.UserId;
        report.ReviewNote = Clean(request.ReviewNote);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, report.TenantId, report.SiteId, report.SupervisorUserId, $"DailyReport{request.Status}", "SupervisorDailyReport", report.Id, request.Status == FieldDailyReportStatus.Rejected, BuildDailyReportReviewDescription(report.ReportDate, request.Status), ct);
        return Results.Ok(await LoadReportDtoAsync(db, id, ct));
    }

    private static async Task<IResult> GetManagementWarehouseRequestsAsync(Guid? siteId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var siteIds = siteId is null
            ? await db.Sites.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.Id).ToArrayAsync(ct)
            : [siteId.Value];
        return Results.Ok(await LoadWarehouseRequestsAsync(db, siteIds, ct));
    }

    private static async Task<IResult> ReviewManagementWarehouseRequestAsync(Guid id, ReviewFieldWarehouseRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var tenantId = RequireTenantId(tenantContext);
        var row = await db.FieldWarehouseRequests
            .Include(x => x.CatalogItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (row is null) return Results.NotFound();
        if (!IsValidWarehouseReviewTransition(row.Status, request.Status))
        {
            return Results.BadRequest(new { error = $"Invalid warehouse request transition: {row.Status} -> {request.Status}" });
        }

        row.Status = request.Status;
        row.ManagerComment = Clean(request.ManagerComment);
        row.ReviewedAt = DateTimeOffset.UtcNow;
        row.ReviewedByUserId = tenantContext.UserId;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.Status == FieldWarehouseRequestStatus.Rejected)
        {
            foreach (var line in row.Lines)
            {
                line.Status = FieldWarehouseRequestLineStatus.Rejected;
                line.UpdatedAt = row.UpdatedAt;
            }
        }

        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(db, row.TenantId, row.SiteId, row.SupervisorUserId, $"WarehouseRequest{request.Status}", "FieldWarehouseRequest", row.Id, row.Status == FieldWarehouseRequestStatus.NeedsJustification, $"Anbar sorğusu yeniləndi: {BuildWarehouseAuditMaterialSummary(row)}", ct);
        return Results.Ok((await LoadWarehouseRequestsAsync(db, [row.SiteId], ct)).First(x => x.Id == row.Id));
    }

    private static async Task<IResult> GetSupervisorAuditEventsAsync(Guid? siteId, Guid? supervisorUserId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        if (!IsManagementRole(tenantContext.Role)) return Results.Forbid();
        var query = db.SupervisorAuditEvents.AsNoTracking();
        if (siteId is not null) query = query.Where(x => x.SiteId == siteId.Value);
        if (supervisorUserId is not null) query = query.Where(x => x.SupervisorUserId == supervisorUserId.Value);
        var siteNames = await db.Sites.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var rows = await query.OrderByDescending(x => x.Timestamp).Take(200).ToListAsync(ct);
        return Results.Ok(rows.Select(x => new SupervisorAuditEventDto(x.Id, x.SiteId, x.SiteId is not null ? siteNames.GetValueOrDefault(x.SiteId.Value) : null, x.SupervisorUserId, x.SupervisorNameSnapshot, x.Action, x.EntityType, x.EntityId, x.Timestamp, x.RiskFlag, x.Description)));
    }

    private static async Task<Site?> ResolveFieldSiteAsync(BuildTrackDbContext db, IFieldAccessService fieldAccess, Guid? siteId, CancellationToken ct)
    {
        var assignments = await fieldAccess.GetActiveAssignmentsAsync(ct);
        var selectedSiteId = siteId ?? assignments.FirstOrDefault()?.SiteId;
        if (selectedSiteId is null) return null;
        if (!assignments.Any(x => x.SiteId == selectedSiteId.Value)) return null;
        return await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == selectedSiteId.Value, ct);
    }

    private static async Task<Guid[]> ResolveAllowedSitesAsync(IFieldAccessService fieldAccess, Guid? siteId, CancellationToken ct)
    {
        if (siteId is not null)
        {
            await fieldAccess.RequireSiteAccessAsync(siteId.Value, ct);
            return [siteId.Value];
        }

        return (await fieldAccess.GetActiveAssignmentsAsync(ct)).Select(x => x.SiteId).Distinct().ToArray();
    }

    private static async Task EnsureDefaultSmetaItemsAsync(BuildTrackDbContext db, Guid siteId, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstAsync(x => x.Id == siteId, ct);
        if (await db.FieldSmetaItems.AnyAsync(x => x.SiteId == siteId, ct)) return;
        db.FieldSmetaItems.AddRange(
            NewSmeta(site.TenantId, siteId, "Torpaq işləri", "Torpaq qazıntısı", "m3", "Kaba işlər"),
            NewSmeta(site.TenantId, siteId, "Bünövrə / Zirzəmi", "Armatur quraşdırılması", "ton", "Monolit"),
            NewSmeta(site.TenantId, siteId, "Bünövrə / Zirzəmi", "Beton tökülməsi", "m3", "Monolit"),
            NewSmeta(site.TenantId, siteId, "Hörgü işləri", "Kubik hörgü", "m2", "Hörgü"),
            NewSmeta(site.TenantId, siteId, "Suvaq işləri", "Daxili suvaq", "m2", "Suvaq"));
        await db.SaveChangesAsync(ct);
    }

    private static FieldSmetaItem NewSmeta(Guid tenantId, Guid siteId, string stage, string work, string unit, string category) => new()
    {
        TenantId = tenantId,
        SiteId = siteId,
        StageName = stage,
        WorkName = work,
        Unit = unit,
        WorkCategory = category,
    };

    private static async Task EnsureDefaultCatalogAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        if (await db.FieldWarehouseCatalogItems.AnyAsync(x => x.TenantId == tenantId, ct)) return;
        db.FieldWarehouseCatalogItems.AddRange(
            NewCatalog(tenantId, "Kaska", "PPE", "ədəd", "PPE-HELMET"),
            NewCatalog(tenantId, "İş əlcəyi", "PPE", "cüt", "PPE-GLOVE"),
            NewCatalog(tenantId, "Reflektor jilet", "PPE", "ədəd", "PPE-VEST"),
            NewCatalog(tenantId, "Sverlo 12mm", "Alət", "ədəd", "TOOL-DRILL-12"),
            NewCatalog(tenantId, "Sement M400", "Material", "kisə", "MAT-CEMENT-M400"));
        await db.SaveChangesAsync(ct);
    }

    private static FieldWarehouseCatalogItem NewCatalog(Guid tenantId, string name, string category, string unit, string code) => new()
    {
        TenantId = tenantId,
        Name = name,
        Category = category,
        Unit = unit,
        Code = code,
    };

    private static async Task<FieldDailyReportDto> LoadReportDtoAsync(BuildTrackDbContext db, Guid reportId, CancellationToken ct)
    {
        var report = await db.SupervisorDailyReports.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.Lines)
            .ThenInclude(x => x.SmetaItem)
            .FirstAsync(x => x.Id == reportId, ct);
        var reviewedByName = report.ReviewedByUserId is null
            ? null
            : await db.Users.AsNoTracking().Where(x => x.Id == report.ReviewedByUserId.Value).Select(x => x.FullName).FirstOrDefaultAsync(ct);
        return MapReport(report, reviewedByName);
    }

    private static FieldDailyReportDto MapReport(SupervisorDailyReport report) => MapReport(report, null);

    private static FieldDailyReportDto MapReport(SupervisorDailyReport report, string? reviewedByName) => new(
        report.Id,
        report.SiteId,
        report.Site?.Name,
        report.SupervisorUserId,
        report.SupervisorUser?.FullName,
        report.ReportDate,
        report.Shift,
        report.Status,
        report.GeneralNote,
        report.WeatherCondition,
        report.CreatedAt,
        report.SubmittedAt,
        report.ReviewedAt,
        report.ReviewedByUserId,
        reviewedByName,
        report.ReviewNote,
        report.Lines
            .OrderBy(x => x.SmetaItem?.StageName)
            .ThenBy(x => x.SmetaItem?.WorkName)
            .Select(x => new FieldDailyReportLineDto(x.Id, x.SmetaItemId, x.SmetaItem?.StageName ?? string.Empty, x.SmetaItem?.WorkName ?? string.Empty, x.ReportedQuantity, x.WorkerCount, x.WorkHours, x.Unit, x.Note))
            .ToArray());

    private static async Task<IReadOnlyList<FieldWarehouseRequestDto>> LoadWarehouseRequestsAsync(BuildTrackDbContext db, IReadOnlyCollection<Guid> siteIds, CancellationToken ct)
    {
        return await db.FieldWarehouseRequests.AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.SupervisorUser)
            .Include(x => x.CatalogItem)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CatalogItem)
            .Where(x => siteIds.Contains(x.SiteId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(150)
            .Select(x => new FieldWarehouseRequestDto(
                x.Id,
                x.SiteId,
                x.Site!.Name,
                x.CatalogItemId,
                x.CatalogItem!.Name,
                x.CatalogItem.Category,
                x.RequestedQuantity,
                x.Unit,
                x.NeededBy,
                x.Urgency,
                x.Reason,
                x.Justification,
                x.ManagerComment,
                x.Status,
                x.SupervisorUserId,
                x.SupervisorUser!.FullName,
                x.CreatedAt,
                x.UpdatedAt,
                x.Code,
                x.GeneralNote,
                x.AbnormalRequest,
                x.Lines.Select(line => new FieldWarehouseRequestLineDto(
                    line.Id,
                    line.CatalogItemId,
                    line.CatalogItem!.Name,
                    line.CatalogItem.Category,
                    line.RequestedQuantity,
                    line.Unit,
                    line.Reason,
                    line.Status)).ToArray()))
            .ToListAsync(ct);
    }

    private static async Task<Guid[]> ValidateTenantSitesAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Guid> siteIds, CancellationToken ct)
    {
        var distinct = siteIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (distinct.Length == 0) throw new InvalidOperationException("At least one site assignment is required");
        var valid = await db.Sites.AsNoTracking().Where(x => x.TenantId == tenantId && distinct.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        if (valid.Length != distinct.Length) throw new UnauthorizedAccessException("One or more sites do not belong to this tenant");
        return valid;
    }

    private static void AddAssignments(BuildTrackDbContext db, Guid tenantId, Guid supervisorUserId, IReadOnlyCollection<Guid> siteIds, Guid? createdByUserId, string? notes, DateTimeOffset? validFrom, DateTimeOffset? validUntil)
    {
        foreach (var siteId in siteIds)
        {
            db.SupervisorSiteAssignments.Add(new SupervisorSiteAssignment
            {
                TenantId = tenantId,
                SupervisorUserId = supervisorUserId,
                SiteId = siteId,
                IsActive = true,
                CreatedByUserId = createdByUserId,
                Notes = Clean(notes),
                ValidFrom = validFrom,
                ValidUntil = validUntil,
            });
        }
    }

    private static async Task WriteAuditAsync(BuildTrackDbContext db, Guid tenantId, Guid? siteId, Guid? supervisorUserId, string action, string entityType, Guid? entityId, bool riskFlag, string description, CancellationToken ct)
    {
        string? supervisorName = null;
        if (supervisorUserId is not null)
        {
            supervisorName = await db.Users.AsNoTracking().Where(x => x.Id == supervisorUserId.Value).Select(x => x.FullName).FirstOrDefaultAsync(ct);
        }

        db.SupervisorAuditEvents.Add(new SupervisorAuditEvent
        {
            TenantId = tenantId,
            SiteId = siteId,
            SupervisorUserId = supervisorUserId,
            SupervisorNameSnapshot = supervisorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            RiskFlag = riskFlag,
            Description = description,
            MetadataJson = JsonSerializer.Serialize(new { source = "FieldPortal" }),
        });
        await db.SaveChangesAsync(ct);
    }

    internal static string BuildDailyReportReviewDescription(DateOnly reportDate, FieldDailyReportStatus status) => status switch
    {
        FieldDailyReportStatus.Approved => $"{reportDate:yyyy-MM-dd} tarixli gündəlik hesabat təsdiqləndi.",
        FieldDailyReportStatus.NeedsCorrection => $"{reportDate:yyyy-MM-dd} tarixli gündəlik hesabat üçün düzəliş tələb olundu.",
        FieldDailyReportStatus.Rejected => $"{reportDate:yyyy-MM-dd} tarixli gündəlik hesabat rədd edildi.",
        _ => $"{reportDate:yyyy-MM-dd} tarixli gündəlik hesabat review edildi.",
    };

    internal static bool IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus current, FieldWarehouseRequestStatus next)
    {
        if (current is FieldWarehouseRequestStatus.Rejected or FieldWarehouseRequestStatus.Issued or FieldWarehouseRequestStatus.Closed or FieldWarehouseRequestStatus.Cancelled)
        {
            return false;
        }

        return next switch
        {
            FieldWarehouseRequestStatus.NeedsJustification => current is FieldWarehouseRequestStatus.Draft
                or FieldWarehouseRequestStatus.Submitted
                or FieldWarehouseRequestStatus.UnderReview
                or FieldWarehouseRequestStatus.PendingApproval,
            FieldWarehouseRequestStatus.Rejected => true,
            FieldWarehouseRequestStatus.Approved => current is FieldWarehouseRequestStatus.Draft
                or FieldWarehouseRequestStatus.Submitted
                or FieldWarehouseRequestStatus.UnderReview
                or FieldWarehouseRequestStatus.NeedsJustification
                or FieldWarehouseRequestStatus.PendingApproval,
            FieldWarehouseRequestStatus.Issued => current is FieldWarehouseRequestStatus.Approved
                or FieldWarehouseRequestStatus.PartiallyApproved
                or FieldWarehouseRequestStatus.ReadyForPickup,
            _ => false,
        };
    }

    private static string BuildWarehouseAuditMaterialSummary(FieldWarehouseRequest request)
    {
        var names = request.Lines
            .Select(x => x.CatalogItem?.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        if (names.Length == 0) return request.CatalogItem?.Name ?? "Məlumat mövcud deyil";
        return names.Length == 1 ? names[0]! : $"{names[0]} +{names.Length - 1}";
    }

    private static IReadOnlyList<FieldAssignmentDto> MapAssignments(IReadOnlyCollection<SupervisorSiteAssignment> assignments) =>
        assignments
            .Where(x => x.Site is not null)
            .Select(x => new FieldAssignmentDto(x.Id, x.SiteId, x.Site!.Name, x.Site.Address, x.ProjectId, x.IsActive, x.ValidFrom, x.ValidUntil))
            .ToArray();

    private static AuthUserResponse ToAuthUserResponse(AppUser user) =>
        new(user.Id, user.TenantId, user.FullName, user.Email, user.Role, user.Status);

    private static TenantResponse ToTenantResponse(Tenant tenant) =>
        new(tenant.Id, tenant.CompanyName, tenant.Code, tenant.Status);

    private static Guid RequireTenantId(ITenantContext tenantContext) =>
        tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context is missing");

    private static Guid RequireUserId(ITenantContext tenantContext) =>
        tenantContext.UserId ?? throw new UnauthorizedAccessException("User context is missing");

    private static bool IsManagementRole(string? role) =>
        string.Equals(role, BuildTrackUserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase);

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "Asia/Baku" : id);
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
