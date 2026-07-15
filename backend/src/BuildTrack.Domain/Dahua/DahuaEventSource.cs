namespace BuildTrack.Domain.Dahua;

public enum DahuaEventSource
{
    CgiPolling,
    ActiveRegister,
}

public static class DahuaEventSourceExtensions
{
    public const string CgiPollingSource = "dahua_cgi_polling";
    public const string ActiveRegisterSource = "dahua_active_register";

    public static string ToSourceString(this DahuaEventSource source) => source switch
    {
        DahuaEventSource.CgiPolling => CgiPollingSource,
        DahuaEventSource.ActiveRegister => ActiveRegisterSource,
        _ => "dahua_unknown",
    };
}
