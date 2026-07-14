using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaDevSimulatorPolicy
{
    public static bool IsEnabled(IConfiguration configuration) =>
        IsTruthy(configuration["DAHUA_SIMULATOR_ENABLED"])
        || IsTruthy(configuration["DAHUA_DEV_SIMULATOR_ACTIONS_ENABLED"]);

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
