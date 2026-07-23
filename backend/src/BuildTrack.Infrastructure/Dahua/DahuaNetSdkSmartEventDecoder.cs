using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaNetSdkSmartEventDecoder
{
    public const uint EventIvsAccessControl = 0x00000204;
    public const string EventIvsAccessControlName = "EVENT_IVS_ACCESS_CTL";
    public const string AccessControlStructName = "DEV_EVENT_ACCESS_CTL_INFO";
    private const int DiagnosticCopyBytes = 8192;

    private static readonly string[] NoiseStrings =
    [
        "AccessControl",
        "Access Control",
        "Entrance Guard",
        "Unknown",
        "Normal",
    ];

    public static DahuaSmartEventDecodeResult Decode(uint eventType, IntPtr alarmInfo, IntPtr imageBuffer, uint imageBufferSize, int sequence)
    {
        var eventName = ResolveEventName(eventType);
        if (alarmInfo == IntPtr.Zero)
        {
            return DahuaSmartEventDecodeResult.Skipped(eventType, eventName, "MissingAlarmInfo", "Smart Event callback pAlarmInfo was null", imageBufferSize, sequence);
        }

        var payload = CopyAlarmInfoPrefix(alarmInfo);
        var first256Hex = Convert.ToHexString(payload.AsSpan(0, Math.Min(256, payload.Length)));
        var strings = ExtractCandidateStrings(payload);

        if (eventType != EventIvsAccessControl)
        {
            return DahuaSmartEventDecodeResult.Skipped(eventType, eventName, "UnsupportedSmartEvent", $"Smart Event type 0x{eventType:X} is not {EventIvsAccessControlName}", imageBufferSize, sequence, first256Hex, strings);
        }

        var utc = TryReadNetTimeEx(payload, 144);
        var eventId = TryReadInt32(payload, 180);
        var channelId = TryReadInt32(payload, 0);
        var sdkEvent = BuildSdkEvent(strings, utc, eventId, channelId, imageBufferSize, sequence, first256Hex);
        var hasPersonSignal = !string.IsNullOrWhiteSpace(sdkEvent.UserId) || !string.IsNullOrWhiteSpace(sdkEvent.CardName);
        var status = hasPersonSignal ? "DecodedAccessSmartEvent" : "DecodedAccessSmartEventNoPersonFields";
        var reason = hasPersonSignal
            ? null
            : "EVENT_IVS_ACCESS_CTL received, but bounded diagnostic decode did not find UserID/CardName in DEV_EVENT_ACCESS_CTL_INFO prefix. Raw strings are stored for layout verification.";

        DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);
        var rawSummary = BuildRawSummary(eventType, eventName, status, reason, sdkEvent, strings, imageBufferSize, sequence, first256Hex);
        return new DahuaSmartEventDecodeResult(eventType, eventName, AccessControlStructName, status, reason, first256Hex, imageBufferSize, sequence, sdkEvent, record, rawSummary);
    }

    public static string ResolveEventName(uint eventType) =>
        eventType == EventIvsAccessControl ? EventIvsAccessControlName : $"UNKNOWN_SMART_EVENT_0x{eventType:X}";

    private static DahuaSdkAccessEvent BuildSdkEvent(
        IReadOnlyList<string> strings,
        DateTimeOffset? utc,
        int? eventId,
        int? channelId,
        uint imageBufferSize,
        int sequence,
        string first256Hex)
    {
        var snapshotPath = strings.FirstOrDefault(value => value.Contains("/SnapShot/", StringComparison.OrdinalIgnoreCase)
                                                           || value.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
                                                           || value.Contains(".jpeg", StringComparison.OrdinalIgnoreCase));
        var registerDeviceId = strings.FirstOrDefault(value => value.StartsWith("BT-", StringComparison.OrdinalIgnoreCase));
        var cardName = ChooseLikelyName(strings);
        var userId = ChooseLikelyUserId(strings, cardName);
        var status = string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(cardName) ? "0" : "1";

        return new DahuaSdkAccessEvent
        {
            RegisterDeviceId = registerDeviceId,
            UserId = userId,
            CardName = cardName,
            EventTime = utc ?? DateTimeOffset.UtcNow,
            Status = status,
            Method = "face",
            Direction = "Entry",
            RecNo = eventId is > 0 ? eventId.Value : null,
            SnapshotPath = snapshotPath,
            RawFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SmartEventType"] = $"0x{EventIvsAccessControl:X}",
                ["SmartEventName"] = EventIvsAccessControlName,
                ["EventStruct"] = AccessControlStructName,
                ["ParseStatus"] = "DecodedAccessSmartEvent",
                ["UserID"] = userId,
                ["CardName"] = cardName,
                ["Status"] = status,
                ["Method"] = "15",
                ["Type"] = "Entry",
                ["EventID"] = eventId?.ToString(),
                ["RecNo"] = eventId is > 0 ? eventId.Value.ToString() : null,
                ["ChannelID"] = channelId?.ToString(),
                ["SnapshotPath"] = snapshotPath,
                ["ImageBytesLength"] = imageBufferSize.ToString(),
                ["Sequence"] = sequence.ToString(),
                ["PayloadFirst256Hex"] = first256Hex,
                ["DecodedStringCandidates"] = JsonSerializer.Serialize(strings.Take(40)),
                ["Source"] = DahuaEventSourceExtensions.ActiveRegisterSource,
            },
        };
    }

    private static string? ChooseLikelyName(IReadOnlyList<string> strings)
    {
        return strings
            .Where(value => value.Length is >= 2 and <= 80)
            .Where(value => value.Any(char.IsLetter))
            .Where(value => !value.Contains('/'))
            .Where(value => !value.Contains('\\'))
            .Where(value => !value.Contains('.'))
            .Where(value => !value.StartsWith("BT-", StringComparison.OrdinalIgnoreCase))
            .Where(value => !NoiseStrings.Any(noise => value.Equals(noise, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(value => value.Length)
            .FirstOrDefault();
    }

    private static string? ChooseLikelyUserId(IReadOnlyList<string> strings, string? cardName)
    {
        var numeric = strings
            .Where(value => value.Length is >= 1 and <= 32)
            .Where(value => value.All(char.IsDigit))
            .Where(value => value != "0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (numeric.Count == 1) return numeric[0];
        if (numeric.Count > 1)
        {
            return numeric.OrderBy(value => value.Length).ThenBy(value => value).FirstOrDefault();
        }

        return !string.IsNullOrWhiteSpace(cardName) ? cardName : null;
    }

    private static DateTimeOffset? TryReadNetTimeEx(byte[] payload, int offset)
    {
        if (payload.Length < offset + 36) return null;

        var year = TryReadInt32(payload, offset);
        var month = TryReadInt32(payload, offset + 4);
        var day = TryReadInt32(payload, offset + 8);
        var hour = TryReadInt32(payload, offset + 12);
        var minute = TryReadInt32(payload, offset + 16);
        var second = TryReadInt32(payload, offset + 20);
        var millisecond = TryReadInt32(payload, offset + 24) ?? 0;

        if (year is null || month is null || day is null || hour is null || minute is null || second is null)
        {
            return null;
        }

        if (year.Value is < 1970 or > 9999 || month.Value is < 1 or > 12 || day.Value is < 1 or > 31 || hour.Value is < 0 or > 23 || minute.Value is < 0 or > 59 || second.Value is < 0 or > 59)
        {
            return null;
        }

        return new DateTimeOffset(year.Value, month.Value, day.Value, hour.Value, minute.Value, second.Value, Math.Clamp(millisecond, 0, 999), TimeSpan.Zero);
    }

    private static int? TryReadInt32(byte[] payload, int offset)
    {
        return payload.Length >= offset + 4 ? BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, 4)) : null;
    }

    private static byte[] CopyAlarmInfoPrefix(IntPtr alarmInfo)
    {
        var payload = new byte[DiagnosticCopyBytes];
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(alarmInfo, payload, 0, payload.Length);
            return payload;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ExtractCandidateStrings(byte[] payload)
    {
        var candidates = new List<string>();
        var buffer = new List<byte>();

        foreach (var value in payload)
        {
            if (value is >= 32 and <= 126)
            {
                buffer.Add(value);
                continue;
            }

            Flush();
        }

        Flush();
        return candidates
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .ToList();

        void Flush()
        {
            if (buffer.Count > 0)
            {
                var text = Encoding.UTF8.GetString(buffer.ToArray()).Trim('\0', ' ', '\r', '\n', '\t');
                if (text.Length > 0) candidates.Add(text);
                buffer.Clear();
            }
        }
    }

    private static string BuildRawSummary(
        uint eventType,
        string eventName,
        string parseStatus,
        string? reason,
        DahuaSdkAccessEvent sdkEvent,
        IReadOnlyList<string> strings,
        uint imageBufferSize,
        int sequence,
        string first256Hex) =>
        JsonSerializer.Serialize(new
        {
            SmartEventType = $"0x{eventType:X}",
            SmartEventName = eventName,
            StructName = AccessControlStructName,
            ParseStatus = parseStatus,
            Reason = reason,
            sdkEvent.UserId,
            sdkEvent.CardName,
            sdkEvent.RecNo,
            sdkEvent.EventTime,
            sdkEvent.Status,
            sdkEvent.Method,
            sdkEvent.Direction,
            sdkEvent.SnapshotPath,
            ImageBytesLength = imageBufferSize,
            Sequence = sequence,
            PayloadFirst256Hex = first256Hex,
            StringCandidates = strings.Take(60),
        });
}

public sealed record DahuaSmartEventDecodeResult(
    uint EventType,
    string EventName,
    string? StructName,
    string ParseStatus,
    string? FailureReason,
    string? PayloadFirst256Hex,
    uint ImageBytesLength,
    int Sequence,
    DahuaSdkAccessEvent? SdkEvent,
    DahuaAccessRecord? Record,
    string RawStructSummaryJson)
{
    public static DahuaSmartEventDecodeResult Skipped(
        uint eventType,
        string eventName,
        string parseStatus,
        string reason,
        uint imageBytesLength,
        int sequence,
        string? first256Hex = null,
        IReadOnlyList<string>? strings = null) =>
        new(
            eventType,
            eventName,
            null,
            parseStatus,
            reason,
            first256Hex,
            imageBytesLength,
            sequence,
            null,
            null,
            JsonSerializer.Serialize(new
            {
                SmartEventType = $"0x{eventType:X}",
                SmartEventName = eventName,
                ParseStatus = parseStatus,
                Reason = reason,
                ImageBytesLength = imageBytesLength,
                Sequence = sequence,
                PayloadFirst256Hex = first256Hex,
                StringCandidates = strings?.Take(60),
            }));
}
