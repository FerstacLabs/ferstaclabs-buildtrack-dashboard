namespace BuildTrack.Domain.Entities;

public enum FieldDailyReportStatus
{
    Draft,
    Submitted,
    Approved,
    NeedsCorrection,
    Rejected,
}

public enum FieldSiteNoteCategory
{
    Weather,
    MaterialDelay,
    Equipment,
    Labor,
    Safety,
    Quality,
    Access,
    Other,
}

public enum SupervisorWorkerEventType
{
    Late,
    LeftEarly,
    Absent,
    Permission,
    Medical,
    SiteTransfer,
    SafetyWarning,
    ManualAttendanceCorrectionRequest,
    Other,
}

public enum SupervisorWorkerEventStatus
{
    Submitted,
    Reviewed,
    Rejected,
}

public enum FieldWarehouseRequestStatus
{
    Draft,
    Submitted,
    UnderReview,
    NeedsJustification,
    PendingApproval,
    Approved,
    PartiallyApproved,
    Rejected,
    InFulfillment,
    ReadyForPickup,
    Issued,
    Closed,
    Cancelled,
}

public enum FieldWarehouseUrgency
{
    Normal,
    Urgent,
    Critical,
}

public sealed class SupervisorSiteAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class FieldSmetaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string WorkName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? WorkCategory { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class SupervisorDailyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public DateOnly ReportDate { get; set; }
    public string? Shift { get; set; }
    public FieldDailyReportStatus Status { get; set; } = FieldDailyReportStatus.Draft;
    public string? GeneralNote { get; set; }
    public string? WeatherCondition { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public ICollection<SupervisorDailyReportLine> Lines { get; set; } = new List<SupervisorDailyReportLine>();
}

public sealed class SupervisorDailyReportLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ReportId { get; set; }
    public SupervisorDailyReport? Report { get; set; }
    public Guid SmetaItemId { get; set; }
    public FieldSmetaItem? SmetaItem { get; set; }
    public decimal ReportedQuantity { get; set; }
    public int? WorkerCount { get; set; }
    public decimal? WorkHours { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SupervisorSiteNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public DateTimeOffset EventDateTime { get; set; } = DateTimeOffset.UtcNow;
    public FieldSiteNoteCategory Category { get; set; } = FieldSiteNoteCategory.Other;
    public string Text { get; set; } = string.Empty;
    public string? AttachmentPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SupervisorWorkerEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public SupervisorWorkerEventType EventType { get; set; }
    public DateTimeOffset EventDateTime { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public int RiskDelta { get; set; }
    public SupervisorWorkerEventStatus Status { get; set; } = SupervisorWorkerEventStatus.Submitted;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}

public sealed class FieldWarehouseCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAz { get; set; }
    public string? NameRu { get; set; }
    public string? NameEn { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Code { get; set; }
    public SupplyItemType ItemType { get; set; } = SupplyItemType.Other;
    public string? Description { get; set; }
    public string? SearchAliases { get; set; }
    public string? SpecificationSchemaJson { get; set; }
    public bool IsCustom { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class FieldWarehouseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid SupervisorUserId { get; set; }
    public AppUser? SupervisorUser { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateOnly? NeededBy { get; set; }
    public FieldWarehouseUrgency Urgency { get; set; } = FieldWarehouseUrgency.Normal;
    public string Reason { get; set; } = string.Empty;
    public string? GeneralNote { get; set; }
    public string? JustificationRequestNote { get; set; }
    public string? Justification { get; set; }
    public string? ManagerComment { get; set; }
    public bool AbnormalRequest { get; set; }
    public FieldWarehouseRequestStatus Status { get; set; } = FieldWarehouseRequestStatus.PendingApproval;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public ICollection<FieldWarehouseRequestLine> Lines { get; set; } = new List<FieldWarehouseRequestLine>();
}

public sealed class SupervisorAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? SupervisorUserId { get; set; }
    public string? SupervisorNameSnapshot { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool RiskFlag { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
}
