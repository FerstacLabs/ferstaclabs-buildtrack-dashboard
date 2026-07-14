using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class DeviceConnectionLogger(
    BuildTrackDbContext db,
    ILogger<DeviceConnectionLogger> logger) : IDeviceConnectionLogger
{
    public async Task<DeviceConnectionLog> LogAsync(
        Guid? deviceId,
        string? registerDeviceId,
        string? remoteIp,
        int? remotePort,
        string eventType,
        string message,
        object? raw = null,
        CancellationToken cancellationToken = default)
    {
        var item = new DeviceConnectionLog
        {
            DeviceId = deviceId,
            RegisterDeviceId = registerDeviceId,
            RemoteIp = remoteIp,
            RemotePort = remotePort,
            EventType = eventType,
            Message = message,
            RawPayloadJson = ToSafeJson(raw),
        };

        try
        {
            db.DeviceConnectionLogs.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            db.Entry(item).State = EntityState.Detached;
            logger.LogError(ex, "Failed to save device connection log. EventType {EventType}, RegisterDeviceId {RegisterDeviceId}, Remote {RemoteIp}:{RemotePort}",
                eventType,
                registerDeviceId,
                remoteIp,
                remotePort);
        }

        logger.LogInformation("Device connection log: {EventType} {RegisterDeviceId} {RemoteIp}:{RemotePort} {Message}",
            eventType,
            registerDeviceId,
            remoteIp,
            remotePort,
            message);
        return item;
    }

    private static string ToSafeJson(object? raw)
    {
        if (raw is null) return "{}";

        try
        {
            var json = JsonSerializer.Serialize(raw);
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch
        {
            return JsonSerializer.Serialize(new { serializationError = "Raw payload could not be serialized safely" });
        }
    }
}
