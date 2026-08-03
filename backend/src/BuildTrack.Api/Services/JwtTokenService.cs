using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildTrack.Api.Options;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Api.Services;

public sealed record BuildTrackPrincipal(Guid UserId, Guid TenantId, string Email, BuildTrackUserRole Role);

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
    BuildTrackPrincipal? ValidateToken(string token);
}

public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    public string CreateToken(AppUser user)
    {
        if (!options.Configured) throw new InvalidOperationException("JWT_SECRET is not configured");

        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = user.Id.ToString(),
            ["tenant_id"] = user.TenantId.ToString(),
            ["email"] = user.Email,
            ["role"] = user.Role.ToString(),
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(Math.Clamp(options.ExpiresMinutes, 15, 60 * 24 * 7)).ToUnixTimeSeconds(),
        };

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var unsigned = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        var signature = Sign(unsigned);
        return $"{unsigned}.{signature}";
    }

    public BuildTrackPrincipal? ValidateToken(string token)
    {
        if (!options.Configured || string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var unsigned = $"{parts[0]}.{parts[1]}";
        var expected = Sign(unsigned);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return null;

        try
        {
            using var document = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = document.RootElement;
            if (!root.TryGetProperty("exp", out var exp) || DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) <= DateTimeOffset.UtcNow) return null;
            if (!StringEquals(root, "iss", options.Issuer) || !StringEquals(root, "aud", options.Audience)) return null;
            if (!Guid.TryParse(root.GetProperty("sub").GetString(), out var userId)) return null;
            if (!Guid.TryParse(root.GetProperty("tenant_id").GetString(), out var tenantId)) return null;
            if (!Enum.TryParse<BuildTrackUserRole>(root.GetProperty("role").GetString(), out var role)) return null;
            return new BuildTrackPrincipal(userId, tenantId, root.GetProperty("email").GetString() ?? string.Empty, role);
        }
        catch
        {
            return null;
        }
    }

    private string Sign(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Secret));
        return Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(value)));
    }

    private static bool StringEquals(JsonElement root, string property, string expected) =>
        root.TryGetProperty(property, out var value)
        && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
