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
        var result = await service.BuildContextAsync("işçi vəziyyəti necədir?", siteA, CancellationToken.None);

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
        var result = await service.BuildContextAsync("obyekt xülasəsi", siteB, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    private static BuildTrackDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, tenantContext);
    }
}
