using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class SecurityEventService(
    BuildTrackDbContext db,
    ISecuritySnapshotStore snapshotStore,
    ILogger<SecurityEventService> logger) : ISecurityEventService
{
    public async Task<SecurityEventIngestionResult> IngestUnknownFaceAsync(
        Guid deviceId,
        DahuaAccessRecord record,
        TimeSpan debounceWindow,
        TimeZoneInfo eventTimeZone,
        string source = "dahua_cgi_polling",
        CancellationToken cancellationToken = default) =>
        await IngestFaceReviewEventAsync(deviceId, record, debounceWindow, eventTimeZone, source, cancellationToken);

    public async Task<SecurityEventIngestionResult> IngestFaceReviewEventAsync(
        Guid deviceId,
        DahuaAccessRecord record,
        TimeSpan debounceWindow,
        TimeZoneInfo eventTimeZone,
        string source = "dahua_cgi_polling",
        CancellationToken cancellationToken = default)
    {
        if (!DahuaSecurityReviewEventPolicy.IsFaceReviewEvent(record))
        {
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Ignored, Reason: "not face review event");
        }

        var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
        if (device is null)
        {
            logger.LogWarning("Face review security event ignored because device {DeviceId} was not found", deviceId);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Ignored, Reason: "device not found");
        }

        if (record.RecNo is not null)
        {
            var duplicate = await db.SecurityEvents.AnyAsync(x => x.DeviceId == deviceId && x.RawRecNo == record.RecNo, cancellationToken);
            if (duplicate)
            {
                return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Duplicate, Reason: "duplicate raw rec no");
            }
        }

        var eventType = DahuaSecurityReviewEventPolicy.ResolveEventType(record);
        var eventTime = string.Equals(source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)
            ? DateTimeOffset.UtcNow
            : record.CreateTime;
        var windowStart = eventTime - debounceWindow;
        var debounced = await db.SecurityEvents.AnyAsync(
            x => x.DeviceId == deviceId
                 && x.EventType == eventType
                 && x.EventTime >= windowStart
                 && x.EventTime <= eventTime,
            cancellationToken);
        if (debounced)
        {
            logger.LogInformation("Skipped face review event by debounce. EventType {EventType}, Device {DeviceId}, RecNo {RecNo}, EventTime {EventTime}, SnapshotPath {SnapshotPath}", eventType, deviceId, record.RecNo, eventTime, record.Url);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Debounced, Reason: "face review debounce");
        }

        var securityEvent = new SecurityEvent
        {
            TenantId = device.TenantId,
            SiteId = device.SiteId,
            DeviceId = device.Id,
            EventTime = eventTime,
            EventDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(eventTime, eventTimeZone).DateTime),
            EventType = eventType,
            Severity = SecurityEventSeverity.Warning,
            Status = SecurityEventStatus.Open,
            RawRecNo = record.RecNo,
            Method = record.NormalizedMethod.ToString(),
            Direction = record.NormalizedDirection.ToString(),
            SnapshotPath = record.Url,
            StoredSnapshotPath = IsLocalSmartEventSnapshot(record) ? record.Url : null,
            StoredSnapshotContentType = IsLocalSmartEventSnapshot(record) ? "image/jpeg" : null,
            SnapshotDownloadStatus = IsLocalSmartEventSnapshot(record) ? "Stored" : null,
            SnapshotSource = IsLocalSmartEventSnapshot(record) ? "NetSdkSmartEventImageBuffer" : null,
            ErrorCode = record.RawFields.GetValueOrDefault("ErrorCode"),
            Message = DahuaSecurityReviewEventPolicy.ResolveMessage(eventType),
            Source = source,
            RawPayloadJson = JsonSerializer.Serialize(record.RawFields),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.SecurityEvents.Add(securityEvent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateException(ex))
        {
            db.Entry(securityEvent).State = EntityState.Detached;
            logger.LogInformation(ex, "Duplicate face review security event ignored. EventType {EventType}, Device {DeviceId}, RecNo {RecNo}", eventType, deviceId, record.RecNo);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Duplicate, Reason: "duplicate database constraint");
        }

        logger.LogWarning("Created face review security event. Event {EventId}, EventType {EventType}, Device {DeviceId}, RecNo {RecNo}, SnapshotPath {SnapshotPath}", securityEvent.Id, eventType, deviceId, record.RecNo, record.Url);
        logger.LogInformation("Face review snapshot path: {SnapshotPath}", record.Url);

        if (IsLocalSmartEventSnapshot(record))
        {
            logger.LogInformation("Smart Event face review snapshot already stored locally. SecurityEventId={SecurityEventId}, StoredSnapshotPath={StoredSnapshotPath}", securityEvent.Id, securityEvent.StoredSnapshotPath);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Created, securityEvent);
        }

        var snapshotResult = await snapshotStore.TryStoreSnapshotAsync(securityEvent, cancellationToken);
        securityEvent.StoredSnapshotPath = snapshotResult.StoredPath;
        securityEvent.StoredSnapshotContentType = snapshotResult.ContentType;
        securityEvent.SnapshotDownloadStatus = snapshotResult.Status;
        securityEvent.SnapshotDownloadError = snapshotResult.Error;
        securityEvent.SnapshotSource = snapshotResult.Source;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Face review snapshot final status. SecurityEventId={SecurityEventId}, SnapshotDownloadStatus={SnapshotDownloadStatus}", securityEvent.Id, securityEvent.SnapshotDownloadStatus);

        return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Created, securityEvent);
    }

    private static bool IsDuplicateException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsLocalSmartEventSnapshot(DahuaAccessRecord record) =>
        record.RawFields.TryGetValue("SnapshotSource", out var source)
        && string.Equals(source, "NetSdkSmartEventImageBuffer", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(record.Url)
        && Path.IsPathRooted(record.Url);
}


