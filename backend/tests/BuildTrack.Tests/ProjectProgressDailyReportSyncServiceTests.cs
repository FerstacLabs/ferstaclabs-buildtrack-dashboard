using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Tests;

public sealed class ProjectProgressDailyReportSyncServiceTests
{
    [Fact]
    public async Task RecalculateUsesOnlyApprovedCanonicalDailyReportLines()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedWorkspace(db, tenantId, siteId, quantity: 100);
        var smeta = SeedSmetaItem(db, tenantId, siteId, "work-a", 100);
        SeedReport(db, tenantId, siteId, FieldDailyReportStatus.Submitted, smeta, 20, 8);
        SeedReport(db, tenantId, siteId, FieldDailyReportStatus.Rejected, smeta, 15, 5);
        await db.SaveChangesAsync();

        var service = new ProjectProgressDailyReportSyncService(db);
        await service.RecalculateApprovedDailyReportProgressAsync(tenantId, null, CancellationToken.None);

        var item = ReadFirstWorkItem(await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenantId));
        Assert.Equal(0, item.GetProperty("completedQuantity").GetDecimal());
        Assert.Equal(0, item.GetProperty("progressPercent").GetDecimal());

        SeedReport(db, tenantId, siteId, FieldDailyReportStatus.Approved, smeta, 25, 10);
        await db.SaveChangesAsync();
        await service.RecalculateApprovedDailyReportProgressAsync(tenantId, null, CancellationToken.None);

        item = ReadFirstWorkItem(await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenantId));
        Assert.Equal(25, item.GetProperty("completedQuantity").GetDecimal());
        Assert.Equal(25, item.GetProperty("progressPercent").GetDecimal());
        Assert.Equal(10, item.GetProperty("actualHours").GetDecimal());

        await service.RecalculateApprovedDailyReportProgressAsync(tenantId, null, CancellationToken.None);
        item = ReadFirstWorkItem(await db.ProjectProgressWorkspaces.SingleAsync(x => x.TenantId == tenantId));
        Assert.Equal(25, item.GetProperty("completedQuantity").GetDecimal());
    }

    [Fact]
    public async Task ValidateApprovedReportBlocksOverreportedWorkItem()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedWorkspace(db, tenantId, siteId, quantity: 100);
        var smeta = SeedSmetaItem(db, tenantId, siteId, "work-a", 100);
        SeedReport(db, tenantId, siteId, FieldDailyReportStatus.Approved, smeta, 80, 10);
        var submitted = SeedReport(db, tenantId, siteId, FieldDailyReportStatus.Submitted, smeta, 25, 6);
        await db.SaveChangesAsync();

        var service = new ProjectProgressDailyReportSyncService(db);
        var result = await service.ValidateApprovedReportAsync(tenantId, submitted, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("work-a", result.ProjectWorkItemId);
        Assert.Equal(100, result.PlannedQuantity);
        Assert.Equal(105, result.CandidateApprovedQuantity);
    }

    [Fact]
    public async Task SyncFieldSmetaItemsCreatesDurableWorkItemLinksFromWorkspace()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        SeedWorkspace(db, tenantId, siteId, quantity: 42);
        await db.SaveChangesAsync();

        var service = new ProjectProgressDailyReportSyncService(db);
        await service.SyncFieldSmetaItemsFromWorkspaceAsync(tenantId, CancellationToken.None);

        var item = await db.FieldSmetaItems.SingleAsync(x => x.TenantId == tenantId && x.ProjectWorkItemId == "work-a");
        Assert.Equal(siteId, item.SiteId);
        Assert.Equal("Beton işi", item.WorkName);
        Assert.Equal("Monolit", item.StageName);
        Assert.Equal(42, item.PlannedQuantity);
    }

    private static BuildTrackDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext { TenantId = tenantId });
    }

    private static void SeedWorkspace(BuildTrackDbContext db, Guid tenantId, Guid siteId, decimal quantity)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "TENANT", CompanyName = "Tenant" });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "Obyekt", Address = "Bakı", TimeZone = "Asia/Baku" });
        db.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = JsonSerializer.Serialize(new
            {
                workspaceTenantId = tenantId.ToString(),
                projects = new[] { new { id = "project-a", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-a" } },
                activeProjectId = "project-a",
                objects = new[] { new { id = siteId.ToString(), name = "Obyekt", projectId = "project-a", status = "InProgress" } },
                project = new { id = "project-a", name = "Project", currency = "AZN", createdAt = DateTimeOffset.UtcNow, activeEstimateVersionId = "estimate-a" },
                estimateVersions = Array.Empty<object>(),
                summary = new { totalAmount = 100, laborAmount = 40, materialAmount = 60, hiddenCostAmount = 0, currency = "AZN" },
                stages = new[] { new { id = "stage-a", objectId = siteId.ToString(), name = "Monolit", order = 1, totalCost = 100, laborCost = 40, materialCost = 60, plannedStartDate = "2026-08-01", plannedEndDate = "2026-08-10", status = "NotStarted", progressPercent = 0, plannedHours = 10, actualHours = 0 } },
                workItems = new[] { new { id = "work-a", objectId = siteId.ToString(), stageId = "stage-a", name = "Beton işi", unit = "m3", quantity, laborUnitPrice = 40, laborTotal = 40, materialQuantity = quantity, materialUnitPrice = 60, materialTotal = 60, totalCost = 100, plannedHours = 10, actualHours = 0, completedQuantity = 0, status = "NotStarted", progressPercent = 0 } },
                crews = Array.Empty<object>(),
                workerAssignments = Array.Empty<object>(),
                materials = Array.Empty<object>(),
                attendanceSessions = Array.Empty<object>(),
                workHourAllocations = Array.Empty<object>(),
                dailyReports = Array.Empty<object>(),
                issues = Array.Empty<object>(),
                risks = Array.Empty<object>(),
                assistantMessages = Array.Empty<object>(),
            }),
        });
    }

    private static FieldSmetaItem SeedSmetaItem(BuildTrackDbContext db, Guid tenantId, Guid siteId, string workItemId, decimal plannedQuantity)
    {
        var item = new FieldSmetaItem
        {
            TenantId = tenantId,
            SiteId = siteId,
            StageName = "Monolit",
            WorkName = "Beton işi",
            Unit = "m3",
            ProjectWorkItemId = workItemId,
            PlannedQuantity = plannedQuantity,
            IsActive = true,
        };
        db.FieldSmetaItems.Add(item);
        return item;
    }

    private static SupervisorDailyReport SeedReport(BuildTrackDbContext db, Guid tenantId, Guid siteId, FieldDailyReportStatus status, FieldSmetaItem smeta, decimal quantity, decimal hours)
    {
        var report = new SupervisorDailyReport
        {
            TenantId = tenantId,
            SiteId = siteId,
            SupervisorUserId = Guid.NewGuid(),
            ReportDate = new DateOnly(2026, 8, 14),
            Status = status,
            SubmittedAt = DateTimeOffset.UtcNow,
        };
        report.Lines.Add(new SupervisorDailyReportLine
        {
            TenantId = tenantId,
            SmetaItemId = smeta.Id,
            SmetaItem = smeta,
            ProjectWorkItemId = smeta.ProjectWorkItemId,
            ReportedQuantity = quantity,
            WorkHours = hours,
            Unit = smeta.Unit,
        });
        db.SupervisorDailyReports.Add(report);
        return report;
    }

    private static JsonElement ReadFirstWorkItem(ProjectProgressWorkspace workspace)
    {
        using var document = JsonDocument.Parse(workspace.WorkspaceJson);
        return document.RootElement.GetProperty("workItems")[0].Clone();
    }
}
