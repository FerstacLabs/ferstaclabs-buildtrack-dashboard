using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class AttendanceIngestionService(
    BuildTrackDbContext db,
    IAttendanceSessionService attendanceSessionService,
    IWorkerCameraIdentityResolver workerCameraIdentityResolver,
    IWorkerSiteAssignmentService workerSiteAssignmentService,
    IConfiguration configuration,
    ILogger<AttendanceIngestionService> logger) : IAttendanceIngestionService
{
    public async Task<AttendanceEvent?> IngestDahuaRecordAsync(
        Guid deviceId,
        DahuaAccessRecord record,
        string? remoteIp = null,
        int? remotePort = null,
        CancellationToken cancellationToken = default,
        string source = "dahua_terminal",
        bool requireSuccessfulAttendance = false)
    {
        var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
        if (device is null)
        {
            logger.LogWarning("Dahua event ignored because device {DeviceId} was not found", deviceId);
            return null;
        }

        if (requireSuccessfulAttendance && !DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record))
        {
            logger.LogInformation("Dahua NetSDK event skipped for attendance. Device {DeviceId}, UserId {UserId}, Status {Status}, Method {Method}",
                device.Id,
                record.UserId,
                record.NormalizedStatus,
                record.NormalizedMethod);
            return null;
        }

        if (record.RecNo is not null)
        {
            var duplicateByRecNo = await db.AttendanceEvents.AnyAsync(
                x => x.DeviceId == device.Id && x.RawRecNo == record.RecNo,
                cancellationToken);
            if (duplicateByRecNo)
            {
                logger.LogInformation("Duplicate Dahua event ignored by RecNo {RecNo} for device {DeviceId}", record.RecNo, device.Id);
                return null;
            }
        }

        var resolution = await workerCameraIdentityResolver.ResolveAsync(device, record, cancellationToken);
        var worker = resolution.Worker;
        if (worker is null && IsDahuaCameraSource(source) && IsRecognizedCameraRecord(record))
        {
            await CreateUnmappedCameraIdentitySecurityEventAsync(device, record, source, resolution.Reason, cancellationToken);
            logger.LogWarning(
                "Dahua recognized camera record blocked from payroll because no worker-camera identity mapping exists. Device {DeviceId}, DahuaUserId {DahuaUserId}, CardName {CardName}, RecNo {RecNo}",
                device.Id,
                record.UserId,
                record.CardName,
                record.RecNo);
            return null;
        }

        var effectiveWorkerExternalId = worker?.ExternalWorkerCode ?? (string.IsNullOrWhiteSpace(record.UserId) ? null : record.UserId.Trim());
        var effectiveWorkerName = worker?.FullName ?? (!string.IsNullOrWhiteSpace(record.CardName) ? record.CardName : null);
        if (worker is not null && IsDahuaCameraSource(source))
        {
            await workerSiteAssignmentService.EnsureAssignmentAsync(
                worker.Id,
                device.SiteId,
                "Worker auto-assigned to site from camera attendance",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(effectiveWorkerExternalId))
        {
            var duplicateByBusinessKey = await db.AttendanceEvents.AnyAsync(
                x => x.DeviceId == device.Id
                     && x.WorkerExternalId == effectiveWorkerExternalId
                     && x.EventTime == record.CreateTime
                     && x.Method == record.NormalizedMethod,
                cancellationToken);
            if (duplicateByBusinessKey)
            {
                logger.LogInformation("Duplicate Dahua event ignored by business key for device {DeviceId}, worker {WorkerExternalId}, time {EventTime}",
                    device.Id,
                    effectiveWorkerExternalId,
                    record.CreateTime);
                return null;
            }
        }

        var attendanceEvent = new AttendanceEvent
        {
            TenantId = device.TenantId,
            SiteId = device.SiteId,
            DeviceId = device.Id,
            WorkerId = worker?.Id,
            WorkerExternalId = effectiveWorkerExternalId,
            WorkerName = effectiveWorkerName,
            EventTime = record.CreateTime,
            Direction = record.NormalizedDirection,
            Status = record.NormalizedStatus,
            Method = record.NormalizedMethod,
            RawRecNo = record.RecNo,
            SnapshotPath = string.IsNullOrWhiteSpace(record.Url) ? null : record.Url,
            Source = source,
            RawPayloadJson = JsonSerializer.Serialize(BuildCanonicalRawPayload(record, worker, resolution)),
        };

        db.AttendanceEvents.Add(attendanceEvent);
        device.Status = DeviceStatus.Online;
        device.LastKnownIp = remoteIp ?? device.LastKnownIp;
        device.LastSeenAt = DateTimeOffset.UtcNow;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        if (DahuaCgiCursorPolicy.ShouldAdvanceCgiCursor(source, record.RecNo, device.CgiLastRecNo))
        {
            device.CgiLastRecNo = record.RecNo;
            device.LastRecNo = record.RecNo;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateException(ex))
        {
            db.Entry(attendanceEvent).State = EntityState.Detached;
            logger.LogInformation(ex, "Duplicate Dahua event ignored by database constraint for device {DeviceId}, RecNo {RecNo}", device.Id, record.RecNo);
            return null;
        }

        await attendanceSessionService.ProcessEventAsync(attendanceEvent, cancellationToken);
        await AutoResolveSupersededFaceReviewEventsAsync(attendanceEvent, cancellationToken);

        logger.LogInformation("Attendance event inserted. Device {DeviceId}, WorkerExternalId {WorkerExternalId}, Status {Status}, Method {Method}, RecNo {RecNo}, Source {Source}",
            device.Id,
            attendanceEvent.WorkerExternalId,
            attendanceEvent.Status,
            attendanceEvent.Method,
            attendanceEvent.RawRecNo,
            attendanceEvent.Source);

        return attendanceEvent;
    }

    private static bool IsDuplicateException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;

    private async Task CreateUnmappedCameraIdentitySecurityEventAsync(Device device, DahuaAccessRecord record, string source, string? reason, CancellationToken cancellationToken)
    {
        if (record.RecNo is not null)
        {
            var duplicate = await db.SecurityEvents.AnyAsync(x => x.DeviceId == device.Id && x.RawRecNo == record.RecNo, cancellationToken);
            if (duplicate) return;
        }

        var timeZone = ResolveTimeZone(configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
        var eventTime = string.Equals(source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)
            ? DateTimeOffset.UtcNow
            : record.CreateTime;
        var rawFields = new Dictionary<string, string?>(record.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "UnmappedCameraIdentity",
            ["ClassificationReason"] = reason ?? "Recognized camera identity is not linked to a worker profile",
            ["DahuaUserID"] = record.UserId,
            ["CameraUserID"] = record.UserId,
            ["DahuaCardName"] = record.CardName,
            ["ReceivedCardName"] = record.RawFields.GetValueOrDefault("ReceivedCardName") ?? record.CardName,
            ["WorkerResolutionStatus"] = "UnmappedCameraIdentity",
            ["WorkerResolved"] = "false",
        };

        var securityEvent = new SecurityEvent
        {
            TenantId = device.TenantId,
            SiteId = device.SiteId,
            DeviceId = device.Id,
            EventTime = eventTime,
            EventDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(eventTime, timeZone).DateTime),
            EventType = SecurityEventType.UnmappedCameraIdentity,
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
            Message = DahuaSecurityReviewEventPolicy.ResolveMessage(SecurityEventType.UnmappedCameraIdentity),
            Source = source,
            RawPayloadJson = JsonSerializer.Serialize(rawFields),
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
            logger.LogInformation(ex, "Duplicate unmapped camera identity security event ignored. Device {DeviceId}, RecNo {RecNo}", device.Id, record.RecNo);
        }
    }

    private static Dictionary<string, string?> BuildCanonicalRawPayload(DahuaAccessRecord record, Worker? worker, WorkerCameraIdentityResolution resolution)
    {
        var rawFields = new Dictionary<string, string?>(record.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["DahuaUserID"] = record.UserId,
            ["CameraUserID"] = record.RawFields.GetValueOrDefault("CameraUserID") ?? record.UserId,
            ["DahuaCardName"] = record.RawFields.GetValueOrDefault("ReceivedCardName") ?? record.RawFields.GetValueOrDefault("TrustedCardName") ?? record.CardName,
            ["WorkerResolutionStatus"] = resolution.Status,
            ["IdentityResolvedBy"] = resolution.ResolvedBy,
        };

        if (worker is not null)
        {
            rawFields["WorkerID"] = worker.Id.ToString();
            rawFields["WorkerExternalId"] = worker.ExternalWorkerCode;
            rawFields["ResolvedWorkerExternalId"] = worker.ExternalWorkerCode;
            rawFields["ResolvedWorkerName"] = worker.FullName;
            rawFields["CardName"] = worker.FullName;
            rawFields["WorkerResolved"] = "true";
        }

        if (resolution.Identity is not null)
        {
            rawFields["WorkerCameraIdentityId"] = resolution.Identity.Id.ToString();
        }

        return rawFields;
    }

    private static bool IsDahuaCameraSource(string source) =>
        source.StartsWith("dahua_", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecognizedCameraRecord(DahuaAccessRecord record) =>
        record.NormalizedStatus == AttendanceEventStatus.Ok
        && !string.IsNullOrWhiteSpace(record.UserId)
        && !string.IsNullOrWhiteSpace(record.CardName);

    private static bool IsLocalSmartEventSnapshot(DahuaAccessRecord record) =>
        record.RawFields.TryGetValue("SnapshotSource", out var snapshotSource)
        && string.Equals(snapshotSource, "NetSdkSmartEventImageBuffer", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(record.Url)
        && Path.IsPathRooted(record.Url);

    private async Task AutoResolveSupersededFaceReviewEventsAsync(AttendanceEvent attendanceEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(attendanceEvent.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)) return;
        if (attendanceEvent.Status != AttendanceEventStatus.Ok || string.IsNullOrWhiteSpace(attendanceEvent.WorkerExternalId)) return;
        if (!DahuaVerifiedAttendancePayload.IsVerifiedAttendance(attendanceEvent)) return;

        var window = TimeSpan.FromSeconds(ParsePositiveInt(configuration["DAHUA_PARSER_UNCERTAIN_AUTO_RESOLVE_SECONDS"], 90));
        var windowStart = attendanceEvent.CreatedAt - window;
        var windowEnd = attendanceEvent.CreatedAt.AddSeconds(5);
        var candidates = await db.SecurityEvents
            .Where(x => x.TenantId == attendanceEvent.TenantId
                        && x.DeviceId == attendanceEvent.DeviceId
                        && x.Status == SecurityEventStatus.Open
                        && (x.EventType == SecurityEventType.ParserUncertainSmartEvent
                            || x.EventType == SecurityEventType.SuspiciousRecognition)
                        && x.CreatedAt >= windowStart
                        && x.CreatedAt <= windowEnd)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        const string autoResolveNote = "Avtomatik bağlandı: eyni cihazdan təsdiqlənmiş davamiyyət qeydi alındı.";
        var resolved = 0;
        var cameraUserId = ExtractCameraUserId(attendanceEvent.RawPayloadJson);
        foreach (var securityEvent in candidates.Where(x => RawPayloadHasWorkerExternalId(x.RawPayloadJson, attendanceEvent.WorkerExternalId)
                                                            || (!string.IsNullOrWhiteSpace(cameraUserId) && RawPayloadHasWorkerExternalId(x.RawPayloadJson, cameraUserId))))
        {
            securityEvent.Status = SecurityEventStatus.AutoResolved;
            securityEvent.ReviewedAt = now;
            securityEvent.ReviewNote = autoResolveNote;
            securityEvent.Message = autoResolveNote;
            resolved++;
        }

        if (resolved == 0) return;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Auto-resolved {Count} parser-uncertain security events after verified attendance. Device {DeviceId}, WorkerExternalId {WorkerExternalId}", resolved, attendanceEvent.DeviceId, attendanceEvent.WorkerExternalId);
    }

    private static bool RawPayloadHasWorkerExternalId(string rawPayloadJson, string workerExternalId)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            var root = document.RootElement;
            return string.Equals(GetJsonString(root, "UserID"), workerExternalId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(GetJsonString(root, "UserId"), workerExternalId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(GetJsonString(root, "WorkerExternalId"), workerExternalId, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractCameraUserId(string rawPayloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            var root = document.RootElement;
            return GetJsonString(root, "DahuaUserID")
                   ?? GetJsonString(root, "CameraUserID")
                   ?? GetJsonString(root, "UserID")
                   ?? GetJsonString(root, "UserId");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetJsonString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (InvalidTimeZoneException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
    }

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
}






