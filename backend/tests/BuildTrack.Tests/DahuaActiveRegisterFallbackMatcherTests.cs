using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaActiveRegisterFallbackMatcherTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    public void IsSingleDeviceFallbackEnabled_AcceptsExpectedTruthyValues(string value)
    {
        Assert.True(DahuaActiveRegisterFallbackMatcher.IsSingleDeviceFallbackEnabled(value));
    }

    [Fact]
    public void MatchSingleDeviceFallback_EnabledAndOneDeviceOnSamePort_ReturnsDevice()
    {
        var device = CreateDevice(7000);
        var candidates = CandidatesForPort([device], 7000);

        var result = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, enabled: true);

        Assert.Same(device, result);
    }

    [Fact]
    public void MatchSingleDeviceFallback_EnabledAndTwoDevicesOnSamePort_ReturnsNull()
    {
        var candidates = CandidatesForPort([CreateDevice(7000), CreateDevice(7000)], 7000);

        var result = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, enabled: true);

        Assert.Null(result);
    }

    [Fact]
    public void MatchSingleDeviceFallback_DisabledAndOneDevice_ReturnsNull()
    {
        var candidates = CandidatesForPort([CreateDevice(7000)], 7000);

        var result = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, enabled: false);

        Assert.Null(result);
    }

    [Fact]
    public void MatchSingleDeviceFallback_DifferentRegisterPort_ReturnsNull()
    {
        var candidates = CandidatesForPort([CreateDevice(9500)], 7000);

        var result = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, enabled: true);

        Assert.Null(result);
    }

    private static IEnumerable<Device> CandidatesForPort(IEnumerable<Device> devices, int listenerPort) =>
        devices.Where(device => device.Mode == DeviceMode.ActiveRegister && device.RegisterPort == listenerPort);

    private static Device CreateDevice(int registerPort) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        Name = $"Device {registerPort}",
        Mode = DeviceMode.ActiveRegister,
        RegisterPort = registerPort,
        RegisterDeviceId = $"BT-{Guid.NewGuid():N}",
        Status = DeviceStatus.Pending,
    };
}
