namespace BuildTrack.Domain.Dahua;

public sealed class DahuaSdkAccessEvent
{
    public string? RegisterDeviceId { get; set; }
    public string? UserId { get; set; }
    public string? CardName { get; set; }
    public DateTimeOffset? EventTime { get; set; }
    public string? Status { get; set; }
    public string? Method { get; set; }
    public string? Direction { get; set; }
    public long? RecNo { get; set; }
    public string? SnapshotPath { get; set; }
    public Dictionary<string, string?> RawFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
