using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Security;

public sealed class AesPasswordProtector(IConfiguration configuration) : IPasswordProtector
{
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuration["BUILDTRACK_SECRET_KEY"]
        ?? configuration["BuildTrack:SecretKey"]
        ?? "dev-only-buildtrack-secret-key-change-me"));

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var payload = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(payload, 0, payload.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return string.Empty;

        var payload = Convert.FromBase64String(protectedText);
        var iv = payload[..16];
        var cipher = payload[16..];
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }
}
