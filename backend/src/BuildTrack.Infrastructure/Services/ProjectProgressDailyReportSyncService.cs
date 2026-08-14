using System.Text.Json;
using System.Text.Json.Nodes;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Services;

public sealed class ProjectProgressDailyReportSyncService(BuildTrackDbContext db) : IProjectProgressDailyReportSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SyncFieldSmetaItemsFromWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (workspace is null) return;

        var root = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject();
        if (root?["workItems"] is not JsonArray workItems || workItems.Count == 0) return;

        var stageNamesById = BuildStageNamesById(root["stages"] as JsonArray);
        var existing = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        var byProjectWorkItemId = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.ProjectWorkItemId))
            .GroupBy(x => x.ProjectWorkItemId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var bySiteAndName = existing
            .GroupBy(x => $"{x.SiteId:N}|{x.WorkName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        foreach (var node in workItems)
        {
            if (node is not JsonObject workItem) continue;
            var projectWorkItemId = GetString(workItem["id"]);
            var siteIdText = GetString(workItem["objectId"]);
            var workName = GetString(workItem["name"]);
            if (string.IsNullOrWhiteSpace(projectWorkItemId)
                || string.IsNullOrWhiteSpace(siteIdText)
                || !Guid.TryParse(siteIdText, out var siteId)
                || string.IsNullOrWhiteSpace(workName))
            {
                continue;
            }

            var siteAndNameKey = $"{siteId:N}|{workName}";
            if (!byProjectWorkItemId.TryGetValue(projectWorkItemId, out var item)
                && !bySiteAndName.TryGetValue(siteAndNameKey, out item))
            {
                item = new FieldSmetaItem
                {
                    TenantId = tenantId,
                    SiteId = siteId,
                    CreatedAt = now,
                    IsActive = true,
                };
                db.FieldSmetaItems.Add(item);
                byProjectWorkItemId[projectWorkItemId] = item;
                bySiteAndName[siteAndNameKey] = item;
            }

            var stageId = GetString(workItem["stageId"]);
            item.ProjectWorkItemId = projectWorkItemId;
            item.SiteId = siteId;
            item.StageName = !string.IsNullOrWhiteSpace(stageId) && stageNamesById.TryGetValue(stageId, out var stageName)
                ? stageName
                : GetString(workItem["stageName"]) ?? "Smeta";
            item.WorkName = workName;
            item.Unit = GetString(workItem["unit"]) ?? item.Unit;
            item.WorkCategory = GetString(workItem["notes"]) ?? GetString(workItem["category"]) ?? item.WorkCategory;
            item.PlannedQuantity = GetDecimal(workItem["quantity"]);
            item.IsActive = true;
            item.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectProgressApprovalValidationResult> ValidateApprovedReportAsync(
        Guid tenantId,
        SupervisorDailyReport report,
        CancellationToken cancellationToken)
    {
        var workspace = await db.ProjectProgressWorkspaces.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (workspace is null)
        {
            return new ProjectProgressApprovalValidationResult(false, "Project progress workspace was not found.", null, 0, 0);
        }

        var root = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject();
        var plannedByWorkItemId = BuildPlannedQuantityMap(root["workItems"] as JsonArray);
        var existingApproved = await LoadApprovedLineTotalsAsync(tenantId, excludeReportId: report.Id, cancellationToken);
        var currentReportTotals = BuildReportLineTotals(report.Lines);

        foreach (var (workItemId, currentQuantity) in currentReportTotals)
        {
            var planned = plannedByWorkItemId.GetValueOrDefault(workItemId);
            if (planned <= 0) continue;

            var candidate = existingApproved.GetValueOrDefault(workItemId) + currentQuantity;
            if (candidate > planned)
            {
                return new ProjectProgressApprovalValidationResult(
                    false,
                    $"Approved quantity {candidate:0.###} exceeds planned quantity {planned:0.###} for work item {workItemId}.",
                    workItemId,
                    planned,
                    candidate);
            }
        }

        return new ProjectProgressApprovalValidationResult(true, null, null, 0, 0);
    }

    public async Task<ProjectProgressRecalculationResult> RecalculateApprovedDailyReportProgressAsync(
        Guid tenantId,
        Guid? sourceReportId,
        CancellationToken cancellationToken)
    {
        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (workspace is null)
        {
            return new ProjectProgressRecalculationResult(0, 0, 0, sourceReportId);
        }

        var root = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject();
        if (root["workItems"] is not JsonArray workItems)
        {
            return new ProjectProgressRecalculationResult(0, 0, 0, sourceReportId);
        }

        var approvedLines = await LoadApprovedLinesAsync(tenantId, cancellationToken);
        var approvedQuantityByWorkItemId = approvedLines
            .GroupBy(x => x.ProjectWorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.ReportedQuantity), StringComparer.OrdinalIgnoreCase);
        var approvedHoursByWorkItemId = approvedLines
            .GroupBy(x => x.ProjectWorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.WorkHours ?? 0), StringComparer.OrdinalIgnoreCase);

        var updatedWorkItems = 0;
        foreach (var node in workItems)
        {
            if (node is not JsonObject workItem) continue;
            var workItemId = GetString(workItem["id"]);
            if (string.IsNullOrWhiteSpace(workItemId)) continue;

            var plannedQuantity = GetDecimal(workItem["quantity"]) ?? 0;
            var completedQuantity = approvedQuantityByWorkItemId.GetValueOrDefault(workItemId);
            var actualHours = approvedHoursByWorkItemId.GetValueOrDefault(workItemId);
            var progress = plannedQuantity > 0
                ? Math.Min(100m, Math.Round(completedQuantity / plannedQuantity * 100m, 1))
                : 0;

            workItem["completedQuantity"] = Math.Round(completedQuantity, 3);
            workItem["actualHours"] = Math.Round(actualHours, 2);
            workItem["progressPercent"] = progress;
            workItem["status"] = progress >= 100 ? "Completed" : progress > 0 ? "InProgress" : "NotStarted";
            updatedWorkItems++;
        }

        var updatedStages = RecalculateStages(root["stages"] as JsonArray, workItems);
        root["workspaceTenantId"] = tenantId.ToString();
        workspace.WorkspaceJson = root.ToJsonString(JsonOptions);
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new ProjectProgressRecalculationResult(
            updatedWorkItems,
            updatedStages,
            approvedQuantityByWorkItemId.Values.Sum(),
            sourceReportId);
    }

    private async Task<Dictionary<string, decimal>> LoadApprovedLineTotalsAsync(Guid tenantId, Guid excludeReportId, CancellationToken cancellationToken)
    {
        var approvedLines = await LoadApprovedLinesAsync(tenantId, cancellationToken);
        return approvedLines
            .Where(x => x.ReportId != excludeReportId)
            .GroupBy(x => x.ProjectWorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.ReportedQuantity), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<ApprovedReportLine>> LoadApprovedLinesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var lines = await db.SupervisorDailyReportLines
            .AsNoTracking()
            .Include(x => x.Report)
            .Include(x => x.SmetaItem)
            .Where(x => x.TenantId == tenantId && x.Report != null && x.Report.Status == FieldDailyReportStatus.Approved)
            .Select(x => new
            {
                x.ReportId,
                ProjectWorkItemId = x.ProjectWorkItemId ?? x.SmetaItem!.ProjectWorkItemId,
                x.ReportedQuantity,
                x.WorkHours,
            })
            .ToListAsync(cancellationToken);

        return lines
            .Where(x => !string.IsNullOrWhiteSpace(x.ProjectWorkItemId))
            .Select(x => new ApprovedReportLine(x.ReportId, x.ProjectWorkItemId!, x.ReportedQuantity, x.WorkHours))
            .ToArray();
    }

    private static Dictionary<string, decimal> BuildReportLineTotals(IEnumerable<SupervisorDailyReportLine> lines) =>
        lines
            .Select(line => new
            {
                WorkItemId = line.ProjectWorkItemId ?? line.SmetaItem?.ProjectWorkItemId,
                line.ReportedQuantity,
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.WorkItemId))
            .GroupBy(x => x.WorkItemId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.ReportedQuantity), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> BuildStageNamesById(JsonArray? stages)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (stages is null) return map;
        foreach (var node in stages)
        {
            if (node is not JsonObject stage) continue;
            var id = GetString(stage["id"]);
            var name = GetString(stage["name"]);
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name)) map[id] = name;
        }

        return map;
    }

    private static Dictionary<string, decimal> BuildPlannedQuantityMap(JsonArray? workItems)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (workItems is null) return map;
        foreach (var node in workItems)
        {
            if (node is not JsonObject workItem) continue;
            var id = GetString(workItem["id"]);
            if (string.IsNullOrWhiteSpace(id)) continue;
            map[id] = GetDecimal(workItem["quantity"]) ?? 0;
        }

        return map;
    }

    private static int RecalculateStages(JsonArray? stages, JsonArray workItems)
    {
        if (stages is null) return 0;
        var workItemsByStage = workItems
            .OfType<JsonObject>()
            .GroupBy(x => GetString(x["stageId"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        foreach (var node in stages)
        {
            if (node is not JsonObject stage) continue;
            var stageId = GetString(stage["id"]);
            if (string.IsNullOrWhiteSpace(stageId) || !workItemsByStage.TryGetValue(stageId, out var stageItems) || stageItems.Length == 0) continue;

            var weightedNumerator = 0m;
            var weightedDenominator = 0m;
            var actualHours = 0m;
            foreach (var item in stageItems)
            {
                var progress = GetDecimal(item["progressPercent"]) ?? 0;
                var weight = GetDecimal(item["totalCost"]) ?? 0;
                if (weight <= 0) weight = GetDecimal(item["quantity"]) ?? 0;
                if (weight <= 0) weight = 1;
                weightedNumerator += progress * weight;
                weightedDenominator += weight;
                actualHours += GetDecimal(item["actualHours"]) ?? 0;
            }

            var stageProgress = weightedDenominator > 0 ? Math.Round(weightedNumerator / weightedDenominator, 1) : 0;
            stage["progressPercent"] = stageProgress;
            stage["actualHours"] = Math.Round(actualHours, 2);
            stage["status"] = stageProgress >= 100 ? "Completed" : stageProgress > 0 ? "InProgress" : "NotStarted";
            updated++;
        }

        return updated;
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

    private static decimal? GetDecimal(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            return node.GetValue<decimal>();
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

    private sealed record ApprovedReportLine(Guid ReportId, string ProjectWorkItemId, decimal ReportedQuantity, decimal? WorkHours);
}
