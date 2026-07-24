using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSmartEventClassification
{
    public static bool IsRecognizedAttendance(DahuaAccessRecord record, Worker? resolvedWorker) =>
        resolvedWorker is not null
        && DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record)
        && string.Equals(record.StatusRaw, "1", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(record.UserId)
        && !string.IsNullOrWhiteSpace(record.CardName)
        && IsSameRecognizedWorker(record, resolvedWorker);

    public static DahuaAccessRecord BuildUnknownFaceRecord(DahuaAccessRecord source, string rawStructSummaryJson)
    {
        var rawFields = new Dictionary<string, string?>(source.RawFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Classification"] = "UnknownFace",
            ["ClassificationReason"] = "Smart Event was not a trusted recognized worker attendance event",
            ["RawStructSummaryJson"] = rawStructSummaryJson,
            ["UserID"] = null,
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

    private static bool IsSameRecognizedWorker(DahuaAccessRecord record, Worker resolvedWorker)
    {
        if (!string.Equals(record.UserId, resolvedWorker.ExternalWorkerCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cardName = NormalizePersonName(record.CardName);
        var workerName = NormalizePersonName(resolvedWorker.FullName);
        return cardName.Length > 0
               && workerName.Length > 0
               && (string.Equals(cardName, workerName, StringComparison.OrdinalIgnoreCase)
                   || workerName.Contains(cardName, StringComparison.OrdinalIgnoreCase)
                   || cardName.Contains(workerName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePersonName(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
