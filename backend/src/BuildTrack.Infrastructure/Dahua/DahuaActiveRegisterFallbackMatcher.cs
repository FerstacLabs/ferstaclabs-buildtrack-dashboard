using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaActiveRegisterFallbackMatcher
{
    public static bool IsSingleDeviceFallbackEnabled(string? value) =>
        value?.Trim() switch
        {
            "true" => true,
            "True" => true,
            "TRUE" => true,
            "1" => true,
            "yes" => true,
            _ => false,
        };

    public static Device? MatchSingleDeviceFallback(IEnumerable<Device> candidates, bool enabled)
    {
        if (!enabled) return null;

        var activeRegisterDevices = candidates
            .Where(device => device.Mode == DeviceMode.ActiveRegister)
            .Take(2)
            .ToList();

        return activeRegisterDevices.Count == 1 ? activeRegisterDevices[0] : null;
    }
}
