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
        CancellationToken cancellationToken = default)
    {
        if (!DahuaUnknownFacePolicy.IsUnknownFace(record))
        {
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Ignored, Reason: "not unknown face");
        }

        var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
        if (device is null)
        {
            logger.LogWarning("Unknown face security event ignored because device {DeviceId} was not found", deviceId);
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

        var windowStart = record.CreateTime - debounceWindow;
        var debounced = await db.SecurityEvents.AnyAsync(
            x => x.DeviceId == deviceId
                 && x.EventType == SecurityEventType.UnknownFace
                 && x.EventTime >= windowStart
                 && x.EventTime <= record.CreateTime,
            cancellationToken);
        if (debounced)
        {
            logger.LogInformation("Skipped unknown face by debounce. Device {DeviceId}, RecNo {RecNo}, EventTime {EventTime}, SnapshotPath {SnapshotPath}", deviceId, record.RecNo, record.CreateTime, record.Url);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Debounced, Reason: "unknown face debounce");
        }

        var securityEvent = new SecurityEvent
        {
            SiteId = device.SiteId,
            DeviceId = device.Id,
            EventTime = record.CreateTime,
            EventDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(record.CreateTime, eventTimeZone).DateTime),
            EventType = SecurityEventType.UnknownFace,
            Severity = SecurityEventSeverity.Warning,
            Status = SecurityEventStatus.Open,
            RawRecNo = record.RecNo,
            Method = record.NormalizedMethod.ToString(),
            Direction = record.NormalizedDirection.ToString(),
            SnapshotPath = record.Url,
            ErrorCode = record.RawFields.GetValueOrDefault("ErrorCode"),
            Message = "Tanınmayan üz aşkarlandı",
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
            logger.LogInformation(ex, "Duplicate unknown face security event ignored. Device {DeviceId}, RecNo {RecNo}", deviceId, record.RecNo);
            return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Duplicate, Reason: "duplicate database constraint");
        }

        logger.LogWarning("Created unknown face security event. Event {EventId}, Device {DeviceId}, RecNo {RecNo}, SnapshotPath {SnapshotPath}", securityEvent.Id, deviceId, record.RecNo, record.Url);
        logger.LogInformation("Unknown face snapshot path: {SnapshotPath}", record.Url);

        var snapshotResult = await snapshotStore.TryStoreSnapshotAsync(securityEvent, cancellationToken);
        securityEvent.StoredSnapshotPath = snapshotResult.StoredPath;
        securityEvent.StoredSnapshotContentType = snapshotResult.ContentType;
        securityEvent.SnapshotDownloadStatus = snapshotResult.Status;
        securityEvent.SnapshotDownloadError = snapshotResult.Error;
        securityEvent.SnapshotSource = snapshotResult.Source;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("UnknownFace snapshot final status. SecurityEventId={SecurityEventId}, SnapshotDownloadStatus={SnapshotDownloadStatus}", securityEvent.Id, securityEvent.SnapshotDownloadStatus);

        return new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Created, securityEvent);
    }

    private static bool IsDuplicateException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;
}


