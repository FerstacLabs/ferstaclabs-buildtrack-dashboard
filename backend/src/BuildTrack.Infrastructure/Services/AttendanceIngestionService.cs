using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class AttendanceIngestionService(
    BuildTrackDbContext db,
    IAttendanceSessionService attendanceSessionService,
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

        if (!string.IsNullOrWhiteSpace(record.UserId))
        {
            var duplicateByBusinessKey = await db.AttendanceEvents.AnyAsync(
                x => x.DeviceId == device.Id
                     && x.WorkerExternalId == record.UserId
                     && x.EventTime == record.CreateTime
                     && x.Method == record.NormalizedMethod,
                cancellationToken);
            if (duplicateByBusinessKey)
            {
                logger.LogInformation("Duplicate Dahua event ignored by business key for device {DeviceId}, worker {WorkerExternalId}, time {EventTime}",
                    device.Id,
                    record.UserId,
                    record.CreateTime);
                return null;
            }
        }

        var worker = string.IsNullOrWhiteSpace(record.UserId)
            ? null
            : await db.Workers.FirstOrDefaultAsync(
                x => x.SiteId == device.SiteId && x.ExternalWorkerCode == record.UserId,
                cancellationToken);

        var attendanceEvent = new AttendanceEvent
        {
            TenantId = device.TenantId,
            SiteId = device.SiteId,
            DeviceId = device.Id,
            WorkerId = worker?.Id,
            WorkerExternalId = string.IsNullOrWhiteSpace(record.UserId) ? null : record.UserId,
            WorkerName = worker?.FullName ?? (!string.IsNullOrWhiteSpace(record.CardName) ? record.CardName : null),
            EventTime = record.CreateTime,
            Direction = record.NormalizedDirection,
            Status = record.NormalizedStatus,
            Method = record.NormalizedMethod,
            RawRecNo = record.RecNo,
            SnapshotPath = string.IsNullOrWhiteSpace(record.Url) ? null : record.Url,
            Source = source,
            RawPayloadJson = JsonSerializer.Serialize(record.RawFields),
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
}






