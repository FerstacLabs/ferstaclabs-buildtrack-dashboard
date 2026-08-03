namespace BuildTrack.Api.Options;

public sealed class JwtOptions
{
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "BuildTrack";
    public string Audience { get; init; } = "BuildTrack.App";
    public int ExpiresMinutes { get; init; } = 720;
    public bool Configured => !string.IsNullOrWhiteSpace(Secret);
}
