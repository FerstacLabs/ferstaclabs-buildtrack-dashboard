using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaUnknownFacePolicy
{
    public static bool IsUnknownFace(DahuaAccessRecord record) =>
        record.NormalizedMethod == BuildTrack.Domain.Entities.AttendanceMethod.Face
        && record.StatusRaw == "0"
        && string.IsNullOrWhiteSpace(record.UserId)
        && string.IsNullOrWhiteSpace(record.CardName)
        && !string.IsNullOrWhiteSpace(record.Url);
}
