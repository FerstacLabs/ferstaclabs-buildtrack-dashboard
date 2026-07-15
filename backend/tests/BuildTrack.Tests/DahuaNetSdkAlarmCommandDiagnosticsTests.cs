using System.Runtime.InteropServices;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkAlarmCommandDiagnosticsTests
{
    [Theory]
    [InlineData(0x21A9, "DH_ALARM_AP_CONNECT")]
    [InlineData(0x3173, "DH_ALARM_CHASSISINTRUDED")]
    [InlineData(0x3169, "DH_ALARM_NET_ABORT")]
    [InlineData(0x300C, "DH_START_LISTEN_FINISH_EVENT")]
    [InlineData(0x3491, "DH_ALARM_SIP_REGISTER_RESULT")]
    [InlineData(0x3181, "DH_ALARM_ACCESS_CTL_EVENT")]
    public void ResolveCommandName_MapsReceivedRuntimeCommands(int command, string expected)
    {
        Assert.Equal(expected, DahuaNetSdkAlarmCommandDiagnostics.ResolveCommandName(command));
    }

    [Fact]
    public void ApConnectStruct_MatchesObservedRuntimePayloadSize()
    {
        Assert.Equal(2168, Marshal.SizeOf<DahuaNetSdkAlarmCommandDiagnostics.NetAlarmApConnectInfo>());
    }

    [Fact]
    public void Inspect_ApConnect_DecodesDiagnosticFieldsAndMarksNonAttendance()
    {
        var info = new DahuaNetSdkAlarmCommandDiagnostics.NetAlarmApConnectInfo
        {
            NChannelId = 1,
            NAction = 0,
            StuUtc = new DahuaNetSdkAccessEventDecoder.NetTimeEx
            {
                DwYear = 2026,
                DwMonth = 7,
                DwDay = 16,
                DwHour = 8,
                DwMinute = 30,
                DwSecond = 5,
            },
            StuEventInfoEx = new DahuaNetSdkAlarmCommandDiagnostics.NetEventInfoExtend
            {
                ByReserved = new byte[4],
                SzReserved = new byte[968],
            },
            SzMacAddress = Bytes(32, "AA:BB:CC:DD:EE:FF"),
            SzIpAddress = Bytes(32, "192.168.1.20"),
            SzReserved = new byte[992],
        };
        var payload = StructToBytes(info);

        var diagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(0x21A9, payload);

        Assert.Equal("DH_ALARM_AP_CONNECT", diagnostic.CommandName);
        Assert.Equal("NET_ALARM_AP_CONNECT_INFO", diagnostic.StructName);
        Assert.Equal("DecodedNonAttendanceAlarm", diagnostic.DecodeStatus);
        Assert.Equal("AA:BB:CC:DD:EE:FF", diagnostic.Fields["macAddress"]);
        Assert.Equal("192.168.1.20", diagnostic.Fields["ipAddress"]);
        Assert.Contains("not an access-control attendance event", diagnostic.FailureReason);
    }

    [Fact]
    public void Inspect_StartListenFinish_DecodesEventResult()
    {
        var info = new DahuaNetSdkAlarmCommandDiagnostics.StartListenFinishResultInfo
        {
            DwEventResult = 0,
            ByReserved = new byte[508],
        };

        var diagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(0x300C, StructToBytes(info));

        Assert.Equal("DH_START_LISTEN_FINISH_EVENT", diagnostic.CommandName);
        Assert.Equal("START_LISTEN_FINISH_RESULT_INFO", diagnostic.StructName);
        Assert.Equal("0", diagnostic.Fields["eventResult"]);
    }

    private static byte[] StructToBytes<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
            return bytes;
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] Bytes(int size, string value)
    {
        var bytes = new byte[size];
        var source = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(source, bytes, Math.Min(size, source.Length));
        return bytes;
    }
}
