namespace BuildTrack.Domain.Entities;

public enum AttendanceSessionStatus
{
    Open,
    Closed
}

public sealed class AttendanceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string WorkerExternalId { get; set; } = string.Empty;
    public string? WorkerName { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid CheckInEventId { get; set; }
    public AttendanceEvent? CheckInEvent { get; set; }
    public DateTimeOffset CheckInTime { get; set; }
    public Guid? CheckOutEventId { get; set; }
    public AttendanceEvent? CheckOutEvent { get; set; }
    public DateTimeOffset? CheckOutTime { get; set; }
    public Guid? LastSeenEventId { get; set; }
    public AttendanceEvent? LastSeenEvent { get; set; }
    public DateTimeOffset? LastSeenTime { get; set; }
    public string? CloseReason { get; set; }
    public string? PresenceStatus { get; set; }
    public AttendanceSessionStatus Status { get; set; } = AttendanceSessionStatus.Open;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
