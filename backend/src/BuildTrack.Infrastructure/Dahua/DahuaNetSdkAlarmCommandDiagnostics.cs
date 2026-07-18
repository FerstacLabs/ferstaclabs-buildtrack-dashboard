using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace BuildTrack.Infrastructure.Dahua;

public sealed record DahuaNetSdkAlarmCommandDiagnostic(
    int Command,
    string CommandHex,
    string CommandName,
    string? StructName,
    int PayloadBytes,
    string PayloadFirst256Hex,
    string DecodeStatus,
    string? FailureReason,
    IReadOnlyDictionary<string, string?> Fields);

public static class DahuaNetSdkAlarmCommandDiagnostics
{
    public const int AlarmApConnect = 0x21A9;
    public const int AlarmNetAbort = 0x3169;
    public const int AlarmChassisIntruded = 0x3173;
    public const int StartListenFinish = 0x300C;
    public const int AlarmSipRegisterResult = 0x3491;
    public const int AlarmAccessControlStatus = 0x3185;
    public const int EventMotionDetect = 0x218F;
    public const int AlarmScreenSaver = 0x3475;
    public const int AlarmAccessSnap = 0x3186;
    public const int AlarmFaceRecognition = 0x3435;

    public static string ResolveCommandName(int command) => command switch
    {
        AlarmApConnect => "DH_ALARM_AP_CONNECT",
        AlarmNetAbort => "DH_ALARM_NET_ABORT",
        AlarmChassisIntruded => "DH_ALARM_CHASSISINTRUDED",
        StartListenFinish => "DH_START_LISTEN_FINISH_EVENT",
        AlarmSipRegisterResult => "DH_ALARM_SIP_REGISTER_RESULT",
        AlarmAccessControlStatus => "DH_ALARM_ACCESS_CTL_STATUS",
        EventMotionDetect => "DH_EVENT_MOTIONDETECT",
        AlarmScreenSaver => "DH_ALARM_SCREENSAVER",
        DahuaNetSdkAccessEventDecoder.AccessControlEventCommand => "DH_ALARM_ACCESS_CTL_EVENT",
        AlarmAccessSnap => "DH_ALARM_ACCESS_SNAP",
        AlarmFaceRecognition => "DH_ALARM_FACE_RECOGNITION",
        _ => command switch
        {
            -1 => "DH_DVR_DISCONNECT",
            1 => "DH_DVR_SERIAL_RETURN",
            2 => "NET_DEV_AUTOREGISTER_RETURN",
            3 => "NET_DEV_NOTIFY_IP_RETURN",
            4 => "NET_DEV_AUTOREGISTER_PRIMARY_BACKUP",
            5 => "DH_DVR_SERIAL_RETURN_EX",
            _ => $"UnknownCommand_0x{command:X}"
        }
    };

