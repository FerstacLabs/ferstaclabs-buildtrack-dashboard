namespace BuildTrack.Domain.Entities;

public enum ProjectEntityStatus
{
    NotStarted,
    InProgress,
    Paused,
    Completed,
    Delayed,
    Archived,
}

public sealed class ProjectRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Currency { get; set; } = "AZN";
    public string? Location { get; set; }
    public string? ClientName { get; set; }
    public string? ActiveEstimateVersionId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectEntityStatus Status { get; set; } = ProjectEntityStatus.InProgress;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectEstimateVersionRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public ProjectRecord? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectSiteRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public ProjectRecord? Project { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public string? Zone { get; set; }
    public ProjectEntityStatus Status { get; set; } = ProjectEntityStatus.NotStarted;
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectStageRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string? EstimateVersionId { get; set; }
    public Guid? SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int Order { get; set; }
    public decimal TotalCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal MaterialCost { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public ProjectEntityStatus Status { get; set; } = ProjectEntityStatus.NotStarted;
    public decimal ProgressPercent { get; set; }
    public string? AssignedCrewId { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectWorkItemRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public Guid SiteId { get; set; }
    public string StageId { get; set; } = string.Empty;
    public string? EstimateVersionId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CompletedQuantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal LaborUnitPrice { get; set; }
    public decimal LaborTotal { get; set; }
    public string? MaterialUnit { get; set; }
    public decimal MaterialQuantity { get; set; }
    public decimal MaterialUnitPrice { get; set; }
    public decimal MaterialTotal { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public string? AssignedCrewId { get; set; }
    public ProjectEntityStatus Status { get; set; } = ProjectEntityStatus.NotStarted;
    public decimal ProgressPercent { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectWorkItemMaterialRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public Guid? SiteId { get; set; }
    public string? StageId { get; set; }
    public string WorkItemId { get; set; } = string.Empty;
    public string? CatalogItemId { get; set; }
    public string? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UsedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ProjectCrewRecord
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public Guid? SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ForemanName { get; set; } = string.Empty;
    public int WorkerCount { get; set; }
    public string? ActiveWorkStageId { get; set; }
    public string? ActiveWorkItemId { get; set; }
    public decimal PlannedDailyHours { get; set; } = 8;
    public ProjectEntityStatus? Status { get; set; }
    public decimal? ProgressPercent { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
