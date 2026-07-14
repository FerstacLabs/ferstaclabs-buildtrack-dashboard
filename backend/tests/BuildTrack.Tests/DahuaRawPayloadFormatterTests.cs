using System.Text.Json;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaRawPayloadFormatterTests
{
    [Fact]
    public void CreateLogPayloadJson_StoresBinaryPayloadAsBase64AndHex()
    {
        var payload = new byte[] { 0xFF, 0x00, 0xFE, 0x41, 0x42, 0x10, 0x43 };

        var json = DahuaRawPayloadFormatter.CreateLogPayloadJson(
            payload,
            listenerPort: 7000,
            remoteIp: "203.0.113.10",
            remotePort: 52610,
            receivedAt: DateTimeOffset.Parse("2026-07-06T10:00:00Z"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(Convert.ToBase64String(payload), root.GetProperty("payloadBase64").GetString());
        Assert.Equal(Convert.ToHexString(payload), root.GetProperty("payloadHex").GetString());
        Assert.Equal(payload.Length, root.GetProperty("byteLength").GetInt32());
        Assert.Equal(7000, root.GetProperty("listenerPort").GetInt32());
        Assert.Equal("203.0.113.10", root.GetProperty("remoteIp").GetString());
        Assert.Equal(52610, root.GetProperty("remotePort").GetInt32());
    }

    [Fact]
    public void CreateAsciiPreview_RemovesNullBytesAndReplacesNonPrintableBytes()
    {
        var payload = new byte[] { 0x00, 0x44, 0x65, 0x76, 0x69, 0x63, 0x65, 0x49, 0x44, 0x3D, 0x42, 0x54, 0x01, 0xFF, 0x0A, 0x31 };

        var preview = DahuaRawPayloadFormatter.CreateAsciiPreview(payload);

        Assert.DoesNotContain(preview, ch => ch == '\0');
        Assert.Equal("DeviceID=BT...1", preview);
    }

    [Fact]
    public void CreateAsciiPreview_LimitsPreviewToThreeHundredCharacters()
    {
        var payload = Enumerable.Repeat((byte)'A', 500).ToArray();

        var preview = DahuaRawPayloadFormatter.CreateAsciiPreview(payload);

        Assert.Equal(300, preview.Length);
    }
}


