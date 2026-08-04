namespace BuildTrack.Domain.Entities;

public sealed class WorkerCameraIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Vendor { get; set; } = "Dahua";
    public string? ExternalUserId { get; set; }
    public string? CardName { get; set; }
    public string? NormalizedCardName { get; set; }
    public bool IsPrimary { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
