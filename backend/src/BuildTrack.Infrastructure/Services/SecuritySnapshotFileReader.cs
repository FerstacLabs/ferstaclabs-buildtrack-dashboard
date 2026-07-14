using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public static class SecuritySnapshotFileReader
{
    public static async Task<SecuritySnapshotFileReadResult> TryReadAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(securityEvent.StoredSnapshotPath)) return SecuritySnapshotFileReadResult.NotFound();
        if (!File.Exists(securityEvent.StoredSnapshotPath)) return SecuritySnapshotFileReadResult.NotFound();

        var bytes = await File.ReadAllBytesAsync(securityEvent.StoredSnapshotPath, cancellationToken);
        return SecuritySnapshotFileReadResult.Found(bytes, securityEvent.StoredSnapshotContentType ?? "image/jpeg");
    }
}

public sealed record SecuritySnapshotFileReadResult(bool Exists, byte[]? Bytes = null, string ContentType = "image/jpeg")
{
    public static SecuritySnapshotFileReadResult Found(byte[] bytes, string contentType) => new(true, bytes, contentType);
    public static SecuritySnapshotFileReadResult NotFound() => new(false);
}
