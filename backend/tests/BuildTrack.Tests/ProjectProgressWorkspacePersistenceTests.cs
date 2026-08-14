using System.Text.Json;
using BuildTrack.Api;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildTrack.Tests;

public sealed class ProjectProgressWorkspacePersistenceTests
{
    [Fact]
    public async Task WorkspaceSavedByOneSessionIsLoadedByFreshSessionWithoutBrowserState()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString("N");
        var root = new InMemoryDatabaseRoot();

        await using (var sessionA = CreateDb(databaseName, root, tenantId))
        {
            sessionA.Tenants.Add(new Tenant { Id = tenantId, Code = "TENANT-A", CompanyName = "Tenant A" });
            var body = CreateWorkspaceJson(tenantId.ToString(), objectId: "object-a", stageId: "stage-a", workItemId: "item-a");
            sessionA.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
            {
                TenantId = tenantId,
                WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(body, tenantId),
            });
            await sessionA.SaveChangesAsync();
        }

        await using (var sessionB = CreateDb(databaseName, root, tenantId))
        {
            var workspace = await sessionB.ProjectProgressWorkspaces.SingleAsync();
            using var document = JsonDocument.Parse(workspace.WorkspaceJson);

            Assert.Equal(tenantId.ToString(), document.RootElement.GetProperty("workspaceTenantId").GetString());
            Assert.Equal("object-a", document.RootElement.GetProperty("objects")[0].GetProperty("id").GetString());
            Assert.Equal("stage-a", document.RootElement.GetProperty("stages")[0].GetProperty("id").GetString());
            Assert.Equal("item-a", document.RootElement.GetProperty("workItems")[0].GetProperty("id").GetString());
        }
    }

    [Fact]
    public void NormalWorkspaceSaveRejectsCrossTenantPayload()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var body = CreateWorkspaceJson(tenantA.ToString(), objectId: "object-a", stageId: "stage-a", workItemId: "item-a");

        var error = ProjectProgressEndpoints.ValidateWorkspaceTenant(body, tenantB, allowLegacyImport: false);

        Assert.Equal("Workspace tenant does not match authenticated tenant", error);
    }

    [Fact]
    public void LegacyImportAllowsAndNormalizesOldWorkspaceTenant()
    {
        var tenantId = Guid.NewGuid();
        var body = CreateWorkspaceJson("legacy-browser", objectId: "object-legacy", stageId: "stage-legacy", workItemId: "item-legacy");

        Assert.Null(ProjectProgressEndpoints.ValidateWorkspaceTenant(body, tenantId, allowLegacyImport: true));

        var normalized = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(body, tenantId);
        using var document = JsonDocument.Parse(normalized);

        Assert.Equal(tenantId.ToString(), document.RootElement.GetProperty("workspaceTenantId").GetString());
        Assert.Equal("object-legacy", document.RootElement.GetProperty("objects")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void WorkspaceNormalizationDerivesMaterialObjectIdFromLinkedWorkItemOrStage()
    {
        var tenantId = Guid.NewGuid();
        var body = CreateWorkspaceJsonWithLegacyMaterials(tenantId.ToString(), "object-a", "stage-a", "item-a");

        var normalized = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(body, tenantId);
        using var document = JsonDocument.Parse(normalized);
        var materials = document.RootElement.GetProperty("materials").EnumerateArray().ToArray();

        Assert.Equal("object-a", materials[0].GetProperty("objectId").GetString());
        Assert.Equal("object-a", materials[1].GetProperty("objectId").GetString());
    }

    [Fact]
    public async Task TenantQueryFilterPreventsCrossWorkspaceReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString("N");
        var root = new InMemoryDatabaseRoot();

        await using (var seedDb = CreateDb(databaseName, root, tenantA))
        {
            seedDb.Tenants.AddRange(
                new Tenant { Id = tenantA, Code = "TENANT-A", CompanyName = "Tenant A" },
                new Tenant { Id = tenantB, Code = "TENANT-B", CompanyName = "Tenant B" });
            seedDb.ProjectProgressWorkspaces.AddRange(
                new ProjectProgressWorkspace
                {
                    TenantId = tenantA,
                    WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(CreateWorkspaceJson(tenantA.ToString(), "object-a", "stage-a", "item-a"), tenantA),
                },
                new ProjectProgressWorkspace
                {
                    TenantId = tenantB,
                    WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(CreateWorkspaceJson(tenantB.ToString(), "object-b", "stage-b", "item-b"), tenantB),
                });
            await seedDb.SaveChangesAsync();
        }

        await using var tenantBSession = CreateDb(databaseName, root, tenantB);
        var visibleWorkspace = await tenantBSession.ProjectProgressWorkspaces.SingleAsync();
        using var document = JsonDocument.Parse(visibleWorkspace.WorkspaceJson);

        Assert.Equal(tenantB.ToString(), document.RootElement.GetProperty("workspaceTenantId").GetString());
        Assert.Equal("object-b", document.RootElement.GetProperty("objects")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task LegacyMigrationUsesTrackedProjectWhenActiveProjectMatchesProjectArray()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot(), tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "TRACKER", CompanyName = "Tracker Tenant" });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "Tracker Site", TimeZone = "Asia/Baku" });
        db.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(
                CreateWorkspaceJson(tenantId.ToString(), siteId.ToString(), "stage-1", "work-1"),
                tenantId),
        });
        await db.SaveChangesAsync();

        await ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, CancellationToken.None);

        Assert.Equal(1, await db.Projects.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal("project-1", (await db.Projects.SingleAsync(x => x.TenantId == tenantId)).Id);
        Assert.Equal(ProjectProgressEndpoints.CurrentNormalizedMigrationVersion, (await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenantId)).NormalizedMigrationVersion);
    }

    [Fact]
    public async Task LegacyMigrationRestoresBakDemoLikeWorkspaceCountsAndIsIdempotent()
    {
        var tenantId = Guid.Parse("ba100000-0000-4000-9000-000000000001");
        var siteIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot(), tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "BAK-DEMO", CompanyName = "BAKİNİTY MMC" });
        foreach (var (siteId, index) in siteIds.Select((id, index) => (id, index)))
        {
            db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = $"BAK object {index + 1}", TimeZone = "Asia/Baku" });
        }

        db.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(CreateBakDemoLikeWorkspaceJson(tenantId, siteIds), tenantId),
        });
        await db.SaveChangesAsync();

        await ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, CancellationToken.None);

        await AssertBakDemoLikeCountsAsync(db, tenantId, projects: 1, sites: 4, estimates: 1, stages: 9, workItems: 10, crews: 6, materials: 6);
        var workspace = await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(ProjectProgressEndpoints.CurrentNormalizedMigrationVersion, workspace.NormalizedMigrationVersion);
        Assert.NotNull(workspace.NormalizedMigratedAt);

        await ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, CancellationToken.None);

        await AssertBakDemoLikeCountsAsync(db, tenantId, projects: 1, sites: 4, estimates: 1, stages: 9, workItems: 10, crews: 6, materials: 6);
    }

    [Fact]
    public async Task PostMigrationCanonicalEditIsNotOverwrittenByLegacyWorkspace()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = CreateDb(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot(), tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "EDIT", CompanyName = "Edit Tenant" });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "Edit Site", TimeZone = "Asia/Baku" });
        db.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = ProjectProgressEndpoints.NormalizeWorkspaceJsonForTenant(
                CreateWorkspaceJson(tenantId.ToString(), siteId.ToString(), "stage-1", "work-1"),
                tenantId),
        });
        await db.SaveChangesAsync();
        await ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, CancellationToken.None);

        var item = await db.ProjectWorkItems.SingleAsync(x => x.TenantId == tenantId && x.Id == "work-1");
        item.Name = "Server canonical edit";
        await db.SaveChangesAsync();

        await ProjectProgressEndpoints.EnsureCanonicalProjectProgressAsync(db, tenantId, CancellationToken.None);

        Assert.Equal("Server canonical edit", (await db.ProjectWorkItems.SingleAsync(x => x.TenantId == tenantId && x.Id == "work-1")).Name);
    }

    private static async Task AssertBakDemoLikeCountsAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        int projects,
        int sites,
        int estimates,
        int stages,
        int workItems,
        int crews,
        int materials)
    {
        Assert.Equal(projects, await db.Projects.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(sites, await db.ProjectSites.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(estimates, await db.ProjectEstimateVersions.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(stages, await db.ProjectStages.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(workItems, await db.ProjectWorkItems.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(crews, await db.ProjectCrews.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(materials, await db.ProjectWorkItemMaterials.CountAsync(x => x.TenantId == tenantId));
    }

    private static JsonElement CreateBakDemoLikeWorkspaceJson(Guid tenantId, Guid[] siteIds)
    {
        var projectId = "BAK-DEMO-PROJECT";
        var estimateId = "BAK-DEMO-ESTIMATE-V1";
        var stageIds = Enumerable.Range(1, 9).Select(index => $"bak-stage-{index}").ToArray();
        var workItems = Enumerable.Range(1, 10)
            .Select(index => new
            {
                id = $"bak-work-{index}",
                objectId = siteIds[(index - 1) % siteIds.Length].ToString(),
                projectId,
                estimateVersionId = estimateId,
                stageId = stageIds[(index - 1) % stageIds.Length],
                name = $"BAK smeta işi {index}",
                unit = "m2",
                quantity = 100 + index,
                completedQuantity = index,
                laborUnitPrice = 5,
                laborTotal = 500 + index,
                materialUnit = "m2",
                materialQuantity = 20 + index,
                materialUnitPrice = 3,
                materialTotal = 300 + index,
                totalCost = 800 + index,
                plannedHours = 40 + index,
                actualHours = index,
                assignedCrewId = $"bak-crew-{((index - 1) % 6) + 1}",
                status = "InProgress",
                progressPercent = 10 + index,
                plannedStartDate = "2026-08-01",
                plannedEndDate = "2026-08-30",
                notes = $"Qeyd {index}",
            })
            .ToArray();

        return JsonSerializer.SerializeToElement(new
        {
            workspaceTenantId = tenantId.ToString(),
            projects = new[] { new { id = projectId, name = "BAKİNİTY Residence", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = estimateId } },
            activeProjectId = projectId,
            objects = siteIds.Select((siteId, index) => new { id = siteId.ToString(), name = $"BAK object {index + 1}", projectId, status = "InProgress", plannedStartDate = "2026-08-01" }).ToArray(),
            project = new { id = projectId, name = "BAKİNİTY Residence", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = estimateId },
            estimateVersions = new[] { new { id = estimateId, projectId, name = "BAKİNİTY demo smeta v1", createdAt = DateTimeOffset.UtcNow, totalAmount = 316822.70m, notes = "BAK demo" } },
            summary = new { totalAmount = 316822.70m, laborAmount = 69717.50m, materialAmount = 205730.50m, hiddenCostAmount = 41324.70m, currency = "AZN" },
            stages = stageIds.Select((stageId, index) => new
            {
                id = stageId,
                objectId = siteIds[index % siteIds.Length].ToString(),
                projectId,
                estimateVersionId = estimateId,
                name = $"BAK etap {index + 1}",
                order = index + 1,
                totalCost = 1000 + index,
                laborCost = 400 + index,
                materialCost = 600 + index,
                plannedStartDate = "2026-08-01",
                plannedEndDate = "2026-08-30",
                status = "InProgress",
                progressPercent = 20 + index,
                plannedHours = 80 + index,
                actualHours = 10 + index,
            }).ToArray(),
            workItems,
            crews = Enumerable.Range(1, 6).Select(index => new
            {
                id = $"bak-crew-{index}",
                objectId = siteIds[(index - 1) % siteIds.Length].ToString(),
                projectId,
                name = $"BAK briqada {index}",
                type = "Tikinti",
                foremanName = $"Prorab {index}",
                workerCount = 5 + index,
                plannedDailyHours = 8,
                activeWorkStageId = stageIds[(index - 1) % stageIds.Length],
                activeWorkItemId = $"bak-work-{index}",
                status = "InProgress",
                progressPercent = 15 + index,
            }).ToArray(),
            workerAssignments = Array.Empty<object>(),
            materials = Enumerable.Range(1, 6).Select(index => new
            {
                id = $"bak-material-{index}",
                objectId = siteIds[(index - 1) % siteIds.Length].ToString(),
                projectId,
                name = $"BAK material {index}",
                unit = "ədəd",
                quantity = 10 + index,
                usedQuantity = index,
                remainingQuantity = 10,
                unitPrice = 2,
                linkedStageId = stageIds[(index - 1) % stageIds.Length],
                linkedWorkItemId = $"bak-work-{index}",
            }).ToArray(),
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        });
    }

    private static JsonElement CreateWorkspaceJson(string workspaceTenantId, string objectId, string stageId, string workItemId) =>
        JsonSerializer.SerializeToElement(new
        {
            workspaceTenantId,
            projects = new[] { new { id = "project-1", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-1" } },
            activeProjectId = "project-1",
            objects = new[] { new { id = objectId, name = "Object", projectId = "project-1", status = "InProgress" } },
            project = new { id = "project-1", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-1" },
            estimateVersions = Array.Empty<object>(),
            summary = new { totalAmount = 0, laborAmount = 0, materialAmount = 0, hiddenCostAmount = 0, currency = "AZN" },
            stages = new[] { new { id = stageId, objectId, name = "Stage", order = 1, totalCost = 100, laborCost = 40, materialCost = 60, plannedStartDate = "2026-08-01", plannedEndDate = "2026-08-10", status = "InProgress", progressPercent = 20, plannedHours = 10, actualHours = 2 } },
            workItems = new[] { new { id = workItemId, objectId, stageId, name = "Work item", unit = "m2", quantity = 1, laborUnitPrice = 40, laborTotal = 40, materialQuantity = 1, materialUnitPrice = 60, materialTotal = 60, totalCost = 100, plannedHours = 10, actualHours = 2, status = "InProgress", progressPercent = 20 } },
            crews = Array.Empty<object>(),
            workerAssignments = Array.Empty<object>(),
            materials = Array.Empty<object>(),
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        });

    private static JsonElement CreateWorkspaceJsonWithLegacyMaterials(string workspaceTenantId, string objectId, string stageId, string workItemId) =>
        JsonSerializer.SerializeToElement(new
        {
            workspaceTenantId,
            projects = new[] { new { id = "project-1", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-1" } },
            activeProjectId = "project-1",
            objects = new[] { new { id = objectId, name = "Object", projectId = "project-1", status = "InProgress" } },
            project = new { id = "project-1", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-1" },
            estimateVersions = Array.Empty<object>(),
            summary = new { totalAmount = 0, laborAmount = 0, materialAmount = 0, hiddenCostAmount = 0, currency = "AZN" },
            stages = new[] { new { id = stageId, objectId, name = "Stage", order = 1, totalCost = 100, laborCost = 40, materialCost = 60, plannedStartDate = "2026-08-01", plannedEndDate = "2026-08-10", status = "InProgress", progressPercent = 20, plannedHours = 10, actualHours = 2 } },
            workItems = new[] { new { id = workItemId, objectId, stageId, name = "Work item", unit = "m2", quantity = 1, laborUnitPrice = 40, laborTotal = 40, materialQuantity = 1, materialUnitPrice = 60, materialTotal = 60, totalCost = 100, plannedHours = 10, actualHours = 2, status = "InProgress", progressPercent = 20 } },
            crews = Array.Empty<object>(),
            workerAssignments = Array.Empty<object>(),
            materials = new[]
            {
                new { id = "mat-work-item", name = "Material A", unit = "m2", quantity = 1, usedQuantity = 0, remainingQuantity = 1, linkedWorkItemId = (string?)workItemId, linkedStageId = (string?)null },
                new { id = "mat-stage", name = "Material B", unit = "m2", quantity = 1, usedQuantity = 0, remainingQuantity = 1, linkedWorkItemId = (string?)null, linkedStageId = (string?)stageId },
            },
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        });

    private static BuildTrackDbContext CreateDb(string databaseName, InMemoryDatabaseRoot root, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }
}
