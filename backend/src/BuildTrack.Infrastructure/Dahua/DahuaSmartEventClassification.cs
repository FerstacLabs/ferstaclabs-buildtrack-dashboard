using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using System.Text.Json;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSmartEventClassification
{
    public static DahuaAccessRecord BuildTrustedRecord(DahuaAccessRecord source, string rawStructSummaryJson, Worker? resolvedWorker)
    {
        var summary = TrustedSmartEventSummary.Parse(rawStructSummaryJson);
        var sourceUserIdConfidence = source.RawFields.GetValueOrDefault("UserIdConfidence");
        var sourceStatusConfidence = source.RawFields.GetValueOrDefault("StatusConfidence");
        var sourceCardNameConfidence = source.RawFields.GetValueOrDefault("CardNameConfidence");
        var trustedUserId = FirstNotBlank(summary.UserIdIfHighConfidence, source.UserId);
        var trustedStatus = FirstNotBlank(summary.StatusIfHighConfidence, source.StatusRaw);
        var trustedMethod = NormalizeMethod(FirstNotBlank(summary.Method, source.MethodRaw));
        var trustedDirection = FirstNotBlank(summary.Direction, source.Type);
        var trustedCardName = FirstNotBlank(summary.CardNameIfHighConfidence, source.CardName);

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
            ["Classification"] = "Pending",
            ["ClassificationReason"] = "Smart Event classification pending",
            ["UserIdSource"] = FirstNotBlank(summary.UserIdSource, source.RawFields.GetValueOrDefault("UserIdSource"), "Unknown"),
            ["CardNameSource"] = FirstNotBlank(summary.CardNameSource, source.RawFields.GetValueOrDefault("CardNameSource"), "Unknown"),
            ["StatusSource"] = FirstNotBlank(summary.StatusSource, source.RawFields.GetValueOrDefault("StatusSource"), "Unknown"),
            ["UserIdConfidence"] = FirstNotBlank(summary.UserIdConfidence, sourceUserIdConfidence, "Low"),
            ["CardNameConfidence"] = FirstNotBlank(summary.CardNameConfidence, sourceCardNameConfidence, "Low"),
            ["StatusConfidence"] = FirstNotBlank(summary.StatusConfidence, sourceStatusConfidence, "Low"),
            ["UsedDecodedStringCandidatesForClassification"] = "false",
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
        && HasCompatibleMappedWorkerName(record, resolvedWorker)
        && !HasFailureErrorCode(record);

    public static bool IsConfirmedUnknownFace(DahuaAccessRecord record) =>
        record.NormalizedMethod == AttendanceMethod.Face
        && !string.IsNullOrWhiteSpace(record.Url)
        && ((record.StatusRaw == "0" && HasFailureErrorCode(record))
            || HasUnknownClassification(record));

    public static DahuaAccessRecord BuildParserUncertainRecord(DahuaAccessRecord source, string rawStructSummaryJson)
    {
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "ParserUncertainSmartEvent",
            ["ClassificationReason"] = "Smart Event image arrived, but access/person fields were not decoded confidently enough for attendance or confirmed unknown-face classification",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UsedDecodedStringCandidatesForClassification"] = "false",
            ["Status"] = null,
            ["UserID"] = null,
            ["UserId"] = null,
            ["CardName"] = null,
        };

        return new DahuaAccessRecord
        {
            RecNo = source.RecNo,
            CreateTime = source.CreateTime,
            UserId = null,
            CardName = null,
            StatusRaw = null,
            MethodRaw = "15",
            Type = string.IsNullOrWhiteSpace(source.Type) ? "Entry" : source.Type,
            Url = source.Url,
            RawFields = rawFields,
        };
    }

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
            ["UserIdConfidence"] = source.RawFields.GetValueOrDefault("UserIdConfidence") ?? "Low",
            ["CardNameConfidence"] = source.RawFields.GetValueOrDefault("CardNameConfidence") ?? "Low",
            ["StatusConfidence"] = source.RawFields.GetValueOrDefault("StatusConfidence") ?? "Low",
            ["UserIdSource"] = source.RawFields.GetValueOrDefault("UserIdSource") ?? "Unknown",
            ["CardNameSource"] = source.RawFields.GetValueOrDefault("CardNameSource") ?? "Unknown",
            ["StatusSource"] = source.RawFields.GetValueOrDefault("StatusSource") ?? "Unknown",
            ["UsedDecodedStringCandidatesForClassification"] = "false",
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

    private static bool HasUnknownClassification(DahuaAccessRecord record) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), "UnknownFace", StringComparison.OrdinalIgnoreCase);

    private static bool HasCompatibleMappedWorkerName(DahuaAccessRecord record, Worker? resolvedWorker)
    {
        if (resolvedWorker is null) return !LooksLikeRandomCandidate(record.CardName);

        var originalCardName = NormalizePersonName(FirstNotBlank(record.RawFields.GetValueOrDefault("TrustedCardName"), record.CardName));
        var workerName = NormalizePersonName(resolvedWorker.FullName);
        return originalCardName.Length > 0
               && workerName.Length > 0
               && (string.Equals(originalCardName, workerName, StringComparison.OrdinalIgnoreCase)
                   || workerName.Contains(originalCardName, StringComparison.OrdinalIgnoreCase)
                   || originalCardName.Contains(workerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeRandomCandidate(string? value)
    {
        var normalized = NormalizePersonName(value);
        return normalized.Length < 3
               || normalized.StartsWith('*')
               || string.Equals(normalized, "KF", StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkRecognizedAttendance(DahuaAccessRecord record)
    {
        record.RawFields["Classification"] = "RecognizedAttendance";
        record.RawFields["ClassificationReason"] = "High-confidence Smart Event access fields passed attendance rules";
        record.RawFields["UsedDecodedStringCandidatesForClassification"] = "false";
    }

    private static bool HasHighConfidence(DahuaAccessRecord record, string key) =>
        IsHighConfidence(record.RawFields.GetValueOrDefault(key));

    private static bool IsHighConfidence(string? value) =>
        string.Equals(value, "High", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return method;
        return method.Equals("face", StringComparison.OrdinalIgnoreCase) ? "15" : method;
    }

    private static string NormalizePersonName(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

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
        public string? UserIdIfHighConfidence => IsHighConfidence(UserIdConfidence) ? UserId : null;
        public string? CardNameIfHighConfidence => IsHighConfidence(CardNameConfidence) ? CardName : null;
        public string? StatusIfHighConfidence => IsHighConfidence(StatusConfidence) ? Status : null;

        public string? UserIdSource { get; init; }
        public string? CardNameSource { get; init; }
        public string? StatusSource { get; init; }
        public string? UserIdConfidence { get; init; }
        public string? CardNameConfidence { get; init; }
        public string? StatusConfidence { get; init; }

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
                    GetInt(root, "ImageBytesLength"))
                {
                    UserIdSource = GetString(root, "UserIdSource"),
                    CardNameSource = GetString(root, "CardNameSource"),
                    StatusSource = GetString(root, "StatusSource"),
                    UserIdConfidence = GetString(root, "UserIdConfidence"),
                    CardNameConfidence = GetString(root, "CardNameConfidence"),
                    StatusConfidence = GetString(root, "StatusConfidence"),
                };
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
