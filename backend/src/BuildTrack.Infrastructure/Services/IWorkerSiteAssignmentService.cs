using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IWorkerSiteAssignmentService
{
    Task SyncAssignmentsAsync(
        Guid workerId,
        IReadOnlyCollection<Guid> siteIds,
        Guid? primarySiteId,
        CancellationToken cancellationToken = default);

    Task<WorkerSiteAssignment?> EnsureAssignmentAsync(
        Guid workerId,
        Guid siteId,
        string reason,
        CancellationToken cancellationToken = default);
}
