using BuildTrack.Domain.Entities;

namespace BuildTrack.Api.Contracts;

public sealed record CreateSiteRequest(string Name, string Address, string TimeZone);

public sealed record CreateWorkerRequest(
    Guid SiteId,
    string ExternalWorkerCode,
    string FullName,
    WorkerStatus Status = WorkerStatus.Active,
    string? Brigade = null,
    string? Role = null,
    decimal HourlyRate = 0,
    decimal PlannedDailyHours = 8,
    string AttendanceSource = "Manual",
    int RiskScore = 0,
    string? Notes = null,
    IReadOnlyList<SaveWorkerSiteAssignmentRequest>? SiteAssignments = null,
    SaveWorkerCameraIdentityRequest? CameraIdentity = null);

public sealed record UpdateWorkerRequest(
    Guid SiteId,
    string ExternalWorkerCode,
    string FullName,
    WorkerStatus Status = WorkerStatus.Active,
    string? Brigade = null,
    string? Role = null,
    decimal HourlyRate = 0,
    decimal PlannedDailyHours = 8,
    string AttendanceSource = "Manual",
    int RiskScore = 0,
    string? Notes = null,
    IReadOnlyList<SaveWorkerSiteAssignmentRequest>? SiteAssignments = null,
    SaveWorkerCameraIdentityRequest? CameraIdentity = null);

public sealed record SaveWorkerSiteAssignmentRequest(Guid SiteId, bool IsPrimary = false);

public sealed record SaveWorkerCameraIdentityRequest(
    Guid? DeviceId,
    string? ExternalUserId,
    string? CardName,
    bool IsPrimary = true);

public sealed record WorkerCameraIdentityResponse(
    Guid Id,
    Guid WorkerId,
    Guid? DeviceId,
    string? DeviceName,
    string Vendor,
    string? ExternalUserId,
    string? CardName,
    string? NormalizedCardName,
    bool IsPrimary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record WorkerPayrollSummaryResponse(
    double TodayCameraHours,
    decimal TodayEstimatedPay,
    decimal TodayEstimatedAmount,
    double MonthlyCameraHours,
    decimal MonthlyEstimatedPay,
    decimal MonthlyEstimatedAmount,
    bool IsCurrentlyActive,
    DateTimeOffset? CurrentSessionStartedAt,
    DateTimeOffset? LastSeenAt);

public sealed record WorkerSiteAssignmentResponse(
    Guid Id,
    Guid WorkerId,
    Guid SiteId,
    string? SiteName,
    bool IsPrimary,
    WorkerSiteAssignmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record WorkerResponse(
    Guid Id,
    Guid SiteId,
    string ExternalWorkerCode,
    string FullName,
    WorkerStatus Status,
    string? Brigade,
    string? Role,
    decimal HourlyRate,
    decimal PlannedDailyHours,
    string AttendanceSource,
    int RiskScore,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<WorkerCameraIdentityResponse> CameraIdentities,
    IReadOnlyList<WorkerSiteAssignmentResponse> SiteAssignments,
    WorkerPayrollSummaryResponse PayrollSummary);

public sealed record TestWorkerCameraIdentityRequest(Guid? DeviceId, string? ExternalUserId, string? CardName);

public sealed record TestWorkerCameraIdentityResponse(bool Matched, Guid? WorkerId, string? WorkerName, string? WorkerCode, string? ResolvedBy, string? Status, string? Reason);

public sealed record WorkerCameraIdentityRemapResponse(int AttendanceEventsUpdated, int AttendanceSessionsUpdated);

public sealed record LinkSecurityEventToWorkerRequest(Guid WorkerId, Guid? DeviceId = null, bool RemapRecent = true, string? ReviewNote = null);

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
    string? SnapshotPath,
    string? SnapshotUrl,
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
    string Source,
    AttendanceMethod? Method = null,
    string? SnapshotPath = null,
    string? SnapshotUrl = null);

public sealed record AttendanceSnapshotResponse(
    Guid Id,
    DateTimeOffset EventTime,
    string EventTimeLocal,
    string? SnapshotUrl,
    AttendanceMethod Method,
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
    string? SnapshotUrl,
    string? SnapshotDownloadStatus,
    string? SnapshotDownloadError,
    string? SnapshotSource,
    string? Message,
    long? RawRecNo,
    string? CameraExternalUserId = null,
    string? CameraCardName = null);

public sealed record ReviewSecurityEventRequest(SecurityEventStatus Status, string? ReviewNote);

public sealed record RegisterRequest(
    string CompanyName,
    string FullName,
    string Email,
    string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    AuthUserResponse User,
    TenantResponse Tenant,
    LicenseResponse? License);

public sealed record AuthUserResponse(
    Guid Id,
    Guid TenantId,
    string FullName,
    string Email,
    BuildTrackUserRole Role,
    BuildTrackUserStatus Status);

public sealed record TenantResponse(
    Guid Id,
    string CompanyName,
    string Code,
    TenantStatus Status);

public sealed record LicenseResponse(
    Guid Id,
    Guid TenantId,
    LicensePlan Plan,
    LicenseStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset? ExpiresAt,
    int? MaxProjects,
    int? MaxUsers,
    int? MaxCameras);

public sealed record AuthMeResponse(
    AuthUserResponse User,
    TenantResponse Tenant,
    LicenseResponse? License);

public sealed record ActivateLicenseRequest(string LicenseKey);

public sealed record CreateLicenseRequest(
    Guid TenantId,
    LicensePlan Plan,
    DateTimeOffset? ExpiresAt,
    int? MaxProjects,
    int? MaxUsers,
    int? MaxCameras);

public sealed record CreateLicenseResponse(string LicenseKey, LicenseResponse License);

public sealed record AdminTenantLicenseResponse(
    Guid TenantId,
    string CompanyName,
    string? OwnerEmail,
    TenantStatus TenantStatus,
    LicensePlan? LicensePlan,
    LicenseStatus? LicenseStatus,
    DateTimeOffset? ExpiresAt,
    int? MaxProjects,
    int? MaxUsers,
    int? MaxCameras,
    DateTimeOffset CreatedAt,
    Guid? LicenseId);

public sealed record AdminActivateTenantLicenseRequest(Guid? LicenseId);





