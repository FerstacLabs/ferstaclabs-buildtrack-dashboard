namespace BuildTrack.Domain.Entities;

public sealed class ProjectProgressWorkspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string WorkspaceJson { get; set; } = "{}";
    public bool LegacyBrowserImportCompleted { get; set; }
    public DateTimeOffset? LegacyBrowserImportedAt { get; set; }
    public int NormalizedMigrationVersion { get; set; }
    public string? NormalizedMigrationStatus { get; set; }
    public DateTimeOffset? NormalizedMigratedAt { get; set; }
    public string? NormalizedMigrationError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
