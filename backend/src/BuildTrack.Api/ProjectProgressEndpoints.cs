using System.Text.Json;
using System.Text.Json.Nodes;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BuildTrack.Api;

public static class ProjectProgressEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication MapProjectProgressEndpoints(this WebApplication app)
    {
        app.MapGet("/api/project-progress/workspace", GetWorkspaceAsync);
        app.MapPut("/api/project-progress/workspace", SaveWorkspaceAsync);
        app.MapPost("/api/project-progress/import-legacy", ImportLegacyWorkspaceAsync);
        app.MapGet("/api/project-progress/summary", async (BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
            Results.Json(await GetWorkspacePropertyAsync(db, tenantContext, "summary", ct)));
        app.MapGet("/api/project-progress/stages", async (BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
            Results.Json(await GetWorkspacePropertyAsync(db, tenantContext, "stages", ct)));
        app.MapGet("/api/project-progress/work-items", async (BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
            Results.Json(await GetWorkspacePropertyAsync(db, tenantContext, "workItems", ct)));
        app.MapGet("/api/project-progress/crews", async (BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
            Results.Json(await GetWorkspacePropertyAsync(db, tenantContext, "crews", ct)));
        app.MapPut("/api/project-progress/stages/{id}", async (string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, IProjectProgressDailyReportSyncService dailyReportSync, ILoggerFactory loggerFactory, CancellationToken ct) =>
            await UpsertWorkspaceArrayItemAsync(id, "stages", body, db, tenantContext, dailyReportSync, loggerFactory, ct));
        app.MapPut("/api/project-progress/work-items/{id}", async (string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, IProjectProgressDailyReportSyncService dailyReportSync, ILoggerFactory loggerFactory, CancellationToken ct) =>
            await UpsertWorkspaceArrayItemAsync(id, "workItems", body, db, tenantContext, dailyReportSync, loggerFactory, ct));
        app.MapPut("/api/project-progress/crews/{id}", async (string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, IProjectProgressDailyReportSyncService dailyReportSync, ILoggerFactory loggerFactory, CancellationToken ct) =>
            await UpsertWorkspaceArrayItemAsync(id, "crews", body, db, tenantContext, dailyReportSync, loggerFactory, ct));
        return app;
    }

    private static async Task<IResult> GetWorkspaceAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var workspace = await GetOrCreateWorkspaceAsync(db, tenantId, ct);
        var normalizedWorkspace = NormalizeWorkspaceJsonStringForTenant(workspace.WorkspaceJson, tenantId);
        if (!string.Equals(normalizedWorkspace, workspace.WorkspaceJson, StringComparison.Ordinal))
        {
            workspace.WorkspaceJson = normalizedWorkspace;
            workspace.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.Content(workspace.WorkspaceJson, "application/json");
    }

    private static async Task<IResult> SaveWorkspaceAsync(
        JsonElement body,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IProjectProgressDailyReportSyncService dailyReportSync,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var validation = ValidateWorkspace(body);
        if (validation is not null) return Results.BadRequest(new { error = validation });
        var tenantValidation = ValidateWorkspaceTenant(body, tenantId, allowLegacyImport: false);
        if (tenantValidation is not null) return Results.BadRequest(new { error = tenantValidation });

        return await SaveWorkspaceWithFieldSmetaSyncAsync(
            db,
            tenantId,
            dailyReportSync,
            loggerFactory,
            async () =>
            {
                var workspace = await GetOrCreateWorkspaceForWriteAsync(db, tenantId, ct);
                workspace.WorkspaceJson = NormalizeWorkspaceJsonForTenant(body, tenantId);
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                return Results.Ok(new { saved = true, workspaceId = workspace.Id, updatedAt = workspace.UpdatedAt });
            },
            ct);
    }

    private static async Task<IResult> ImportLegacyWorkspaceAsync(
        JsonElement body,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IProjectProgressDailyReportSyncService dailyReportSync,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var validation = ValidateWorkspace(body);
        if (validation is not null) return Results.BadRequest(new { error = validation });

        return await SaveWorkspaceWithFieldSmetaSyncAsync(
            db,
            tenantId,
            dailyReportSync,
            loggerFactory,
            async () =>
            {
                var workspace = await GetOrCreateWorkspaceForWriteAsync(db, tenantId, ct);
                workspace.WorkspaceJson = NormalizeWorkspaceJsonForTenant(body, tenantId);
                workspace.LegacyBrowserImportCompleted = true;
                workspace.LegacyBrowserImportedAt = DateTimeOffset.UtcNow;
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                return Results.Ok(new { imported = true, workspaceId = workspace.Id, legacyBrowserImportedAt = workspace.LegacyBrowserImportedAt });
            },
            ct);
    }

    private static async Task<JsonNode> GetWorkspacePropertyAsync(BuildTrackDbContext db, ITenantContext tenantContext, string propertyName, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var workspace = await GetOrCreateWorkspaceAsync(db, tenantId, ct);
        var node = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject();
        return node[propertyName]?.DeepClone() ?? (propertyName == "summary" ? JsonSerializer.SerializeToNode(EmptySummary(), JsonOptions)! : new JsonArray());
    }

    private static async Task<IResult> UpsertWorkspaceArrayItemAsync(
        string id,
        string arrayName,
        JsonElement body,
        BuildTrackDbContext db,
        ITenantContext tenantContext,
        IProjectProgressDailyReportSyncService dailyReportSync,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        return await SaveWorkspaceWithFieldSmetaSyncAsync(
            db,
            tenantId,
            dailyReportSync,
            loggerFactory,
            async () =>
            {
                var workspace = await GetOrCreateWorkspaceForWriteAsync(db, tenantId, ct);
                var root = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject();
                root["workspaceTenantId"] = tenantId.ToString();
                var item = JsonNode.Parse(body.GetRawText())?.AsObject();
                if (item is null) return Results.BadRequest(new { error = "Invalid JSON object" });
                item["id"] = id;

                var array = root[arrayName] as JsonArray;
                if (array is null)
                {
                    array = new JsonArray();
                    root[arrayName] = array;
                }

                var existingIndex = -1;
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i]?["id"]?.GetValue<string>() == id)
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    array[existingIndex] = item;
                }
                else
                {
                    array.Add(item);
                }

                NormalizeWorkspaceRoot(root, tenantId);
                workspace.WorkspaceJson = root.ToJsonString(JsonOptions);
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                return Results.Json(item);
            },
            ct,
            syncFieldSmeta: arrayName is "stages" or "workItems");
    }

    private static async Task<IResult> SaveWorkspaceWithFieldSmetaSyncAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        IProjectProgressDailyReportSyncService dailyReportSync,
        ILoggerFactory loggerFactory,
        Func<Task<IResult>> mutateWorkspace,
        CancellationToken ct,
        bool syncFieldSmeta = true)
    {
        var logger = loggerFactory.CreateLogger("ProjectProgressEndpoints");
        IDbContextTransaction? transaction = null;
        var beforeFieldSmetaFingerprint = syncFieldSmeta
            ? await GetFieldSmetaRelevantWorkspaceFingerprintAsync(db, tenantId, ct)
            : null;
        if (db.Database.IsRelational())
        {
            transaction = await db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var result = await mutateWorkspace();
            if (syncFieldSmeta)
            {
                var afterFieldSmetaFingerprint = await GetFieldSmetaRelevantWorkspaceFingerprintAsync(db, tenantId, ct);
                if (!string.Equals(beforeFieldSmetaFingerprint, afterFieldSmetaFingerprint, StringComparison.Ordinal))
                {
                    await dailyReportSync.SyncFieldSmetaItemsFromWorkspaceAsync(tenantId, ct);
                }
                else
                {
                    logger.LogDebug("Project progress save skipped FieldSmeta sync because objects/stages/workItems fingerprint did not change. TenantId={TenantId}", tenantId);
                }
            }

            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return result;
        }
        catch (ProjectProgressSmetaSyncException ex)
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            logger.LogWarning("Project progress field smeta sync conflict for tenant {TenantId}. Code={Code}; Conflicts={Conflicts}", tenantId, ex.Code, ex.Conflicts.Count);
            var error = ex.Code switch
            {
                "FIELD_SMETA_DUPLICATE_WORK_NAME" => "Eyni obyekt daxilində eyni adlı iki aktiv smeta işi yaradıla bilməz.",
                _ => "Smeta sinxronizasiyası zamanı eyni obyekt və iş adı üzrə konflikt aşkarlandı.",
            };
            return Results.Conflict(new
            {
                error,
                code = ex.Code,
                conflicts = ex.Conflicts,
            });
        }
        catch (DbUpdateException ex) when (IsFieldSmetaUniqueConflict(ex))
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            logger.LogWarning(ex, "Project progress field smeta database unique conflict for tenant {TenantId}", tenantId);
            return Results.Conflict(new
            {
                error = "Smeta sinxronizasiyası zamanı eyni obyekt və iş adı üzrə konflikt aşkarlandı.",
                code = "FIELD_SMETA_IDENTITY_CONFLICT",
            });
        }
        catch (Exception ex)
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            var correlationId = Guid.NewGuid().ToString("N");
            logger.LogError(ex, "Project progress workspace save failed. CorrelationId={CorrelationId}; TenantId={TenantId}", correlationId, tenantId);
            return Results.Json(
                new
                {
                    error = "Layihə məlumatları serverdə saxlanarkən xəta baş verdi.",
                    code = "PROJECT_PROGRESS_SAVE_FAILED",
                    correlationId,
                },
                statusCode: StatusCodes.Status500InternalServerError);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static async Task<string> GetFieldSmetaRelevantWorkspaceFingerprintAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var workspace = db.ProjectProgressWorkspaces.Local.FirstOrDefault(x => x.TenantId == tenantId)
            ?? await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        return BuildFieldSmetaRelevantWorkspaceFingerprint(workspace?.WorkspaceJson);
    }

    private static string BuildFieldSmetaRelevantWorkspaceFingerprint(string? workspaceJson)
    {
        if (string.IsNullOrWhiteSpace(workspaceJson)) return "{}";
        var root = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
        var relevant = new JsonObject
        {
            ["objects"] = root["objects"]?.DeepClone() ?? new JsonArray(),
            ["stages"] = root["stages"]?.DeepClone() ?? new JsonArray(),
            ["workItems"] = root["workItems"]?.DeepClone() ?? new JsonArray(),
        };
        return relevant.ToJsonString(JsonOptions);
    }

    private static async Task<ProjectProgressWorkspace> GetOrCreateWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (workspace is not null) return workspace;

        workspace = new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = await BuildWorkspaceFromCanonicalDataAsync(db, tenantId, ct),
        };
        db.ProjectProgressWorkspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    private static async Task<ProjectProgressWorkspace> GetOrCreateWorkspaceForWriteAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (workspace is not null) return workspace;

        workspace = new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = await BuildWorkspaceFromCanonicalDataAsync(db, tenantId, ct),
        };
        db.ProjectProgressWorkspaces.Add(workspace);
        return workspace;
    }

    private static bool IsFieldSmetaUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(postgresException.ConstraintName, "UX_field_smeta_items_site_work", StringComparison.Ordinal);

    private static async Task<string> BuildWorkspaceFromCanonicalDataAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        var sites = await db.Sites.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToArrayAsync(ct);
        var workers = await db.Workers.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.FullName).ToArrayAsync(ct);
        var smetaItems = await db.FieldSmetaItems.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.StageName).ThenBy(x => x.WorkName).ToArrayAsync(ct);

        var projectId = tenantId.ToString();
        var estimateId = $"{tenantId:N}-estimate";
        var project = new
        {
            id = projectId,
            name = tenant?.CompanyName ?? "BuildTrack layihəsi",
            currency = "AZN",
            location = sites.FirstOrDefault()?.Address,
            clientName = tenant?.CompanyName,
            createdAt = DateTimeOffset.UtcNow.ToString("O"),
            activeEstimateVersionId = estimateId,
        };
        var stages = smetaItems
            .GroupBy(x => x.StageName)
            .Select((group, index) => new
            {
                id = SlugId("stage", group.Key),
                objectId = group.First().SiteId.ToString(),
                name = group.Key,
                order = index + 1,
                totalCost = 0,
                laborCost = 0,
                materialCost = 0,
                plannedStartDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                plannedEndDate = DateTime.UtcNow.Date.AddDays(30 + index * 7).ToString("yyyy-MM-dd"),
                status = "NotStarted",
                progressPercent = 0,
                plannedHours = 0,
                actualHours = 0,
                notes = "Backend smeta itemlərindən yaradılıb",
            })
            .ToArray();
        var stageMap = stages.ToDictionary(x => x.name, x => x.id);

        var data = new
        {
            workspaceTenantId = tenantId.ToString(),
            projects = new[] { project },
            activeProjectId = projectId,
            objects = sites.Select(site => new
            {
                id = site.Id.ToString(),
                name = site.Name,
                zone = site.Address,
                address = site.Address,
                projectId,
                status = "NotStarted",
                plannedStartDate = site.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd"),
                clientName = tenant?.CompanyName,
            }).ToArray(),
            project,
            estimateVersions = new[]
            {
                new { id = estimateId, projectId, name = "Backend smeta", createdAt = DateTimeOffset.UtcNow.ToString("O"), totalAmount = 0, notes = "Server workspace" },
            },
            summary = EmptySummary(),
            stages,
            workItems = smetaItems.Select((item, index) => new
            {
                id = string.IsNullOrWhiteSpace(item.ProjectWorkItemId) ? item.Id.ToString() : item.ProjectWorkItemId,
                objectId = item.SiteId.ToString(),
                stageId = stageMap.TryGetValue(item.StageName, out var stageId) ? stageId : SlugId("stage", item.StageName),
                name = item.WorkName,
                unit = item.Unit,
                quantity = item.PlannedQuantity ?? 0,
                laborUnitPrice = 0,
                laborTotal = 0,
                materialQuantity = item.PlannedQuantity ?? 0,
                materialUnitPrice = 0,
                materialTotal = 0,
                totalCost = 0,
                plannedHours = 0,
                actualHours = 0,
                completedQuantity = 0,
                status = "NotStarted",
                progressPercent = 0,
                notes = item.WorkCategory,
            }).ToArray(),
            crews = workers
                .Where(x => !string.IsNullOrWhiteSpace(x.Brigade))
                .GroupBy(x => x.Brigade!)
                .Select(group => new
                {
                    id = SlugId("crew", group.Key),
                    objectId = group.First().SiteId.ToString(),
                    name = group.Key,
                    type = group.First().Role ?? "Briqada",
                    foremanName = "Təyin edilməyib",
                    workerCount = group.Count(),
                    plannedDailyHours = 8,
                    status = "NotStarted",
                    progressPercent = 0,
                }).ToArray(),
            workerAssignments = workers.Select(worker => new
            {
                id = worker.Id.ToString(),
                workerName = worker.FullName,
                workerExternalId = worker.ExternalWorkerCode,
                projectId,
                objectId = worker.SiteId.ToString(),
                crewId = string.IsNullOrWhiteSpace(worker.Brigade) ? string.Empty : SlugId("crew", worker.Brigade),
                role = worker.Role ?? string.Empty,
                hourlyRate = worker.HourlyRate,
                plannedDailyHours = worker.PlannedDailyHours,
                attendanceSource = worker.AttendanceSource,
                status = worker.Status == WorkerStatus.Active ? "active" : "inactive",
                riskScore = worker.RiskScore,
                notes = worker.Notes,
            }).ToArray(),
            materials = Array.Empty<object>(),
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    internal static string? ValidateWorkspaceTenant(JsonElement body, Guid tenantId, bool allowLegacyImport)
    {
        if (allowLegacyImport) return null;
        if (!body.TryGetProperty("workspaceTenantId", out var workspaceTenantId)) return null;
        if (workspaceTenantId.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

        var value = workspaceTenantId.ValueKind == JsonValueKind.String
            ? workspaceTenantId.GetString()
            : workspaceTenantId.GetRawText();
        if (string.IsNullOrWhiteSpace(value)) return null;

        return string.Equals(value, tenantId.ToString(), StringComparison.OrdinalIgnoreCase)
            ? null
            : "Workspace tenant does not match authenticated tenant";
    }

    internal static string NormalizeWorkspaceJsonForTenant(JsonElement body, Guid tenantId)
    {
        var root = JsonNode.Parse(body.GetRawText())?.AsObject() ?? new JsonObject();
        NormalizeWorkspaceRoot(root, tenantId);
        return root.ToJsonString(JsonOptions);
    }

    internal static string NormalizeWorkspaceJsonStringForTenant(string workspaceJson, Guid tenantId)
    {
        var root = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
        NormalizeWorkspaceRoot(root, tenantId);
        return root.ToJsonString(JsonOptions);
    }

    private static void NormalizeWorkspaceRoot(JsonObject root, Guid tenantId)
    {
        root["workspaceTenantId"] = tenantId.ToString();
        NormalizeMaterialObjectIds(root);
    }

    private static void NormalizeMaterialObjectIds(JsonObject root)
    {
        var stageObjectIds = BuildObjectIdMap(root["stages"] as JsonArray);
        var workItemObjectIds = BuildObjectIdMap(root["workItems"] as JsonArray);

        if (root["materials"] is not JsonArray materials) return;
        foreach (var materialNode in materials)
        {
            if (materialNode is not JsonObject material) continue;
            if (!string.IsNullOrWhiteSpace(GetString(material["objectId"]))) continue;

            var linkedWorkItemId = GetString(material["linkedWorkItemId"]);
            if (!string.IsNullOrWhiteSpace(linkedWorkItemId) && workItemObjectIds.TryGetValue(linkedWorkItemId, out var workItemObjectId))
            {
                material["objectId"] = workItemObjectId;
                continue;
            }

            var linkedStageId = GetString(material["linkedStageId"]);
            if (!string.IsNullOrWhiteSpace(linkedStageId) && stageObjectIds.TryGetValue(linkedStageId, out var stageObjectId))
            {
                material["objectId"] = stageObjectId;
            }
        }
    }

    private static Dictionary<string, string> BuildObjectIdMap(JsonArray? rows)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows is null) return map;

        foreach (var rowNode in rows)
        {
            if (rowNode is not JsonObject row) continue;
            var id = GetString(row["id"]);
            var objectId = GetString(row["objectId"]);
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(objectId))
            {
                map[id] = objectId;
            }
        }

        return map;
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            return node.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ValidateWorkspace(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) return "Workspace must be a JSON object";
        if (!body.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array) return "Workspace projects array is required";
        if (!body.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array) return "Workspace objects array is required";
        return null;
    }

    private static object EmptySummary() => new
    {
        totalAmount = 0,
        laborAmount = 0,
        materialAmount = 0,
        hiddenCostAmount = 0,
        currency = "AZN",
    };

    private static string SlugId(string prefix, string value)
    {
        var normalized = string.Join("-", value.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-')).Replace(" ", "-");
        return $"{prefix}-{normalized}";
    }

    private static Guid RequireTenantId(ITenantContext tenantContext)
    {
        if (tenantContext.TenantId is { } tenantId) return tenantId;
        throw new InvalidOperationException("Tenant context is required.");
    }
}
