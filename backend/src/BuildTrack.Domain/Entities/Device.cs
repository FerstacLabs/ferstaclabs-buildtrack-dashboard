namespace BuildTrack.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = "dahua";
    public string Model { get; set; } = "DHI-ASI6213J-MW";
    public DeviceMode Mode { get; set; } = DeviceMode.ActiveRegister;
    public string RegisterDeviceId { get; set; } = string.Empty;
    public int RegisterPort { get; set; } = 9500;
    public string? LastKnownIp { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Pending;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public long? LastRecNo { get; set; }
    public long? CgiLastRecNo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AttendanceEvent> AttendanceEvents { get; set; } = new List<AttendanceEvent>();
    public ICollection<DeviceConnectionLog> ConnectionLogs { get; set; } = new List<DeviceConnectionLog>();
}



