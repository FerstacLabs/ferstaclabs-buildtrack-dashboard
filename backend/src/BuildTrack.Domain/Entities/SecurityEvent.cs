namespace BuildTrack.Domain.Entities;

public enum SecurityEventType
{
    UnknownFace,
    SuspiciousRecognition,
    IdentityMismatch,
    IdentityMappingConflict,
    ParserUncertainSmartEvent
}

public enum SecurityEventSeverity
{
    Warning
}

public enum SecurityEventStatus
{
    Open,
    Reviewed,
    Ignored,
    AutoResolved
}

public sealed class SecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateOnly EventDate { get; set; }
    public SecurityEventType EventType { get; set; } = SecurityEventType.UnknownFace;
    public SecurityEventSeverity Severity { get; set; } = SecurityEventSeverity.Warning;
    public SecurityEventStatus Status { get; set; } = SecurityEventStatus.Open;
    public long? RawRecNo { get; set; }
    public string? Method { get; set; }
    public string? Direction { get; set; }
    public string? SnapshotPath { get; set; }
    public string? SnapshotUrl { get; set; }
    public string? StoredSnapshotPath { get; set; }
    public string? StoredSnapshotContentType { get; set; }
    public string? SnapshotDownloadStatus { get; set; }
    public string? SnapshotDownloadError { get; set; }
    public string? SnapshotSource { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public string Source { get; set; } = "dahua_cgi_polling";
    public string RawPayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

