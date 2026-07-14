using BuildTrack.Domain.Entities;

namespace BuildTrack.Domain.Dahua;

public sealed class DahuaAccessRecord
{
    public long? RecNo { get; set; }
    public DateTimeOffset CreateTime { get; set; } = DateTimeOffset.UtcNow;
    public string? CardName { get; set; }
    public string? UserId { get; set; }
    public string? StatusRaw { get; set; }
    public string? MethodRaw { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string?> RawFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AttendanceEventStatus NormalizedStatus =>
        string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(CardName)
            ? AttendanceEventStatus.Stranger
            : StatusRaw == "1"
                ? AttendanceEventStatus.Ok
                : AttendanceEventStatus.Failed;

    public AttendanceMethod NormalizedMethod => MethodRaw switch
    {
        "15" => AttendanceMethod.Face,
        "1" => AttendanceMethod.Card,
        "2" => AttendanceMethod.Fingerprint,
        "3" => AttendanceMethod.Password,
        _ => AttendanceMethod.Unknown,
    };

    public AttendanceDirection NormalizedDirection => Type?.Equals("Entry", StringComparison.OrdinalIgnoreCase) == true
        ? AttendanceDirection.Entry
        : Type?.Equals("Exit", StringComparison.OrdinalIgnoreCase) == true
            ? AttendanceDirection.Exit
            : AttendanceDirection.Unknown;
}
