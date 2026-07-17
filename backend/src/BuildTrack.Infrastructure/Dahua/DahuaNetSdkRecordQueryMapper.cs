using System.Buffers.Binary;
using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaNetSdkRecordQueryMapper
{
    public const int RecordTypeAccessControlCardRecLegacy = 6;
    public const int RecordTypeAccessControlCardRecEx = 16;
    public const int AccessControlCardRecordMinimumBytes = 1164;
    public const int DefaultRecordBufferBytes = 64 * 1024;

    private const int OffsetDwSize = 0;
    private const int OffsetRecNo = 4;
    private const int OffsetCardNo = 8;
    private const int OffsetTime = 104;
    private const int OffsetStatus = 128;
    private const int OffsetMethod = 132;
    private const int OffsetDoor = 136;
    private const int OffsetUserId = 140;
    private const int OffsetSnapFtpUrl = 176;
    private const int OffsetErrorCode = 472;
    private const int OffsetRecordUrl = 476;
    private const int OffsetDirection = 612;
    private const int OffsetCardName = 664;
    private const int OffsetSnapFaceUrl = 1036;

    public static bool TryMapAccessControlCardRecord(byte[] payload, TimeZoneInfo deviceTimeZone, out DahuaAccessRecord record, out string? error, string recordTypeName = "NET_RECORD_ACCESSCTLCARDREC_EX")
    {
        record = new DahuaAccessRecord();
        error = null;

        if (payload.Length < AccessControlCardRecordMinimumBytes)
        {
            error = $"Payload too small for NET_RECORDSET_ACCESS_CTL_CARDREC. PayloadBytes={payload.Length}, RequiredBytes={AccessControlCardRecordMinimumBytes}";
            return false;
        }

        try
        {
            var dwSize = ReadUInt32(payload, OffsetDwSize);
            var recNo = ReadInt32(payload, OffsetRecNo);
            var cardNo = ReadString(payload, OffsetCardNo, 32);
            var eventTime = ReadNetTimeAsUtc(payload, OffsetTime, deviceTimeZone);
            var status = ReadBool(payload, OffsetStatus) ? "1" : "0";
            var openMethod = ReadInt32(payload, OffsetMethod);
            var door = ReadInt32(payload, OffsetDoor);
            var userId = ReadString(payload, OffsetUserId, 32);
            var snapFtpUrl = ReadString(payload, OffsetSnapFtpUrl, 260);
            var errorCode = ReadInt32(payload, OffsetErrorCode);
            var recordUrl = ReadString(payload, OffsetRecordUrl, 128);
            var directionRaw = ReadInt32(payload, OffsetDirection);
            var cardName = ReadString(payload, OffsetCardName, 64);
            var snapFaceUrl = ReadString(payload, OffsetSnapFaceUrl, 128);
            var url = FirstNonEmpty(recordUrl, snapFtpUrl, snapFaceUrl);
            var method = DahuaNetSdkAccessEventDecoder.IsFaceMethod(openMethod) ? "15" : openMethod.ToString();
            var direction = directionRaw switch
            {
                1 => "Entry",
                2 => "Exit",
                _ => "Unknown",
            };

            record = new DahuaAccessRecord
            {
                RecNo = recNo > 0 ? recNo : null,
                CreateTime = eventTime,
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                CardName = string.IsNullOrWhiteSpace(cardName) ? null : cardName,
                StatusRaw = status,
                MethodRaw = method,
                Type = direction,
                Url = string.IsNullOrWhiteSpace(url) ? null : url,
                RawFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Source"] = DahuaEventSourceExtensions.ActiveRegisterSource,
                    ["NetSdkRecordType"] = recordTypeName,
                    ["NetSdkStruct"] = "NET_RECORDSET_ACCESS_CTL_CARDREC",
                    ["dwSize"] = dwSize.ToString(),
                    ["RecNo"] = recNo > 0 ? recNo.ToString() : null,
                    ["UserID"] = userId,
                    ["CardName"] = cardName,
                    ["CardNo"] = cardNo,
                    ["Status"] = status,
                    ["ErrorCode"] = errorCode.ToString(),
                    ["OpenMethodRaw"] = openMethod.ToString(),
                    ["Method"] = method,
                    ["Door"] = door.ToString(),
                    ["DirectionRaw"] = directionRaw.ToString(),
                    ["Type"] = direction,
                    ["RecordURL"] = recordUrl,
                    ["SnapFtpUrl"] = snapFtpUrl,
                    ["SnapFaceURL"] = snapFaceUrl,
                    ["URL"] = url,
                    ["CreateTime"] = eventTime.ToString("O"),
                },
            };

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static DateTimeOffset ReadNetTimeAsUtc(byte[] payload, int offset, TimeZoneInfo deviceTimeZone)
    {
        var year = ReadUInt32(payload, offset);
        var month = ReadUInt32(payload, offset + 4);
        var day = ReadUInt32(payload, offset + 8);
        var hour = ReadUInt32(payload, offset + 12);
        var minute = ReadUInt32(payload, offset + 16);
        var second = ReadUInt32(payload, offset + 20);

        if (year is < 1970 or > 9999 || month is < 1 or > 12 || day is < 1 or > 31)
        {
            return DateTimeOffset.UtcNow;
        }

        var local = new DateTime((int)year, (int)month, (int)day, (int)hour, (int)minute, (int)second, DateTimeKind.Unspecified);
        var offsetValue = deviceTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offsetValue).ToUniversalTime();
    }

    private static int ReadInt32(byte[] payload, int offset) => BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] payload, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));

    private static bool ReadBool(byte[] payload, int offset) => ReadInt32(payload, offset) != 0;

    private static string ReadString(byte[] payload, int offset, int length)
    {
        if (offset >= payload.Length) return string.Empty;
        var available = Math.Min(length, payload.Length - offset);
        return DahuaNetSdkAccessEventDecoder.DecodeSdkString(payload.AsSpan(offset, available).ToArray());
    }

    private static string? FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public static class DahuaNetSdkRecordQueryCursor
{
    public static DahuaNetSdkRecordQueryCursorResult Apply(IEnumerable<DahuaAccessRecord> mappedRecords, long cursor)
    {
        var candidates = mappedRecords
            .Where(x => x.RecNo is not null && x.RecNo > cursor)
            .OrderBy(x => x.RecNo)
            .ToList();
        var lastRecNo = candidates.Count == 0
            ? cursor
            : candidates.Max(x => x.RecNo ?? cursor);

        return new DahuaNetSdkRecordQueryCursorResult(candidates, lastRecNo);
    }
}

public sealed record DahuaNetSdkRecordQueryCursorResult(
    IReadOnlyList<DahuaAccessRecord> CandidateRecords,
    long LastRecNo);
