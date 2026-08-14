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
        if (root?["workItems"] is not JsonArray workItems) return;

        var stageNamesById = BuildStageNamesById(root["stages"] as JsonArray);
        var incomingRows = BuildIncomingWorkItems(workItems, stageNamesById);
        var existing = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        if (incomingRows.Count == 0)
        {
            DeactivateMissingProjectProgressRows(existing, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DateTimeOffset.UtcNow);
            return;
        }

        var snapshots = existing.Select(ExistingFieldSmetaSnapshot.FromEntity).ToArray();
        var incomingProjectWorkItemIds = incomingRows.Select(x => x.ProjectWorkItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plans = BuildReconciliationPlan(incomingRows, snapshots, incomingProjectWorkItemIds);
        ValidateFinalUniqueKeys(snapshots, plans);

        var existingById = existing.ToDictionary(x => x.Id);
        var now = DateTimeOffset.UtcNow;
        foreach (var plan in plans)
        {
            var item = plan.ExistingFieldSmetaItemId is Guid existingId
                ? existingById[existingId]
                : new FieldSmetaItem
                {
                    TenantId = tenantId,
                    CreatedAt = now,
                };

            if (plan.ExistingFieldSmetaItemId is null)
            {
                db.FieldSmetaItems.Add(item);
            }

            item.TenantId = tenantId;
            item.ProjectWorkItemId = plan.Incoming.ProjectWorkItemId;
            item.SiteId = plan.Incoming.SiteId;
            item.StageName = plan.Incoming.StageName;
            item.WorkName = plan.Incoming.WorkName;
            item.Unit = plan.Incoming.Unit;
            item.WorkCategory = plan.Incoming.WorkCategory;
            item.PlannedQuantity = plan.Incoming.PlannedQuantity;
            item.IsActive = true;
            item.UpdatedAt = now;
        }

        var touchedIds = plans.Select(x => x.ExistingFieldSmetaItemId).OfType<Guid>().ToHashSet();
        DeactivateMissingProjectProgressRows(existing.Where(x => !touchedIds.Contains(x.Id)), incomingProjectWorkItemIds, now);
    }

    public async Task<ProjectProgressApprovalValidationResult> ValidateApprovedReportAsync(
        Guid tenantId,
        SupervisorDailyReport report,
        CancellationToken cancellationToken)
    {
        var plannedByWorkItemId = await db.ProjectWorkItems
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (plannedByWorkItemId.Count == 0)
        {
            var workspace = await db.ProjectProgressWorkspaces.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            if (workspace is null)
            {
                return new ProjectProgressApprovalValidationResult(false, "Project progress workspace was not found.", null, 0, 0);
            }

            var root = JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject();
            plannedByWorkItemId = BuildPlannedQuantityMap(root["workItems"] as JsonArray);
        }

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
        var approvedLines = await LoadApprovedLinesAsync(tenantId, cancellationToken);
        var approvedQuantityByWorkItemId = approvedLines
            .GroupBy(x => x.ProjectWorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.ReportedQuantity), StringComparer.OrdinalIgnoreCase);
        var approvedHoursByWorkItemId = approvedLines
            .GroupBy(x => x.ProjectWorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.WorkHours ?? 0), StringComparer.OrdinalIgnoreCase);

        var updatedWorkItems = 0;
        var updatedStages = 0;
        var canonicalItems = await db.ProjectWorkItems
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToArrayAsync(cancellationToken);
        if (canonicalItems.Length > 0)
        {
            foreach (var item in canonicalItems)
            {
                var completedQuantity = approvedQuantityByWorkItemId.GetValueOrDefault(item.Id);
                var actualHours = approvedHoursByWorkItemId.GetValueOrDefault(item.Id);
                var progress = item.Quantity > 0
                    ? Math.Min(100m, Math.Round(completedQuantity / item.Quantity * 100m, 1))
                    : 0;

                item.CompletedQuantity = Math.Round(completedQuantity, 3);
                item.ActualHours = Math.Round(actualHours, 2);
                item.ProgressPercent = progress;
                item.Status = progress >= 100 ? ProjectEntityStatus.Completed : progress > 0 ? ProjectEntityStatus.InProgress : ProjectEntityStatus.NotStarted;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                updatedWorkItems++;
            }

            var stageIds = canonicalItems.Select(x => x.StageId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var stages = await db.ProjectStages.Where(x => x.TenantId == tenantId && stageIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
            foreach (var stage in stages)
            {
                var stageItems = canonicalItems.Where(x => string.Equals(x.StageId, stage.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (stageItems.Length == 0) continue;
                stage.LaborCost = Math.Round(stageItems.Sum(x => x.LaborTotal), 2);
                stage.MaterialCost = Math.Round(stageItems.Sum(x => x.MaterialTotal), 2);
                stage.TotalCost = Math.Round(stageItems.Sum(x => x.TotalCost), 2);
                stage.PlannedHours = Math.Round(stageItems.Sum(x => x.PlannedHours), 2);
                stage.ActualHours = Math.Round(stageItems.Sum(x => x.ActualHours), 2);
                var costWeighted = stageItems.Where(x => x.TotalCost > 0).ToArray();
                var totalCost = costWeighted.Sum(x => x.TotalCost);
                stage.ProgressPercent = totalCost > 0
                    ? Math.Min(100m, Math.Round(costWeighted.Sum(x => x.TotalCost * x.ProgressPercent) / totalCost, 1))
                    : stage.PlannedHours > 0
                        ? Math.Min(100m, Math.Round(stageItems.Sum(x => x.PlannedHours * x.ProgressPercent) / stage.PlannedHours, 1))
                        : stage.ProgressPercent;
                stage.Status = stage.ProgressPercent >= 100 ? ProjectEntityStatus.Completed : stage.ProgressPercent > 0 ? ProjectEntityStatus.InProgress : ProjectEntityStatus.NotStarted;
                stage.UpdatedAt = DateTimeOffset.UtcNow;
                updatedStages++;
            }
        }

        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var root = workspace is not null ? JsonNode.Parse(workspace.WorkspaceJson)?.AsObject() ?? new JsonObject() : null;
        if (root?["workItems"] is not JsonArray workItems)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ProjectProgressRecalculationResult(
                updatedWorkItems,
                updatedStages,
                approvedQuantityByWorkItemId.Values.Sum(),
                sourceReportId);
        }

        updatedWorkItems = 0;
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

        updatedStages = RecalculateStages(root["stages"] as JsonArray, workItems);
        root["workspaceTenantId"] = tenantId.ToString();
        workspace!.WorkspaceJson = root.ToJsonString(JsonOptions);
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new ProjectProgressRecalculationResult(
            updatedWorkItems,
            updatedStages,
            approvedQuantityByWorkItemId.Values.Sum(),
            sourceReportId);
    }

    private static IReadOnlyList<IncomingWorkspaceWorkItem> BuildIncomingWorkItems(JsonArray workItems, IReadOnlyDictionary<string, string> stageNamesById)
    {
        var rows = new List<IncomingWorkspaceWorkItem>();
        foreach (var node in workItems)
        {
            if (node is not JsonObject workItem) continue;
            var projectWorkItemId = CleanText(GetString(workItem["id"]));
            var siteIdText = CleanText(GetString(workItem["objectId"]));
            var workName = CleanText(GetString(workItem["name"]));
            if (string.IsNullOrWhiteSpace(projectWorkItemId)
                || string.IsNullOrWhiteSpace(siteIdText)
                || !Guid.TryParse(siteIdText, out var siteId)
                || string.IsNullOrWhiteSpace(workName))
            {
                continue;
            }

            var stageId = CleanText(GetString(workItem["stageId"]));
            var stageName = !string.IsNullOrWhiteSpace(stageId) && stageNamesById.TryGetValue(stageId, out var mappedStageName)
                ? mappedStageName
                : CleanText(GetString(workItem["stageName"])) ?? "Smeta";

            rows.Add(new IncomingWorkspaceWorkItem(
                projectWorkItemId,
                siteId,
                workName,
                NormalizeWorkName(workName),
                stageName,
                CleanText(GetString(workItem["unit"])) ?? string.Empty,
                CleanText(GetString(workItem["notes"])) ?? CleanText(GetString(workItem["category"])),
                GetDecimal(workItem["quantity"])));
        }

        var duplicate = rows
            .GroupBy(x => x.NormalizedSiteWorkKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Select(x => x.ProjectWorkItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
        if (duplicate is not null)
        {
            var first = duplicate.First();
            throw new ProjectProgressSmetaSyncException(
                "FIELD_SMETA_DUPLICATE_WORK_NAME",
                $"Eyni layihə daxilində eyni adlı iki aktiv smeta işi yaradıla bilməz: {first.WorkName}.",
                duplicate.Select(row => new ProjectProgressSmetaSyncConflict(
                    null,
                    null,
                    row.SiteId,
                    row.WorkName,
                    row.ProjectWorkItemId,
                    null,
                    "IncomingWorkspaceDuplicate")).ToArray());
        }

        return rows;
    }

    private static IReadOnlyList<FieldSmetaReconciliationPlan> BuildReconciliationPlan(
        IReadOnlyList<IncomingWorkspaceWorkItem> incomingRows,
        IReadOnlyList<ExistingFieldSmetaSnapshot> snapshots,
        IReadOnlySet<string> incomingProjectWorkItemIds)
    {
        var plans = new List<FieldSmetaReconciliationPlan>();
        var plannedExistingIds = new Dictionary<Guid, IncomingWorkspaceWorkItem>();

        foreach (var incoming in incomingRows)
        {
            var sameProject = snapshots.FirstOrDefault(x => string.Equals(x.ProjectWorkItemId, incoming.ProjectWorkItemId, StringComparison.OrdinalIgnoreCase));
            var sameExactSiteWork = snapshots.FirstOrDefault(x => string.Equals(x.ExactSiteWorkKey, incoming.ExactSiteWorkKey, StringComparison.Ordinal));
            var sameNormalizedSiteWork = snapshots.FirstOrDefault(x => string.Equals(x.NormalizedSiteWorkKey, incoming.NormalizedSiteWorkKey, StringComparison.OrdinalIgnoreCase));
            ExistingFieldSmetaSnapshot? target = null;

            if (sameProject is not null)
            {
                target = sameProject;
                var nameOccupier = sameExactSiteWork ?? sameNormalizedSiteWork;
                if (nameOccupier is not null && nameOccupier.Id != target.Id)
                {
                    throw BuildIdentityConflict(target, nameOccupier, incoming, "RenameOrMoveWouldCollide");
                }
            }
            else
            {
                var adoptCandidate = sameExactSiteWork ?? sameNormalizedSiteWork;
                if (adoptCandidate is not null)
                {
                    if (!string.IsNullOrWhiteSpace(adoptCandidate.ProjectWorkItemId)
                        && incomingProjectWorkItemIds.Contains(adoptCandidate.ProjectWorkItemId))
                    {
                        throw BuildIdentityConflict(adoptCandidate, adoptCandidate, incoming, "SiteWorkNameBelongsToAnotherActiveWorkItem");
                    }

                    target = adoptCandidate;
                }
            }

            if (target is not null)
            {
                if (plannedExistingIds.TryGetValue(target.Id, out var previousIncoming)
                    && !string.Equals(previousIncoming.ProjectWorkItemId, incoming.ProjectWorkItemId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProjectProgressSmetaSyncException(
                        "FIELD_SMETA_IDENTITY_CONFLICT",
                        "Smeta sinxronizasiyası zamanı eyni Field Smeta sətri iki fərqli ProjectProgress işi ilə uyğunlaşdı.",
                        [
                            new ProjectProgressSmetaSyncConflict(
                                target.Id,
                                target.Id,
                                incoming.SiteId,
                                incoming.WorkName,
                                incoming.ProjectWorkItemId,
                                previousIncoming.ProjectWorkItemId,
                                "DuplicateTargetResolution"),
                        ]);
                }

                plannedExistingIds[target.Id] = incoming;
            }

            plans.Add(new FieldSmetaReconciliationPlan(target?.Id, incoming));
        }

        return plans;
    }

    private static void ValidateFinalUniqueKeys(
        IReadOnlyList<ExistingFieldSmetaSnapshot> snapshots,
        IReadOnlyList<FieldSmetaReconciliationPlan> plans)
    {
        var plansByExistingId = plans
            .Where(x => x.ExistingFieldSmetaItemId is not null)
            .ToDictionary(x => x.ExistingFieldSmetaItemId!.Value);
        var finalOwnersByExactKey = new Dictionary<string, FinalFieldSmetaOwner>(StringComparer.Ordinal);

        foreach (var snapshot in snapshots)
        {
            var planned = plansByExistingId.GetValueOrDefault(snapshot.Id);
            var exactKey = planned?.Incoming.ExactSiteWorkKey ?? snapshot.ExactSiteWorkKey;
            AddFinalOwner(finalOwnersByExactKey, exactKey, new FinalFieldSmetaOwner(snapshot.Id, planned?.Incoming.ProjectWorkItemId ?? snapshot.ProjectWorkItemId, snapshot.SiteId, planned?.Incoming.WorkName ?? snapshot.WorkName));
        }

        var createIndex = 0;
        foreach (var plan in plans.Where(x => x.ExistingFieldSmetaItemId is null))
        {
            AddFinalOwner(finalOwnersByExactKey, plan.Incoming.ExactSiteWorkKey, new FinalFieldSmetaOwner(Guid.Empty, plan.Incoming.ProjectWorkItemId, plan.Incoming.SiteId, plan.Incoming.WorkName, $"new-{++createIndex}"));
        }
    }

    private static void AddFinalOwner(Dictionary<string, FinalFieldSmetaOwner> owners, string key, FinalFieldSmetaOwner owner)
    {
        if (!owners.TryGetValue(key, out var existing))
        {
            owners[key] = owner;
            return;
        }

        var samePersistedRow = owner.Id != Guid.Empty && owner.Id == existing.Id;
        var sameNewRow = owner.NewRowKey is not null && owner.NewRowKey == existing.NewRowKey;
        if (samePersistedRow || sameNewRow) return;

        throw new ProjectProgressSmetaSyncException(
            "FIELD_SMETA_IDENTITY_CONFLICT",
            "Smeta sinxronizasiyası zamanı eyni layihə və iş adı üzrə konflikt aşkarlandı.",
            [
                new ProjectProgressSmetaSyncConflict(
                    existing.Id == Guid.Empty ? null : existing.Id,
                    owner.Id == Guid.Empty ? null : owner.Id,
                    owner.SiteId,
                    owner.WorkName,
                    owner.ProjectWorkItemId,
                    existing.ProjectWorkItemId,
                    "FinalUniqueKeyCollision"),
            ]);
    }

    private static ProjectProgressSmetaSyncException BuildIdentityConflict(
        ExistingFieldSmetaSnapshot target,
        ExistingFieldSmetaSnapshot occupier,
        IncomingWorkspaceWorkItem incoming,
        string reason) =>
        new(
            "FIELD_SMETA_IDENTITY_CONFLICT",
            "Smeta sinxronizasiyası zamanı eyni layihə və iş adı üzrə konflikt aşkarlandı.",
            [
                new ProjectProgressSmetaSyncConflict(
                    target.Id,
                    occupier.Id,
                    incoming.SiteId,
                    incoming.WorkName,
                    incoming.ProjectWorkItemId,
                    occupier.ProjectWorkItemId,
                    reason),
            ]);

    private static void DeactivateMissingProjectProgressRows(
        IEnumerable<FieldSmetaItem> existing,
        IReadOnlySet<string> incomingProjectWorkItemIds,
        DateTimeOffset now)
    {
        foreach (var item in existing)
        {
            if (!string.IsNullOrWhiteSpace(item.ProjectWorkItemId)
                && !incomingProjectWorkItemIds.Contains(item.ProjectWorkItemId)
                && item.IsActive)
            {
                item.IsActive = false;
                item.UpdatedAt = now;
            }
        }
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

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeWorkName(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private sealed record IncomingWorkspaceWorkItem(
        string ProjectWorkItemId,
        Guid SiteId,
        string WorkName,
        string NormalizedWorkName,
        string StageName,
        string Unit,
        string? WorkCategory,
        decimal? PlannedQuantity)
    {
        public string ExactSiteWorkKey => $"{SiteId:N}|{WorkName}";
        public string NormalizedSiteWorkKey => $"{SiteId:N}|{NormalizedWorkName}";
    }

    private sealed record ExistingFieldSmetaSnapshot(
        Guid Id,
        Guid SiteId,
        string WorkName,
        string NormalizedWorkName,
        string? ProjectWorkItemId)
    {
        public string ExactSiteWorkKey => $"{SiteId:N}|{WorkName}";
        public string NormalizedSiteWorkKey => $"{SiteId:N}|{NormalizedWorkName}";

        public static ExistingFieldSmetaSnapshot FromEntity(FieldSmetaItem item)
        {
            var workName = CleanText(item.WorkName) ?? string.Empty;
            return new ExistingFieldSmetaSnapshot(
                item.Id,
                item.SiteId,
                workName,
                NormalizeWorkName(workName),
                CleanText(item.ProjectWorkItemId));
        }
    }

    private sealed record FieldSmetaReconciliationPlan(Guid? ExistingFieldSmetaItemId, IncomingWorkspaceWorkItem Incoming);

    private sealed record FinalFieldSmetaOwner(Guid Id, string? ProjectWorkItemId, Guid SiteId, string WorkName, string? NewRowKey = null);

    private sealed record ApprovedReportLine(Guid ReportId, string ProjectWorkItemId, decimal ReportedQuantity, decimal? WorkHours);
}
