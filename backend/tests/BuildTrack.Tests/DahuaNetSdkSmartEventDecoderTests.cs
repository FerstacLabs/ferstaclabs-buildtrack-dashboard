using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkSmartEventDecoderTests
{
    [Fact]
    public void ResolveEventName_MapsAccessControlSmartEvent()
    {
        Assert.Equal("EVENT_IVS_ACCESS_CTL", DahuaNetSdkSmartEventDecoder.ResolveEventName(0x204));
    }

    [Fact]
    public void Decode_SkipsUnsupportedSmartEvent()
    {
        var buffer = new byte[8192];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x9999, handle.AddrOfPinnedObject(), IntPtr.Zero, 0, 7);

            Assert.Equal("UnsupportedSmartEvent", result.ParseStatus);
            Assert.Null(result.Record);
            Assert.Equal(7, result.Sequence);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void Decode_AccessEventWithoutPersonFields_ReturnsDiagnosticOnlyResult()
    {
        var buffer = new byte[8192];
        WriteInt(buffer, 0, 1);
        WriteAscii(buffer, 4, 128, "AccessControl");
        WriteInt(buffer, 144, 2026);
        WriteInt(buffer, 148, 7);
        WriteInt(buffer, 152, 24);
        WriteInt(buffer, 156, 9);
        WriteInt(buffer, 160, 35);
        WriteInt(buffer, 164, 12);
        WriteInt(buffer, 180, 1234);

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x204, handle.AddrOfPinnedObject(), IntPtr.Zero, 4096, 11);

            Assert.Equal("EVENT_IVS_ACCESS_CTL", result.EventName);
            Assert.Equal("DEV_EVENT_ACCESS_CTL_INFO", result.StructName);
            Assert.Equal("DecodedAccessSmartEventNoPersonFields", result.ParseStatus);
            Assert.NotNull(result.Record);
            Assert.Equal(1234, result.Record!.RecNo);
            Assert.Equal("15", result.Record.MethodRaw);
            Assert.Equal("Entry", result.Record.Type);
            Assert.Contains("ImageBytesLength", result.RawStructSummaryJson);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
    }

    private static void WriteAscii(byte[] buffer, int offset, int length, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(length, bytes.Length));
    }
}
