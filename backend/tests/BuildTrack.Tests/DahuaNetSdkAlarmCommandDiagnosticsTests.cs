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
    [InlineData(0x3185, "DH_ALARM_ACCESS_CTL_STATUS")]
    [InlineData(0x218F, "DH_EVENT_MOTIONDETECT")]
    [InlineData(0x3475, "DH_ALARM_SCREENSAVER")]
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


    [Fact]
    public void AccessControlStatusStruct_MatchesObservedRuntimePayloadSize()
    {
        Assert.Equal(332, Marshal.SizeOf<DahuaNetSdkAlarmCommandDiagnostics.AlarmAccessControlStatusInfo>());
    }

    [Fact]
    public void MotionDetectStruct_MatchesObservedRuntimePayloadSize()
    {
        Assert.Equal(20472, Marshal.SizeOf<DahuaNetSdkAlarmCommandDiagnostics.AlarmMotionDetectInfo>());
    }

    [Fact]
    public void ScreenSaverStruct_MatchesObservedRuntimePayloadSize()
    {
        Assert.Equal(144, Marshal.SizeOf<DahuaNetSdkAlarmCommandDiagnostics.AlarmScreenSaverInfo>());
    }

    [Fact]
    public void Inspect_AccessControlStatus_DecodesDoorStatusAsDiagnosticOnly()
    {
        var info = new DahuaNetSdkAlarmCommandDiagnostics.AlarmAccessControlStatusInfo
        {
            DwSize = 332,
            NDoor = 2,
            StuTime = new DahuaNetSdkAccessEventDecoder.NetTime
            {
                DwYear = 2026,
                DwMonth = 7,
                DwDay = 15,
                DwHour = 19,
                DwMinute = 4,
                DwSecond = 33,
            },
            EmStatus = 2,
            SzSerialNumber = Bytes(256, "BT-LOCK-001"),
            BRealUtc = false,
        };

        var diagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(0x3185, StructToBytes(info));

        Assert.Equal("DH_ALARM_ACCESS_CTL_STATUS", diagnostic.CommandName);
        Assert.Equal("ALARM_ACCESS_CTL_STATUS_INFO", diagnostic.StructName);
        Assert.Equal("DecodedNonAttendanceAlarm", diagnostic.DecodeStatus);
        Assert.Equal("2", diagnostic.Fields["door"]);
        Assert.Equal("Close", diagnostic.Fields["statusName"]);
        Assert.Contains("no UserID", diagnostic.Fields["note"]);
    }

    [Fact]
    public void Inspect_MotionDetect_DecodesHeaderFieldsAsDiagnosticOnly()
    {
        var info = new DahuaNetSdkAlarmCommandDiagnostics.AlarmMotionDetectInfo
        {
            DwSize = 20472,
            NChannelId = 1,
            Utc = new DahuaNetSdkAccessEventDecoder.NetTimeEx
            {
                DwYear = 2026,
                DwMonth = 7,
                DwDay = 15,
                DwHour = 19,
                DwMinute = 5,
                DwSecond = 46,
            },
            NEventId = 10,
            NEventAction = 0,
            NRegionNum = 1,
            StuRegion = Enumerable.Range(0, 32).Select(i => new DahuaNetSdkAlarmCommandDiagnostics.NetMotionDetectRegionInfo
            {
                NRegionId = (uint)i,
                SzRegionName = Bytes(64, i == 0 ? "main" : string.Empty),
                BReserved = new byte[508],
            }).ToArray(),
            BSmartMotionEnable = true,
            NDetectTypeNum = 1,
            EmDetectType = Enumerable.Repeat(0, 32).ToArray(),
            StuEventInfoEx = new DahuaNetSdkAlarmCommandDiagnostics.NetEventInfoExtend
            {
                ByReserved = new byte[4],
                SzReserved = new byte[968],
            },
            StuGpsStatusInfo = new DahuaNetSdkAlarmCommandDiagnostics.NetGpsStatusInfo
            {
                DvrSerial = new byte[50],
                ByReserved1 = new byte[6],
                ByReserved2 = new byte[2],
                NAlarmState = new int[128],
                ByReserved3 = new byte[2],
                ByReserved = new byte[96],
            },
        };

        var diagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(0x218F, StructToBytes(info));

        Assert.Equal("DH_EVENT_MOTIONDETECT", diagnostic.CommandName);
        Assert.Equal("ALARM_MOTIONDETECT_INFO", diagnostic.StructName);
        Assert.Equal("1", diagnostic.Fields["regionNum"]);
        Assert.Equal("main", diagnostic.Fields["firstRegionName"]);
        Assert.Contains("Video motion", diagnostic.FailureReason);
    }

    [Fact]
    public void Inspect_ScreenSaver_DecodesDiagnosticFields()
    {
        var info = new DahuaNetSdkAlarmCommandDiagnostics.AlarmScreenSaverInfo
        {
            NAction = 0,
            EmStatus = 2,
            BClosePage = true,
            BScreenOff = false,
            BReserved = new byte[128],
        };

        var diagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(0x3475, StructToBytes(info));

        Assert.Equal("DH_ALARM_SCREENSAVER", diagnostic.CommandName);
        Assert.Equal("ALARM_SCREENSAVER_INFO", diagnostic.StructName);
        Assert.Equal("2", diagnostic.Fields["statusRaw"]);
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
