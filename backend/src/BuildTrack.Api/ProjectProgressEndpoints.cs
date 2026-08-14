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
    internal const int CurrentNormalizedMigrationVersion = 1;

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
        app.MapGet("/api/projects", GetProjectsAsync);
        app.MapPost("/api/projects", CreateProjectAsync);
        app.MapGet("/api/projects/{projectId}/stages", GetProjectStagesAsync);
        app.MapPost("/api/projects/{projectId}/stages", CreateProjectStageAsync);
        app.MapPut("/api/project-stages/{id}", UpdateProjectStageAsync);
        app.MapDelete("/api/project-stages/{id}", DeleteProjectStageAsync);
        app.MapGet("/api/projects/{projectId}/work-items", GetProjectWorkItemsAsync);
        app.MapPost("/api/projects/{projectId}/work-items", CreateProjectWorkItemAsync);
        app.MapPut("/api/project-work-items/{id}", UpdateProjectWorkItemAsync);
        app.MapDelete("/api/project-work-items/{id}", DeleteProjectWorkItemAsync);
        app.MapGet("/api/projects/{projectId}/crews", GetProjectCrewsAsync);
        app.MapPost("/api/projects/{projectId}/crews", CreateProjectCrewAsync);
        app.MapPut("/api/project-crews/{id}", UpdateProjectCrewAsync);
        app.MapDelete("/api/project-crews/{id}", DeleteProjectCrewAsync);
        app.MapGet("/api/projects/{projectId}/materials", GetProjectMaterialsAsync);
        app.MapPost("/api/projects/{projectId}/materials", CreateProjectMaterialAsync);
        app.MapPut("/api/project-materials/{id}", UpdateProjectMaterialAsync);
        app.MapDelete("/api/project-materials/{id}", DeleteProjectMaterialAsync);
        return app;
    }

    private static async Task<IResult> GetWorkspaceAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var workspaceJson = await BuildWorkspaceFromCanonicalTablesAsync(db, tenantId, ct);
        return Results.Content(workspaceJson, "application/json");
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
                await ImportWorkspaceJsonIntoCanonicalAsync(db, tenantId, workspace.WorkspaceJson, ct);
                MarkWorkspaceMigrationSucceeded(workspace, DateTimeOffset.UtcNow, "ManualWorkspaceSave");
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
                await ImportWorkspaceJsonIntoCanonicalAsync(db, tenantId, workspace.WorkspaceJson, ct);
                MarkWorkspaceMigrationSucceeded(workspace, DateTimeOffset.UtcNow, "LegacyBrowserImport");
                return Results.Ok(new { imported = true, workspaceId = workspace.Id, legacyBrowserImportedAt = workspace.LegacyBrowserImportedAt });
            },
            ct);
    }

    private static async Task<JsonNode> GetWorkspacePropertyAsync(BuildTrackDbContext db, ITenantContext tenantContext, string propertyName, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var workspaceJson = await BuildWorkspaceFromCanonicalTablesAsync(db, tenantId, ct);
        var node = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
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

    private static async Task<IResult> GetProjectsAsync(BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var rows = await db.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .ToArrayAsync(ct);
        return Results.Json(rows.Select(MapProject).ToArray(), JsonOptions);
    }

    private static async Task<IResult> CreateProjectAsync(JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var now = DateTimeOffset.UtcNow;
        var id = GetString(body, "id") ?? Guid.NewGuid().ToString();
        var name = CleanText(GetString(body, "name")) ?? "Yeni layihə";
        var project = new ProjectRecord
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Code = CleanText(GetString(body, "code")),
            Currency = CleanText(GetString(body, "currency")) ?? "AZN",
            Location = CleanText(GetString(body, "location")),
            ClientName = CleanText(GetString(body, "clientName")),
            ActiveEstimateVersionId = CleanText(GetString(body, "activeEstimateVersionId")),
            Status = ParseStatus(GetString(body, "status"), ProjectEntityStatus.InProgress),
            CreatedAt = now,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return Results.Json(MapProject(project), JsonOptions);
    }

    private static async Task<IResult> GetProjectStagesAsync(string projectId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var rows = await db.ProjectStages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToArrayAsync(ct);
        return Results.Json(rows.Select(MapStage).ToArray(), JsonOptions);
    }

    private static async Task<IResult> CreateProjectStageAsync(string projectId, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        if (!await db.Projects.AnyAsync(x => x.TenantId == tenantId && x.Id == projectId, ct)) return Results.NotFound(new { error = "Layihə tapılmadı" });

        var nextOrder = await db.ProjectStages.Where(x => x.TenantId == tenantId && x.ProjectId == projectId).Select(x => (int?)x.Order).MaxAsync(ct) ?? 0;
        var stage = new ProjectStageRecord
        {
            Id = GetString(body, "id") ?? Guid.NewGuid().ToString(),
            TenantId = tenantId,
            ProjectId = projectId,
            SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct),
            EstimateVersionId = CleanText(GetString(body, "estimateVersionId")),
            Name = CleanText(GetString(body, "name")) ?? "Yeni etap",
            Code = CleanText(GetString(body, "code")),
            Order = GetInt(body, "order", nextOrder + 1),
            TotalCost = GetDecimal(body, "totalCost"),
            LaborCost = GetDecimal(body, "laborCost"),
            MaterialCost = GetDecimal(body, "materialCost"),
            PlannedStartDate = GetDate(body, "plannedStartDate"),
            PlannedEndDate = GetDate(body, "plannedEndDate"),
            Status = ParseStatus(GetString(body, "status"), ProjectEntityStatus.NotStarted),
            ProgressPercent = ClampPercent(GetDecimal(body, "progressPercent")),
            AssignedCrewId = CleanText(GetString(body, "assignedCrewId")),
            PlannedHours = GetDecimal(body, "plannedHours"),
            ActualHours = GetDecimal(body, "actualHours"),
            Notes = CleanText(GetString(body, "notes")),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ProjectStages.Add(stage);
        await db.SaveChangesAsync(ct);
        return Results.Json(MapStage(stage), JsonOptions);
    }

    private static async Task<IResult> UpdateProjectStageAsync(string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var stage = await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (stage is null) return Results.NotFound(new { error = "Etap tapılmadı" });

        ApplyStagePatch(stage, body);
        stage.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct) ?? stage.SiteId;
        stage.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Json(MapStage(stage), JsonOptions);
    }

    private static async Task<IResult> DeleteProjectStageAsync(string id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var stage = await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (stage is null) return Results.NoContent();

        var now = DateTimeOffset.UtcNow;
        stage.IsActive = false;
        stage.Status = ProjectEntityStatus.Archived;
        stage.UpdatedAt = now;

        var items = await db.ProjectWorkItems.Where(x => x.TenantId == tenantId && x.StageId == id).ToArrayAsync(ct);
        foreach (var item in items)
        {
            item.IsActive = false;
            item.Status = ProjectEntityStatus.Archived;
            item.UpdatedAt = now;
        }

        var itemIds = items.Select(x => x.Id).ToArray();
        if (itemIds.Length > 0)
        {
            var smetaItems = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId && itemIds.Contains(x.ProjectWorkItemId!)).ToArrayAsync(ct);
            foreach (var smetaItem in smetaItems)
            {
                smetaItem.IsActive = false;
                smetaItem.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProjectWorkItemsAsync(string projectId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var rows = await db.ProjectWorkItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.IsActive)
            .OrderBy(x => x.StageId)
            .ThenBy(x => x.Name)
            .ToArrayAsync(ct);
        return Results.Json(rows.Select(MapWorkItem).ToArray(), JsonOptions);
    }

    private static async Task<IResult> CreateProjectWorkItemAsync(string projectId, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var stageId = CleanText(GetString(body, "stageId"));
        if (string.IsNullOrWhiteSpace(stageId)) return Results.BadRequest(new { error = "Etap seçilməlidir" });
        var stage = await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProjectId == projectId && x.Id == stageId, ct);
        if (stage is null) return Results.NotFound(new { error = "Etap tapılmadı" });

        var item = new ProjectWorkItemRecord
        {
            Id = GetString(body, "id") ?? Guid.NewGuid().ToString(),
            TenantId = tenantId,
            ProjectId = projectId,
            SiteId = await ResolveRequiredSiteIdAsync(db, tenantId, GetString(body, "objectId") ?? stage.SiteId?.ToString(), ct),
            StageId = stageId,
            EstimateVersionId = CleanText(GetString(body, "estimateVersionId")),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ApplyWorkItemPatch(item, body);
        await UpsertFieldSmetaItemForWorkItemAsync(db, tenantId, item, ct);
        db.ProjectWorkItems.Add(item);

        try
        {
            await db.SaveChangesAsync(ct);
            await RecalculateStageAsync(db, tenantId, item.StageId, ct);
            return Results.Json(MapWorkItem(item), JsonOptions);
        }
        catch (DbUpdateException ex) when (IsProjectProgressUniqueConflict(ex))
        {
            return Results.Conflict(new { error = "Bu layihə üzrə eyni smeta işi artıq mövcuddur.", code = "PROJECT_WORK_ITEM_CONFLICT" });
        }
    }

    private static async Task<IResult> UpdateProjectWorkItemAsync(string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var item = await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (item is null) return Results.NotFound(new { error = "Smeta sətri tapılmadı" });

        var previousStageId = item.StageId;
        if (body.TryGetProperty("objectId", out _))
        {
            item.SiteId = await ResolveRequiredSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct);
        }
        ApplyWorkItemPatch(item, body);
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await UpsertFieldSmetaItemForWorkItemAsync(db, tenantId, item, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await RecalculateStageAsync(db, tenantId, item.StageId, ct);
            if (!string.Equals(previousStageId, item.StageId, StringComparison.OrdinalIgnoreCase))
            {
                await RecalculateStageAsync(db, tenantId, previousStageId, ct);
            }
            return Results.Json(MapWorkItem(item), JsonOptions);
        }
        catch (DbUpdateException ex) when (IsProjectProgressUniqueConflict(ex))
        {
            return Results.Conflict(new { error = "Bu layihə üzrə eyni smeta işi artıq mövcuddur.", code = "PROJECT_WORK_ITEM_CONFLICT" });
        }
    }

    private static async Task<IResult> DeleteProjectWorkItemAsync(string id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var item = await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (item is null) return Results.NoContent();

        var hasReportLines = await db.SupervisorDailyReportLines.AnyAsync(x => x.TenantId == tenantId && x.ProjectWorkItemId == id, ct);
        var stageId = item.StageId;
        if (hasReportLines)
        {
            item.IsActive = false;
            item.Status = ProjectEntityStatus.Archived;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var materials = await db.ProjectWorkItemMaterials.Where(x => x.TenantId == tenantId && x.WorkItemId == id).ToArrayAsync(ct);
            db.ProjectWorkItemMaterials.RemoveRange(materials);
            db.ProjectWorkItems.Remove(item);
        }

        var smetaItems = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId && x.ProjectWorkItemId == id).ToArrayAsync(ct);
        foreach (var smetaItem in smetaItems)
        {
            smetaItem.IsActive = false;
            smetaItem.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await RecalculateStageAsync(db, tenantId, stageId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProjectCrewsAsync(string projectId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var rows = await db.ProjectCrews.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToArrayAsync(ct);
        return Results.Json(rows.Select(MapCrew).ToArray(), JsonOptions);
    }

    private static async Task<IResult> CreateProjectCrewAsync(string projectId, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var crew = new ProjectCrewRecord
        {
            Id = GetString(body, "id") ?? Guid.NewGuid().ToString(),
            TenantId = tenantId,
            ProjectId = projectId,
            SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ApplyCrewPatch(crew, body);
        db.ProjectCrews.Add(crew);
        await db.SaveChangesAsync(ct);
        return Results.Json(MapCrew(crew), JsonOptions);
    }

    private static async Task<IResult> UpdateProjectCrewAsync(string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var crew = await db.ProjectCrews.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (crew is null) return Results.NotFound(new { error = "Briqada tapılmadı" });
        if (body.TryGetProperty("objectId", out _))
        {
            crew.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct);
        }
        ApplyCrewPatch(crew, body);
        crew.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Json(MapCrew(crew), JsonOptions);
    }

    private static async Task<IResult> DeleteProjectCrewAsync(string id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var crew = await db.ProjectCrews.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (crew is null) return Results.NoContent();
        crew.IsActive = false;
        crew.Status = ProjectEntityStatus.Archived;
        crew.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProjectMaterialsAsync(string projectId, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var rows = await db.ProjectWorkItemMaterials.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToArrayAsync(ct);
        return Results.Json(rows.Select(MapMaterial).ToArray(), JsonOptions);
    }

    private static async Task<IResult> CreateProjectMaterialAsync(string projectId, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        await EnsureCanonicalProjectProgressAsync(db, tenantId, ct);
        var material = new ProjectWorkItemMaterialRecord
        {
            Id = GetString(body, "id") ?? Guid.NewGuid().ToString(),
            TenantId = tenantId,
            ProjectId = projectId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await ApplyMaterialPatchAsync(material, body, db, tenantId, ct);
        db.ProjectWorkItemMaterials.Add(material);
        await db.SaveChangesAsync(ct);
        return Results.Json(MapMaterial(material), JsonOptions);
    }

    private static async Task<IResult> UpdateProjectMaterialAsync(string id, JsonElement body, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var material = await db.ProjectWorkItemMaterials.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (material is null) return Results.NotFound(new { error = "Material tapılmadı" });
        await ApplyMaterialPatchAsync(material, body, db, tenantId, ct);
        material.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Json(MapMaterial(material), JsonOptions);
    }

    private static async Task<IResult> DeleteProjectMaterialAsync(string id, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct)
    {
        var tenantId = RequireTenantId(tenantContext);
        var material = await db.ProjectWorkItemMaterials.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (material is null) return Results.NoContent();
        material.IsActive = false;
        material.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
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
                "FIELD_SMETA_DUPLICATE_WORK_NAME" => "Eyni layihə daxilində eyni adlı iki aktiv smeta işi yaradıla bilməz.",
                _ => "Smeta sinxronizasiyası zamanı eyni layihə və iş adı üzrə konflikt aşkarlandı.",
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
                error = "Smeta sinxronizasiyası zamanı eyni layihə və iş adı üzrə konflikt aşkarlandı.",
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
            WorkspaceJson = await BuildWorkspaceFromCanonicalTablesAsync(db, tenantId, ct),
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
            WorkspaceJson = await BuildWorkspaceFromCanonicalTablesAsync(db, tenantId, ct),
        };
        db.ProjectProgressWorkspaces.Add(workspace);
        return workspace;
    }

    private static bool IsFieldSmetaUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(postgresException.ConstraintName, "UX_field_smeta_items_site_work", StringComparison.Ordinal);

    private static bool IsProjectProgressUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;

    internal static async Task EnsureCanonicalProjectProgressAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var hasProject = await db.Projects.AnyAsync(x => x.TenantId == tenantId, ct);
        var hasWorkItems = await db.ProjectWorkItems.AnyAsync(x => x.TenantId == tenantId, ct);
        var workspace = db.ProjectProgressWorkspaces.Local.FirstOrDefault(x => x.TenantId == tenantId)
            ?? await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (workspace is not null && workspace.NormalizedMigrationVersion < CurrentNormalizedMigrationVersion)
        {
            if (ShouldRunLegacyWorkspaceMigration(workspace.WorkspaceJson, hasProject, hasWorkItems))
            {
                await RunLegacyWorkspaceMigrationTransactionAsync(db, tenantId, workspace, ct);
                return;
            }

            if (hasProject)
            {
                MarkWorkspaceMigrationSucceeded(workspace, DateTimeOffset.UtcNow, "CanonicalDataAlreadyPresent");
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        if (!hasProject)
        {
            await CreateDefaultCanonicalProjectAsync(db, tenantId, ct);
            if (workspace is not null)
            {
                MarkWorkspaceMigrationSucceeded(workspace, DateTimeOffset.UtcNow, "DefaultCanonicalProjectCreated");
            }
            await db.SaveChangesAsync(ct);
            return;
        }

        await EnsureProjectSitesFromTenantSitesAsync(db, tenantId, ct);
        await db.SaveChangesAsync(ct);
    }

    private static bool ShouldRunLegacyWorkspaceMigration(string workspaceJson, bool hasProject, bool hasWorkItems)
    {
        if (string.IsNullOrWhiteSpace(workspaceJson)) return false;
        var root = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
        var hasLegacyProjects = root["projects"] is JsonArray projects && projects.Count > 0;
        var hasLegacyWorkItems = root["workItems"] is JsonArray workItems && workItems.Count > 0;
        var hasLegacyStages = root["stages"] is JsonArray stages && stages.Count > 0;
        return (hasLegacyProjects || hasLegacyStages || hasLegacyWorkItems) && (!hasProject || !hasWorkItems);
    }

    private static async Task RunLegacyWorkspaceMigrationTransactionAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        ProjectProgressWorkspace workspace,
        CancellationToken ct)
    {
        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
        {
            transaction = await db.Database.BeginTransactionAsync(ct);
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            await ImportWorkspaceJsonIntoCanonicalAsync(db, tenantId, workspace.WorkspaceJson, ct);
            MarkWorkspaceMigrationSucceeded(workspace, now, "LegacyWorkspaceInitialMigration");
            workspace.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static void MarkWorkspaceMigrationSucceeded(ProjectProgressWorkspace workspace, DateTimeOffset migratedAt, string status)
    {
        workspace.NormalizedMigrationVersion = CurrentNormalizedMigrationVersion;
        workspace.NormalizedMigrationStatus = status;
        workspace.NormalizedMigratedAt = migratedAt;
        workspace.NormalizedMigrationError = null;
    }

    private static async Task<ProjectRecord?> FindProjectAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.Projects.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.Projects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<ProjectEstimateVersionRecord?> FindEstimateVersionAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.ProjectEstimateVersions.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.ProjectEstimateVersions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<ProjectSiteRecord?> FindProjectSiteAsync(BuildTrackDbContext db, Guid tenantId, string projectId, Guid siteId, CancellationToken ct) =>
        db.ProjectSites.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.ProjectId, projectId, StringComparison.OrdinalIgnoreCase) && x.SiteId == siteId)
        ?? await db.ProjectSites.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProjectId == projectId && x.SiteId == siteId, ct);

    private static async Task<ProjectStageRecord?> FindProjectStageAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.ProjectStages.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<ProjectWorkItemRecord?> FindProjectWorkItemAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.ProjectWorkItems.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<ProjectCrewRecord?> FindProjectCrewAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.ProjectCrews.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.ProjectCrews.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<ProjectWorkItemMaterialRecord?> FindProjectMaterialAsync(BuildTrackDbContext db, Guid tenantId, string id, CancellationToken ct) =>
        db.ProjectWorkItemMaterials.Local.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? await db.ProjectWorkItemMaterials.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    private static async Task<FieldSmetaItem?> FindFieldSmetaByWorkItemAsync(BuildTrackDbContext db, Guid tenantId, Guid siteId, string workItemId, CancellationToken ct) =>
        db.FieldSmetaItems.Local.FirstOrDefault(x => x.TenantId == tenantId && x.SiteId == siteId && string.Equals(x.ProjectWorkItemId, workItemId, StringComparison.OrdinalIgnoreCase))
        ?? await db.FieldSmetaItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.ProjectWorkItemId == workItemId, ct);

    private static async Task<FieldSmetaItem?> FindActiveFieldSmetaByNameAsync(BuildTrackDbContext db, Guid tenantId, Guid siteId, string workName, CancellationToken ct) =>
        db.FieldSmetaItems.Local.FirstOrDefault(x => x.TenantId == tenantId && x.SiteId == siteId && x.IsActive && string.Equals(x.WorkName, workName, StringComparison.Ordinal))
        ?? await db.FieldSmetaItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.IsActive && x.WorkName == workName, ct);

    private static async Task CreateDefaultCanonicalProjectAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        var projectId = tenantId.ToString();
        var estimateId = $"{tenantId:N}-estimate";
        var now = DateTimeOffset.UtcNow;
        var project = await FindProjectAsync(db, tenantId, projectId, ct);
        if (project is null)
        {
            project = new ProjectRecord
            {
                Id = projectId,
                TenantId = tenantId,
                CreatedAt = now,
            };
            db.Projects.Add(project);
        }

        project.Name = tenant?.CompanyName ?? "BuildTrack layihəsi";
        project.Currency = "AZN";
        project.ClientName = tenant?.CompanyName;
        project.ActiveEstimateVersionId = estimateId;
        project.UpdatedAt = now;

        var estimate = await FindEstimateVersionAsync(db, tenantId, estimateId, ct);
        if (estimate is null)
        {
            estimate = new ProjectEstimateVersionRecord
            {
                Id = estimateId,
                TenantId = tenantId,
                CreatedAt = now,
            };
            db.ProjectEstimateVersions.Add(estimate);
        }

        estimate.ProjectId = projectId;
        estimate.Name = "Cari smeta";
        estimate.UpdatedAt = now;
        await EnsureProjectSitesFromTenantSitesAsync(db, tenantId, ct, projectId);
        await ImportFieldSmetaItemsIntoCanonicalAsync(db, tenantId, projectId, estimateId, ct);
    }

    private static async Task EnsureProjectSitesFromTenantSitesAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct, string? projectId = null)
    {
        var project = projectId is not null
            ? await FindProjectAsync(db, tenantId, projectId, ct)
            : await db.Projects.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (project is null) return;

        if (projectId is null)
        {
            var hasExplicitProjectSites = db.ProjectSites.Local.Any(x => x.TenantId == tenantId && x.ProjectId == project.Id)
                || await db.ProjectSites.AnyAsync(x => x.TenantId == tenantId && x.ProjectId == project.Id, ct);
            if (hasExplicitProjectSites) return;
        }

        var existing = (await db.ProjectSites
            .Where(x => x.TenantId == tenantId && x.ProjectId == project.Id)
            .Select(x => x.SiteId)
            .ToListAsync(ct))
            .Concat(db.ProjectSites.Local.Where(x => x.TenantId == tenantId && x.ProjectId == project.Id).Select(x => x.SiteId))
            .ToArray();
        var existingSet = existing.ToHashSet();
        var sites = await db.Sites.Where(x => x.TenantId == tenantId).ToArrayAsync(ct);
        foreach (var site in sites.Where(site => !existingSet.Contains(site.Id)))
        {
            db.ProjectSites.Add(new ProjectSiteRecord
            {
                Id = site.Id.ToString(),
                TenantId = tenantId,
                ProjectId = project.Id,
                SiteId = site.Id,
                Zone = site.Address,
                Status = ProjectEntityStatus.NotStarted,
                PlannedStartDate = DateOnly.FromDateTime(site.CreatedAt.UtcDateTime.Date),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static async Task ImportFieldSmetaItemsIntoCanonicalAsync(BuildTrackDbContext db, Guid tenantId, string projectId, string estimateId, CancellationToken ct)
    {
        var smetaItems = await db.FieldSmetaItems
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.StageName)
            .ThenBy(x => x.WorkName)
            .ToArrayAsync(ct);
        if (smetaItems.Length == 0) return;

        var stageIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var order = Math.Max(
            await db.ProjectStages.Where(x => x.TenantId == tenantId && x.ProjectId == projectId).Select(x => (int?)x.Order).MaxAsync(ct) ?? 0,
            db.ProjectStages.Local.Where(x => x.TenantId == tenantId && x.ProjectId == projectId).Select(x => (int?)x.Order).Max() ?? 0);
        foreach (var group in smetaItems.GroupBy(x => x.StageName))
        {
            var first = group.First();
            var stageId = SlugId("stage", $"{first.SiteId:N}-{group.Key}");
            stageIdsByName[group.Key] = stageId;
            var stage = await FindProjectStageAsync(db, tenantId, stageId, ct);
            if (stage is not null) continue;

            db.ProjectStages.Add(new ProjectStageRecord
            {
                Id = stageId,
                TenantId = tenantId,
                ProjectId = projectId,
                EstimateVersionId = estimateId,
                SiteId = first.SiteId,
                Name = group.Key,
                Order = ++order,
                Status = ProjectEntityStatus.NotStarted,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        foreach (var smetaItem in smetaItems)
        {
            var workItemId = string.IsNullOrWhiteSpace(smetaItem.ProjectWorkItemId) ? smetaItem.Id.ToString() : smetaItem.ProjectWorkItemId!;
            var workItem = await FindProjectWorkItemAsync(db, tenantId, workItemId, ct);
            if (workItem is not null) continue;

            smetaItem.ProjectWorkItemId = workItemId;
            db.ProjectWorkItems.Add(new ProjectWorkItemRecord
            {
                Id = workItemId,
                TenantId = tenantId,
                ProjectId = projectId,
                SiteId = smetaItem.SiteId,
                StageId = stageIdsByName.GetValueOrDefault(smetaItem.StageName) ?? SlugId("stage", $"{smetaItem.SiteId:N}-{smetaItem.StageName}"),
                EstimateVersionId = estimateId,
                Name = smetaItem.WorkName,
                Unit = smetaItem.Unit,
                Quantity = smetaItem.PlannedQuantity ?? 0,
                MaterialQuantity = smetaItem.PlannedQuantity ?? 0,
                Notes = smetaItem.WorkCategory,
                Status = ProjectEntityStatus.NotStarted,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static async Task ImportWorkspaceJsonIntoCanonicalAsync(BuildTrackDbContext db, Guid tenantId, string workspaceJson, CancellationToken ct)
    {
        var root = JsonNode.Parse(workspaceJson)?.AsObject() ?? new JsonObject();
        NormalizeWorkspaceRoot(root, tenantId);
        var now = DateTimeOffset.UtcNow;

        var projectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["projects"] is JsonArray projects)
        {
            foreach (var node in projects.OfType<JsonObject>())
            {
                var id = CleanText(GetString(node["id"])) ?? tenantId.ToString();
                projectIds.Add(id);
                var project = await FindProjectAsync(db, tenantId, id, ct);
                if (project is null)
                {
                    project = new ProjectRecord { Id = id, TenantId = tenantId, CreatedAt = now };
                    db.Projects.Add(project);
                }

                project.Name = CleanText(GetString(node["name"])) ?? project.Name;
                project.Currency = CleanText(GetString(node["currency"])) ?? "AZN";
                project.Location = CleanText(GetString(node["location"]));
                project.ClientName = CleanText(GetString(node["clientName"]));
                project.ActiveEstimateVersionId = CleanText(GetString(node["activeEstimateVersionId"]));
                project.UpdatedAt = now;
            }
        }

        if (projectIds.Count == 0)
        {
            projectIds.Add(CleanText(GetString(root["activeProjectId"])) ?? tenantId.ToString());
        }

        var defaultProjectId = CleanText(GetString(root["activeProjectId"])) ?? projectIds.First();
        var defaultProject = await FindProjectAsync(db, tenantId, defaultProjectId, ct);
        if (defaultProject is null)
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
            defaultProject = new ProjectRecord
            {
                Id = defaultProjectId,
                TenantId = tenantId,
                Name = tenant?.CompanyName ?? "BuildTrack layihəsi",
                Currency = "AZN",
                CreatedAt = now,
            };
            db.Projects.Add(defaultProject);
        }

        if (root["estimateVersions"] is JsonArray estimates)
        {
            foreach (var node in estimates.OfType<JsonObject>())
            {
                var id = CleanText(GetString(node["id"]));
                if (string.IsNullOrWhiteSpace(id)) continue;
                var estimate = await FindEstimateVersionAsync(db, tenantId, id, ct);
                if (estimate is null)
                {
                    estimate = new ProjectEstimateVersionRecord { Id = id, TenantId = tenantId, CreatedAt = now };
                    db.ProjectEstimateVersions.Add(estimate);
                }

                estimate.ProjectId = CleanText(GetString(node["projectId"])) ?? defaultProjectId;
                estimate.Name = CleanText(GetString(node["name"])) ?? estimate.Name;
                estimate.TotalAmount = GetDecimal(node["totalAmount"]);
                estimate.Notes = CleanText(GetString(node["notes"]));
                estimate.UpdatedAt = now;
            }
        }

        await UpsertObjectsFromWorkspaceAsync(db, tenantId, defaultProjectId, root["objects"] as JsonArray, ct);
        await UpsertStagesFromWorkspaceAsync(db, tenantId, defaultProjectId, root["stages"] as JsonArray, ct);
        await UpsertWorkItemsFromWorkspaceAsync(db, tenantId, defaultProjectId, root["workItems"] as JsonArray, ct);
        await UpsertCrewsFromWorkspaceAsync(db, tenantId, defaultProjectId, root["crews"] as JsonArray, ct);
        await UpsertMaterialsFromWorkspaceAsync(db, tenantId, defaultProjectId, root["materials"] as JsonArray, ct);
    }

    private static async Task UpsertObjectsFromWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, string defaultProjectId, JsonArray? objects, CancellationToken ct)
    {
        if (objects is null) return;
        foreach (var node in objects.OfType<JsonObject>())
        {
            var objectId = CleanText(GetString(node["id"]));
            if (!Guid.TryParse(objectId, out var siteId)) continue;
            if (!await db.Sites.AnyAsync(x => x.TenantId == tenantId && x.Id == siteId, ct)) continue;

            var projectId = CleanText(GetString(node["projectId"])) ?? defaultProjectId;
            var projectSite = await FindProjectSiteAsync(db, tenantId, projectId, siteId, ct);
            if (projectSite is null)
            {
                projectSite = new ProjectSiteRecord
                {
                    Id = siteId.ToString(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    SiteId = siteId,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.ProjectSites.Add(projectSite);
            }

            projectSite.Zone = CleanText(GetString(node["zone"])) ?? CleanText(GetString(node["address"]));
            projectSite.Status = ParseStatus(GetString(node["status"]), projectSite.Status);
            projectSite.PlannedStartDate = GetDate(node["plannedStartDate"]);
            projectSite.PlannedEndDate = GetDate(node["plannedEndDate"]);
            projectSite.Notes = CleanText(GetString(node["notes"]));
            projectSite.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task UpsertStagesFromWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, string defaultProjectId, JsonArray? stages, CancellationToken ct)
    {
        if (stages is null) return;
        foreach (var node in stages.OfType<JsonObject>())
        {
            var id = CleanText(GetString(node["id"]));
            if (string.IsNullOrWhiteSpace(id)) continue;
            var stage = await FindProjectStageAsync(db, tenantId, id, ct);
            if (stage is null)
            {
                stage = new ProjectStageRecord { Id = id, TenantId = tenantId, ProjectId = defaultProjectId, CreatedAt = DateTimeOffset.UtcNow };
                db.ProjectStages.Add(stage);
            }

            ApplyStagePatch(stage, node);
            stage.ProjectId = CleanText(GetString(node["projectId"])) ?? stage.ProjectId ?? defaultProjectId;
            stage.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(node["objectId"]), ct);
            stage.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task UpsertWorkItemsFromWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, string defaultProjectId, JsonArray? workItems, CancellationToken ct)
    {
        if (workItems is null) return;
        foreach (var node in workItems.OfType<JsonObject>())
        {
            var id = CleanText(GetString(node["id"]));
            var stageId = CleanText(GetString(node["stageId"]));
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(stageId)) continue;
            var siteId = await ResolveSiteIdAsync(db, tenantId, GetString(node["objectId"]), ct);
            if (siteId is null) continue;

            var item = await FindProjectWorkItemAsync(db, tenantId, id, ct);
            if (item is null)
            {
                item = new ProjectWorkItemRecord
                {
                    Id = id,
                    TenantId = tenantId,
                    ProjectId = defaultProjectId,
                    SiteId = siteId.Value,
                    StageId = stageId,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.ProjectWorkItems.Add(item);
            }

            item.ProjectId = CleanText(GetString(node["projectId"])) ?? item.ProjectId ?? defaultProjectId;
            item.SiteId = siteId.Value;
            ApplyWorkItemPatch(item, node);
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await UpsertFieldSmetaItemForWorkItemAsync(db, tenantId, item, ct);
        }
    }

    private static async Task UpsertCrewsFromWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, string defaultProjectId, JsonArray? crews, CancellationToken ct)
    {
        if (crews is null) return;
        foreach (var node in crews.OfType<JsonObject>())
        {
            var id = CleanText(GetString(node["id"]));
            if (string.IsNullOrWhiteSpace(id)) continue;
            var crew = await FindProjectCrewAsync(db, tenantId, id, ct);
            if (crew is null)
            {
                crew = new ProjectCrewRecord { Id = id, TenantId = tenantId, ProjectId = defaultProjectId, CreatedAt = DateTimeOffset.UtcNow };
                db.ProjectCrews.Add(crew);
            }

            crew.ProjectId = CleanText(GetString(node["projectId"])) ?? crew.ProjectId ?? defaultProjectId;
            crew.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(node["objectId"]), ct);
            ApplyCrewPatch(crew, node);
            crew.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task UpsertMaterialsFromWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, string defaultProjectId, JsonArray? materials, CancellationToken ct)
    {
        if (materials is null) return;
        foreach (var node in materials.OfType<JsonObject>())
        {
            var id = CleanText(GetString(node["id"]));
            if (string.IsNullOrWhiteSpace(id)) continue;
            var material = await FindProjectMaterialAsync(db, tenantId, id, ct);
            if (material is null)
            {
                material = new ProjectWorkItemMaterialRecord { Id = id, TenantId = tenantId, ProjectId = defaultProjectId, CreatedAt = DateTimeOffset.UtcNow };
                db.ProjectWorkItemMaterials.Add(material);
            }

            material.ProjectId = CleanText(GetString(node["projectId"])) ?? material.ProjectId ?? defaultProjectId;
            await ApplyMaterialPatchAsync(material, node, db, tenantId, ct);
            material.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static object MapProject(ProjectRecord project) => new
    {
        id = project.Id,
        name = project.Name,
        currency = project.Currency,
        location = project.Location,
        clientName = project.ClientName,
        createdAt = project.CreatedAt.ToString("O"),
        activeEstimateVersionId = project.ActiveEstimateVersionId ?? string.Empty,
    };

    private static object MapStage(ProjectStageRecord stage) => new
    {
        id = stage.Id,
        objectId = stage.SiteId?.ToString(),
        name = stage.Name,
        order = stage.Order,
        totalCost = stage.TotalCost,
        laborCost = stage.LaborCost,
        materialCost = stage.MaterialCost,
        plannedStartDate = FormatDate(stage.PlannedStartDate),
        plannedEndDate = FormatDate(stage.PlannedEndDate),
        status = stage.Status == ProjectEntityStatus.Archived ? ProjectEntityStatus.NotStarted.ToString() : stage.Status.ToString(),
        progressPercent = stage.ProgressPercent,
        assignedCrewId = stage.AssignedCrewId,
        plannedHours = stage.PlannedHours,
        actualHours = stage.ActualHours,
        notes = stage.Notes,
    };

    private static object MapWorkItem(ProjectWorkItemRecord item) => new
    {
        id = item.Id,
        objectId = item.SiteId.ToString(),
        stageId = item.StageId,
        name = item.Name,
        costCode = item.Code,
        unit = item.Unit,
        quantity = item.Quantity,
        unitPrice = item.UnitPrice,
        completedQuantity = item.CompletedQuantity,
        laborUnitPrice = item.LaborUnitPrice,
        laborTotal = item.LaborTotal,
        materialUnit = item.MaterialUnit,
        materialQuantity = item.MaterialQuantity,
        materialUnitPrice = item.MaterialUnitPrice,
        materialTotal = item.MaterialTotal,
        totalCost = item.TotalCost,
        plannedHours = item.PlannedHours,
        actualHours = item.ActualHours,
        remainingHours = Math.Max(0, item.PlannedHours - item.ActualHours),
        assignedCrewId = item.AssignedCrewId,
        status = item.Status == ProjectEntityStatus.Archived ? ProjectEntityStatus.NotStarted.ToString() : item.Status.ToString(),
        progressPercent = item.ProgressPercent,
        plannedStartDate = FormatDate(item.PlannedStartDate),
        plannedEndDate = FormatDate(item.PlannedEndDate),
        notes = item.Notes,
    };

    private static object MapCrew(ProjectCrewRecord crew) => new
    {
        id = crew.Id,
        objectId = crew.SiteId?.ToString(),
        name = crew.Name,
        type = crew.Type,
        foremanName = crew.ForemanName,
        workerCount = crew.WorkerCount,
        activeWorkStageId = crew.ActiveWorkStageId,
        activeWorkItemId = crew.ActiveWorkItemId,
        plannedDailyHours = crew.PlannedDailyHours,
        status = crew.Status?.ToString(),
        progressPercent = crew.ProgressPercent,
        notes = crew.Notes,
    };

    private static object MapMaterial(ProjectWorkItemMaterialRecord material) => new
    {
        id = material.Id,
        objectId = material.SiteId?.ToString(),
        catalogItemId = material.CatalogItemId,
        category = material.Category,
        name = material.Name,
        unit = material.Unit,
        quantity = material.Quantity,
        usedQuantity = material.UsedQuantity,
        remainingQuantity = material.RemainingQuantity,
        unitPrice = material.UnitPrice,
        linkedStageId = material.StageId,
        linkedWorkItemId = material.WorkItemId,
        deliveryDate = FormatDate(material.DeliveryDate),
        supplier = material.Supplier,
        notes = material.Notes,
    };

    private static void ApplyStagePatch(ProjectStageRecord stage, JsonElement body)
    {
        stage.Name = CleanText(GetString(body, "name")) ?? stage.Name;
        stage.Code = CleanText(GetString(body, "code")) ?? stage.Code;
        stage.EstimateVersionId = CleanText(GetString(body, "estimateVersionId")) ?? stage.EstimateVersionId;
        stage.Order = GetInt(body, "order", stage.Order);
        stage.TotalCost = GetDecimal(body, "totalCost", stage.TotalCost);
        stage.LaborCost = GetDecimal(body, "laborCost", stage.LaborCost);
        stage.MaterialCost = GetDecimal(body, "materialCost", stage.MaterialCost);
        stage.PlannedStartDate = GetDate(body, "plannedStartDate") ?? stage.PlannedStartDate;
        stage.PlannedEndDate = GetDate(body, "plannedEndDate") ?? stage.PlannedEndDate;
        stage.Status = ParseStatus(GetString(body, "status"), stage.Status);
        stage.ProgressPercent = ClampPercent(GetDecimal(body, "progressPercent", stage.ProgressPercent));
        stage.AssignedCrewId = CleanText(GetString(body, "assignedCrewId")) ?? stage.AssignedCrewId;
        stage.PlannedHours = GetDecimal(body, "plannedHours", stage.PlannedHours);
        stage.ActualHours = GetDecimal(body, "actualHours", stage.ActualHours);
        stage.Notes = CleanText(GetString(body, "notes")) ?? stage.Notes;
    }

    private static void ApplyStagePatch(ProjectStageRecord stage, JsonObject node)
    {
        stage.Name = CleanText(GetString(node["name"])) ?? stage.Name;
        stage.Code = CleanText(GetString(node["code"])) ?? stage.Code;
        stage.EstimateVersionId = CleanText(GetString(node["estimateVersionId"])) ?? stage.EstimateVersionId;
        stage.Order = GetInt(node["order"], stage.Order);
        stage.TotalCost = GetDecimal(node["totalCost"], stage.TotalCost);
        stage.LaborCost = GetDecimal(node["laborCost"], stage.LaborCost);
        stage.MaterialCost = GetDecimal(node["materialCost"], stage.MaterialCost);
        stage.PlannedStartDate = GetDate(node["plannedStartDate"]) ?? stage.PlannedStartDate;
        stage.PlannedEndDate = GetDate(node["plannedEndDate"]) ?? stage.PlannedEndDate;
        stage.Status = ParseStatus(GetString(node["status"]), stage.Status);
        stage.ProgressPercent = ClampPercent(GetDecimal(node["progressPercent"], stage.ProgressPercent));
        stage.AssignedCrewId = CleanText(GetString(node["assignedCrewId"])) ?? stage.AssignedCrewId;
        stage.PlannedHours = GetDecimal(node["plannedHours"], stage.PlannedHours);
        stage.ActualHours = GetDecimal(node["actualHours"], stage.ActualHours);
        stage.Notes = CleanText(GetString(node["notes"])) ?? stage.Notes;
    }

    private static void ApplyWorkItemPatch(ProjectWorkItemRecord item, JsonElement body)
    {
        item.StageId = CleanText(GetString(body, "stageId")) ?? item.StageId;
        item.EstimateVersionId = CleanText(GetString(body, "estimateVersionId")) ?? item.EstimateVersionId;
        item.Code = CleanText(GetString(body, "costCode")) ?? CleanText(GetString(body, "code")) ?? item.Code;
        item.Name = CleanText(GetString(body, "name")) ?? item.Name;
        item.Unit = CleanText(GetString(body, "unit")) ?? item.Unit;
        item.Quantity = GetDecimal(body, "quantity", item.Quantity);
        item.CompletedQuantity = GetDecimal(body, "completedQuantity", item.CompletedQuantity);
        item.UnitPrice = GetNullableDecimal(body, "unitPrice") ?? item.UnitPrice;
        item.LaborUnitPrice = GetDecimal(body, "laborUnitPrice", item.LaborUnitPrice);
        item.LaborTotal = GetDecimal(body, "laborTotal", Math.Round(item.Quantity * item.LaborUnitPrice, 2));
        item.MaterialUnit = CleanText(GetString(body, "materialUnit")) ?? item.MaterialUnit;
        item.MaterialQuantity = GetDecimal(body, "materialQuantity", item.MaterialQuantity);
        item.MaterialUnitPrice = GetDecimal(body, "materialUnitPrice", item.MaterialUnitPrice);
        item.MaterialTotal = GetDecimal(body, "materialTotal", Math.Round(item.MaterialQuantity * item.MaterialUnitPrice, 2));
        item.TotalCost = GetDecimal(body, "totalCost", Math.Round(item.LaborTotal + item.MaterialTotal, 2));
        item.PlannedHours = GetDecimal(body, "plannedHours", item.PlannedHours);
        item.ActualHours = GetDecimal(body, "actualHours", item.ActualHours);
        item.AssignedCrewId = CleanText(GetString(body, "assignedCrewId")) ?? item.AssignedCrewId;
        item.Status = ParseStatus(GetString(body, "status"), item.Status);
        item.ProgressPercent = ClampPercent(GetDecimal(body, "progressPercent", item.ProgressPercent));
        item.PlannedStartDate = GetDate(body, "plannedStartDate") ?? item.PlannedStartDate;
        item.PlannedEndDate = GetDate(body, "plannedEndDate") ?? item.PlannedEndDate;
        item.Notes = CleanText(GetString(body, "notes")) ?? item.Notes;
        if (item.ProgressPercent <= 0 && item.Quantity > 0 && item.CompletedQuantity > 0)
        {
            item.ProgressPercent = ClampPercent(Math.Round(item.CompletedQuantity / item.Quantity * 100m, 1));
        }
    }

    private static void ApplyWorkItemPatch(ProjectWorkItemRecord item, JsonObject node)
    {
        item.StageId = CleanText(GetString(node["stageId"])) ?? item.StageId;
        item.EstimateVersionId = CleanText(GetString(node["estimateVersionId"])) ?? item.EstimateVersionId;
        item.Code = CleanText(GetString(node["costCode"])) ?? CleanText(GetString(node["code"])) ?? item.Code;
        item.Name = CleanText(GetString(node["name"])) ?? item.Name;
        item.Unit = CleanText(GetString(node["unit"])) ?? item.Unit;
        item.Quantity = GetDecimal(node["quantity"], item.Quantity);
        item.CompletedQuantity = GetDecimal(node["completedQuantity"], item.CompletedQuantity);
        item.UnitPrice = GetNullableDecimal(node["unitPrice"]) ?? item.UnitPrice;
        item.LaborUnitPrice = GetDecimal(node["laborUnitPrice"], item.LaborUnitPrice);
        item.LaborTotal = GetDecimal(node["laborTotal"], Math.Round(item.Quantity * item.LaborUnitPrice, 2));
        item.MaterialUnit = CleanText(GetString(node["materialUnit"])) ?? item.MaterialUnit;
        item.MaterialQuantity = GetDecimal(node["materialQuantity"], item.MaterialQuantity);
        item.MaterialUnitPrice = GetDecimal(node["materialUnitPrice"], item.MaterialUnitPrice);
        item.MaterialTotal = GetDecimal(node["materialTotal"], Math.Round(item.MaterialQuantity * item.MaterialUnitPrice, 2));
        item.TotalCost = GetDecimal(node["totalCost"], Math.Round(item.LaborTotal + item.MaterialTotal, 2));
        item.PlannedHours = GetDecimal(node["plannedHours"], item.PlannedHours);
        item.ActualHours = GetDecimal(node["actualHours"], item.ActualHours);
        item.AssignedCrewId = CleanText(GetString(node["assignedCrewId"])) ?? item.AssignedCrewId;
        item.Status = ParseStatus(GetString(node["status"]), item.Status);
        item.ProgressPercent = ClampPercent(GetDecimal(node["progressPercent"], item.ProgressPercent));
        item.PlannedStartDate = GetDate(node["plannedStartDate"]) ?? item.PlannedStartDate;
        item.PlannedEndDate = GetDate(node["plannedEndDate"]) ?? item.PlannedEndDate;
        item.Notes = CleanText(GetString(node["notes"])) ?? item.Notes;
        if (item.ProgressPercent <= 0 && item.Quantity > 0 && item.CompletedQuantity > 0)
        {
            item.ProgressPercent = ClampPercent(Math.Round(item.CompletedQuantity / item.Quantity * 100m, 1));
        }
    }

    private static void ApplyCrewPatch(ProjectCrewRecord crew, JsonElement body)
    {
        crew.Name = CleanText(GetString(body, "name")) ?? crew.Name;
        crew.Type = CleanText(GetString(body, "type")) ?? crew.Type;
        crew.ForemanName = CleanText(GetString(body, "foremanName")) ?? crew.ForemanName;
        crew.WorkerCount = GetInt(body, "workerCount", crew.WorkerCount);
        crew.ActiveWorkStageId = CleanText(GetString(body, "activeWorkStageId")) ?? crew.ActiveWorkStageId;
        crew.ActiveWorkItemId = CleanText(GetString(body, "activeWorkItemId")) ?? crew.ActiveWorkItemId;
        crew.PlannedDailyHours = GetDecimal(body, "plannedDailyHours", crew.PlannedDailyHours);
        crew.Status = ParseNullableStatus(GetString(body, "status")) ?? crew.Status;
        crew.ProgressPercent = GetNullableDecimal(body, "progressPercent") ?? crew.ProgressPercent;
        crew.Notes = CleanText(GetString(body, "notes")) ?? crew.Notes;
    }

    private static void ApplyCrewPatch(ProjectCrewRecord crew, JsonObject node)
    {
        crew.Name = CleanText(GetString(node["name"])) ?? crew.Name;
        crew.Type = CleanText(GetString(node["type"])) ?? crew.Type;
        crew.ForemanName = CleanText(GetString(node["foremanName"])) ?? crew.ForemanName;
        crew.WorkerCount = GetInt(node["workerCount"], crew.WorkerCount);
        crew.ActiveWorkStageId = CleanText(GetString(node["activeWorkStageId"])) ?? crew.ActiveWorkStageId;
        crew.ActiveWorkItemId = CleanText(GetString(node["activeWorkItemId"])) ?? crew.ActiveWorkItemId;
        crew.PlannedDailyHours = GetDecimal(node["plannedDailyHours"], crew.PlannedDailyHours);
        crew.Status = ParseNullableStatus(GetString(node["status"])) ?? crew.Status;
        crew.ProgressPercent = GetNullableDecimal(node["progressPercent"]) ?? crew.ProgressPercent;
        crew.Notes = CleanText(GetString(node["notes"])) ?? crew.Notes;
    }

    private static async Task ApplyMaterialPatchAsync(ProjectWorkItemMaterialRecord material, JsonElement body, BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        material.CatalogItemId = CleanText(GetString(body, "catalogItemId")) ?? material.CatalogItemId;
        material.Category = CleanText(GetString(body, "category")) ?? material.Category;
        material.Name = CleanText(GetString(body, "name")) ?? material.Name;
        material.Unit = CleanText(GetString(body, "unit")) ?? material.Unit;
        material.Quantity = GetDecimal(body, "quantity", material.Quantity);
        material.UsedQuantity = GetDecimal(body, "usedQuantity", material.UsedQuantity);
        material.RemainingQuantity = Math.Max(0, material.Quantity - material.UsedQuantity);
        material.UnitPrice = GetNullableDecimal(body, "unitPrice") ?? material.UnitPrice;
        material.StageId = CleanText(GetString(body, "linkedStageId")) ?? material.StageId;
        material.WorkItemId = CleanText(GetString(body, "linkedWorkItemId")) ?? material.WorkItemId;
        material.DeliveryDate = GetDate(body, "deliveryDate") ?? material.DeliveryDate;
        material.Supplier = CleanText(GetString(body, "supplier")) ?? material.Supplier;
        material.Notes = CleanText(GetString(body, "notes")) ?? material.Notes;
        material.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(body, "objectId"), ct) ?? material.SiteId;
        if (string.IsNullOrWhiteSpace(material.WorkItemId) && !string.IsNullOrWhiteSpace(material.StageId))
        {
            material.WorkItemId = $"material-plan:{material.StageId}:{material.Id}";
        }
    }

    private static async Task ApplyMaterialPatchAsync(ProjectWorkItemMaterialRecord material, JsonObject node, BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        material.CatalogItemId = CleanText(GetString(node["catalogItemId"])) ?? material.CatalogItemId;
        material.Category = CleanText(GetString(node["category"])) ?? material.Category;
        material.Name = CleanText(GetString(node["name"])) ?? material.Name;
        material.Unit = CleanText(GetString(node["unit"])) ?? material.Unit;
        material.Quantity = GetDecimal(node["quantity"], material.Quantity);
        material.UsedQuantity = GetDecimal(node["usedQuantity"], material.UsedQuantity);
        material.RemainingQuantity = GetDecimal(node["remainingQuantity"], Math.Max(0, material.Quantity - material.UsedQuantity));
        material.UnitPrice = GetNullableDecimal(node["unitPrice"]) ?? material.UnitPrice;
        material.StageId = CleanText(GetString(node["linkedStageId"])) ?? material.StageId;
        material.WorkItemId = CleanText(GetString(node["linkedWorkItemId"])) ?? material.WorkItemId;
        material.DeliveryDate = GetDate(node["deliveryDate"]) ?? material.DeliveryDate;
        material.Supplier = CleanText(GetString(node["supplier"])) ?? material.Supplier;
        material.Notes = CleanText(GetString(node["notes"])) ?? material.Notes;
        material.SiteId = await ResolveSiteIdAsync(db, tenantId, GetString(node["objectId"]), ct) ?? material.SiteId;
        if (string.IsNullOrWhiteSpace(material.WorkItemId) && !string.IsNullOrWhiteSpace(material.StageId))
        {
            material.WorkItemId = $"material-plan:{material.StageId}:{material.Id}";
        }
    }

    private static async Task UpsertFieldSmetaItemForWorkItemAsync(BuildTrackDbContext db, Guid tenantId, ProjectWorkItemRecord item, CancellationToken ct)
    {
        var stage = await FindProjectStageAsync(db, tenantId, item.StageId, ct);
        var stageName = stage?.Name ?? "Smeta";
        var smetaItem = await FindFieldSmetaByWorkItemAsync(db, tenantId, item.SiteId, item.Id, ct);
        var sameNameRow = await FindActiveFieldSmetaByNameAsync(db, tenantId, item.SiteId, item.Name, ct);
        if (sameNameRow is not null && !string.Equals(sameNameRow.ProjectWorkItemId, item.Id, StringComparison.OrdinalIgnoreCase))
        {
            var linkedCanonicalItem = string.IsNullOrWhiteSpace(sameNameRow.ProjectWorkItemId)
                ? null
                : await FindProjectWorkItemAsync(db, tenantId, sameNameRow.ProjectWorkItemId!, ct);

            if (linkedCanonicalItem is not null && !string.Equals(linkedCanonicalItem.Id, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProjectProgressSmetaSyncException(
                    "FIELD_SMETA_DUPLICATE_WORK_NAME",
                    "Eyni layihə daxilində eyni adlı iki aktiv smeta işi yaradıla bilməz.",
                    [
                        new ProjectProgressSmetaSyncConflict(sameNameRow.Id, null, item.SiteId, item.Name, item.Id, sameNameRow.ProjectWorkItemId, "CanonicalWorkItemNameCollision"),
                    ]);
            }

            smetaItem ??= sameNameRow;
            smetaItem.ProjectWorkItemId = item.Id;
        }

        if (smetaItem is null)
        {
            smetaItem = new FieldSmetaItem
            {
                TenantId = tenantId,
                SiteId = item.SiteId,
                ProjectWorkItemId = item.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.FieldSmetaItems.Add(smetaItem);
        }

        smetaItem.StageName = stageName;
        smetaItem.WorkName = item.Name;
        smetaItem.Unit = item.Unit;
        smetaItem.WorkCategory = item.Notes;
        smetaItem.PlannedQuantity = item.Quantity;
        smetaItem.IsActive = item.IsActive;
        smetaItem.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task RecalculateStageAsync(BuildTrackDbContext db, Guid tenantId, string stageId, CancellationToken ct)
    {
        var stage = await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == stageId, ct);
        if (stage is null) return;
        var items = await db.ProjectWorkItems
            .Where(x => x.TenantId == tenantId && x.StageId == stageId && x.IsActive)
            .ToArrayAsync(ct);
        if (items.Length == 0) return;

        stage.LaborCost = Math.Round(items.Sum(x => x.LaborTotal), 2);
        stage.MaterialCost = Math.Round(items.Sum(x => x.MaterialTotal), 2);
        stage.TotalCost = Math.Round(items.Sum(x => x.TotalCost), 2);
        stage.PlannedHours = Math.Round(items.Sum(x => x.PlannedHours), 2);
        stage.ActualHours = Math.Round(items.Sum(x => x.ActualHours), 2);

        var costWeighted = items.Where(x => x.TotalCost > 0).ToArray();
        var totalCost = costWeighted.Sum(x => x.TotalCost);
        if (totalCost > 0)
        {
            stage.ProgressPercent = ClampPercent(Math.Round(costWeighted.Sum(x => x.TotalCost * x.ProgressPercent) / totalCost, 1));
        }
        else if (stage.PlannedHours > 0)
        {
            stage.ProgressPercent = ClampPercent(Math.Round(items.Sum(x => x.PlannedHours * x.ProgressPercent) / stage.PlannedHours, 1));
        }

        stage.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid?> ResolveSiteIdAsync(BuildTrackDbContext db, Guid tenantId, string? objectId, CancellationToken ct)
    {
        if (Guid.TryParse(objectId, out var siteId)
            && await db.Sites.AnyAsync(x => x.TenantId == tenantId && x.Id == siteId, ct))
        {
            return siteId;
        }

        return await db.Sites
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Guid> ResolveRequiredSiteIdAsync(BuildTrackDbContext db, Guid tenantId, string? objectId, CancellationToken ct)
    {
        var siteId = await ResolveSiteIdAsync(db, tenantId, objectId, ct);
        if (siteId is { } value) return value;
        throw new InvalidOperationException("Tenant site is required for project progress mutation.");
    }

    private static ProjectEntityStatus ParseStatus(string? value, ProjectEntityStatus fallback) =>
        Enum.TryParse<ProjectEntityStatus>(value, ignoreCase: true, out var parsed) && parsed != ProjectEntityStatus.Archived
            ? parsed
            : fallback;

    private static ProjectEntityStatus? ParseNullableStatus(string? value) =>
        Enum.TryParse<ProjectEntityStatus>(value, ignoreCase: true, out var parsed) && parsed != ProjectEntityStatus.Archived ? parsed : null;

    private static decimal ClampPercent(decimal value) => Math.Max(0, Math.Min(100, Math.Round(value, 1)));

    private static string? FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd");

    private static string? CleanText(string? value)
    {
        var cleaned = value?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string? GetString(JsonElement body, string propertyName) =>
        body.TryGetProperty(propertyName, out var value) ? GetString(value) : null;

    private static string? GetString(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return value.GetRawText();
        return null;
    }

    private static decimal GetDecimal(JsonElement body, string propertyName, decimal fallback = 0) =>
        body.TryGetProperty(propertyName, out var value) ? GetDecimal(value, fallback) : fallback;

    private static decimal GetDecimal(JsonElement value, decimal fallback = 0)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return fallback;
    }

    private static decimal? GetNullableDecimal(JsonElement body, string propertyName) =>
        body.TryGetProperty(propertyName, out var value) ? GetNullableDecimal(value) : null;

    private static decimal? GetNullableDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static int GetInt(JsonElement body, string propertyName, int fallback = 0) =>
        body.TryGetProperty(propertyName, out var value) ? GetInt(value, fallback) : fallback;

    private static int GetInt(JsonElement value, int fallback = 0)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return fallback;
    }

    private static DateOnly? GetDate(JsonElement body, string propertyName) =>
        body.TryGetProperty(propertyName, out var value) ? GetDate(value) : null;

    private static DateOnly? GetDate(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) return null;
        return DateOnly.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static decimal GetDecimal(JsonNode? node, decimal fallback = 0)
    {
        if (node is null) return fallback;
        if (decimal.TryParse(node.ToJsonString().Trim('"'), out var parsed)) return parsed;
        return fallback;
    }

    private static decimal? GetNullableDecimal(JsonNode? node)
    {
        if (node is null) return null;
        return decimal.TryParse(node.ToJsonString().Trim('"'), out var parsed) ? parsed : null;
    }

    private static int GetInt(JsonNode? node, int fallback = 0)
    {
        if (node is null) return fallback;
        return int.TryParse(node.ToJsonString().Trim('"'), out var parsed) ? parsed : fallback;
    }

    private static DateOnly? GetDate(JsonNode? node)
    {
        var value = GetString(node);
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }

    private static async Task<string> BuildWorkspaceFromCanonicalTablesAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        var sites = await db.Sites.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToArrayAsync(ct);
        var workers = await db.Workers.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.FullName).ToArrayAsync(ct);
        var projects = await db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);

        if (projects.Length == 0)
        {
            var fallbackProjectId = tenantId.ToString();
            var fallbackEstimateId = $"{tenantId:N}-estimate";
            var fallbackProject = new
            {
                id = fallbackProjectId,
                name = tenant?.CompanyName ?? "BuildTrack layihəsi",
                currency = "AZN",
                location = sites.FirstOrDefault()?.Address,
                clientName = tenant?.CompanyName,
                createdAt = DateTimeOffset.UtcNow.ToString("O"),
                activeEstimateVersionId = fallbackEstimateId,
            };

            return JsonSerializer.Serialize(new
            {
                workspaceTenantId = tenantId.ToString(),
                projects = new[] { fallbackProject },
                activeProjectId = fallbackProjectId,
                objects = sites.Select(site => new
                {
                    id = site.Id.ToString(),
                    name = site.Name,
                    zone = site.Address,
                    address = site.Address,
                    projectId = fallbackProjectId,
                    status = "NotStarted",
                    plannedStartDate = site.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd"),
                    clientName = tenant?.CompanyName,
                }).ToArray(),
                project = fallbackProject,
                estimateVersions = new[] { new { id = fallbackEstimateId, projectId = fallbackProjectId, name = "Cari smeta", createdAt = DateTimeOffset.UtcNow.ToString("O"), totalAmount = 0, notes = "Server workspace" } },
                summary = EmptySummary(),
                stages = Array.Empty<object>(),
                workItems = Array.Empty<object>(),
                crews = Array.Empty<object>(),
                workerAssignments = Array.Empty<object>(),
                materials = Array.Empty<object>(),
                attendanceSessions = Array.Empty<object>(),
                workHourAllocations = Array.Empty<object>(),
                dailyReports = Array.Empty<object>(),
                issues = Array.Empty<object>(),
                risks = Array.Empty<object>(),
                assistantMessages = Array.Empty<object>(),
            }, JsonOptions);
        }

        var activeProject = projects.First();
        var estimates = await db.ProjectEstimateVersions.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        var projectSites = await db.ProjectSites.AsNoTracking().Include(x => x.Site).Where(x => x.TenantId == tenantId).OrderBy(x => x.Site!.Name).ToArrayAsync(ct);
        var stages = await db.ProjectStages.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.Order).ThenBy(x => x.Name).ToArrayAsync(ct);
        var workItems = await db.ProjectWorkItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.StageId).ThenBy(x => x.Name).ToArrayAsync(ct);
        var crews = await db.ProjectCrews.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.Name).ToArrayAsync(ct);
        var materials = await db.ProjectWorkItemMaterials.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.Name).ToArrayAsync(ct);
        var summary = new
        {
            totalAmount = Math.Round(workItems.Sum(x => x.TotalCost), 2),
            laborAmount = Math.Round(workItems.Sum(x => x.LaborTotal), 2),
            materialAmount = Math.Round(workItems.Sum(x => x.MaterialTotal), 2),
            hiddenCostAmount = 0,
            currency = "AZN",
        };

        var data = new
        {
            workspaceTenantId = tenantId.ToString(),
            projects = projects.Select(MapProject).ToArray(),
            activeProjectId = activeProject.Id,
            objects = projectSites
                .Where(projectSite => projectSite.Site is not null)
                .Select(projectSite => new
                {
                    id = projectSite.SiteId.ToString(),
                    name = projectSite.Site!.Name,
                    zone = projectSite.Zone ?? projectSite.Site.Address,
                    address = projectSite.Site.Address,
                    projectId = projectSite.ProjectId,
                    status = projectSite.Status == ProjectEntityStatus.Archived ? "NotStarted" : projectSite.Status.ToString(),
                    plannedStartDate = FormatDate(projectSite.PlannedStartDate),
                    plannedEndDate = FormatDate(projectSite.PlannedEndDate),
                    clientName = tenant?.CompanyName,
                    notes = projectSite.Notes,
                }).ToArray(),
            project = MapProject(activeProject),
            estimateVersions = estimates.Select(estimate => new
            {
                id = estimate.Id,
                projectId = estimate.ProjectId,
                name = estimate.Name,
                createdAt = estimate.CreatedAt.ToString("O"),
                totalAmount = estimate.TotalAmount,
                notes = estimate.Notes,
            }).ToArray(),
            summary,
            stages = stages.Select(MapStage).ToArray(),
            workItems = workItems.Select(MapWorkItem).ToArray(),
            crews = crews.Select(MapCrew).ToArray(),
            workerAssignments = workers.Select(worker => new
            {
                id = worker.Id.ToString(),
                workerName = worker.FullName,
                workerExternalId = worker.ExternalWorkerCode,
                projectId = activeProject.Id,
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
            materials = materials.Select(MapMaterial).ToArray(),
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

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
