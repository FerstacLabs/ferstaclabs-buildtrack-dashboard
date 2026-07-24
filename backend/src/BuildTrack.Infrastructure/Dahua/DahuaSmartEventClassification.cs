using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using System.Text.Json;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSmartEventClassification
{
    public static DahuaAccessRecord BuildTrustedRecord(DahuaAccessRecord source, string rawStructSummaryJson, Worker? resolvedWorker)
    {
        var summary = TrustedSmartEventSummary.Parse(rawStructSummaryJson);
        var trustedUserId = FirstNotBlank(summary.UserId, source.UserId);
        var trustedStatus = FirstNotBlank(summary.Status, source.StatusRaw);
        var trustedMethod = NormalizeMethod(FirstNotBlank(summary.Method, source.MethodRaw));
        var trustedDirection = FirstNotBlank(summary.Direction, source.Type);
        var trustedCardName = FirstNotBlank(summary.CardName, source.CardName);

        var workerMatches = resolvedWorker is not null
                            && !string.IsNullOrWhiteSpace(trustedUserId)
                            && string.Equals(trustedUserId, resolvedWorker.ExternalWorkerCode, StringComparison.OrdinalIgnoreCase);
        var workerResolutionStatus = workerMatches
            ? "ResolvedWorker"
            : string.IsNullOrWhiteSpace(trustedUserId)
                ? "MissingExternalWorker"
                : "UnresolvedExternalWorker";
        var displayName = workerMatches ? resolvedWorker!.FullName : trustedCardName;

        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["SmartEventTrustedSource"] = "DEV_EVENT_ACCESS_CTL_INFO",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["Status"] = trustedStatus,
            ["UserID"] = trustedUserId,
            ["UserId"] = trustedUserId,
            ["CardName"] = displayName,
            ["TrustedCardName"] = trustedCardName,
            ["WorkerResolutionStatus"] = workerResolutionStatus,
            ["Method"] = trustedMethod,
            ["Type"] = string.IsNullOrWhiteSpace(trustedDirection) ? "Entry" : trustedDirection,
        };

        if (!string.IsNullOrWhiteSpace(summary.ErrorCode)) rawFields["ErrorCode"] = summary.ErrorCode;
        if (!string.IsNullOrWhiteSpace(summary.SnapshotPath)) rawFields["SnapshotPath"] = summary.SnapshotPath;
        if (summary.ImageBytesLength is not null) rawFields["ImageBytesLength"] = summary.ImageBytesLength.Value.ToString();

        return new DahuaAccessRecord
        {
            RecNo = source.RecNo,
            CreateTime = summary.EventTime ?? source.CreateTime,
            UserId = string.IsNullOrWhiteSpace(trustedUserId) ? null : trustedUserId,
            CardName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            StatusRaw = string.IsNullOrWhiteSpace(trustedStatus) ? source.StatusRaw : trustedStatus,
            MethodRaw = trustedMethod,
            Type = string.IsNullOrWhiteSpace(trustedDirection) ? source.Type : trustedDirection,
            Url = string.IsNullOrWhiteSpace(source.Url) ? summary.SnapshotPath : source.Url,
            RawFields = rawFields,
        };
    }

    public static bool IsRecognizedAttendance(DahuaAccessRecord record, Worker? resolvedWorker) =>
        DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record)
        && string.Equals(record.StatusRaw, "1", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(record.UserId)
        && !string.IsNullOrWhiteSpace(record.CardName)
        && !HasFailureErrorCode(record);

    public static DahuaAccessRecord BuildUnknownFaceRecord(DahuaAccessRecord source, string rawStructSummaryJson)
    {
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "UnknownFace",
            ["ClassificationReason"] = "Smart Event was not a trusted recognized worker attendance event",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UserID"] = null,
            ["UserId"] = null,
            ["CardName"] = null,
            ["Status"] = "0",
            ["Method"] = "15",
            ["Type"] = string.IsNullOrWhiteSpace(source.Type) ? "Entry" : source.Type,
        };

        return new DahuaAccessRecord
        {
            RecNo = source.RecNo,
            CreateTime = source.CreateTime,
            CardName = null,
            UserId = null,
            StatusRaw = "0",
            MethodRaw = "15",
            Type = string.IsNullOrWhiteSpace(source.Type) ? "Entry" : source.Type,
            Url = source.Url,
            RawFields = rawFields,
        };
    }

    private static bool HasFailureErrorCode(DahuaAccessRecord record)
    {
        var errorCode = record.RawFields.GetValueOrDefault("ErrorCode");
        return !string.IsNullOrWhiteSpace(errorCode) && errorCode != "0";
    }

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return method;
        return method.Equals("face", StringComparison.OrdinalIgnoreCase) ? "15" : method;
    }

    private sealed record TrustedSmartEventSummary(
        string? Status,
        string? UserId,
        string? CardName,
        string? Method,
        string? Direction,
        DateTimeOffset? EventTime,
        string? SnapshotPath,
        string? ErrorCode,
        int? ImageBytesLength)
    {
        public static TrustedSmartEventSummary Parse(string rawStructSummaryJson)
        {
            if (string.IsNullOrWhiteSpace(rawStructSummaryJson))
            {
                return new TrustedSmartEventSummary(null, null, null, null, null, null, null, null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(rawStructSummaryJson);
                var root = document.RootElement;
                return new TrustedSmartEventSummary(
                    GetString(root, "Status"),
                    FirstNotBlank(GetString(root, "UserId"), GetString(root, "UserID")),
                    GetString(root, "CardName"),
                    GetString(root, "Method"),
                    GetString(root, "Direction"),
                    DateTimeOffset.TryParse(GetString(root, "EventTime"), out var eventTime) ? eventTime : null,
                    GetString(root, "SnapshotPath"),
                    GetString(root, "ErrorCode"),
                    GetInt(root, "ImageBytesLength"));
            }
            catch (JsonException)
            {
                return new TrustedSmartEventSummary(null, null, null, null, null, null, null, null, null);
            }
        }

        private static string? GetString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value)) return null;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        private static int? GetInt(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value)) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            return int.TryParse(GetString(root, name), out var parsed) ? parsed : null;
        }
    }
}
