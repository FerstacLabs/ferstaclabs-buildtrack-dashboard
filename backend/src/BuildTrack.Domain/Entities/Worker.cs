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
    public string? Brigade { get; set; }
    public string? Role { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal PlannedDailyHours { get; set; } = 8;
    public string AttendanceSource { get; set; } = "Manual";
    public int RiskScore { get; set; }
    public string? Notes { get; set; }
    public WorkerStatus Status { get; set; } = WorkerStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<WorkerCameraIdentity> CameraIdentities { get; set; } = new List<WorkerCameraIdentity>();
    public ICollection<WorkerSiteAssignment> SiteAssignments { get; set; } = new List<WorkerSiteAssignment>();
}
