using BuildTrack.Domain.Dahua;
using BuildTrack.Infrastructure.Dahua;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class DahuaAccessRecordIngestionPipeline(
    IConfiguration configuration,
    IAttendanceIngestionService attendanceIngestion,
    ISecurityEventService securityEvents,
    ILogger<DahuaAccessRecordIngestionPipeline> logger) : IDahuaAccessRecordIngestionPipeline
{
    public async Task IngestAsync(Guid deviceId, DahuaAccessRecord record, DahuaEventSource source, CancellationToken cancellationToken)
    {
        var sourceName = source.ToSourceString();
        if (DahuaUnknownFacePolicy.IsUnknownFace(record))
        {
            var debounceWindow = TimeSpan.FromSeconds(ParsePositiveInt(configuration["DAHUA_UNKNOWN_FACE_DEBOUNCE_SECONDS"], 30));
            var eventTimeZone = ResolveTimeZone(configuration["DAHUA_CGI_DEVICE_TIMEZONE"] ?? configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
            var result = await securityEvents.IngestUnknownFaceAsync(deviceId, record, debounceWindow, eventTimeZone, sourceName, cancellationToken);
            logger.LogInformation("Dahua access record pipeline processed unknown face. Source {Source}, Device {DeviceId}, RecNo {RecNo}, Result {Result}, Reason {Reason}", sourceName, deviceId, record.RecNo, result.Status, result.Reason);
            return;
        }

        if (DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record))
        {
            var inserted = await attendanceIngestion.IngestDahuaRecordAsync(
                deviceId,
                record,
                cancellationToken: cancellationToken,
                source: sourceName,
                requireSuccessfulAttendance: true);
            logger.LogInformation("Dahua access record pipeline processed attendance record. Source {Source}, Device {DeviceId}, WorkerExternalId {WorkerExternalId}, RecNo {RecNo}, Inserted {Inserted}", sourceName, deviceId, record.UserId, record.RecNo, inserted is not null);
            return;
        }

        logger.LogInformation("Dahua access record pipeline ignored non-attendance record. Source {Source}, Device {DeviceId}, Status {Status}, Method {Method}, RecNo {RecNo}", sourceName, deviceId, record.NormalizedStatus, record.NormalizedMethod, record.RecNo);
    }

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;

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
}
