namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaCgiCursorPolicy
{
    public const long PollutedRecNoThreshold = 1_000_000_000;

    public static DahuaCgiCursorResolution Resolve(long? cgiLastRecNo, long? legacyLastRecNo)
    {
        if (cgiLastRecNo is > PollutedRecNoThreshold)
        {
            return new DahuaCgiCursorResolution(0, true, "CgiLastRecNo");
        }

        if (cgiLastRecNo is not null)
        {
            return new DahuaCgiCursorResolution(Math.Max(0, cgiLastRecNo.Value), false, "CgiLastRecNo");
        }

        if (legacyLastRecNo is > PollutedRecNoThreshold)
        {
            return new DahuaCgiCursorResolution(0, true, "LastRecNo");
        }

        return new DahuaCgiCursorResolution(Math.Max(0, legacyLastRecNo ?? 0), false, "LastRecNo");
    }

    public static bool IsSafeCgiRecNo(long? recNo) => recNo is not null and >= 0 and <= PollutedRecNoThreshold;

    public static bool ShouldAdvanceCgiCursor(string source, long? recNo, long? currentCgiLastRecNo) =>
        source.Equals("dahua_cgi_polling", StringComparison.OrdinalIgnoreCase)
        && IsSafeCgiRecNo(recNo)
        && (currentCgiLastRecNo is null || recNo > currentCgiLastRecNo);
}

public sealed record DahuaCgiCursorResolution(long LastRecNo, bool WasPolluted, string SourceField);

