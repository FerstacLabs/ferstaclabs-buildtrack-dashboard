using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public enum SecurityEventIngestionResultStatus
{
    Created,
    Debounced,
    Duplicate,
    Ignored
}

public sealed record SecurityEventIngestionResult(SecurityEventIngestionResultStatus Status, SecurityEvent? Event = null, string? Reason = null);

public interface ISecurityEventService
{
    Task<SecurityEventIngestionResult> IngestUnknownFaceAsync(Guid deviceId, DahuaAccessRecord record, TimeSpan debounceWindow, TimeZoneInfo eventTimeZone, string source = "dahua_cgi_polling", CancellationToken cancellationToken = default);
}
