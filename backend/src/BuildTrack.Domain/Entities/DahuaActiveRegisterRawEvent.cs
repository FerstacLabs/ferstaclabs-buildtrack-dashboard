namespace BuildTrack.Domain.Entities;

public sealed class DahuaActiveRegisterRawEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string? RegisterDeviceId { get; set; }
    public string? RemoteIp { get; set; }
    public int? RemotePort { get; set; }
    public int ListenerPort { get; set; }
    public int CallbackCommand { get; set; }
    public string? CallbackCommandName { get; set; }
    public int PayloadBytes { get; set; }
    public string? PayloadFirstBytesHex { get; set; }
    public string? PayloadBase64 { get; set; }
    public string DecodeStatus { get; set; } = "RawSaved";
    public string? DecodedJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
