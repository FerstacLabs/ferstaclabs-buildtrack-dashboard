using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaSecurityReviewEventPolicy
{
    public static bool IsFaceReviewEvent(DahuaAccessRecord record) =>
        DahuaUnknownFacePolicy.IsUnknownFace(record)
        || IsClassification(record, "ParserUncertainSmartEvent")
        || IsClassification(record, "SuspiciousRecognition")
        || IsClassification(record, "IdentityMismatch")
        || IsClassification(record, "IdentityMappingConflict");

    public static SecurityEventType ResolveEventType(DahuaAccessRecord record)
    {
        var classification = record.RawFields.GetValueOrDefault("Classification");
        if (string.Equals(classification, "IdentityMismatch", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.IdentityMismatch;
        if (string.Equals(classification, "IdentityMappingConflict", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.IdentityMappingConflict;
        if (string.Equals(classification, "SuspiciousRecognition", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.SuspiciousRecognition;
        if (string.Equals(classification, "ParserUncertainSmartEvent", StringComparison.OrdinalIgnoreCase)) return SecurityEventType.ParserUncertainSmartEvent;

        return DahuaUnknownFacePolicy.IsUnknownFace(record)
            ? SecurityEventType.UnknownFace
            : SecurityEventType.SuspiciousRecognition;
    }

    public static string ResolveMessage(SecurityEventType eventType) => eventType switch
    {
        SecurityEventType.UnknownFace => "Taninmayan uz askarlandi",
        SecurityEventType.IdentityMismatch => "Subheli tanima / worker identity mismatch",
        SecurityEventType.IdentityMappingConflict => "Subheli tanima / duplicate camera UserID mapping",
        SecurityEventType.SuspiciousRecognition => "Subheli tanima / worker identity mismatch",
        SecurityEventType.ParserUncertainSmartEvent => "Subheli tanima / worker identity mismatch",
        _ => "Yoxlanilmali uz hadisesi",
    };

    private static bool IsClassification(DahuaAccessRecord record, string classification) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), classification, StringComparison.OrdinalIgnoreCase);
}
