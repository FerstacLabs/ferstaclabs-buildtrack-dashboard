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
        var result = await service.BuildContextAsync("obyekt xülasəsi", null, siteB, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
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
        var result = await service.BuildContextAsync("obyekt", "project-a", siteB, CancellationToken.None);

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
