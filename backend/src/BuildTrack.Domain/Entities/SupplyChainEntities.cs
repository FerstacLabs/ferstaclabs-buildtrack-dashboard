namespace BuildTrack.Domain.Entities;

public enum SupplyItemType
{
    ConstructionMaterial,
    PPE,
    Tool,
    Consumable,
    Equipment,
    Electrical,
    Plumbing,
    HVAC,
    Finishing,
    Formwork,
    Steel,
    Concrete,
    Fastener,
    Chemical,
    Fuel,
    Other,
}

public enum FieldWarehouseRequestLineStatus
{
    Pending,
    StockAvailable,
    Reserved,
    NeedsProcurement,
    ProcurementInProgress,
    Received,
    ReadyForIssue,
    Issued,
    Rejected,
}

public enum WarehouseReservationStatus
{
    Active,
    Consumed,
    Released,
    Expired,
}

public enum WarehouseStockMovementType
{
    OpeningBalance,
    PurchaseReceipt,
    Issue,
    Return,
    TransferIn,
    TransferOut,
    AdjustmentIncrease,
    AdjustmentDecrease,
    WriteOff,
}

public enum ProcurementNeedStatus
{
    PendingApproval,
    Approved,
    Assigned,
    InPurchase,
    PartiallyPurchased,
    Purchased,
    AwaitingReceipt,
    Received,
    Cancelled,
}

public enum ProcurementTaskStatus
{
    Draft,
    Assigned,
    Accepted,
    Shopping,
    PartiallyCompleted,
    Completed,
    SubmittedForVerification,
    Verified,
    RejectedForCorrection,
    Cancelled,
}

public enum ProcurementTaskLineStatus
{
    Pending,
    Searching,
    PartiallyPurchased,
    Purchased,
    Unavailable,
    SubstitutionProposed,
    Received,
    Rejected,
}

public enum ProcurementAttachmentType
{
    ProductPhoto,
    Receipt,
    Invoice,
    DeliveryNote,
    Other,
}

public enum SupplierStatus
{
    Active,
    Proposed,
    Suspended,
}

public enum WarehouseGoodsReceiptStatus
{
    Draft,
    Verified,
    Cancelled,
}

public enum WarehouseGoodsReceiptLineCondition
{
    Accepted,
    Damaged,
    WrongItem,
    Partial,
    Rejected,
}

public enum WarehouseIssueStatus
{
    Draft,
    Issued,
    Cancelled,
}

public enum SupplyNotificationAudience
{
    Field,
    Procurement,
    Management,
    Warehouse,
}

public enum SupplyNotificationStatus
{
    Unread,
    Read,
}

