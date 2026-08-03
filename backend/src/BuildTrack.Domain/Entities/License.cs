namespace BuildTrack.Domain.Entities;

public enum LicensePlan
{
    Trial,
    Starter,
    Business,
    Enterprise,
    Unlimited,
}

public enum LicenseStatus
{
    Pending,
    Active,
    Expired,
    Revoked,
}

public sealed class License
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string LicenseKeyHash { get; set; } = string.Empty;
    public LicensePlan Plan { get; set; } = LicensePlan.Trial;
    public LicenseStatus Status { get; set; } = LicenseStatus.Pending;
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public int? MaxProjects { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxCameras { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
}
