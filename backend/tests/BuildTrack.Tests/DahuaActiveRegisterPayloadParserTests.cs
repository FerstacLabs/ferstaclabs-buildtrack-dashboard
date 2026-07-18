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


    [Fact]
    public void Inspect_SerialReturn_ReportsRegisterIdAtOffsetZeroAndFirst256Hex()
    {
        var payload = new byte[300];
        var serial = "BT-API-TEST-001"u8.ToArray();
        Array.Copy(serial, payload, serial.Length);
        payload[260] = 0xAB;

        var diagnostic = DahuaActiveRegisterPayloadParser.Inspect(1, payload, "185.146.112.123", 60062, new IntPtr(1234));

        Assert.Equal("BT-API-TEST-001", diagnostic.RegisterDeviceId);
        Assert.Equal(0, diagnostic.RegisterDeviceIdOffset);
        Assert.Equal(512, diagnostic.PayloadFirst256Hex.Length);
        Assert.Equal("185.146.112.123", diagnostic.RemoteIp);
        Assert.Equal(60062, diagnostic.RemotePort);
        Assert.Contains("char* szDevSerial", diagnostic.StructLayout);
    }

    [Fact]
    public void Inspect_SerialReturnEx_ReportsHeaderLayoutAndScansReservedHandleCandidates()
    {
        var payload = new byte[1088];
        var serial = "BT-API-TEST-001"u8.ToArray();
        Array.Copy(serial, payload, serial.Length);
        BitConverter.GetBytes(1).CopyTo(payload, 64);
        BitConverter.GetBytes(140004122793824L).CopyTo(payload, 72);

        var diagnostic = DahuaActiveRegisterPayloadParser.Inspect(5, payload, "185.146.112.123", 60099, new IntPtr(5555));

        Assert.Equal("BT-API-TEST-001", diagnostic.Serial);
        Assert.Equal(0, diagnostic.SerialOffset);
        Assert.Contains("NET_CB_SERIAL_RETURN_INFO", diagnostic.StructLayout);
        Assert.Contains(140004122793824L, diagnostic.PossibleSessionHandles);
        Assert.Equal(512, diagnostic.PayloadFirst256Hex.Length);
    }
    private static byte[] SerialPayload(string value, int size)
    {
        var payload = new byte[size];
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, payload, Math.Min(bytes.Length, payload.Length));
        return payload;
    }
}