public sealed class SupplyUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string NameAz { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class FieldWarehouseRequestLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public FieldWarehouseRequest? Request { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? SpecificationJson { get; set; }
    public FieldWarehouseRequestLineStatus Status { get; set; } = FieldWarehouseRequestLineStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class WarehouseReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public Guid RequestLineId { get; set; }
    public FieldWarehouseRequestLine? RequestLine { get; set; }
    public decimal Quantity { get; set; }
    public WarehouseReservationStatus Status { get; set; } = WarehouseReservationStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class WarehouseStockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public WarehouseStockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
}

public sealed class WarehouseUsagePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string? Category { get; set; }
    public decimal? DefaultMaximumPerRequest { get; set; }
    public decimal? DefaultMaximumPerWorker { get; set; }
    public decimal? DefaultMaximumPerSitePeriod { get; set; }
    public int PeriodDays { get; set; } = 30;
    public bool RequireJustificationAboveThreshold { get; set; } = true;
    public int RiskWeight { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProcurementNeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid SourceRequestId { get; set; }
    public FieldWarehouseRequest? SourceRequest { get; set; }
    public Guid SourceRequestLineId { get; set; }
    public FieldWarehouseRequestLine? SourceRequestLine { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal AlreadyAvailableQuantity { get; set; }
    public decimal ShortfallQuantity { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public FieldWarehouseUrgency Priority { get; set; } = FieldWarehouseUrgency.Normal;
    public DateOnly? RequiredBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ProcurementNeedStatus Status { get; set; } = ProcurementNeedStatus.PendingApproval;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProcurementTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? AssignedProcurementUserId { get; set; }
    public AppUser? AssignedProcurementUser { get; set; }
    public ProcurementTaskStatus Status { get; set; } = ProcurementTaskStatus.Draft;
    public FieldWarehouseUrgency Priority { get; set; } = FieldWarehouseUrgency.Normal;
    public DateOnly? RequiredBy { get; set; }
    public string? ManagerInstruction { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerificationNote { get; set; }
    public ICollection<ProcurementTaskLine> Lines { get; set; } = new List<ProcurementTaskLine>();
}

public sealed class ProcurementTaskLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public ProcurementTask? Task { get; set; }
    public Guid ProcurementNeedId { get; set; }
    public ProcurementNeed? ProcurementNeed { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? SpecificationJson { get; set; }
    public ProcurementTaskLineStatus Status { get; set; } = ProcurementTaskLineStatus.Pending;
    public string? Note { get; set; }
    public decimal? UnitPrice { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Categories { get; set; }
    public SupplierStatus Status { get; set; } = SupplierStatus.Active;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProcurementAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public ProcurementTask? Task { get; set; }
    public Guid? TaskLineId { get; set; }
    public ProcurementTaskLine? TaskLine { get; set; }
    public ProcurementAttachmentType AttachmentType { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProcurementReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public ProcurementTask? Task { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateOnly ReceiptDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "AZN";
    public decimal? TaxAmount { get; set; }
    public Guid? StorageAttachmentId { get; set; }
    public ProcurementAttachment? StorageAttachment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public ICollection<ProcurementReceiptLine> Lines { get; set; } = new List<ProcurementReceiptLine>();
}

public sealed class ProcurementReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public ProcurementReceipt? Receipt { get; set; }
    public Guid TaskLineId { get; set; }
    public ProcurementTaskLine? TaskLine { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

public sealed class CatalogItemPurchasePrice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "AZN";
    public decimal Quantity { get; set; }
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ProcurementTaskId { get; set; }
}

public sealed class WarehouseGoodsReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid ProcurementTaskId { get; set; }
    public ProcurementTask? ProcurementTask { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
    public WarehouseGoodsReceiptStatus Status { get; set; } = WarehouseGoodsReceiptStatus.Draft;
    public ICollection<WarehouseGoodsReceiptLine> Lines { get; set; } = new List<WarehouseGoodsReceiptLine>();
}

public sealed class WarehouseGoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public WarehouseGoodsReceipt? Receipt { get; set; }
    public Guid ProcurementTaskLineId { get; set; }
    public ProcurementTaskLine? ProcurementTaskLine { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public WarehouseGoodsReceiptLineCondition Condition { get; set; } = WarehouseGoodsReceiptLineCondition.Accepted;
    public string? Note { get; set; }
}

public sealed class WarehouseIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SiteId { get; set; }
    public Guid FieldRequestId { get; set; }
    public FieldWarehouseRequest? FieldRequest { get; set; }
    public Guid IssuedByUserId { get; set; }
    public Guid ReceivedBySupervisorUserId { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public WarehouseIssueStatus Status { get; set; } = WarehouseIssueStatus.Draft;
    public string? RecipientName { get; set; }
    public string? HandoverNote { get; set; }
    public string? HandoverAttachmentPath { get; set; }
    public ICollection<WarehouseIssueLine> Lines { get; set; } = new List<WarehouseIssueLine>();
}

public sealed class WarehouseIssueLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid IssueId { get; set; }
    public WarehouseIssue? Issue { get; set; }
    public Guid CatalogItemId { get; set; }
    public FieldWarehouseCatalogItem? CatalogItem { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public WarehouseReservation? Reservation { get; set; }
}

public sealed class SupplyNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? SiteId { get; set; }
    public SupplyNotificationAudience Audience { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public SupplyNotificationStatus Status { get; set; } = SupplyNotificationStatus.Unread;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
