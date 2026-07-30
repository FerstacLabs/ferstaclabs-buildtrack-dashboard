namespace BuildTrack.Infrastructure.Dahua;

public enum DahuaIdentityResolutionMode
{
    StrictUserId,
    CardNamePrimary,
    Hybrid
}

public static class DahuaIdentityResolutionModeParser
{
    public static DahuaIdentityResolutionMode Parse(string? value)
    {
        if (string.Equals(value, "cardname_primary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "cardname-primary", StringComparison.OrdinalIgnoreCase))
        {
            return DahuaIdentityResolutionMode.CardNamePrimary;
        }

        if (string.Equals(value, "hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return DahuaIdentityResolutionMode.Hybrid;
        }

        return DahuaIdentityResolutionMode.StrictUserId;
    }
}
