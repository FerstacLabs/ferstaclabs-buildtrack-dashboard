namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaActiveRegisterLoginDiagnostics
{
    public static bool IsPossibleMarshallingWarning(long loginHandle, int nativeErrorPointer, int lastErrorAfterCall) =>
        loginHandle == 0 && nativeErrorPointer == 0 && lastErrorAfterCall == 0;

    public static bool ShouldReleaseRegistrationKeyAfterSubscription(bool subscribed) => !subscribed;
}

public sealed record DahuaActiveRegisterLoginStrategy(
    string Name,
    string LoginApi,
    string IpArgument,
    int PortArgument,
    string CapParamKind,
    bool UsesLoginEx2);

public static class DahuaActiveRegisterLoginStrategyPlan
{
    public const string ApiLoginEx = "LoginEx";
    public const string ApiLoginEx2 = "LoginEx2";
    public const string ApiHighLevel = "LoginWithHighLevelSecurity";
    public const string CapRawRegisterId = "RawRegisterId";
    public const string CapNullTerminatedRegisterId = "NullTerminatedRegisterId";
    public const string CapNull = "Null";

    public static IReadOnlyList<DahuaActiveRegisterLoginStrategy> Build(
        string registerDeviceId,
        string? remoteIp,
        int remotePort,
        bool hasLoginEx,
        bool hasLoginEx2,
        bool hasHighLevelLogin)
    {
        var strategies = new List<DahuaActiveRegisterLoginStrategy>();
        var validRemotePort = remotePort > 0 && remotePort <= ushort.MaxValue;
        var hasRemoteIp = !string.IsNullOrWhiteSpace(remoteIp);

        AddOriginalRawStrategies(strategies, registerDeviceId, remoteIp, remotePort, hasLoginEx, hasLoginEx2, hasRemoteIp, validRemotePort);
        AddNullTerminatedLoginExStrategies(strategies, registerDeviceId, remoteIp, remotePort, hasLoginEx, hasLoginEx2, hasRemoteIp, validRemotePort);

        if (hasHighLevelLogin)
        {
            AddHighLevelVariants(strategies, "HighLevelEmptyIp", string.Empty, 0);
            AddHighLevelVariants(strategies, "HighLevelRegisterIdAsIp", registerDeviceId, 0);

            if (hasRemoteIp)
            {
                AddHighLevelVariants(strategies, "HighLevelRemoteIp", remoteIp!, 0);
            }

            if (hasRemoteIp && validRemotePort)
            {
                AddHighLevelVariants(strategies, "HighLevelRemoteEndpoint", remoteIp!, remotePort);
            }

            if (validRemotePort)
            {
                AddHighLevelVariants(strategies, "HighLevelDeviceIdWithRemotePort", registerDeviceId, remotePort);
            }
        }

        return strategies;
    }

    private static void AddOriginalRawStrategies(List<DahuaActiveRegisterLoginStrategy> strategies, string registerDeviceId, string? remoteIp, int remotePort, bool hasLoginEx, bool hasLoginEx2, bool hasRemoteIp, bool validRemotePort)
    {
        if (hasLoginEx)
        {
            strategies.Add(New("LoginExEmptyIp", ApiLoginEx, string.Empty, 0, CapRawRegisterId, usesLoginEx2: false));
            strategies.Add(New("LoginExRegisterIdAsIp", ApiLoginEx, registerDeviceId, 0, CapRawRegisterId, usesLoginEx2: false));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(New("LoginExRemoteEndpoint", ApiLoginEx, remoteIp!, remotePort, CapRawRegisterId, usesLoginEx2: false));
            }

            if (validRemotePort)
            {
                strategies.Add(New("LoginExDeviceIdWithRemotePort", ApiLoginEx, registerDeviceId, remotePort, CapRawRegisterId, usesLoginEx2: false));
            }
        }

        if (hasLoginEx2)
        {
            strategies.Add(New("LoginEx2EmptyIp", ApiLoginEx2, string.Empty, 0, CapRawRegisterId, usesLoginEx2: true));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(New("LoginEx2RemoteEndpoint", ApiLoginEx2, remoteIp!, remotePort, CapRawRegisterId, usesLoginEx2: true));
            }

            if (validRemotePort)
            {
                strategies.Add(New("LoginEx2DeviceIdWithRemotePort", ApiLoginEx2, registerDeviceId, remotePort, CapRawRegisterId, usesLoginEx2: true));
            }
        }
    }

    private static void AddNullTerminatedLoginExStrategies(List<DahuaActiveRegisterLoginStrategy> strategies, string registerDeviceId, string? remoteIp, int remotePort, bool hasLoginEx, bool hasLoginEx2, bool hasRemoteIp, bool validRemotePort)
    {
        if (hasLoginEx)
        {
            strategies.Add(New("LoginExEmptyIpNullTerminated", ApiLoginEx, string.Empty, 0, CapNullTerminatedRegisterId, usesLoginEx2: false));
            strategies.Add(New("LoginExRegisterIdAsIpNullTerminated", ApiLoginEx, registerDeviceId, 0, CapNullTerminatedRegisterId, usesLoginEx2: false));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(New("LoginExRemoteEndpointNullTerminated", ApiLoginEx, remoteIp!, remotePort, CapNullTerminatedRegisterId, usesLoginEx2: false));
            }

            if (validRemotePort)
            {
                strategies.Add(New("LoginExDeviceIdWithRemotePortNullTerminated", ApiLoginEx, registerDeviceId, remotePort, CapNullTerminatedRegisterId, usesLoginEx2: false));
            }
        }

        if (hasLoginEx2)
        {
            strategies.Add(New("LoginEx2EmptyIpNullTerminated", ApiLoginEx2, string.Empty, 0, CapNullTerminatedRegisterId, usesLoginEx2: true));

            if (hasRemoteIp && validRemotePort)
            {
                strategies.Add(New("LoginEx2RemoteEndpointNullTerminated", ApiLoginEx2, remoteIp!, remotePort, CapNullTerminatedRegisterId, usesLoginEx2: true));
            }

            if (validRemotePort)
            {
                strategies.Add(New("LoginEx2DeviceIdWithRemotePortNullTerminated", ApiLoginEx2, registerDeviceId, remotePort, CapNullTerminatedRegisterId, usesLoginEx2: true));
            }
        }
    }

    private static void AddHighLevelVariants(List<DahuaActiveRegisterLoginStrategy> strategies, string name, string ip, int port)
    {
        strategies.Add(New(name + "Raw", ApiHighLevel, ip, port, CapRawRegisterId, usesLoginEx2: false));
        strategies.Add(New(name + "NullTerminated", ApiHighLevel, ip, port, CapNullTerminatedRegisterId, usesLoginEx2: false));
    }

    private static DahuaActiveRegisterLoginStrategy New(string name, string loginApi, string ip, int port, string capParamKind, bool usesLoginEx2) =>
        new(name, loginApi, ip, port, capParamKind, usesLoginEx2);
}
