namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaActiveRegisterServiceMode
{
    public const string ListenServer = "ListenServer";
    public const string StartServiceExperimental = "StartServiceExperimental";

    public static string Parse(string? value)
    {
        if (string.Equals(value, StartServiceExperimental, StringComparison.OrdinalIgnoreCase)) return StartServiceExperimental;
        return ListenServer;
    }

    public static bool IsExperimentalEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    public static int ParseExperimentalPort(string? value, int defaultPort = 7001) =>
        int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : defaultPort;

    public static bool HasSamePortConflict(string mode, IEnumerable<int> listenServerPorts, int experimentalPort) =>
        string.Equals(mode, StartServiceExperimental, StringComparison.OrdinalIgnoreCase)
        && listenServerPorts.Any(port => port == experimentalPort);
}
