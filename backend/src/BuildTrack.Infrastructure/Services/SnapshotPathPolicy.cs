namespace BuildTrack.Infrastructure.Services;

public sealed record SnapshotPathReference(string RelativePath, string ApiUrl);

public static class SnapshotPathPolicy
{
    public const string DefaultStorageRoot = "/app/data/security-snapshots";
    private const string StorageMarker = "security-snapshots/";

    public static bool TryCreateApiUrl(string? snapshotPath, out string? apiUrl)
    {
        if (TryCreateReference(snapshotPath, out var reference))
        {
            apiUrl = reference.ApiUrl;
            return true;
        }

        apiUrl = null;
        return false;
    }

    public static bool TryCreateReference(string? snapshotPath, out SnapshotPathReference reference)
    {
        reference = default!;
        if (string.IsNullOrWhiteSpace(snapshotPath)) return false;

        var normalized = snapshotPath.Trim().Replace('\\', '/');
        var markerIndex = normalized.IndexOf(StorageMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return false;

        var relativePath = normalized[(markerIndex + StorageMarker.Length)..].Trim('/');
        if (!IsSafeRelativePath(relativePath)) return false;

        var encoded = string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
        reference = new SnapshotPathReference(relativePath, $"/api/snapshots/{encoded}");
        return true;
    }

    public static bool TryResolveLocalPath(string storageRoot, string relativePath, out string localPath)
    {
        localPath = string.Empty;
        if (!IsSafeRelativePath(relativePath)) return false;

        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(storageRoot) ? DefaultStorageRoot : storageRoot);
        var candidate = Path.GetFullPath(Path.Combine(new[] { root }.Concat(relativePath.Split('/')).ToArray()));
        var relativeToRoot = Path.GetRelativePath(root, candidate);
        if (relativeToRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeToRoot)) return false;

        localPath = candidate;
        return true;
    }

    public static bool Matches(string? snapshotPath, string relativePath)
    {
        return TryCreateReference(snapshotPath, out var reference)
               && string.Equals(reference.RelativePath, NormalizeRelativePath(relativePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeRelativePath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var segments = normalized.Split('/');
        if (segments.Length > 4) return false;
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or "..")) return false;
        if (segments.Any(segment => segment.Contains(':') || segment.Contains('\\'))) return false;

        var fileName = segments[^1];
        return fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Trim().Replace('\\', '/').Trim('/');
}
