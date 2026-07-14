using System.Net;
using BuildTrack.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class SecuritySnapshotStore(
    IConfiguration configuration,
    ILogger<SecuritySnapshotStore> logger) : ISecuritySnapshotStore
{
    private const string StoredStatus = "Stored";
    private const string FailedStatus = "Failed";
    private const string AllInvalidError = "All snapshot sources returned black or invalid image";

    public async Task<SecuritySnapshotStoreResult> TryStoreSnapshotAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        var host = configuration["DAHUA_CGI_HOST"] ?? "192.168.31.174";
        var username = configuration["DAHUA_CGI_USERNAME"] ?? "admin";
        var password = configuration["DAHUA_CGI_PASSWORD"] ?? string.Empty;
        var storagePath = configuration["SECURITY_SNAPSHOT_STORAGE_PATH"] ?? "/app/data/security-snapshots";

        logger.LogInformation(
            "UnknownFace snapshot storage started. SecurityEventId={SecurityEventId}, OriginalSnapshotPath={SnapshotPath}",
            securityEvent.Id,
            securityEvent.SnapshotPath);

        using var httpClient = CreateHttpClient(username, password);
        var attempts = SecuritySnapshotAttemptPlanner.BuildAttempts(host, securityEvent.SnapshotPath).ToArray();
        var errors = new List<string>();

        foreach (var attempt in attempts)
        {
            var result = await TryDownloadAsync(httpClient, attempt, cancellationToken);
            if (result.Bytes is null)
            {
                errors.Add(result.Error ?? $"{attempt.Source} failed");
                continue;
            }

            var filePath = Path.Combine(storagePath, $"{securityEvent.Id}.jpg");
            Directory.CreateDirectory(storagePath);
            await File.WriteAllBytesAsync(filePath, result.Bytes, cancellationToken);
            logger.LogInformation(
                "UnknownFace snapshot stored locally. SecurityEventId={SecurityEventId}, Source={SnapshotSource}, StoredPath={StoredPath}, ContentType={ContentType}, ByteLength={ByteLength}, FirstBytes={FirstBytesHex}",
                securityEvent.Id,
                attempt.Source,
                filePath,
                result.ContentType,
                result.Bytes.Length,
                SecuritySnapshotValidator.FirstBytesHex(result.Bytes));
            return new SecuritySnapshotStoreResult(StoredStatus, filePath, "image/jpeg", Source: attempt.Source);
        }

        var detailedError = string.Join("; ", errors.Where(value => !string.IsNullOrWhiteSpace(value)).Take(3));
        var error = errors.Count > 0 ? AllInvalidError : "snapshot download failed";
        if (!string.IsNullOrWhiteSpace(detailedError)) error = $"{error}. {detailedError}";
        if (error.Length > 500) error = error[..500];
        logger.LogWarning(
            "UnknownFace snapshot storage failed. SecurityEventId={SecurityEventId}, SnapshotDownloadStatus={SnapshotDownloadStatus}, Error={SnapshotDownloadError}",
            securityEvent.Id,
            FailedStatus,
            error);
        return new SecuritySnapshotStoreResult(FailedStatus, Error: error);
    }

    private async Task<SnapshotDownloadAttemptResult> TryDownloadAsync(HttpClient httpClient, SecuritySnapshotAttempt attempt, CancellationToken cancellationToken)
    {
        logger.LogInformation("UnknownFace snapshot download attempt. Source={SnapshotSource}, Url={Url}", attempt.Source, attempt.Uri);
        try
        {
            using var response = await httpClient.GetAsync(attempt.Uri, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var firstBytesHex = SecuritySnapshotValidator.FirstBytesHex(bytes);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "UnknownFace snapshot download response. Source={SnapshotSource}, Url={Url}, HttpStatus={HttpStatus}, ContentType={ContentType}, ByteLength={ByteLength}, FirstBytes={FirstBytesHex}, AverageBrightness={AverageBrightness}, IsBlack={IsBlack}",
                    attempt.Source,
                    attempt.Uri,
                    (int)response.StatusCode,
                    contentType ?? "unknown",
                    bytes.Length,
                    firstBytesHex,
                    null,
                    null);
                return SnapshotDownloadAttemptResult.Fail($"{attempt.Source} returned HTTP {(int)response.StatusCode}");
            }

            var validation = SecuritySnapshotValidator.Validate(bytes, contentType);
            logger.LogInformation(
                "UnknownFace snapshot download response. Source={SnapshotSource}, Url={Url}, HttpStatus={HttpStatus}, ContentType={ContentType}, ByteLength={ByteLength}, FirstBytes={FirstBytesHex}, AverageBrightness={AverageBrightness}, IsBlack={IsBlack}",
                attempt.Source,
                attempt.Uri,
                (int)response.StatusCode,
                contentType ?? "unknown",
                bytes.Length,
                firstBytesHex,
                validation.AverageBrightness,
                validation.IsBlack);

            if (!validation.IsValid)
            {
                return SnapshotDownloadAttemptResult.Fail($"{attempt.Source} invalid image: {validation.Error}");
            }

            return SnapshotDownloadAttemptResult.Success(bytes, contentType ?? "image/jpeg");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogWarning(ex, "UnknownFace snapshot download attempt failed. Source={SnapshotSource}, Url={Url}", attempt.Source, attempt.Uri);
            return SnapshotDownloadAttemptResult.Fail($"{attempt.Source} failed: {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient(string username, string password)
    {
        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(username, password),
            PreAuthenticate = true,
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    private sealed record SnapshotDownloadAttemptResult(byte[]? Bytes, string? ContentType, string? Error)
    {
        public static SnapshotDownloadAttemptResult Success(byte[] bytes, string contentType) => new(bytes, contentType, null);
        public static SnapshotDownloadAttemptResult Fail(string error) => new(null, null, error);
    }
}

public sealed record SecuritySnapshotAttempt(string Source, Uri Uri);

public static class SecuritySnapshotAttemptPlanner
{
    public static IReadOnlyList<SecuritySnapshotAttempt> BuildAttempts(string host, string? snapshotPath)
    {
        var normalizedHost = NormalizeHost(host);
        var attempts = new List<SecuritySnapshotAttempt>();
        if (!string.IsNullOrWhiteSpace(snapshotPath) && snapshotPath.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase))
        {
            attempts.Add(new SecuritySnapshotAttempt("DahuaEventRpcLoadfile", new Uri($"{normalizedHost}/cgi-bin/RPC_Loadfile{snapshotPath}")));
        }

        attempts.Add(new SecuritySnapshotAttempt("LiveSnapshotChannel0", new Uri($"{normalizedHost}/cgi-bin/snapshot.cgi?channel=0")));
        attempts.Add(new SecuritySnapshotAttempt("LiveSnapshotChannel1", new Uri($"{normalizedHost}/cgi-bin/snapshot.cgi?channel=1")));
        attempts.Add(new SecuritySnapshotAttempt("LiveSnapshotChannel2", new Uri($"{normalizedHost}/cgi-bin/snapshot.cgi?channel=2")));
        return attempts;
    }

    private static string NormalizeHost(string host) => host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        ? host.TrimEnd('/')
        : $"http://{host.TrimEnd('/')}";
}
