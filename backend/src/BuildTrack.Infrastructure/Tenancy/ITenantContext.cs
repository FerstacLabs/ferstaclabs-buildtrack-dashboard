namespace BuildTrack.Infrastructure.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; set; }
    Guid? UserId { get; set; }
    string? Role { get; set; }
    bool HasTenant { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public bool HasTenant => TenantId is not null;
}
