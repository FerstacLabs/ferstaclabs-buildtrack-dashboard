using BuildTrack.Domain.Entities;

namespace BuildTrack.Api.Contracts;

public sealed record CreateSiteRequest(string Name, string Address, string TimeZone);

public sealed record CreateWorkerRequest(Guid SiteId, string ExternalWorkerCode, string FullName, WorkerStatus Status = WorkerStatus.Active);

public sealed record CreateDeviceRequest(
    Guid SiteId,
    string Name,
    string RegisterDeviceId,
    int RegisterPort,
    string Username,
    string Password,
    DeviceMode Mode = DeviceMode.ActiveRegister,
    string Vendor = "dahua",
    string Model = "DHI-ASI6213J-MW");

public sealed record DeviceResponse(
    Guid Id,
    Guid SiteId,
    string Name,
    string Vendor,
    string Model,
    DeviceMode Mode,
    string RegisterDeviceId,
    int RegisterPort,
    string? LastKnownIp,
    DateTimeOffset? LastSeenAt,
    DeviceStatus Status,
    long? LastRecNo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastEventAt,
    string? LastEventWorkerName,
    string NetSdkDecodeStatus);

public sealed record AttendanceEventResponse(
    Guid Id,
    Guid SiteId,
    string? SiteName,
    Guid DeviceId,
    string? DeviceName,
    string? WorkerExternalId,
    string? WorkerName,
    DateTimeOffset EventTime,
    AttendanceDirection Direction,
    AttendanceEventStatus Status,
    AttendanceMethod Method,
    long? RawRecNo,
    string Source,
    DateTimeOffset CreatedAt);

public sealed record SimulateEventRequest(
    string? WorkerExternalId,
    string? WorkerName,
    DateTimeOffset? EventTime,
    string Status = "1",
    string Method = "15",
    string Direction = "Entry",
    long? RecNo = null);
public sealed record AttendanceLiveStatusResponse(
    DateOnly WorkDate,
    int ActiveWorkersCount,
    IReadOnlyList<AttendanceLiveWorkerResponse> Workers,
    int StaleOpenSessionsCount = 0);

public sealed record AttendanceLiveWorkerResponse(
    string WorkerExternalId,
    string? WorkerName,
    DateTimeOffset CheckInTime,
    string CheckInTimeLocal,
    DateTimeOffset? LastSeenTime,
    string? LastSeenTimeLocal,
    DateTimeOffset? ConfirmedCheckOutTime,
    string? ConfirmedCheckOutTimeLocal,
    string? CloseReason,
    string DisplayStatus,
    bool IsCheckoutConfirmed,
    int WorkedMinutesSoFar,
    AttendanceSessionStatus Status);

public sealed record AttendanceDailyResponse(
    DateOnly WorkDate,
    int TotalWorkersCheckedIn,
    int ActiveWorkersCount,
    int ClosedSessionsCount,
    double TotalWorkedHours,
    IReadOnlyList<AttendanceSessionResponse> Sessions);

public sealed record AttendanceSessionResponse(
    Guid Id,
    string WorkerExternalId,
    string? WorkerName,
    DateTimeOffset CheckInTime,
    DateTimeOffset? CheckOutTime,
    string CheckInTimeLocal,
    string? CheckOutTimeLocal,
    DateTimeOffset? LastSeenTime,
    string? LastSeenTimeLocal,
    DateTimeOffset? ConfirmedCheckOutTime,
    string? ConfirmedCheckOutTimeLocal,
    string? CloseReason,
    string DisplayStatus,
    bool IsCheckoutConfirmed,
    int WorkedMinutes,
    AttendanceSessionStatus Status,
    string Source);
public sealed record SecurityEventResponse(
    Guid Id,
    DateTimeOffset EventTime,
    string EventTimeLocal,
    SecurityEventType EventType,
    SecurityEventSeverity Severity,
    SecurityEventStatus Status,
    string? DeviceName,
    string? SiteName,
    string? SnapshotPath,
    string SnapshotUrl,
    string? SnapshotDownloadStatus,
    string? SnapshotDownloadError,
    string? SnapshotSource,
    string? Message,
    long? RawRecNo);

public sealed record ReviewSecurityEventRequest(SecurityEventStatus Status, string? ReviewNote);





