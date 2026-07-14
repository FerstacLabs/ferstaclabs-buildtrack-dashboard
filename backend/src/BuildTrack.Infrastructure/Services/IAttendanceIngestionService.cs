using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IAttendanceIngestionService
{
    Task<AttendanceEvent?> IngestDahuaRecordAsync(
        Guid deviceId,
        DahuaAccessRecord record,
        string? remoteIp = null,
        int? remotePort = null,
        CancellationToken cancellationToken = default,
        string source = "dahua_terminal",
        bool requireSuccessfulAttendance = false);
}
