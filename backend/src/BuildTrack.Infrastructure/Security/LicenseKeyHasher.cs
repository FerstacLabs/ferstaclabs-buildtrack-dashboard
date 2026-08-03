using System.Security.Cryptography;
using System.Text;

namespace BuildTrack.Infrastructure.Security;

public static class LicenseKeyHasher
{
    public static string GenerateRawLicenseKey() =>
        $"BT-{RandomSegment()}-{RandomSegment()}-{RandomSegment()}";

    public static string HashLicenseKey(string licenseKey)
    {
        var normalized = Normalize(licenseKey);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Normalize(string licenseKey) =>
        (licenseKey ?? string.Empty).Trim().ToUpperInvariant();

    private static string RandomSegment()
    {
        Span<byte> bytes = stackalloc byte[5];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
