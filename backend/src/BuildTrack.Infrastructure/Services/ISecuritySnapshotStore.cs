using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public sealed record SecuritySnapshotStoreResult(string Status, string? StoredPath = null, string? ContentType = null, string? Error = null, string? Source = null);

public interface ISecuritySnapshotStore
{
    Task<SecuritySnapshotStoreResult> TryStoreSnapshotAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);
}

