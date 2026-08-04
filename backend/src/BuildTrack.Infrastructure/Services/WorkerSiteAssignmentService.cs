using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class WorkerSiteAssignmentService(
    BuildTrackDbContext db,
    ILogger<WorkerSiteAssignmentService> logger) : IWorkerSiteAssignmentService
{
    public async Task SyncAssignmentsAsync(
        Guid workerId,
        IReadOnlyCollection<Guid> siteIds,
        Guid? primarySiteId,
        CancellationToken cancellationToken = default)
    {
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == workerId, cancellationToken)
            ?? throw new InvalidOperationException("Worker was not found");

        var uniqueSiteIds = siteIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (primarySiteId is not null && primarySiteId.Value != Guid.Empty && !uniqueSiteIds.Contains(primarySiteId.Value))
        {
            uniqueSiteIds.Add(primarySiteId.Value);
        }

        if (uniqueSiteIds.Count > 0)
        {
            var validSiteIds = await db.Sites
                .AsNoTracking()
                .Where(x => uniqueSiteIds.Contains(x.Id) && x.TenantId == worker.TenantId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (validSiteIds.Count != uniqueSiteIds.Count)
            {
                throw new InvalidOperationException("One or more selected sites were not found for this tenant");
            }
        }

        var primary = primarySiteId is not null && uniqueSiteIds.Contains(primarySiteId.Value)
            ? primarySiteId.Value
            : uniqueSiteIds.FirstOrDefault();

        var assignments = await db.WorkerSiteAssignments
            .Where(x => x.WorkerId == worker.Id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var assignment in assignments.Where(x => x.Status == WorkerSiteAssignmentStatus.Active && !uniqueSiteIds.Contains(x.SiteId)))
        {
            assignment.Status = WorkerSiteAssignmentStatus.Inactive;
            assignment.IsPrimary = false;
            assignment.UpdatedAt = now;
        }

        foreach (var siteId in uniqueSiteIds)
        {
            var assignment = assignments.FirstOrDefault(x => x.SiteId == siteId && x.Status == WorkerSiteAssignmentStatus.Active);
            if (assignment is null)
            {
                assignment = new WorkerSiteAssignment
                {
                    TenantId = worker.TenantId,
                    WorkerId = worker.Id,
                    SiteId = siteId,
                    CreatedAt = now,
                    Status = WorkerSiteAssignmentStatus.Active,
                };
                db.WorkerSiteAssignments.Add(assignment);
            }

            assignment.IsPrimary = siteId == primary;
            assignment.UpdatedAt = now;
        }

        if (primary != Guid.Empty)
        {
            worker.SiteId = primary;
            worker.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkerSiteAssignment?> EnsureAssignmentAsync(
        Guid workerId,
        Guid siteId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == workerId, cancellationToken)
            ?? throw new InvalidOperationException("Worker was not found");
        var siteExists = await db.Sites.AsNoTracking().AnyAsync(x => x.Id == siteId && x.TenantId == worker.TenantId, cancellationToken);
        if (!siteExists) throw new InvalidOperationException("Site was not found for this tenant");

        var assignment = await db.WorkerSiteAssignments
            .FirstOrDefaultAsync(
                x => x.TenantId == worker.TenantId
                     && x.WorkerId == worker.Id
                     && x.SiteId == siteId
                     && x.Status == WorkerSiteAssignmentStatus.Active,
                cancellationToken);
        if (assignment is not null) return assignment;

        var hasActiveAssignment = await db.WorkerSiteAssignments
            .AnyAsync(x => x.WorkerId == worker.Id && x.Status == WorkerSiteAssignmentStatus.Active, cancellationToken);
        assignment = new WorkerSiteAssignment
        {
            TenantId = worker.TenantId,
            WorkerId = worker.Id,
            SiteId = siteId,
            IsPrimary = !hasActiveAssignment,
            Status = WorkerSiteAssignmentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.WorkerSiteAssignments.Add(assignment);

        if (!hasActiveAssignment)
        {
            worker.SiteId = siteId;
            worker.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Reason}. WorkerId={WorkerId}, SiteId={SiteId}", reason, worker.Id, siteId);
        return assignment;
    }
}
