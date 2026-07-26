namespace BuildTrack.Infrastructure.Dahua;

public enum DahuaIdentityMatchPolicy
{
    Strict,
    UserIdPrimary
}

public static class DahuaIdentityMatchPolicyParser
{
    public static DahuaIdentityMatchPolicy Parse(string? value) =>
        string.Equals(value, "user_id_primary", StringComparison.OrdinalIgnoreCase)
            ? DahuaIdentityMatchPolicy.UserIdPrimary
            : DahuaIdentityMatchPolicy.Strict;
}
