using BuildTrack.Infrastructure.Dahua;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Tests;

public sealed class DahuaDevSimulatorPolicyTests
{
    [Fact]
    public void SimulatorEndpointGuard_IsDisabledByDefault()
    {
        var configuration = BuildConfiguration();

        Assert.False(DahuaDevSimulatorPolicy.IsEnabled(configuration));
    }

    [Fact]
    public void SimulatorEndpointGuard_WorksWhenDevSimulatorActionsEnabled()
    {
        var configuration = BuildConfiguration(new KeyValuePair<string, string?>("DAHUA_DEV_SIMULATOR_ACTIONS_ENABLED", "true"));

        Assert.True(DahuaDevSimulatorPolicy.IsEnabled(configuration));
    }

    [Fact]
    public void SimulatorEndpointGuard_WorksWhenSimulatorEnabled()
    {
        var configuration = BuildConfiguration(new KeyValuePair<string, string?>("DAHUA_SIMULATOR_ENABLED", "true"));

        Assert.True(DahuaDevSimulatorPolicy.IsEnabled(configuration));
    }

    [Fact]
    public void SimulatorEndpointGuard_AcceptsOneAndYes()
    {
        Assert.True(DahuaDevSimulatorPolicy.IsEnabled(BuildConfiguration(new KeyValuePair<string, string?>("DAHUA_DEV_SIMULATOR_ACTIONS_ENABLED", "1"))));
        Assert.True(DahuaDevSimulatorPolicy.IsEnabled(BuildConfiguration(new KeyValuePair<string, string?>("DAHUA_DEV_SIMULATOR_ACTIONS_ENABLED", "yes"))));
    }

    private static IConfiguration BuildConfiguration(params KeyValuePair<string, string?>[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
