using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Services;

public interface IDahuaAccessRecordIngestionPipeline
{
    Task IngestAsync(Guid deviceId, DahuaAccessRecord record, DahuaEventSource source, CancellationToken cancellationToken);
}
