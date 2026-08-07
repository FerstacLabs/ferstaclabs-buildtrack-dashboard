using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IFieldAccessService
{
    Task<bool> CanUseFieldPortalAsync(CancellationToken ct);
    Task<bool> CanAccessSiteAsync(Guid siteId, CancellationToken ct);
    Task RequireSiteAccessAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<SupervisorSiteAssignment>> GetActiveAssignmentsAsync(CancellationToken ct);
}
