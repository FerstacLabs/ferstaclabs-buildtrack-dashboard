using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IDeviceConnectionLogger
{
    Task<DeviceConnectionLog> LogAsync(
        Guid? deviceId,
        string? registerDeviceId,
        string? remoteIp,
        int? remotePort,
        string eventType,
        string message,
        object? raw = null,
        CancellationToken cancellationToken = default);
}
