using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaActiveRegisterPayloadParserTests
{
    [Fact]
    public void Parse_Command1_ExtractsRegisterDeviceIdFromSerialPayload()
    {
        var payload = SerialPayload("BT-API-TEST-001", 32);

        var parsed = DahuaActiveRegisterPayloadParser.Parse(1, payload);

        Assert.Equal("DH_DVR_SERIAL_RETURN", parsed.Kind);
        Assert.Equal("BT-API-TEST-001", parsed.RegisterDeviceId);
        Assert.Equal("BT-API-TEST-001", parsed.Serial);
        Assert.False(parsed.HasSessionHandle);
        Assert.Equal(IntPtr.Zero, parsed.SessionHandle);
    }

    [Fact]
    public void Parse_Command5_ExtractsSerialAndRedirectionFlagWithoutFakeSessionHandle()
    {
        var payload = new byte[1088];
        var serial = System.Text.Encoding.ASCII.GetBytes("BT-API-TEST-001");
        Array.Copy(serial, payload, serial.Length);
        BitConverter.GetBytes(1).CopyTo(payload, 64);

        var parsed = DahuaActiveRegisterPayloadParser.Parse(5, payload);

        Assert.Equal("DH_DVR_SERIAL_RETURN_EX", parsed.Kind);
        Assert.Equal("BT-API-TEST-001", parsed.RegisterDeviceId);
        Assert.Equal("BT-API-TEST-001", parsed.Serial);
        Assert.True(parsed.SupportsRedirection);
        Assert.False(parsed.HasSessionHandle);
        Assert.Equal(IntPtr.Zero, parsed.SessionHandle);
    }

    private static byte[] SerialPayload(string value, int size)
    {
        var payload = new byte[size];
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, payload, Math.Min(bytes.Length, payload.Length));
        return payload;
    }
}
