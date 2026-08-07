namespace BuildTrack.Domain.Entities;

public enum BuildTrackUserRole
{
    Owner,
    Admin,
    Manager,
    Supervisor,
    User,
}

public enum BuildTrackUserStatus
{
    Active,
    Disabled,
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public BuildTrackUserRole Role { get; set; } = BuildTrackUserRole.User;
    public BuildTrackUserStatus Status { get; set; } = BuildTrackUserStatus.Active;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
