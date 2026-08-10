using System.Reflection;
using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;

namespace BuildTrack.Tests;

public sealed class FieldPortalPrivacyTests
{
    [Fact]
    public void FieldDailyReportSubmitContractUsesReportedQuantity()
    {
        var lineProperties = typeof(SaveFieldDailyReportLineRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(SaveFieldDailyReportLineRequest.ReportedQuantity), lineProperties);
        Assert.Contains(nameof(SaveFieldDailyReportLineRequest.WorkerCount), lineProperties);
        Assert.Contains(nameof(SaveFieldDailyReportLineRequest.WorkHours), lineProperties);
        Assert.DoesNotContain(lineProperties, name => name.Equals("CompletedQuantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FieldDailyReportReadContractReturnsWorkerCountAndWorkHours()
    {
        var lineProperties = typeof(FieldDailyReportLineDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(FieldDailyReportLineDto.ReportedQuantity), lineProperties);
        Assert.Contains(nameof(FieldDailyReportLineDto.WorkerCount), lineProperties);
        Assert.Contains(nameof(FieldDailyReportLineDto.WorkHours), lineProperties);
        Assert.DoesNotContain(lineProperties, name => name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lineProperties, name => name.Contains("Price", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FieldDailyReportReadContractReturnsReviewTraceabilityFields()
    {
        var reportProperties = typeof(FieldDailyReportDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(FieldDailyReportDto.WeatherCondition), reportProperties);
        Assert.Contains(nameof(FieldDailyReportDto.GeneralNote), reportProperties);
        Assert.Contains(nameof(FieldDailyReportDto.ReviewedAt), reportProperties);
        Assert.Contains(nameof(FieldDailyReportDto.ReviewedByUserId), reportProperties);
        Assert.Contains(nameof(FieldDailyReportDto.ReviewedByName), reportProperties);
        Assert.Contains(nameof(FieldDailyReportDto.ReviewNote), reportProperties);
    }

    [Fact]
    public void SupervisorAuditContractReturnsActionAndDescription()
    {
        var auditProperties = typeof(SupervisorAuditEventDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(SupervisorAuditEventDto.Action), auditProperties);
        Assert.Contains(nameof(SupervisorAuditEventDto.EntityType), auditProperties);
        Assert.Contains(nameof(SupervisorAuditEventDto.Description), auditProperties);
    }

    [Fact]
    public void SupervisorDailyReportLineStoresWorkerCountAndWorkHours()
    {
        var lineProperties = typeof(SupervisorDailyReportLine)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(SupervisorDailyReportLine.WorkerCount), lineProperties);
        Assert.Contains(nameof(SupervisorDailyReportLine.WorkHours), lineProperties);
    }

    [Theory]
    [InlineData(nameof(FieldSmetaItemDto), "planned")]
    [InlineData(nameof(FieldSmetaItemDto), "cost")]
    [InlineData(nameof(FieldSmetaItemDto), "price")]
    [InlineData(nameof(FieldWarehouseCatalogItemDto), "stock")]
    [InlineData(nameof(FieldWarehouseCatalogItemDto), "balance")]
    [InlineData(nameof(FieldWarehouseCatalogItemDto), "cost")]
    [InlineData(nameof(FieldWarehouseCatalogItemDto), "price")]
    public void FieldDtosDoNotExposeRestrictedCommercialFields(string dtoName, string forbiddenToken)
    {
        var type = typeof(FieldSmetaItemDto).Assembly.GetTypes().Single(x => x.Name == dtoName);
        var propertyNames = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name);

        Assert.DoesNotContain(propertyNames, name => name.Contains(forbiddenToken, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(SupervisorWorkerEventType.Late, 1)]
    [InlineData(SupervisorWorkerEventType.LeftEarly, 1)]
    [InlineData(SupervisorWorkerEventType.Absent, 3)]
    [InlineData(SupervisorWorkerEventType.SafetyWarning, 3)]
    [InlineData(SupervisorWorkerEventType.Permission, 0)]
    public void SupervisorWorkerEventsUseServerSideRiskPolicy(SupervisorWorkerEventType eventType, int expectedDelta)
    {
        Assert.Equal(expectedDelta, FieldRiskPolicy.CalculateRiskDelta(eventType));
    }
}
