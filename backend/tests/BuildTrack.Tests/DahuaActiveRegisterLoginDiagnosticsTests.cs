using BuildTrack.Infrastructure.Dahua;
using Xunit;

namespace BuildTrack.Tests;

public sealed class DahuaActiveRegisterLoginDiagnosticsTests
{
    [Fact]
    public void ZeroHandleAndZeroErrors_IsReportedAsPossibleMarshallingWarning()
    {
        var warning = DahuaActiveRegisterLoginDiagnostics.IsPossibleMarshallingWarning(0, 0, 0);

        Assert.True(warning);
    }

    [Fact]
    public void NonZeroError_IsNotReportedAsPossibleMarshallingWarning()
    {
        var warning = DahuaActiveRegisterLoginDiagnostics.IsPossibleMarshallingWarning(0, unchecked((int)0x80000004), 0);

        Assert.False(warning);
    }

    [Fact]
    public void FailedSubscription_ReleasesRegistrationKeyForFutureRetry()
    {
        var release = DahuaActiveRegisterLoginDiagnostics.ShouldReleaseRegistrationKeyAfterSubscription(subscribed: false);

        Assert.True(release);
    }

    [Fact]
    public void SuccessfulSubscription_KeepsRegistrationKeyHandled()
    {
        var release = DahuaActiveRegisterLoginDiagnostics.ShouldReleaseRegistrationKeyAfterSubscription(subscribed: true);

        Assert.False(release);
    }

    [Fact]
    public void StrategyPlan_IncludesRemoteEndpointStrategies_WhenRemoteEndpointIsValid()
    {
        var strategies = DahuaActiveRegisterLoginStrategyPlan.Build(
            "BT-API-TEST-001",
            "185.146.112.123",
            60062,
            hasLoginEx: true,
            hasLoginEx2: true,
            hasHighLevelLogin: true);

        Assert.Contains(strategies, x => x.Name == "LoginExRemoteEndpoint" && x.LoginApi == DahuaActiveRegisterLoginStrategyPlan.ApiLoginEx && x.IpArgument == "185.146.112.123" && x.PortArgument == 60062 && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapRawRegisterId);
        Assert.Contains(strategies, x => x.Name == "LoginExRemoteEndpointNullTerminated" && x.LoginApi == DahuaActiveRegisterLoginStrategyPlan.ApiLoginEx && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNullTerminatedRegisterId);
        Assert.Contains(strategies, x => x.Name == "LoginEx2RemoteEndpoint" && x.LoginApi == DahuaActiveRegisterLoginStrategyPlan.ApiLoginEx2 && x.IpArgument == "185.146.112.123" && x.PortArgument == 60062 && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapRawRegisterId);
        Assert.Contains(strategies, x => x.Name == "LoginEx2RemoteEndpointNullTerminated" && x.LoginApi == DahuaActiveRegisterLoginStrategyPlan.ApiLoginEx2 && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNullTerminatedRegisterId);
        Assert.Contains(strategies, x => x.Name == "LoginExDeviceIdWithRemotePort" && x.IpArgument == "BT-API-TEST-001" && x.PortArgument == 60062);
        Assert.Contains(strategies, x => x.Name == "LoginEx2DeviceIdWithRemotePort" && x.IpArgument == "BT-API-TEST-001" && x.PortArgument == 60062);
    }

    [Fact]
    public void StrategyPlan_IncludesHighLevelSecurityStrategies_WhenExportExists()
    {
        var strategies = DahuaActiveRegisterLoginStrategyPlan.Build(
            "BT-API-TEST-001",
            "185.146.112.123",
            60099,
            hasLoginEx: false,
            hasLoginEx2: false,
            hasHighLevelLogin: true);

        Assert.Contains(strategies, x => x.Name == "HighLevelEmptyIpRaw" && x.LoginApi == DahuaActiveRegisterLoginStrategyPlan.ApiHighLevel && x.IpArgument == string.Empty && x.PortArgument == 0 && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapRawRegisterId);
        Assert.Contains(strategies, x => x.Name == "HighLevelEmptyIpNullTerminated" && x.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNullTerminatedRegisterId);
        Assert.Contains(strategies, x => x.Name == "HighLevelRegisterIdAsIpRaw" && x.IpArgument == "BT-API-TEST-001" && x.PortArgument == 0);
        Assert.Contains(strategies, x => x.Name == "HighLevelRemoteEndpointRaw" && x.IpArgument == "185.146.112.123" && x.PortArgument == 60099);
        Assert.Contains(strategies, x => x.Name == "HighLevelDeviceIdWithRemotePortRaw" && x.IpArgument == "BT-API-TEST-001" && x.PortArgument == 60099);
    }

    [Fact]
    public void StrategyPlan_SkipsRemoteEndpointStrategies_WhenRemotePortIsInvalid()
    {
        var strategies = DahuaActiveRegisterLoginStrategyPlan.Build(
            "BT-API-TEST-001",
            "185.146.112.123",
            70000,
            hasLoginEx: true,
            hasLoginEx2: true,
            hasHighLevelLogin: true);

        Assert.DoesNotContain(strategies, x => x.Name.Contains("RemoteEndpoint", StringComparison.Ordinal));
        Assert.DoesNotContain(strategies, x => x.Name.Contains("RemotePort", StringComparison.Ordinal));
    }
}

