using System.Text.Json.Nodes;
using BuildTrack.Api;
using BuildTrack.Api.Services;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class ProjectAssistantContextServiceTests
{
    [Fact]
    public async Task BuildContext_UsesCanonicalCurrentTenantDataOnly()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var tenantContext = new TenantContext
        {
            TenantId = tenantA,
            UserId = Guid.NewGuid(),
            Role = BuildTrackUserRole.Manager.ToString(),
        };
        await using var db = CreateDbContext(tenantContext);

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, CompanyName = "Tenant A MMC", Code = "TA" },
            new Tenant { Id = tenantB, CompanyName = "Tenant B MMC", Code = "TB" });
        db.Users.Add(new AppUser
        {
            Id = tenantContext.UserId.Value,
            TenantId = tenantA,
            FullName = "Manager A",
            Email = "manager-a@example.test",
            PasswordHash = "SUPER-SECRET-HASH",
            Role = BuildTrackUserRole.Manager,
        });
        db.Sites.AddRange(
            new Site { Id = siteA, TenantId = tenantA, Name = "Tenant A Site", TimeZone = "Asia/Baku" },
            new Site { Id = siteB, TenantId = tenantB, Name = "Tenant B Site", TimeZone = "Asia/Baku" });
        db.Workers.AddRange(
            new Worker
            {
                TenantId = tenantA,
                SiteId = siteA,
                FullName = "Tenant A Worker",
                ExternalWorkerCode = "A-001",
                HourlyRate = 7,
            },
            new Worker
            {
                TenantId = tenantB,
                SiteId = siteB,
                FullName = "Tenant B Worker",
                ExternalWorkerCode = "B-001",
                HourlyRate = 99,
            });
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("işçi vəziyyəti necədir?", null, siteA, CancellationToken.None);

        Assert.True(result.Success);
        var json = result.Context.ToJsonString();
        Assert.Contains("Tenant A MMC", json);
        Assert.Contains("Tenant A Site", json);
        Assert.Contains("Tenant A Worker", json);
        Assert.DoesNotContain("Tenant B MMC", json);
        Assert.DoesNotContain("Tenant B Site", json);
        Assert.DoesNotContain("Tenant B Worker", json);
        Assert.DoesNotContain("SUPER-SECRET-HASH", json);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EncryptedPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildContext_RejectsSelectedSiteOutsideCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var tenantContext = new TenantContext { TenantId = tenantA, Role = BuildTrackUserRole.Manager.ToString() };
        await using var db = CreateDbContext(tenantContext);

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, CompanyName = "Tenant A MMC", Code = "TA" },
            new Tenant { Id = tenantB, CompanyName = "Tenant B MMC", Code = "TB" });
        db.Sites.Add(new Site { Id = siteB, TenantId = tenantB, Name = "Tenant B Site", TimeZone = "Asia/Baku" });
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("layihə xülasəsi", null, siteB, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("layihə", result.Error ?? string.Empty);
        Assert.DoesNotContain("obyekt", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildContext_FiltersNormalizedProjectDataBySelectedSiteOnly()
    {
        var tenantId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var tenantContext = new TenantContext { TenantId = tenantId, Role = BuildTrackUserRole.Manager.ToString() };
        await using var db = CreateDbContext(tenantContext);

        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant MMC", Code = "TEN" });
        db.Sites.AddRange(
            new Site { Id = siteA, TenantId = tenantId, Name = "Blok A", TimeZone = "Asia/Baku" },
            new Site { Id = siteB, TenantId = tenantId, Name = "Blok B", TimeZone = "Asia/Baku" });
        db.Projects.Add(new ProjectRecord { Id = "tenant-project", TenantId = tenantId, Name = "Residence", CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectSites.AddRange(
            new ProjectSiteRecord { Id = "site-a-link", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, CreatedAt = DateTimeOffset.UtcNow },
            new ProjectSiteRecord { Id = "site-b-link", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectStages.AddRange(
            new ProjectStageRecord { Id = "stage-a", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, Name = "Block A stage", TotalCost = 1000, CreatedAt = DateTimeOffset.UtcNow },
            new ProjectStageRecord { Id = "stage-b", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, Name = "Block B stage", TotalCost = 2000, CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectWorkItems.AddRange(
            new ProjectWorkItemRecord { Id = "work-a", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, StageId = "stage-a", Name = "Block A concrete work", Unit = "m3", Quantity = 1, TotalCost = 1000, CreatedAt = DateTimeOffset.UtcNow },
            new ProjectWorkItemRecord { Id = "work-b", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, StageId = "stage-b", Name = "Block B plaster work", Unit = "m2", Quantity = 1, TotalCost = 2000, CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectCrews.AddRange(
            new ProjectCrewRecord { Id = "crew-a", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, Name = "Block A crew", Type = "Monolit", ForemanName = "Prorab A", CreatedAt = DateTimeOffset.UtcNow },
            new ProjectCrewRecord { Id = "crew-b", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, Name = "Block B crew", Type = "Suvaq", ForemanName = "Prorab B", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("layihə üzrə status", null, siteA, CancellationToken.None);

        Assert.True(result.Success);
        var json = result.Context.ToJsonString();
        Assert.Contains("Block A concrete work", json);
        Assert.Contains("Block A crew", json);
        Assert.DoesNotContain("Block B plaster work", json);
        Assert.DoesNotContain("Block B crew", json);

        var metadata = result.Context["metadata"]!.AsObject();
        Assert.Equal(siteA, metadata["selectedSiteId"]!.GetValue<Guid>());
        Assert.Equal("Blok A", metadata["selectedSiteName"]!.GetValue<string>());

        var summary = result.Context["projectProgress"]!["summary"]!.AsObject();
        Assert.Equal(1, summary["workItemCount"]!.GetValue<int>());
        Assert.Equal(1, summary["crewCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task BuildContext_AllProjectsUsesTenantWideScopeWhenSelectedSiteIsNull()
    {
        var tenantId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var tenantContext = new TenantContext { TenantId = tenantId, Role = BuildTrackUserRole.Manager.ToString() };
        await using var db = CreateDbContext(tenantContext);

        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant MMC", Code = "TEN" });
        db.Sites.AddRange(
            new Site { Id = siteA, TenantId = tenantId, Name = "Blok A", TimeZone = "Asia/Baku" },
            new Site { Id = siteB, TenantId = tenantId, Name = "Villa Korpus 1", TimeZone = "Asia/Baku" });
        db.Projects.Add(new ProjectRecord { Id = "tenant-project", TenantId = tenantId, Name = "Residence", CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectSites.AddRange(
            new ProjectSiteRecord { Id = "site-a-link", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, CreatedAt = DateTimeOffset.UtcNow },
            new ProjectSiteRecord { Id = "site-b-link", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectWorkItems.AddRange(
            new ProjectWorkItemRecord { Id = "work-a", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteA, StageId = "stage-a", Name = "Block A concrete work", Unit = "m3", Quantity = 1, TotalCost = 1000, CreatedAt = DateTimeOffset.UtcNow },
            new ProjectWorkItemRecord { Id = "work-b", TenantId = tenantId, ProjectId = "tenant-project", SiteId = siteB, StageId = "stage-b", Name = "Villa roof work", Unit = "m2", Quantity = 1, TotalCost = 2000, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("layihələr üzrə ümumi vəziyyət", null, null, CancellationToken.None);

        Assert.True(result.Success);
        var json = result.Context.ToJsonString();
        Assert.Contains("Block A concrete work", json);
        Assert.Contains("Villa roof work", json);

        var metadata = result.Context["metadata"]!.AsObject();
        Assert.Null(metadata["selectedSiteId"]);
        Assert.Equal("Bütün layihələr", metadata["selectedSiteName"]!.GetValue<string>());

        var summary = result.Context["projectProgress"]!["summary"]!.AsObject();
        Assert.Equal(2, summary["workItemCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task BuildContext_UsesNormalizedProjectProgressTablesForSmetaAndCrewCounts()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tenantContext = new TenantContext { TenantId = tenantId, Role = BuildTrackUserRole.Manager.ToString() };
        await using var db = CreateDbContext(tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "BAKİNİTY MMC", Code = "BAK-DEMO" });
        db.Sites.Add(new Site { Id = siteId, TenantId = tenantId, Name = "BAK object", TimeZone = "Asia/Baku" });
        db.ProjectProgressWorkspaces.Add(new ProjectProgressWorkspace
        {
            TenantId = tenantId,
            WorkspaceJson = """
                {
                  "projects":[{"id":"legacy-project","name":"Legacy"}],
                  "activeProjectId":"legacy-project",
                  "objects":[],
                  "stages":[],
                  "workItems":[{"id":"legacy-work-1","name":"Legacy item"}],
                  "crews":[{"id":"legacy-crew-1","name":"Legacy crew"}]
                }
                """,
            NormalizedMigrationVersion = ProjectProgressEndpoints.CurrentNormalizedMigrationVersion,
        });
        db.Projects.Add(new ProjectRecord { Id = "project-normalized", TenantId = tenantId, Name = "BAKİNİTY Residence", CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectSites.Add(new ProjectSiteRecord { Id = siteId.ToString(), TenantId = tenantId, ProjectId = "project-normalized", SiteId = siteId, CreatedAt = DateTimeOffset.UtcNow });
        for (var index = 1; index <= 10; index++)
        {
            db.ProjectWorkItems.Add(new ProjectWorkItemRecord
            {
                Id = $"work-{index}",
                TenantId = tenantId,
                ProjectId = "project-normalized",
                SiteId = siteId,
                StageId = "stage-1",
                Name = $"Smeta işi {index}",
                Unit = "m2",
                Quantity = 1,
                TotalCost = 100,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        for (var index = 1; index <= 6; index++)
        {
            db.ProjectCrews.Add(new ProjectCrewRecord
            {
                Id = $"crew-{index}",
                TenantId = tenantId,
                ProjectId = "project-normalized",
                SiteId = siteId,
                Name = $"Briqada {index}",
                Type = "Tikinti",
                ForemanName = $"Prorab {index}",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("Neçə smeta iş sətri və briqadamız var?", "project-normalized", null, CancellationToken.None);

        Assert.True(result.Success);
        var projectProgress = result.Context["projectProgress"]!.AsObject();
        var summary = projectProgress["summary"]!.AsObject();
        Assert.Equal(10, summary["workItemCount"]!.GetValue<int>());
        Assert.Equal(6, summary["crewCount"]!.GetValue<int>());
        Assert.DoesNotContain("Legacy item", result.Context.ToJsonString());
    }

    [Fact]
    public async Task BuildContext_RejectsSelectedSiteOutsideSelectedProject()
    {
        var tenantId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var tenantContext = new TenantContext { TenantId = tenantId, Role = BuildTrackUserRole.Manager.ToString() };
        await using var db = CreateDbContext(tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant", Code = "TEN" });
        db.Sites.AddRange(
            new Site { Id = siteA, TenantId = tenantId, Name = "Site A", TimeZone = "Asia/Baku" },
            new Site { Id = siteB, TenantId = tenantId, Name = "Site B", TimeZone = "Asia/Baku" });
        db.Projects.Add(new ProjectRecord { Id = "project-a", TenantId = tenantId, Name = "Project A", CreatedAt = DateTimeOffset.UtcNow });
        db.ProjectSites.Add(new ProjectSiteRecord { Id = siteA.ToString(), TenantId = tenantId, ProjectId = "project-a", SiteId = siteA, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new BuildTrackAiContextService(db, tenantContext, NullLogger<BuildTrackAiContextService>.Instance);
        var result = await service.BuildContextAsync("layihə", "project-a", siteB, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    private static BuildTrackDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, tenantContext);
    }
}
