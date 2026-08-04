using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public sealed record WorkerCameraIdentityResolution(
    Worker? Worker,
    WorkerCameraIdentity? Identity,
    string Status,
    string? ResolvedBy,
    string? Reason)
{
    public bool Resolved => Worker is not null && Identity is not null;
}

public sealed record WorkerCameraIdentityRemapResult(int AttendanceEventsUpdated, int AttendanceSessionsUpdated);

public interface IWorkerCameraIdentityResolver
{
    string? NormalizeCardName(string? value);

    Task<WorkerCameraIdentityResolution> ResolveAsync(
        Device device,
        DahuaAccessRecord record,
        CancellationToken cancellationToken = default);

    Task<WorkerCameraIdentity> UpsertAsync(
        Guid workerId,
        Guid? deviceId,
        string? externalUserId,
        string? cardName,
        bool isPrimary,
        CancellationToken cancellationToken = default);

    Task<WorkerCameraIdentityRemapResult> RemapRecentAsync(
        Guid workerId,
        Guid? identityId,
        CancellationToken cancellationToken = default);
}
