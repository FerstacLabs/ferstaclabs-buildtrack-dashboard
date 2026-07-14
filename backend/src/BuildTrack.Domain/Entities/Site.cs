namespace BuildTrack.Domain.Entities;

public sealed class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Asia/Baku";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Worker> Workers { get; set; } = new List<Worker>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
