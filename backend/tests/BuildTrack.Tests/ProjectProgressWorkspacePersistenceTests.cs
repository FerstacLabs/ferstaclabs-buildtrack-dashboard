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

    private static BuildTrackDbContext CreateDb(string databaseName, InMemoryDatabaseRoot root, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }
}
