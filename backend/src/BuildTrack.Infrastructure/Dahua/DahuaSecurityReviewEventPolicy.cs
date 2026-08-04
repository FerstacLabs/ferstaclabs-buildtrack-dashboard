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
        SecurityEventType.UnknownFace => "Tanınmayan üz aşkarlandı",
        SecurityEventType.IdentityMismatch => "Şübhəli tanıma: kamera məlumatı işçi profili ilə uyğun gəlmədi",
        SecurityEventType.IdentityMappingConflict => "Şübhəli tanıma: kamera UserID bir neçə işçi ilə uyğunlaşır",
        SecurityEventType.SuspiciousRecognition => "Şübhəli tanıma: təsdiq üçün yoxlanılmalıdır",
        SecurityEventType.ParserUncertainSmartEvent => "Şübhəli tanıma: kamera məlumatı tam etibarlı oxunmadı",
        _ => "Yoxlanılmalı üz hadisəsi",
    };

    private static bool IsClassification(DahuaAccessRecord record, string classification) =>
        string.Equals(record.RawFields.GetValueOrDefault("Classification"), classification, StringComparison.OrdinalIgnoreCase);
}
