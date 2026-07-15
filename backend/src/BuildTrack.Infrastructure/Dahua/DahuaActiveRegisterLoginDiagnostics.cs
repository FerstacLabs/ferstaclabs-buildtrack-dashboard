namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaActiveRegisterLoginDiagnostics
{
    public static bool IsPossibleMarshallingWarning(long loginHandle, int nativeErrorPointer, int lastErrorAfterCall) =>
        loginHandle == 0 && nativeErrorPointer == 0 && lastErrorAfterCall == 0;

    public static bool ShouldReleaseRegistrationKeyAfterSubscription(bool subscribed) => !subscribed;
}

public sealed record DahuaActiveRegisterLoginStrategy(
    string Name,
    string IpArgument,
    int PortArgument,
    bool UsesLoginEx2);

public static class DahuaActiveRegisterLoginStrategyPlan
{
    public static IReadOnlyList<DahuaActiveRegisterLoginStrategy> Build(
        string registerDeviceId,
        string? remoteIp,
        int remotePort,
        bool hasLoginEx,
        bool hasLoginEx2)
    {
        var strategies = new List<DahuaActiveRegisterLoginStrategy>();
        var validRemotePort = remotePort > 0 && remotePort <= ushort.MaxValue;
        var hasRemoteIp = !string.IsNullOrWhiteSpace(remoteIp);

        if (hasLoginEx)
        {
            strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginExEmptyIp", string.Empty, 0, UsesLoginEx2: false));
            strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginExRegisterIdAsIp", registerDeviceId, 0, UsesLoginEx2: false));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginExRemoteEndpoint", remoteIp!, remotePort, UsesLoginEx2: false));
            }

            if (validRemotePort)
            {
                strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginExDeviceIdWithRemotePort", registerDeviceId, remotePort, UsesLoginEx2: false));
            }
        }

        if (hasLoginEx2)
        {
            strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginEx2EmptyIp", string.Empty, 0, UsesLoginEx2: true));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginEx2RemoteEndpoint", remoteIp!, remotePort, UsesLoginEx2: true));
            }

            if (validRemotePort)
            {
                strategies.Add(new DahuaActiveRegisterLoginStrategy("LoginEx2DeviceIdWithRemotePort", registerDeviceId, remotePort, UsesLoginEx2: true));
            }
        }

        return strategies;
    }
}
