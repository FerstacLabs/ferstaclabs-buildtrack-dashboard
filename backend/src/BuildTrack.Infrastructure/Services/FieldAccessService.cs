using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Services;

public sealed class FieldAccessService : IFieldAccessService
{
    private readonly BuildTrackDbContext db;
    private readonly ITenantContext tenantContext;

    public FieldAccessService(BuildTrackDbContext db, ITenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public Task<bool> CanUseFieldPortalAsync(CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult(IsManagementRole(tenantContext.Role)
                               || string.Equals(tenantContext.Role, BuildTrackUserRole.Supervisor.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<SupervisorSiteAssignment>> GetActiveAssignmentsAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();
        if (IsManagementRole(tenantContext.Role))
        {
            var sites = await db.Sites.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);
            return sites.Select(x => new SupervisorSiteAssignment
                {
                    Id = x.Id,
                    TenantId = tenantId,
                    SupervisorUserId = userId,
                    SiteId = x.Id,
                    Site = x,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                })
                .ToList();
        }

        return await db.SupervisorSiteAssignments
            .AsNoTracking()
            .Include(x => x.Site)
            .Where(x => x.TenantId == tenantId
                        && x.SupervisorUserId == userId
                        && x.IsActive
                        && (x.ValidFrom == null || x.ValidFrom <= DateTimeOffset.UtcNow)
                        && (x.ValidUntil == null || x.ValidUntil >= DateTimeOffset.UtcNow))
            .OrderBy(x => x.Site!.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> CanAccessSiteAsync(Guid siteId, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        if (IsManagementRole(tenantContext.Role))
        {
            return await db.Sites.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == siteId, ct);
        }

        var userId = RequireUser();
        return await db.SupervisorSiteAssignments.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId
                           && x.SupervisorUserId == userId
                           && x.SiteId == siteId
                           && x.IsActive
                           && (x.ValidFrom == null || x.ValidFrom <= DateTimeOffset.UtcNow)
                           && (x.ValidUntil == null || x.ValidUntil >= DateTimeOffset.UtcNow),
                ct);
    }

    public async Task RequireSiteAccessAsync(Guid siteId, CancellationToken ct)
    {
        if (!await CanAccessSiteAsync(siteId, ct))
        {
            throw new UnauthorizedAccessException("Field supervisor is not assigned to this site");
        }
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required");

    private Guid RequireUser() =>
        tenantContext.UserId ?? throw new UnauthorizedAccessException("User context is required");

    private static bool IsManagementRole(string? role) =>
        string.Equals(role, BuildTrackUserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, BuildTrackUserRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase);
}
