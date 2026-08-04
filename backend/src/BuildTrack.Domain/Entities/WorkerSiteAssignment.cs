namespace BuildTrack.Domain.Entities;

public enum WorkerSiteAssignmentStatus
{
    Active,
    Inactive,
}

public sealed class WorkerSiteAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public bool IsPrimary { get; set; }
    public WorkerSiteAssignmentStatus Status { get; set; } = WorkerSiteAssignmentStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
