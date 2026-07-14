namespace BuildTrack.Domain.Entities;

public sealed class DeviceConnectionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string? RegisterDeviceId { get; set; }
    public string? RemoteIp { get; set; }
    public int? RemotePort { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawPayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
