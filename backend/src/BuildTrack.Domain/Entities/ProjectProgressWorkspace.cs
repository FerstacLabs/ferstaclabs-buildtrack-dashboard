namespace BuildTrack.Domain.Entities;

public sealed class ProjectProgressWorkspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string WorkspaceJson { get; set; } = "{}";
    public bool LegacyBrowserImportCompleted { get; set; }
    public DateTimeOffset? LegacyBrowserImportedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
