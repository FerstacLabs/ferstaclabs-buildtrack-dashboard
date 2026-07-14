using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public sealed record DahuaExperimentalStartServiceCallbackDiagnostic(
    int Command,
    int PayloadBytes,
    string DecodeStatus,
    DahuaSdkAccessEvent? AccessEvent);

public static class DahuaExperimentalStartServiceDiagnostics
{
    public static DahuaExperimentalStartServiceCallbackDiagnostic Inspect(int command, byte[] payload)
    {
        var safePayload = payload ?? [];
        if (command != DahuaNetSdkAccessEventDecoder.AccessControlEventCommand)
        {
            return new DahuaExperimentalStartServiceCallbackDiagnostic(command, safePayload.Length, $"Skipped command 0x{command:X}; diagnostic-only StartService callback", null);
        }

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(safePayload, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var ok = DahuaNetSdkAccessEventDecoder.TryDecodeAccessControlEvent(
                handle.AddrOfPinnedObject(),
                (uint)safePayload.Length,
                out var sdkEvent,
                out var skipReason);
            var status = ok
                ? $"Experimental StartService decoded DH_ALARM_ACCESS_CTL_EVENT diagnostic-only. UserID={sdkEvent.UserId}, Name={sdkEvent.CardName}, Status={sdkEvent.Status}, Method={sdkEvent.Method}"
                : $"Experimental StartService received DH_ALARM_ACCESS_CTL_EVENT but decode skipped: {skipReason}";
            return new DahuaExperimentalStartServiceCallbackDiagnostic(command, safePayload.Length, status, ok ? sdkEvent : null);
        }
        finally
        {
            handle.Free();
        }
    }
}
