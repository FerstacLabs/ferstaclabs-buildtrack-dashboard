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
        Assert.DoesNotContain(lineProperties, name => name.Equals("CompletedQuantity", StringComparison.OrdinalIgnoreCase));
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
