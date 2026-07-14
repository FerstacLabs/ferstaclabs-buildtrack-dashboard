using System.Text.Json;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaRawPayloadFormatter
{
    public static object CreateLogPayload(
        byte[] payload,
        int listenerPort,
        string? remoteIp,
        int? remotePort,
        DateTimeOffset? receivedAt = null)
    {
        var safePayload = payload ?? [];
        return new
        {
            payloadBase64 = Convert.ToBase64String(safePayload),
            payloadHex = Convert.ToHexString(safePayload),
            byteLength = safePayload.Length,
            receivedAt = (receivedAt ?? DateTimeOffset.UtcNow).ToString("O"),
            listenerPort,
            remoteIp,
            remotePort,
            asciiPreview = CreateAsciiPreview(safePayload),
        };
    }

    public static string CreateLogPayloadJson(
        byte[] payload,
        int listenerPort,
        string? remoteIp,
        int? remotePort,
        DateTimeOffset? receivedAt = null) => JsonSerializer.Serialize(CreateLogPayload(payload, listenerPort, remoteIp, remotePort, receivedAt));

    public static string CreateAsciiPreview(byte[] payload, int maxLength = 300)
    {
        if (payload.Length == 0) return string.Empty;

        var chars = new List<char>(Math.Min(payload.Length, maxLength));
        foreach (var value in payload)
        {
            if (value == 0) continue;
            chars.Add(value is >= 32 and <= 126 ? (char)value : '.');
            if (chars.Count >= maxLength) break;
        }

        return new string(chars.ToArray());
    }
}
