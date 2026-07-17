using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public interface IDahuaActiveRegisterSdk
{
    bool IsRealSdkAvailable { get; }
    bool IsSdkListenerActive { get; }
    string DecodeStatus { get; }
    string StartupWarning { get; }
    DahuaNetSdkDiagnostics Diagnostics { get; }
    Task StartAsync(IEnumerable<int> ports, CancellationToken cancellationToken);
    Task<object> RunRecordQueryDiagnosticAsync(Guid deviceId, int maxRecords, CancellationToken cancellationToken);
}

public interface IDahuaEventSubscriber
{
    Task SubscribeAsync(string registerDeviceId, CancellationToken cancellationToken);
}

public interface IDahuaRecordReader
{
    Task<IReadOnlyList<DahuaAccessRecord>> ReadRecordsAsync(Guid deviceId, CancellationToken cancellationToken);
}
