using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSecurityReviewEventPolicy
{
    public static bool IsFaceReviewEvent(DahuaAccessRecord record) =>
        DahuaUnknownFacePolicy.IsUnknownFace(record)
        || IsParserUncertain(record)
        || IsSuspiciousRecognition(record)
        || IsIdentityMismatch(record);

    public static SecurityEventType ResolveEventType(DahuaAccessRecord record)
    {
        var classification = record.RawFields.GetValueOrDefault("Classification");
        if (string.Equals(classification, "IdentityMismatch", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.IdentityMismatch;
        if (string.Equals(classification, "SuspiciousRecognition", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.SuspiciousRecognition;
        if (string.Equals(classification, "ParserUncertainSmartEvent", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.ParserUncertainSmartEvent;

        return DahuaUnknownFacePolicy.IsUnknownFace(record)
            ? SecurityEventType.UnknownFace
            : SecurityEventType.SuspiciousRecognition;
    }

    public static string ResolveMessage(SecurityEventType eventType) => eventType switch
    {
        SecurityEventType.UnknownFace => "Tanınmayan üz aşkarlandı",
        SecurityEventType.IdentityMismatch => "Şübhəli üz tanınması / worker identity mismatch",
        SecurityEventType.SuspiciousRecognition => "Şübhəli üz tanınması / worker identity mismatch",
        SecurityEventType.ParserUncertainSmartEvent => "Şübhəli üz tanınması / worker identity mismatch",
        _ => "Yoxlanılmalı üz hadisəsi",
    };

    private static bool IsParserUncertain(DahuaAccessRecord record) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), "ParserUncertainSmartEvent", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuspiciousRecognition(DahuaAccessRecord record) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), "SuspiciousRecognition", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdentityMismatch(DahuaAccessRecord record) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), "IdentityMismatch", StringComparison.OrdinalIgnoreCase);
}
