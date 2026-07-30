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
        var cardNameMismatch = workerMatches && resolvedWorker is not null && IsMappedWorkerNameMismatch(trustedCardName, resolvedWorker);

        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["SmartEventTrustedSource"] = "DEV_EVENT_ACCESS_CTL_INFO",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["Status"] = trustedStatus,
            ["UserID"] = trustedUserId,
            ["UserId"] = trustedUserId,
            ["CardName"] = displayName,
            ["TrustedCardName"] = trustedCardName,
            ["ReceivedCardName"] = trustedCardName,
            ["ExpectedWorkerName"] = workerMatches ? resolvedWorker!.FullName : null,
            ["WorkerResolved"] = workerMatches ? "true" : "false",
            ["CardNameMismatch"] = cardNameMismatch ? "true" : "false",
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
        IsRecognizedAttendance(record, resolvedWorker, DahuaIdentityMatchPolicy.Strict);

    public static bool IsRecognizedAttendance(DahuaAccessRecord record, Worker? resolvedWorker, DahuaIdentityMatchPolicy identityPolicy) =>
        IsRecognizedAttendance(record, resolvedWorker, identityPolicy, allowCardNameMismatchAttendance: false);

    public static bool IsRecognizedAttendance(
        DahuaAccessRecord record,
        Worker? resolvedWorker,
        DahuaIdentityMatchPolicy identityPolicy,
        bool allowCardNameMismatchAttendance)
    {
        if (!DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record)) return false;
        if (!string.Equals(record.StatusRaw, "1", StringComparison.OrdinalIgnoreCase)) return false;
        if (!HasResolvedWorkerUserId(record, resolvedWorker)) return false;
        if (HasFailureErrorCode(record)) return false;

        var receivedCardName = GetReceivedCardName(record);
        if (string.IsNullOrWhiteSpace(receivedCardName))
        {
            return identityPolicy == DahuaIdentityMatchPolicy.UserIdPrimary && allowCardNameMismatchAttendance;
        }

        if (HasCompatibleMappedWorkerName(record, resolvedWorker)) return true;

        return identityPolicy == DahuaIdentityMatchPolicy.UserIdPrimary && allowCardNameMismatchAttendance;
    }

    public static bool IsConfirmedUnknownFace(DahuaAccessRecord record) =>
        record.NormalizedMethod == AttendanceMethod.Face
        && !string.IsNullOrWhiteSpace(record.Url)
        && ((record.StatusRaw == "0" && HasFailureErrorCode(record))
            || HasUnknownClassification(record));

    public static DahuaAccessRecord BuildParserUncertainRecord(DahuaAccessRecord source, string rawStructSummaryJson)
    {
        var observedCardName = FirstNotBlank(source.RawFields.GetValueOrDefault("ReceivedCardName"), source.RawFields.GetValueOrDefault("TrustedCardName"), source.CardName);
        var workerResolved = string.Equals(source.RawFields.GetValueOrDefault("WorkerResolutionStatus"), "ResolvedWorker", StringComparison.OrdinalIgnoreCase);
        var expectedWorkerName = workerResolved ? FirstNotBlank(source.RawFields.GetValueOrDefault("ExpectedWorkerName"), source.CardName) : null;
        var safeStatus = string.Equals(source.StatusRaw, "1", StringComparison.OrdinalIgnoreCase) ? source.StatusRaw : null;
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "ParserUncertainSmartEvent",
            ["ClassificationReason"] = "Smart Event image arrived, but access/person fields were inconsistent with the worker mapping or not decoded confidently enough for attendance",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UsedDecodedStringCandidatesForClassification"] = "false",
            ["Status"] = safeStatus,
            ["UserID"] = source.UserId,
            ["UserId"] = source.UserId,
            ["WorkerExternalId"] = source.UserId,
            ["CardName"] = observedCardName,
            ["ExpectedWorkerName"] = expectedWorkerName,
            ["WorkerResolved"] = workerResolved ? "true" : "false",
            ["CardNameMismatch"] = workerResolved && !ArePersonNamesCompatible(observedCardName, expectedWorkerName) ? "true" : "false",
        };

        return new DahuaAccessRecord
        {
            RecNo = source.RecNo,
            CreateTime = source.CreateTime,
            UserId = null,
            CardName = null,
            StatusRaw = safeStatus,
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

    public static DahuaAccessRecord BuildIdentityMismatchRecord(DahuaAccessRecord source, string rawStructSummaryJson)
    {
        var observedCardName = FirstNotBlank(source.RawFields.GetValueOrDefault("ReceivedCardName"), source.RawFields.GetValueOrDefault("TrustedCardName"), source.CardName);
        var expectedWorkerName = FirstNotBlank(source.RawFields.GetValueOrDefault("ExpectedWorkerName"), source.CardName);
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "IdentityMismatch",
            ["ClassificationReason"] = "Status=1 and UserID mapped, but received CardName did not match the expected worker identity",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UserID"] = source.UserId,
            ["UserId"] = source.UserId,
            ["WorkerExternalId"] = source.UserId,
            ["CardName"] = observedCardName,
            ["ReceivedCardName"] = observedCardName,
            ["ExpectedWorkerName"] = expectedWorkerName,
            ["WorkerResolved"] = source.RawFields.GetValueOrDefault("WorkerResolved") ?? "true",
            ["CardNameMismatch"] = "true",
            ["IdentityVerified"] = "false",
            ["IdentityRisk"] = "High",
        };

        return BuildSecurityReviewRecord(source, rawFields);
    }

    public static DahuaAccessRecord BuildIdentityMappingConflictRecord(DahuaAccessRecord source, string rawStructSummaryJson, IReadOnlyCollection<Worker> workers)
    {
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "IdentityMappingConflict",
            ["ClassificationReason"] = "Camera identity maps to multiple active workers for this site",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UserID"] = source.UserId,
            ["UserId"] = source.UserId,
            ["WorkerExternalId"] = source.UserId,
            ["CardName"] = GetReceivedCardName(source),
            ["ReceivedCardName"] = GetReceivedCardName(source),
            ["MappedWorkerCount"] = workers.Count.ToString(),
            ["MappedWorkerNames"] = string.Join(", ", workers.Select(worker => worker.FullName)),
            ["IdentityVerified"] = "false",
            ["IdentityRisk"] = "High",
        };

        return BuildSecurityReviewRecord(source, rawFields);
    }

    public static DahuaAccessRecord BuildCardNamePrimaryRecognizedRecord(
        DahuaAccessRecord source,
        string rawStructSummaryJson,
        Worker resolvedWorker,
        string? rawCameraUserId,
        bool userIdCollision,
        string? originalUserIdMappedWorkerName,
        bool autoProvisioned)
    {
        var trusted = BuildTrustedRecord(source, rawStructSummaryJson, null);
        var receivedCardName = FirstNotBlank(trusted.RawFields.GetValueOrDefault("ReceivedCardName"), trusted.RawFields.GetValueOrDefault("TrustedCardName"), source.CardName);
        var rawFields = new Dictionary<string, string?>(trusted.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "Pending",
            ["ClassificationReason"] = "CardName-primary identity resolution passed attendance rules",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UserID"] = rawCameraUserId,
            ["UserId"] = rawCameraUserId,
            ["CameraUserID"] = rawCameraUserId,
            ["WorkerExternalId"] = resolvedWorker.ExternalWorkerCode,
            ["CardName"] = resolvedWorker.FullName,
            ["ReceivedCardName"] = receivedCardName,
            ["TrustedCardName"] = receivedCardName,
            ["ResolvedWorkerName"] = resolvedWorker.FullName,
            ["ResolvedWorkerExternalId"] = resolvedWorker.ExternalWorkerCode,
            ["ExpectedWorkerName"] = resolvedWorker.FullName,
            ["WorkerResolved"] = "true",
            ["WorkerResolutionStatus"] = "ResolvedWorkerByCardName",
            ["IdentityResolvedBy"] = "CardName",
            ["CardNameMismatch"] = "false",
            ["IdentityVerified"] = "true",
            ["IdentityRisk"] = "Normal",
            ["UserIdCollision"] = userIdCollision ? "true" : "false",
            ["AutoProvisionedWorker"] = autoProvisioned ? "true" : "false",
        };

        if (!string.IsNullOrWhiteSpace(originalUserIdMappedWorkerName))
        {
            rawFields["OriginalUserIdMappedWorkerName"] = originalUserIdMappedWorkerName;
        }

        return new DahuaAccessRecord
        {
            RecNo = trusted.RecNo,
            CreateTime = trusted.CreateTime,
            UserId = resolvedWorker.ExternalWorkerCode,
            CardName = resolvedWorker.FullName,
            StatusRaw = trusted.StatusRaw,
            MethodRaw = trusted.MethodRaw,
            Type = trusted.Type,
            Url = trusted.Url,
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

    private static bool HasResolvedWorkerUserId(DahuaAccessRecord record, Worker? resolvedWorker) =>
        resolvedWorker is not null
        && !string.IsNullOrWhiteSpace(record.UserId)
        && string.Equals(record.UserId, resolvedWorker.ExternalWorkerCode, StringComparison.OrdinalIgnoreCase);

    private static bool HasCompatibleMappedWorkerName(DahuaAccessRecord record, Worker? resolvedWorker)
    {
        if (resolvedWorker is null) return !LooksLikeRandomCandidate(record.CardName);

        var originalCardName = FirstNotBlank(GetReceivedCardName(record), record.CardName);
        return ArePersonNamesCompatible(originalCardName, resolvedWorker.FullName);
    }

    private static DahuaAccessRecord BuildSecurityReviewRecord(DahuaAccessRecord source, Dictionary<string, string?> rawFields) => new()
    {
        RecNo = source.RecNo,
        CreateTime = source.CreateTime,
        UserId = null,
        CardName = null,
        StatusRaw = string.Equals(source.StatusRaw, "1", StringComparison.OrdinalIgnoreCase) ? source.StatusRaw : "0",
        MethodRaw = "15",
        Type = string.IsNullOrWhiteSpace(source.Type) ? "Entry" : source.Type,
        Url = source.Url,
        RawFields = rawFields,
    };

    private static bool IsMappedWorkerNameMismatch(string? receivedCardName, Worker resolvedWorker) =>
        !ArePersonNamesCompatible(receivedCardName, resolvedWorker.FullName);

    private static bool ArePersonNamesCompatible(string? receivedName, string? expectedName)
    {
        var originalCardName = NormalizePersonName(receivedName);
        var workerName = NormalizePersonName(expectedName);
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

    public static void MarkRecognizedAttendance(
        DahuaAccessRecord record,
        Worker? resolvedWorker = null,
        DahuaIdentityMatchPolicy identityPolicy = DahuaIdentityMatchPolicy.Strict,
        bool allowCardNameMismatchAttendance = false)
    {
        record.RawFields["Classification"] = "RecognizedAttendance";
        record.RawFields["ClassificationReason"] = "High-confidence Smart Event access fields passed attendance rules";
        record.RawFields["UsedDecodedStringCandidatesForClassification"] = "false";
        record.RawFields["IdentityMatchPolicy"] = identityPolicy == DahuaIdentityMatchPolicy.UserIdPrimary ? "user_id_primary" : "strict";
        record.RawFields.TryAdd("IdentityResolvedBy", "UserID+CardName");
        if (resolvedWorker is null) return;

        var receivedCardName = GetReceivedCardName(record);
        var cardNameMismatch = IsMappedWorkerNameMismatch(receivedCardName, resolvedWorker);
        record.RawFields["ExpectedWorkerName"] = resolvedWorker.FullName;
        record.RawFields.TryAdd("ResolvedWorkerName", resolvedWorker.FullName);
        record.RawFields.TryAdd("ResolvedWorkerExternalId", resolvedWorker.ExternalWorkerCode);
        record.RawFields["ReceivedCardName"] = receivedCardName;
        record.RawFields["WorkerResolved"] = "true";
        record.RawFields["CardNameMismatch"] = cardNameMismatch ? "true" : "false";
        record.RawFields["IdentityVerified"] = cardNameMismatch ? "false" : "true";
        record.RawFields["IdentityRisk"] = cardNameMismatch ? "High" : "Normal";
        if (identityPolicy == DahuaIdentityMatchPolicy.UserIdPrimary && cardNameMismatch && allowCardNameMismatchAttendance)
        {
            record.RawFields["ClassificationReason"] = "UserID matched canonical worker while Smart Event CardName was inconsistent";
            record.RawFields["UnsafeMismatchAttendanceAllowed"] = "true";
        }
    }

    public static bool HasCardNameMismatch(DahuaAccessRecord record) =>
        string.Equals(record.RawFields.GetValueOrDefault("CardNameMismatch"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool HasHighConfidence(DahuaAccessRecord record, string key) =>
        IsHighConfidence(record.RawFields.GetValueOrDefault(key));

    private static bool IsHighConfidence(string? value) =>
        string.Equals(value, "High", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? GetReceivedCardName(DahuaAccessRecord record) =>
        FirstNotBlank(record.RawFields.GetValueOrDefault("ReceivedCardName"), record.RawFields.GetValueOrDefault("TrustedCardName"));

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
