namespace BuildTrack.Domain.Entities;

public sealed class AttendanceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string? WorkerExternalId { get; set; }
    public string? WorkerName { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public AttendanceDirection Direction { get; set; } = AttendanceDirection.Unknown;
    public AttendanceEventStatus Status { get; set; } = AttendanceEventStatus.Failed;
    public AttendanceMethod Method { get; set; } = AttendanceMethod.Unknown;
    public long? RawRecNo { get; set; }
    public string? SnapshotPath { get; set; }
    public string Source { get; set; } = "dahua_terminal";
    public string RawPayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