    public static DahuaNetSdkAlarmCommandDiagnostic Inspect(int command, byte[] payload)
    {
        var commandName = ResolveCommandName(command);
        var first256Hex = Convert.ToHexString(payload.Take(256).ToArray());

        try
        {
            return command switch
            {
                AlarmApConnect => DecodeStruct<NetAlarmApConnectInfo>(command, commandName, "NET_ALARM_AP_CONNECT_INFO", payload, first256Hex, DecodeApConnect),
                AlarmNetAbort => DecodeStruct<AlarmNetAbortInfo>(command, commandName, "ALARM_NETABORT_INFO", payload, first256Hex, DecodeNetAbort),
                AlarmChassisIntruded => DecodeStruct<AlarmChassisIntrudedInfo>(command, commandName, "ALARM_CHASSISINTRUDED_INFO", payload, first256Hex, DecodeChassisIntruded),
                StartListenFinish => DecodeStruct<StartListenFinishResultInfo>(command, commandName, "START_LISTEN_FINISH_RESULT_INFO", payload, first256Hex, DecodeStartListenFinish),
                AlarmSipRegisterResult => DecodeStruct<AlarmSipRegisterResultInfo>(command, commandName, "ALARM_SIP_REGISTER_RESULT_INFO", payload, first256Hex, DecodeSipRegisterResult),
                AlarmAccessControlStatus => DecodeStruct<AlarmAccessControlStatusInfo>(command, commandName, "ALARM_ACCESS_CTL_STATUS_INFO", payload, first256Hex, DecodeAccessControlStatus),
                EventMotionDetect => DecodeStruct<AlarmMotionDetectInfo>(command, commandName, "ALARM_MOTIONDETECT_INFO", payload, first256Hex, DecodeMotionDetect),
                AlarmScreenSaver => DecodeStruct<AlarmScreenSaverInfo>(command, commandName, "ALARM_SCREENSAVER_INFO", payload, first256Hex, DecodeScreenSaver),
                DahuaNetSdkAccessEventDecoder.AccessControlEventCommand => new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, "ALARM_ACCESS_CTL_EVENT_INFO", payload.Length, first256Hex, "AccessControlCommand", null, new Dictionary<string, string?>
                {
                    ["note"] = "Access-control command; decoded by DahuaNetSdkAccessEventDecoder."
                }),
                AlarmAccessSnap => new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, "ALARM_ACCESS_SNAP_INFO", payload.Length, first256Hex, "KnownUnsupportedNonAttendanceAlarm", "Access snapshot alarm is diagnostic-only until a face/person record is present.", new Dictionary<string, string?>()),
                AlarmFaceRecognition => new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, "ALARM_FACE_RECOGNITION_INFO", payload.Length, first256Hex, "KnownUnsupportedPotentialFaceAlarm", "Face recognition alarm struct in this SDK is not the access-control attendance struct. Raw payload saved for vendor validation.", new Dictionary<string, string?>()),
                _ => new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, null, payload.Length, first256Hex, "UnknownUnsupportedAlarmCommand", "No BuildTrack decoder mapped for this NetSDK alarm command.", new Dictionary<string, string?>()),
            };
        }
        catch (Exception ex)
        {
            return new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, null, payload.Length, first256Hex, "DecodeFailed", ex.Message, new Dictionary<string, string?>());
        }
    }

    private static DahuaNetSdkAlarmCommandDiagnostic DecodeStruct<T>(int command, string commandName, string structName, byte[] payload, string first256Hex, Func<T, IReadOnlyDictionary<string, string?>> decode) where T : struct
    {
        var required = Marshal.SizeOf<T>();
        if (payload.Length < required)
        {
            return new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, structName, payload.Length, first256Hex, "DecodeFailed", $"Payload too small for {structName}. PayloadBytes={payload.Length}, RequiredBytes={required}", new Dictionary<string, string?>());
        }

        var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
        try
        {
            var value = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            var fields = decode(value);
            return new DahuaNetSdkAlarmCommandDiagnostic(command, $"0x{command:X}", commandName, structName, payload.Length, first256Hex, "DecodedNonAttendanceAlarm", NonAttendanceReason(commandName), fields);
        }
        finally
        {
            handle.Free();
        }
    }

    private static string NonAttendanceReason(string commandName) => commandName switch
    {
        "DH_ALARM_AP_CONNECT" => "Connection hotspot alarm; not an access-control attendance event.",
        "DH_ALARM_NET_ABORT" => "Network fault alarm; not an access-control attendance event.",
        "DH_ALARM_CHASSISINTRUDED" => "Chassis tamper alarm; not an access-control attendance event.",
        "DH_START_LISTEN_FINISH_EVENT" => "Start-listen completion notification; not an access-control attendance event.",
        "DH_ALARM_SIP_REGISTER_RESULT" => "SIP registration status alarm; not an access-control attendance event.",
        "DH_ALARM_ACCESS_CTL_STATUS" => "Access-control door/status event; it contains no worker/person recognition fields.",
        "DH_EVENT_MOTIONDETECT" => "Video motion detection event; it contains no worker/person recognition fields.",
        "DH_ALARM_SCREENSAVER" => "Screen saver status event; not an access-control attendance event.",
        _ => "Known NetSDK alarm is diagnostic-only for BuildTrack attendance."
    };

    private static string? FormatTime(DahuaNetSdkAccessEventDecoder.NetTimeEx time) => time.IsValid ? time.ToDateTimeOffset().ToString("O") : null;

    private static IReadOnlyDictionary<string, string?> DecodeApConnect(NetAlarmApConnectInfo info) => new Dictionary<string, string?>
    {
        ["channelId"] = info.NChannelId.ToString(),
        ["action"] = info.NAction.ToString(),
        ["utc"] = FormatTime(info.StuUtc),
        ["eventInfoRealUtcValid"] = info.StuEventInfoEx.BRealUtc.ToString(),
        ["eventInfoRealUtc"] = FormatTime(info.StuEventInfoEx.StuRealUtc),
        ["macAddress"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzMacAddress),
        ["ipAddress"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzIpAddress),
    };

    private static IReadOnlyDictionary<string, string?> DecodeNetAbort(AlarmNetAbortInfo info) => new Dictionary<string, string?>
    {
        ["dwSize"] = info.DwSize.ToString(),
        ["action"] = info.NAction.ToString(),
        ["netAbortType"] = info.EmNetAbortType.ToString(),
        ["time"] = info.StuTime.ToDateTimeOffset().ToString("O"),
        ["interface"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzInterface),
    };

    private static IReadOnlyDictionary<string, string?> DecodeChassisIntruded(AlarmChassisIntrudedInfo info) => new Dictionary<string, string?>
    {
        ["dwSize"] = info.DwSize.ToString(),
        ["action"] = info.NAction.ToString(),
        ["time"] = info.StuTime.ToDateTimeOffset().ToString("O"),
        ["channelId"] = info.NChannelId.ToString(),
        ["readerId"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzReaderId),
        ["eventId"] = info.NEventId.ToString(),
        ["sn"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzSn),
        ["realUtcUsed"] = info.BRealUtc.ToString(),
        ["realUtc"] = FormatTime(info.RealUtc),
        ["deviceType"] = info.EmDevType.ToString(),
    };

    private static IReadOnlyDictionary<string, string?> DecodeStartListenFinish(StartListenFinishResultInfo info) => new Dictionary<string, string?>
    {
        ["eventResult"] = info.DwEventResult.ToString(),
        ["eventResultHex"] = $"0x{info.DwEventResult:X8}",
    };

    private static IReadOnlyDictionary<string, string?> DecodeSipRegisterResult(AlarmSipRegisterResultInfo info) => new Dictionary<string, string?>
    {
        ["channelId"] = info.NChannelId.ToString(),
        ["action"] = info.NAction.ToString(),
        ["utc"] = FormatTime(info.Utc),
        ["success"] = info.BSuccess.ToString(),
        ["date"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzDate),
        ["sipId"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.StuUserInfo.SzId),
        ["sipIpAddress"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.StuUserInfo.SzIpAddr),
        ["sipPort"] = info.StuUserInfo.NPort.ToString(),
        ["sipOnline"] = info.StuUserInfo.BOnline.ToString(),
        ["sipDevType"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.StuUserInfo.SzDevType),
        ["sipUserAgent"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.StuUserInfo.SzUserAgent),
    };


    private static IReadOnlyDictionary<string, string?> DecodeAccessControlStatus(AlarmAccessControlStatusInfo info)
    {
        var eventTime = info.BRealUtc && info.RealUtc.IsValid ? FormatTime(info.RealUtc) : info.StuTime.ToDateTimeOffset().ToString("O");
        return new Dictionary<string, string?>
        {
            ["dwSize"] = info.DwSize.ToString(),
            ["door"] = info.NDoor.ToString(),
            ["time"] = eventTime,
            ["statusRaw"] = info.EmStatus.ToString(),
            ["statusName"] = info.EmStatus switch
            {
                1 => "Open",
                2 => "Close",
                3 => "Abnormal",
                4 => "FakeLocked",
                5 => "CloseAlways",
                6 => "OpenAlways",
                7 => "Normal",
                _ => "Unknown",
            },
            ["serialNumber"] = DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.SzSerialNumber),
            ["realUtcUsed"] = info.BRealUtc.ToString(),
            ["realUtc"] = FormatTime(info.RealUtc),
            ["note"] = "Access-control status event only; no UserID/CardName/open method fields are present in SDK struct.",
        };
    }

    private static IReadOnlyDictionary<string, string?> DecodeMotionDetect(AlarmMotionDetectInfo info) => new Dictionary<string, string?>
    {
        ["dwSize"] = info.DwSize.ToString(),
        ["channelId"] = info.NChannelId.ToString(),
        ["pts"] = info.Pts.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["utc"] = FormatTime(info.Utc),
        ["eventId"] = info.NEventId.ToString(),
        ["eventAction"] = info.NEventAction.ToString(),
        ["regionNum"] = info.NRegionNum.ToString(),
        ["smartMotionEnabled"] = info.BSmartMotionEnable.ToString(),
        ["detectTypeNum"] = info.NDetectTypeNum.ToString(),
        ["firstDetectType"] = info.EmDetectType is { Length: > 0 } ? info.EmDetectType[0].ToString() : null,
        ["firstRegionId"] = info.StuRegion is { Length: > 0 } ? info.StuRegion[0].NRegionId.ToString() : null,
        ["firstRegionName"] = info.StuRegion is { Length: > 0 } ? DahuaNetSdkAccessEventDecoder.DecodeSdkString(info.StuRegion[0].SzRegionName) : null,
        ["eventInfoRealUtcValid"] = info.StuEventInfoEx.BRealUtc.ToString(),
        ["eventInfoRealUtc"] = FormatTime(info.StuEventInfoEx.StuRealUtc),
        ["note"] = "Video motion event only; no UserID/CardName/open method fields are present in SDK struct.",
    };

    private static IReadOnlyDictionary<string, string?> DecodeScreenSaver(AlarmScreenSaverInfo info) => new Dictionary<string, string?>
    {
        ["action"] = info.NAction.ToString(),
        ["statusRaw"] = info.EmStatus.ToString(),
        ["closePage"] = info.BClosePage.ToString(),
        ["screenOff"] = info.BScreenOff.ToString(),
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct NetTimeEx2
    {
        public uint DwYear;
        public uint DwMonth;
        public uint DwDay;
        public uint DwHour;
        public uint DwMinute;
        public uint DwSecond;
        public uint DwMillisecond;
        public uint DwReserved0;
        public long DwUtc;

        [JsonIgnore]
        public readonly bool IsValid => DwYear is >= 1970 and <= 9999 && DwMonth is >= 1 and <= 12 && DwDay is >= 1 and <= 31;

        public readonly string? ToIsoString() => IsValid
            ? new DateTimeOffset((int)DwYear, (int)DwMonth, (int)DwDay, (int)DwHour, (int)DwMinute, (int)DwSecond, (int)Math.Min(DwMillisecond, 999), TimeSpan.Zero).ToString("O")
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetEventInfoExtend
    {
        [MarshalAs(UnmanagedType.Bool)] public bool BRealUtc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] ByReserved;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx StuRealUtc;
        [MarshalAs(UnmanagedType.Bool)] public bool BIsEventsTypeValid;
        public uint SzEventsType;
        public int NTransfer;
        public NetTimeEx2 StuRealUtcEx;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 968)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetAlarmApConnectInfo
    {
        public int NChannelId;
        public int NAction;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx StuUtc;
        public NetEventInfoExtend StuEventInfoEx;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzMacAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzIpAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 992)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmNetAbortInfo
    {
        public uint DwSize;
        public int NAction;
        public int EmNetAbortType;
        public DahuaNetSdkAccessEventDecoder.NetTime StuTime;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzInterface;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmChassisIntrudedInfo
    {
        public uint DwSize;
        public int NAction;
        public DahuaNetSdkAccessEventDecoder.NetTime StuTime;
        public int NChannelId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzReaderId;
        public uint NEventId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzSn;
        [MarshalAs(UnmanagedType.Bool)] public bool BRealUtc;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx RealUtc;
        public int EmDevType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartListenFinishResultInfo
    {
        public uint DwEventResult;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 508)] public byte[] ByReserved;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmAccessControlStatusInfo
    {
        public uint DwSize;
        public int NDoor;
        public DahuaNetSdkAccessEventDecoder.NetTime StuTime;
        public int EmStatus;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzSerialNumber;
        [MarshalAs(UnmanagedType.Bool)] public bool BRealUtc;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx RealUtc;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetMotionDetectRegionInfo
    {
        public uint NRegionId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzRegionName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 508)] public byte[] BReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetGpsStatusInfo
    {
        public DahuaNetSdkAccessEventDecoder.NetTime RevTime;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)] public byte[] DvrSerial;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] ByReserved1;
        public double Longitude;
        public double Latitude;
        public double Height;
        public double Angle;
        public double Speed;
        public ushort StarCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] ByReserved2;
        public int AntennaState;
        public int OrientationState;
        public int WorkState;
        public int NAlarmCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public int[] NAlarmState;
        public byte BOffline;
        public byte BSNR;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] ByReserved3;
        public int EmDateSource;
        public int NSignalStrength;
        public float FHdop;
        public float FPdop;
        public int NMileage;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)] public byte[] ByReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmMotionDetectInfo
    {
        public uint DwSize;
        public int NChannelId;
        public double Pts;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx Utc;
        public int NEventId;
        public int NEventAction;
        public uint NRegionNum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public NetMotionDetectRegionInfo[] StuRegion;
        [MarshalAs(UnmanagedType.Bool)] public bool BSmartMotionEnable;
        public uint NDetectTypeNum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public int[] EmDetectType;
        public NetEventInfoExtend StuEventInfoEx;
        public NetGpsStatusInfo StuGpsStatusInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmScreenSaverInfo
    {
        public int NAction;
        public int EmStatus;
        [MarshalAs(UnmanagedType.Bool)] public bool BClosePage;
        [MarshalAs(UnmanagedType.Bool)] public bool BScreenOff;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] BReserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct NetSipRegisterUserInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzIpAddr;
        public uint NPort;
        [MarshalAs(UnmanagedType.Bool)] public bool BOnline;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzDevType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzVtoType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzUserAgent;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzGroupNbr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmSipRegisterResultInfo
    {
        public int NChannelId;
        public int NAction;
        public DahuaNetSdkAccessEventDecoder.NetTimeEx Utc;
        [MarshalAs(UnmanagedType.Bool)] public bool BSuccess;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzDate;
        public NetSipRegisterUserInfo StuUserInfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] public byte[] SzReserved;
    }
}
