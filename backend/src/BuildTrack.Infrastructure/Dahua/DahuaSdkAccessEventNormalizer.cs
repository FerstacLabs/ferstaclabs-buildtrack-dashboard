using System.Text;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSdkAccessEventNormalizer
{
    public static bool TryNormalize(DahuaSdkAccessEvent sdkEvent, out DahuaAccessRecord record)
    {
        record = new DahuaAccessRecord
        {
            RecNo = sdkEvent.RecNo,
            CreateTime = sdkEvent.EventTime ?? DateTimeOffset.UtcNow,
            UserId = string.IsNullOrWhiteSpace(sdkEvent.UserId) ? null : sdkEvent.UserId.Trim(),
            CardName = string.IsNullOrWhiteSpace(sdkEvent.CardName) ? null : sdkEvent.CardName.Trim(),
            StatusRaw = NormalizeStatus(sdkEvent.Status),
            MethodRaw = NormalizeMethod(sdkEvent.Method),
            Type = NormalizeDirection(sdkEvent.Direction),
            Url = string.IsNullOrWhiteSpace(sdkEvent.SnapshotPath) ? null : sdkEvent.SnapshotPath.Trim(),
            RawFields = sdkEvent.RawFields.Count == 0
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(sdkEvent.RawFields, StringComparer.OrdinalIgnoreCase),
        };

        record.RawFields.TryAdd("UserID", record.UserId);
        record.RawFields.TryAdd("CardName", record.CardName);
        record.RawFields.TryAdd("Status", record.StatusRaw);
        record.RawFields.TryAdd("Method", record.MethodRaw);
        record.RawFields.TryAdd("Type", record.Type);
        record.RawFields.TryAdd("Source", DahuaEventSourceExtensions.ActiveRegisterSource);
        if (record.RecNo is not null) record.RawFields.TryAdd("RecNo", record.RecNo.Value.ToString());
        record.RawFields.TryAdd("CreateTime", record.CreateTime.ToString("O"));

        return true;
    }

    public static bool ShouldInsertPayrollAttendance(DahuaAccessRecord record) =>
        record.NormalizedStatus == AttendanceEventStatus.Ok
        && !string.IsNullOrWhiteSpace(record.UserId)
        && !string.IsNullOrWhiteSpace(record.CardName)
        && record.NormalizedMethod is AttendanceMethod.Face or AttendanceMethod.Card or AttendanceMethod.Fingerprint;

    public static bool TryParseAsciiKeyValuePayload(byte[] payload, out DahuaSdkAccessEvent sdkEvent)
    {
        sdkEvent = new DahuaSdkAccessEvent();
        if (payload.Length == 0) return false;

        var preview = Encoding.ASCII.GetString(payload.Where(value => value != 0).ToArray());
        if (string.IsNullOrWhiteSpace(preview)) return false;

        var fields = preview
            .Split(['\r', '\n', ';', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .ToDictionary(parts => parts[0], parts => (string?)parts[1], StringComparer.OrdinalIgnoreCase);

        if (fields.Count == 0) return false;

        sdkEvent.RegisterDeviceId = Get(fields, "DeviceID", "DeviceId", "RegisterDeviceId", "RegisterID");
        sdkEvent.UserId = Get(fields, "UserID", "UserId", "User", "UID");
        sdkEvent.CardName = Get(fields, "CardName", "UserName", "Name");
        sdkEvent.Status = Get(fields, "Status", "OpenDoorStatus", "Result");
        sdkEvent.Method = Get(fields, "Method", "OpenDoorMethod", "VerifyMethod");
        sdkEvent.Direction = Get(fields, "Type", "Direction", "AccessType");
        sdkEvent.SnapshotPath = Get(fields, "URL", "Url", "SnapshotPath");
        sdkEvent.RawFields = fields;

        if (long.TryParse(Get(fields, "RecNo", "RecordNo", "RecordNumber"), out var recNo)) sdkEvent.RecNo = recNo;
        if (DateTimeOffset.TryParse(Get(fields, "CreateTime", "EventTime", "Time"), out var eventTime)) sdkEvent.EventTime = eventTime;

        return !string.IsNullOrWhiteSpace(sdkEvent.UserId)
               || !string.IsNullOrWhiteSpace(sdkEvent.CardName)
               || !string.IsNullOrWhiteSpace(sdkEvent.Status)
               || !string.IsNullOrWhiteSpace(sdkEvent.Method);
    }

    private static string NormalizeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim();
        return normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("ok", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("success", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
            ? "1"
            : "0";
    }

    private static string NormalizeMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim();
        if (normalized.Equals("15", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("face", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("face recognition", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("facerecognition", StringComparison.OrdinalIgnoreCase)) return "15";
        if (normalized.Equals("1", StringComparison.OrdinalIgnoreCase) || normalized.Equals("card", StringComparison.OrdinalIgnoreCase)) return "1";
        if (normalized.Equals("2", StringComparison.OrdinalIgnoreCase) || normalized.Equals("fingerprint", StringComparison.OrdinalIgnoreCase)) return "2";
        if (normalized.Equals("3", StringComparison.OrdinalIgnoreCase) || normalized.Equals("password", StringComparison.OrdinalIgnoreCase)) return "3";
        return normalized;
    }

    private static string NormalizeDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        var normalized = value.Trim();
        if (normalized.Equals("entry", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("in", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("enter", StringComparison.OrdinalIgnoreCase)) return "Entry";
        if (normalized.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("out", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("leave", StringComparison.OrdinalIgnoreCase)) return "Exit";
        return "Unknown";
    }

    private static string? Get(IReadOnlyDictionary<string, string?> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }
}


