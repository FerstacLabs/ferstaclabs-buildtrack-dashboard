using System.Globalization;
using System.Text.RegularExpressions;
using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public static partial class DahuaCgiRecordParser
{
    private static readonly Regex RecordRegex = BuildRecordRegex();

    public static IReadOnlyList<DahuaAccessRecord> ParseKeyValueResponse(string text, TimeZoneInfo? deviceTimeZone = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<DahuaAccessRecord>();

        var records = new Dictionary<int, DahuaAccessRecord>();
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawLine.IndexOf('=');
            if (separator <= 0) continue;

            var key = rawLine[..separator].Trim();
            var value = rawLine[(separator + 1)..].Trim();
            var match = RecordRegex.Match(key);
            if (!match.Success) continue;

            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var field = match.Groups[2].Value;
            if (!records.TryGetValue(index, out var record))
            {
                record = new DahuaAccessRecord();
                records[index] = record;
            }

            record.RawFields[field] = value;
            ApplyField(record, field, value, deviceTimeZone);
        }

        foreach (var record in records.Values)
        {
            PreferSnapshotTimestamp(record, deviceTimeZone);
        }

        return records.OrderBy(x => x.Key).Select(x => x.Value).ToList();
    }

    private static void ApplyField(DahuaAccessRecord record, string field, string value, TimeZoneInfo? deviceTimeZone)
    {
        switch (field)
        {
            case "RecNo" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recNo):
                record.RecNo = recNo;
                break;
            case "CreateTime":
                record.CreateTime = ParseCreateTime(value, deviceTimeZone);
                break;
            case "CardName":
                record.CardName = value;
                break;
            case "UserID":
                record.UserId = value;
                break;
            case "Status":
                record.StatusRaw = value;
                break;
            case "Method":
                record.MethodRaw = value;
                break;
            case "Type":
                record.Type = value;
                break;
            case "URL":
                record.Url = value;
                break;
        }
    }

    public static DateTimeOffset ParseCreateTime(string value, TimeZoneInfo? deviceTimeZone = null)
    {
        var trimmed = value.Trim();
        var timeZone = deviceTimeZone ?? TimeZoneInfo.Local;

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offsetTime)
            && HasExplicitOffset(trimmed))
        {
            return offsetTime.ToUniversalTime();
        }

        if (IsDigitsOnly(trimmed))
        {
            if (trimmed.Length == 14
                && DateTime.TryParseExact(trimmed, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compactLocalTime))
            {
                return ConvertLocalDeviceTimeToUtc(compactLocalTime, timeZone);
            }

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            {
                return unix > 9_999_999_999
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                    : DateTimeOffset.FromUnixTimeSeconds(unix);
            }
        }

        if (DateTime.TryParseExact(
                trimmed,
                ["yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localDateTime)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out localDateTime))
        {
            return ConvertLocalDeviceTimeToUtc(localDateTime, timeZone);
        }

        return DateTimeOffset.UtcNow;
    }

    private static void PreferSnapshotTimestamp(DahuaAccessRecord record, TimeZoneInfo? deviceTimeZone)
    {
        if (string.IsNullOrWhiteSpace(record.Url)) return;
        var match = SnapshotTimestampRegex().Match(record.Url);
        if (!match.Success) return;
        record.CreateTime = ParseCreateTime(match.Groups[1].Value, deviceTimeZone);
        record.RawFields["CreateTimeSource"] = "SnapshotPath";
    }

    private static DateTimeOffset ConvertLocalDeviceTimeToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private static bool IsDigitsOnly(string value) => value.Length > 0 && value.All(char.IsDigit);

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return true;
        return Regex.IsMatch(value, @"(?:T|\s)\d{2}:\d{2}:\d{2}(?:\.\d+)?[+-]\d{2}:?\d{2}$", RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"^records\[(\d+)\]\.(.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex BuildRecordRegex();

    [GeneratedRegex(@"(?<!\d)(\d{14})(?:\d{3,})?(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SnapshotTimestampRegex();
}





