using System.Runtime.InteropServices;
using System.Text;
using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaNetSdkAccessEventDecoder
{
    public const int AccessControlEventCommand = 0x3181;
    public const int EventTypeEntry = 1;
    public const int EventTypeExit = 2;

    private static readonly HashSet<int> FaceMethodValues =
    [
        16, 18, 19, 23, 25, 26, 27, 29, 30, 32, 33, 35, 36, 37, 38, 39, 40,
        41, 42, 45, 51, 54, 56, 57, 59, 60, 62, 63, 88, 91, 93, 94, 96, 97,
        99, 100, 103, 109, 112, 115, 116, 118, 119, 120, 121, 122
    ];

    public static bool TryDecodeAccessControlEvent(IntPtr payload, uint payloadLength, out DahuaSdkAccessEvent sdkEvent, out string? skipReason)
    {
        sdkEvent = new DahuaSdkAccessEvent();
        skipReason = null;

        var requiredSize = Marshal.SizeOf<AlarmAccessControlEventInfo>();
        if (payload == IntPtr.Zero || payloadLength < requiredSize)
        {
            skipReason = $"Payload too small for ALARM_ACCESS_CTL_EVENT_INFO. PayloadBytes={payloadLength}, RequiredBytes={requiredSize}";
            return false;
        }

        try
        {
            return TryDecode(Marshal.PtrToStructure<AlarmAccessControlEventInfo>(payload), out sdkEvent, out skipReason);
        }
        catch (Exception ex)
        {
            skipReason = $"Failed to marshal ALARM_ACCESS_CTL_EVENT_INFO: {ex.Message}";
            return false;
        }
    }

    public static bool TryDecode(AlarmAccessControlEventInfo info, out DahuaSdkAccessEvent sdkEvent, out string? skipReason)
    {
        skipReason = null;

        var userId = DecodeSdkString(info.SzUserID);
        var name = ChooseName(info, userId);
        var status = info.BStatus ? "1" : "0";
        var method = IsFaceMethod(info.EmOpenMethod) ? "face" : "card";
        var direction = info.EmEventType switch
        {
            EventTypeEntry => "Entry",
            EventTypeExit => "Exit",
            _ => "Unknown"
        };
        var eventTime = info.BRealUtc && info.RealUtc.IsValid
            ? info.RealUtc.ToDateTimeOffset()
            : info.StuTime.ToDateTimeOffset();

        sdkEvent = new DahuaSdkAccessEvent
        {
            RegisterDeviceId = DecodeSdkString(info.SzDeviceID),
            UserId = userId,
            CardName = name,
            EventTime = eventTime,
            Status = status,
            Method = method,
            Direction = direction,
            RecNo = info.NPunchingRecNo > 0 ? info.NPunchingRecNo : null,
            SnapshotPath = DecodeSdkString(info.SzSnapURL),
            RawFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Command"] = $"0x{AccessControlEventCommand:X}",
                ["EventStruct"] = "ALARM_ACCESS_CTL_EVENT_INFO",
                ["UserID"] = userId,
                ["CardName"] = name,
                ["CitizenName"] = DecodeSdkString(info.SzCitizenName),
                ["CardNameEx"] = DecodeSdkString(info.SzCardNameEx),
                ["UseCardNameEx"] = info.BUseCardNameEx.ToString(),
                ["Status"] = status,
                ["Method"] = method,
                ["OpenMethodRaw"] = info.EmOpenMethod.ToString(),
                ["Type"] = direction,
                ["EventTypeRaw"] = info.EmEventType.ToString(),
                ["RecNo"] = info.NPunchingRecNo > 0 ? info.NPunchingRecNo.ToString() : null,
                ["SnapshotPath"] = DecodeSdkString(info.SzSnapURL),
                ["DeviceID"] = DecodeSdkString(info.SzDeviceID),
                ["UserUniqueID"] = DecodeSdkString(info.SzUserUniqueID),
                ["Score"] = info.NScore.ToString(),
                ["Similarity"] = info.NSimilarity.ToString(),
                ["AliveFlag"] = info.NAliveFlag.ToString(),
                ["EventTime"] = eventTime.ToString("O"),
                ["RealUTCUsed"] = (info.BRealUtc && info.RealUtc.IsValid).ToString(),
            }
        };

        if (!info.BStatus)
        {
            skipReason = "Access-control event status is failed";
            return false;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            skipReason = "Access-control event has empty UserID";
            return false;
        }

        return true;
    }

    public static bool IsFaceMethod(int openMethod) => FaceMethodValues.Contains(openMethod);

    internal static string DecodeSdkString(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return string.Empty;
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        if (length == 0) return string.Empty;

        var slice = bytes.AsSpan(0, length).ToArray();
        try
        {
            return Encoding.UTF8.GetString(slice).Trim('\0', ' ', '\r', '\n', '\t');
        }
        catch
        {
            return Encoding.Default.GetString(slice).Trim('\0', ' ', '\r', '\n', '\t');
        }
    }

    private static string ChooseName(AlarmAccessControlEventInfo info, string? userId)
    {
        if (info.BUseCardNameEx)
        {
            var cardNameEx = DecodeSdkString(info.SzCardNameEx);
            if (!string.IsNullOrWhiteSpace(cardNameEx)) return cardNameEx;
        }

        var cardName = DecodeSdkString(info.SzCardName);
        if (!string.IsNullOrWhiteSpace(cardName)) return cardName;

        var citizenName = DecodeSdkString(info.SzCitizenName);
        if (!string.IsNullOrWhiteSpace(citizenName)) return citizenName;

        return string.IsNullOrWhiteSpace(userId) ? string.Empty : userId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetTime
    {
        public uint DwYear;
        public uint DwMonth;
        public uint DwDay;
        public uint DwHour;
        public uint DwMinute;
        public uint DwSecond;

        public readonly bool IsValid => DwYear is >= 1970 and <= 9999 && DwMonth is >= 1 and <= 12 && DwDay is >= 1 and <= 31;

        public readonly DateTimeOffset ToDateTimeOffset() =>
            IsValid
                ? new DateTimeOffset((int)DwYear, (int)DwMonth, (int)DwDay, (int)DwHour, (int)DwMinute, (int)DwSecond, TimeSpan.Zero)
                : DateTimeOffset.UtcNow;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetTimeEx
    {
        public uint DwYear;
        public uint DwMonth;
        public uint DwDay;
        public uint DwHour;
        public uint DwMinute;
        public uint DwSecond;
        public uint DwMillisecond;
        public uint DwUtc;
        public uint DwReserved0;

        public readonly bool IsValid => DwYear is >= 1970 and <= 9999 && DwMonth is >= 1 and <= 12 && DwDay is >= 1 and <= 31;

        public readonly DateTimeOffset ToDateTimeOffset() =>
            IsValid
                ? new DateTimeOffset((int)DwYear, (int)DwMonth, (int)DwDay, (int)DwHour, (int)DwMinute, (int)DwSecond, (int)Math.Min(DwMillisecond, 999), TimeSpan.Zero)
                : DateTimeOffset.UtcNow;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetManTemperatureInfo
    {
        public float FCurrentTemperature;
        public int EmTemperatureUnit;
        [MarshalAs(UnmanagedType.Bool)] public bool BIsOverTemperature;
        public int EmTemperatureStatus;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] ByReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetTestResult
    {
        public uint NHandValue;
        public uint NLeftFootValue;
        public uint NRightFootValue;
        public int EmEsdResult;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] BReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetVaccineInfo
    {
        public int NVaccinateFlag;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SzVaccineName;
        public int NDateCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzVaccinateDate;
        public int NVaccineIntensifyFlag;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1020)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetTravelInfo
    {
        public int EmTravelCodeColor;
        public int NCityCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)] public byte[] SzPassingCity;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetHsjcInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzHSJCReportDate;
        public int NHSJCExpiresIn;
        public int NHSJCResult;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzHSJCInstitution;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 768)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetAntigenInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzAntigenReportDate;
        public int NAntigenStatus;
        public int NAntigenExpiresIn;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetAccessCtlObjectProperties
    {
        public uint NRedScarfResult;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetButtonControlInfo
    {
        public int NOperate;
        public uint NDoorIndex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)] public byte[] SzReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AlarmAccessControlEventInfo
    {
        public uint DwSize;
        public int NDoor;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SzDoorName;
        public NetTime StuTime;
        public int EmEventType;
        [MarshalAs(UnmanagedType.Bool)] public bool BStatus;
        public int EmCardType;
        public int EmOpenMethod;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzCardNo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzPwd;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzReaderID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzUserID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzSnapURL;
        public int NErrorCode;
        public int NPunchingRecNo;
        public int NNumbers;
        public int EmStatus;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzSN;
        public int EmAttendanceState;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] SzQRCode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] SzCallLiftFloor;
        public int EmCardState;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] public byte[] SzCitizenIDNo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 192)] public byte[] SzCompanionCards;
        public int NCompanionCardCount;
        public int EmHatStyle;
        public int EmHatColor;
        public int EmLiftCallerType;
        [MarshalAs(UnmanagedType.Bool)] public bool BManTemperature;
        public NetManTemperatureInfo StuManTemperatureInfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzCitizenName;
        public int EmMask;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzCardName;
        public uint NFaceIndex;
        public int EmUserType;
        [MarshalAs(UnmanagedType.Bool)] public bool BRealUtc;
        public NetTimeEx RealUtc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 200)] public byte[] SzCompanyName;
        public int NScore;
        public int NLiftNo;
        public int EmQRCodeIsExpired;
        public int EmQRCodeState;
        public NetTime StuQRCodeValidTo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzDynPWD;
        public uint NBlockId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzSection;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzWorkClass;
        public int EmTestItems;
        public NetTestResult StuTestResult;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SzDeviceID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SzUserUniqueID;
        [MarshalAs(UnmanagedType.Bool)] public bool BUseCardNameEx;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] SzCardNameEx;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzTempPassword;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] SzNote;
        public int NHSJCResult;
        public NetVaccineInfo StuVaccineInfo;
        public NetTravelInfo StuTravelInfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)] public byte[] SzQRCodeEx;
        public NetHsjcInfo StuHSJCInfo;
        public NetAntigenInfo StuAntigenInfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] public byte[] SzHealthGreenStatus;
        public int NAge;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzCheckOutType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] SzCheckOutCause;
        public int NTargetCheck;
        public NetAccessCtlObjectProperties StuObjectProperties;
        public NetButtonControlInfo StuButtonControlInfo;
        public uint NSimilarity;
        public int NPassResult;
        public int NCustomerPWDType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)] public byte[] SzOperatorID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzTransmissionUuid;
        public ulong NAKID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SzBTCardNo;
        public int NRemoteQRCodeType;
        public int NOperationMode;
        public int NAliveFlag;
        public uint NButtonCheck;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] SzUUID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SzGPSInfo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 880)] public byte[] SzReserved;
    }
}
