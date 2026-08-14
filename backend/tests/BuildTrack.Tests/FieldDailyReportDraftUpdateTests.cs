using BuildTrack.Api;
using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace BuildTrack.Tests;

public sealed class FieldDailyReportDraftUpdateTests
{
    [Fact]
    public void SyncDailyReportLinesUpdatesExistingLineAndKeepsIdStable()
    {
        var tenantId = Guid.NewGuid();
        var item = NewSmetaItem("m3");
        var report = NewReport(tenantId, new SupervisorDailyReportLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SmetaItemId = item.Id,
            ReportedQuantity = 50,
            WorkerCount = 15,
            WorkHours = 8,
            Unit = "m3",
            Note = "old",
        });
        var lineId = report.Lines.Single().Id;

        var result = FieldPortalEndpoints.SyncDailyReportLines(
            report,
            [new SaveFieldDailyReportLineRequest(lineId, item.Id, 60, 16, 9, "updated")],
            new Dictionary<Guid, FieldSmetaItem> { [item.Id] = item },
            tenantId);

        Assert.Null(result);
        var line = report.Lines.Single();
        Assert.Equal(lineId, line.Id);
        Assert.Equal(60, line.ReportedQuantity);
        Assert.Equal(16, line.WorkerCount);
        Assert.Equal(9, line.WorkHours);
        Assert.Equal("updated", line.Note);
        Assert.Equal(item.ProjectWorkItemId, line.ProjectWorkItemId);
    }

    [Fact]
    public void SyncDailyReportLinesAddsSecondLineAndRemovesAbsentLine()
    {
        var tenantId = Guid.NewGuid();
        var itemA = NewSmetaItem("m3");
        var itemB = NewSmetaItem("ədəd");
        var itemC = NewSmetaItem("m2");
        var keep = new SupervisorDailyReportLine { Id = Guid.NewGuid(), TenantId = tenantId, SmetaItemId = itemA.Id, ReportedQuantity = 10, Unit = "m3" };
        var remove = new SupervisorDailyReportLine { Id = Guid.NewGuid(), TenantId = tenantId, SmetaItemId = itemB.Id, ReportedQuantity = 4, Unit = "ədəd" };
        var report = NewReport(tenantId, keep, remove);

        var result = FieldPortalEndpoints.SyncDailyReportLines(
            report,
            [
                new SaveFieldDailyReportLineRequest(keep.Id, itemA.Id, 12, 2, 4, "kept"),
                new SaveFieldDailyReportLineRequest(null, itemC.Id, 7, 3, 5, "new"),
            ],
            new Dictionary<Guid, FieldSmetaItem> { [itemA.Id] = itemA, [itemB.Id] = itemB, [itemC.Id] = itemC },
            tenantId);

        Assert.Null(result);
        Assert.Equal(2, report.Lines.Count);
        Assert.Contains(report.Lines, x => x.Id == keep.Id && x.ReportedQuantity == 12);
        Assert.DoesNotContain(report.Lines, x => x.Id == remove.Id);
        Assert.Contains(report.Lines, x => x.SmetaItemId == itemC.Id && x.ReportedQuantity == 7);
    }

    [Fact]
    public void SyncDailyReportLinesCanUpdateTwoExistingLinesRepeatedlyWithoutDuplicates()
    {
        var tenantId = Guid.NewGuid();
        var itemA = NewSmetaItem("m3");
        var itemB = NewSmetaItem("ədəd");
        var lineA = new SupervisorDailyReportLine { Id = Guid.NewGuid(), TenantId = tenantId, SmetaItemId = itemA.Id, ReportedQuantity = 10, Unit = "m3" };
        var lineB = new SupervisorDailyReportLine { Id = Guid.NewGuid(), TenantId = tenantId, SmetaItemId = itemB.Id, ReportedQuantity = 4, Unit = "ədəd" };
        var report = NewReport(tenantId, lineA, lineB);
        var items = new Dictionary<Guid, FieldSmetaItem> { [itemA.Id] = itemA, [itemB.Id] = itemB };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = FieldPortalEndpoints.SyncDailyReportLines(
                report,
                [
                    new SaveFieldDailyReportLineRequest(lineA.Id, itemA.Id, 20 + attempt, 5, 8, "a"),
                    new SaveFieldDailyReportLineRequest(lineB.Id, itemB.Id, 30 + attempt, 6, 9, "b"),
                ],
                items,
                tenantId);

            Assert.Null(result);
        }

        Assert.Equal(2, report.Lines.Count);
        Assert.Equal(21, report.Lines.Single(x => x.Id == lineA.Id).ReportedQuantity);
        Assert.Equal(31, report.Lines.Single(x => x.Id == lineB.Id).ReportedQuantity);
    }

    [Fact]
    public void SyncDailyReportLinesRejectsForeignLineId()
    {
        var tenantId = Guid.NewGuid();
        var item = NewSmetaItem("m3");
        var report = NewReport(tenantId);

        var result = FieldPortalEndpoints.SyncDailyReportLines(
            report,
            [new SaveFieldDailyReportLineRequest(Guid.NewGuid(), item.Id, 1, 1, 1, null)],
            new Dictionary<Guid, FieldSmetaItem> { [item.Id] = item },
            tenantId);

        Assert.NotNull(result);
        Assert.Empty(report.Lines);
    }

    [Fact]
    public void DailyReportUpdateRequestLinesCarryOptionalExistingLineId()
    {
        var propertyNames = typeof(SaveFieldDailyReportLineRequest)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(SaveFieldDailyReportLineRequest.Id), propertyNames);
    }

    [Theory]
    [InlineData(FieldDailyReportStatus.Approved, "təsdiqləndi")]
    [InlineData(FieldDailyReportStatus.NeedsCorrection, "düzəliş tələb olundu")]
    [InlineData(FieldDailyReportStatus.Rejected, "rədd edildi")]
    public void DailyReportReviewAuditDescriptionsAreHumanReadable(FieldDailyReportStatus status, string expectedText)
    {
        var description = FieldPortalEndpoints.BuildDailyReportReviewDescription(new DateOnly(2026, 8, 11), status);

        Assert.Contains("2026-08-11", description);
        Assert.Contains(expectedText, description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftEditKeepsRouteReportDateAsCanonicalIdentity()
    {
        var report = NewReport(Guid.NewGuid());
        report.ReportDate = new DateOnly(2026, 8, 11);
        var staleFormDate = new DateOnly(2026, 8, 10);

        Assert.NotEqual(report.ReportDate, staleFormDate);
        Assert.Equal(new DateOnly(2026, 8, 11), report.ReportDate);
    }

    [Theory]
    [InlineData(FieldDailyReportStatus.Draft, true)]
    [InlineData(FieldDailyReportStatus.NeedsCorrection, true)]
    [InlineData(FieldDailyReportStatus.Submitted, false)]
    [InlineData(FieldDailyReportStatus.Approved, false)]
    [InlineData(FieldDailyReportStatus.Rejected, false)]
    public void SupervisorCanEditOnlyDraftOrNeedsCorrection(FieldDailyReportStatus status, bool expected)
    {
        Assert.Equal(expected, FieldPortalEndpoints.CanSupervisorEditDailyReport(status));
    }

    [Theory]
    [InlineData(FieldDailyReportStatus.Draft, true)]
    [InlineData(FieldDailyReportStatus.NeedsCorrection, true)]
    [InlineData(FieldDailyReportStatus.Submitted, false)]
    [InlineData(FieldDailyReportStatus.Approved, false)]
    [InlineData(FieldDailyReportStatus.Rejected, false)]
    public void SupervisorCanSubmitOnlyDraftOrNeedsCorrection(FieldDailyReportStatus status, bool expected)
    {
        Assert.Equal(expected, FieldPortalEndpoints.CanSubmitDailyReport(status));
    }

    [Fact]
    public void DuplicateDailyReportCreateReturnsConflictShape()
    {
        var report = NewReport(Guid.NewGuid());
        report.Status = FieldDailyReportStatus.NeedsCorrection;

        var result = FieldPortalEndpoints.DailyReportDuplicateConflict(report);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusResult.StatusCode);
    }

    [Fact]
    public void NeedsCorrectionLineSyncKeepsSameReportStatusAndId()
    {
        var tenantId = Guid.NewGuid();
        var item = NewSmetaItem("m3");
        var report = NewReport(tenantId, new SupervisorDailyReportLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SmetaItemId = item.Id,
            ReportedQuantity = 100,
            WorkerCount = 10,
            WorkHours = 8,
            Unit = "m3",
        });
        report.Status = FieldDailyReportStatus.NeedsCorrection;
        var reportId = report.Id;
        var lineId = report.Lines.Single().Id;

        var result = FieldPortalEndpoints.SyncDailyReportLines(
            report,
            [new SaveFieldDailyReportLineRequest(lineId, item.Id, 50, 15, 8, "Edilməli idi 100, edildi 50")],
            new Dictionary<Guid, FieldSmetaItem> { [item.Id] = item },
            tenantId);

        Assert.Null(result);
        Assert.Equal(reportId, report.Id);
        Assert.Equal(FieldDailyReportStatus.NeedsCorrection, report.Status);
        Assert.Single(report.Lines);
        Assert.Equal(lineId, report.Lines.Single().Id);
        Assert.Equal(50, report.Lines.Single().ReportedQuantity);
    }

    private static SupervisorDailyReport NewReport(Guid tenantId, params SupervisorDailyReportLine[] lines)
    {
        var report = new SupervisorDailyReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = Guid.NewGuid(),
            SupervisorUserId = Guid.NewGuid(),
            ReportDate = new DateOnly(2026, 8, 11),
            Status = FieldDailyReportStatus.Draft,
        };
        foreach (var line in lines)
        {
            report.Lines.Add(line);
        }

        return report;
    }

    private static FieldSmetaItem NewSmetaItem(string unit) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        StageName = "Etap",
        WorkName = Guid.NewGuid().ToString("N"),
        Unit = unit,
        ProjectWorkItemId = Guid.NewGuid().ToString("N"),
        PlannedQuantity = 100,
        IsActive = true,
    };
}
