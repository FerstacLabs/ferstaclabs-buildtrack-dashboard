namespace BuildTrack.Domain.Entities;

public sealed class Worker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public string ExternalWorkerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public WorkerStatus Status { get; set; } = WorkerStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
