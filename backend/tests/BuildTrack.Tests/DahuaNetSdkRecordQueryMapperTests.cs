using System.Buffers.Binary;
using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkRecordQueryMapperTests
{
    [Fact]
    public void RecordTypeAccessControlCardRecEx_MatchesSdkEnumOrder()
    {
        // dhnetsdk.h enum order: UNKNOWN=0, ... HEALTHCARENOTICE=15, ACCESSCTLCARDREC_EX=16.
        Assert.Equal(16, DahuaNetSdkRecordQueryMapper.RecordTypeAccessControlCardRecEx);
        Assert.Equal(6, DahuaNetSdkRecordQueryMapper.RecordTypeAccessControlCardRecLegacy);
    }

    [Fact]
    public void TryMapAccessControlCardRecord_MapsKnownFaceRecord()
    {
        var payload = CreatePayload(
            recNo: 692,
            userId: "1",
            cardName: "ilham",
            status: true,
            method: 16,
            direction: 1,
            url: "/mnt/appdata1/userpic/SnapShot/2026-07-16/04/00/1_99_0_20260716040049771.jpg");
        var baku = TimeZoneInfo.CreateCustomTimeZone("Asia/Baku-Test", TimeSpan.FromHours(4), "Asia/Baku-Test", "Asia/Baku-Test");

        var mapped = DahuaNetSdkRecordQueryMapper.TryMapAccessControlCardRecord(payload, baku, out var record, out var error);

        Assert.True(mapped, error);
        Assert.Equal(692, record.RecNo);
        Assert.Equal("1", record.UserId);
        Assert.Equal("ilham", record.CardName);
        Assert.Equal("1", record.StatusRaw);
        Assert.Equal("15", record.MethodRaw);
        Assert.Equal("Entry", record.Type);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal(AttendanceEventStatus.Ok, record.NormalizedStatus);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 0, 0, 49, TimeSpan.Zero), record.CreateTime);
        Assert.Contains("20260716040049771", record.Url);
    }

    [Fact]
    public void TryMapAccessControlCardRecord_MapsFailedUnknownFaceForSecurityPipeline()
    {
        var payload = CreatePayload(
            recNo: 693,
            userId: string.Empty,
            cardName: string.Empty,
            status: false,
            method: 16,
            direction: 1,
            url: "/mnt/appdata1/userpic/SnapShot/2026-07-16/04/01/0_0_20260716040100000.jpg",
            errorCode: 16);
        var baku = TimeZoneInfo.CreateCustomTimeZone("Asia/Baku-Test", TimeSpan.FromHours(4), "Asia/Baku-Test", "Asia/Baku-Test");

        var mapped = DahuaNetSdkRecordQueryMapper.TryMapAccessControlCardRecord(payload, baku, out var record, out var error);

        Assert.True(mapped, error);
        Assert.Equal(AttendanceEventStatus.Stranger, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal("16", record.RawFields["ErrorCode"]);
        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(record));
    }
    [Fact]
    public void CursorFilter_HappensAfterMapping()
    {
        var records = Enumerable.Range(696, 7)
            .Select(recNo => new DahuaAccessRecord
            {
                RecNo = recNo,
                UserId = "1",
                CardName = "ilham",
                StatusRaw = "1",
                MethodRaw = "15",
                Type = "Entry",
            })
            .ToList();

        var result = DahuaNetSdkRecordQueryCursor.Apply(records, 692);

        Assert.Equal(7, result.CandidateRecords.Count);
        Assert.Equal(702, result.LastRecNo);
    }

    [Fact]
    public void CursorFilter_KeepsCursorWhenNoMappedRecords()
    {
        var result = DahuaNetSdkRecordQueryCursor.Apply([], 692);

        Assert.Empty(result.CandidateRecords);
        Assert.Equal(692, result.LastRecNo);
    }

    [Fact]
    public void DefaultEmptyMappedRecord_IsInvalidAndDoesNotBecomeCandidate()
    {
        var payload = new byte[DahuaNetSdkRecordQueryMapper.DefaultRecordBufferBytes];
        var baku = TimeZoneInfo.CreateCustomTimeZone("Asia/Baku-Test", TimeSpan.FromHours(4), "Asia/Baku-Test", "Asia/Baku-Test");

        var mapped = DahuaNetSdkRecordQueryMapper.TryMapAccessControlCardRecord(payload, baku, out var record, out var error);
        var valid = DahuaNetSdkRecordQueryMapper.IsValidAccessRecord(record, out var reason);
        var cursor = DahuaNetSdkRecordQueryCursor.Apply([record], 692);

        Assert.True(mapped, error);
        Assert.False(valid);
        Assert.Contains("InvalidMappedRecord", reason);
        Assert.Empty(cursor.CandidateRecords);
        Assert.Equal(692, cursor.LastRecNo);
    }

    [Fact]
    public void QueryAttemptJson_IncludesNativeFindRecordAndFindNextDiagnostics()
    {
        var strategy = DahuaNetSdkRecordQueryStrategy.Ex(
            "B_EX_NoCondition",
            "None",
            [],
            batchSize: 20);
        var attempt = DahuaNetSdkRecordQueryAttempt.Create(
            strategy,
            findRecordReturnBool: true,
            findRecordReturnHandle: new IntPtr(123),
            findRecordNativeErrorSigned: 0,
            findNextReturnBool: true,
            findNextNativeErrorSigned: 0,
            findNextCalls: 1,
            nativeReturnedRecords: 1,
            suspiciousEmptyBufferCount: 0,
            zeroBufferRecords: 0,
            outParamRetRecordNum: 0,
            outputBufferFirst256Hex: "ABCD",
            outputBufferFirst1024BeforeHex: "",
            outputBufferFirst1024AfterHex: "ABCD",
            findNextInputBytesHex: "0102",
            findNextOutputBeforeBytesHex: "0304",
            findNextOutputAfterBytesHex: "0506",
            recordBufferLength: DahuaNetSdkRecordQueryMapper.RecordStructBytes * 20,
            mappedRecords: [],
            validMappedRecords: [],
            mappedRecordDiagnostics: [],
            invalidMappedRecordDiagnostics: [],
            error: null);

        var json = JsonSerializer.Serialize(new { attempts = new[] { attempt } });

        Assert.Contains("FindRecordReturnHandle", json);
        Assert.Contains("FindNextReturnBool", json);
        Assert.Contains("OutParamRetRecordNum", json);
        Assert.Contains("OutputBufferFirst256Hex", json);
        Assert.Contains("SuspiciousEmptyBufferCount", json);
    }

    [Fact]
    public void RecordQueryLayoutDiagnostics_PlacesRecordListAndReturnCountInOutFindNextStruct()
    {
        var layouts = DahuaNetSdkNativeClient.GetRecordQueryLayoutDiagnostics();
        var outNext = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(layouts["NET_OUT_FIND_NEXT_RECORD_PARAM"]);
        var offsets = Assert.IsAssignableFrom<Dictionary<string, int>>(outNext["Offsets"]);

        Assert.Equal(24, outNext["Size"]);
        Assert.Equal(8, offsets["RecordList"]);
        Assert.Equal(16, offsets["MaxRecordNum"]);
        Assert.Equal(20, offsets["ReturnRecordNum"]);
    }

    [Fact]
    public void SuspiciousEmptyBufferAttempt_DoesNotCountAsNativeReturnedRecord()
    {
        var strategy = DahuaNetSdkRecordQueryStrategy.Ex(
            "B_EX_NoCondition_Batch1",
            "None",
            [],
            batchSize: 1);
        var attempt = DahuaNetSdkRecordQueryAttempt.Create(
            strategy,
            findRecordReturnBool: true,
            findRecordReturnHandle: new IntPtr(123),
            findRecordNativeErrorSigned: 0,
            findNextReturnBool: true,
            findNextNativeErrorSigned: 0,
            findNextCalls: 1,
            nativeReturnedRecords: 0,
            suspiciousEmptyBufferCount: 1,
            zeroBufferRecords: 1,
            outParamRetRecordNum: 1,
            outputBufferFirst256Hex: new string('0', 512),
            outputBufferFirst1024BeforeHex: new string('0', 2048),
            outputBufferFirst1024AfterHex: new string('0', 2048),
            findNextInputBytesHex: "0102",
            findNextOutputBeforeBytesHex: "0304",
            findNextOutputAfterBytesHex: "0506",
            recordBufferLength: DahuaNetSdkRecordQueryMapper.RecordStructBytes,
            mappedRecords: [],
            validMappedRecords: [],
            mappedRecordDiagnostics: [],
            invalidMappedRecordDiagnostics:
            [
                new Dictionary<string, string?>
                {
                    ["reason"] = "SuspiciousEmptyBuffer",
                    ["nRetRecordNum"] = "1",
                }
            ],
            error: null);

        Assert.Equal(0, attempt.NativeReturnedRecords);
        Assert.Equal(1, attempt.SuspiciousEmptyBufferCount);
        Assert.Equal(1, attempt.ZeroBufferRecords);
        Assert.Empty(attempt.MappedRecords);
        Assert.Empty(attempt.ValidMappedRecords);
    }

    private static byte[] CreatePayload(int recNo, string userId, string cardName, bool status, int method, int direction, string url, int errorCode = 0)
    {
        var payload = new byte[DahuaNetSdkRecordQueryMapper.DefaultRecordBufferBytes];
        WriteUInt32(payload, 0, 0);
        WriteInt32(payload, 4, recNo);
        WriteTime(payload, 104, 2026, 7, 16, 4, 0, 49);
        WriteInt32(payload, 128, status ? 1 : 0);
        WriteInt32(payload, 132, method);
        WriteInt32(payload, 136, 1);
        WriteString(payload, 140, 32, userId);
        WriteInt32(payload, 472, errorCode);
        WriteString(payload, 476, 128, url);
        WriteInt32(payload, 612, direction);
        WriteString(payload, 664, 64, cardName);
        return payload;
    }

    private static void WriteTime(byte[] payload, int offset, uint year, uint month, uint day, uint hour, uint minute, uint second)
    {
        WriteUInt32(payload, offset, year);
        WriteUInt32(payload, offset + 4, month);
        WriteUInt32(payload, offset + 8, day);
        WriteUInt32(payload, offset + 12, hour);
        WriteUInt32(payload, offset + 16, minute);
        WriteUInt32(payload, offset + 20, second);
    }

    private static void WriteInt32(byte[] payload, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, 4), value);

    private static void WriteUInt32(byte[] payload, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), value);

    private static void WriteString(byte[] payload, int offset, int length, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        Array.Copy(bytes, 0, payload, offset, Math.Min(length - 1, bytes.Length));
    }
}
