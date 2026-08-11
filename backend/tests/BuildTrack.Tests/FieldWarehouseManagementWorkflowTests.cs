using BuildTrack.Api;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Tests;

public sealed class FieldWarehouseManagementWorkflowTests
{
    [Fact]
    public void TerminalWarehouseRequestStatuses_DoNotAllowFurtherReviewActions()
    {
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.Rejected, FieldWarehouseRequestStatus.Approved));
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.Issued, FieldWarehouseRequestStatus.Rejected));
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.Closed, FieldWarehouseRequestStatus.NeedsJustification));
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.Cancelled, FieldWarehouseRequestStatus.Issued));
    }

    [Fact]
    public void PendingWarehouseRequest_AllowsManagementReviewDecisions()
    {
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.PendingApproval, FieldWarehouseRequestStatus.Approved));
        Assert.True(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.PendingApproval, FieldWarehouseRequestStatus.NeedsJustification));
        Assert.True(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.PendingApproval, FieldWarehouseRequestStatus.Rejected));
    }

    [Fact]
    public void NeedsJustificationWarehouseRequest_CannotBeApprovedUntilSupervisorResponds()
    {
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.NeedsJustification, FieldWarehouseRequestStatus.Approved));
        Assert.True(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.NeedsJustification, FieldWarehouseRequestStatus.Rejected));
    }

    [Fact]
    public void ReadyForPickupWarehouseRequest_AllowsOnlyRejectReviewAction()
    {
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.ReadyForPickup, FieldWarehouseRequestStatus.Issued));
        Assert.True(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.ReadyForPickup, FieldWarehouseRequestStatus.Rejected));
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.ReadyForPickup, FieldWarehouseRequestStatus.Approved));
        Assert.False(FieldPortalEndpoints.IsValidWarehouseReviewTransition(FieldWarehouseRequestStatus.ReadyForPickup, FieldWarehouseRequestStatus.NeedsJustification));
    }
}
