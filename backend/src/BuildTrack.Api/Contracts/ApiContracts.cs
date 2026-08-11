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

public sealed record FieldAssignmentDto(
    Guid Id,
    Guid SiteId,
    string SiteName,
    string? SiteAddress,
    Guid? ProjectId,
    bool IsActive,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record FieldMeResponse(
    AuthUserResponse User,
    TenantResponse Tenant,
    IReadOnlyList<FieldAssignmentDto> Assignments);

public sealed record FieldDashboardResponse(
    Guid SiteId,
    string SiteName,
    DateOnly WorkDate,
    string SupervisorName,
    int TodaySeenWorkers,
    int OpenWorkerNotes,
    int SubmittedReportsToday,
    int OpenWarehouseRequests,
    IReadOnlyList<FieldActivityDto> RecentActivity);

public sealed record FieldActivityDto(
    DateTimeOffset Timestamp,
    string Type,
    string Text,
    string? Status);

public sealed record FieldSmetaItemDto(
    Guid Id,
    Guid SiteId,
    string StageName,
    string WorkName,
    string Unit,
    string? WorkCategory);

public sealed record FieldDailyReportLineDto(
    Guid Id,
    Guid SmetaItemId,
    string StageName,
    string WorkName,
    decimal ReportedQuantity,
    int? WorkerCount,
    decimal? WorkHours,
    string Unit,
    string? Note);

public sealed record FieldDailyReportDto(
    Guid Id,
    Guid SiteId,
    string? SiteName,
    Guid SupervisorUserId,
    string? SupervisorName,
    DateOnly ReportDate,
    string? Shift,
    FieldDailyReportStatus Status,
    string? GeneralNote,
    string? WeatherCondition,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedByUserId,
    string? ReviewedByName,
    string? ReviewNote,
    IReadOnlyList<FieldDailyReportLineDto> Lines);

public sealed record SaveFieldDailyReportRequest(
    Guid SiteId,
    DateOnly ReportDate,
    string? Shift,
    string? GeneralNote,
    string? WeatherCondition,
    IReadOnlyList<SaveFieldDailyReportLineRequest> Lines);

public sealed record SaveFieldDailyReportLineRequest(
    Guid? Id,
    Guid SmetaItemId,
    decimal ReportedQuantity,
    int? WorkerCount,
    decimal? WorkHours,
    string? Note);

public sealed record ReviewFieldDailyReportRequest(FieldDailyReportStatus Status, string? ReviewNote);

public sealed record FieldWorkerDto(
    Guid Id,
    Guid SiteId,
    string ExternalWorkerCode,
    string FullName,
    string? Brigade,
    string? Role,
    string AttendanceState,
    DateTimeOffset? FirstSeen,
    DateTimeOffset? LastSeen,
    int WorkedMinutes,
    int RiskScore);

public sealed record FieldWorkerEventDto(
    Guid Id,
    Guid SiteId,
    Guid WorkerId,
    string WorkerName,
    Guid SupervisorUserId,
    string? SupervisorName,
    SupervisorWorkerEventType EventType,
    DateTimeOffset EventDateTime,
    string Reason,
    int RiskDelta,
    SupervisorWorkerEventStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CreateFieldWorkerEventRequest(
    Guid SiteId,
    Guid WorkerId,
    SupervisorWorkerEventType EventType,
    DateTimeOffset? EventDateTime,
    string Reason);

public sealed record FieldSiteNoteDto(
    Guid Id,
    Guid SiteId,
    string? SiteName,
    Guid SupervisorUserId,
    string? SupervisorName,
    DateTimeOffset EventDateTime,
    FieldSiteNoteCategory Category,
    string Text,
    DateTimeOffset CreatedAt);

public sealed record CreateFieldSiteNoteRequest(
    Guid SiteId,
    DateTimeOffset? EventDateTime,
    FieldSiteNoteCategory Category,
    string Text);

public sealed record FieldWarehouseCatalogItemDto(
    Guid Id,
    string Name,
    string Category,
    string Unit,
    string? Code);

public sealed record CatalogSearchItemDto(
    Guid Id,
    string Name,
    string? NameAz,
    string? NameRu,
    string? NameEn,
    string Category,
    string? Subcategory,
    string Unit,
    string? Code,
    SupplyItemType ItemType);

public sealed record SupplyUnitDto(
    Guid Id,
    string Code,
    string NameAz,
    string NameEn,
    string NameRu);

public sealed record FieldWarehouseRequestLineDto(
    Guid Id,
    Guid CatalogItemId,
    string ItemName,
    string Category,
    decimal RequestedQuantity,
    string Unit,
    string? Reason,
    FieldWarehouseRequestLineStatus Status);

public sealed record FieldWarehouseRequestDto(
    Guid Id,
    Guid SiteId,
    string? SiteName,
    Guid CatalogItemId,
    string ItemName,
    string Category,
    decimal RequestedQuantity,
    string Unit,
    DateOnly? NeededBy,
    FieldWarehouseUrgency Urgency,
    string Reason,
    string? JustificationRequestNote,
    string? Justification,
    string? ManagerComment,
    FieldWarehouseRequestStatus Status,
    Guid SupervisorUserId,
    string? SupervisorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Code = null,
    string? GeneralNote = null,
    bool AbnormalRequest = false,
    IReadOnlyList<FieldWarehouseRequestLineDto>? Lines = null);

public sealed record CreateFieldWarehouseRequest(
    Guid SiteId,
    Guid CatalogItemId,
    decimal RequestedQuantity,
    DateOnly? NeededBy,
    FieldWarehouseUrgency Urgency,
    string Reason,
    string? Justification);

public sealed record CreateFieldWarehouseCartRequest(
    Guid SiteId,
    DateOnly? NeededBy,
    FieldWarehouseUrgency Urgency,
    string? GeneralNote,
    IReadOnlyList<CreateFieldWarehouseCartLineRequest> Lines);

public sealed record CreateFieldWarehouseCartLineRequest(
    Guid CatalogItemId,
    decimal RequestedQuantity,
    string? Reason,
    string? SpecificationJson = null);

public sealed record ReviewFieldWarehouseRequest(FieldWarehouseRequestStatus Status, string? ManagerComment);

public sealed record SubmitFieldWarehouseJustificationRequest(string Justification);

public sealed record ManagementWarehouseLineDto(
    Guid Id,
    Guid CatalogItemId,
    string ItemName,
    string? Code,
    string Category,
    decimal RequestedQuantity,
    decimal ApprovedQuantity,
    decimal ReservedQuantity,
    decimal IssuedQuantity,
    decimal OnHandQuantity,
    decimal AvailableQuantity,
    decimal ShortfallQuantity,
    string Unit,
    string? Reason,
    FieldWarehouseRequestLineStatus Status);

public sealed record ManagementWarehouseRequestDto(
    Guid Id,
    string Code,
    Guid SiteId,
    string? SiteName,
    Guid SupervisorUserId,
    string? SupervisorName,
    DateOnly? NeededBy,
    FieldWarehouseUrgency Urgency,
    FieldWarehouseRequestStatus Status,
    string? GeneralNote,
    string? JustificationRequestNote,
    string? Justification,
    string? ManagerComment,
    bool AbnormalRequest,
    decimal TotalRequested,
    decimal TotalReserved,
    decimal TotalShortfall,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ManagementWarehouseLineDto> Lines);

public sealed record WarehouseStockItemDto(
    Guid CatalogItemId,
    string ItemName,
    string Category,
    string? Subcategory,
    string Unit,
    string? Code,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal IssuedQuantity,
    decimal MinimumQuantity,
    string StockStatus);

public sealed record ProcurementNeedDto(
    Guid Id,
    Guid SourceRequestId,
    Guid SourceRequestLineId,
    Guid CatalogItemId,
    string ItemName,
    string Category,
    decimal RequiredQuantity,
    decimal AlreadyAvailableQuantity,
    decimal ShortfallQuantity,
    decimal PurchasedQuantity,
    decimal ReceivedQuantity,
    string Unit,
    FieldWarehouseUrgency Priority,
    DateOnly? RequiredBy,
    ProcurementNeedStatus Status,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record ProcurementTaskLineDto(
    Guid Id,
    Guid ProcurementNeedId,
    Guid CatalogItemId,
    string ItemName,
    string Category,
    decimal RequestedQuantity,
    decimal PurchasedQuantity,
    decimal AcceptedQuantity,
    string Unit,
    ProcurementTaskLineStatus Status,
    string? Note,
    decimal? UnitPrice,
    Guid? SupplierId,
    string? SupplierName);

public sealed record ProcurementTaskDto(
    Guid Id,
    string Code,
    Guid? AssignedProcurementUserId,
    string? AssignedProcurementUserName,
    ProcurementTaskStatus Status,
    FieldWarehouseUrgency Priority,
    DateOnly? RequiredBy,
    string? ManagerInstruction,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? VerifiedAt,
    string? VerificationNote,
    IReadOnlyList<ProcurementTaskLineDto> Lines,
    IReadOnlyList<ProcurementAttachmentDto> Attachments);

public sealed record ProcurementAttachmentDto(
    Guid Id,
    Guid TaskId,
    Guid? TaskLineId,
    ProcurementAttachmentType AttachmentType,
    string OriginalFileName,
    string MimeType,
    long Size,
    DateTimeOffset CreatedAt,
    string DownloadUrl);

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Categories,
    SupplierStatus Status,
    string? Notes);

public sealed record SaveSupplierRequest(
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Categories,
    SupplierStatus Status = SupplierStatus.Active,
    string? Notes = null);

public sealed record ApproveProcurementNeedRequest(string? ManagerComment);

public sealed record AssignProcurementTaskRequest(IReadOnlyList<Guid> NeedIds, Guid? AssignedProcurementUserId, string? ManagerInstruction);

public sealed record UpdateProcurementTaskLinePurchaseRequest(decimal PurchasedQuantity, decimal? UnitPrice, Guid? SupplierId, string? Note);

public sealed record SubmitProcurementTaskRequest(string? Note);

public sealed record VerifyProcurementTaskRequest(string? VerificationNote);

public sealed record ReturnProcurementTaskForCorrectionRequest(string? Note);

public sealed record CreateGoodsReceiptRequest(Guid TaskId, Guid? WarehouseId, string? Note);

public sealed record IssueWarehouseRequest(Guid? WarehouseId, string? RecipientName, string? HandoverNote);

public sealed record ProcurementAgentDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    BuildTrackUserStatus Status,
    int OpenTasks,
    DateTimeOffset? LastLoginAt);

public sealed record CreateProcurementAgentRequest(string FullName, string Email, string? Phone, string TemporaryPassword);

public sealed record UpdateProcurementAgentRequest(string FullName, string? Phone, BuildTrackUserStatus Status);

public sealed record SupplyDashboardDto(
    int AssignedTasks,
    int ShoppingTasks,
    int SubmittedTasks,
    int UnreadNotifications,
    IReadOnlyList<ProcurementTaskDto> RecentTasks);

public sealed record SupplyNotificationDto(
    Guid Id,
    SupplyNotificationAudience Audience,
    string Title,
    string Message,
    string? ReferenceType,
    Guid? ReferenceId,
    SupplyNotificationStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ProcurementTraceDto(
    Guid FieldRequestId,
    string RequestCode,
    FieldWarehouseRequestStatus RequestStatus,
    IReadOnlyList<ManagementWarehouseLineDto> RequestLines,
    IReadOnlyList<ProcurementNeedDto> Needs,
    IReadOnlyList<ProcurementTaskDto> Tasks,
    IReadOnlyList<string> AuditTrail);

public sealed record SupervisorSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    BuildTrackUserStatus Status,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<FieldAssignmentDto> Assignments,
    int PendingDailyReports,
    int OpenWarehouseRequests,
    int RecentFieldEvents);

public sealed record CreateSupervisorRequest(
    string FullName,
    string Email,
    string? Phone,
    string TemporaryPassword,
    IReadOnlyList<Guid> SiteIds,
    string? Notes,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record UpdateSupervisorRequest(
    string FullName,
    string? Phone,
    BuildTrackUserStatus Status,
    IReadOnlyList<Guid> SiteIds,
    string? Notes,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record ResetSupervisorPasswordRequest(string TemporaryPassword);

public sealed record SupervisorAuditEventDto(
    Guid Id,
    Guid? SiteId,
    string? SiteName,
    Guid? SupervisorUserId,
    string? SupervisorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    DateTimeOffset Timestamp,
    bool RiskFlag,
    string Description);





