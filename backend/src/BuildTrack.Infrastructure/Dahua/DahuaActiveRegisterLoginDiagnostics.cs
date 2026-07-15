namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaActiveRegisterLoginDiagnostics
{
    public static bool IsPossibleMarshallingWarning(long loginHandle, int nativeErrorPointer, int lastErrorAfterCall) =>
        loginHandle == 0 && nativeErrorPointer == 0 && lastErrorAfterCall == 0;

    public static bool ShouldReleaseRegistrationKeyAfterSubscription(bool subscribed) => !subscribed;
}
