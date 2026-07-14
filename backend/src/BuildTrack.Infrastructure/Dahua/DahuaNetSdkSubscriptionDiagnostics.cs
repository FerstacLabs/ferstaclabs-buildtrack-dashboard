namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaNetSdkSubscriptionDiagnostics
{
    public const string StrategyResultAttempting = "Attempting";
    public const string StrategyResultSucceeded = "Succeeded";
    public const string StrategyResultFailed = "Failed";
    public const string StrategyResultNotAttempted = "NotAttempted";
    public const string StatusSubscriptionFailed = "SubscriptionFailed";

    public static string BuildRegistrationKey(string registerDeviceId, string? remoteIp, int remotePort, long serviceCallbackHandle)
        => $"{registerDeviceId}|{remoteIp ?? "unknown"}:{remotePort}|{serviceCallbackHandle}";

    public static void MarkStartListenExFailure(DahuaNetSdkDiagnostics diagnostics, int errorSigned, string errorHex)
    {
        diagnostics.StartListenExCalled = true;
        diagnostics.StartListenExSuccess = false;
        diagnostics.StartListenExErrorSigned = errorSigned;
        diagnostics.StartListenExErrorHex = errorHex;
        diagnostics.ActiveRegisterSessionHandleStrategyResult = StrategyResultFailed;
        diagnostics.LastDecodeError = $"CLIENT_StartListenEx failed. ErrorSigned={errorSigned}, ErrorHex={errorHex}";
        diagnostics.NetSdkDecodeStatus = StatusSubscriptionFailed;
    }
}

