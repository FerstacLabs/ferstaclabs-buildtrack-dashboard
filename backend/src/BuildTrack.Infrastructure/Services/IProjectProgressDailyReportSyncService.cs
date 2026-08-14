using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IProjectProgressDailyReportSyncService
{
    Task SyncFieldSmetaItemsFromWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ProjectProgressApprovalValidationResult> ValidateApprovedReportAsync(
        Guid tenantId,
        SupervisorDailyReport report,
        CancellationToken cancellationToken);

    Task<ProjectProgressRecalculationResult> RecalculateApprovedDailyReportProgressAsync(
        Guid tenantId,
        Guid? sourceReportId,
        CancellationToken cancellationToken);
}

public sealed record ProjectProgressApprovalValidationResult(
    bool IsValid,
    string? Error,
    string? ProjectWorkItemId,
    decimal PlannedQuantity,
    decimal CandidateApprovedQuantity);

public sealed record ProjectProgressRecalculationResult(
    int UpdatedWorkItems,
    int UpdatedStages,
    decimal TotalApprovedQuantity,
    Guid? SourceReportId);

public sealed class ProjectProgressSmetaSyncException(
    string code,
    string message,
    IReadOnlyList<ProjectProgressSmetaSyncConflict>? conflicts = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<ProjectProgressSmetaSyncConflict> Conflicts { get; } = conflicts ?? Array.Empty<ProjectProgressSmetaSyncConflict>();
}

public sealed record ProjectProgressSmetaSyncConflict(
    Guid? ExistingFieldSmetaItemId,
    Guid? ConflictingFieldSmetaItemId,
    Guid SiteId,
    string WorkName,
    string? ProjectWorkItemId,
    string? ConflictingProjectWorkItemId,
    string Reason);
