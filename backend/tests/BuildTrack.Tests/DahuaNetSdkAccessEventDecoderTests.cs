using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkAccessEventDecoderTests
{
    [Fact]
    public void TryDecode_MapsSuccessfulFaceEventToAttendanceRecord()
    {
        var info = SuccessfulInfo(openMethod: 16, eventType: DahuaNetSdkAccessEventDecoder.EventTypeEntry);

        var decoded = DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out var skipReason);
        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.True(decoded);
        Assert.Null(skipReason);
        Assert.Equal("1", record.UserId);
        Assert.Equal("Ilham", record.CardName);
        Assert.Equal(AttendanceEventStatus.Ok, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal(AttendanceDirection.Entry, record.NormalizedDirection);
        Assert.Equal(12345, record.RecNo);
        Assert.True(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record));
    }

    [Fact]
    public void TryDecode_UsesCardNameExWhenFlagIsSet()
    {
        var info = SuccessfulInfo();
        info.BUseCardNameEx = true;
        info.SzCardNameEx = Bytes(128, "Ilham Ex");

        DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out _);

        Assert.Equal("Ilham Ex", sdkEvent.CardName);
    }

    [Fact]
    public void TryDecode_FallsBackNameFromCardNameCitizenNameThenUserId()
    {
        var cardName = SuccessfulInfo();
        cardName.SzCardName = Bytes(64, "Card Name");
        cardName.SzCitizenName = Bytes(256, "Citizen Name");

        var citizenName = SuccessfulInfo();
        citizenName.SzCardName = Bytes(64, "");
        citizenName.SzCitizenName = Bytes(256, "Citizen Name");

        var userId = SuccessfulInfo();
        userId.SzCardName = Bytes(64, "");
        userId.SzCitizenName = Bytes(256, "");

        DahuaNetSdkAccessEventDecoder.TryDecode(cardName, out var cardNameEvent, out _);
        DahuaNetSdkAccessEventDecoder.TryDecode(citizenName, out var citizenNameEvent, out _);
        DahuaNetSdkAccessEventDecoder.TryDecode(userId, out var userIdEvent, out _);

        Assert.Equal("Card Name", cardNameEvent.CardName);
        Assert.Equal("Citizen Name", citizenNameEvent.CardName);
        Assert.Equal("1", userIdEvent.CardName);
    }

    [Theory]
    [InlineData(1, AttendanceDirection.Entry)]
    [InlineData(2, AttendanceDirection.Exit)]
    public void TryDecode_MapsEntryAndExit(int eventType, AttendanceDirection expectedDirection)
    {
        var info = SuccessfulInfo(eventType: eventType);

        DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out _);
        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.Equal(expectedDirection, record.NormalizedDirection);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(26)]
    public void TryDecode_MapsKnownFaceMethods(int method)
    {
        var info = SuccessfulInfo(openMethod: method);

        DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out _);
        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
    }

    [Fact]
    public void TryDecode_SkipsFailedEvent()
    {
        var info = SuccessfulInfo();
        info.BStatus = false;

        var decoded = DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out var skipReason);
        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.False(decoded);
        Assert.Contains("failed", skipReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record));
    }

    [Fact]
    public void TryDecode_SkipsEmptyUserIdAsStranger()
    {
        var info = SuccessfulInfo();
        info.SzUserID = Bytes(64, "");

        var decoded = DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out var skipReason);
        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.False(decoded);
        Assert.Contains("UserID", skipReason);
        Assert.False(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record));
    }

    [Fact]
    public void TryDecode_UsesRealUtcWhenAvailable()
    {
        var info = SuccessfulInfo();
        info.BRealUtc = true;
        info.StuTime = Time(2026, 1, 1, 8, 0, 0);
        info.RealUtc = TimeEx(2026, 7, 7, 4, 15, 30, 120);

        DahuaNetSdkAccessEventDecoder.TryDecode(info, out var sdkEvent, out _);

        Assert.Equal(DateTimeOffset.Parse("2026-07-07T04:15:30.120+00:00"), sdkEvent.EventTime);
    }

    private static DahuaNetSdkAccessEventDecoder.AlarmAccessControlEventInfo SuccessfulInfo(int openMethod = 16, int eventType = 1)
    {
        return new DahuaNetSdkAccessEventDecoder.AlarmAccessControlEventInfo
        {
            DwSize = 1,
            BStatus = true,
            EmOpenMethod = openMethod,
            EmEventType = eventType,
            NPunchingRecNo = 12345,
            StuTime = Time(2026, 7, 7, 8, 15, 0),
            SzUserID = Bytes(64, "1"),
            SzCardName = Bytes(64, "Ilham"),
            SzCitizenName = Bytes(256, "Citizen Ilham"),
            SzSnapURL = Bytes(256, "/snapshots/12345.jpg"),
            SzDeviceID = Bytes(128, "BT-API-TEST-001"),
            SzUserUniqueID = Bytes(128, "person-1"),
            NScore = 95,
            NSimilarity = 96,
            NAliveFlag = 1,
        };
    }

    private static DahuaNetSdkAccessEventDecoder.NetTime Time(uint year, uint month, uint day, uint hour, uint minute, uint second) => new()
    {
        DwYear = year,
        DwMonth = month,
        DwDay = day,
        DwHour = hour,
        DwMinute = minute,
        DwSecond = second,
    };

    private static DahuaNetSdkAccessEventDecoder.NetTimeEx TimeEx(uint year, uint month, uint day, uint hour, uint minute, uint second, uint millisecond) => new()
    {
        DwYear = year,
        DwMonth = month,
        DwDay = day,
        DwHour = hour,
        DwMinute = minute,
        DwSecond = second,
        DwMillisecond = millisecond,
    };

    private static byte[] Bytes(int size, string value)
    {
        var bytes = new byte[size];
        var source = System.Text.Encoding.UTF8.GetBytes(value);
        Array.Copy(source, bytes, Math.Min(source.Length, size));
        return bytes;
    }
}
